using AgentWorld.Context;
using Microsoft.Extensions.AI;

namespace AgentWorld.Agent;

public class StatelessUserAgent<TContext>(
    IChatClient chatClient,
    string instructions,
    string name,
    string description,
    Func<TContext, string> taskPromptProvider,
    ICriticAgent? criticAgent = null,
    string? criticFallbackContent = null) : IUserAgent<TContext>
    where TContext : IContext
{
    /// <summary>
    /// 底层使用的 AI 聊天客户端。
    /// </summary>
    private readonly IChatClient _chatClient = chatClient;

    /// <summary>
    /// Agent 的系统指令。
    /// </summary>
    private readonly string _instructions = instructions;

    /// <summary>
    /// 生成提示词的提供程序。
    /// </summary>
    private readonly Func<TContext, string> _taskPromptProvider = taskPromptProvider;

    /// <summary>
    /// 可选的输出守卫反思 Agent。
    /// </summary>
    private readonly ICriticAgent? _criticAgent = criticAgent;

    /// <summary>
    /// 反思校验失败时的兜底回复文本。
    /// </summary>
    private readonly string _criticFallbackContent = criticFallbackContent ?? "抱歉，我刚才有点走神了，没听清您说什么。";

    /// <summary>
    /// Agent 名称。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Agent 描述。
    /// </summary>
    public string Description { get; } = description;


    /// <summary>
    /// 异步运行该 Agent，根据上下文中的历史记录和 promptProvider 生成下一轮回复。
    /// </summary>
    /// <param name="context">当前对话场景的上下文状态。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Agent 生成的最终回复文本。</returns>
    public async Task<string> RunAsync(TContext context, CancellationToken cancellationToken = default)
    {
        // 根据世界事件和当前状态生成 prompt
        var prompt = _taskPromptProvider(context);
        List<ChatMessage> messages = [.. context.ConversationHistory, new ChatMessage(ChatRole.User, prompt)]; //todo

        var response = await _chatClient.GetResponseAsync(
            messages,
            new ChatOptions
            {
                Instructions = _instructions
            },
            cancellationToken: cancellationToken);

        var content = response.Text ?? string.Empty;

        if (_criticAgent is not null)
        {
            var success = false;
            for (var i = 0; i < _criticAgent.MaxCount; i++)
            {
                var checkResult = await _criticAgent.RunAsync(content, context, cancellationToken);
                if (checkResult.IsValid)
                {
                    success = true;
                    break;
                }

                // Console.WriteLine($"[{Name} 反思] 检测到输出不符合要求：{checkResult.Reason}，正在要求重新生成...");

                var criticPrompt = $"[系统提示] 你刚才的回复违反了设定要求。原因：{checkResult.Reason}。请反思并重新生成一段完整回复，直接输出修改后的回复内容，不要为你的错误道歉。";

                messages.Add(new ChatMessage(ChatRole.Assistant, content));
                messages.Add(new ChatMessage(ChatRole.User, criticPrompt));

                var retryResponse = await _chatClient.GetResponseAsync(
                    messages,
                    new ChatOptions
                    {
                        Instructions = _instructions
                    },
                    cancellationToken: cancellationToken);
                content = retryResponse.Text ?? string.Empty;
            }

            if (!success)
            {
                // Console.WriteLine($"[{Name} 最终失败] 无法生成合规回复，使用默认兜底内容。");
                content = _criticFallbackContent;
            }
        }

        context.ConversationHistory.Add(
            new ChatMessage(ChatRole.User, $"现在是**{Name}**发言: {content}")
            {
                AuthorName = Name
            });

        return content;
    }
}
