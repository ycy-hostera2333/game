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
            // 狼人（策划/14 §2.5）
            ["LB01"] = () => new Fangs(),
            ["LB02"] = () => new HowlAtTheMoon(),
            ["LB03"] = () => new SilverChain(),
            ["LC01"] = () => new WolfBite(),
            ["LC02"] = () => new ThroatCut(),
            ["LC03"] = () => new ScentOfBlood(),
            ["LC04"] = () => new LickWounds(),
            ["LC05"] = () => new RakingClaws(),
            ["LC06"] = () => new Pounce(),
            ["LC07"] = () => new RampageSweep(),
            ["LC08"] = () => new LowGrowl(),
            ["LC09"] = () => new SuppressTheBeast(),
            ["LC10"] = () => new Moonlight(),
            ["LC11"] = () => new ScentTrail(),
            ["LC12"] = () => new LieInWait(),
            ["LU01"] = () => new Regeneration(),
            ["LU02"] = () => new Gnaw(),
            ["LU03"] = () => new Shred(),
            ["LU04"] = () => new PackHunt(),
            ["LU05"] = () => new Feral(),
            ["LU06"] = () => new DualAspect(),
            ["LU07"] = () => new MoonlitTwins(),
            ["LU08"] = () => new FaceInTheMirror(),
            ["LU09"] = () => new Bloodthirst(),
            ["LU10"] = () => new OpenTheArtery(),
            ["LU11"] = () => new BloodFeast(),
            ["LR01"] = () => new EternalNight(),
            ["LR02"] = () => new ManAndWolf(),
            ["LR03"] = () => new BarbedClaws(),
            ["LR04"] = () => new Gorge(),
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

    /// <summary>U12 心智崩塌：伤害 = 目标当前恐惧 × 倍率，然后清空它的恐惧（一次性兑现，意志不变；03 §4）。</summary>
    public sealed class MindCollapse : Card
    {
        public override void Play(PlayContext c) => c.Then(x =>
        {
            var t = x.Target;
            x.Damage(t.Fear * V("Mult"));
            x.Enqueue(new DelegateAction(e => { if (!t.IsDead) e.SetFear(t, 0, t.Will); }));
        });

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

    /// <summary>
    /// U07 疯狂本能（能力）：每因代价或自身效果失去 1 点理智，对随机敌人造成效果伤害（每点单独选目标）。
    /// 敌人、光环、侵蚀、疯语造成的理智损失不触发，否则削理智的敌人反而在替堕落流打伤害（策划/07 §8.5）。
    /// </summary>
    public sealed class MadInstinct : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new MadInstinctPower(), V("Hit"));
    }

    public sealed class MadInstinctPower : Power
    {
        public override void OnSanityLost(BattleEngine e, int amount, SanitySource source)
        {
            if (source != SanitySource.Card && source != SanitySource.Effect) return;
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

        List<ChoiceOption> Offer(BattleEngine e, int count)
        {
            // 只从这张牌所属角色的罕见牌里抽（策划/14 §1.1）
            var pool = CardLibrary.All.Where(d => d.Rarity == Rarity.Uncommon && d.Id != Def.Id && d.Character == Def.Character).ToList();
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

    // ==================== 狼人（策划/14 §2.5、09 M6.3） ====================
    // 兽形的每段 +2、打出的牌格挡减半由特性「兽血」（BeastBlood）在伤害 / 格挡公式里处理，牌只管自己的效果。
    // 人形面 / 兽形面就是清醒面 / 癫狂面（c.Mad）。「若因此变身 / 复归」看本轮标记在这张牌结算前后有没有变（策划/14 §1.2）。

    /// <summary>LB01 ◐ 獠牙：人形面伤害 + 格挡；兽形面两段伤害。</summary>
    public sealed class Fangs : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad) c.Damage(V("Damage"), V("MadHits"));
            else
            {
                c.Damage(V("Damage"));
                c.Block(V("Block"));
            }
        }
    }

    /// <summary>LB02 嚎月：代价2（引擎支付），抽牌。第一回合用它变身。</summary>
    public sealed class HowlAtTheMoon : Card
    {
        public override void Play(PlayContext c) => c.Draw(V("Draw"));
    }

    /// <summary>LB03 银链：先恢复理智（复归回人形），再拿格挡，所以格挡是满的。</summary>
    public sealed class SilverChain : Card
    {
        public override void Play(PlayContext c)
        {
            c.RestoreSanity(V("Restore"));
            c.Block(V("Block"));
        }
    }

    /// <summary>LC01 撕咬：伤害 + 流血。</summary>
    public sealed class WolfBite : Card
    {
        public override void Play(PlayContext c)
        {
            c.Damage(V("Damage"));
            c.Status(c.Target, StatusId.Bleed, V("Bleed"));
        }
    }

    /// <summary>LC02 ◐ 割喉：人形面单纯伤害；兽形面伤害少一些、施加流血。</summary>
    public sealed class ThroatCut : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad)
            {
                c.Damage(V("MadDamage"));
                c.Status(c.Target, StatusId.Bleed, V("MadBleed"));
            }
            else c.Damage(V("Damage"));
        }
    }

    /// <summary>LC03 血腥气味：对所有敌人施加流血。</summary>
    public sealed class ScentOfBlood : Card
    {
        public override void Play(PlayContext c) => c.StatusAll(StatusId.Bleed, V("Bleed"));
    }

    /// <summary>LC04 ◐ 舔舐伤口：人形面格挡；兽形面回血、抽牌（兽形格挡减半，所以换成回血）。</summary>
    public sealed class LickWounds : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad)
            {
                c.Enqueue(new HealAction(c.Player, V("Heal")));
                c.Draw(V("Draw"));
            }
            else c.Block(V("Block"));
        }
    }

    /// <summary>LC05 连爪 / LU02 啃噬（代价由引擎支付）/ LU03 撕碎：多段攻击，兽形每段 +2。</summary>
    public sealed class RakingClaws : Card
    {
        public override void Play(PlayContext c) => c.Damage(V("Damage"), V("Hits"));
    }

    public sealed class Gnaw : Card
    {
        public override void Play(PlayContext c) => c.Damage(V("Damage"), V("Hits"));
    }

    public sealed class Shred : Card
    {
        public override void Play(PlayContext c) => c.Damage(V("Damage"), V("Hits"));
    }

    /// <summary>LC06 ◐ 扑杀：人形面一下重击；兽形面两段（吃两次 +2）。</summary>
    public sealed class Pounce : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad) c.Damage(V("MadDamage"), V("MadHits"));
            else c.Damage(V("Damage"));
        }
    }

    /// <summary>LC07 狂暴横扫：全体多段。</summary>
    public sealed class RampageSweep : Card
    {
        public override void Play(PlayContext c) => c.DamageAll(V("Damage"), V("Hits"));
    }

    /// <summary>LC08 低嗥：代价1；若这次代价让你变身（本轮第一次），获得能量（升级再抽牌），否则抽牌。</summary>
    public sealed class LowGrowl : Card
    {
        public override void Play(PlayContext c)
        {
            bool before = c.Engine.State.TransformedThisRound;   // 代价还没付（排在效果之前），这里记下付之前的样子
            c.Then(x =>
            {
                if (!before && x.Engine.State.TransformedThisRound)
                {
                    x.Energy(V("Energy"));
                    if (V("BonusDraw") > 0) x.Draw(V("BonusDraw"));
                }
                else x.Draw(V("Draw"));
            });
        }
    }

    /// <summary>LC09 压抑兽性：恢复理智；若因此复归（本轮第一次），抽牌。</summary>
    public sealed class SuppressTheBeast : Card
    {
        public override void Play(PlayContext c)
        {
            bool before = c.Engine.State.RevertedThisRound;
            c.RestoreSanity(V("Restore"));
            c.Then(x => { if (!before && x.Engine.State.RevertedThisRound) x.Draw(V("Draw")); });
        }
    }

    /// <summary>LC10 ◐ 月光：人形面先挡再失去理智（往下跨）；兽形面先恢复理智再挡（往上跨，格挡是满的）。</summary>
    public sealed class Moonlight : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad)
            {
                c.RestoreSanity(V("Restore"));
                c.Block(V("Block"));
            }
            else
            {
                c.Block(V("Block"));
                c.LoseSanity(V("Loss"));
            }
        }
    }

    /// <summary>LC11 ◐ 嗅迹：人形面抽牌再弃 1 张；兽形面少抽一张，但本回合看清敌人意图（用鼻子代替眼睛）。</summary>
    public sealed class ScentTrail : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad)
            {
                c.Draw(V("MadDraw"));
                c.Enqueue(new DelegateAction(e => e.SetIntentsRevealed(true)));
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

    /// <summary>LC12 伺机：保留，格挡。兽形时拿着不打，等复归了再打满额。</summary>
    public sealed class LieInWait : Card
    {
        public override void Play(PlayContext c) => c.Block(V("Block"));
    }

    /// <summary>LU01 再生（能力）：回合结束时若处于兽形，恢复生命。</summary>
    public sealed class Regeneration : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new RegenerationPower(), V("Heal"));
    }

    public sealed class RegenerationPower : Power
    {
        public override void OnPlayerTurnEnd(BattleEngine e) =>
            e.Queue.AddToBottom(new DelegateAction(en =>
            {
                if (en.Player.IsLunatic) en.Queue.AddToTop(new HealAction(en.Player, Amount));
            }));
    }

    /// <summary>LU04 围猎：本回合每打出过一张其他攻击牌，多打一段（结算时数）。</summary>
    public sealed class PackHunt : Card
    {
        public override void Play(PlayContext c) => c.Then(x => x.Damage(V("Damage"), x.Engine.State.AttacksPlayedThisTurn));

        // 还没打出时，计数里没有这张自己
        public override (int PerHit, int Hits)? PreviewAttack(BattleEngine e, bool mad, EnemyState target) =>
            (DamageCalculator.Compute(e, e.Player, target, V("Damage")), e.State.AttacksPlayedThisTurn + (Pile == PileKind.Play ? 0 : 1));

        protected override string PreviewNote(BattleEngine e, bool mad, EnemyState target) =>
            $"（当前 {PreviewAttack(e, mad, target).Value.Hits} 段）";
    }

    /// <summary>LU05 狂性（能力）：每轮第一次变身时抽牌。</summary>
    public sealed class Feral : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new FeralPower(), V("Draw"));
    }

    public sealed class FeralPower : Power
    {
        public override void OnTransform(BattleEngine e)
        {
            e.Log.Add(BattleEventType.PowerTriggered, $"狂性：变身，抽 {Amount} 张牌");
            e.Queue.AddToTop(new DrawCardsAction(Amount));
        }
    }

    /// <summary>LU06 双相（能力）：每轮第一次复归时获得格挡（能力给的，不减半、不吃敏捷）。</summary>
    public sealed class DualAspect : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new DualAspectPower(), V("Guard"));
    }

    public sealed class DualAspectPower : Power
    {
        public override void OnRevert(BattleEngine e)
        {
            e.Log.Add(BattleEventType.PowerTriggered, $"双相：复归，获得 {Amount} 格挡");
            e.Queue.AddToTop(new GainBlockAction(e.Player, Amount, false));
        }
    }

    /// <summary>LU07 月下双生：本轮每变身、复归过一次，多打一段（最多三段，结算时数）。</summary>
    public sealed class MoonlitTwins : Card
    {
        static int Hits(BattleEngine e) => 1 + (e.State.TransformedThisRound ? 1 : 0) + (e.State.RevertedThisRound ? 1 : 0);

        public override void Play(PlayContext c) => c.Then(x => x.Damage(V("Damage"), Hits(x.Engine)));

        public override (int PerHit, int Hits)? PreviewAttack(BattleEngine e, bool mad, EnemyState target) =>
            (DamageCalculator.Compute(e, e.Player, target, V("Damage")), Hits(e));

        protected override string PreviewNote(BattleEngine e, bool mad, EnemyState target) => $"（当前 {Hits(e)} 段）";
    }

    /// <summary>
    /// LU08 ◐ 镜中之脸：人形面理智掉到癫狂线（4），抽牌；兽形面理智回到癫狂线上一格（5），再挡。
    /// 一定会跨线，所以它是最稳的变身 / 复归开关。数值按结算时的理智算。
    /// </summary>
    public sealed class FaceInTheMirror : Card
    {
        public override void Play(PlayContext c)
        {
            if (c.Mad)
            {
                c.Then(x => x.RestoreSanity(x.Player.LunacyThreshold + 1 - x.Player.Sanity));
                c.Block(V("Block"));
            }
            else
            {
                c.Then(x => x.LoseSanity(Math.Max(0, x.Player.Sanity - x.Player.LunacyThreshold)));
                c.Draw(V("Draw"));
            }
        }
    }

    /// <summary>LU09 嗜血（能力）：每当有敌人因流血失去生命，恢复生命。</summary>
    public sealed class Bloodthirst : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new BloodthirstPower(), V("Heal"));
    }

    public sealed class BloodthirstPower : Power
    {
        // 当场回血、不排队：流血打死最后一个敌人时战斗立刻结束、队列清空，排队的回血会丢（和饱餐同理）
        public override void OnEnemyBled(BattleEngine e, EnemyState enemy, int amount) => e.Heal(e.Player, Amount);
    }

    /// <summary>LU10 撕开动脉：目标的流血翻倍（结算时取值）。</summary>
    public sealed class OpenTheArtery : Card
    {
        public override void Play(PlayContext c) => c.Then(x =>
        {
            int bleed = x.Target.Statuses.Get(StatusId.Bleed);
            if (bleed > 0) x.Status(x.Target, StatusId.Bleed, bleed);
        });

        protected override string PreviewNote(BattleEngine e, bool mad, EnemyState target) =>
            target == null ? null : $"（{target.Statuses.Get(StatusId.Bleed)} → {target.Statuses.Get(StatusId.Bleed) * 2}）";
    }

    /// <summary>LU11 血之狂宴：全体伤害，然后每有一个敌人带着流血就抽牌。</summary>
    public sealed class BloodFeast : Card
    {
        public override void Play(PlayContext c)
        {
            c.DamageAll(V("Damage"));
            c.Then(x =>
            {
                int bleeding = x.Engine.State.AliveEnemies.Count(en => en.Statuses.Get(StatusId.Bleed) > 0);
                if (bleeding > 0) x.Draw(bleeding * V("Draw"));
            });
        }
    }

    /// <summary>LR01 永夜（能力）：理智不会降到 Floor 以下，所以不会崩溃。再打一张不会把下限抬高。</summary>
    public sealed class EternalNight : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new EternalNightPower { Floor = V("Floor") }, 1);
    }

    public sealed class EternalNightPower : Power
    {
        public int Floor;

        public override int SanityFloor(BattleEngine e) => Floor;
    }

    /// <summary>LR02 人狼同体（能力）：双面牌两面都结算（引擎按 ResolvesBothFaces 处理，策划/14 §2.5 结算细则）。</summary>
    public sealed class ManAndWolf : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new ManAndWolfPower(), 1);
    }

    public sealed class ManAndWolfPower : Power
    {
        public override bool ResolvesBothFaces(BattleEngine e, Card card) => true;
    }

    /// <summary>LR03 倒刺爪（能力）：你的攻击每有一段打穿格挡，给那个敌人施加流血。</summary>
    public sealed class BarbedClaws : Card
    {
        public override void Play(PlayContext c) => c.ApplyPower(new BarbedClawsPower(), V("Bleed"));
    }

    public sealed class BarbedClawsPower : Power
    {
        public override void OnAttackHit(BattleEngine e, Combatant source, Combatant target, int hpLoss)
        {
            if (source != e.Player || hpLoss <= 0 || !(target is EnemyState en) || en.IsDead) return;
            e.Queue.AddToTop(new ApplyStatusAction(en, StatusId.Bleed, Amount));
        }
    }

    /// <summary>
    /// LR04 饱餐：伤害；若这一下打死了目标（召唤物除外），最大生命永久增加。
    /// 加生命和伤害放在同一个动作里：打死最后一个敌人时战斗立刻结束，后面排着的效果会作废（策划/02 §3.5）。
    /// </summary>
    public sealed class Gorge : Card
    {
        public override void Play(PlayContext c)
        {
            var target = c.Target;
            c.Enqueue(new DelegateAction(e =>
            {
                if (target == null || target.IsDead) return;
                int hpLoss = e.ApplyDamage(e.Player, target, DamageCalculator.Compute(e, e.Player, target, V("Damage")));
                e.AttackHit(e.Player, target, hpLoss);
                if (target.IsDead && !target.Minion) e.GainMaxHp(V("MaxHp"));
            }));
        }
    }
}
