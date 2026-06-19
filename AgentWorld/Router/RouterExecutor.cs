using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentWorld.Router;

/// <summary>
/// 将 <see cref="RouterAgent"/> 封装为工作流执行器（Executor），负责接收外部消息并驱动路由逻辑。
/// 当路由未命中时，将大模型回复透传给调用方；当路由命中时，重置会话并将任务转发给目标执行器。
/// </summary>
/// <param name="chatClient">用于驱动路由 Agent 的聊天客户端。</param>
/// <param name="routes">路由规则列表，每条规则描述触发条件及目标执行器 ID。</param>
/// <param name="instructions">可选的自定义路由 Agent 系统指令，为 <see langword="null"/> 时使用默认指令。</param>
public class RouteExecutor(IChatClient chatClient, IReadOnlyList<Route> routes, string? instructions = null) : Executor(nameof(RouteExecutor))
{
    /// <summary>负责意图识别与路由决策的底层路由 Agent。</summary>
    private readonly RouterAgent _agent = new(chatClient, routes, instructions);

    /// <summary>
    /// 当前多轮对话的会话上下文。
    /// 路由成功后重置为 <see langword="null"/>，以便下一次对话从全新会话开始。
    /// </summary>
    private AgentSession? _session;

    /// <summary>
    /// 配置此执行器的通信协议：注册字符串类型的消息处理器、声明输出类型及可发送的消息类型。
    /// </summary>
    /// <param name="builder">用于构建协议配置的构建器。</param>
    /// <returns>配置完成的 <see cref="ProtocolBuilder"/> 实例。</returns>
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
    {
        return builder
            .ConfigureRoutes(routeBuilder => routeBuilder.AddHandler<string>(HandleAsync))
            .YieldsOutput<string>()
            .SendsMessageTypes([typeof(string)]);
    }

    /// <summary>
    /// 异步处理一条传入消息：驱动路由 Agent 进行意图识别，并根据路由结果决定后续行为。
    /// <list type="bullet">
    ///   <item><description><see cref="NoRouteSelected"/>：意图尚不明确，将大模型的回复文本通过 <c>YieldOutput</c> 返回给调用方，继续等待用户下一轮输入。</description></item>
    ///   <item><description><see cref="RouteSelected"/>：意图已明确，重置会话并将提炼出的任务描述转发给目标执行器。</description></item>
    /// </list>
    /// </summary>
    /// <param name="message">当前轮次收到的用户输入消息。</param>
    /// <param name="context">工作流上下文，提供输出与消息转发能力。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        _session ??= await _agent.CreateSessionAsync(cancellationToken);
        var response = await _agent.RunAsync(message, _session, cancellationToken);

        switch (response)
        {
            case NoRouteSelected result:
                await context.YieldOutputAsync(result.Message, cancellationToken);
                break;
            case RouteSelected result:
                _session = null;
                await context.SendMessageAsync(result.Task, result.Route.TargetExecutorId, cancellationToken);
                break;
            default:
                _session = null;
                throw new InvalidOperationException($"Unexpected router response type: {response.GetType().Name}");
        }
    }
}
