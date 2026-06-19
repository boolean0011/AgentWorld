using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentWorld.Router;

/// <summary>
/// 基于大模型的意图路由代理。
/// 通过与用户多轮对话识别用户意图，并在意图明确时将对应任务转发到下游的专门执行器。
/// </summary>
/// <remarks>
/// 工作原理：将每条 <see cref="Route"/> 的 <c>TriggerCondition</c> 注册为大模型的 Function Tool，
/// 由大模型自动判断用户意图与哪个 Tool 匹配。若匹配成功则返回 <see cref="RouteSelected"/>，
/// 若意图不明确则返回 <see cref="NoRouteSelected"/> 继续与用户对话。
/// </remarks>
public class RouterAgent
{
    /// <summary>底层 AI 代理实例，负责驱动与大模型的多轮对话。</summary>
    private readonly AIAgent _agent;

    /// <summary>对话历史提供器，用于在多轮对话中维护上下文。</summary>
    private readonly ChatHistoryProvider _chatHistoryProvider;

    /// <summary>Function 名称到路由规则的映射，用于在大模型调用 Function 后快速定位目标路由。</summary>
    private readonly Dictionary<string, Route> _routes;

    /// <summary>
    /// 默认的路由 Agent 系统指令，用于指导大模型扮演前台路由助手角色。
    /// 可在构造时通过 <c>instructions</c> 参数覆盖。
    /// </summary>
    private const string DefaultInstructions = """
        # Role: 前台路由助手

        # Goal
        你的主要任务是判断用户意图是否匹配你提供的 Tools。

        # Rules
        - 如果用户意图匹配某个 Tool，你必须调用对应的 Tool。
        - 调用 Tool 时必须填写 task 参数，用简短明确的话描述下游代理需要完成的任务。
        - task 参数必须包含用户已经给出的关键事实，也要说明仍然缺少哪些必要信息。
        - 如果用户意图不匹配任何 Tool，就不要调用工具，直接陪用户闲聊，用自然语言回复即可。
        - 每次回复最多只能选择一次工具调用，不要同时调用多个工具。
        """;

    /// <summary>
    /// 初始化 <see cref="RouterAgent"/> 的新实例。
    /// </summary>
    /// <param name="chatClient">用于驱动大模型对话的聊天客户端。</param>
    /// <param name="routes">路由规则列表，每条规则的 <c>TriggerCondition</c> 将被注册为大模型可调用的 Function Tool。</param>
    /// <param name="instructions">可选的自定义系统指令，若为 <see langword="null"/> 或空白则使用默认指令。</param>
    /// <param name="chatHistoryProvider">可选的对话历史提供器，默认使用内存实现。</param>
    public RouterAgent(IChatClient chatClient, IReadOnlyList<Route> routes, string? instructions = null, ChatHistoryProvider? chatHistoryProvider = null)
    {
        var (routesDict, tools) = CreateRouteTools(routes);
        _routes = routesDict;
        _chatHistoryProvider = chatHistoryProvider ?? new InMemoryChatHistoryProvider();

        _agent = chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "RouterAgent",
                Description = "负责根据用户意图决定是否路由到下游专门代理。",
                ChatHistoryProvider = _chatHistoryProvider,
                UseProvidedChatClientAsIs = true, // 禁止框架自动调用function tools
                ChatOptions = new ChatOptions
                {
                    Instructions = string.IsNullOrWhiteSpace(instructions) ? DefaultInstructions : instructions,
                    AllowMultipleToolCalls = false,
                    Tools = tools
                }
            });
    }

    /// <summary>
    /// 异步创建一个新的多轮对话会话。
    /// 会话用于在连续的 <see cref="RunAsync"/> 调用之间保持对话上下文。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新建的 <see cref="AgentSession"/>，若底层不支持会话则为 <see langword="null"/>。</returns>
    public async ValueTask<AgentSession?> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        return await _agent.CreateSessionAsync(cancellationToken);
    }

    /// <summary>
    /// 异步处理一轮用户输入，判断用户意图并返回路由决策结果。
    /// </summary>
    /// <param name="messsage">当前轮次的用户输入消息。</param>
    /// <param name="session">当前多轮对话的会话上下文，首轮传入 <see langword="null"/> 时将自动创建。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description><see cref="RouteSelected"/>：大模型识别到用户意图，包含提炼出的任务描述和匹配的路由规则。</description></item>
    ///   <item><description><see cref="NoRouteSelected"/>：意图尚不明确，包含大模型的继续对话回复文本。</description></item>
    /// </list>
    /// </returns>
    public async Task<RouterResponse> RunAsync(string messsage, AgentSession? session, CancellationToken cancellationToken = default)
    {
        var response = await _agent.RunAsync(new ChatMessage(ChatRole.User, messsage), session, cancellationToken: cancellationToken);

        // 如果返回结果中有 functionCall，说明识别了用户意图，这时路由到下游代理，否则，继续与用户对话进一步识别用户意图。
        var functionCall = response.Messages
            .SelectMany(static message => message.Contents)
            .OfType<FunctionCallContent>()
            .FirstOrDefault();

        if (functionCall is not null)
        {
            return functionCall.Name switch
            {
                var name when _routes.ContainsKey(name) => HandoffTask(functionCall),
                _ => throw new InvalidOperationException($"Unsupported function call: {functionCall.Name}")
            };
        }

        return new NoRouteSelected(response.Text);
    }

    /// <summary>
    /// 从大模型的 Function Call 结果中提取任务描述，并构建 <see cref="RouteSelected"/> 路由响应。
    /// </summary>
    /// <param name="functionCall">大模型触发的 Function Call 内容，其中包含 <c>task</c> 参数。</param>
    /// <returns>包含任务描述和目标路由规则的 <see cref="RouteSelected"/> 实例。</returns>
    /// <exception cref="InvalidOperationException">当 Function Call 缺少必需的 <c>task</c> 参数时抛出。</exception>
    private RouteSelected HandoffTask(FunctionCallContent functionCall)
    {
        var agentName = functionCall.Name;

        if (functionCall.Arguments is not null
            && functionCall.Arguments.TryGetValue("task", out var task)
            && task?.ToString() is { } taskText
            && !string.IsNullOrWhiteSpace(taskText))
        {
            return new RouteSelected(taskText, _routes[agentName]);
        }

        throw new InvalidOperationException(
            $"Route function '{functionCall.Name}' did not provide required task argument.");
    }

    /// <summary>
    /// 根据路由规则列表，为每条规则创建对应的 <see cref="AITool"/> Function Tool，
    /// 并建立 Function 名称到路由规则的映射字典。
    /// </summary>
    /// <param name="routes">路由规则列表。</param>
    /// <returns>路由字典（Function 名 → 路由规则）和供大模型调用的 Tool 列表的元组。</returns>
    private static (Dictionary<string, Route> Routes, List<AITool> Tools) CreateRouteTools(IReadOnlyList<Route> routes)
    {
        var routesDict = new Dictionary<string, Route>(routes.Count);
        var tools = new List<AITool>(routes.Count);

        for (var index = 0; index < routes.Count; index++)
        {
            var route = routes[index];
            var name = $"RouteFunc{index + 1}";
            var function = AIFunctionFactory.Create(
                FunctionCallDummy,
                new AIFunctionFactoryOptions
                {
                    Name = name,
                    Description = route.TriggerCondition
                });

            routesDict.Add(name, route);
            tools.Add(function);
        }

        return (routesDict, tools);
    }

    /// <summary>
    /// 占位 Function，仅用于向大模型暴露路由 Tool 的元数据（名称与描述），
    /// 实际不执行任何业务逻辑。大模型调用此 Function 即表示命中了对应路由。
    /// </summary>
    /// <param name="task">大模型提炼并传入的下游任务描述。</param>
    /// <returns>固定返回 <c>"Route selected."</c>，此返回值不会被实际使用。</returns>
    private static string FunctionCallDummy(
        [Description("传递给下游专门代理的任务描述。需要包含用户已经表达的目标、关键事实，以及下游还需要补齐的问题。")]
        string task) => "Route selected.";
}

