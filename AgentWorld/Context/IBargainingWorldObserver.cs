namespace AgentWorld.Context;

public interface IWorldObserver<TContext, TWorldObservation>
{
    Task<TWorldObservation> RunAsync(TContext context);
}
