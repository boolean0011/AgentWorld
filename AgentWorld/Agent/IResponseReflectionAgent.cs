using AgentWorld.Context;

namespace AgentWorld.Agent;

public interface IResponseReflectionAgent
{
    Task<OutputCheckResult> RunAsync(
        string content,
        IContext context,
        CancellationToken cancellationToken = default);

    int MaxCount { set; get; }
}
