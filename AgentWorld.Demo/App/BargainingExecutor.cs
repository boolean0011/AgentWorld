using AgentWorld.Agent;
using AgentWorld.Context;
using AgentWorld.Scenarios.Bargaining;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentWorld.Demo.App;

/// <summary>
/// 砍价场景的工作流执行器，接收 <see cref="BargainingParameters"/> 消息后驱动完整砍价流程。
/// <para>
/// 该执行器在初始化时组装好砍价编排器（包含店员、顾客、裁判和环境观察 Agent），
/// 接到参数后构建 <see cref="BargainingContext"/> 并委托编排器运行，
/// 最后将每条对话消息逐条通过 <c>YieldOutput</c> 实时推送给调用方。
/// </para>
/// </summary>
public class BargainingExecutor : Executor
{
    /// <summary>用于驱动所有大模型 Agent 的聊天客户端。</summary>
    private readonly IChatClient _chatClient;

    /// <summary>砍价编排器实例，在构造时由 <see cref="CreateBargainingOrchestrator"/> 初始化。</summary>
    private readonly BargainingOrchestrator<BargainingContext> _orchestrator;

    /// <summary>
    /// 初始化 <see cref="BargainingExecutor"/> 的新实例，并完成内部编排器的组装。
    /// </summary>
    /// <param name="chatClient">用于驱动所有大模型 Agent 的聊天客户端。</param>
    public BargainingExecutor(IChatClient chatClient) : base(nameof(BargainingExecutor))
    {
        _chatClient = chatClient;
        _orchestrator = CreateBargainingOrchestrator();
    }

    /// <summary>
    /// 配置此执行器的通信协议：注册 <see cref="BargainingParameters"/> 类型的消息处理器，并声明输出类型为字符串流。
    /// </summary>
    /// <param name="builder">用于构建协议配置的构建器。</param>
    /// <returns>配置完成的 <see cref="ProtocolBuilder"/> 实例。</returns>
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
    {
        return builder
            .ConfigureRoutes(routeBuilder => routeBuilder.AddHandler<BargainingParameters>(HandleAsync))
            .YieldsOutput<string>();
    }

    /// <summary>
    /// 异步处理一次砍价请求：根据参数构建砍价上下文，驱动编排器完成全流程，
    /// 并将每条 Agent 发言和旁白实时通过 <c>YieldOutput</c> 推送给调用方。
    /// </summary>
    /// <param name="request">砍价所需的初始参数，包含商品名称和买方目标价位。</param>
    /// <param name="context">工作流上下文，提供消息输出能力。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async ValueTask HandleAsync(BargainingParameters request, IWorkflowContext context, CancellationToken cancellationToken)
    {
        // 构建砍价上下文，包括砍价商品名称、预期价格、最大回合数等等关键信息
        var bargainingContext = new BargainingContext
        {
            ProductName = request.ProductName,
            TargetPrice = request.TargetPrice,
            MaxRounds = _orchestrator.MaxRounds,
            ChatHistory = new InMemoryChatHistoryProvider()
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

    /// <summary>
    /// 组装并返回砍价编排器实例。
    /// <para>
    /// 组装内容：
    /// <list type="bullet">
    ///   <item><description>店员 Agent：小面团（附带折扣合规性 Critic Agent）</description></item>
    ///   <item><description>顾客 Agent：牧野、小艾</description></item>
    ///   <item><description>裁判 Agent：<see cref="BargainingJudgeAgent"/></description></item>
    ///   <item><description>环境观察 Agent：<see cref="BargainingWorldObserverAgent"/>（随机生成突发事件）</description></item>
    ///   <item><description>最大回合数：20</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <returns>配置完成的 <see cref="BargainingOrchestrator{TContext}"/> 实例。</returns>
    private BargainingOrchestrator<BargainingContext> CreateBargainingOrchestrator()
    {
        IReadOnlyList<IUserAgent<BargainingContext>> clerkAgents =
        [
            new StatelessUserAgent<BargainingContext>(
                chatClient: _chatClient,
                instructions: File.ReadAllText(Path.Combine("Prompts", "xiao_mian_tuan.md")),
                name: "小面团",
                description: "云朵面包店年轻店员小面团",
                promptProvider: UserAgentPrompts.XiaoMianTuan,
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
                promptProvider: UserAgentPrompts.MuYe
            ),
            new StatelessUserAgent<BargainingContext>(
                chatClient: _chatClient,
                instructions: File.ReadAllText(Path.Combine("Prompts", "xiao_ai.md")),
                name: "小艾",
                description: "云朵面包店的老顾客小艾",
                promptProvider: UserAgentPrompts.XiaoAi
            )
        ];

        var judgeAgent = new BargainingJudgeAgent(_chatClient);
        var worldObserverAgent = new BargainingWorldObserverAgent();
        var orchestrator = new BargainingOrchestrator<BargainingContext>(clerkAgents, consumerAgents, judgeAgent, worldObserverAgent, 20);

        return orchestrator;
    }
}

