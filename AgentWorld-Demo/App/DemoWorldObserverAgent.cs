using AgentWorld.Core.Agent;
using AgentWorld.Core.Context;

using AgentWorld.Scenarios.Bargaining;

namespace AgentWorld.Demo.App;

public class DemoWorldObserverAgent : ISystemAgent<BargainingContext, WorldObservation>
{
    private readonly Random _random = new();

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
