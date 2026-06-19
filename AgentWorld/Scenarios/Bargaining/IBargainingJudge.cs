namespace AgentWorld.Scenarios.Bargaining;

/// <summary>
/// 定义砍价场景裁判代理的契约。
/// 裁判负责在每轮对话结束后，基于完整的对话历史对本轮互动进行客观评估，
/// 并返回耐心值变化、好感度变化及当前谈判结局（如有）。
/// </summary>
public interface IBargainingJudge
{
    /// <summary>
    /// 异步评估最新一轮的砍价对话，返回裁判的结构化评分结果。
    /// </summary>
    /// <param name="context">当前砍价上下文，包含完整对话历史及场景状态信息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本轮的裁判评估结果 <see cref="BargainingRoundEvaluation"/>，包含状态变化量及谈判结局。</returns>
    Task<BargainingRoundEvaluation> RunAsync(BargainingContext context, CancellationToken cancellationToken = default);
}
