using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentWorld.Router;

public class RouterAgent
{
    private readonly AIAgent _agent;

    private readonly ChatHistoryProvider _chatHistoryProvider;

    private readonly Dictionary<string, Route> _routes;

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

    public async ValueTask<AgentSession?> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        return await _agent.CreateSessionAsync(cancellationToken);
    }

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

    private static string FunctionCallDummy(
        [Description("传递给下游专门代理的任务描述。需要包含用户已经表达的目标、关键事实，以及下游还需要补齐的问题。")]
        string task) => "Route selected.";
}
