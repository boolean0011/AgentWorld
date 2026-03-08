using AgentWorld.Core.Context;

namespace AgentWorld.Core.Agent;

public interface IResponseReflectionAgent
{
    Task<OutputCheckResult> RunAsync(string content, IContext context);

    int MaxReflections { set; get; }
}
