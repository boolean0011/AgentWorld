namespace AgentWorld.Context;

/// <summary>
/// 定义世界观察者（环境代理/观察代理）的契约。
/// 用于在特定的上下文环境下，观察并产生关于当前世界/环境的状态、变化或随机突发事件。
/// </summary>
/// <typeparam name="TContext">执行世界观察时的上下文类型。</typeparam>
/// <typeparam name="TWorldObservation">观察结果的数据模型类型，必须派生自 <see cref="WorldObservation"/>。</typeparam>
public interface IWorldObserver<TContext, TWorldObservation>
    where TContext : IContext
    where TWorldObservation: WorldObservation
{
    /// <summary>
    /// 异步执行观察逻辑，基于当前的上下文生成并返回新的世界观察信息。
    /// </summary>
    /// <param name="context">当前执行观察所需的上下文实例。</param>
    /// <returns>包含世界/环境观察结果的异步任务。</returns>
    Task<TWorldObservation> RunAsync(TContext context);
}

