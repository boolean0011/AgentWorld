namespace AgentWorld.Agent;

public abstract record ParametersCollectionResponse;

public sealed record ParametersCollectionMessage(string Message) : ParametersCollectionResponse;

public sealed record ParametersCollected<T>(T Result) : ParametersCollectionResponse;
