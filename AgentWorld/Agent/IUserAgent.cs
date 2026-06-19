using AgentWorld.Context;

namespace AgentWorld.Agent;

/// <summary>
/// 定义场景中参与对话的用户侧 Agent 行为契约。
/// <para>
/// 实现此接口的 Agent 代表场景中的一个对话参与者（如店员、顾客等），
/// 根据当前上下文生成自然语言回复，并将其追加到对话历史中。
/// </para>
/// </summary>
/// <typeparam name="TContext">场景上下文类型，必须实现 <see cref="IContext"/>。</typeparam>
public interface IUserAgent<TContext>
    where TContext : IContext
{
    /// <summary>
    /// Agent 的名称，用于在对话历史中标识发言者。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Agent 的简要描述，说明其在场景中扮演的角色或职责。
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 根据当前上下文异步生成一轮回复。
    /// </summary>
    /// <param name="context">当前场景的上下文，包含对话历史、阶段状态等信息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Agent 生成的最终回复文本。</returns>
    Task<string> RunAsync(TContext context, CancellationToken cancellationToken = default);
}
