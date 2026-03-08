using AgentWorld.Core.Context;

namespace AgentWorld.Core.Agent;

public interface ISystemAgent<TOutput>
{
    Task<TOutput> RunAsync();
}

public interface ISystemAgent<TInput, TOutput> where TInput : IContext
{
    Task<TOutput> RunAsync(TInput input);
}
