namespace AgentWorld.Scenarios.Bargaining;

public abstract record BargainingBuildResult;

public sealed record BargainingParametersNeeded(string Message) : BargainingBuildResult;

public sealed record BargainingBuildCancelled(string TargetAgentId, string? InitialContext = null) : BargainingBuildResult;

public sealed record BargainingBuildReady(string ProductName, decimal TargetPrice) : BargainingBuildResult;
