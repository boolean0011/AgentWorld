namespace AgentWorld.Scenarios.Bargaining;

public interface IBargainingJudge
{
    Task<BargainingRoundEvaluation> RunAsync(BargainingContext context, CancellationToken cancellationToken = default);
}
