using Microsoft.Extensions.AI;

namespace AgentWorld.Context;

/// <summary>
/// 对话历史的读写抽象，解耦存储细节与业务逻辑。
/// 默认实现为 <see cref="InMemoryChatHistoryProvider"/>，可替换为滑动窗口、摘要压缩或外部存储等策略。
/// </summary>
public interface IChatHistoryProvider
{
    /// <summary>获取当前完整的对话历史（只读视图）。</summary>
    IReadOnlyList<ChatMessage> GetHistory();

    /// <summary>追加一条新消息到历史记录末尾。</summary>
    void Append(ChatMessage message);

    /// <summary>清空所有历史记录。</summary>
    void Clear();
}
