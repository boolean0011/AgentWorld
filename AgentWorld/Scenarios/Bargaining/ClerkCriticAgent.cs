using AgentWorld.Agent;
using AgentWorld.Context;
using Microsoft.Extensions.AI;

namespace AgentWorld.Scenarios.Bargaining;

/// <summary>
/// 砍价场景中专门针对店员回复的合规性审核代理，实现 <see cref="ICriticAgent"/> 契约。
/// <para>
/// 审核规则：
/// <list type="number">
///   <item><description><b>底线不破</b>：店员回复中给出的折扣不得低于 8折（即售价低于原价的 80%）。</description></item>
///   <item><description><b>落子无悔</b>：折扣力度只能维持或递增，不能反悔涨价（如已给出9折后不得改口9.5折）。</description></item>
/// </list>
/// 若违反上述任意一条，则返回 <c>IsValid = false</c> 并附带具体原因；若解析失败则默认放行。
/// </para>
/// </summary>
/// <param name="chatClient">用于调用大模型执行审核判定的聊天客户端。</param>
public class ClerkCriticAgent(IChatClient chatClient) : ICriticAgent
{
    /// <summary>底层 AI 聊天客户端，用于驱动审核推理。</summary>
    private readonly IChatClient _chatClient = chatClient;

    /// <summary>审核 Agent 的系统级指令，指示大模型以严格审核员角色输出 JSON 判定结果。</summary>
    private readonly string _instructions = "你是一个严格的输出审核员，擅长判定文本并输出json。";

    /// <summary>
    /// 异步对店员草拟的回复内容进行合规性审核，结合完整对话历史验证折扣是否符合规则。
    /// </summary>
    /// <param name="context">当前砍价上下文，提供完整的对话历史供审核时参考。</param>
    /// <param name="content">待审核的店员回复草稿文本。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>
    /// 审核结果 <see cref="CriticResult"/>：
    /// <list type="bullet">
    ///   <item><description><c>IsValid = true</c>：回复合规或未给出具体折扣，可放行。</description></item>
    ///   <item><description><c>IsValid = false</c>：违反底线或折扣反悔，<c>Reason</c> 中含具体原因。</description></item>
    ///   <item><description>若大模型返回解析失败，则默认放行（<c>IsValid = true</c>）以避免误伤。</description></item>
    /// </list>
    /// </returns>
    public async Task<CriticResult> RunAsync(IContext context, string content, CancellationToken cancellationToken = default)
    {
        var prompt = $$"""
            你是一个专门负责审核价格折扣的裁判。
            请结合对话历史，仔细检查店员（小面团）最新给出的回复，验证其给出的折扣是否符合以下规则：
            1. 绝对底线不能破：最新回复中不能明确或隐晦地给出低于 8折（即80%价格）的折扣。
            2. 落子无悔（必须单调递减）：比起他在前面历史对话中曾给出的折扣，在这个最新回复里的折扣力度只能更大（折扣数值更小）或持平，绝对不能"涨价"（即给出一个比之前数值更大的折扣，比如之前答应9折，这次不能改口要9.5折）。
            
            如果最新回复违反了上述任意一条（比如破了8折底线，或者反悔涨价），则认为不合法 (IsValid = false)，并在 reason 中指出具体原因。
            如果回复中没有给出具体折扣，或者是在合法范围内逐步降价/维持上一次折扣，则认为合法 (IsValid = true)。
            
            请输出 json 格式：{ "isValid": true/false, "reason": "简短的原因说明" }。
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, prompt)
        };
        messages.AddRange(context.ChatHistory.GetHistory());
        messages.Add(new(ChatRole.User, $"[这是店员草拟的最新回复，请审核这段内容]：{content}"));

        try
        {
            var response = await _chatClient.GetResponseAsync<CriticResult>(
                messages,
                new ChatOptions
                {
                    Instructions = _instructions
                },
                cancellationToken: cancellationToken);
            return response.Result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OutputCheck 解析失败: {ex.Message}");
            return new CriticResult { IsValid = true, Reason = "解析失败允许放行" };
        }
    }
}

