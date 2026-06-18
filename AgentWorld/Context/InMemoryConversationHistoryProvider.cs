using Microsoft.Extensions.AI;

namespace AgentWorld.Context;

/// <summary>
/// 基于内存列表的默认对话历史实现，直接在进程内维护消息列表。
/// </summary>
public class InMemoryConversationHistoryProvider : IConversationHistoryProvider
{
    private readonly List<ChatMessage> _messages = [];

    public IReadOnlyList<ChatMessage> GetHistory() => _messages;

    public void Append(ChatMessage message) => _messages.Add(message);

    public void Clear() => _messages.Clear();
}
