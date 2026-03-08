using AgentWorld.Core.Context;

namespace AgentWorld.Core.Agent;

public interface IUserAgent
{
    string Name { get; }

    string Role { get; }

    string Description { get; }

    Task<string> RunAsync(IContext context);
}
