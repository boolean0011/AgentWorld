using System.ClientModel;
using AgentWorld.Demo.App;
using AgentWorld.Router;
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

// 核心业务流程:
// 1. 用户输入先经过 RouteExecutor 判断用户意图。
// 2. 如果用户表现出购买意向，则交由 BargainingParametersCollectionExecutor 收集必要参数。
// 3. 参数收集完成后，将参数传递给 BargainingExecutor 执行砍价。
var routeExecutor = new RouteExecutor(
    chatClient,
    [
        new Route("适用于用户表达购买意向、咨询商品价格或要求砍价的场景。", nameof(BargainingParametersCollectionExecutor))
    ]);
var parametersCollectionExecutor = new BargainingParametersCollectionExecutor(chatClient);
var bargainingExecutor = new BargainingExecutor(chatClient);
var parametersCollectionUserInputPort = RequestPort.Create<string, string>(BargainingParametersCollectionExecutor.UserInputPortId);

var workflow = new WorkflowBuilder(routeExecutor)
    .AddEdge(routeExecutor, parametersCollectionExecutor)
    .AddEdge(parametersCollectionExecutor, parametersCollectionUserInputPort)
    .AddEdge(parametersCollectionUserInputPort, parametersCollectionExecutor)
    .AddEdge(parametersCollectionExecutor, bargainingExecutor)
    .WithOutputFrom(routeExecutor, bargainingExecutor)
    .WithName("AgentWorld Demo Workflow")
    .Build();

Console.WriteLine("Said:");
var userInput = Console.ReadLine();
while (userInput?.Trim().Length == 0)
{
    userInput = Console.ReadLine();
}

if (userInput is { } input)
{
    await using var run = await InProcessExecution.Default.RunStreamingAsync(workflow, input);
    await ProcessWorkflowRunAsync(run);
}

static async Task<bool> ProcessWorkflowRunAsync(StreamingRun run)
{
    await foreach (var workflowEvent in run.WatchStreamAsync())
    {
        switch (workflowEvent)
        {
            case WorkflowOutputEvent e when e.Is<string>(out var output):
                Console.WriteLine(output);
                break;
            case RequestInfoEvent e:
                if (!await WaitForUserInputAsync(run, e.Request))
                {
                    await run.CancelRunAsync();
                    return false;
                }
                break;
            case WorkflowErrorEvent e:
                Console.Error.WriteLine(e.Exception?.ToString() ?? "Unknown workflow error occurred.");
                return false;
            case ExecutorFailedEvent e:
                Console.Error.WriteLine(
                    $"Executor '{e.ExecutorId}' failed with {(e.Data is null ? "unknown error" : e.Data)}.");
                return false;
            default:
                Console.Error.WriteLine($"Unexpected workflow event type: {workflowEvent.GetType().Name}");
                return false;
        }
    }

    return true;
}

static async Task<bool> WaitForUserInputAsync(StreamingRun run, ExternalRequest request)
{
    while (Console.ReadLine() is { } userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            continue;
        }

        await run.SendResponseAsync(request.CreateResponse(userInput));
        return true;
    }

    return false;
}
