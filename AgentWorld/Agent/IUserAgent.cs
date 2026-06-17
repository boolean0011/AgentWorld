using AgentWorld.Context;

namespace AgentWorld.Agent;

public interface IUserAgent<TContext>
    where TContext : IContext
{
    string Name { get; }

    string Description { get; }

    Task<string> RunAsync(TContext context, CancellationToken cancellationToken = default);
}
