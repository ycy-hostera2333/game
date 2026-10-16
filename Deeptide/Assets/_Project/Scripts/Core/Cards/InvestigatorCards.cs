using System;

namespace Deeptide.Core.Cards
{
    /// <summary>
    /// 调查员卡牌：阶段 0 纸面原型用到的 15 种牌 + 恐怖低语 + 疯语（策划/03 §3）。
    /// 表格部分以后会换成 Data/cards.csv，类名与 ID 一一对应。
    /// </summary>
    public static class InvestigatorCards
    {
        internal static void Register()
        {
            // ---------------- 基础
            Def("B01", "打击", CardType.Attack, Rarity.Basic, "基础", 1, CardTarget.Enemy,
                "Damage=6", "Damage=9", "造成 {Damage} 伤害。", null, () => new Strike());
            Def("B02", "防御", CardType.Skill, Rarity.Basic, "基础", 1, CardTarget.None,
                "Block=5", "Block=8", "获得 {Block} 格挡。", null, () => new Defend());
            Def("B03", "禁忌一瞥", CardType.Skill, Rarity.Basic, "堕落", 0, CardTarget.None,
                "Sanity=1;MadSanity=1;Draw=2;MadDraw=3", "Draw=3;MadDraw=4",
                "代价{Sanity}。抽 {Draw} 张牌。", "代价{MadSanity}。抽 {MadDraw} 张牌。", () => new ForbiddenGlimpse(), dual: true);

            // ---------------- 守夜
            Def("C01", "祷告", CardType.Skill, Rarity.Common, "守夜", 1, CardTarget.None,
                "Block=6;Restore=1", "Block=9", "获得 {Block} 格挡，恢复 {Restore} 理智。", null, () => new Prayer());
            Def("C02", "守夜灯", CardType.Skill, Rarity.Common, "守夜", 1, CardTarget.None,
                "Block=7;Bonus=4", "Block=9;Bonus=5", "获得 {Block} 格挡。清明：再获得 {Bonus} 格挡。", null, () => new VigilLamp());
            Def("C03", "银质匕首", CardType.Attack, Rarity.Common, "守夜", 1, CardTarget.Enemy,
                "Damage=7;Draw=1", "Damage=10", "造成 {Damage} 伤害。清明：抽 {Draw} 张牌。", null, () => new SilverDagger());
            Def("C04", "镇定", CardType.Skill, Rarity.Common, "守夜", 0, CardTarget.None,
                "Restore=2", "Restore=3", "恢复 {Restore} 理智。消耗。", null, () => new Composure(), exhaust: true);
            Def("U02", "坚定信念", CardType.Skill, Rarity.Uncommon, "守夜", 2, CardTarget.None,
                "Block=12;Ward=2", "Block=16", "获得 {Block} 格挡。清明：获得 {Ward} 层心防。", null, () => new FirmBelief());
            Def("R05", "明理之刃", CardType.Attack, Rarity.Rare, "守夜", 2, CardTarget.Enemy,
                "Mult=3", "Mult=4", "造成等同于当前理智 ×{Mult} 的伤害。", null, () => new BladeOfReason());

            // ---------------- 堕落
            Def("C06", "梦魇之刃", CardType.Attack, Rarity.Common, "堕落", 1, CardTarget.Enemy,
                "Damage=8;MadDamage=14;SelfHp=2", "Damage=10;MadDamage=18",
                "造成 {Damage} 伤害。", "造成 {MadDamage} 伤害，失去 {SelfHp} 生命。", () => new NightmareBlade(), dual: true);
            Def("C07", "撕裂", CardType.Attack, Rarity.Common, "堕落", 0, CardTarget.Enemy,
                "Sanity=1;MadSanity=1;Damage=6;MadHits=2", "Damage=8",
                "代价{Sanity}。造成 {Damage} 伤害。", "代价{MadSanity}。造成 {Damage} 伤害 {MadHits} 次。", () => new Rend(), dual: true);
            Def("C08", "坠落", CardType.Skill, Rarity.Common, "堕落", 0, CardTarget.None,
                "Sanity=2;Energy=1;Draw=1", "Draw=2", "代价{Sanity}。获得 {Energy} 能量，抽 {Draw} 张牌。", null, () => new Plunge());
            Def("C09", "狂乱挥砍", CardType.Attack, Rarity.Common, "堕落", 1, CardTarget.Enemy,
                "Damage=4;Hits=2;MadSanity=1;MadHits=4", "Damage=5",
                "造成 {Damage} 伤害 {Hits} 次。", "代价{MadSanity}。造成 {Damage} 伤害 {MadHits} 次。", () => new FrenziedSlash(), dual: true);
            Def("C10", "扭曲躯壳", CardType.Skill, Rarity.Common, "堕落", 1, CardTarget.None,
                "Block=7;MadBlock=12;Discard=1", "Block=10;MadBlock=16",
                "获得 {Block} 格挡。", "获得 {MadBlock} 格挡，随机弃 {Discard} 张牌。", () => new TwistedHusk(), dual: true);
            Def("U09", "崩坏边缘", CardType.Attack, Rarity.Uncommon, "堕落", 1, CardTarget.Enemy,
                "Damage=4;PerPoint=2", "PerPoint=3", "造成 {Damage} 伤害。你每缺失 1 点理智，伤害 +{PerPoint}。", null, () => new BrinkOfCollapse());

            // ---------------- 传染
            Def("C11", "恐怖低语", CardType.Skill, Rarity.Common, "传染", 1, CardTarget.Enemy,
                "Fear=4;Draw=1", "Fear=6", "施加 {Fear} 恐惧，抽 {Draw} 张牌。", null, () => new TerrorWhisper());

            // ---------------- 状态 / 诅咒
            Def(CardIds.Whispers, "疯语", CardType.Curse, Rarity.Special, "诅咒", 0, CardTarget.None,
                "Loss=1", null, "不能打出。回合结束时若在手中，失去 {Loss} 理智。", null, () => new Whispers(), unplayable: true);
        }

        static void Def(string id, string name, CardType type, Rarity rarity, string archetype, int cost, CardTarget target,
                        string vars, string upgradeVars, string textSane, string textMad, Func<Card> factory,
                        bool dual = false, bool exhaust = false, bool unplayable = false, int upgradedCost = -1)
        {
            CardLibrary.Add(new CardDef
            {
                Id = id,
                Name = name,
                Type = type,
                Rarity = rarity,
                Archetype = archetype,
                Cost = cost,
                UpgradedCost = upgradedCost,
                Target = target,
                Dual = dual,
                Exhaust = exhaust,
                Unplayable = unplayable,
                Vars = CardLibrary.ParseVars(vars),
                UpgradeVars = CardLibrary.ParseVars(upgradeVars),
                TextSane = textSane,
                TextMad = textMad,
                Factory = factory,
            });
        }
    }

    // ==================== 基础 ====================

    public sealed class Strike : Card
    {
        public override void Play(PlayContext c) => c.Damage(V("Damage"));
    }

    public sealed class Defend : Card
    {
        public override void Play(PlayContext c) => c.Block(V("Block"));
    }

    public sealed class ForbiddenGlimpse : Card
    {
        public override void Play(PlayContext c) => c.Draw(c.Mad ? V("MadDraw") : V("Draw"));
    }

    // ==================== 守夜 ====================

    public sealed class Prayer : Card
    {
        public override void Play(PlayContext c)
        {
            c.Block(V("Block"));
            c.RestoreSanity(V("Restore"));
        }
    }

    public sealed class VigilLamp : Card
    {
        public override void Play(PlayContext c)
        {
            c.Block(V("Block"));
            c.IfClarity(x => x.Block(V("Bonus")));
        }
    }

    public sealed class SilverDagger : Card
    {
        public override void Play(PlayContext c)
        {
            c.Damage(V("Damage"));
            c.IfClarity(x => x.Draw(V("Draw")));
        }
    }

    public sealed class Composure : Card
    {
        public override void Play(PlayContext c) => c.RestoreSanity(V("Restore"));
    }

    public sealed class FirmBelief : Card
    {
        public override void Play(PlayContext c)
        {
            c.Block(V("Block"));
            c.IfClarity(x => x.Status(x.Player, StatusId.Ward, V("Ward")));
        }
    }

    public sealed class BladeOfReason : Card
    {
        public override void Play(PlayContext c) => c.Then(x => x.Damage(x.Player.Sanity * V("Mult")));
    }

    // ==================== 堕落 ====================

    public sealed class NightmareBlade : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad)
            {
                c.Damage(V("MadDamage"));
                c.LoseHp(V("SelfHp"));
            }
            else c.Damage(V("Damage"));
        }
    }

    public sealed class Rend : Card
    {
        public override void Play(PlayContext c) => c.Damage(V("Damage"), c.Mad ? V("MadHits") : 1);
    }

    public sealed class Plunge : Card
    {
        public override void Play(PlayContext c)
        {
            c.Energy(V("Energy"));
            c.Draw(V("Draw"));
        }
    }

    public sealed class FrenziedSlash : Card
    {
        public override void Play(PlayContext c) => c.Damage(V("Damage"), c.Mad ? V("MadHits") : V("Hits"));
    }

    public sealed class TwistedHusk : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad)
            {
                c.Block(V("MadBlock"));
                c.DiscardRandom(V("Discard"));
            }
            else c.Block(V("Block"));
        }
    }

    public sealed class BrinkOfCollapse : Card
    {
        public override void Play(PlayContext c) =>
            c.Then(x => x.Damage(V("Damage") + x.Player.MissingSanity * V("PerPoint")));
    }

    // ==================== 传染 ====================

    public sealed class TerrorWhisper : Card
    {
        public override void Play(PlayContext c)
        {
            c.Fear(V("Fear"));
            c.Draw(V("Draw"));
        }
    }

    // ==================== 诅咒 ====================

    public sealed class Whispers : Card
    {
        public override void Play(PlayContext c) { }

        public override void OnTurnEndInHand(BattleEngine e) =>
            e.Queue.AddToBottom(new Actions.LoseSanityAction(V("Loss"), SanitySource.Curse));
    }
}
