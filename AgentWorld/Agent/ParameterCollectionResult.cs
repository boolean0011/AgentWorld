namespace AgentWorld.Agent;

public abstract record ParameterCollectionResult;

public sealed record IncompleteParameterResult(string Message) : ParameterCollectionResult;

public sealed record ParameterCollectionSuccess<T>(T Result) : ParameterCollectionResult;
