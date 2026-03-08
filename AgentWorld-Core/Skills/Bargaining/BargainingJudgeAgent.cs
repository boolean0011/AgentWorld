using AgentWorld.Core.Agent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AIAgentResponse = Microsoft.Agents.AI.AgentResponse<AgentWorld.Skills.Bargaining.BargainingEvaluationResult>;

namespace AgentWorld.Skills.Bargaining;

public class BargainingJudgeAgent(IChatClient chatClient, string? prompt = null) : ISystemAgent<BargainingContext, BargainingEvaluationResult>
{
    private const string defaultPrompt = """
        请作为独立裁判，分析上述对话的最新一轮互动，你需要评估：
        1. 谈判结果：输出字段 outcome，值只能是以下三个之一：
           - "Ongoing"：谈判仍在继续，尚未结束。
           - "Agreed"：买卖双方明确表达了"达成交易"、"接受价格"或"同意条件"。
           - "Broken"：卖方明确下达了"逐客令"、拒绝继续沟通或明确表示不卖了。
        2. 耐心值变化：买方的发言对卖方小面团的"耐心"有何影响？如果是胡搅蛮缠、出价极低，耐心下降（负数，如 -10 到 -20）；正常交流则为 0。(patienceDelta)
        3. 好感度变化：买方的发言对卖方小面团的"好感"有何影响？如果是嘴甜、夸赞、聊到猫等爱好，好感上升（正数，如 +5 到 +15）；无礼则下降。(affectionDelta)
        4. 简述原因。(reason)
        """;

    public async Task<BargainingEvaluationResult> RunAsync(BargainingContext state)
    {
        var messages = new List<ChatMessage>(state.ConversationHistory)
        {
            new(ChatRole.User, prompt ?? defaultPrompt)
        };

        try
        {
            AIAgent agent = chatClient.AsAIAgent(name: "裁判", instructions: "你是一个独立裁判，擅长客观冷静的分析。请以 json 格式输出结果。");
            AIAgentResponse response = await agent.RunAsync<BargainingEvaluationResult>(messages);

            return response.Result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DefaultBargainingStateEvaluator 解析失败: {ex.Message}");
            return new BargainingEvaluationResult
            {
                Result = BargainingRoundEvaluation.Ongoing,
                PatienceDelta = 0,
                AffectionDelta = 0,
                Reason = $"裁判解析失败，采用默认评估: {ex.Message}"
            };
        }
    }
}
