using AgentWorld.Core.Agent;
using AgentWorld.Core.Context;
using Microsoft.Extensions.AI;

namespace AgentWorld.Skills.Bargaining;

public class BargainingOutputValidatorAgent(IChatClient chatClient) : IOutputValidatorAgent
{
    public async Task<OutputCheckResult> RunAsync(string content, IContext context)
    {
        var prompt = $$"""
            你是一个专门负责审核价格折扣的裁判。
            请结合对话历史，仔细检查店员（小面团）最新给出的回复，验证其给出的折扣是否符合以下规则：
            1. 绝对底线不能破：最新回复中不能明确或隐晦地给出低于 8折（即80%价格）的折扣。
            2. 落子无悔（必须单调递减）：比起他在前面历史对话中曾给出的折扣，在这个最新回复里的折扣力度只能更大（折扣数值更小）或持平，绝对不能“涨价”（即给出一个比之前数值更大的折扣，比如之前答应9折，这次不能改口要9.5折）。
            
            如果最新回复违反了上述任意一条（比如破了8折底线，或者反悔涨价），则认为不合法 (IsValid = false)，并在 reason 中指出具体原因。
            如果回复中没有给出具体折扣，或者是在合法范围内逐步降价/维持上一次折扣，则认为合法 (IsValid = true)。
            
            请输出 json 格式：{ "isValid": true/false, "reason": "简短的原因说明" }。
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, prompt)
        };
        messages.AddRange(context.ConversationHistory);
        messages.Add(new(ChatRole.User, $"[这是店员草拟的最新回复，请审核这段内容]：{content}"));

        try
        {
            var agent = chatClient.AsAIAgent(name: "输出守卫", instructions: "你是一个严格的输出审核员，擅长判定文本并输出json。");
            var response = await agent.RunAsync<OutputCheckResult>(messages);
            return response.Result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OutputCheck 解析失败: {ex.Message}");
            return new OutputCheckResult { IsValid = true, Reason = "解析失败允许放行" };
        }
    }

    public int MaxReflections { get; set; } = 3;

    public string FailureFallbackContent => "抱歉，我刚才有点走神了，没听清您说什么，我们继续刚才的话题吧。";
}
