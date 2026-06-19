using Microsoft.Extensions.AI;

namespace AgentWorld.Context;

/// <summary>
/// 基于内存列表的默认对话历史实现，直接在进程内维护消息列表。
/// </summary>
public class InMemoryChatHistoryProvider : IChatHistoryProvider
{
    /// <summary>
    /// 进程内保存对话消息的内存列表。
    /// </summary>
    private readonly List<ChatMessage> _messages = [];

    /// <summary>
    /// 获取当前完整的内存对话历史（只读视图）。
    /// </summary>
    /// <returns>只读的对话消息列表。</returns>
    public IReadOnlyList<ChatMessage> GetHistory() => _messages;

    /// <summary>
    /// 追加一条新消息到内存历史记录末尾。
    /// </summary>
    /// <param name="message">要追加的对话消息。</param>
    public void Append(ChatMessage message) => _messages.Add(message);

    /// <summary>
    /// 清空所有内存中的历史记录。
    /// </summary>
    public void Clear() => _messages.Clear();
}

