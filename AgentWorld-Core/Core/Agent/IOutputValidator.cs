using AgentWorld.Core.Context;

namespace AgentWorld.Core.Agent;

public interface IOutputValidatorAgent
{
    Task<OutputCheckResult> RunAsync(string content, IContext context);

    int MaxReflections { set; get; }

    string FailureFallbackContent { get; }
}
