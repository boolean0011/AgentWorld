namespace AgentWorld.Scenarios.Bargaining;

public interface IBargainingBuildService
{
    Task<BargainingBuildResult> RunAsync(string? userInput = null, CancellationToken cancellationToken = default);
}
