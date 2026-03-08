using AgentWorld.Core.Context;

namespace AgentWorld.Core.Agent;

public interface IUserAgentPrompt
{
    string GetPrompt(string agentName, IContext context);
}
