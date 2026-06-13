namespace AgentWorld.Scenarios.Bargaining;

public interface IBargainingJudgeAgent<TContext, TEvaluation>
{
    Task<TEvaluation> RunAsync(TContext context, CancellationToken cancellationToken = default);
}
