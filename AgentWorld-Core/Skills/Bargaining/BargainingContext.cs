using AgentWorld.Core.Context;
using Microsoft.Extensions.AI;

namespace AgentWorld.Skills.Bargaining;

/// <summary>
/// 砍价场景的世界状态，在每一轮对话中由 Orchestrator 维护并传递给所有 Agent。
/// </summary>
public class BargainingContext : IContext
{
    /// <summary>当前回合数（从 1 开始）。</summary>
    public int CurrentRound { get; set; } = 0;

    /// <summary>最大回合数，由 Orchestrator 在开始时写入。</summary>
    public int MaxRounds { get; set; }

    /// <summary>
    /// 店员的耐心值（0~100）。
    /// 初始值 50，买方无礼/出价过低会下降；降至 0 则触发逐客令。
    /// </summary>
    public int Patience { get; set; } = 50;

    /// <summary>
    /// 店员对顾客的好感度（0~100）。
    /// 初始值 50，顾客嘴甜/投其所好会上升；好感度高时可能获得更低折扣。
    /// </summary>
    public int Affection { get; set; } = 50;

    /// <summary>
    /// 当前砍价结局状态。
    /// Ongoing = 进行中，Agreed = 已达成交易，Broken = 谈判已破裂。
    /// </summary>
    public BargainingRoundEvaluation Result { get; set; } = BargainingRoundEvaluation.Ongoing;

    /// <summary>全局对话历史，按时间顺序追加，所有 Agent 均可读取。</summary>
    public List<ChatMessage> ConversationHistory { get; } = [];

    /// <summary>当前的砍价生命周期阶段。</summary>
    public BargainingPhase Phase { get; set; } = BargainingPhase.NotStarted;

    /// <summary>世界观察者 Agent 对当前环境的描述文本。</summary>
    public string Observation { get; set; } = string.Empty;
}
