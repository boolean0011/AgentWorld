using System.ClientModel;
using AgentWorld.Agent;
using AgentWorld.Demo.App;
using AgentWorld.Workflow;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var apiKey = config["DefaultLLM:ApiKey"] ?? throw new InvalidOperationException("DefaultLLM ApiKey is missing");
var endpointUrl = config["DefaultLLM:EndpointUrl"] ?? throw new InvalidOperationException("DefaultLLM EndpointUrl is missing");
var model = config["DefaultLLM:Model"] ?? throw new InvalidOperationException("DefaultLLM Model is missing");

var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(endpointUrl) })
    .GetChatClient(model)
    .AsIChatClient();

var routeExecutor = new RouteExecutor(
    chatClient,
    [
        new Route(
            "当用户表达想要买东西、甚至要求砍价时执行，你将收集意图并将控制权转交给专门负责砍价的助手。",
            nameof(BargainingParametersCollectionExecutor))
    ]);
var parametersCollectionExecutor = new BargainingParametersCollectionExecutor(chatClient);
var bargainingExecutor = new BargainingExecutor(chatClient);
var parametersCollectionUserInputPort = RequestPort.Create<string, string>(BargainingParametersCollectionExecutor.UserInputPortId);

// 用户输入先经过 RouteExecutor 判断用户意图，如果表现出想买东西的意图，
// 则交给 BargainingParametersCollectionExecutor 收集必要参数，参数收集完成后，
// 会把收集好的参数传递给 BargainingExecutor 继续执行砍价。
var workflow = new WorkflowBuilder(routeExecutor)
    .AddEdge(routeExecutor, parametersCollectionExecutor)
    .AddEdge(parametersCollectionExecutor, parametersCollectionUserInputPort)
    .AddEdge(parametersCollectionUserInputPort, parametersCollectionExecutor)
    .AddEdge(parametersCollectionExecutor, bargainingExecutor)
    .WithOutputFrom(routeExecutor, bargainingExecutor)
    .WithName("AgentWorld Demo Workflow")
    .Build();

while (true)
{
    Console.Write("Said:");
    var userInput = Console.ReadLine();
    if (userInput is null)
    {
        break;
    }

    if (string.IsNullOrWhiteSpace(userInput))
    {
        continue;
    }

    await using var run = await InProcessExecution.Default.RunStreamingAsync(workflow, userInput);
    await foreach (var workflowEvent in run.WatchStreamAsync())
    {
        switch (workflowEvent)
        {
            case WorkflowOutputEvent outputEvent when outputEvent.Is<string>(out var output):
                Console.WriteLine(output);
                break;
            case RequestInfoEvent requestEvent:
                await HandleUserInputRequestAsync(run, requestEvent.Request);
                break;
            case WorkflowErrorEvent errorEvent:
                Console.Error.WriteLine(errorEvent.Exception?.ToString() ?? "Unknown workflow error occurred.");
                return;
            case ExecutorFailedEvent failedEvent:
                Console.Error.WriteLine(
                    $"Executor '{failedEvent.ExecutorId}' failed with {(failedEvent.Data is null ? "unknown error" : failedEvent.Data)}.");
                return;
        }
    }
}

static async Task HandleUserInputRequestAsync(StreamingRun run, ExternalRequest request)
{
    var userInput = Console.ReadLine();
    if (userInput is null)
    {
        await run.CancelRunAsync();
    }
    else
    {
        await run.SendResponseAsync(request.CreateResponse(userInput));
    }
}
