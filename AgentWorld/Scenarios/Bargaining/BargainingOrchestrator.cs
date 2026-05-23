using AgentWorld.Agent;

namespace AgentWorld.Scenarios.Bargaining;

public class BargainingOrchestrator(
    IReadOnlyList<IUserAgent<BargainingContext>> clerkAgents,
    IReadOnlyList<IUserAgent<BargainingContext>> consumerAgents,
    IBargainingJudgeAgent judgeAgent,
    IBargainingWorldObserver worldObserverAgent,
    int maxRounds)
{
    public async IAsyncEnumerable<BargainingResponse> RunAsync(string productName, decimal targetPrice)
    {
        ArgumentOutOfRangeException.ThrowIfZero(clerkAgents.Count);
        ArgumentOutOfRangeException.ThrowIfZero(consumerAgents.Count);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRounds, 1);

        var state = new BargainingContext
        {
            MaxRounds = maxRounds,
            ProductName = productName,
            TargetPrice = targetPrice
        };

        // 环境 Agent 获取环境内的相关事件 (Observation)
        var observation = await worldObserverAgent.RunAsync(state);
        state.Observation = observation.Content;

        // 开场阶段，让商家的每个Agent都轮流发言
        state.Phase = BargainingPhase.Start;
        await foreach (var response in ClerkAgentRunAsync(state))
        {
            yield return response;
        }

        // 循环砍价阶段，让顾客Agent与商家Agent交替发言
        state.Phase = BargainingPhase.Ongoing;
        for (var round = 1; round <= maxRounds; round++)
        {
            state.CurrentRound = round;

            if(maxRounds - state.CurrentRound < 3) // todo
            {
                // 快结束时加快砍价冲刺
                state.Phase = BargainingPhase.FinalPush;
            }

            // 让顾客发言
            await foreach (var response in CustomerAgentRunAsync(state))
            {
                yield return response;
            }

            // 让店员发言
            await foreach (var response in ClerkAgentRunAsync(state))
            {
                yield return response;
            }

            // 裁判进行单轮评估并更新状态
            if (await judgeAgent.RunAsync(state) is { } evaluation)
            {
                state.Patience = Math.Clamp(state.Patience + evaluation.PatienceDelta, 0, 100);
                state.Affection = Math.Clamp(state.Affection + evaluation.AffectionDelta, 0, 100);
                state.Result = evaluation.Result;

                Console.WriteLine($"【局势更新】耐心值: {state.Patience}/100, 好感度: {state.Affection}/100. (原因: {evaluation.Reason})");
            }

            // 根据裁判结果，决定是否收尾生命周期并跳出循环
            if (state.Result == BargainingRoundEvaluation.Agreed)
            {
                state.Phase = BargainingPhase.Agreed;
                yield return new SystemNotification("🎉 【结局达成：和平双赢】交易成功！双方达成一致。", state.Phase);
                break;
            }

            if (state.Result == BargainingRoundEvaluation.Broken || state.Patience <= 0)
            {
                state.Phase = BargainingPhase.Broken;
                yield return new SystemNotification("💥 【结局达成：谈判破裂/被赶出门】失去耐心，谈判破裂！", state.Phase);
                break;
            }
        }

        // 砍价结束后，处理收尾阶段逻辑
        if (state.Phase != BargainingPhase.Agreed && state.Phase != BargainingPhase.Broken)
        {
            state.Phase = BargainingPhase.Timeout;
            yield return new SystemNotification("⌛ 【结局达成：自然结束】对话轮次耗尽，双方未达成交易。", state.Phase);
        }
    }

    private async IAsyncEnumerable<BargainingResponse> CustomerAgentRunAsync(BargainingContext state)
    {
        foreach (var agent in consumerAgents.OrderBy(_ => Random.Shared.Next()))
        {
            var content = await agent.RunAsync(state);
            yield return new AgentResponse(agent.Name, content, state.Phase);
        }
    }

    private async IAsyncEnumerable<BargainingResponse> ClerkAgentRunAsync(BargainingContext state)
    {
        foreach (var agent in clerkAgents.OrderBy(_ => Random.Shared.Next()))
        {
            var content = await agent.RunAsync(state);
            yield return new AgentResponse(agent.Name, content, state.Phase);
        }
    }
}
