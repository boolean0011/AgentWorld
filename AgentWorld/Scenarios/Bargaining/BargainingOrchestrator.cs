using AgentWorld.Agent;
using AgentWorld.Context;

namespace AgentWorld.Scenarios.Bargaining;

public class BargainingOrchestrator<TContext, TEvaluation, TWorldObservation>(
    IReadOnlyList<IUserAgent<TContext>> clerkAgents,
    IReadOnlyList<IUserAgent<TContext>> consumerAgents,
    IBargainingJudgeAgent<TContext, TEvaluation> judgeAgent,
    IWorldObserver<TContext, TWorldObservation> worldObserverAgent,
    int maxRounds)
    where TContext : BargainingContext
    where TEvaluation : BargainingRoundEvaluation
    where TWorldObservation : WorldObservation
{
    protected IReadOnlyList<IUserAgent<TContext>> ClerkAgents { get; } = clerkAgents;

    protected IReadOnlyList<IUserAgent<TContext>> ConsumerAgents { get; } = consumerAgents;

    protected IBargainingJudgeAgent<TContext, TEvaluation> JudgeAgent { get; } = judgeAgent;

    protected IWorldObserver<TContext, TWorldObservation> WorldObserverAgent { get; } = worldObserverAgent;

    public int MaxRounds { get; } = maxRounds;

    public virtual async IAsyncEnumerable<BargainingResponse> RunAsync(TContext context)
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

    protected virtual async Task ObserveWorldAsync(TContext context)
    {
        // 环境 Agent 获取环境内的相关事件 (Observation)
        var observation = await WorldObserverAgent.RunAsync(context);
        context.Observation = observation.Content;
    }

    protected virtual async IAsyncEnumerable<BargainingResponse> StartPhaseAsync(TContext context)
    {
        // 开场阶段，让商家的每个Agent都轮流发言
        context.Phase = BargainingStage.Start;
        await foreach (var response in ClerkAgentRunAsync(context))
        {
            yield return response;
        }
    }

    protected virtual void UpdatePhaseForRound(TContext context)
    {
        if (MaxRounds - context.CurrentRound < 3)
        {
            // 快结束时加快砍价冲刺
            context.Phase = BargainingStage.FinalPush;
        }
    }

    protected virtual async IAsyncEnumerable<BargainingResponse> CustomerTurnAsync(TContext context)
    {
        await foreach (var response in CustomerAgentRunAsync(context))
        {
            yield return response;
        }
    }

    protected virtual async IAsyncEnumerable<BargainingResponse> ClerkTurnAsync(TContext context)
    {
        await foreach (var response in ClerkAgentRunAsync(context))
        {
            yield return response;
        }
    }

    protected virtual async Task<TEvaluation> EvaluateRoundAsync(TContext context)
    {
        return await JudgeAgent.RunAsync(context);
    }

    protected virtual void OnStateUpdated(TContext context, TEvaluation evaluation)
    {
        Console.WriteLine($"【局势更新】耐心值: {context.Patience}/100, 好感度: {context.Affection}/100. (原因: {evaluation.Reason})");
    }

    protected virtual bool CheckTermination(TContext context, out BargainingResponse? terminationResponse)
    {
        if (context.Result == BargainingOutcome.Agreed)
        {
            context.Phase = BargainingStage.Agreed;
            terminationResponse = new SystemNotification("🎉 【结局达成：和平双赢】交易成功！双方达成一致。", context.Phase);
            return true;
        }

        if (context.Result == BargainingOutcome.Broken || context.Patience <= 0)
        {
            context.Phase = BargainingStage.Broken;
            terminationResponse = new SystemNotification("💥 【结局达成：谈判破裂/被赶出门】失去耐心，谈判破裂！", context.Phase);
            return true;
        }

        terminationResponse = null;
        return false;
    }

    protected virtual async IAsyncEnumerable<BargainingResponse> EndPhaseAsync(TContext context)
    {
        // 砍价结束后，处理收尾阶段逻辑
        if (context.Phase != BargainingStage.Agreed && context.Phase != BargainingStage.Broken)
        {
            context.Phase = BargainingStage.Timeout;
            yield return new SystemNotification("⌛ 【结局达成：自然结束】对话轮次耗尽，双方未达成交易。", context.Phase);
        }
        await Task.CompletedTask;
    }

    protected virtual void UpdateState(TContext context, TEvaluation evaluation)
    {
        context.Patience = Math.Clamp(context.Patience + evaluation.PatienceDelta, 0, 100);
        context.Affection = Math.Clamp(context.Affection + evaluation.AffectionDelta, 0, 100);
        context.Result = evaluation.Result;
    }

    protected virtual async IAsyncEnumerable<BargainingResponse> CustomerAgentRunAsync(TContext context)
    {
        foreach (var agent in ConsumerAgents.OrderBy(_ => Random.Shared.Next()))
        {
            var content = await agent.RunAsync(context);
            yield return new AgentResponse(agent.Name, content, context.Phase);
        }
    }

    protected virtual async IAsyncEnumerable<BargainingResponse> ClerkAgentRunAsync(TContext context)
    {
        foreach (var agent in ClerkAgents.OrderBy(_ => Random.Shared.Next()))
        {
            var content = await agent.RunAsync(context);
            yield return new AgentResponse(agent.Name, content, context.Phase);
        }
    }
}

