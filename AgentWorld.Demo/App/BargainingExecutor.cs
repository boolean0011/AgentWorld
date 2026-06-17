using AgentWorld.Agent;
using AgentWorld.Scenarios.Bargaining;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentWorld.Demo.App;

public class BargainingExecutor : Executor
{
    private readonly IChatClient _chatClient;

    private readonly BargainingOrchestrator _orchestrator;

    public BargainingExecutor(IChatClient chatClient) : base(nameof(BargainingExecutor))
    {
        _chatClient = chatClient;
        _orchestrator = CreateBargainingOrchestrator();
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
    {
        return builder
            .ConfigureRoutes(routeBuilder => routeBuilder.AddHandler<BargainingParameters>(HandleAsync))
            .YieldsOutput<string>();
    }

    private async ValueTask HandleAsync(BargainingParameters request, IWorkflowContext context, CancellationToken cancellationToken)
    {
        // 构建砍价上下文，包括砍价商品名称、预期价格、最大回合数等等关键信息
        var bargainingContext = new BargainingContext
        {
            ProductName = request.ProductName,
            TargetPrice = request.TargetPrice,
            MaxRounds = _orchestrator.MaxRounds
        };

        await foreach (var response in _orchestrator.RunAsync(bargainingContext))
        {
            switch (response)
            {
                case AgentMessage msg:
                    await context.YieldOutputAsync($"当前发言角色：{msg.AgentName}\n {msg.Content}\n\n", cancellationToken);
                    break;
                case NarratorMessage msg:
                    await context.YieldOutputAsync($"{msg.Status} {msg.Message}\n\n", cancellationToken);
                    break;
                default:
                    break;
            }
        }
    }

    private BargainingOrchestrator CreateBargainingOrchestrator()
    {
        IReadOnlyList<IUserAgent<BargainingContext>> clerkAgents =
        [
            new StatelessUserAgent<BargainingContext>(
                chatClient: _chatClient,
                instructions: File.ReadAllText(Path.Combine("Prompts", "xiao_mian_tuan.md")),
                name: "小面团",
                description: "云朵面包店年轻店员小面团",
                taskPromptProvider: UserAgentPrompts.XiaoMianTuan,
                criticAgent: new ClerkCriticAgent(_chatClient)
            )
        ];

        IReadOnlyList<IUserAgent<BargainingContext>> consumerAgents =
        [
            new StatelessUserAgent<BargainingContext>(
                chatClient: _chatClient,
                instructions: File.ReadAllText(Path.Combine("Prompts", "mu_ye.md")),
                name: "牧野",
                description: "云朵面包店的老顾客牧野",
                taskPromptProvider: UserAgentPrompts.MuYe
            ),
            new StatelessUserAgent<BargainingContext>(
                chatClient: _chatClient,
                instructions: File.ReadAllText(Path.Combine("Prompts", "xiao_ai.md")),
                name: "小艾",
                description: "云朵面包店的老顾客小艾",
                taskPromptProvider: UserAgentPrompts.XiaoAi
            )
        ];

        var judgeAgent = new BargainingJudgeAgent(_chatClient);
        var worldObserverAgent = new BargainingWorldObserverAgent();
        var orchestrator = new BargainingOrchestrator(clerkAgents, consumerAgents, judgeAgent, worldObserverAgent, 20);

        return orchestrator;
    }
}
