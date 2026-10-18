using System;
using System.Collections.Generic;

namespace Deeptide.Core.Cards
{
    /// <summary>
    /// 卡牌类注册表（策划/09 M2.4）：cards.csv 的每一行按 ID 找到这里的工厂。
    /// 用显式的表而不是反射扫描，避免 Unity IL2CPP 打包时被代码裁剪；数据校验会检查表和 CSV 一一对应。
    /// </summary>
    public static class CardClasses
    {
        public static readonly IReadOnlyDictionary<string, Func<Card>> Factories = new Dictionary<string, Func<Card>>
        {
            // 基础
            ["B01"] = () => new Strike(),
            ["B02"] = () => new Defend(),
            ["B03"] = () => new ForbiddenGlimpse(),
            // 守夜
            ["C01"] = () => new Prayer(),
            ["C02"] = () => new VigilLamp(),
            ["C03"] = () => new SilverDagger(),
            ["C04"] = () => new Composure(),
            ["U02"] = () => new FirmBelief(),
            ["R05"] = () => new BladeOfReason(),
            // 堕落
            ["C06"] = () => new NightmareBlade(),
            ["C07"] = () => new Rend(),
            ["C08"] = () => new Plunge(),
            ["C09"] = () => new FrenziedSlash(),
            ["C10"] = () => new TwistedHusk(),
            ["U09"] = () => new BrinkOfCollapse(),
            // 传染
            ["C11"] = () => new TerrorWhisper(),
            // 状态 / 诅咒
            [CardIds.Whispers] = () => new Whispers(),
        };
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
