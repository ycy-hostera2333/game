using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using NUnit.Framework;
using static Deeptide.Tests.TestUtil;

namespace Deeptide.Tests
{
    /// <summary>理智系统（策划/01 §2、02 §3.3 / §3.5）。</summary>
    public class SanityTests
    {
        [Test]
        public void Ward_BlocksEnemyMindAttack()
        {
            var e = Battle(new[] { EnemyIds.Whisperer });
            e.Player.Statuses.Add(StatusId.Ward, 1);
            SetIntent(e, 0, "Whisper");
            SetHand(e);
            EndTurn(e);
            Assert.That(e.Player.Sanity, Is.EqualTo(10));
            Assert.That(e.Player.Statuses.Get(StatusId.Ward), Is.EqualTo(0));
        }

        [Test]
        public void Ward_DoesNotBlockOwnCardCost()
        {
            var e = Battle();
            e.Player.Statuses.Add(StatusId.Ward, 1);
            SetHand(e, "B03");
            Play(e, 0);
            Assert.That(e.Player.Sanity, Is.EqualTo(9));
            Assert.That(e.Player.Statuses.Get(StatusId.Ward), Is.EqualTo(1));
        }

        [Test]
        public void MindAttack_IgnoresBlock()
        {
            var e = Battle(new[] { EnemyIds.Whisperer });
            SetIntent(e, 0, "Whisper");
            SetHand(e, "B02");
            Play(e, 0);
            EndTurn(e);
            Assert.That(e.Player.Sanity, Is.EqualTo(8));
        }

        [Test]
        public void FaceIsLockedBeforePayingCost()
        {
            // 理智 5 时打撕裂：按清醒面结算（1 段），付完代价才进入癫狂
            var e = Battle();
            e.Player.Sanity = 5;
            SetHand(e, "C07");
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(194));
            Assert.That(e.Player.Sanity, Is.EqualTo(4));
            Assert.That(e.Player.IsLunatic, Is.True);
            Assert.That(e.Log.Events.Count(BattleEventType.LunacyEntered), Is.EqualTo(1));
        }

        [Test]
        public void MadFace_UsedWhenAlreadyLunatic()
        {
            var e = Battle();
            e.Player.Sanity = 4;
            SetHand(e, "C07");
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(188));
            Assert.That(e.Player.Sanity, Is.EqualTo(3));
        }

        [Test]
        public void NonDualCard_PaysItsCostWhileLunatic()
        {
            var e = Battle();
            e.Player.Sanity = 4;
            SetHand(e, "C08");
            Play(e, 0);
            Assert.That(e.Player.Sanity, Is.EqualTo(2));
        }

        [Test]
        public void InvestigatorNotes_TriggersOnFirstLunacyOnly()
        {
            var e = Battle();
            e.Player.Sanity = 5;
            SetHand(e, "B03");
            SetDrawPile(e, Enumerable.Repeat("B02", 10).ToArray());
            Play(e, 0);
            // 代价 → 进入癫狂 → 笔记 +1 能量、抽 2 → 一瞥（癫狂面已锁定为清醒面）抽 2
            Assert.That(e.Player.Energy, Is.EqualTo(4));
            Assert.That(e.Player.Hand.Count, Is.EqualTo(4));

            // 回到清醒再跌入癫狂：不再触发
            e.Player.Sanity = 5;
            SetHand(e, "B03");
            Play(e, 0);
            Assert.That(e.Player.Energy, Is.EqualTo(4));
            Assert.That(e.Log.Events.Count(BattleEventType.RelicTriggered), Is.EqualTo(1));
        }

        [Test]
        public void ClarityMargin_IsConfigurable()
        {
            var e = Battle(config: new BattleConfig { ClarityMargin = 0 });
            e.Player.Sanity = 9;
            Assert.That(e.Player.IsClarity, Is.False);
            e.Player.Sanity = 10;
            Assert.That(e.Player.IsClarity, Is.True);
        }

        [Test]
        public void EnergyGainedDuringEnemyTurn_CarriesToNextTurn()
        {
            // 敌人的精神攻击把玩家推入癫狂 → 调查笔记在敌人回合触发：+1 能量顺延到下回合，抽的 2 张留在手里
            var e = Battle(new[] { EnemyIds.Whisperer });
            e.Player.Sanity = 5;
            SetIntent(e, 0, "Whisper");
            SetHand(e);
            SetDrawPile(e, Enumerable.Repeat("B02", 10).ToArray());
            EndTurn(e);
            Assert.That(e.Player.IsLunatic, Is.True);
            Assert.That(e.Player.Energy, Is.EqualTo(4));
            Assert.That(e.Player.EnergyNextTurn, Is.EqualTo(0));
            Assert.That(e.Player.Hand.Count, Is.EqualTo(7));
        }

        [Test]
        public void Collapse_ResolvesBeforeRestOfCard()
        {
            var e = Battle();
            e.Player.Sanity = 1;
            SetHand(e, "C07");     // 癫狂面：代价1，6 伤害 ×2
            Play(e, 0);

            var p = e.Player;
            Assert.That(p.Hp, Is.EqualTo(70 - 7));
            Assert.That(p.Sanity, Is.EqualTo(3));
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(188));
            Assert.That(p.DiscardPile.Select(c => c.Id), Does.Contain(CardIds.Whispers));
            Assert.That(e.State.Stats.Collapses, Is.EqualTo(1));
            Assert.That(e.State.Stats.CursesGained, Is.EqualTo(1));

            var types = e.Log.Events.Select(x => x.Type).ToList();
            int played = types.LastIndexOf(BattleEventType.CardPlayed);
            int collapse = types.IndexOf(BattleEventType.Collapse, played);
            int damage = types.IndexOf(BattleEventType.Damage, played);
            Assert.That(collapse, Is.LessThan(damage), "崩溃应先于卡牌伤害结算");
        }

        [Test]
        public void Restore_ClampsAtMax_AndReportsOverflow()
        {
            var e = Battle();
            e.Player.Sanity = 9;
            SetHand(e, "C04");
            Play(e, 0);
            Assert.That(e.Player.Sanity, Is.EqualTo(10));
            Assert.That(e.Log.Events.Last(x => x.Type == BattleEventType.SanityRestored).Message, Does.Contain("溢出 1"));
        }

        [Test]
        public void Restore_CanLeaveLunacy()
        {
            var e = Battle();
            e.Player.Sanity = 4;
            SetHand(e, "C04");
            Play(e, 0);
            Assert.That(e.Player.Sanity, Is.EqualTo(6));
            Assert.That(e.Player.IsLunatic, Is.False);
            Assert.That(e.Log.Events.Count(BattleEventType.LunacyLeft), Is.EqualTo(1));
        }

        [TestCase(10, 11)]
        [TestCase(9, 11)]     // 清明 = 理智 ≥ 上限 − 1
        [TestCase(8, 7)]
        public void Clarity_IsCheckedWhenResolving(int sanity, int expectedBlock)
        {
            var e = Battle();
            e.Player.Sanity = sanity;
            SetHand(e, "C02");
            Play(e, 0);
            Assert.That(e.Player.Block, Is.EqualTo(expectedBlock));
        }

        [TestCase(10, 2)]
        [TestCase(9, 2)]
        [TestCase(8, 0)]
        public void FirmBelief_GrantsWardOnlyAtClarity(int sanity, int expectedWard)
        {
            var e = Battle();
            e.Player.Sanity = sanity;
            SetHand(e, "U02");
            Play(e, 0);
            Assert.That(e.Player.Block, Is.EqualTo(12));
            Assert.That(e.Player.Statuses.Get(StatusId.Ward), Is.EqualTo(expectedWard));
        }

        [Test]
        public void BladeOfReason_UsesCurrentSanity()
        {
            var e = Battle();
            e.Player.Sanity = 7;
            SetHand(e, "R05");
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(200 - 7 * CardLibrary.Get("R05").Vars["Mult"]));
        }

        [Test]
        public void BrinkOfCollapse_UsesMissingSanity()
        {
            var e = Battle();
            e.Player.Sanity = 3;
            SetHand(e, "U09");
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(200 - (4 + 7 * 2)));
        }

        [Test]
        public void Erosion_StartsOnTurnFour()
        {
            var e = Battle();
            EndTurn(e);
            EndTurn(e);
            Assert.That(e.State.Turn, Is.EqualTo(3));
            Assert.That(e.Player.Sanity, Is.EqualTo(10));
            EndTurn(e);
            Assert.That(e.State.Turn, Is.EqualTo(4));
            Assert.That(e.Player.Sanity, Is.EqualTo(9));
        }

        [Test]
        public void Whispers_LoseSanityAtEndOfTurn_AndCannotBePlayed()
        {
            var e = Battle();
            SetHand(e, "S01", "S01");
            Assert.That(e.Execute(BattleCommand.Play(0), out _), Is.False);
            EndTurn(e);
            Assert.That(e.Player.Sanity, Is.EqualTo(8));
        }
    }
}
