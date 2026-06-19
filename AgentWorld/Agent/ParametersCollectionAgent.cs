using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentWorld.Agent;

/// <summary>
/// 参数收集 Agent，专用于根据指定的工具定义（<see cref="AIFunction"/>）与用户进行对话，
/// 逐步收集所需的必要参数，并在所有参数收集完毕后自动调用该工具。
/// </summary>
/// <typeparam name="T">收集参数完成并调用工具后返回的结果类型。</typeparam>
public sealed class ParametersCollectionAgent<T>
{
    private readonly AIAgent _agent;

    private readonly AIFunction _collectParametersFunction;

    private const string DefaultInstructions = """
        # Role: 任务参数收集助手

        # Goals
        你的唯一任务是收集执行当前任务所需的必要参数，并在参数齐全后调用提供的工具。

        # Constraints & Rules
        - **强制参数校验**：仅当工具所需的全部参数都已明确时，才可调用工具。
        - **禁止预设/猜测**：严禁在参数不全时私自调用工具或猜测用户未明确提供的参数值。
        - **沉默原则**：当参数齐全并成功触发工具后，**严禁**输出任何文本回复，直接发送 Tool Call 即可。
        - **自然交互**：在参数不全需要询问时，保持礼貌且像真人一样的口吻，不要显得机械。

        ## Execution Workflow
        - **分析任务描述和后续对话**：根据工具参数定义，提取调用工具所需的全部参数。
        - **分支决策**：
            - **[情况 A：参数缺失]**：向用户发起补齐询问。
            - **[情况 B：参数齐全]**：立即调用提供的工具。**（此分支下禁止任何回复文本）**
        """;

    /// <summary>
    /// 初始化 <see cref="ParametersCollectionAgent{T}"/> 类的新实例。
    /// </summary>
    /// <param name="chatClient">底层使用的 AI 聊天客户端。</param>
    /// <param name="collectParametersFunction">用于定义需要收集哪些参数并执行最终逻辑的工具函数。</param>
    /// <param name="instructions">可选的系统提示词/指令。如果不提供，将使用默认的参数收集规则提示词。</param>
    /// <param name="chatHistoryProvider">可选的对话历史存储提供程序。如果不提供，默认使用 <see cref="InMemoryChatHistoryProvider"/>。</param>
    public ParametersCollectionAgent(
        IChatClient chatClient, 
        AIFunction collectParametersFunction, 
        string? instructions = null, 
        ChatHistoryProvider? chatHistoryProvider = null)
    {
        _agent = chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "ParametersCollectionAgent",
                Description = "负责收集参数",
                ChatHistoryProvider = chatHistoryProvider ?? new InMemoryChatHistoryProvider(),
                UseProvidedChatClientAsIs = true, // 不自动调用tools
                ChatOptions = new ChatOptions
                {
                    Instructions = string.IsNullOrWhiteSpace(instructions) ? DefaultInstructions : instructions,
                    AllowMultipleToolCalls = false,
                    Tools = [collectParametersFunction.AsDeclarationOnly()]
                }
            });

        _collectParametersFunction = collectParametersFunction;
    }

    /// <summary>
    /// 异步创建一个新的 Agent 会话，用于维持当前的对话状态。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>创建的 <see cref="AgentSession"/> 实例，若不支持会话则返回 <c>null</c>。</returns>
    public async ValueTask<AgentSession?> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        return await _agent.CreateSessionAsync(cancellationToken);
    }

    /// <summary>
    /// 异步运行参数收集流程，输入用户消息，返回当前的收集状态结果。
    /// </summary>
    /// <param name="message">用户输入的消息内容。</param>
    /// <param name="session">当前的对话会话上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>
    /// 返回参数收集结果 <see cref="ParameterCollectionResult"/>：
    /// <list type="bullet">
    ///   <item><description>如果参数尚不齐全，返回 <see cref="IncompleteParameterCollection"/>，其中包含询问用户的文本信息。</description></item>
    ///   <item><description>如果参数已齐全并成功触发工具，返回 <see cref="ParameterCollectionSuccess{T}"/>，其中包含工具执行的强类型结果。</description></item>
    /// </list>
    /// </returns>
    public async Task<ParameterCollectionResult> RunAsync(string message, AgentSession? session, CancellationToken cancellationToken = default)
    {
        var response = await _agent.RunAsync(
            message: new ChatMessage(ChatRole.User, message),
            session: session,
            cancellationToken: cancellationToken);

        return await ParseAndInvokeAsync(response, cancellationToken);
    }

    private async Task<ParameterCollectionResult> ParseAndInvokeAsync(AgentResponse response, CancellationToken cancellationToken)
    {
        var functionCall = response.Messages
            .SelectMany(static message => message.Contents)
            .OfType<FunctionCallContent>()
            .LastOrDefault();

        if (functionCall is null)
        {
            return new IncompleteParameterCollection(response.Text);
        }

        // 调用tools返回收集参数结果
        var result = await _collectParametersFunction.InvokeAsync(new AIFunctionArguments(functionCall.Arguments), cancellationToken);
        var convertResult = result switch
        {
            T typedResult => typedResult,
            JsonElement element => element.Deserialize<T>(_collectParametersFunction.JsonSerializerOptions)
                ?? throw new InvalidOperationException("Function returned null."),
            _ => throw new InvalidOperationException(
                $"Unexpected function result type: {result?.GetType().Name ?? "null"}")
        };

        return new ParameterCollectionSuccess<T>(convertResult);
    }
}
