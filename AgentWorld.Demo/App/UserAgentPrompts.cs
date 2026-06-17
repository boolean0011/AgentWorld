using AgentWorld.Scenarios.Bargaining;

namespace AgentWorld.Demo.App;

public static class UserAgentPrompts
{
    public static readonly Func<BargainingContext, string> XiaoMianTuan = (context) =>
    {
        var prompt = context switch
        {
            { Phase: BargainingStage.Start } => "小艾和牧野走进了你的面包店，请热情地欢迎他们，并向他们介绍今天的招牌商品和价格。",
            { Phase: BargainingStage.FinalPush } => $"""
                当前进度：第{context.CurrentRound}轮/共{context.MaxRounds}轮，砍价即将结束，请尽快做出最终决定！
                当前耐心值: {context.Patience}/100, 好感度: {context.Affection}/100。
                当前突发状况/事件：
                {context.Observation}

                应对砍价策略（必须遵守规则）：
                - 你的底线是 8折。但在与客户交涉时，绝不能一开始就亮出底线。
                - 建议从原价或较高的折扣（如9.5折、9折）开始试探。
                - 随着回合的推进，你可以基于顾客的情况一点点慢慢降价。
                - 记住：一旦你给出了某个折扣，后续绝不能反悔涨价（给出的折扣数值只能越来越小或者不变）。
                - 如果顾客要求低于 8折 的折扣，必须坚定拒绝。
                - 如果你的耐心值 <= 20，请直接下达逐客令，拒绝卖给他们。
            """,
            _ => $"""
                基于之前的对话，请继续回应顾客的砍价请求。
                当前耐心值: {context.Patience}/100, 好感度: {context.Affection}/100。
                当前突发状况/事件：
                {context.Observation}

                应对砍价策略（必须遵守规则）：
                - 你的底线是 8折。但在与客户交涉时，绝不能一开始就亮出底线。
                - 建议从原价或较高的折扣（如9.5折、9折）开始试探。
                - 随着回合的推进，你可以基于顾客的情况一点点慢慢降价。
                - 记住：一旦你给出了某个折扣，后续绝不能反悔涨价（给出的折扣数值只能越来越小或者不变）。
                - 如果顾客要求低于 8折 的折扣，必须坚定拒绝。
                - 如果你的耐心值 <= 20，请直接下达逐客令，拒绝卖给他们。
            """
        };

        return prompt;
    };

    public static readonly Func<BargainingContext, string> MuYe = (context) =>
    {
        var prompt = context switch
        {
            { Phase: BargainingStage.Start } => "你和小艾一起走进了小面团的面包店，看看有什么好吃的，顺便看看能不能砍砍价。",
            { Phase: BargainingStage.FinalPush } => $"""
                最后机会！第{context.CurrentRound}轮/共{context.MaxRounds}轮，基于之前的对话，请使出你的杀手锏，配合小艾做最后冲刺，争取拿到最低价！
                当前突发状况/事件：
                {context.Observation}
            """,
            _ => $"""
                基于之前的对话，请继续配合小艾砍价，用你的方式帮忙争取优惠。
                当前突发状况/事件：
                {context.Observation}
            """
        };
        
        return prompt;
    };

    public static readonly Func<BargainingContext, string> XiaoAi = (context) =>
    {
        var prompt = context switch
        {
            { Phase: BargainingStage.Start } => "你和牧野一起走进了小面团的面包店，看到菜单上的价格觉得有点贵，决定砍砍价！",
            { Phase: BargainingStage.FinalPush } => $"""
                最后机会！第{context.CurrentRound}轮/共{context.MaxRounds}轮，基于之前的对话，请发起最终攻势！你必须拿到更低的价格！
                当前突发状况/事件：
                {context.Observation}
            """,
            _ => $"""
                基于之前的对话，请继续你的砍价攻势！想办法拿到更低的价格。
                当前突发状况/事件：
                {context.Observation}
            """
        };

        return prompt;
    };
}
