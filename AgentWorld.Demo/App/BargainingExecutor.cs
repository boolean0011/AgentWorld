using AgentWorld.Agent;
using AgentWorld.Context;
using AgentWorld.Scenarios.Bargaining;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentWorld.Demo.App;

public class BargainingExecutor : Executor
{
    private readonly IChatClient _chatClient;

    private readonly BargainingOrchestrator<BargainingContext, BargainingRoundEvaluation, WorldObservation> _orchestrator;

    public BargainingExecutor(IChatClient chatClient) : base(nameof(BargainingExecutor))
    {
        _chatClient = chatClient;
        _orchestrator = CreateBargainingAgent();
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
    {
        return builder
            .ConfigureRoutes(routeBuilder => routeBuilder.AddHandler<BargainingParameters>(HandleAsync))
            .YieldsOutput<string>();
    }

    private async ValueTask HandleAsync(BargainingParameters request, IWorkflowContext context, CancellationToken cancellationToken)
    {
        var bargainingContext = new BargainingContext
        {
            MaxRounds = _orchestrator.MaxRounds,
            ProductName = request.ProductName,
            TargetPrice = request.TargetPrice
        };

        await foreach (var response in _orchestrator.RunAsync(bargainingContext))
        {
            switch (response)
            {
                case AgentResponse result:
                    await context.YieldOutputAsync($"当前发言角色：{result.AgentName}\n {result.Content}\n\n", cancellationToken);
                    break;
                case SystemNotification result:
                    await context.YieldOutputAsync($"{result.Status} {result.Message}\n\n", cancellationToken);
                    break;
                default:
                    break;
            }
        }
    }

    private BargainingOrchestrator<BargainingContext, BargainingRoundEvaluation, WorldObservation> CreateBargainingAgent()
    {
        IReadOnlyList<IUserAgent<BargainingContext>> clerkAgents =
        [
            new StatelessUserAgent<BargainingContext>(
                chatClient: _chatClient,
                instructions: File.ReadAllText(Path.Combine("Prompts", "xiao_mian_tuan.md")),
                name: "小面团",
                description: "小面团",
                taskProvider: UserAgentTasks.XiaoMianTuan,
                responseReflectionAgent: new ClerkResponseReflectionAgent(_chatClient)
            )
        ];

        IReadOnlyList<IUserAgent<BargainingContext>> consumerAgents =
        [
            new StatelessUserAgent<BargainingContext>(
                chatClient: _chatClient,
                instructions: File.ReadAllText(Path.Combine("Prompts", "mu_ye.md")),
                name: "牧野",
                description: "牧野",
                taskProvider: UserAgentTasks.MuYe
            ),
            new StatelessUserAgent<BargainingContext>(
                chatClient: _chatClient,
                instructions: File.ReadAllText(Path.Combine("Prompts", "xiao_ai.md")),
                name: "小艾",
                description: "小艾",
                taskProvider: UserAgentTasks.XiaoAi
            )
        ];

        var judgeAgent = new BargainingJudgeAgent<BargainingContext, BargainingRoundEvaluation>(_chatClient);
        var worldObserverAgent = new BargainingWorldObserver();

        var orchestrator = new BargainingOrchestrator<BargainingContext, BargainingRoundEvaluation, WorldObservation>(
            clerkAgents,
            consumerAgents,
            judgeAgent,
            worldObserverAgent,
            20);

        return orchestrator;
    }
}
