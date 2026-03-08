namespace AgentWorld.Core.Agent;

public interface IAgentOrchestrator<TOutput>
{
    IAsyncEnumerable<TOutput> RunAsync();
}

public interface IAgentOrchestrator<TInput, TOutput>
{
    IAsyncEnumerable<TOutput> RunAsync(TInput input);
}