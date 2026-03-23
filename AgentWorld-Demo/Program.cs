using System.ClientModel;
using System.ComponentModel;
using AgentWorld.Core.Agent;
using AgentWorld.Core.Router;
using AgentWorld.Demo.App;
using AgentWorld.Scenarios.Bargaining;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// 使用豆包大模型做角色扮演
var roleplayApikey = config["RoleplayAgent:ApiKey"] ?? throw new InvalidOperationException("RoleplayAgent ApiKey is missing");
var roleplayEndpointUrl = config["RoleplayAgent:EndpointUrl"] ??
                           throw new InvalidOperationException("RoleplayAgent EndpointUrl is missing");
var roleplayModel = config["RoleplayAgent:Model"] ?? throw new InvalidOperationException("RoleplayAgent Model is missing");

var roleplayChatClient = new OpenAIClient(
        new ApiKeyCredential(roleplayApikey),
        new OpenAIClientOptions { Endpoint = new Uri(roleplayEndpointUrl) })
    .GetChatClient(roleplayModel).AsIChatClient();

// 使用千问大模型做严格的逻辑推理
var reasoningApiKey =
    config["ReasoningAgent:ApiKey"] ?? throw new InvalidOperationException("ReasoningAgent ApiKey is missing");
var reasoningEndpointUrl = config["ReasoningAgent:EndpointUrl"] ??
                             throw new InvalidOperationException("ReasoningAgent EndpointUrl is missing");
var reasoningModel =
    config["ReasoningAgent:Model"] ?? throw new InvalidOperationException("ReasoningAgent Model is missing");

var reasoningChatClient = new OpenAIClient(new ApiKeyCredential(reasoningApiKey), new OpenAIClientOptions { Endpoint = new Uri(reasoningEndpointUrl) })
    .GetChatClient(reasoningModel)
    .AsIChatClient();

// 建立一个路由agent，根据用户输入判断用户意图，执行不同的agent
var router = new RouterAgent(reasoningChatClient, [RouterFunctions.TransferToBargainingAgentSchema]);

while (true)
{
    Console.Write("Said: ");
    var userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput))
    {
        continue;
    }

    var response = await router.RunAsync(userInput);

    switch (response)
    {
        case RouterAgentDirectReply reply:
            Console.WriteLine(reply.Message);
            break;
        case RouterAgentDispatch dispatch:
            if (dispatch.AgentName == nameof(RouterFunctions.TransferToBargainingAgentSchema))
            {
                await ExecBargainingOrchestrator(reasoningChatClient, dispatch.Context);
            }
            break;
        default:
            throw new InvalidOperationException(
                $"Unexpected router response type: {response.GetType().Name}");
    }
}

async Task ExecBargainingOrchestrator(IChatClient chatClient, IReadOnlyList<ChatMessage> context)
{
    var bargainingOrchestratorBuilder = new BargainingOrchestratorBuilder(chatClient, context);
    BargainingBuildReady? buildReadyResult = null;
    string? userInput = null;
    var shouldExit = false;

    // 进行参数收集，完成参数收集后，进行砍价
    while (!shouldExit)
    {
        var response = await bargainingOrchestratorBuilder.RunAsync(userInput);
        switch (response)
        {
            case BargainingParametersNeeded needed:
                Console.WriteLine(needed.Message);
                Console.Write("Said: ");
                userInput = Console.ReadLine();
                break;
            case BargainingBuildReady buildReady:
                buildReadyResult = buildReady;
                shouldExit = true;
                break;
            case BargainingBuildCancelled:
                shouldExit = true;
                break;
            default:
                throw new InvalidOperationException(
                    $"Unexpected bargaining build result type: {response.GetType().Name}");
        }
    }

    if (buildReadyResult is not null)
    {
        var bargainingOrchestrator = CreateBargainingOrchestrator(buildReadyResult.ProductName, buildReadyResult.TargetPrice);
        await foreach (var response in bargainingOrchestrator.RunAsync())
        {
            switch (response)
            {
                case AgentResponse resp:
                    Console.WriteLine($"当前发言角色：{resp.AgentName}\n {resp.Content}\n\n");
                    break;
                case SystemNotification sys:
                    Console.WriteLine($"{sys.Status} {sys.Message}\n\n");
                    break;
                default:
                    break;
            }
        }
    }
}

BargainingOrchestrator CreateBargainingOrchestrator(string productName, decimal targetPrice)
{
    Console.WriteLine($"\n[Skill引擎日志] 拦截到参数成功！商品：【{productName}】，价位：【{targetPrice}】\n");

    IReadOnlyList<UserAgent> clerkAgents =
    [
        new UserAgent(
            chatClient: roleplayChatClient,
            instructions: File.ReadAllText(Path.Combine("Prompts", "xiao_mian_tuan.md")),
            name: "小面团",
            description: "小面团",
            role: "Clerk",
            promptProvider: new DemoUserAgentPrompt(),
            responseReflectionAgent: new ClerkResponseReflectionAgent(reasoningChatClient))
    ];

    IReadOnlyList<UserAgent> consumerAgents =
    [
        new UserAgent(
            chatClient: roleplayChatClient,
            instructions: File.ReadAllText(Path.Combine("Prompts", "mu_ye.md")),
            name: "牧野",
            description: "牧野",
            role: "Customer",
            promptProvider: new DemoUserAgentPrompt()),
        new UserAgent(
            chatClient: roleplayChatClient,
            instructions: File.ReadAllText(Path.Combine("Prompts", "xiao_ai.md")),
            name: "小艾",
            description: "小艾",
            role: "Customer",
            promptProvider: new DemoUserAgentPrompt())
    ];

    var bargainingOrchestrator = new BargainingOrchestrator(
        clerkAgents,
        consumerAgents,
        new BargainingJudgeAgent(reasoningChatClient),
        new DemoWorldObserverAgent(),
        20); 

    bargainingOrchestrator.SetTargetParameters(productName, targetPrice); //todo

    return bargainingOrchestrator;
}

public class RouterFunctions
{
    [Description("当用户表达想要买东西、甚至要求砍价时调用此工具，你将收集意图并将控制权转交给专门负责砍价的助手。")]
    public static string TransferToBargainingAgentSchema()
    {
        return "[TRANSFER_TO_BARGAINING_AGENT]";
    }
}
