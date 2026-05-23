
using Microsoft.Extensions.AI;

namespace AgentWorld.Scenarios.Bargaining;

/// <summary>
/// 谈判判定裁判 Agent，直接使用 IChatClient 进行无状态的独立判定，
/// 评估谈判状态（继续、达成交易、破裂）、耐心值变动、好感度变动，并返回结构化的判定数据。
/// </summary>
public class BargainingJudgeAgent : IBargainingJudgeAgent
{
    /// <summary>
    /// 底层使用的 AI 聊天客户端。
    /// </summary>
    private readonly IChatClient _chatClient;

    /// <summary>
    /// 裁判的系统提示词（指令）。
    /// </summary>
    private readonly string _instructions;

    /// <summary>
    /// 触发裁判对输入的消息列表进行最终判定的 User 唤醒词。
    /// </summary>
    private readonly string _triggerPrompt;


    /// <summary>
    /// 默认的裁判系统指令，定义了裁判角色、评估维度（谈判状态、耐心值、好感度、原因）以及输出格式规范。
    /// </summary>
    private const string DefaultInstructions = """
        # Role
        你是一个独立裁判，擅长客观冷静的分析。

        # Task
        分析用户提供的对话历史中最新一轮互动，并对其进行评估。请以 JSON 格式输出评估结果。

        # Evaluation Criteria
        1. 谈判结果：输出字段 outcome，值只能是以下三个之一：
           - "Ongoing"：谈判仍在继续，尚未结束。
           - "Agreed"：买卖双方明确表达了"达成交易"、"接受价格"或"同意条件"。
           - "Broken"：卖方明确下达了"逐客令"、拒绝继续沟通或明确表示不卖了。
        2. 耐心值变化：买方的发言对卖方小面团的"耐心"有何影响？如果是胡搅蛮缠、出价极低，耐心下降（负数，如 -10 到 -20）；正常交流则为 0。(patienceDelta)
        3. 好感度变化：买方的发言对卖方小面团的"好感"有何影响？如果是嘴甜、夸赞、聊到猫等爱好，好感上升（正数，如 +5 到 +15）；无礼则下降。(affectionDelta)
        4. 简述原因。(reason)
        """;
        
    /// <summary>
    /// 初始化 <see cref="BargainingJudgeAgent"/> 类的新实例。
    /// </summary>
    /// <param name="chatClient">底层使用的 AI 聊天客户端。</param>
    /// <param name="prompt">可选的自定义评判指标系统提示词。如果为 null，将使用默认的 <see cref="DefaultInstructions"/>。</param>
    public BargainingJudgeAgent(IChatClient chatClient, string? prompt = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        _chatClient = chatClient;
        _instructions = prompt ?? DefaultInstructions;
        _triggerPrompt = "请根据设定，分析并评估上述对话的最新一轮互动。";
    }

    /// <summary>
    /// 异步评估最新的谈判情势。由于是无状态评判，此方法不需要维护 session，每次请求会发送完整的历史纪录。
    /// 注意：对话上下文由调用方维护，_agent 是无状态的，因此不需要使用AIAgent中的session、chatHistoryProvider等功能。  
    /// </summary>
    /// <param name="context">当前的谈判上下文状态，包含历史对话纪录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>裁判打分和判定的结构化结果。</returns>
    public async Task<BargainingEvaluationResult> RunAsync(BargainingContext context, CancellationToken cancellationToken = default)
    {
        // 构造发送给大模型的完整上下文（历史对话 + 裁判触发词）
        var messages = new List<ChatMessage>(context.ConversationHistory)
        {
            new(ChatRole.User, _triggerPrompt)
        };

        var response = await _chatClient.GetResponseAsync<BargainingEvaluationResult>(
            messages,
            new ChatOptions
            {
                Instructions = _instructions
            },
            cancellationToken: cancellationToken);

        return response.Result;
    }
}
