using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using Deeptide.Core.Potions;
using Deeptide.Core.Relics;
using NUnit.Framework;
using static Deeptide.Tests.TestUtil;

namespace Deeptide.Tests
{
    /// <summary>遗物和药水（策划/05），以及它们用到的修正钩子（策划/09 M1.3、M1.6）。</summary>
    public class RelicPotionTests
    {
        static BattleEngine With(string relic, string[] enemies = null) =>
            Battle(enemies, relics: new[] { RelicIds.InvestigatorNotes, relic });

        [Test]
        public void AllRelicsAndPotions_HaveDescriptions()
        {
            Assert.That(RelicLibrary.All.Count, Is.EqualTo(20));
            Assert.That(PotionLibrary.All.Count, Is.EqualTo(8));
            foreach (var d in RelicLibrary.All) Assert.That(RelicLibrary.Create(d.Id).Description, Does.Not.Contain("{"), d.Id);
            foreach (var d in PotionLibrary.All) Assert.That(PotionLibrary.Create(d.Id).Description, Does.Not.Contain("{"), d.Id);
        }

        [Test]
        public void OldPipe_WardAtCombatStart() =>
            Assert.That(With("old_pipe").Player.Statuses.Get(StatusId.Ward), Is.EqualTo(1));

        [Test]
        public void SaltCircle_BlockOnFirstTurnOnly()
        {
            var e = With("salt_circle");
            Assert.That(e.Player.Block, Is.EqualTo(10));
            EndTurn(e);
            Assert.That(e.Player.Block, Is.EqualTo(0));
        }

        [Test]
        public void BlackCandle_FearsAllAtStart()
        {
            var e = With("black_candle", new[] { EnemyIds.Whisperer, EnemyIds.Acolyte });
            Assert.That(e.State.Enemies.Select(x => x.Fear), Is.All.EqualTo(3));
        }

        [Test]
        public void WhaleOilLamp_KeepsIntentsClearWhenLunatic()
        {
            var e = With("whale_oil_lamp");
            e.Player.Sanity = 2;
            Assert.That(e.IntentsBlurred, Is.False);
        }

        [Test]
        public void WornPrayerBook_FirstRestoreOnly()
        {
            var e = With("worn_prayer_book");
            e.Player.Sanity = 2;
            SetHand(e, "C04", "C04");
            Play(e, 0, -1);
            Assert.That(e.Player.Sanity, Is.EqualTo(6));
            Play(e, 0, -1);
            Assert.That(e.Player.Sanity, Is.EqualTo(8));
        }

        [Test]
        public void LeadAmulet_HalvesCollapseHpLoss()
        {
            var e = With("lead_amulet");
            e.Player.Sanity = 1;
            SetHand(e, "B03");
            Play(e, 0, -1);
            Assert.That(e.Player.Hp, Is.EqualTo(70 - 3));
            Assert.That(e.State.Stats.CursesGained, Is.EqualTo(1));
        }

        [Test]
        public void DrownedRing_FirstCollapseIsFree_SecondIsNot()
        {
            var e = Battle(relics: new[] { "drowned_ring", "lead_amulet" });
            e.Player.Sanity = 1;
            SetHand(e, "B03", "C08");
            Play(e, 0, -1);
            Assert.That(e.Player.Hp, Is.EqualTo(70));
            Assert.That(e.State.Stats.Collapses, Is.EqualTo(1));
            Assert.That(e.State.Stats.CursesGained, Is.EqualTo(0));
            Assert.That(e.Player.DiscardPile.Any(c => c.Id == CardIds.Whispers), Is.False);
            e.Player.Sanity = 2;
            Play(e, 0, -1);
            Assert.That(e.Player.Hp, Is.EqualTo(67), "第二次照常崩溃（铅制护符减半）");
            Assert.That(e.State.Stats.CursesGained, Is.EqualTo(1));
        }

        [TestCase(false, 63)]
        [TestCase(true, 53)]
        public void FishermansCharm_HealsOnVictoryWithoutCollapse(bool collapse, int expectedHp)
        {
            var e = With("fishermans_charm", new[] { EnemyIds.Whisperer });
            e.Player.Hp = 60;
            if (collapse)
            {
                e.Player.Sanity = 1;
                SetHand(e, "B03");
                Play(e, 0, -1);
            }
            e.State.Enemies[0].Hp = 1;
            SetHand(e, "B01");
            Play(e, 0);
            Assert.That(e.State.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(e.Player.Hp, Is.EqualTo(expectedHp));
        }

        [Test]
        public void BrokenWatch_LoseSanityGainStrength()
        {
            var e = With("broken_watch");
            Assert.That(e.Player.Sanity, Is.EqualTo(6));
            Assert.That(e.Player.Statuses.Get(StatusId.Strength), Is.EqualTo(1));
        }

        [Test]
        public void ThirdEye_ExtraDrawWhenLunatic()
        {
            var e = With("third_eye");
            Assert.That(e.Player.Hand.Count, Is.EqualTo(5));
            e.Player.Sanity = 4;
            SetDrawPile(e, Enumerable.Repeat("B02", 10).ToArray());
            EndTurn(e);
            Assert.That(e.Player.Hand.Count, Is.EqualTo(6));
        }

        [Test]
        public void SilverCross_BlockAtEndOfTurnIfClarity()
        {
            var e = With("silver_cross", new[] { EnemyIds.Acolyte });
            SetIntent(e, 0, "Dagger");
            SetHand(e);
            EndTurn(e);
            Assert.That(e.Player.Hp, Is.EqualTo(70), "6 格挡挡住 6 点匕首");
        }

        [Test]
        public void FearMask_EnergyOnStun_CarriesOverWhenStunnedAfterTurnEnd()
        {
            var e = With("fear_mask", new[] { EnemyIds.Whisperer });
            SetHand(e, "C12", "U11");
            Play(e, 0);   // 4 恐惧（呓语者意志 5）
            Assert.That(e.Player.Energy, Is.EqualTo(2));
            Play(e, 0, -1);   // 恐惧之源：回合结束时 +2 → 6 ≥ 5 震慑
            EndTurn(e);
            Assert.That(e.State.Enemies[0].Will, Is.EqualTo(8), "回合结束时被震慑");
            Assert.That(e.Player.Energy, Is.EqualTo(4), "回合结束后获得的能量顺延到下回合");
        }

        [Test]
        public void ConsecratedBell_FirstMindAttackRestoresInstead_BeforeWard()
        {
            var e = With("consecrated_bell", new[] { EnemyIds.Whisperer });
            e.Player.Sanity = 6;
            e.Player.Statuses.Add(StatusId.Ward, 1);
            SetIntent(e, 0, "Whisper");
            SetHand(e);
            EndTurn(e);
            Assert.That(e.Player.Sanity, Is.EqualTo(8));
            Assert.That(e.Player.Statuses.Get(StatusId.Ward), Is.EqualTo(1), "银铃先于心防");
            SetIntent(e, 0, "Shriek");
            SetHand(e);
            EndTurn(e);
            Assert.That(e.Player.Sanity, Is.EqualTo(8), "第二次被心防挡住");
            Assert.That(e.Player.Statuses.Get(StatusId.Ward), Is.EqualTo(0));
        }

        [TestCase(10, 6)]
        [TestCase(8, 5)]
        public void WatchersLantern_ExtraDrawNextTurnIfClarity(int sanity, int expectedHand)
        {
            var e = With("watchers_lantern");
            e.Player.Sanity = sanity;
            SetHand(e);
            SetDrawPile(e, Enumerable.Repeat("B02", 10).ToArray());
            EndTurn(e);
            Assert.That(e.Player.Hand.Count, Is.EqualTo(expectedHand));
        }

        [Test]
        public void AbyssalFragment_StrengthPerThreeLost()
        {
            var e = With("abyssal_fragment");
            SetHand(e, "C08", "C08");
            Play(e, 0, -1);
            Assert.That(e.Player.Statuses.Get(StatusId.Strength), Is.EqualTo(0));
            Play(e, 0, -1);
            Assert.That(e.Player.Statuses.Get(StatusId.Strength), Is.EqualTo(1));
        }

        [Test]
        public void CrownOfDread_StunnedEnemiesTakeMore()
        {
            var e = With("crown_of_dread", new[] { EnemyIds.Acolyte });
            SetHand(e, "U13", "B01");
            e.State.Enemies[0].Fear = 3;
            Play(e, 0);   // 3 + 5 ≥ 8 → 震慑
            int hp = e.State.Enemies[0].Hp;
            Play(e, 0);
            Assert.That(hp - e.State.Enemies[0].Hp, Is.EqualTo(9));
        }

        // ---------------- 药水

        [Test]
        public void Potions_EachEffect()
        {
            var e = Battle(new[] { EnemyIds.Dummy, EnemyIds.Dummy },
                potions: new[] { "smelling_salts", "iron_tonic", "powder_flask" });
            e.Player.Sanity = 3;
            e.Player.Statuses.Add(StatusId.Dexterity, 5);
            UsePotion(e, 0);
            Assert.That(e.Player.Sanity, Is.EqualTo(8));
            UsePotion(e, 1);
            Assert.That(e.Player.Block, Is.EqualTo(12), "药水的格挡不吃敏捷");
            e.Player.Statuses.Add(StatusId.Strength, 5);
            UsePotion(e, 2);
            Assert.That(e.State.Enemies.Select(x => x.Hp), Is.All.EqualTo(190), "火药瓶是效果伤害，不吃力量");
            Assert.That(e.Player.Potions, Is.All.Null);
            Assert.That(e.Player.Energy, Is.EqualTo(3), "药水不耗能量");
            Assert.That(e.State.Stats.PotionsUsed, Is.EqualTo(3));
            Assert.That(e.Execute(BattleCommand.Potion(0), out var error), Is.False);
            Assert.That(error, Is.EqualTo("这个药水栏是空的"));
        }

        [Test]
        public void Potions_TargetedAndStatus()
        {
            var e = Battle(new[] { EnemyIds.Whisperer }, potions: new[] { "acid_vial", "fear_powder", "berserk_tincture", "warding_draught" });
            Assert.That(e.Player.Potions.Count, Is.EqualTo(4), "Setup 给的药水多于默认栏数时扩栏");
            Assert.That(e.Execute(BattleCommand.Potion(0, 5), out var error), Is.False);
            Assert.That(error, Is.EqualTo("目标无效"));
            UsePotion(e, 0, 0);
            Assert.That(e.State.Enemies[0].Statuses.Get(StatusId.Vulnerable), Is.EqualTo(3));
            UsePotion(e, 1);
            Assert.That(e.State.Enemies[0].Stunned, Is.True);
            UsePotion(e, 2);
            UsePotion(e, 3);
            Assert.That(e.Player.Statuses.Get(StatusId.Strength), Is.EqualTo(2));
            Assert.That(e.Player.Statuses.Get(StatusId.Ward), Is.EqualTo(2));
        }

        [Test]
        public void Hallucinogen_SetsSanityToFour_TriggersLunacy()
        {
            var e = Battle(potions: new[] { "hallucinogen" });
            SetDrawPile(e, Enumerable.Repeat("B01", 6).ToArray());
            SetHand(e);
            UsePotion(e, 0);
            Assert.That(e.Player.Sanity, Is.EqualTo(4));
            Assert.That(e.Player.IsLunatic, Is.True);
            Assert.That(e.Player.Hand.Count, Is.EqualTo(4), "调查笔记抽 2 + 药水抽 2");
            Assert.That(e.Player.Energy, Is.EqualTo(4));
        }
    }
}
