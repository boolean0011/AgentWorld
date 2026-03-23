namespace AgentWorld.Scenarios.Bargaining;

/// <summary>砍价谈判的结局状态。</summary>
public enum BargainingRoundEvaluation
{
    /// <summary>谈判进行中，尚未结束。</summary>
    Ongoing,

    /// <summary>买卖双方达成交易。</summary>
    Agreed,

    /// <summary>谈判破裂，店员下达逐客令。</summary>
    Broken
}
