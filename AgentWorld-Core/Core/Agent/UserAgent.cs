using AgentWorld.Core.Context;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentWorld.Core.Agent;

public class UserAgent(
    IChatClient chatClient,
    string instructions,
    string name,
    string description,
    string role,
    IUserAgentPrompt promptProvider,
    IResponseReflectionAgent? responseReflectionAgent = null,
    string? reflectionFailureFallbackContent = null) : IUserAgent
{
    public string ReflectionFailureFallbackContent { get; } = reflectionFailureFallbackContent ?? "抱歉，我刚才有点走神了，没听清您说什么，我们继续刚才的话题吧。";

    private readonly AIAgent _agent = new ChatClientAgent(
            chatClient: chatClient,
            instructions: instructions,
            name: name,
            description: description);

    private readonly IUserAgentPrompt _promptProvider = promptProvider;

    public string Name => _agent.Name ?? string.Empty;

    public string Description => _agent.Description ?? string.Empty;

    public string Role { get; } = role;

    public async Task<string> RunAsync(IContext context)
    {
        // 根据世界事件和当前状态生成 prompt
        var prompt = _promptProvider.GetPrompt(_agent.Name!, context);

        List<ChatMessage> messages = [.. context.ConversationHistory, new ChatMessage(ChatRole.User, prompt)];

        var response = await _agent.RunAsync(messages);
        var content = response.Text ?? string.Empty;

        if (responseReflectionAgent != null)
        {
            var success = false;
            for (var i = 0; i < responseReflectionAgent.MaxReflections; i++)
            {
                var checkResult = await responseReflectionAgent.RunAsync(content, context);
                if (checkResult.IsValid)
                {
                    success = true;
                    break;
                }

                Console.WriteLine($"[{_agent.Name} 反思] 检测到输出不符合要求：{checkResult.Reason}，正在要求重新生成...");

                var reflectionPrompt = $"[系统提示] 你刚才的回复违反了设定要求。原因：{checkResult.Reason}。请反思并重新生成一段完整回复，直接输出修改后的回复内容，不要为你的错误道歉。";

                messages.Add(new ChatMessage(ChatRole.Assistant, content));
                messages.Add(new ChatMessage(ChatRole.User, reflectionPrompt));

                response = await _agent.RunAsync(messages);
                content = response.Text ?? string.Empty;
            }

            if (!success)
            {
                Console.WriteLine($"[{_agent.Name} 最终失败] 无法生成合规回复，使用默认兜底内容。");
                content = ReflectionFailureFallbackContent;
            }
        }

        context.ConversationHistory.Add(
            new ChatMessage(ChatRole.User, $"现在是**{_agent.Name}**发言: {content}")
            {
                AuthorName = _agent.Name
            });

        return content;
    }
}
