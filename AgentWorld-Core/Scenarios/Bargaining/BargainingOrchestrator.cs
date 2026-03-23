using AgentWorld.Core.Agent;
using AgentWorld.Core.Context;

namespace AgentWorld.Scenarios.Bargaining;

public class BargainingOrchestrator(
    IReadOnlyList<UserAgent> clerkAgents,
    IReadOnlyList<UserAgent> consumerAgents,
    ISystemAgent<BargainingContext, BargainingEvaluationResult> judgeAgent,
    ISystemAgent<BargainingContext, WorldObservation> worldObserverAgent,
    int maxRounds) : IAgentOrchestrator<BargainingResponse>
{
    private readonly BargainingContext _state = new();

    public void SetTargetParameters(string productName, decimal targetPrice)
    {
        _state.ProductName = productName;
        _state.TargetPrice = targetPrice;
    }

    public async IAsyncEnumerable<BargainingResponse> RunAsync()
    {
        ArgumentOutOfRangeException.ThrowIfZero(clerkAgents.Count);
        ArgumentOutOfRangeException.ThrowIfZero(consumerAgents.Count);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRounds, 1);

        // 环境 Agent 获取环境内的相关事件 (Observation)
        var observation = await worldObserverAgent.RunAsync(_state);
        _state.Observation = observation.Content;

        // 开场阶段，让商家的每个Agent都轮流发言
        _state.Phase = BargainingPhase.Start;
        await foreach (var response in ClerkAgentRunAsync())
        {
            yield return response;
        }

        // 循环砍价阶段，让顾客Agent与商家Agent交替发言
        _state.Phase = BargainingPhase.Ongoing;
        for (var round = 1; round <= maxRounds; round++)
        {
            _state.CurrentRound = round;

            if(maxRounds - _state.CurrentRound < 3) // todo
            {
                // 快结束时加快砍价冲刺
                _state.Phase = BargainingPhase.FinalPush;
            }

            // 让顾客发言
            await foreach (var response in CustomerAgentRunAsync())
            {
                yield return response;
            }

            // 让店员发言
            await foreach (var response in ClerkAgentRunAsync())
            {
                yield return response;
            }

            // 裁判进行单轮评估并更新状态
            if (await judgeAgent.RunAsync(_state) is { } evaluation)
            {
                _state.Patience = Math.Clamp(_state.Patience + evaluation.PatienceDelta, 0, 100);
                _state.Affection = Math.Clamp(_state.Affection + evaluation.AffectionDelta, 0, 100);
                _state.Result = evaluation.Result;

                Console.WriteLine($"【局势更新】耐心值: {_state.Patience}/100, 好感度: {_state.Affection}/100. (原因: {evaluation.Reason})");
            }

            // 根据裁判结果，决定是否收尾生命周期并跳出循环
            if (_state.Result == BargainingRoundEvaluation.Agreed)
            {
                _state.Phase = BargainingPhase.Agreed;
                yield return new SystemNotification("🎉 【结局达成：和平双赢】交易成功！双方达成一致。", _state.Phase);
                break;
            }

            if (_state.Result == BargainingRoundEvaluation.Broken || _state.Patience <= 0)
            {
                _state.Phase = BargainingPhase.Broken;
                yield return new SystemNotification("💥 【结局达成：谈判破裂/被赶出门】失去耐心，谈判破裂！", _state.Phase);
                break;
            }
        }

        // 砍价结束后，处理收尾阶段逻辑
        if (_state.Phase != BargainingPhase.Agreed && _state.Phase != BargainingPhase.Broken)
        {
            _state.Phase = BargainingPhase.Timeout;
            yield return new SystemNotification("⌛ 【结局达成：自然结束】对话轮次耗尽，双方未达成交易。", _state.Phase);
        }
    }

    private async IAsyncEnumerable<BargainingResponse> CustomerAgentRunAsync()
    {
        foreach (var agent in consumerAgents.OrderBy(_ => Random.Shared.Next()))
        {
            var content = await agent.RunAsync(_state);
            yield return new AgentResponse(agent.Name, content, BargainingPhase.Ongoing);
        }
    }

    private async IAsyncEnumerable<BargainingResponse> ClerkAgentRunAsync()
    {
        foreach (var agent in clerkAgents.OrderBy(_ => Random.Shared.Next()))
        {
            var content = await agent.RunAsync(_state);
            yield return new AgentResponse(agent.Name, content, BargainingPhase.Ongoing);
        }
    }
}
