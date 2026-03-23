using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentWorld.Core.Router;


public class RouterAgent
{
    private readonly HashSet<string> _dispatchFunctionNames;
    private readonly InMemoryChatHistoryProvider _chatHistoryProvider;
    private readonly AIAgent _agent;
    private AgentSession? _session;

    private const string DefaultInstructions = """
        # Role: 前台路由助手

        # Goal
        你的主要任务是判断用户意图是否匹配你提供的 Tools。

        # Rules
        - 如果用户意图匹配某个 Tool，你必须调用对应的 Tool。
        - 如果用户意图不匹配任何 Tool，就不要调用工具，直接陪用户闲聊，用自然语言回复即可。
        - 每次回复最多只能选择一次工具调用，不要同时调用多个工具。
        """;

    public RouterAgent(IChatClient chatClient, IReadOnlyList<Func<string>> intentFuncs, string? instructions = null)
    {
        _dispatchFunctionNames = [.. intentFuncs.Select(static func => func.Method.Name)];
        _chatHistoryProvider = new InMemoryChatHistoryProvider();
        _agent = chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "RouterAgent",
                Description = "负责根据用户意图决定是否把会话路由给下游专门代理。",
                ChatHistoryProvider = _chatHistoryProvider,
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = string.IsNullOrWhiteSpace(instructions) ? DefaultInstructions : instructions,
                    AllowMultipleToolCalls = false,
                    Tools = [.. intentFuncs.Select(static func => AIFunctionFactory.Create(func))]
                }
            });
    }

    public async Task<RouterAgentResult> RunAsync(string userInput, CancellationToken cancellationToken = default)
    {
        _session ??= await _agent.CreateSessionAsync(cancellationToken);
        var response = await _agent.RunAsync(
            message: new ChatMessage(ChatRole.User, userInput),
            session: _session,
            cancellationToken: cancellationToken);

        var functionCall = response.Messages
            .SelectMany(static message => message.Contents)
            .OfType<FunctionCallContent>()
            .FirstOrDefault();

        if (functionCall is not null)
        {
            return functionCall.Name switch
            {
                var name when _dispatchFunctionNames.Contains(name) => CreateDispatchResult(name),
                _ => throw new InvalidOperationException($"Unsupported function call: {functionCall.Name}")
            };
        }

        return new RouterAgentDirectReply(response.Text);
    }

    private RouterAgentDispatch CreateDispatchResult(string agentName)
    {
        var context = GetDispatchContext();
        _session = null;

        return new RouterAgentDispatch(agentName, context);
    }

    private IReadOnlyList<ChatMessage> GetDispatchContext() // todo
    {
        if (_session is null)
        {
            return [];
        }

        return
        [
            .. _chatHistoryProvider
                .GetMessages(_session)
                .Where(static message => !message.Contents.OfType<FunctionCallContent>().Any())
        ];
    }
}
