namespace AgentWorld.Context;

public interface IWorldObserver<TContext, TOutput>
{
    Task<TOutput> RunAsync(TContext context);
}
