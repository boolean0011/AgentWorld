using AgentWorld.Context;

namespace AgentWorld.Scenarios.Bargaining;

/// <summary>
/// 砍价场景的世界状态，在每一轮对话中由 Orchestrator 维护并传递给所有 Agent。
/// </summary>
public class BargainingContext : IContext
{
    /// <summary>目标商品名称，由 SkillAgent 在启动前填充</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>买方心理最低价位预期，由 SkillAgent 在启动前填充</summary>
    public decimal TargetPrice { get; set; }

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

    /// <summary>砍价过程中感知到的环境信息</summary>
    public string Observation { get; set; } = string.Empty;

    /// <summary>当前的砍价生命周期阶段。</summary>
    public BargainingStage Phase { get; set; } = BargainingStage.NotStarted;

    /// <summary>
    /// 当前砍价结局，null 表示谈判仍在进行中。
    /// </summary>
    public BargainingResult? Result { get; set; } = null;

    /// <summary>全局对话历史提供器，由调用方在初始化时注入，可替换为不同的存储策略。</summary>
    public required IConversationHistoryProvider ConversationHistory { get; init; }

    /// <summary>动态属性包，允许注入或存放任意自定义的附加上下文数据。</summary>
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
}
