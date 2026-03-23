using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentWorld.Scenarios.Bargaining;

public class BargainingOrchestratorBuilder : IBargainingBuildService
{
    private readonly IReadOnlyList<ChatMessage> _initialContext;
    private readonly InMemoryChatHistoryProvider _chatHistoryProvider = new();
    private readonly AIAgent _agent;
    private AgentSession? _session;
    private bool _isBootstrapped = false;

    private const string DefaultInstructions = """
        # Role: 自动化砍价助手执行官

        # Goals
        你的唯一任务是收集必要参数并触发后端的『自动化系统砍价引擎』，即调用ExecuteBargainEngine。

        # Constraints & Rules
        - **强制参数校验**：调用 `ExecuteBargainEngine` 必须满足以下两个条件，缺一不可：
            - `productName`: 明确的商品名称（例如：iPhone 15 Pro）。
            - `targetPrice`: 具体的数字价格（必须是数值型，不能是模糊的描述）。
        - **禁止预设/猜测**：严禁在参数不全时私自调用工具或猜测用户的心理价位。
        - **沉默原则**：当参数齐全并成功触发工具后，**严禁**输出任何文本回复，直接发送 Tool Call 即可。
        - **自然交互**：在参数不全需要询问时，保持礼貌且像真人一样的口吻，不要显得机械。

        ## Execution Workflow
        - **分析上下文**：检索历史对话，提取【目标商品】和【预期价格数字】。
        - **分支决策**：
            - **[情况 A：参数缺失]**：向用户发起补齐询问。
                - *示例*：“没问题，砍价我最拿手。请问您看中的是哪款商品？心里的理想成交价是多少呢？”
            - **[情况 B：参数齐全]**：立即执行 `ExecuteBargainEngine`。**（此分支下禁止任何回复文本）**
            - **[情况 C：用户放弃/转移话题]**：立即执行 `CancelAndExit`。
        """;

    public BargainingOrchestratorBuilder(IChatClient chatClient, IReadOnlyList<ChatMessage> initialContext, string? instructions = null)
    {
        _initialContext = initialContext;
        _agent = chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "BargainingOrchestratorBuilder",
                Description = "负责收集砍价所需参数，并在参数齐全后触发执行。",
                ChatHistoryProvider = _chatHistoryProvider,
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = string.IsNullOrWhiteSpace(instructions) ? DefaultInstructions : instructions,
                    AllowMultipleToolCalls = false,
                    Tools =
                    [
                        AIFunctionFactory.Create(ExecuteBargainEngineSchema),
                        AIFunctionFactory.Create(CancelAndExitSchema)
                    ]
                }
            });
    }

    public async Task<BargainingBuildResult> RunAsync(string? userInput = null, CancellationToken cancellationToken = default)
    {
        _session ??= await _agent.CreateSessionAsync(cancellationToken);

        if (!_isBootstrapped)
        {
            _chatHistoryProvider.SetMessages(_session, [.. _initialContext]);
            _isBootstrapped = true;
        }

        Microsoft.Agents.AI.AgentResponse response;
        if (string.IsNullOrWhiteSpace(userInput))
        {
            response = await _agent.RunAsync(_session, null, cancellationToken);
        }
        else
        {
            response = await _agent.RunAsync(
                new ChatMessage(ChatRole.User, userInput),
                _session,
                null,
                cancellationToken);
        }

        var functionCall = response.Messages
            .SelectMany(static message => message.Contents)
            .OfType<FunctionCallContent>()
            .FirstOrDefault();

        if (functionCall is not null)
        {
            return functionCall.Name switch
            {
                nameof(ExecuteBargainEngineSchema) => CreateExecRequest(functionCall),
                nameof(CancelAndExitSchema) => new BargainingBuildCancelled("Exit"),
                _ => throw new InvalidOperationException($"Unsupported function call: {functionCall.Name}")
            };
        }

        var message = string.IsNullOrWhiteSpace(response.Text) ? "你想买什么商品？你的心理预期价位是多少？" : response.Text;

        return new BargainingParametersNeeded(message);
    }

    private BargainingBuildReady CreateExecRequest(FunctionCallContent functionCall)
    {
        var arguments = functionCall.Arguments
            ?? throw new InvalidOperationException("ExecuteBargainEngine returned null arguments.");

        if (!arguments.TryGetValue("productName", out var productNameValue))
        {
            throw new InvalidOperationException("ExecuteBargainEngine is missing productName.");
        }

        if (!arguments.TryGetValue("targetPrice", out var targetPriceValue))
        {
            throw new InvalidOperationException("ExecuteBargainEngine is missing targetPrice.");
        }

        var productName = productNameValue?.ToString();
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new InvalidOperationException("ExecuteBargainEngine returned an empty productName.");
        }

        var targetPrice = ParseTargetPrice(targetPriceValue);

        return new BargainingBuildReady(productName, targetPrice);
    }

    private static decimal ParseTargetPrice(object? targetPriceValue)
    {
        return targetPriceValue switch
        {
            decimal value => value,
            JsonElement { ValueKind: JsonValueKind.Number } value => value.GetDecimal(),
            JsonElement { ValueKind: JsonValueKind.String } value
                when decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            IConvertible value => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"ExecuteBargainEngine returned an unsupported targetPrice: {targetPriceValue}")
        };
    }

    [Description("【极其重要：只有当参数全齐时才可调用此函数！】调用底层的砍价算法引擎提交任务。")]
    private static string ExecuteBargainEngineSchema(
        [Description("必须明确：被砍价的商品名称是什么？")] string productName,
        [Description("必须明确：用户的心理期望价位是多少？（必须是数字形式）")] decimal targetPrice)
    {
        return $"Execute bargain engine for product '{productName}' with target price '{targetPrice}'.";
    }

    [Description("当控制权在商品砍价对话期间并且用户决定取消、停止或者退出砍价时，调用此函数返回上一级")]
    private static string CancelAndExitSchema() => "Cancel bargaining and exit.";
}
