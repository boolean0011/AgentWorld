using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentWorld.Agent;

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

    public ParametersCollectionAgent(
        IChatClient chatClient,
        AIFunction collectParametersFunction,
        string? instructions = null,
        ChatHistoryProvider? chatHistoryProvider = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(collectParametersFunction);

        _agent = chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "ParameterCollectionAgent",
                Description = "负责收集参数，并在参数齐全后触发执行。",
                ChatHistoryProvider = chatHistoryProvider ?? new InMemoryChatHistoryProvider(),
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = string.IsNullOrWhiteSpace(instructions) ? DefaultInstructions : instructions,
                    AllowMultipleToolCalls = false,
                    Tools = [collectParametersFunction.AsDeclarationOnly()]
                }
            });

        _collectParametersFunction = collectParametersFunction;
    }

    public async ValueTask<AgentSession?> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        return await _agent.CreateSessionAsync(cancellationToken);
    }

    public async Task<ParametersCollectionResponse> RunAsync(string message, AgentSession? session, CancellationToken cancellationToken = default)
    {
        var response = await _agent.RunAsync(
            message: new ChatMessage(ChatRole.User, message),
            session: session,
            cancellationToken: cancellationToken);

        return await ParseAndInvokeAsync(response, cancellationToken);
    }

    private async Task<ParametersCollectionResponse> ParseAndInvokeAsync(AgentResponse response, CancellationToken cancellationToken)
    {
        var functionCall = response.Messages
            .SelectMany(static message => message.Contents)
            .OfType<FunctionCallContent>()
            .LastOrDefault();

        if (functionCall is null)
        {
            return new ParametersCollectionMessage(response.Text);
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

        return new ParametersCollected<T>(convertResult);
    }
}
