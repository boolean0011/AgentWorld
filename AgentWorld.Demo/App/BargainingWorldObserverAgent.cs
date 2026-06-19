using AgentWorld.Context;

using AgentWorld.Scenarios.Bargaining;

namespace AgentWorld.Demo.App;

/// <summary>
/// 砍价场景的环境观察 Agent，实现 <see cref="IWorldObserver{TContext,TWorldObservation}"/> 契约。
/// <para>
/// 在每次砍价开始前，以随机概率从预设的突发事件池中抽取一条事件描述，
/// 为砍价对话注入额外的环境背景信息，增强场景的随机性与沉浸感。
/// </para>
/// </summary>
public class BargainingWorldObserverAgent : IWorldObserver<BargainingContext, WorldObservation>
{
    /// <summary>用于随机抽取突发事件的随机数生成器。</summary>
    private readonly Random _random = new();

    /// <summary>
    /// 以随机概率从预设事件池中抽取一条突发事件，并返回包含该事件描述的世界观察结果。
    /// <list type="bullet">
    ///   <item><description>15% 概率：新鲜出炉的奶油可颂事件。</description></item>
    ///   <item><description>15% 概率：土豪路过橱窗事件。</description></item>
    ///   <item><description>15% 概率：猫咪跳上收银台撒娇事件。</description></item>
    ///   <item><description>55% 概率（默认）：老板路过前台巡视事件。</description></item>
    /// </list>
    /// </summary>
    /// <param name="context">当前砍价上下文（本实现中暂不使用，保留以符合接口契约）。</param>
    /// <returns>包含随机突发事件描述的 <see cref="WorldObservation"/> 实例。</returns>
    public Task<WorldObservation> RunAsync(BargainingContext context)
    {
        var rand = _random.Next(100);
        string content = rand switch
        {
            < 15 => "[突发事件：新鲜出炉] 店里刚烤好一批奶油可颂，香溢满屋！",
            < 30 => "[突发事件：土豪路过] 一位土豪看着橱窗里的甜点说'这些我全包了！不过我还没决定付钱'，气氛紧张起来。",
            < 45 => "[突发事件：猫咪撒娇] 店里的小猫突然跳到了收银台上，向着顾客卖萌。",
            _ => "[突发事件：老板巡视] 面包店老板刚好路过前台，看了这边一眼。"
        };

        return Task.FromResult(new WorldObservation { Content = content });
    }
}

