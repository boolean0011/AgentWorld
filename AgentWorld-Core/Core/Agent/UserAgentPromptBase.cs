using AgentWorld.Core.Context;

namespace AgentWorld.Core.Agent;

/// <summary>
/// 类型安全的 prompt 提供者基类。
/// 子类只需实现强类型的 <see cref="GetPrompt(string, TContext)"/>，
/// 基类负责将 <see cref="IContext"/> 窄化为 <typeparamref name="TContext"/>。
/// </summary>
public abstract class UserAgentPromptBase<TContext> : IUserAgentPrompt
    where TContext : IContext
{
    public string GetPrompt(string agentName, IContext context)
    {
        if (context is not TContext typedContext)
        {
            throw new ArgumentException(
                $"Context 类型不匹配：期望 {typeof(TContext).Name}，实际 {context.GetType().Name}",
                nameof(context));
        }

        return GetPrompt(agentName, typedContext);
    }

    protected abstract string GetPrompt(string agentName, TContext context);
}
