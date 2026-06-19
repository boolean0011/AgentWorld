namespace AgentWorld.Agent;

/// <summary>
/// 表示参数收集流程的基类结果。
/// </summary>
public abstract record ParameterCollectionResult;

/// <summary>
/// 表示参数尚未收集完整的状态，包含向用户索要缺失参数的提示消息。
/// </summary>
/// <param name="Message">需要发送给用户的引导或提问消息，用于补全参数。</param>
public sealed record IncompleteParameterCollection(string Message) : ParameterCollectionResult;

/// <summary>
/// 表示所有必要参数均已成功收集，并携带最终生成的强类型结果。
/// </summary>
/// <typeparam name="T">所收集参数的目标类型。</typeparam>
/// <param name="Result">收集并构造完毕的参数对象。</param>
public sealed record ParameterCollectionSuccess<T>(T Result) : ParameterCollectionResult;
