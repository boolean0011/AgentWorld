namespace AgentWorld.Router;

/// <summary>
/// 描述一条路由规则，将特定的触发条件映射到目标执行器。
/// </summary>
/// <param name="TriggerCondition">触发该路由的自然语言条件描述，由大模型据此判断用户意图是否匹配。</param>
/// <param name="TargetExecutorId">匹配成功后消息将被转发到的目标执行器 ID。</param>
public sealed record Route(string TriggerCondition, string TargetExecutorId);

/// <summary>
/// 路由器响应的抽象基类，所有路由决策结果均从此派生。
/// </summary>
public abstract record RouterResponse;

/// <summary>
/// 表示路由器未匹配到任何路由时的响应结果。
/// 通常由路由器在大模型未调用任何工具时返回，内含用于继续与用户对话的回复文本。
/// </summary>
/// <param name="Message">路由器生成的自然语言回复，用于继续与用户对话或进一步澄清意图。</param>
public sealed record NoRouteSelected(string Message) : RouterResponse;

/// <summary>
/// 表示路由器成功匹配到路由时的响应结果。
/// 内含大模型提炼出的下游任务描述以及匹配到的路由规则，供执行器据此将消息转发到对应的下游代理。
/// </summary>
/// <param name="Task">大模型提炼出的、将传递给下游执行器的任务描述；包含用户的关键目标信息。</param>
/// <param name="Route">匹配成功的路由规则，其中包含目标执行器 ID。</param>
public sealed record RouteSelected(string Task, Route Route) : RouterResponse;
