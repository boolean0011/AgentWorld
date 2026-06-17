namespace AgentWorld.Scenarios.Bargaining;

/// <summary>砍价 Orchestrator 响应的抽象基类。</summary>
public abstract record BargainingMessage(BargainingStage Status);

/// <summary>Agent 发言响应：包含发言者名称与大模型输出内容。</summary>
public record AgentMessage(string AgentName, string Content, BargainingStage Status)
    : BargainingMessage(Status);

/// <summary>系统通知响应：来自 Orchestrator 的流程级通知。</summary>
public record NarratorMessage(string Message, BargainingStage Status)
    : BargainingMessage(Status);
