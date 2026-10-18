using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Content;
using Deeptide.Core.Enemies;
using Deeptide.Core.Random;
using NUnit.Framework;
using static Deeptide.Tests.TestUtil;

namespace Deeptide.Tests
{
    /// <summary>回合流程、胜负判定、确定性与回放（策划/02 §2、08 §5 / §7）。</summary>
    public class FlowTests
    {
        [Test]
        public void CombatStart_DrawsFiveWithFullEnergyAndSanity()
        {
            var e = Battle(deck: TestDecks.Get("starter").ToArray());
            Assert.That(e.State.Turn, Is.EqualTo(1));
            Assert.That(e.State.Phase, Is.EqualTo(BattlePhase.PlayerAction));
            Assert.That(e.Player.Hand.Count, Is.EqualTo(5));
            Assert.That(e.Player.DrawPile.Count, Is.EqualTo(5));
            Assert.That(e.Player.Energy, Is.EqualTo(3));
            Assert.That(e.Player.Sanity, Is.EqualTo(10));
        }

        [Test]
        public void CannotPlayWithoutEnoughEnergy()
        {
            var e = Battle();
            SetHand(e, "U02", "U02");
            Play(e, 0);
            Assert.That(e.Execute(BattleCommand.Play(0), out var error), Is.False);
            Assert.That(error, Is.EqualTo("能量不足"));
        }

        [Test]
        public void AttackNeedsLivingTarget()
        {
            var e = Battle();
            SetHand(e, "B01");
            Assert.That(e.Execute(BattleCommand.Play(0, 5), out var error), Is.False);
            Assert.That(error, Is.EqualTo("目标无效"));
        }

        [Test]
        public void HandLimit_OverflowGoesToDiscard()
        {
            var e = Battle();
            SetHand(e, "B03", "B02", "B02", "B02", "B02", "B02", "B02", "B02", "B02", "B02");
            SetDrawPile(e, "B01", "B01");
            Play(e, 0);   // 手牌 9，抽 2：第 1 张进手牌，第 2 张进弃牌堆
            Assert.That(e.Player.Hand.Count, Is.EqualTo(10));
            Assert.That(e.Player.DiscardPile.Select(c => c.Id), Is.EquivalentTo(new[] { "B01", "B03" }));
        }

        [Test]
        public void EmptyDrawPile_ShufflesDiscardBack()
        {
            var e = Battle();
            SetHand(e, "B03");
            SetDrawPile(e);
            e.Player.DiscardPile.Add(e.NewCard("B01"));
            e.Player.DiscardPile.Add(e.NewCard("B01"));
            Play(e, 0);
            Assert.That(e.Player.Hand.Count, Is.EqualTo(2));
            Assert.That(e.Log.Events.Count(BattleEventType.Shuffled), Is.EqualTo(1));
        }

        [Test]
        public void TurnEnd_DiscardsHand_ResetsEnergyAndBlock()
        {
            var e = Battle();
            SetHand(e, "B02", "B02");
            Play(e, 0);
            Assert.That(e.Player.Block, Is.EqualTo(5));
            EndTurn(e);
            Assert.That(e.Player.Block, Is.EqualTo(0));
            Assert.That(e.Player.Energy, Is.EqualTo(3));
            Assert.That(e.Player.Hand.Count, Is.EqualTo(5));
        }

        [Test]
        public void Victory_WhenAllEnemiesDie_ThenCommandsRejected()
        {
            var e = Battle(new[] { EnemyIds.Whisperer });
            e.State.Enemies[0].Hp = 5;
            SetHand(e, "B01", "B01");
            Play(e, 0);
            Assert.That(e.State.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(e.Execute(BattleCommand.Play(0, 0), out _), Is.False);
            Assert.That(e.Execute(BattleCommand.End(), out _), Is.False);
        }

        [Test]
        public void Defeat_WhenPlayerHpReachesZero()
        {
            var e = Battle(new[] { EnemyIds.Acolyte });
            e.Player.Hp = 3;
            SetIntent(e, 0, "Dagger");
            SetHand(e);
            EndTurn(e);
            Assert.That(e.State.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(e.Player.Hp, Is.EqualTo(0));
        }

        // ---------------- 确定性

        static BattleSetup PrototypeSetup(ulong seed, string deck = "B", string encounter = "2") => new BattleSetup
        {
            Seed = seed,
            Deck = TestDecks.Get(deck),
            Enemies = Encounters.Prototype[encounter].ToList(),
        };

        /// <summary>简单脚本：能出的牌从左到右打（攻击打第一个活着的敌人），打不了就结束回合。</summary>
        static List<string> RunScripted(BattleEngine e, int maxCommands)
        {
            var hashes = new List<string> { StateHasher.Hash(e) };
            for (int n = 0; n < maxCommands && e.State.Outcome == BattleOutcome.Ongoing; n++)
            {
                int target = e.State.Enemies.FindIndex(x => !x.IsDead);
                int idx = Enumerable.Range(0, e.Player.Hand.Count).FirstOrDefault(i => e.CanPlay(i, out _), -1);
                var cmd = idx >= 0 ? BattleCommand.Play(idx, target) : BattleCommand.End();
                Assert.That(e.Execute(cmd, out var error), Is.True, error);
                hashes.Add(StateHasher.Hash(e));
            }
            return hashes;
        }

        [TestCase(1UL)]
        [TestCase(42UL)]
        [TestCase(20261016UL)]
        public void SameSeedAndCommands_ProduceIdenticalStates(ulong seed)
        {
            var a = RunScripted(BattleEngine.Start(PrototypeSetup(seed)), 200);
            var b = RunScripted(BattleEngine.Start(PrototypeSetup(seed)), 200);
            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void Replay_ReproducesFinalState()
        {
            var original = BattleEngine.Start(PrototypeSetup(99, "C", "3"));
            RunScripted(original, 200);
            var replayed = BattleEngine.Replay(PrototypeSetup(99, "C", "3"), original.History);
            Assert.That(StateHasher.Hash(replayed), Is.EqualTo(StateHasher.Hash(original)));
            Assert.That(replayed.Log.Events.Count, Is.EqualTo(original.Log.Events.Count));
            Assert.That(replayed.State.Stats.PlaysByCard.Values.Sum(), Is.EqualTo(replayed.State.Stats.CardsPlayed));
        }

        [Test]
        public void DifferentSeeds_Diverge()
        {
            var hashes = Enumerable.Range(1, 20)
                .Select(s => StateHasher.Hash(BattleEngine.Start(PrototypeSetup((ulong)s))))
                .Distinct()
                .Count();
            Assert.That(hashes, Is.GreaterThan(15));
        }

        [Test]
        public void Command_RoundTripsThroughText()
        {
            foreach (var cmd in new[] { BattleCommand.Play(3, 1), BattleCommand.Play(0, -1), BattleCommand.End() })
                Assert.That(BattleCommand.Parse(cmd.ToString()), Is.EqualTo(cmd));
        }

        [Test]
        public void Pcg32_IsDeterministicAndBounded()
        {
            var a = new Pcg32(12345, 1);
            var b = new Pcg32(12345, 1);
            var c = new Pcg32(12345, 2);
            bool streamsDiffer = false;
            for (int i = 0; i < 1000; i++)
            {
                int x = a.NextInt(6);
                Assert.That(b.NextInt(6), Is.EqualTo(x));
                Assert.That(x, Is.InRange(0, 5));
                if (c.NextInt(6) != x) streamsDiffer = true;
            }
            Assert.That(streamsDiffer, Is.True);
            for (int i = 0; i < 1000; i++) Assert.That(a.Range(46, 50), Is.InRange(46, 50));
        }

        // ---------------- 数据校验

        [Test]
        public void AllCards_DescribeBothFaces()
        {
            int uid = 1;
            foreach (var def in CardLibrary.All)
            {
                var card = CardLibrary.Create(def.Id, uid++);
                Assert.That(card.Describe(false), Does.Not.Contain("{"), def.Id);
                if (def.Dual) Assert.That(card.Describe(true), Does.Not.Contain("{"), def.Id);
                var upgraded = CardLibrary.Create(def.Id + "+", uid++);
                Assert.That(upgraded.Name, Does.EndWith("+"));
            }
            Assert.That(CardLibrary.All.Count(), Is.EqualTo(42));   // 40 张角色牌 + 疯语 + 缠网（策划/03 §2）
        }

        [Test]
        public void TestDecks_AreSixteenCardsAndValid()
        {
            foreach (var name in TestDecks.Names.Where(n => n != "starter"))
            {
                var deck = TestDecks.Get(name);
                Assert.That(deck.Count, Is.EqualTo(16), name);
                Assert.That(deck.All(CardLibrary.Exists), Is.True, name);
            }
        }
    }
}
