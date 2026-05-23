using AgentWorld.Agent;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentWorld.Workflow;

public class RouteExecutor(IChatClient chatClient, IReadOnlyList<Route> routes, string? instructions = null) : Executor(nameof(RouteExecutor))
{
    private readonly RouterAgent _agent = new(chatClient, routes, instructions);

    private AgentSession? _session;

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
    {
        return builder
            .ConfigureRoutes(routeBuilder => routeBuilder.AddHandler<string>(HandleAsync))
            .YieldsOutput<string>()
            .SendsMessageTypes([typeof(string)]);
    }

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
                await context.SendMessageAsync(result.HandoffTask, result.Route.TargetExecutorId, cancellationToken);
                break;
            default:
                _session = null;
                throw new InvalidOperationException($"Unexpected router response type: {response.GetType().Name}");
        }
    }
}
