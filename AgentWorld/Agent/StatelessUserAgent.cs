using AgentWorld.Context;
using Microsoft.Extensions.AI;

namespace AgentWorld.Agent;

/// <summary>
/// 表示一个无状态的用户 Agent 实现，无状态是指 Agent 自身不维护状态，状态完全由外部 <see cref="IContext"/> 维护。
/// <para>
/// 该 Agent 封装了与 <see cref="IChatClient"/> 的交互，能够根据任务提示词提供程序生成提示词，
/// 并且支持可选的审核 Agent (<see cref="ICriticAgent"/>) 对输出结果进行多轮合规校验与修正。
/// </para>
/// </summary>
/// <typeparam name="TContext">场景上下文类型，必须实现 <see cref="IContext"/>。</typeparam>
/// <param name="chatClient">底层使用的 AI 聊天客户端。</param>
/// <param name="instructions">Agent 的系统指令。</param>
/// <param name="name">Agent 的名称。</param>
/// <param name="description">Agent 的角色或职责描述。</param>
/// <param name="promptProvider">根据上下文动态生成当前轮次用户提示词的委托函数。</param>
/// <param name="criticAgent">可选的输出合规性审核 Critic Agent。</param>
/// <param name="criticFallbackContent">可选的在审核多次失败时的默认兜底回复文本。</param>
/// <param name="maxCriticAttempts">Critic 审核的最大重试次数，默认为 3 次。</param>
public class StatelessUserAgent<TContext>(
    IChatClient chatClient,
    string instructions,
    string name,
    string description,
    Func<TContext, string> promptProvider,
    ICriticAgent? criticAgent = null,
    string? criticFallbackContent = null,
    int maxCriticAttempts = 3) : IUserAgent<TContext>
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
    private readonly Func<TContext, string> _promptProvider = promptProvider;

    /// <summary>
    /// 可选的输出守卫反思 Agent。
    /// </summary>
    private readonly ICriticAgent? _criticAgent = criticAgent;

    /// <summary>
    /// 反思校验失败时的兜底回复文本。
    /// </summary>
    private readonly string _criticFallbackContent = criticFallbackContent ?? "抱歉，我刚才有点走神了，没听清您说什么。";

    /// <summary>
    /// Critic 审核最大重试次数。
    /// </summary>
    private readonly int _maxCriticAttempts = maxCriticAttempts;

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
        var prompt = _promptProvider(context);
        List<ChatMessage> messages = [.. context.ChatHistory.GetHistory(), new ChatMessage(ChatRole.User, prompt)];

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
            for (var i = 0; i < _maxCriticAttempts; i++)
            {
                var checkResult = await _criticAgent.RunAsync(context, content, cancellationToken);
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

        context.ChatHistory.Append(
            new ChatMessage(ChatRole.User, $"现在是**{Name}**发言: {content}")
            {
                AuthorName = Name
            });

        return content;
    }
}
