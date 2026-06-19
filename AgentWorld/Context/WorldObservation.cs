namespace AgentWorld.Context;

/// <summary>
/// 表示世界或环境观察结果的基类。
/// 用于承载智能体在特定时刻对外部环境进行观察所获得的描述性信息及相关状态数据。
/// </summary>
public class WorldObservation
{
    /// <summary>
    /// 获取或初始化观察到的环境具体内容或事件描述（例如随机突发事件、环境变更状态等）。
    /// </summary>
    public string Content { get; init; } = string.Empty;
}