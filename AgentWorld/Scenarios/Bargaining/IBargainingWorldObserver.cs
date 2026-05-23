
using AgentWorld.Context;

namespace AgentWorld.Scenarios.Bargaining;



public interface IBargainingWorldObserver
{
    Task<WorldObservation> RunAsync(BargainingContext context);
}
