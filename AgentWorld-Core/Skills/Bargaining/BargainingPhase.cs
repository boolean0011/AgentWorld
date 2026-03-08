namespace AgentWorld.Skills.Bargaining;

/// <summary>
/// 砍价的生命周期阶段，用于指示当前处于互动流程的哪一部分，
/// 并驱动 Agent 生成处于对应阶段的 Prompt。
/// </summary>
public enum BargainingPhase
{
    NotStarted,
    Start,          // 开场欢迎/提出意向
    Ongoing,        // 正常砍价拉锯阶段
    FinalPush,      // 最后冲刺阶段（即将结束）
    Agreed,         // 达成交易，寒暄道别
    Broken,         // 谈判破裂，下达逐客令
    Timeout         // 轮次耗尽，未达成交易自然结束
}
