using Microsoft.Extensions.AI;

namespace AgentWorld.Core.Router;

public abstract record RouterAgentResult;

public sealed record RouterAgentDirectReply(string Message) : RouterAgentResult;

public sealed record RouterAgentDispatch(string AgentName, IReadOnlyList<ChatMessage> Context) : RouterAgentResult;
