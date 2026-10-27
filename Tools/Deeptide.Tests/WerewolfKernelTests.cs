using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Data;
using Deeptide.Core.Enemies;
using Deeptide.Core.Map;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;
using Deeptide.Tools;
using NUnit.Framework;
using static Deeptide.Tests.TestUtil;

namespace Deeptide.Tests
{
    /// <summary>
    /// 狼人的内核规则（策划/09 M6.2、14 §1–§2）：每轮第一次变身 / 复归、兽形修正（每段 +2、打出的牌格挡减半）、流血、人狼同体。
    /// 狼人的牌和遗物还没进表（6.3），这里用调查员的牌和测试监听器来驱动规则。
    /// </summary>
    public class WerewolfKernelTests
    {
        const string Werewolf = "werewolf";

        /// <summary>狼人的一场战斗：上限 6，挂兽血特性，没有起始遗物（调查笔记会抽牌，干扰手牌）。</summary>
        static BattleEngine Wolf(string[] enemies = null, BattleConfig config = null, bool trait = true) =>
            BattleEngine.Start(new BattleSetup
            {
                Seed = 1,
                Deck = Enumerable.Repeat("B02", 10).ToList(),
                Enemies = (enemies ?? new[] { EnemyIds.Dummy }).ToList(),
                MaxHp = 75,
                Hp = 75,
                MaxSanity = 6,
                Relics = new List<string>(),
                Traits = trait ? new List<string> { RelicIds.BeastBlood } : new List<string>(),
                Config = config ?? new BattleConfig(),
            });

        /// <summary>记下收到的通知，测试用来数「变身时 / 复归时」触发了几次。</summary>
        sealed class Probe : BattleListener
        {
            public int Transforms, Reverts, Entered, Left;
            public bool BothFaces;
            public readonly List<int> Bled = new List<int>();
            public override void OnTransform(BattleEngine e) => Transforms++;
            public override void OnRevert(BattleEngine e) => Reverts++;
            public override void OnLunacyChanged(BattleEngine e, bool entered) { if (entered) Entered++; else Left++; }
            public override void OnEnemyBled(BattleEngine e, EnemyState enemy, int amount) => Bled.Add(amount);
            public override bool ResolvesBothFaces(BattleEngine e, Card card) => BothFaces;
        }

        static Probe Listen(BattleEngine e)
        {
            var probe = new Probe();
            e.AddListener(probe);
            return probe;
        }

        static List<int> HitsOn(BattleEngine e, int from, int enemy = 0) =>
            e.Log.Events.Skip(from).Where(x => x.Type == BattleEventType.Damage && x.Entity == enemy).Select(x => x.Amount).ToList();

        // ---------------- 兽形修正

        [Test]
        public void BeastForm_AddsTwoPerHit_AndPreviewMatches()
        {
            var e = Wolf();
            var target = e.State.Enemies[0];
            SetHand(e, "B01", "B01");
            Assert.That(e.Player.Hand[0].PreviewValue(e, "Damage", target), Is.EqualTo(6), "人形：没有加成");
            int mark = e.Log.Events.Count;
            Play(e, 0);
            Assert.That(HitsOn(e, mark), Is.EqualTo(new[] { 6 }));

            e.SetSanity(4);   // 兽形
            Assert.That(e.Player.Hand[0].PreviewValue(e, "Damage", target), Is.EqualTo(8), "卡面预览和实际一致");
            mark = e.Log.Events.Count;
            Play(e, 0);
            Assert.That(HitsOn(e, mark), Is.EqualTo(new[] { 8 }));

            // 多段：每段都加。狂乱挥砍的癫狂面 4 × 4，兽形 6 × 4
            SetHand(e, "C09");
            e.SetEnergy(3);
            mark = e.Log.Events.Count;
            Play(e, 0);
            Assert.That(HitsOn(e, mark), Is.EqualTo(new[] { 6, 6, 6, 6 }));

            // 调查员（没有兽血）癫狂时不加
            var inv = Wolf(trait: false);
            inv.SetSanity(4);
            SetHand(inv, "B01");
            mark = inv.Log.Events.Count;
            Play(inv, 0);
            Assert.That(HitsOn(inv, mark), Is.EqualTo(new[] { 6 }));
        }

        [Test]
        public void BeastForm_DoesNotBoostEnemiesOrEffectDamage()
        {
            var e = Wolf(new[] { EnemyIds.Acolyte });
            e.SetSanity(4);
            var acolyte = e.State.Enemies[0];
            Assert.That(DamageCalculator.Compute(e, acolyte, e.Player, 6), Is.EqualTo(6), "敌人打你不加");
            Assert.That(DamageCalculator.Compute(e, e.Player, acolyte, 6, DamageKind.Effect), Is.EqualTo(6), "效果伤害不加");
        }

        [Test]
        public void BeastForm_HalvesCardBlockRoundedDown_NotOtherBlock()
        {
            var e = Wolf();
            SetHand(e, "B02", "B02", "B02");
            Play(e, 0);
            Assert.That(e.Player.Block, Is.EqualTo(5), "人形：满额");

            e.SetSanity(4);
            Assert.That(e.Player.Hand[0].PreviewValue(e, "Block"), Is.EqualTo(2), "卡面预览：5 减半向下取整");
            Play(e, 0);
            Assert.That(e.Player.Block, Is.EqualTo(7));

            e.SetStatus(e.Player, StatusId.Dexterity, 1);
            Assert.That(BlockCalculator.Compute(e, e.Player, 5, true), Is.EqualTo(3), "（5 + 1）÷ 2");
            e.SetStatus(e.Player, StatusId.Frail, 1);
            Assert.That(BlockCalculator.Compute(e, e.Player, 5, true), Is.EqualTo(2), "（5 + 1）× 0.75 ÷ 2 = 2.25");
            Assert.That(BlockCalculator.Compute(e, e.Player, 10, false), Is.EqualTo(10), "遗物 / 能力给的格挡不减半");
        }

        [Test]
        public void BlockFirstThenTransform_KeepsFullBlock()
        {
            var e = Wolf();
            SetHand(e, "B02", "C08");   // 防御，然后坠落（代价2：6 → 4，变身）
            Play(e, 0);
            Play(e, 0);
            Assert.That(e.Player.IsLunatic, Is.True);
            Assert.That(e.Player.Block, Is.EqualTo(5), "格挡在打出时结算，之后变身不会再减");
        }

        // ---------------- 变身 / 复归

        [Test]
        public void TransformAndRevert_CountOncePerRound_ResetAtNextTurn()
        {
            var e = Wolf();
            var probe = Listen(e);
            SetHand(e, "C08", "C04", "C08");   // 坠落 6 → 4 变身；镇定 4 → 6 复归；坠落 6 → 4 再变身（这一轮不算）
            Play(e, 0);
            Assert.That((probe.Transforms, e.State.TransformedThisRound), Is.EqualTo((1, true)));
            Play(e, 0);
            Assert.That((probe.Reverts, e.State.RevertedThisRound), Is.EqualTo((1, true)));
            Play(e, 0);
            Assert.That(probe.Entered, Is.EqualTo(2), "每次跨线照常通知 OnLunacyChanged（双面牌照常翻面）");
            Assert.That(probe.Transforms, Is.EqualTo(1), "同一轮第二次变身不触发");
            Assert.That(e.Player.Sanity, Is.EqualTo(4));

            EndTurn(e);   // 稻草人不攻击，第 2 回合还没有侵蚀
            Assert.That((e.State.TransformedThisRound, e.State.RevertedThisRound), Is.EqualTo((false, false)), "新的一轮清零");
            Assert.That(e.Log.Events.Any(x => x.Type == BattleEventType.RoundFlagsChanged && x.Value == 0), Is.True, "清零写状态事件");

            SetHand(e, "C04");
            Play(e, 0);
            Assert.That(probe.Reverts, Is.EqualTo(2), "新的一轮又能复归");
        }

        [Test]
        public void MindAttackOnEnemyTurn_CountsForThatRound()
        {
            var e = Wolf(new[] { EnemyIds.Whisperer });
            var probe = Listen(e);
            e.SetSanity(5);
            SetIntent(e, 0, "Whisper");   // 精神攻击 2：5 → 3
            EndTurn(e);
            Assert.That(e.State.Turn, Is.EqualTo(2));
            Assert.That(probe.Transforms, Is.EqualTo(1), "敌人回合里被推过线，算上一轮的变身");
            Assert.That(e.State.TransformedThisRound, Is.False, "第 2 回合开始时已经换了一轮");
        }

        [Test]
        public void ErosionAtTurnStart_CountsForTheNewRound()
        {
            var e = Wolf();
            var probe = Listen(e);
            for (int i = 0; i < 2; i++) EndTurn(e);   // 进入第 3 回合
            e.SetSanity(5);
            SetHand(e, "C08", "C04");   // 先在第 3 回合变身一次（6 → … 5 → 3）、再复归
            Play(e, 0);
            Play(e, 0);
            Assert.That((probe.Transforms, probe.Reverts), Is.EqualTo((1, 1)));
            Assert.That(e.Player.Sanity, Is.EqualTo(5));
            EndTurn(e);   // 第 4 回合开始，侵蚀 −1：5 → 4
            Assert.That(e.Player.Sanity, Is.EqualTo(4));
            Assert.That(probe.Transforms, Is.EqualTo(2), "侵蚀发生在新一轮里，算新一轮的变身");
            Assert.That(e.State.TransformedThisRound, Is.True);
        }

        [Test]
        public void CollapseReset_IsNotARevert()
        {
            // 把癫狂线压到 2，崩溃重置到 3 就会「离开癫狂」，正好用来测
            var e = Wolf(config: new BattleConfig { LunacyThreshold = 2 });
            var probe = Listen(e);
            e.SetSanity(1);
            SetHand(e, "B03");   // 禁忌一瞥 代价1：1 → 0，崩溃，重置到 3
            Play(e, 0);
            Assert.That(e.State.Stats.Collapses, Is.EqualTo(1));
            Assert.That(e.Player.Sanity, Is.EqualTo(3));
            Assert.That(probe.Left, Is.EqualTo(1), "翻面照常通知");
            Assert.That((probe.Reverts, e.State.RevertedThisRound), Is.EqualTo((0, false)), "崩溃重置不算复归");
        }

        // ---------------- 流血

        [Test]
        public void Bleed_TicksBeforeAction_IgnoresBlock_AndDecays()
        {
            var e = Wolf();
            var probe = Listen(e);
            var dummy = e.State.Enemies[0];
            e.SetBlock(dummy, 10);
            e.Queue.AddToBottom(new Core.Actions.ApplyStatusAction(dummy, StatusId.Bleed, 3));
            SetHand(e, "B02");
            Play(e, 0);   // 借出牌把队列里的施加流血结算掉
            Assert.That(dummy.Statuses.Get(StatusId.Bleed), Is.EqualTo(3));

            var hp = new List<int> { dummy.Hp };
            for (int i = 0; i < 4; i++)
            {
                EndTurn(e);
                hp.Add(dummy.Hp);
            }
            Assert.That(hp.Zip(hp.Skip(1), (a, b) => a - b), Is.EqualTo(new[] { 3, 2, 1, 0 }), "无视格挡，每次结算后 −1");
            Assert.That(dummy.Statuses.Get(StatusId.Bleed), Is.EqualTo(0));
            Assert.That(probe.Bled, Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(e.Log.Events.Count(BattleEventType.Bled), Is.EqualTo(3));
        }

        [Test]
        public void Bleed_KillsBeforeTheEnemyActs()
        {
            var e = Wolf(new[] { EnemyIds.Acolyte });
            var acolyte = e.State.Enemies[0];
            e.SetHp(acolyte, 2);
            e.SetStatus(acolyte, StatusId.Bleed, 5);
            SetIntent(e, 0, "Heavy");
            int hp = e.Player.Hp;
            EndTurn(e);
            Assert.That(acolyte.IsDead, Is.True);
            Assert.That(e.Player.Hp, Is.EqualTo(hp), "流血死了，献祭重击不再打出");
            Assert.That(e.State.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void Bleed_StillTicksWhenStunned()
        {
            var e = Wolf();
            var dummy = e.State.Enemies[0];
            e.SetStatus(dummy, StatusId.Bleed, 2);
            e.SetStunned(dummy, true);
            int hp = dummy.Hp;
            EndTurn(e);
            Assert.That(hp - dummy.Hp, Is.EqualTo(2));
            Assert.That(dummy.Stunned, Is.False, "震慑照常用掉");
            Assert.That(e.Log.Events.Count(BattleEventType.EnemyStunSkipped), Is.EqualTo(1));
        }

        // ---------------- 人狼同体

        [Test]
        public void BothFaces_ResolveSaneThenMad_EachPaysItsCost_FormChecksAtResolution()
        {
            var e = Wolf();
            var probe = Listen(e);
            probe.BothFaces = true;
            e.SetSanity(5);
            SetHand(e, "C07");   // 撕裂：清醒面 代价1 造成 6；癫狂面 代价1 造成 6 × 2
            int mark = e.Log.Events.Count;
            Play(e, 0);
            // 人形面：付代价 5 → 4（变身），结算时已是兽形 6 + 2；兽形面：4 → 3，(6 + 2) × 2
            Assert.That(HitsOn(e, mark), Is.EqualTo(new[] { 8, 8, 8 }));
            Assert.That(e.Player.Sanity, Is.EqualTo(3), "两面各付一次代价");
            Assert.That(probe.Transforms, Is.EqualTo(1));
            var played = e.Log.Events.Skip(mark).First(x => x.Type == BattleEventType.CardPlayed);
            Assert.That(played.Message, Does.Contain("（两面）"));
            Assert.That(played.Flag, Is.True);
            Assert.That(e.Player.DiscardPile.Count(c => c.Id == "C07"), Is.EqualTo(1), "只进一次弃牌堆");
        }

        [Test]
        public void BothFaces_OnlyAffectsDualCards()
        {
            var e = Wolf();
            Listen(e).BothFaces = true;
            SetHand(e, "B01");
            int mark = e.Log.Events.Count;
            Play(e, 0);
            Assert.That(HitsOn(e, mark), Is.EqualTo(new[] { 6 }));
        }

        // ---------------- 复制、哈希、跑局

        [Test]
        public void Fork_CopiesTraitsAndRoundFlags()
        {
            var e = Wolf();
            SetHand(e, "C08", "B01");
            Play(e, 0);   // 变身
            var f = e.Fork();
            Assert.That(StateHasher.Hash(f), Is.EqualTo(StateHasher.Hash(e)));
            Assert.That(f.State.TransformedThisRound, Is.True);
            Assert.That(f.Player.Traits.Select(t => t.Id), Is.EqualTo(new[] { RelicIds.BeastBlood }));
            Assert.That(f.Player.Relics, Is.Empty, "特性不进遗物栏");
            int mark = f.Log.Events.Count;
            Play(f, 0);
            Assert.That(HitsOn(f, mark), Is.EqualTo(new[] { 8 }), "复制出的战斗里兽形加成照样生效");
        }

        [Test]
        public void Run_GivesTheCharactersTraitToEveryBattle()
        {
            var wolf = RunEngine.NewRun(4, new RunConfig { Character = Werewolf });
            wolf.State.Floor = 1;
            wolf.EnterNode(0, 0, RoomType.Battle);
            Assert.That(wolf.Battle.Player.Traits.Select(t => t.Id), Is.EqualTo(new[] { RelicIds.BeastBlood }));
            Assert.That(wolf.Battle.Player.Relics.Select(t => t.Id), Is.EqualTo(new[] { RelicIds.WolfFangPendant }), "特性不进遗物栏");
            Assert.That(wolf.Battle.Player.MaxSanity, Is.EqualTo(6));

            var inv = RunEngine.NewRun(4);
            inv.State.Floor = 1;
            inv.EnterNode(0, 0, RoomType.Battle);
            Assert.That(inv.Battle.Player.Traits, Is.Empty, "调查员没有特性");
        }

        [Test]
        public void Validation_TraitMustBeATraitRelic()
        {
            var src = DataFiles.Read();
            src.Characters = src.Characters.Replace("investigator_notes,,C06", "investigator_notes,old_pipe,C06");
            Assert.That(GameData.Validate(src).Any(x => x.Contains("特性 old_pipe 的稀有度不是 Trait")), Is.True);
            src = DataFiles.Read();
            src.Characters = src.Characters.Replace("investigator_notes,,C06", "investigator_notes,no_trait,C06");
            Assert.That(GameData.Validate(src).Any(x => x.Contains("特性 no_trait 不存在")), Is.True);
            Assert.That(RelicLibrary.Get(RelicIds.BeastBlood).Rarity, Is.EqualTo(RelicRarity.Trait));
        }
    }
}
