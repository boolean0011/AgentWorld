using AgentWorld.Agent;
using AgentWorld.Context;

namespace AgentWorld.Scenarios.Bargaining;

/// <summary>
/// 砍价场景的多智能体编排器，负责驱动砍价全流程的生命周期。
/// <para>
/// 流程分为四个阶段：
/// <list type="number">
///   <item><description><b>世界观察</b>：调用 <see cref="WorldObserverAgent"/> 获取当前环境事件并写入上下文。</description></item>
///   <item><description><b>开场阶段</b>：让所有店员 Agent 轮流发言完成开场白。</description></item>
///   <item><description><b>砍价循环</b>：顾客与店员交替发言，每轮由裁判评估并更新状态，直到达成、破裂或超时。</description></item>
///   <item><description><b>收尾阶段</b>：在自然结束时补充超时旁白，已达成或破裂则直接结束。</description></item>
/// </list>
/// </para>
/// <para>
/// 所有受保护的虚方法均支持重写，便于子类针对特定场景定制行为。
/// </para>
/// </summary>
/// <typeparam name="TContext">砍价场景的上下文类型，必须派生自 <see cref="BargainingContext"/>。</typeparam>
/// <param name="clerkAgents">参与砍价的店员 Agent 列表。</param>
/// <param name="consumerAgents">参与砍价的顾客 Agent 列表。</param>
/// <param name="judgeAgent">裁判 Agent，负责每轮对话的评估与终止条件判定。</param>
/// <param name="worldObserverAgent">环境观察 Agent，负责在砍价开始前获取当前世界事件。</param>
/// <param name="maxRounds">砍价的最大回合数，超过后触发超时结局。</param>
public class BargainingOrchestrator<TContext>(
    IReadOnlyList<IUserAgent<TContext>> clerkAgents,
    IReadOnlyList<IUserAgent<TContext>> consumerAgents,
    IBargainingJudge judgeAgent,
    IWorldObserver<TContext, WorldObservation> worldObserverAgent,
    int maxRounds)
    where TContext : BargainingContext
{
    /// <summary>获取参与砍价的店员 Agent 只读列表。</summary>
    protected IReadOnlyList<IUserAgent<TContext>> ClerkAgents { get; } = clerkAgents;

    /// <summary>获取参与砍价的顾客 Agent 只读列表。</summary>
    protected IReadOnlyList<IUserAgent<TContext>> ConsumerAgents { get; } = consumerAgents;

    /// <summary>获取裁判 Agent，负责评估每轮对话并判定终止条件。</summary>
    protected IBargainingJudge JudgeAgent { get; } = judgeAgent;

    /// <summary>获取环境观察 Agent，负责在砍价开始前生成当前世界事件描述。</summary>
    protected IWorldObserver<TContext, WorldObservation> WorldObserverAgent { get; } = worldObserverAgent;

    /// <summary>获取砍价的最大回合数。</summary>
    public int MaxRounds { get; } = maxRounds;

    /// <summary>
    /// 异步驱动完整的砍价生命周期，按序执行世界观察、开场、砍价循环和收尾四个阶段，
    /// 并以异步流形式逐条产出过程中的 <see cref="BargainingMessage"/> 消息。
    /// </summary>
    /// <param name="context">当前砍价场景的上下文，记录双方状态和对话历史。</param>
    /// <returns>砍价全程产出的消息异步序列，包含旁白、Agent 发言及结局通知。</returns>
    public virtual async IAsyncEnumerable<BargainingMessage> RunAsync(TContext context)
    {
        // 1. Observe the world
        await ObserveWorldAsync(context);

        // 2. Start Phase
        await foreach (var response in StartPhaseAsync(context))
        {
            yield return response;
        }

        // 3. Ongoing Phase (Loop)
        context.Phase = BargainingStage.Ongoing;
        for (var round = 1; round <= MaxRounds; round++)
        {
            context.CurrentRound = round;

            UpdatePhaseForRound(context);

            // 让顾客发言
            await foreach (var response in CustomerTurnAsync(context))
            {
                yield return response;
            }

            // 让店员发言
            await foreach (var response in ClerkTurnAsync(context))
            {
                yield return response;
            }

            // 裁判进行单轮评估并更新状态
            var evaluation = await EvaluateRoundAsync(context);
            UpdateState(context, evaluation);
            // OnStateUpdated(context, evaluation);

            // 根据裁判结果，决定是否收尾生命周期并跳出循环
            if (CheckTermination(context, out var terminationResponse))
            {
                yield return terminationResponse!;
                break;
            }
        }

        // 4. End Phase
        await foreach (var response in EndPhaseAsync(context))
        {
            yield return response;
        }
    }

    /// <summary>
    /// 调用 <see cref="WorldObserverAgent"/> 获取当前环境事件，并将内容写入上下文的 <see cref="BargainingContext.Observation"/>。
    /// </summary>
    /// <param name="context">当前砍价上下文。</param>
    protected virtual async Task ObserveWorldAsync(TContext context)
    {
        // 环境 Agent 获取环境内的相关事件 (Observation)
        var observation = await WorldObserverAgent.RunAsync(context);
        context.Observation = observation.Content;
    }

    /// <summary>
    /// 执行开场阶段：将阶段设为 <see cref="BargainingStage.Start"/>，并让所有店员 Agent 依次发言完成开场白。
    /// </summary>
    /// <param name="context">当前砍价上下文。</param>
    /// <returns>开场阶段所有店员发言的消息异步序列。</returns>
    protected virtual async IAsyncEnumerable<BargainingMessage> StartPhaseAsync(TContext context)
    {
        // 开场阶段，让商家的每个Agent都轮流发言
        context.Phase = BargainingStage.Start;
        await foreach (var response in ClerkAgentRunAsync(context))
        {
            yield return response;
        }
    }

    /// <summary>
    /// 根据当前回合与最大回合数的差距，决定是否将阶段切换为 <see cref="BargainingStage.FinalPush"/>（最后冲刺）。
    /// </summary>
    /// <param name="context">当前砍价上下文。</param>
    protected virtual void UpdatePhaseForRound(TContext context)
    {
        if (MaxRounds - context.CurrentRound < 3)
        {
            // 快结束时加快砍价冲刺
            context.Phase = BargainingStage.FinalPush;
        }
    }

    /// <summary>
    /// 执行顾客发言回合：让所有顾客 Agent 按随机顺序依次发言。
    /// </summary>
    /// <param name="context">当前砍价上下文。</param>
    /// <returns>本轮所有顾客发言的消息异步序列。</returns>
    protected virtual async IAsyncEnumerable<BargainingMessage> CustomerTurnAsync(TContext context)
    {
        await foreach (var response in CustomerAgentRunAsync(context))
        {
            yield return response;
        }
    }

    /// <summary>
    /// 执行店员发言回合：让所有店员 Agent 按随机顺序依次发言。
    /// </summary>
    /// <param name="context">当前砍价上下文。</param>
    /// <returns>本轮所有店员发言的消息异步序列。</returns>
    protected virtual async IAsyncEnumerable<BargainingMessage> ClerkTurnAsync(TContext context)
    {
        await foreach (var response in ClerkAgentRunAsync(context))
        {
            yield return response;
        }
    }

    /// <summary>
    /// 调用裁判 Agent 对本轮对话进行评估，返回耐心值、好感度变化及谈判结果。
    /// </summary>
    /// <param name="context">当前砍价上下文，包含完整对话历史。</param>
    /// <returns>本轮的评估结果 <see cref="BargainingRoundEvaluation"/>。</returns>
    protected virtual async Task<BargainingRoundEvaluation> EvaluateRoundAsync(TContext context)
    {
        return await JudgeAgent.RunAsync(context);
    }

    /// <summary>
    /// 在状态更新后输出当前耐心值和好感度的调试日志（默认未启用，可由子类覆写后自行调用）。
    /// </summary>
    /// <param name="context">当前砍价上下文。</param>
    /// <param name="evaluation">本轮评估结果。</param>
    protected virtual void OnStateUpdated(TContext context, BargainingRoundEvaluation evaluation)
    {
        Console.WriteLine($"【局势更新】耐心值: {context.Patience}/100, 好感度: {context.Affection}/100. (原因: {evaluation.Reason})");
    }

    /// <summary>
    /// 检查当前是否满足终止条件（达成交易或谈判破裂/耐心耗尽），并在满足时输出对应的结局旁白消息。
    /// </summary>
    /// <param name="context">当前砍价上下文。</param>
    /// <param name="terminationResponse">
    /// 若触发终止，输出对应的 <see cref="BargainingMessage"/> 结局消息；否则为 <see langword="null"/>。
    /// </param>
    /// <returns>若应终止砍价循环则返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    protected virtual bool CheckTermination(TContext context, out BargainingMessage? terminationResponse)
    {
        if (context.Result == BargainingResult.Agreed)
        {
            context.Phase = BargainingStage.Agreed;
            terminationResponse = new NarratorMessage("🎉 【结局达成：和平双赢】交易成功！双方达成一致。", context.Phase);
            return true;
        }

        if (context.Result == BargainingResult.Broken || context.Patience <= 0)
        {
            context.Phase = BargainingStage.Broken;
            terminationResponse = new NarratorMessage("💥 【结局达成：谈判破裂/被赶出门】失去耐心，谈判破裂！", context.Phase);
            return true;
        }

        terminationResponse = null;
        return false;
    }

    /// <summary>
    /// 执行收尾阶段：若砍价既未达成也未破裂，则将阶段设为超时并输出超时旁白。
    /// </summary>
    /// <param name="context">当前砍价上下文。</param>
    /// <returns>收尾阶段产出的消息异步序列（超时时含一条旁白，其余情况为空序列）。</returns>
    protected virtual async IAsyncEnumerable<BargainingMessage> EndPhaseAsync(TContext context)
    {
        // 砍价结束后，处理收尾阶段逻辑
        if (context.Phase != BargainingStage.Agreed && context.Phase != BargainingStage.Broken)
        {
            context.Phase = BargainingStage.Timeout;
            yield return new NarratorMessage("⌛ 【结局达成：自然结束】对话轮次耗尽，双方未达成交易。", context.Phase);
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// 根据裁判评估结果更新上下文中的耐心值和好感度（均限制在 [0, 100] 范围内），
    /// 并在裁判给出明确谈判结果时同步写入 <see cref="BargainingContext.Result"/>。
    /// </summary>
    /// <param name="context">当前砍价上下文。</param>
    /// <param name="evaluation">本轮的裁判评估结果。</param>
    protected virtual void UpdateState(TContext context, BargainingRoundEvaluation evaluation)
    {
        context.Patience = Math.Clamp(context.Patience + evaluation.PatienceDelta, 0, 100);
        context.Affection = Math.Clamp(context.Affection + evaluation.AffectionDelta, 0, 100);
        if (evaluation.Result.HasValue)
        {
            context.Result = evaluation.Result;
        }
    }

    /// <summary>
    /// 让所有顾客 Agent 按随机顺序依次执行，产出各自的发言消息。
    /// </summary>
    /// <param name="context">当前砍价上下文。</param>
    /// <returns>所有顾客 Agent 按随机顺序发言的消息异步序列。</returns>
    protected virtual async IAsyncEnumerable<BargainingMessage> CustomerAgentRunAsync(TContext context)
    {
        foreach (var agent in ConsumerAgents.OrderBy(_ => Random.Shared.Next()))
        {
            var content = await agent.RunAsync(context);
            yield return new AgentMessage(agent.Name, content, context.Phase);
        }
    }

    /// <summary>
    /// 让所有店员 Agent 按随机顺序依次执行，产出各自的发言消息。
    /// </summary>
    /// <param name="context">当前砍价上下文。</param>
    /// <returns>所有店员 Agent 按随机顺序发言的消息异步序列。</returns>
    protected virtual async IAsyncEnumerable<BargainingMessage> ClerkAgentRunAsync(TContext context)
    {
        foreach (var agent in ClerkAgents.OrderBy(_ => Random.Shared.Next()))
        {
            var content = await agent.RunAsync(context);
            yield return new AgentMessage(agent.Name, content, context.Phase);
        }
    }
}


