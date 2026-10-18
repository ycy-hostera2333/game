using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using NUnit.Framework;
using static Deeptide.Tests.TestUtil;

namespace Deeptide.Tests
{
    /// <summary>批次 3.3 / 3.4 的敌人（策划/04），以及它们用到的内核能力（策划/09 M1.5）。</summary>
    public class EnemyContentTests
    {
        static List<string> Moves(BattleEngine e, int enemy, int turns)
        {
            var seq = new List<string>();
            for (int i = 0; i < turns && e.State.Outcome == BattleOutcome.Ongoing; i++)
            {
                seq.Add(e.State.Enemies[enemy].Intent.Id);
                e.Player.Hp = e.Player.MaxHp;
                SetHand(e);
                EndTurn(e);
            }
            return seq;
        }

        [Test]
        public void EncounterPools_MatchDesign()
        {
            Assert.That(EncounterLibrary.InPool(EncounterPool.Easy).Count(), Is.EqualTo(4));
            Assert.That(EncounterLibrary.InPool(EncounterPool.Hard).Count(), Is.EqualTo(7));
            Assert.That(EncounterLibrary.InPool(EncounterPool.Elite).Count(), Is.EqualTo(3));
            Assert.That(EncounterLibrary.InPool(EncounterPool.Boss).Single().Enemies, Is.EqualTo(new[] { EnemyIds.Priest }));
            Assert.That(EnemyLibrary.All.Count(d => d.Id != EnemyIds.Dummy), Is.EqualTo(12), "8 普通 + 3 精英 + 1 Boss");
        }

        [Test]
        public void Drowned_CyclesSoakThenEmbrace_AndKeepsCycleAfterStun()
        {
            var e = Battle(new[] { EnemyIds.Drowned });
            Assert.That(Moves(e, 0, 4), Is.EqualTo(new[] { "Soak", "Embrace", "Soak", "Embrace" }));
            Assert.That(e.State.Enemies[0].Block, Is.EqualTo(0), "上回合的格挡在它行动前清零");
        }

        [Test]
        public void Scaleling_SpinesMakesYouVulnerable()
        {
            var e = Battle(new[] { EnemyIds.Scaleling });
            SetIntent(e, 0, "Spines");
            SetHand(e);
            EndTurn(e);
            Assert.That(e.Player.Hp, Is.EqualTo(67));
            Assert.That(e.Player.Statuses.Get(StatusId.Vulnerable), Is.EqualTo(1));
        }

        [Test]
        public void Crows_PeckIsMultiHit_CawIsMindAndWeak()
        {
            var e = Battle(new[] { EnemyIds.Crows });
            SetIntent(e, 0, "Peck");
            Assert.That(e.GetIntent(e.State.Enemies[0]).Hits, Is.EqualTo(3));
            SetHand(e, "B02");
            Play(e, 0, -1);
            EndTurn(e);
            Assert.That(e.Player.Hp, Is.EqualTo(70 - 1), "5 格挡挡住 2×3 里的 5 点");
            SetIntent(e, 0, "Caw");
            SetHand(e);
            EndTurn(e);
            Assert.That(e.Player.Sanity, Is.EqualTo(9));
            Assert.That(e.Player.Statuses.Get(StatusId.Weak), Is.EqualTo(1), "敌人回合施加的虚弱，整个下回合都有效（自己回合结束时才 −1）");
        }

        [Test]
        public void Tentacle_AlternatesAndIsMindless()
        {
            var e = Battle(new[] { EnemyIds.Tentacle });
            Assert.That(e.State.Enemies[0].Mindless, Is.True);
            var seq = Moves(e, 0, 6);
            for (int i = 1; i < seq.Count; i++) Assert.That(seq[i], Is.Not.EqualTo(seq[i - 1]));
            SetIntent(e, 0, "Entangle");
            SetHand(e);
            SetDrawPile(e, Enumerable.Repeat("B02", 10).ToArray());
            EndTurn(e);
            Assert.That(e.Player.Hand.Count, Is.EqualTo(4), "束缚：下回合少抽 1 张");
        }

        [Test]
        public void Rat_TwoAttacks()
        {
            var e = Battle(new[] { EnemyIds.Rat });
            var kinds = Moves(e, 0, 40).Distinct().OrderBy(x => x);
            Assert.That(kinds, Is.EqualTo(new[] { "Frenzy", "Gnaw" }));
        }

        [Test]
        public void Lighthouse_GlareEachTurn_WardBlocksIt_BellDoesNotRewriteIt()
        {
            var e = Battle(new[] { EnemyIds.Lighthouse }, relics: new[] { "consecrated_bell" });
            Assert.That(e.Player.Sanity, Is.EqualTo(9), "第 1 回合开始就失去 1 理智");
            Assert.That(e.State.Enemies[0].Intent.Id, Is.EqualTo("OilSplash"));
            e.Player.Statuses.Add(StatusId.Ward, 1);
            SetHand(e);
            EndTurn(e);
            Assert.That(e.Player.Statuses.Get(StatusId.Ward), Is.EqualTo(0));
            Assert.That(e.Player.Sanity, Is.EqualTo(9));
            Assert.That(e.Player.Statuses.Get(StatusId.Frail), Is.EqualTo(1));
        }

        [Test]
        public void Nun_IntentHiddenUntilFirstStun()
        {
            var e = Battle(new[] { EnemyIds.Nun });
            var nun = e.State.Enemies[0];
            var view = e.GetIntent(nun);
            Assert.That(view.Hidden, Is.True);
            Assert.That(view.Type, Is.EqualTo(IntentType.Unknown));
            Assert.That(view.Damage, Is.EqualTo(0));
            nun.Fear = 14;
            SetHand(e, "C11");
            Play(e, 0);
            EndTurn(e);
            Assert.That(e.GetIntent(nun).Hidden, Is.False);
            Assert.That(nun.Counter("Revealed"), Is.EqualTo(1));
        }

        [Test]
        public void Nun_NeverAbsolvesAboveEightyPercent()
        {
            var e = Battle(new[] { EnemyIds.Nun });
            var seq = Moves(e, 0, 30);
            Assert.That(seq, Does.Not.Contain("Absolution"));
        }

        [Test]
        public void Fisherman_CycleAndHaulCountsWebs()
        {
            var e = Battle(new[] { EnemyIds.Fisherman });
            var fisher = e.State.Enemies[0];
            Assert.That(fisher.Intent.Id, Is.EqualTo("Cast"));
            SetHand(e);
            EndTurn(e);   // 抛网：2 张缠网进抽牌堆
            var p = e.Player;
            Assert.That(p.DrawPile.Concat(p.Hand).Concat(p.DiscardPile).Count(c => c.Id == CardIds.Web), Is.EqualTo(2));
            Assert.That(fisher.Intent.Id, Is.EqualTo("Harpoon"));
            EndTurn(e);   // 手牌照常弃掉，抽到的缠网也还在牌堆里
            Assert.That(fisher.Intent.Id, Is.EqualTo("Haul"));
            Assert.That(e.GetIntent(fisher).Hits, Is.EqualTo(2));
            var web = p.DrawPile.Concat(p.DiscardPile).Concat(p.Hand).First(c => c.Id == CardIds.Web);
            p.DrawPile.Remove(web);
            p.DiscardPile.Remove(web);
            p.Hand.Remove(web);
            p.Hand.Insert(0, web);
            p.Block = 0;
            Play(e, 0, -1);   // 打掉一张缠网（消耗）→ 收网少一段
            Assert.That(e.GetIntent(fisher).Hits, Is.EqualTo(1));
            int hp = p.Hp;
            EndTurn(e);
            Assert.That(hp - p.Hp, Is.EqualTo(3));
            Assert.That(fisher.Intent.Id, Is.EqualTo("Harpoon"));
        }

        // ---------------- Boss

        static BattleEngine Boss(ulong seed = 1) => Battle(new[] { EnemyIds.Priest }, seed: seed);

        [Test]
        public void Priest_SummonsTwoTentaclesFirst_AsMinions()
        {
            var e = Boss();
            var boss = e.State.Enemies[0];
            Assert.That(boss.Hp, Is.EqualTo(240));
            Assert.That(boss.Intent.Id, Is.EqualTo("Summon"));
            Assert.That(e.GetIntent(boss).Type, Is.EqualTo(IntentType.Summon));
            SetHand(e);
            EndTurn(e);
            var tentacles = e.State.Enemies.Skip(1).ToList();
            Assert.That(tentacles.Count, Is.EqualTo(2));
            Assert.That(tentacles.All(t => t.Minion && t.Def.Id == EnemyIds.Tentacle && t.Intent != null), Is.True);
            Assert.That(tentacles.Select(t => t.Index), Is.EqualTo(new[] { 1, 2 }), "召唤物追加在末尾，下标稳定");
            Assert.That(boss.Counter(DrownedPriest.Tide), Is.EqualTo(1));
        }

        [Test]
        public void Priest_TentacleDeathLowersTide()
        {
            var e = Boss();
            SetHand(e);
            EndTurn(e);
            var boss = e.State.Enemies[0];
            e.SetCounter(boss, DrownedPriest.Tide, 3);
            e.State.Enemies[1].Hp = 1;
            SetHand(e, "B01");
            Play(e, 0, 1);
            Assert.That(boss.Counter(DrownedPriest.Tide), Is.EqualTo(2));
        }

        [Test]
        public void Priest_HighTideForcedAtFive_ResetsToZero_StunKeepsTide()
        {
            var e = Boss();
            var boss = e.State.Enemies[0];
            SetHand(e);
            EndTurn(e);
            e.SetCounter(boss, DrownedPriest.Tide, 4);
            boss.Intent = boss.Behaviour.GetMove("Baptism");
            e.Player.Statuses.Add(StatusId.Ward, 5);
            SetHand(e);
            EndTurn(e);   // 回合结束潮位 → 5，下回合强制大潮
            Assert.That(boss.Counter(DrownedPriest.Tide), Is.EqualTo(5));
            Assert.That(boss.Intent.Id, Is.EqualTo("HighTide"));
            Assert.That(e.IncomingDamage(), Is.GreaterThanOrEqualTo(28));

            boss.Fear = 29;
            SetHand(e, "C11");
            Play(e, 0, 0);   // 震慑掉大潮
            SetHand(e);
            EndTurn(e);
            Assert.That(boss.Counter(DrownedPriest.Tide), Is.EqualTo(5), "大潮被取消，潮位不变");
            Assert.That(boss.Intent.Id, Is.EqualTo("HighTide"), "下回合再试");

            e.Player.Hp = 70;
            e.Player.Block = 0;
            SetHand(e);
            EndTurn(e);
            Assert.That(boss.Counter(DrownedPriest.Tide), Is.EqualTo(0), "大潮后清零，那一回合不再 +1");
        }

        [Test]
        public void Priest_PhaseTwoAtHalfHp()
        {
            var e = Boss();
            var boss = e.State.Enemies[0];
            SetHand(e);
            EndTurn(e);
            int before = e.State.Enemies.Count;
            boss.Statuses.Add(StatusId.Vulnerable, 2);
            boss.Statuses.Add(StatusId.Weak, 1);
            boss.Hp = 125;
            SetHand(e, "B01");
            Play(e, 0, 0);   // 125 − 9 → 116 ≤ 120
            Assert.That(boss.Counter(DrownedPriest.Phase), Is.EqualTo(2));
            Assert.That(boss.Statuses.Get(StatusId.Vulnerable), Is.EqualTo(0));
            Assert.That(boss.Statuses.Get(StatusId.Weak), Is.EqualTo(0));
            Assert.That(boss.Statuses.Get(StatusId.Strength), Is.EqualTo(2));
            Assert.That(e.State.Enemies.Count, Is.EqualTo(before + 1));

            int sanity = e.Player.Sanity;
            SetHand(e);
            EndTurn(e);
            Assert.That(e.Player.Sanity, Is.LessThanOrEqualTo(sanity - 1), "深海之歌：每回合开始额外失去 1 理智");
            var phase2 = new[] { "Devour", "SeaCurse", "Summon", "HighTide2" };
            Assert.That(phase2, Does.Contain(boss.Intent.Id));
        }

        [Test]
        public void Priest_DeathWinsEvenWithTentaclesAlive()
        {
            var e = Boss();
            SetHand(e);
            EndTurn(e);
            Assert.That(e.State.AliveEnemies.Count(), Is.EqualTo(3));
            e.State.Enemies[0].Hp = 5;
            SetHand(e, "B01");
            Play(e, 0, 0);
            Assert.That(e.State.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void Priest_SeaCurseAtMostTwice()
        {
            var e = Boss(seed: 4);
            var boss = e.State.Enemies[0];
            e.SetCounter(boss, DrownedPriest.Phase, 2);
            var seq = Moves(e, 0, 40);
            Assert.That(seq.Count(x => x == "SeaCurse"), Is.LessThanOrEqualTo(2));
            Assert.That(seq.Count(x => x == "HighTide2"), Is.GreaterThan(3));
        }
    }
}
