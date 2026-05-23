namespace AgentWorld.Scenarios.Bargaining;

public interface IBargainingJudgeAgent
{
    Task<BargainingEvaluationResult> RunAsync(
        BargainingContext state,
        CancellationToken cancellationToken = default);
}
