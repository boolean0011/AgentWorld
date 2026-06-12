namespace AgentWorld.Scenarios.Bargaining;

public interface IBargainingJudgeAgent<TEvaluation, TContext>
{
    Task<TEvaluation> RunAsync(TContext context, CancellationToken cancellationToken = default);
}
