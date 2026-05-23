using System.ComponentModel;
using AgentWorld.Agent;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentWorld.Demo.App;

public class BargainingParametersCollectionExecutor(IChatClient chatClient) : Executor(nameof(BargainingParametersCollectionExecutor))
{
    [Description("【极其重要：只有当参数全齐时才可调用此函数！】调用底层的砍价算法引擎提交任务。")]
    private static BargainingParameters CollectParametersFunction(
        [Description("必须明确：被砍价的商品名称是什么？")] string productName,
        [Description("必须明确：用户的心理期望价位是多少？（必须是数字形式）")] decimal targetPrice)
    {
        return new BargainingParameters(productName, targetPrice);
    }

    public const string UserInputPortId = "BargainingParametersCollectionUserInput";

    private readonly ParametersCollectionAgent<BargainingParameters> _agent = new(chatClient, AIFunctionFactory.Create(CollectParametersFunction));

    private AgentSession? _session;

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
    {
        return builder
            .ConfigureRoutes(routeBuilder => routeBuilder.AddHandler<string>(HandleAsync))
            .SendsMessageTypes([typeof(string), typeof(BargainingParameters)]);
    }

    private async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        _session ??= await _agent.CreateSessionAsync(cancellationToken);
        var response = await _agent.RunAsync(message, _session, cancellationToken);

        switch (response)
        {
            case ParametersCollectionMessage result:
                await context.SendMessageAsync(result.Message, UserInputPortId, cancellationToken);
                break;
            case ParametersCollected<BargainingParameters> result:
                _session = null;
                await context.SendMessageAsync(result.Result, nameof(BargainingExecutor), cancellationToken);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unexpected bargaining build result type: {response.GetType().Name}");
        }
    }
}
