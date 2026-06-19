namespace AgentWorld.Agent;

/// <summary>
/// 表示 Critic Agent 对其他 Agent 输出内容的审查结果。
/// <para>
/// 当 <see cref="IsValid"/> 为 <c>true</c> 时，表示输出通过验证，流程继续；
/// 当 <see cref="IsValid"/> 为 <c>false</c> 时，表示输出被拒绝，通常会触发重试或修正，
/// 并可通过 <see cref="Reason"/> 获取具体原因。
/// </para>
/// </summary>
public class CriticResult
{
    /// <summary>
    /// 表示被审查的内容是否有效（通过验证）。
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 对审查结论的原因说明。
    /// 当 <see cref="IsValid"/> 为 <c>false</c> 时，应包含具体的拒绝理由。
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
