namespace AgentWorld.Scenarios.Bargaining;

/// <summary>
/// 砍价场景的输入参数，用于在砍价开始前向场景注入核心初始信息。
/// </summary>
/// <param name="ProductName">目标商品的名称。</param>
/// <param name="TargetPrice">买方的心理目标价位（期望以不高于此价格成交）。</param>
public sealed record BargainingParameters(string ProductName, decimal TargetPrice);
