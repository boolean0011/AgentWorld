using System.ClientModel;
using AgentWorld.Core.Agent;
using AgentWorld.Demo.App;
using AgentWorld.Skills.Bargaining;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// 使用豆包大模型做角色对话
var RoleAgentApikey = config["RoleAgent:ApiKey"] ?? throw new InvalidOperationException("RoleAgent ApiKey is missing");
var RoleAgentEndpointUrl = config["RoleAgent:EndpointUrl"] ?? throw new InvalidOperationException("RoleAgent EndpointUrl is missing");
var RoleModel = config["RoleAgent:Model"] ?? throw new InvalidOperationException("RoleAgent Model is missing");

var roleAgentClient = new OpenAIClient(new ApiKeyCredential(RoleAgentApikey), new OpenAIClientOptions { Endpoint = new Uri(RoleAgentEndpointUrl) })
                            .GetChatClient(RoleModel).AsIChatClient();

// 使用千问大模型做严格的逻辑推理
var SystemAgentApiKey = config["SystemAgent:ApiKey"] ?? throw new InvalidOperationException("SystemAgent ApiKey is missing");
var SystemAgentEndpointUrl = config["SystemAgent:EndpointUrl"] ?? throw new InvalidOperationException("SystemAgent EndpointUrl is missing");
var SystemAgentModel = config["SystemAgent:Model"] ?? throw new InvalidOperationException("SystemAgent Model is missing");

var systemAgentClient = new OpenAIClient(new ApiKeyCredential(SystemAgentApiKey), new OpenAIClientOptions { Endpoint = new Uri(SystemAgentEndpointUrl) })
                                .GetChatClient(SystemAgentModel).AsIChatClient();

IReadOnlyList<UserAgent> clerkAgents =
[
    new UserAgent(
        chatClient: roleAgentClient,
        instructions: File.ReadAllText(Path.Combine("Prompts", "xiao_mian_tuan.md")),
        name: "小面团",
        description: "小面团",
        role: "Clerk",
        promptProvider: new DemoUserAgentPrompt(),
        responseReflectionAgent: new ClerkResponseReflectionAgent(systemAgentClient))
];

IReadOnlyList<UserAgent> consumerAgents =
[
    new UserAgent(
        chatClient: roleAgentClient,
        instructions: File.ReadAllText(Path.Combine("Prompts", "mu_ye.md")),
        name: "牧野",
        description: "牧野",
        role: "Customer",
        promptProvider: new DemoUserAgentPrompt()),
    new UserAgent(
        chatClient: roleAgentClient,
        instructions: File.ReadAllText(Path.Combine("Prompts", "xiao_ai.md")),
        name: "小艾",
        description: "小艾",
        role: "Customer",
        promptProvider: new DemoUserAgentPrompt())
];

var skill = new BargainingOrchestrator(
    clerkAgents,
    consumerAgents,
    new BargainingJudgeAgent(systemAgentClient),
    new DemoWorldObserverAgent(),
    20);

Console.WriteLine("面包店砍价大战开始！\n");

await foreach (var response in skill.RunAsync())
{
    switch (response)
    {
        case AgentResponse agent:
            Console.WriteLine($"当前发言角色：{agent.AgentName}\n {agent.Content}\n\n");
            break;
        case SystemNotification sys:
            Console.WriteLine($"{sys.Status} {sys.Message}\n\n");
            break;
    }
}

Console.WriteLine("\n✅ Press any key to exit...");
