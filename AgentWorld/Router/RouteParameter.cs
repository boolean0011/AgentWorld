namespace AgentWorld.Router;

public sealed record Route(string TriggerCondition, string TargetExecutorId);

public abstract record RouterResponse;

public sealed record NoRouteSelected(string Message) : RouterResponse;

public sealed record RouteSelected(string HandoffTask, Route Route) : RouterResponse;
