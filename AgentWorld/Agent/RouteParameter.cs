namespace AgentWorld.Agent;

public sealed record Route(string Description, string TargetExecutorId);

public abstract record RouterResponse;

public sealed record NoRouteSelected(string Message) : RouterResponse;

public sealed record RouteSelected(string HandoffTask, Route Route) : RouterResponse;
