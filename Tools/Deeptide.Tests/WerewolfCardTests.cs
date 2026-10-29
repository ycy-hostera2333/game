using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Content;
using Deeptide.Core.Enemies;
using Deeptide.Core.Map;
using Deeptide.Core.Random;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;
using NUnit.Framework;
using static Deeptide.Tests.TestUtil;

namespace Deeptide.Tests
{
    /// <summary>狼人的内容（策划/09 M6.3、14 §2）：30 张牌、3 件遗物、血腥诱饵、角色表那一行，以及按角色变化的禁书残页和梦中的导师。</summary>
    public class WerewolfCardTests
    {
        const string Werewolf = "werewolf";

        /// <summary>和跑局里一样的狼人战斗：上限 6、兽血特性；relics 默认为空，测遗物时再给。</summary>
        static BattleEngine Wolf(string[] enemies = null, string[] relics = null, string[] potions = null) =>
            BattleEngine.Start(new BattleSetup
            {
                Seed = 1,
                Deck = Enumerable.Repeat("B02", 10).ToList(),
                Enemies = (enemies ?? new[] { EnemyIds.Dummy }).ToList(),
                MaxHp = 75,
                Hp = 75,
                MaxSanity = 6,
                Relics = (relics ?? new string[0]).ToList(),
                Potions = (potions ?? new string[0]).ToList(),
                Traits = new List<string> { RelicIds.BeastBlood },
            });

        static List<int> HitsSince(BattleEngine e, int mark, int enemy = 0) =>
            e.Log.Events.Skip(mark).Where(x => x.Type == BattleEventType.Damage && x.Entity == enemy).Select(x => x.Amount).ToList();

        // ---------------- 角色表

        [Test]
        public void WerewolfRow_MatchesDesign()
        {
            var ch = CharacterLibrary.Get(Werewolf);
            Assert.That((ch.MaxHp, ch.MaxSanity, ch.Gold), Is.EqualTo((75, 6, 99)), "策划/14 §2.2");
            Assert.That(ch.StartingRelic, Is.EqualTo(RelicIds.WolfFangPendant));
            Assert.That(ch.Trait, Is.EqualTo(RelicIds.BeastBlood));
            Assert.That(ch.Deck, Is.EqualTo(TestDecks.WolfStarter), "和模拟器用的狼人起始牌组一致");
            Assert.That(RelicLibrary.Get(RelicIds.BeastBlood).Character, Is.EqualTo(Werewolf));

            var cards = CardLibrary.All.Where(d => d.Character == Werewolf).ToList();
            Assert.That(cards.Count, Is.EqualTo(30));
            Assert.That(cards.Count(d => d.Rarity == Rarity.Basic), Is.EqualTo(3));
            Assert.That(cards.Count(d => d.Rarity == Rarity.Common), Is.EqualTo(12));
            Assert.That(cards.Count(d => d.Rarity == Rarity.Uncommon), Is.EqualTo(11));
            Assert.That(cards.Count(d => d.Rarity == Rarity.Rare), Is.EqualTo(4));
            Assert.That(cards.Count(d => d.Dual), Is.EqualTo(7));
            Assert.That(cards.GroupBy(d => d.Archetype).ToDictionary(g => g.Key, g => g.Count()),
                Is.EqualTo(new Dictionary<string, int> { ["月相"] = 11, ["狂猎"] = 8, ["血宴"] = 8, ["通用"] = 3 }), "14 §2.5 的统计（月相含 3 张起始专属）");
        }

        [Test]
        public void WerewolfRun_StartsRight_AndOnlyOffersWerewolfAndSharedContent()
        {
            var run = RunEngine.NewRun(7, new RunConfig { Character = Werewolf });
            Assert.That((run.State.MaxHp, run.State.MaxSanity), Is.EqualTo((75, 6)));
            Assert.That(run.State.Relics, Is.EqualTo(new[] { RelicIds.WolfFangPendant }));
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var wolf = RunEngine.NewRun(seed, new RunConfig { Character = Werewolf });
                var inv = RunEngine.NewRun(seed);
                Assert.That(wolf.RollCardChoices(new Pcg32(seed, 1), 3, true).All(id => CardLibrary.Get(id).Character == Werewolf), Is.True);
                Assert.That(inv.RollCardChoices(new Pcg32(seed, 1), 3, true).Any(id => CardLibrary.Get(id).Character == Werewolf), Is.False);
                foreach (var rarity in new[] { RelicRarity.Common, RelicRarity.Uncommon })
                {
                    Assert.That(RelicLibrary.Get(wolf.RollRelic(new Pcg32(seed, 2), rarity)).Character, Is.Not.EqualTo(CharacterIds.Investigator));
                    Assert.That(RelicLibrary.Get(inv.RollRelic(new Pcg32(seed, 2), rarity)).Character, Is.Not.EqualTo(Werewolf));
                }
                Assert.That(inv.RollPotion(new Pcg32(seed, 3)), Is.Not.EqualTo("bloody_bait"));
                Assert.That(wolf.RollPotion(new Pcg32(seed, 3)), Is.Not.EqualTo("fear_powder"));
            }
        }

        [Test]
        public void Shop_RelicFallback_NeverStocksBeastBlood()
        {
            // 普通 / 罕见 / 稀有全拿光，商店的遗物兜底以前会落到「兽血」这类特性上
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var run = RunEngine.NewRun(seed, new RunConfig { Character = Werewolf });
                run.State.Relics.AddRange(RelicLibrary.All
                    .Where(d => d.Rarity == RelicRarity.Common || d.Rarity == RelicRarity.Uncommon || d.Rarity == RelicRarity.Rare)
                    .Select(d => d.Id));
                run.State.Floor = 7;
                run.EnterNode(6, 0, RoomType.Shop);
                var relics = run.State.Shop.Items.Where(i => i.Kind == ShopItemKind.Relic).Select(i => RelicLibrary.Get(i.Id)).ToList();
                Assert.That(relics.All(d => d.Rarity == RelicRarity.Shop), Is.True, $"种子 {seed}：{string.Join(" / ", relics.Select(d => d.Id))}");
            }
        }

        // ---------------- 起始专属牌

        [Test]
        public void Fangs_HumanFaceBlocks_BeastFaceTwoHits()
        {
            var e = Wolf();
            SetHand(e, "LB01", "LB01");
            int mark = e.Log.Events.Count;
            Play(e, 0);
            Assert.That(HitsSince(e, mark), Is.EqualTo(new[] { 3 }));
            Assert.That(e.Player.Block, Is.EqualTo(4));
            e.SetSanity(4);
            mark = e.Log.Events.Count;
            Play(e, 0);
            Assert.That(HitsSince(e, mark), Is.EqualTo(new[] { 5, 5 }), "兽形 (3 + 2) × 2");
        }

        [Test]
        public void HowlThenSilverChain_TransformThenRevertWithFullBlock()
        {
            var e = Wolf(relics: new[] { RelicIds.WolfFangPendant });
            SetHand(e, "LB02", "LB03");
            int hand = e.Player.Hand.Count;
            Play(e, 0);   // 嚎月：6 → 4 变身，抽 1，吊坠再抽 1
            Assert.That(e.Player.Sanity, Is.EqualTo(4));
            Assert.That(e.Player.Hand.Count, Is.EqualTo(hand - 1 + 2));
            Play(e, 0);   // 银链：先恢复到 6（复归，吊坠 4 格挡），再拿 5 格挡（人形，满额）
            Assert.That(e.Player.Sanity, Is.EqualTo(6));
            Assert.That(e.Player.Block, Is.EqualTo(4 + 5));
        }

        // ---------------- 月相

        [Test]
        public void LowGrowl_EnergyOnlyWhenItCausesTheFirstTransform()
        {
            var e = Wolf();
            e.SetSanity(5);
            SetHand(e, "LC08", "LC10", "LC08");
            Play(e, 0);   // 5 → 4 变身
            Assert.That(e.Player.Energy, Is.EqualTo(4));
            Play(e, 0);   // 月光兽形面：复归到 6，再 6 格挡
            e.SetSanity(5);
            int hand = e.Player.Hand.Count;
            Play(e, 0);   // 又变身，但这一轮已经变过：抽牌而不是能量
            Assert.That(e.Player.Energy, Is.EqualTo(3));
            Assert.That(e.Player.Hand.Count, Is.EqualTo(hand - 1 + 1));
        }

        [Test]
        public void SuppressTheBeast_DrawsOnlyIfItReverts()
        {
            var e = Wolf();
            SetHand(e, "LC09", "LC09");
            int hand = e.Player.Hand.Count;
            Play(e, 0);   // 6 → 6，没有跨线
            Assert.That(e.Player.Hand.Count, Is.EqualTo(hand - 1));
            e.SetSanity(3);
            Play(e, 0);   // 3 → 5 复归
            Assert.That(e.Player.Hand.Count, Is.EqualTo(hand - 2 + 2));
        }

        [Test]
        public void Moonlight_HumanFaceGoesDown_BeastFaceComesUpWithFullBlock()
        {
            var e = Wolf();
            SetHand(e, "LC10", "LC10");
            Play(e, 0);
            Assert.That((e.Player.Block, e.Player.Sanity, e.State.TransformedThisRound), Is.EqualTo((6, 4, true)));
            Play(e, 0);
            Assert.That((e.Player.Block, e.Player.Sanity, e.State.RevertedThisRound), Is.EqualTo((12, 6, true)), "先恢复再挡，所以不减半");
        }

        [Test]
        public void FaceInTheMirror_AlwaysCrossesTheLine()
        {
            var e = Wolf();
            SetHand(e, "LU08", "LU08");
            Play(e, 0);
            Assert.That(e.Player.Sanity, Is.EqualTo(4), "人形面：掉到癫狂线");
            e.SetSanity(1);
            Play(e, 0);
            Assert.That(e.Player.Sanity, Is.EqualTo(5), "兽形面：回到线上一格");
            Assert.That(e.Player.Block, Is.EqualTo(6), "回到人形后再挡，满额");
        }

        [Test]
        public void MoonlitTwins_HitsOncePerCrossingThisRound()
        {
            var e = Wolf();
            SetHand(e, "LU07", "LC10", "LC10", "LU07");
            var twins = e.Player.Hand[0];
            Assert.That(twins.PreviewAttack(e, false, e.State.Enemies[0]).Value.Hits, Is.EqualTo(1));
            e.SetEnergy(10);
            Play(e, 1);   // 变身
            Play(e, 1);   // 复归
            Assert.That(twins.PreviewAttack(e, false, e.State.Enemies[0]).Value.Hits, Is.EqualTo(3));
            int mark = e.Log.Events.Count;
            Play(e, 0);
            Assert.That(HitsSince(e, mark), Is.EqualTo(new[] { 8, 8, 8 }));
        }

        [Test]
        public void FeralAndDualAspect_TriggerOncePerRound()
        {
            var e = Wolf();
            SetHand(e, "LU05", "LU06", "LC10", "LC10", "LC10");
            e.SetEnergy(10);
            Play(e, 0);
            Play(e, 0);
            int hand = e.Player.Hand.Count;
            Play(e, 0);   // 变身：狂性抽 2
            Assert.That(e.Player.Hand.Count, Is.EqualTo(hand - 1 + 2));
            int block = e.Player.Block;
            Play(e, 0);   // 复归：双相 6 格挡 + 月光 6 格挡
            Assert.That(e.Player.Block - block, Is.EqualTo(12));
            hand = e.Player.Hand.Count;
            Play(e, e.Player.Hand.FindIndex(c => c.Id == "LC10"));   // 再变身：这一轮不再抽
            Assert.That(e.Player.Hand.Count, Is.EqualTo(hand - 1));
        }

        [Test]
        public void ManAndWolf_MoonlightResolvesBothFaces()
        {
            var e = Wolf();
            SetHand(e, "LR02", "LC10");
            e.SetEnergy(10);
            Play(e, 0);
            Play(e, 0);   // 人形面：6 格挡，6 → 4；兽形面：4 → 6，6 格挡（复归后满额）
            Assert.That(e.Player.Block, Is.EqualTo(12));
            Assert.That(e.Player.Sanity, Is.EqualTo(6));
            Assert.That((e.State.TransformedThisRound, e.State.RevertedThisRound), Is.EqualTo((true, true)));
        }

        // ---------------- 狂猎

        [Test]
        public void PackHunt_RepeatsForEachOtherAttackThisTurn()
        {
            var e = Wolf();
            SetHand(e, "B01", "B01", "LU04");
            e.SetEnergy(10);
            Play(e, 0);
            Play(e, 0);
            Assert.That(e.Player.Hand[0].PreviewAttack(e, false, e.State.Enemies[0]).Value.Hits, Is.EqualTo(3));
            int mark = e.Log.Events.Count;
            Play(e, 0);
            Assert.That(HitsSince(e, mark), Is.EqualTo(new[] { 3, 3, 3 }));
        }

        [Test]
        public void Regeneration_HealsAtTurnEndOnlyInBeastForm()
        {
            var e = Wolf();
            e.SetHp(e.Player, 50);
            SetHand(e, "LU01");
            Play(e, 0);
            EndTurn(e);
            Assert.That(e.Player.Hp, Is.EqualTo(50), "人形不回");
            e.SetSanity(4);
            EndTurn(e);
            Assert.That(e.Player.Hp, Is.EqualTo(53));
        }

        [Test]
        public void EternalNight_SanityStopsAtOne_NoCollapse()
        {
            var e = Wolf();
            SetHand(e, "LR01", "LU02", "LU02", "LB02");
            e.SetEnergy(10);
            Play(e, 0);
            e.SetSanity(2);
            Play(e, 0);   // 啃噬 代价1：2 → 1
            Play(e, 0);   // 再付 1：停在 1
            Play(e, 0);   // 嚎月 代价2：还是 1
            Assert.That(e.Player.Sanity, Is.EqualTo(1));
            Assert.That(e.State.Stats.Collapses, Is.EqualTo(0));
        }

        [Test]
        public void Gorge_KillGivesPermanentMaxHp_EvenWhenItEndsTheFight()
        {
            var run = RunEngine.NewRun(5, new RunConfig { Character = Werewolf });
            run.State.Floor = 1;
            run.EnterNode(0, 0, RoomType.Battle);
            var b = run.Battle;
            foreach (var en in b.State.Enemies.Skip(1)) en.Hp = 0;
            b.SetHp(b.State.Enemies[0], 1);
            SetHand(b, "LR04");
            b.SetEnergy(3);
            Assert.That(run.Execute(RunCommand.Fight(BattleCommand.Play(0, 0)), out var error), Is.True, error);
            Assert.That(run.Battle, Is.Null, "打死最后一个敌人，战斗结束");
            Assert.That(run.State.MaxHp, Is.EqualTo(78), "加的最大生命带回跑局");
        }

        // ---------------- 血宴

        [Test]
        public void Bite_ThroatCut_ScentOfBlood_ApplyBleed()
        {
            var e = Wolf(new[] { EnemyIds.Dummy, EnemyIds.Dummy });
            SetHand(e, "LC01", "LC02", "LC03");
            e.SetEnergy(10);
            Play(e, 0, 0);
            Assert.That(e.State.Enemies[0].Statuses.Get(StatusId.Bleed), Is.EqualTo(3));
            e.SetSanity(4);
            Play(e, 0, 1);   // 割喉兽形面：5 + 2 伤害，4 流血
            Assert.That(e.State.Enemies[1].Statuses.Get(StatusId.Bleed), Is.EqualTo(4));
            Play(e, 0);
            Assert.That(e.State.Enemies.Select(x => x.Statuses.Get(StatusId.Bleed)), Is.EqualTo(new[] { 6, 7 }));
        }

        [Test]
        public void OpenTheArtery_DoublesBleed_BloodFeastDrawsPerBleedingEnemy()
        {
            var e = Wolf(new[] { EnemyIds.Dummy, EnemyIds.Dummy });
            e.SetStatus(e.State.Enemies[0], StatusId.Bleed, 4);
            SetHand(e, "LU10", "LU11");
            e.SetEnergy(10);
            Play(e, 0, 0);
            Assert.That(e.State.Enemies[0].Statuses.Get(StatusId.Bleed), Is.EqualTo(8));
            int hand = e.Player.Hand.Count;
            Play(e, 0);
            Assert.That(e.Player.Hand.Count, Is.EqualTo(hand - 1 + 1), "只有一个敌人在流血");
        }

        [Test]
        public void Bloodthirst_HealsOnBleed_BloodBowlOnBleedDeath()
        {
            var e = Wolf(new[] { EnemyIds.Acolyte }, relics: new[] { "blood_bowl" });
            var en = e.State.Enemies[0];
            e.SetHp(e.Player, 40);
            SetHand(e, "LU09");
            Play(e, 0);
            e.SetHp(en, 3);
            e.SetStatus(en, StatusId.Bleed, 5);
            EndTurn(e);
            Assert.That(en.IsDead, Is.True);
            Assert.That(e.Player.Hp, Is.EqualTo(40 + 1 + 6), "嗜血 1 + 血碗 6");
        }

        [Test]
        public void BarbedClaws_BleedPerUnblockedHit()
        {
            var e = Wolf();
            var dummy = e.State.Enemies[0];
            SetHand(e, "LR03", "LC05");
            e.SetEnergy(10);
            Play(e, 0);
            e.SetBlock(dummy, 2);
            Play(e, 0);   // 连爪 2 × 3：第一段被格挡吃掉
            Assert.That(dummy.Statuses.Get(StatusId.Bleed), Is.EqualTo(2));
        }

        // ---------------- 遗物、药水

        [Test]
        public void WolfPeltCloak_BlockAtTurnEndInBeastForm_NotHalved()
        {
            var e = Wolf(relics: new[] { "wolf_pelt_cloak" });
            e.SetSanity(4);
            SetHand(e);
            EndTurn(e);
            var gained = e.Log.Events.Where(x => x.Type == BattleEventType.BlockGained).Select(x => x.Amount).ToList();
            Assert.That(gained, Is.EqualTo(new[] { 4 }));
        }

        [Test]
        public void BloodyBait_BleedsAllEnemies()
        {
            var e = Wolf(new[] { EnemyIds.Dummy, EnemyIds.Dummy }, potions: new[] { "bloody_bait" });
            UsePotion(e, 0);
            Assert.That(e.State.Enemies.Select(x => x.Statuses.Get(StatusId.Bleed)), Is.EqualTo(new[] { 5, 5 }));
        }

        // ---------------- 禁书残页、梦中的导师

        [Test]
        public void ForbiddenPage_OnlyOffersItsOwnCharactersUncommons()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var e = Battle(seed: seed);
                SetHand(e, "U15");
                Play(e, 0);
                Assert.That(e.State.Choice.Options.All(o => CardLibrary.Get(o.CardId).Character == CharacterIds.Investigator), Is.True, $"种子 {seed}");
            }
        }

        [Test]
        public void DreamMentor_UsesTheCharactersDreamCardAndSanityCap()
        {
            var run = RunEngine.NewRun(12, new RunConfig { Character = Werewolf });
            run.State.Floor = 10;
            run.State.Event = new EventState { Id = "dream_mentor" };
            run.State.Phase = RunPhase.Event;
            var labels = run.Options().Select(o => o.Label).ToList();
            Assert.That(labels[0], Does.Contain("撕咬").And.Not.Contain("梦魇之刃"));
            Assert.That(labels[1], Does.Contain("理智上限恢复到 6"));

            run.State.Deck[0] = "B01+";
            Assert.That(run.Execute(RunCommand.Pick(0), out var error), Is.True, error);
            Assert.That(run.State.Deck[0], Is.EqualTo("LC01+"), "保留升级");

            var wake = RunEngine.NewRun(12, new RunConfig { Character = Werewolf });
            wake.State.Floor = 10;
            wake.State.MaxSanity = 4;
            wake.State.Event = new EventState { Id = "dream_mentor" };
            wake.State.Phase = RunPhase.Event;
            Assert.That(wake.Execute(RunCommand.Pick(1), out error), Is.True, error);
            Assert.That(wake.State.MaxSanity, Is.EqualTo(6), "恢复到狼人的初始上限，不是 10");

            var inv = RunEngine.NewRun(12);
            inv.State.Floor = 10;
            inv.State.Event = new EventState { Id = "dream_mentor" };
            inv.State.Phase = RunPhase.Event;
            var invLabels = inv.Options().Select(o => o.Label).ToList();
            Assert.That(invLabels[0], Does.Contain("梦魇之刃"));
            Assert.That(invLabels[1], Does.Contain("理智上限恢复到 10"));
        }
    }
}
