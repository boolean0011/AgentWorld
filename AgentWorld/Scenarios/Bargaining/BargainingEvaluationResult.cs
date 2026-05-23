namespace AgentWorld.Scenarios.Bargaining;

/// <summary>裁判 Agent 对一轮对话的评估结果。</summary>
public record BargainingEvaluationResult
{
    /// <summary>本轮判定的谈判结局。</summary>
    public BargainingRoundEvaluation Result { get; init; } = BargainingRoundEvaluation.Ongoing;

    /// <summary>店员耐心值变化量（负数表示下降）。</summary>
    public int PatienceDelta { get; init; }

    /// <summary>店员好感度变化量（正数表示上升）。</summary>
    public int AffectionDelta { get; init; }

    /// <summary>评估原因说明。</summary>
    public string Reason { get; init; } = string.Empty;
}
