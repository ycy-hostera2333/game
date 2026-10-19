using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using NUnit.Framework;
using static Deeptide.Tests.TestUtil;

namespace Deeptide.Tests
{
    /// <summary>批次 3.1 / 3.2 的卡牌效果，以及它们用到的内核能力（策划/09 M1.1–M1.4）。</summary>
    public class CardTests
    {
        static readonly string[] TwoDummies = { EnemyIds.Dummy, EnemyIds.Dummy };

        /// <summary>读卡表里的数值（调参后测试不用跟着改）。</summary>
        static int Var(string card, string key) => CardLibrary.Get(card).Vars[key];

        // ---------------- 3.1 守夜 + 传染

        [TestCase(10, 1)]
        [TestCase(8, 0)]
        public void HolyWaterSplash_HitsAll_WeakensOnClarity(int sanity, int weak)
        {
            var e = Battle(TwoDummies);
            e.Player.Sanity = sanity;
            SetHand(e, "C05");
            Play(e, 0, -1);
            Assert.That(e.State.Enemies.Select(x => x.Hp), Is.All.EqualTo(194));
            Assert.That(e.State.Enemies.Select(x => x.Statuses.Get(StatusId.Weak)), Is.All.EqualTo(weak));
        }

        [Test]
        public void ThingInTheShadows_MadFace_DoublesFearAndHurtsSelf()
        {
            var e = Battle();
            SetHand(e, "C12", "C12");
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Fear, Is.EqualTo(Var("C12", "Fear")));
            e.Player.Sanity = 4;
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Stunned, Is.True, "4 + 癫狂面 8 ≥ 意志 10 → 震慑");
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(200 - 2 * Var("C12", "Damage")));
            Assert.That(e.Player.Hp, Is.EqualTo(68));
        }

        [Test]
        public void Scream_SaneBlocksAndFearsAll_MadPaysForMoreFear()
        {
            var e = Battle(TwoDummies);
            SetHand(e, "C13", "C13");
            Play(e, 0, -1);
            Assert.That(e.Player.Block, Is.EqualTo(5));
            Assert.That(e.State.Enemies.Select(x => x.Fear), Is.All.EqualTo(2));
            e.Player.Sanity = 4;
            Play(e, 0, -1);
            Assert.That(e.Player.Sanity, Is.EqualTo(3));
            Assert.That(e.State.Enemies.Select(x => x.Fear), Is.All.EqualTo(7));
        }

        [Test]
        public void HolySigil_BlockPerRestoredPointIncludingOverflow_AndStacks()
        {
            var e = Battle();
            SetHand(e, "U01", "U01+", "C04");
            Play(e, 0, -1);
            Play(e, 0, -1);
            Assert.That(e.Player.Powers.Single().Amount, Is.EqualTo(5), "同名能力叠加：2 + 3");
            Assert.That(e.Log.Events.Count(BattleEventType.PowerApplied), Is.EqualTo(2));
            e.Player.Sanity = 9;
            Play(e, 0, -1);   // 镇定恢复 2：实际 1、溢出 1，按 2 点算
            Assert.That(e.Player.Block, Is.EqualTo(10));
            Assert.That(e.Player.Hand, Is.Empty);
            Assert.That(e.Player.DiscardPile.Concat(e.Player.ExhaustPile).Any(c => c.Id == "U01"), Is.False, "能力牌打出后移出战斗");
        }

        [TestCase(10, 28)]
        [TestCase(7, 16)]
        public void Dispel_ClarityRaisesDamage(int sanity, int expected)
        {
            var e = Battle();
            e.Player.Sanity = sanity;
            SetHand(e, "U03");
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(200 - expected));
        }

        [Test]
        public void SilentPrayer_RestoresToMax_ZeroRestoreDoesNotCount()
        {
            var e = Battle(relics: new[] { "worn_prayer_book" });
            SetHand(e, "U04", "U04");
            Play(e, 0, -1);   // 满理智：恢复 0，不算一次恢复
            Assert.That(e.Log.Events.Count(BattleEventType.SanityRestored), Is.EqualTo(0));
            Assert.That(e.Log.Events.Count(BattleEventType.RelicTriggered), Is.EqualTo(0));
            e.Player.Sanity = 3;
            Play(e, 0, -1);
            Assert.That(e.Player.Sanity, Is.EqualTo(10));
            Assert.That(e.Player.ExhaustPile.Count(c => c.Id == "U04"), Is.EqualTo(2));
        }

        [TestCase(10, 1)]
        [TestCase(8, 0)]
        public void WatchersOath_StrengthAtEndOfTurnIfClarity(int sanity, int strength)
        {
            var e = Battle();
            SetHand(e, "U05");
            Play(e, 0, -1);
            e.Player.Sanity = sanity;
            EndTurn(e);
            Assert.That(e.Player.Statuses.Get(StatusId.Strength), Is.EqualTo(strength));
        }

        [Test]
        public void SourceOfFear_FearsAllAtEndOfTurn()
        {
            var e = Battle(TwoDummies);
            SetHand(e, "U11");
            Play(e, 0, -1);
            EndTurn(e);
            Assert.That(e.State.Enemies.Select(x => x.Fear), Is.All.EqualTo(2));
        }

        [Test]
        public void MindCollapse_DamageFromFear_KeepsFear()
        {
            var e = Battle();
            e.State.Enemies[0].Fear = 6;
            SetHand(e, "U12");
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(200 - 6 * Var("U12", "Mult")));
            Assert.That(e.State.Enemies[0].Fear, Is.EqualTo(6));
        }

        [Test]
        public void GazeIntoTheAbyss_MadFaceDoublesAndCosts()
        {
            var e = Battle(new[] { EnemyIds.Acolyte });
            e.Player.Sanity = 4;
            SetHand(e, "U13");
            Play(e, 0);
            Assert.That(e.Player.Sanity, Is.EqualTo(2));
            Assert.That(e.State.Enemies[0].Stunned, Is.True, "10 恐惧 ≥ 意志 8");
        }

        [TestCase(4, true)]
        [TestCase(3, false)]
        public void Petrify_StunsAtHalfWill(int fear, bool stunned)
        {
            var e = Battle(new[] { EnemyIds.Acolyte });   // 意志 8
            e.State.Enemies[0].Fear = fear;
            SetHand(e, "U14");
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Stunned, Is.EqualTo(stunned));
            Assert.That(e.State.Enemies[0].Will, Is.EqualTo(stunned ? 12 : 8));
            Assert.That(e.Player.ExhaustPile.Single().Id, Is.EqualTo("U14"));
        }

        [Test]
        public void Stigmata_OverflowDealsEffectDamageToAll()
        {
            var e = Battle(TwoDummies);
            e.Player.Statuses.Add(StatusId.Strength, 5);
            SetHand(e, "R02", "C04");
            Play(e, 0, -1);
            Play(e, 0, -1);   // 满理智恢复 2：溢出 2 → 2 × 5 伤害，不吃力量
            Assert.That(e.State.Enemies.Select(x => x.Hp), Is.All.EqualTo(190));
        }

        [Test]
        public void MassHysteria_FearEqualsMissingSanity()
        {
            var e = Battle(TwoDummies);
            e.Player.Sanity = 4;
            SetHand(e, "R04");
            Play(e, 0, -1);
            Assert.That(e.State.Enemies.Select(x => x.Fear), Is.All.EqualTo(6));
        }

        // ---------------- 3.2 堕落 + 通用

        [Test]
        public void RevolverShot_DoubleOnlyAsFirstCard()
        {
            var e = Battle();
            SetHand(e, "C14", "C14");
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(190));
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(185));
        }

        [Test]
        public void Research_DrawsThenAsksWhichToDiscard()
        {
            var e = Battle();
            SetHand(e, "C15", "B01");
            SetDrawPile(e, "C04", "B02", "S01", "B01");
            Play(e, 0, -1);
            Assert.That(e.State.Phase, Is.EqualTo(BattlePhase.WaitingForChoice));
            Assert.That(e.State.Choice.Options.Count, Is.EqualTo(4));
            Assert.That(e.Execute(BattleCommand.Play(0, 0), out var error), Is.False);
            Assert.That(error, Is.EqualTo("请先做出选择"));
            Assert.That(e.Execute(BattleCommand.End(), out _), Is.False);
            Assert.That(e.Execute(BattleCommand.Choose(9), out _), Is.False);

            int whispers = e.State.Choice.Options.FindIndex(o => o.CardId == "S01");
            Choose(e, whispers);
            Assert.That(e.State.Phase, Is.EqualTo(BattlePhase.PlayerAction));
            Assert.That(e.Player.Hand.Select(c => c.Id), Is.EquivalentTo(new[] { "B01", "C04", "B02" }));
            Assert.That(e.Player.DiscardPile.Select(c => c.Id), Is.EquivalentTo(new[] { "S01", "C15" }));
        }

        [Test]
        public void Research_MadFaceDrawsMoreWithoutChoice()
        {
            var e = Battle();
            e.Player.Sanity = 4;
            SetHand(e, "C15");
            Play(e, 0, -1);
            Assert.That(e.State.Phase, Is.EqualTo(BattlePhase.PlayerAction));
            Assert.That(e.Player.Hand.Count, Is.EqualTo(4));
            Assert.That(e.Player.Sanity, Is.EqualTo(3));
        }

        [Test]
        public void LightTheLamp_ClearsBlurForThisTurnOnly()
        {
            var e = Battle(new[] { EnemyIds.Acolyte });
            e.Player.Sanity = 4;
            Assert.That(e.IntentsBlurred, Is.True);
            SetHand(e, "C16");
            Play(e, 0, -1);
            Assert.That(e.IntentsBlurred, Is.False);
            Assert.That(e.Player.Block, Is.EqualTo(5));
            EndTurn(e);
            Assert.That(e.IntentsBlurred, Is.EqualTo(e.Player.IsLunatic));
        }

        [Test]
        public void AbyssalOffering_PaysSanityForStrength()
        {
            var e = Battle();
            SetHand(e, "U06");
            Play(e, 0, -1);
            Assert.That(e.Player.Sanity, Is.EqualTo(7));
            Assert.That(e.Player.Statuses.Get(StatusId.Strength), Is.EqualTo(2));
            Assert.That(e.Player.ExhaustPile.Single().Id, Is.EqualTo("U06"));
        }

        [Test]
        public void MadInstinct_EachLostPointHitsARandomEnemy_IgnoringStrength()
        {
            var e = Battle(TwoDummies);
            e.Player.Statuses.Add(StatusId.Strength, 5);
            SetHand(e, "U07", "C08");
            Play(e, 0, -1);
            Play(e, 0, -1);   // 坠落：代价 2 → 2 次 3 伤害
            Assert.That(e.State.Enemies.Sum(x => 200 - x.Hp), Is.EqualTo(6));
        }

        [Test]
        public void MadInstinct_IgnoresSanityLostToEnemiesAndCurses()
        {
            var e = Battle(new[] { EnemyIds.Whisperer });
            SetHand(e, "U07", "S01");
            Play(e, 0, -1);
            SetIntent(e, 0, "Whisper");
            int hp = e.State.Enemies[0].Hp;
            EndTurn(e);   // 回合结束疯语 −1（Curse），呓语者精神攻击（Enemy）
            Assert.That(e.Player.Sanity, Is.LessThan(9));
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(hp));
        }

        [Test]
        public void TentacleLash_MadFaceHitsAllAndHurtsSelf()
        {
            var e = Battle(TwoDummies);
            SetHand(e, "U08", "U08");
            Play(e, 0, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(188));
            Assert.That(e.State.Enemies[0].Statuses.Get(StatusId.Vulnerable), Is.EqualTo(2));
            Assert.That(e.State.Enemies[1].Hp, Is.EqualTo(200));
            e.Player.Sanity = 4;
            e.Player.Energy = 2;
            Play(e, 0, 1);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(188 - 18), "易伤 ×1.5");
            Assert.That(e.State.Enemies[1].Hp, Is.EqualTo(188));
            Assert.That(e.Player.Hp, Is.EqualTo(66));
        }

        [Test]
        public void MadLaughter_Faces()
        {
            var e = Battle();
            SetHand(e, "U10", "U10+", "U10");
            SetDrawPile(e, "B01");
            Play(e, 0, -1);
            Assert.That(e.Player.Energy, Is.EqualTo(4));
            Play(e, 0, -1);   // 升级后清醒面再抽 1
            Assert.That(e.Player.Energy, Is.EqualTo(5));
            Assert.That(e.Player.Hand.Count(c => c.Id == "B01"), Is.EqualTo(1));
            e.Player.Sanity = 4;
            Play(e, 0, -1);
            Assert.That(e.Player.Energy, Is.EqualTo(7));
            Assert.That(e.Player.Sanity, Is.EqualTo(3));
        }

        [Test]
        public void ForbiddenPage_OffersThreeUncommons_ChosenCardIsFreeThisTurn()
        {
            var e = Battle();
            SetHand(e, "U15");
            Play(e, 0, -1);
            var options = e.State.Choice.Options;
            Assert.That(options.Count, Is.EqualTo(3));
            Assert.That(options.Select(o => o.CardId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(options.All(o => CardLibrary.Get(o.CardId).Rarity == Rarity.Uncommon && o.CardId != "U15"), Is.True);
            string picked = options[1].CardId;
            Choose(e, 1);
            var card = e.Player.Hand.Single();
            Assert.That(card.Id, Is.EqualTo(picked));
            Assert.That(e.CostOf(card), Is.EqualTo(0));
            EndTurn(e);
            Assert.That(card.CostThisTurn, Is.EqualTo(-1), "离开手牌后临时费用清掉");
        }

        [Test]
        public void GazeOfTheOldOnes_DualCardsAlwaysMad()
        {
            var e = Battle();
            SetHand(e, "R01", "C06");
            e.Player.Energy = 4;
            Play(e, 0, -1);
            Assert.That(e.UsesMadFace(e.Player.Hand[0]), Is.True);
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(200 - Var("C06", "MadDamage")));
            Assert.That(e.Player.Hp, Is.EqualTo(68));
            Assert.That(e.Player.Sanity, Is.EqualTo(10), "癫狂面没有代价时不掉理智");
        }

        [Test]
        public void FinalRite_SpendsAllEnergy_PaysXSanity_HitsAllXTimes()
        {
            var e = Battle(TwoDummies);
            SetHand(e, "R03", "R03");
            Play(e, 0, -1);
            Assert.That(e.Player.Energy, Is.EqualTo(0));
            Assert.That(e.Player.Sanity, Is.EqualTo(7));
            int hp = 200 - 3 * Var("R03", "Damage");
            Assert.That(e.State.Enemies.Select(x => x.Hp), Is.All.EqualTo(hp));
            Play(e, 0, -1);   // X = 0：能打出，什么都不发生
            Assert.That(e.Player.Sanity, Is.EqualTo(7));
            Assert.That(e.State.Enemies.Select(x => x.Hp), Is.All.EqualTo(hp));
        }

        [Test]
        public void MindAnchor_AsksAtTurnStartBeforeDrawing()
        {
            var e = Battle();
            SetHand(e, "R06");
            Play(e, 0, -1);
            EndTurn(e);
            Assert.That(e.State.Phase, Is.EqualTo(BattlePhase.WaitingForChoice));
            Assert.That(e.Player.Hand, Is.Empty, "选择在抽牌之前");
            Assert.That(e.State.Turn, Is.EqualTo(2));
            Choose(e, 1);
            Assert.That(e.Player.Sanity, Is.EqualTo(8));
            Assert.That(e.Player.Hand.Count, Is.EqualTo(5));
            Assert.That(e.State.Phase, Is.EqualTo(BattlePhase.PlayerAction));
        }

        [Test]
        public void MindAnchorUpgraded_IsInnate()
        {
            var deck = Enumerable.Repeat("B01", 12).Append("R06+").ToArray();
            for (ulong seed = 1; seed <= 10; seed++)
                Assert.That(Battle(deck: deck, seed: seed).Player.Hand.Any(c => c.Id == "R06"), Is.True, $"种子 {seed}");
        }

        [Test]
        public void Web_CostsOneAndExhausts()
        {
            var e = Battle();
            SetHand(e, "S02");
            Play(e, 0, -1);
            Assert.That(e.Player.Energy, Is.EqualTo(2));
            Assert.That(e.Player.ExhaustPile.Single().Id, Is.EqualTo(CardIds.Web));
        }

        // ---------------- 选择与回放

        [Test]
        public void ChoicesAreRecorded_AndReplayReproducesState()
        {
            var e = Battle(deck: new[] { "C15", "U15", "R06", "B01", "B02", "C04", "U12", "B03", "C08", "B01" }, seed: 5);
            for (int step = 0; step < 120 && e.State.Outcome == BattleOutcome.Ongoing; step++)
                Assert.That(e.Execute(EventStreamTests.Scripted(e, step), out var err), Is.True, err);
            Assert.That(e.History.Any(c => c.Kind == CommandKind.Choose), Is.True);
            var replayed = BattleEngine.Replay(e.Setup, e.History);
            Assert.That(StateHasher.Hash(replayed), Is.EqualTo(StateHasher.Hash(e)));
        }

        [Test]
        public void SingleOptionChoice_ResolvesAutomatically()
        {
            var e = Battle();
            SetHand(e, "C15");
            SetDrawPile(e);
            e.Player.DiscardPile.Clear();
            e.Player.DiscardPile.Add(e.NewCard("B01"));
            Play(e, 0, -1);   // 只抽到 1 张：直接弃掉它，不打断玩家
            Assert.That(e.State.Phase, Is.EqualTo(BattlePhase.PlayerAction));
            Assert.That(e.Player.Hand, Is.Empty);
        }

        // ---------------- 卡面预览和实际结算一致（策划/09 M1.3）

        static readonly (string Name, System.Action<BattleEngine> Setup, string[] Relics)[] PreviewSetups =
        {
            ("无修正", e => { }, null),
            ("力量 3 + 目标易伤", e => { e.Player.Statuses.Add(StatusId.Strength, 3); e.State.Enemies[0].Statuses.Add(StatusId.Vulnerable, 2); }, null),
            ("虚弱 + 王冠 + 目标被震慑", e => { e.Player.Statuses.Add(StatusId.Weak, 1); e.State.Enemies[0].Stunned = true; }, new[] { "crown_of_dread" }),
        };

        [Test]
        public void AttackPreview_MatchesActualDamage_ForEveryAttackCard()
        {
            foreach (var def in CardLibrary.All.Where(d => d.Type == CardType.Attack && d.Target == CardTarget.Enemy))
            foreach (var id in new[] { def.Id, def.Id + "+" })
            foreach (bool mad in def.Dual ? new[] { false, true } : new[] { false })
            foreach (var (name, setup, relics) in PreviewSetups)
            {
                var e = Battle(relics: relics);
                e.Player.Sanity = mad ? 4 : 10;
                e.Player.Energy = 5;
                e.State.Enemies[0].Fear = 4;
                setup(e);
                SetHand(e, id);
                var card = e.Player.Hand[0];
                var preview = card.PreviewAttack(e, e.UsesMadFace(card), e.State.Enemies[0]);
                Assert.That(preview, Is.Not.Null, id);
                Play(e, 0);
                int actual = 200 - e.State.Enemies[0].Hp;
                Assert.That(actual, Is.EqualTo(preview.Value.PerHit * preview.Value.Hits), $"{id} {(mad ? "癫狂面" : "清醒面")}（{name}）");
            }
        }

        [Test]
        public void CardText_ShowsModifiedNumbers()
        {
            var e = Battle();
            e.Player.Statuses.Add(StatusId.Strength, 2);
            e.Player.Statuses.Add(StatusId.Frail, 1);
            SetHand(e, "B01", "B02", "R05");
            Assert.That(e.Player.Hand[0].Describe(e), Is.EqualTo("造成 8 伤害。"));
            Assert.That(e.Player.Hand[1].Describe(e), Is.EqualTo("获得 3 格挡。"));
            Assert.That(e.Player.Hand[2].Describe(e), Does.EndWith($"（当前 {10 * Var("R05", "Mult") + 2}）"));
            Assert.That(e.Player.Hand[0].Describe(false), Is.EqualTo("造成 6 伤害。"), "图鉴文本不带修正");
        }
    }
}
