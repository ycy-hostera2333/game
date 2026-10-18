using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Actions;

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
            // 普通
            ["C01"] = () => new Prayer(),
            ["C02"] = () => new VigilLamp(),
            ["C03"] = () => new SilverDagger(),
            ["C04"] = () => new Composure(),
            ["C05"] = () => new HolyWaterSplash(),
            ["C06"] = () => new NightmareBlade(),
            ["C07"] = () => new Rend(),
            ["C08"] = () => new Plunge(),
            ["C09"] = () => new FrenziedSlash(),
            ["C10"] = () => new TwistedHusk(),
            ["C11"] = () => new TerrorWhisper(),
            ["C12"] = () => new ThingInTheShadows(),
            ["C13"] = () => new Scream(),
            ["C14"] = () => new RevolverShot(),
            ["C15"] = () => new Research(),
            ["C16"] = () => new LightTheLamp(),
            // 罕见
            ["U01"] = () => new HolySigil(),
            ["U02"] = () => new FirmBelief(),
            ["U03"] = () => new Dispel(),
            ["U04"] = () => new SilentPrayer(),
            ["U05"] = () => new WatchersOath(),
            ["U06"] = () => new AbyssalOffering(),
            ["U07"] = () => new MadInstinct(),
            ["U08"] = () => new TentacleLash(),
            ["U09"] = () => new BrinkOfCollapse(),
            ["U10"] = () => new MadLaughter(),
            ["U11"] = () => new SourceOfFear(),
            ["U12"] = () => new MindCollapse(),
            ["U13"] = () => new GazeIntoTheAbyss(),
            ["U14"] = () => new Petrify(),
            ["U15"] = () => new ForbiddenPage(),
            // 稀有
            ["R01"] = () => new GazeOfTheOldOnes(),
            ["R02"] = () => new Stigmata(),
            ["R03"] = () => new FinalRite(),
            ["R04"] = () => new MassHysteria(),
            ["R05"] = () => new BladeOfReason(),
            ["R06"] = () => new MindAnchor(),
            // 状态 / 诅咒
            [CardIds.Whispers] = () => new Whispers(),
            [CardIds.Web] = () => new Web(),
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
            c.IfClarity(x => x.Block(V("BonusBlock")));
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

        public override (int PerHit, int Hits)? PreviewAttack(BattleEngine e, bool mad, EnemyState target) =>
            (DamageCalculator.Compute(e, e.Player, target, e.Player.Sanity * V("Mult")), 1);

        protected override string PreviewNote(BattleEngine e, bool mad, EnemyState target) =>
            $"（当前 {PreviewAttack(e, mad, target).Value.PerHit}）";
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

        public override (int PerHit, int Hits)? PreviewAttack(BattleEngine e, bool mad, EnemyState target) =>
            (DamageCalculator.Compute(e, e.Player, target, V("Damage") + e.Player.MissingSanity * V("PerPoint")), 1);

        protected override string PreviewNote(BattleEngine e, bool mad, EnemyState target) =>
            $"（当前 {PreviewAttack(e, mad, target).Value.PerHit}）";
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

    /// <summary>S02 缠网：网缚渔夫塞进来的状态牌，1 费消耗、没有效果。打掉它能降低收网的伤害。</summary>
    public sealed class Web : Card
    {
        public override void Play(PlayContext c) { }
    }

    // ==================== 批次 3.1：守夜 + 传染 ====================

    /// <summary>C05 圣水泼洒：对所有敌人造成伤害；清明时再对所有敌人施加虚弱。</summary>
    public sealed class HolyWaterSplash : Card
    {
        public override void Play(PlayContext c)
        {
            c.DamageAll(V("Damage"));
            c.IfClarity(x => x.StatusAll(StatusId.Weak, V("Weak")));
        }
    }

    /// <summary>C12 ◐ 影中之物：伤害 + 恐惧；癫狂面恐惧翻倍、自伤。</summary>
    public sealed class ThingInTheShadows : Card
    {
        public override void Play(PlayContext c)
        {
            c.Damage(V("Damage"));
            if (c.Mad)
            {
                c.Fear(V("MadFear"));
                c.LoseHp(V("SelfHp"));
            }
            else c.Fear(V("Fear"));
        }
    }

    /// <summary>C13 ◐ 惊叫：格挡 + 全体恐惧；癫狂面放弃格挡换更多恐惧。</summary>
    public sealed class Scream : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad) c.FearAll(V("MadFear"));
            else
            {
                c.Block(V("Block"));
                c.FearAll(V("Fear"));
            }
        }
    }

    /// <summary>U01 圣徽（能力）：每恢复 1 点理智（含溢出）获得格挡。能力给的格挡不是卡牌格挡，不吃敏捷 / 脆弱。</summary>
    public sealed class HolySigil : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new HolySigilPower(), V("Guard"));
    }

    public sealed class HolySigilPower : Power
    {
        public override void OnSanityRestored(BattleEngine e, int amount, int overflow)
        {
            if (amount <= 0) return;
            e.Queue.AddToTop(new GainBlockAction(e.Player, amount * Amount, false));
        }
    }

    /// <summary>U03 驱散：清明时改为造成更高的伤害（结算到这一句时判定）。</summary>
    public sealed class Dispel : Card
    {
        public override void Play(PlayContext c) => c.Then(x => x.Damage(x.Player.IsClarity ? V("ClarityDamage") : V("Damage")));

        public override (int PerHit, int Hits)? PreviewAttack(BattleEngine e, bool mad, EnemyState target) =>
            (DamageCalculator.Compute(e, e.Player, target, e.Player.IsClarity ? V("ClarityDamage") : V("Damage")), 1);
    }

    /// <summary>U04 默祷：理智恢复到上限（已满时不算恢复）。</summary>
    public sealed class SilentPrayer : Card
    {
        public override void Play(PlayContext c) => c.Then(x => x.RestoreSanity(x.Player.MaxSanity - x.Player.Sanity));
    }

    /// <summary>U05 守夜人之誓（能力）：回合结束时若清明，获得力量。</summary>
    public sealed class WatchersOath : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new WatchersOathPower(), V("Strength"));
    }

    public sealed class WatchersOathPower : Power
    {
        public override void OnPlayerTurnEnd(BattleEngine e) =>
            e.Queue.AddToBottom(new DelegateAction(en =>
            {
                if (en.Player.IsClarity) en.Queue.AddToTop(new ApplyStatusAction(en.Player, StatusId.Strength, Amount));
            }));
    }

    /// <summary>U11 恐惧之源（能力）：回合结束时对所有敌人施加恐惧。</summary>
    public sealed class SourceOfFear : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new SourceOfFearPower(), V("Fear"));
    }

    public sealed class SourceOfFearPower : Power
    {
        public override void OnPlayerTurnEnd(BattleEngine e) => e.Queue.AddToBottom(new FearAllEnemiesAction(Amount));
    }

    /// <summary>U12 心智崩塌：伤害 = 目标当前恐惧 × 倍率（不清除恐惧）。</summary>
    public sealed class MindCollapse : Card
    {
        public override void Play(PlayContext c) => c.Then(x => x.Damage(x.Target.Fear * V("Mult")));

        public override (int PerHit, int Hits)? PreviewAttack(BattleEngine e, bool mad, EnemyState target) =>
            (DamageCalculator.Compute(e, e.Player, target, (target?.Fear ?? 0) * V("Mult")), 1);

        protected override string PreviewNote(BattleEngine e, bool mad, EnemyState target) =>
            target == null ? null : $"（当前 {PreviewAttack(e, mad, target).Value.PerHit}）";
    }

    /// <summary>U13 ◐ 凝视深渊：施加恐惧；癫狂面加倍并付代价。</summary>
    public sealed class GazeIntoTheAbyss : Card
    {
        public override void Play(PlayContext c) => c.Fear(c.Mad ? V("MadFear") : V("Fear"));
    }

    /// <summary>U14 惊惧失神：目标恐惧 ≥ 意志的一半时直接震慑（照常清空恐惧、提升意志）。</summary>
    public sealed class Petrify : Card
    {
        public override void Play(PlayContext c)
        {
            var t = c.Target;
            c.Enqueue(new DelegateAction(e =>
            {
                if (t == null || t.IsDead || t.Stunned) return;
                if (!t.Mindless && t.Fear * 2 >= t.Will) ApplyFearAction.Stun(e, t);
                else e.Log.Add(BattleEventType.PowerTriggered, $"{t.Name} 的恐惧还不够（{t.Fear}/{t.Will}），惊惧失神没有生效").Entity = t.Index;
            }));
        }
    }

    /// <summary>R02 圣痕（能力）：恢复理智时，每溢出 1 点，对所有敌人造成效果伤害。</summary>
    public sealed class Stigmata : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new StigmataPower(), V("Burst"));
    }

    public sealed class StigmataPower : Power
    {
        public override void OnSanityRestored(BattleEngine e, int amount, int overflow)
        {
            if (overflow <= 0) return;
            e.Log.Add(BattleEventType.PowerTriggered, $"圣痕：溢出 {overflow} 点理智");
            e.Queue.AddToTop(new DamageAllEnemiesAction(e.Player, overflow * Amount, 1, DamageKind.Effect));
        }
    }

    /// <summary>R04 群体癔症：对所有敌人施加等同于已缺失理智的恐惧（结算时取值）。</summary>
    public sealed class MassHysteria : Card
    {
        public override void Play(PlayContext c) => c.Then(x => x.FearAll(x.Player.MissingSanity));

        protected override string PreviewNote(BattleEngine e, bool mad, EnemyState target) => $"（当前 {e.Player.MissingSanity}）";
    }

    // ==================== 批次 3.2：堕落 + 通用 ====================

    /// <summary>C14 左轮射击：本回合第一张打出的牌时，再造成一次伤害（两次数值相同）。</summary>
    public sealed class RevolverShot : Card
    {
        public override void Play(PlayContext c)
        {
            c.Damage(V("Damage"));
            if (c.Engine.State.CardsPlayedThisTurn == 1) c.Damage(V("BonusDamage"));
        }

        public override (int PerHit, int Hits)? PreviewAttack(BattleEngine e, bool mad, EnemyState target) =>
            (DamageCalculator.Compute(e, e.Player, target, V("Damage")), e.State.CardsPlayedThisTurn == 0 ? 2 : 1);
    }

    /// <summary>C15 ◐ 查阅资料：抽牌后选 1 张弃掉；癫狂面多抽、不弃但付代价。</summary>
    public sealed class Research : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad)
            {
                c.Draw(V("MadDraw"));
                return;
            }
            c.Draw(V("Draw"));
            for (int i = 0; i < V("Discard"); i++)
                c.Choose("弃 1 张牌", ChooseAction.HandOptions, (e, o) =>
                {
                    var card = e.FindCard(o.CardUid);
                    if (card != null) e.Queue.AddToTop(new MoveCardAction(card, PileKind.Discard, "弃掉"));
                });
        }
    }

    /// <summary>C16 点亮提灯：格挡、抽牌，本回合敌人意图不受癫狂模糊。</summary>
    public sealed class LightTheLamp : Card
    {
        public override void Play(PlayContext c)
        {
            c.Block(V("Block"));
            c.Draw(V("Draw"));
            c.Enqueue(new DelegateAction(e => e.SetIntentsRevealed(true)));
        }
    }

    /// <summary>U06 深渊献礼：付理智换力量。</summary>
    public sealed class AbyssalOffering : Card
    {
        public override void Play(PlayContext c) => c.Status(c.Player, StatusId.Strength, V("Strength"));
    }

    /// <summary>U07 疯狂本能（能力）：每失去 1 点理智，对随机敌人造成效果伤害（每点单独选目标）。</summary>
    public sealed class MadInstinct : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new MadInstinctPower(), V("Hit"));
    }

    public sealed class MadInstinctPower : Power
    {
        public override void OnSanityLost(BattleEngine e, int amount, SanitySource source)
        {
            var hits = Enumerable.Range(0, amount)
                .Select(_ => (GameAction)new DamageRandomEnemyAction(e.Player, Amount, DamageKind.Effect)).ToArray();
            e.Queue.AddToTop(hits);
        }
    }

    /// <summary>U08 ◐ 触须鞭笞：单体伤害 + 易伤；癫狂面变成全体并自伤。</summary>
    public sealed class TentacleLash : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad)
            {
                c.DamageAll(V("Damage"));
                c.StatusAll(StatusId.Vulnerable, V("Vulnerable"));
                c.LoseHp(V("SelfHp"));
            }
            else
            {
                c.Damage(V("Damage"));
                c.Status(c.Target, StatusId.Vulnerable, V("Vulnerable"));
            }
        }
    }

    /// <summary>U10 ◐ 狂笑：获得能量（升级后清醒面再抽 1）；癫狂面付代价换更多能量。</summary>
    public sealed class MadLaughter : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad)
            {
                c.Energy(V("MadEnergy"));
                return;
            }
            c.Energy(V("Energy"));
            if (V("Draw") > 0) c.Draw(V("Draw"));
        }
    }

    /// <summary>U15 禁书残页：从随机 3 张罕见牌（不含自己）里选 1 张加入手牌，本回合 0 费。随机用 misc 流，结算时才抽。</summary>
    public sealed class ForbiddenPage : Card
    {
        public override void Play(PlayContext c) =>
            c.Choose("选 1 张加入手牌（本回合费用为 0）", e => Offer(e, V("Choices")), (e, o) =>
                e.Queue.AddToTop(new AddCardToPileAction(o.CardId, PileKind.Hand, 0)));

        static List<ChoiceOption> Offer(BattleEngine e, int count)
        {
            var pool = CardLibrary.All.Where(d => d.Rarity == Rarity.Uncommon && d.Id != "U15").ToList();
            var options = new List<ChoiceOption>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = e.Rng.Misc.NextInt(pool.Count);
                var def = pool[k];
                pool.RemoveAt(k);
                var preview = CardLibrary.Create(def.Id, 0);
                options.Add(new ChoiceOption { Label = $"{def.Name}：{preview.Describe(false)}", CardId = def.Id });
            }
            return options;
        }
    }

    /// <summary>R01 旧日注视（能力）：双面牌始终使用癫狂面（代价照付）。</summary>
    public sealed class GazeOfTheOldOnes : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new GazeOfTheOldOnesPower(), 1);
    }

    public sealed class GazeOfTheOldOnesPower : Power
    {
        public override bool ForcesMadFace(BattleEngine e, Card card) => true;
    }

    /// <summary>R03 终末仪式：X 费、代价 X，对所有敌人造成伤害 X 次。</summary>
    public sealed class FinalRite : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.X > 0) c.DamageAll(V("Damage"), c.X);
        }
    }

    /// <summary>R06 心灵之锚（能力）：每个玩家回合开始时（抽牌之前）二选一：恢复理智，或失去理智。</summary>
    public sealed class MindAnchor : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new MindAnchorPower(), V("Shift"));
    }

    public sealed class MindAnchorPower : Power
    {
        public override void OnPlayerTurnStart(BattleEngine e)
        {
            int n = Amount;
            e.Queue.AddToBottom(new ChooseAction("心灵之锚", _ => new List<ChoiceOption>
            {
                new ChoiceOption { Label = $"恢复 {n} 理智", Value = 0 },
                new ChoiceOption { Label = $"失去 {n} 理智", Value = 1 },
            }, (en, o) => en.Queue.AddToTop(o.Value == 0
                ? (GameAction)new RestoreSanityAction(n)
                : new LoseSanityAction(n, SanitySource.Effect))));
        }
    }
}
