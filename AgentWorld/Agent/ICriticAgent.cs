using AgentWorld.Context;

namespace AgentWorld.Agent;

public interface ICriticAgent
{
    Task<CriticResult> RunAsync(string content, IContext context, CancellationToken cancellationToken = default);

    int MaxCount { set; get; }
}
