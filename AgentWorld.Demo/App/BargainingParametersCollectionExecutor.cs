using System.ComponentModel;
using AgentWorld.Agent;
using AgentWorld.Scenarios.Bargaining;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentWorld.Demo.App;

/// <summary>
/// 砍价参数收集执行器：通过多轮对话从用户处逐步收集砍价所需的参数（商品名称和目标价位），
/// 参数齐全后将 <see cref="BargainingParameters"/> 转发给 <see cref="BargainingExecutor"/> 启动砍价。
/// <para>
/// 内部使用 <see cref="ParametersCollectionAgent{TParameters}"/> 驱动参数收集，
/// 并通过 <see cref="CollectParametersFunction"/> 作为大模型可调用的 Function Tool，
/// 在参数全部到位时触发函数调用并提交结果。
/// </para>
/// </summary>
/// <param name="chatClient">用于驱动参数收集 Agent 的聊天客户端。</param>
public class BargainingParametersCollectionExecutor(IChatClient chatClient) : Executor(nameof(BargainingParametersCollectionExecutor))
{
    /// <summary>
    /// 供大模型在所有参数齐全时调用的占位函数，负责将商品名称和目标价位打包成 <see cref="BargainingParameters"/>。
    /// </summary>
    /// <param name="productName">被砍价的商品名称。</param>
    /// <param name="targetPrice">用户的心理期望价位（数字形式）。</param>
    /// <returns>封装好的砍价参数对象。</returns>
    [Description("【极其重要：只有当参数全齐时才可调用此函数！】调用底层的砍价算法引擎提交任务。")]
    private static BargainingParameters CollectParametersFunction(
        [Description("必须明确：被砍价的商品名称是什么？")] string productName,
        [Description("必须明确：用户的心理期望价位是多少？（必须是数字形式）")] decimal targetPrice)
    {
        return new BargainingParameters(productName, targetPrice);
    }

    /// <summary>
    /// 用户持续输入消息的端口 ID，参数尚不完整时 Agent 的追问将通过此端口回传给调用方。
    /// </summary>
    public const string UserInputPortId = "BargainingParametersCollectionUserInput";

    /// <summary>参数收集 Agent 实例，负责多轮对话直至所有必要参数均已获取。</summary>
    private readonly ParametersCollectionAgent<BargainingParameters> _agent = new(chatClient, AIFunctionFactory.Create(CollectParametersFunction));

    /// <summary>
    /// 当前多轮参数收集会话的上下文。
    /// 参数收集完成后重置为 <see langword="null"/>，以便下一次对话从新会话开始。
    /// </summary>
    private AgentSession? _session;

    /// <summary>
    /// 配置通信协议：注册字符串类型的消息处理器，并声明可向下游发送 <see cref="string"/> 追问和 <see cref="BargainingParameters"/> 结果。
    /// </summary>
    /// <param name="builder">用于构建协议配置的构建器。</param>
    /// <returns>配置完成的 <see cref="ProtocolBuilder"/> 实例。</returns>
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
    {
        return builder
            .ConfigureRoutes(routeBuilder => routeBuilder.AddHandler<string>(HandleAsync))
            .SendsMessageTypes([typeof(string), typeof(BargainingParameters)]);
    }

    /// <summary>
    /// 异步处理一条用户输入，驱动参数收集 Agent 进行一轮对话，并根据结果决定后续行为：
    /// <list type="bullet">
    ///   <item><description><see cref="IncompleteParameterCollection"/>：参数仍不完整，将追问文本发回 <see cref="UserInputPortId"/> 端口继续等待用户输入。</description></item>
    ///   <item><description><see cref="ParameterCollectionSuccess{T}"/>：参数已齐全，重置会话并将 <see cref="BargainingParameters"/> 转发给 <see cref="BargainingExecutor"/>。</description></item>
    /// </list>
    /// </summary>
    /// <param name="message">当前轮次收到的用户输入文本。</param>
    /// <param name="context">工作流上下文，提供向下游发送消息的能力。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        _session ??= await _agent.CreateSessionAsync(cancellationToken);
        var response = await _agent.RunAsync(message, _session, cancellationToken);

        switch (response)
        {
            case IncompleteParameterCollection msg:
                await context.SendMessageAsync(msg.Message, UserInputPortId, cancellationToken);
                break;
            case ParameterCollectionSuccess<BargainingParameters> msg:
                _session = null;
                await context.SendMessageAsync(msg.Result, nameof(BargainingExecutor), cancellationToken);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unexpected bargaining build result type: {response.GetType().Name}");
        }
    }
}

