using Microsoft.Extensions.AI;

namespace AgentWorld.Core.Context;

/// <summary>
/// 所有 Context 必须实现的基础接口，提供全局对话历史访问能力。
/// </summary>
public interface IContext
{
    /// <summary>全局对话历史，按时间顺序追加，所有 Agent 均可读取。</summary>
    List<ChatMessage> ConversationHistory { get; }
}
