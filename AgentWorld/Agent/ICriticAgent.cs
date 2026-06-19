using AgentWorld.Context;

namespace AgentWorld.Agent;

/// <summary>
/// 定义 Critic Agent 的审核行为契约。
/// <para>
/// Critic Agent 负责对其他 Agent 生成的输出内容进行合规性审查，
/// 返回 <see cref="CriticResult"/> 以指示内容是否有效，以及不合规时的原因。
/// </para>
/// </summary>
public interface ICriticAgent
{
    /// <summary>
    /// 对指定内容进行审核，结合当前对话上下文判断其是否符合规则。
    /// </summary>
    /// <param name="context">当前场景的上下文，包含对话历史等信息。</param>
    /// <param name="content">待审核的文本内容（通常为某个 Agent 生成的回复草稿）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>
    /// 审核结果 <see cref="CriticResult"/>：
    /// <list type="bullet">
    ///   <item><description><c>IsValid = true</c>：内容合规，可以放行。</description></item>
    ///   <item><description><c>IsValid = false</c>：内容违规，<c>Reason</c> 中包含具体原因。</description></item>
    /// </list>
    /// </returns>
    Task<CriticResult> RunAsync(IContext context, string content, CancellationToken cancellationToken = default);
}
