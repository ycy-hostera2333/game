using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using Deeptide.Core.Map;
using Deeptide.Core.Random;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;
using Deeptide.Tools;
using NUnit.Framework;

namespace Deeptide.Tests
{
    /// <summary>跑局内核（策划/09 M4）：地图、节点、奖励、商店、篝火、事件、整局回放和存读档。</summary>
    public class RunTests
    {
        // ---------------- 地图（M4.2）

        [Test]
        public void Maps_ThousandSeeds_NoHardViolations()
        {
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                var map = MapGenerator.Generate(new Pcg32(seed, 11));
                var v = MapGenerator.Violations(map);
                Assert.That(v, Is.Empty, $"种子 {seed}：{string.Join("；", v)}");
                Assert.That(map.InRow(0).Count(), Is.GreaterThanOrEqualTo(2), "第 1、2 条路径起点不同");
                Assert.That(map.InRow(map.Height - 1).All(n => n.Room == RoomType.Campfire));
                Assert.That(map.InRow(8).All(n => n.Room == RoomType.Treasure));
            }
        }

        [Test]
        public void Map_SameSeedSameMap()
        {
            var a = MapGenerator.Render(MapGenerator.Generate(new Pcg32(7, 11)));
            var b = MapGenerator.Render(MapGenerator.Generate(new Pcg32(7, 11)));
            Assert.That(b, Is.EqualTo(a));
            Assert.That(MapGenerator.Render(MapGenerator.Generate(new Pcg32(8, 11))), Is.Not.EqualTo(a));
        }

        // ---------------- 脚本玩家：任意一个节点都能走下去

        /// <summary>按 step 轮换着选可选的选项；战斗用 EventStreamTests 的脚本。</summary>
        static RunCommand Scripted(RunEngine run, int step)
        {
            if (run.State.Phase == RunPhase.Battle) return RunCommand.Fight(EventStreamTests.Scripted(run.Battle, step));
            var options = run.Options();
            var enabled = Enumerable.Range(0, options.Count).Where(i => options[i].Enabled).ToList();
            // 偏向「拿东西」：奖励里先拿完再走，避免一路跳过
            if (run.State.Phase == RunPhase.Reward && enabled.Count > 1) return RunCommand.Pick(enabled[0]);
            return RunCommand.Pick(enabled[step % enabled.Count]);
        }

        sealed class Recording
        {
            public RunEngine Run;
            public readonly List<(int CommandIndex, string Json)> Saves = new List<(int, string)>();
        }

        static Recording PlayScripted(ulong seed, int maxSteps = 6000)
        {
            var rec = new Recording { Run = RunEngine.NewRun(seed) };
            var run = rec.Run;
            int saved = -1;
            for (int step = 0; step < maxSteps && !run.IsOver; step++)
            {
                if (run.SavePoints != saved)
                {
                    saved = run.SavePoints;
                    rec.Saves.Add((run.History.Count, RunSaves.ToJson(run.SaveState())));
                }
                var cmd = Scripted(run, step);
                Assert.That(run.Execute(cmd, out var error), Is.True, $"第 {step} 步 {cmd}（{run.State.Phase}）：{error}");
            }
            return rec;
        }

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        public void FullRun_ReplayReproducesFinalHash(ulong seed)
        {
            var run = PlayScripted(seed).Run;
            Assert.That(run.State.Stats.Battles.Count, Is.GreaterThan(0));
            var replayed = RunEngine.Replay(seed, run.History);
            Assert.That(replayed.Hash(), Is.EqualTo(run.Hash()));
            Assert.That(replayed.State.Deck, Is.EqualTo(run.State.Deck));
        }

        [TestCase(4UL)]
        [TestCase(5UL)]
        public void SaveLoad_AtEverySavePoint_ContinuesToSameEnd(ulong seed)
        {
            var rec = PlayScripted(seed);
            string final = rec.Run.Hash();
            var commands = rec.Run.History;
            Assert.That(rec.Saves.Count, Is.GreaterThan(10));
            foreach (var (index, json) in rec.Saves)
            {
                var loaded = RunEngine.Load(RunSaves.FromJson(json));
                for (int i = index; i < commands.Count; i++)
                    Assert.That(loaded.Execute(commands[i], out var error), Is.True, $"存档点 {index} 之后第 {i} 条 {commands[i]}：{error}");
                Assert.That(loaded.Hash(), Is.EqualTo(final), $"存档点 {index}");
            }
        }

        [Test]
        public void Save_IsJsonRoundTrip()
        {
            var run = RunEngine.NewRun(9);
            var json = RunSaves.ToJson(run.SaveState());
            Assert.That(json, Does.Contain("\"Version\": 1"));
            var again = RunSaves.ToJson(RunSaves.FromJson(json));
            Assert.That(again, Is.EqualTo(json));
            var bad = RunSaves.FromJson(json);
            bad.Version = 99;
            Assert.Throws<System.InvalidOperationException>(() => RunEngine.Load(bad));
        }

        [Test]
        public void Command_RoundTripsThroughText()
        {
            foreach (var cmd in new[] { RunCommand.Pick(3), RunCommand.DropPotion(1), RunCommand.Fight(BattleCommand.Play(2, 0)),
                                        RunCommand.Fight(BattleCommand.End()), RunCommand.Fight(BattleCommand.Choose(1)), RunCommand.Fight(BattleCommand.Potion(0, -1)) })
                Assert.That(RunCommand.Parse(cmd.ToString()), Is.EqualTo(cmd));
        }

        // ---------------- 节点

        static RunEngine At(RoomType room, ulong seed = 11, int row = 6)
        {
            var run = RunEngine.NewRun(seed);
            run.State.Floor = row + 1;
            run.EnterNode(row, 0, room);
            return run;
        }

        static void Pick(RunEngine run, string tag)
        {
            var options = run.Options();
            int i = options.FindIndex(o => o.Tag == tag || (o.Tag != null && o.Tag.StartsWith(tag)));
            Assert.That(i, Is.GreaterThanOrEqualTo(0), $"没有选项 {tag}：{string.Join(" / ", options.Select(o => o.Tag))}");
            Assert.That(run.Execute(RunCommand.Pick(i), out var error), Is.True, error);
        }

        static void WinBattle(RunEngine run)
        {
            foreach (var en in run.Battle.State.Enemies) en.Hp = 1;
            var hand = run.Battle.Player.Hand;
            hand.Clear();
            hand.Add(run.Battle.NewCard("C05"));   // 圣水泼洒：全体伤害、没有理智代价
            run.Battle.Player.Energy = 3;
            Assert.That(run.Execute(RunCommand.Fight(BattleCommand.Play(0, -1)), out var error), Is.True, error);
            Assert.That(run.Battle, Is.Null);
        }

        [Test]
        public void FirstThreeNormalFightsAreEasy_ThenHard_NoImmediateRepeat()
        {
            var run = RunEngine.NewRun(21);
            var pools = new List<EncounterPool>();
            for (int i = 0; i < 12; i++)
            {
                run.EnterNode(1, 0, RoomType.Battle);
                pools.Add(EncounterLibrary.Get(run.State.Battle.EncounterId).Pool);
                WinBattle(run);
                Pick(run, "leave");
            }
            Assert.That(pools.Take(3), Is.All.EqualTo(EncounterPool.Easy));
            Assert.That(pools.Skip(3), Is.All.EqualTo(EncounterPool.Hard));
            var seq = run.State.RecentEncounters;
            for (int i = 1; i < seq.Count; i++) Assert.That(seq[i], Is.Not.EqualTo(seq[i - 1]));
        }

        [Test]
        public void BattleRewards_GoldCardsAndPotionChance()
        {
            var run = At(RoomType.Battle);
            int gold = run.State.Gold;
            WinBattle(run);
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Reward));
            var rewards = run.State.Rewards;
            var g = rewards.Single(r => r.Kind == RewardKind.Gold);
            Assert.That(g.Amount, Is.InRange(10, 20));
            var cards = rewards.Single(r => r.Kind == RewardKind.Cards);
            Assert.That(cards.Cards.Count, Is.EqualTo(3));
            Assert.That(cards.Cards.Distinct().Count(), Is.EqualTo(3));
            Assert.That(cards.Cards.All(c => CardLibrary.Get(c).Rarity != Rarity.Basic && CardLibrary.Get(c).Type != CardType.Curse));
            bool potion = rewards.Any(r => r.Kind == RewardKind.Potion);
            Assert.That(run.State.PotionChance, Is.EqualTo(potion ? 30 : 50));

            Pick(run, "gold");
            Assert.That(run.State.Gold, Is.EqualTo(gold + g.Amount));
            Pick(run, "cards");
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Select));
            string chosen = cards.Cards[1];
            Pick(run, "card:" + chosen);
            Assert.That(run.State.Deck.Last(), Is.EqualTo(chosen));
            Assert.That(run.State.Stats.CardChoices.Single().Picked, Is.EqualTo(chosen));
            Pick(run, "leave");
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Map));
        }

        [Test]
        public void EliteRewards_IncludeRelic_ElitesDoNotRepeat()
        {
            var run = RunEngine.NewRun(5);
            var seen = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                run.EnterNode(6, 0, RoomType.Elite);
                seen.Add(run.State.Battle.EncounterId);
                WinBattle(run);
                Assert.That(run.State.Rewards.Count(r => r.Kind == RewardKind.Relic), Is.EqualTo(1));
                Assert.That(run.State.Rewards.Single(r => r.Kind == RewardKind.Gold).Amount, Is.InRange(25, 35));
                Pick(run, "relic:");
                Pick(run, "leave");
            }
            Assert.That(seen.Distinct().Count(), Is.EqualTo(3));
            Assert.That(run.State.Relics.Count, Is.EqualTo(4));
        }

        [Test]
        public void RarePity_ShiftsUpOnCommonsAndResetsOnRare()
        {
            var run = RunEngine.NewRun(1);
            var rng = new Pcg32(3, 99);
            int pity = run.State.RarePity;
            Assert.That(pity, Is.EqualTo(-5));
            bool sawRare = false;
            for (int i = 0; i < 400; i++)
            {
                int before = run.State.RarePity;
                var r = run.RollCardRarity(rng, false, true);
                if (r == Rarity.Common) Assert.That(run.State.RarePity, Is.EqualTo(before + 1));
                if (r == Rarity.Rare) { Assert.That(run.State.RarePity, Is.EqualTo(-5)); sawRare = true; }
                if (r == Rarity.Uncommon) Assert.That(run.State.RarePity, Is.EqualTo(before));
            }
            Assert.That(sawRare, Is.True);
        }

        [Test]
        public void Treasure_GivesRelicAndGold()
        {
            var run = At(RoomType.Treasure, row: 8);
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Reward));
            Assert.That(run.State.Rewards.Select(r => r.Kind), Is.EquivalentTo(new[] { RewardKind.Relic, RewardKind.Gold }));
            Assert.That(run.State.Rewards.Single(r => r.Kind == RewardKind.Gold).Amount, Is.InRange(25, 50));
        }

        [Test]
        public void PotionReward_WhenSlotsFull_AsksWhichToDrop()
        {
            var run = At(RoomType.Battle, seed: 2);
            WinBattle(run);
            run.State.Potions = new List<string> { "iron_tonic", "acid_vial", "fear_powder" };
            run.State.Rewards.Add(new RewardItem { Kind = RewardKind.Potion, Id = "smelling_salts" });
            Pick(run, "potion:smelling_salts");
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Select));
            Pick(run, "drop:1");
            Assert.That(run.State.Potions, Is.EqualTo(new[] { "iron_tonic", "smelling_salts", "fear_powder" }));
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Reward));
            Assert.That(run.Execute(RunCommand.DropPotion(0), out _), Is.True);
            Assert.That(run.State.Potions[0], Is.Null);
        }

        [Test]
        public void Shop_Inventory_Prices_RemovalAndDiscount()
        {
            var run = At(RoomType.Shop);
            var shop = run.State.Shop;
            var cards = shop.Items.Where(i => i.Kind == ShopItemKind.Card).ToList();
            Assert.That(cards.Count, Is.EqualTo(5));
            Assert.That(cards.Count(c => CardLibrary.Get(c.Id).Type == CardType.Power), Is.GreaterThanOrEqualTo(1));
            Assert.That(shop.Items.Count(i => i.Kind == ShopItemKind.Relic), Is.EqualTo(3));
            Assert.That(shop.Items.Count(i => i.Kind == ShopItemKind.Relic && RelicLibrary.Get(i.Id).Rarity == RelicRarity.Shop), Is.EqualTo(1));
            Assert.That(shop.Items.Count(i => i.Kind == ShopItemKind.Potion), Is.EqualTo(3));
            Assert.That(cards.Count(c => c.Price < 45), Is.EqualTo(1), "随机一张半价");

            run.State.Gold = 1000;
            Assert.That(run.RemovalPrice, Is.EqualTo(75));
            Pick(run, "remove");
            Pick(run, "deck:0");
            Assert.That(run.State.Gold, Is.EqualTo(925));
            Assert.That(run.State.Removals, Is.EqualTo(1));
            Assert.That(run.Options().Single(o => o.Tag == "remove").Enabled, Is.False, "每次进店只能删一张");
            Assert.That(run.RemovalPrice, Is.EqualTo(100));

            var scale = shop.Items.FirstOrDefault(i => i.Id == RelicIds.PawnbrokersScale);
            run.State.Relics.Add(RelicIds.PawnbrokersScale);
            Assert.That(run.PriceOf(100), Is.EqualTo(80));
            Assert.That(run.RemovalPrice, Is.EqualTo(80));
        }

        [Test]
        public void Shop_BuyCardAndRelic_AlchemistBeltAddsSlot()
        {
            var run = At(RoomType.Shop, seed: 3);
            run.State.Gold = 2000;
            var card = run.State.Shop.Items.First(i => i.Kind == ShopItemKind.Card);
            Pick(run, "shop:card:" + card.Id);
            Assert.That(run.State.Deck.Last(), Is.EqualTo(card.Id));
            Assert.That(card.Sold, Is.True);
            int slots = run.State.Potions.Count;
            run.AddRelic(RelicIds.AlchemistBelt);
            Assert.That(run.State.Potions.Count, Is.EqualTo(slots + 1));
            Pick(run, "leave");
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Map));
        }

        [Test]
        public void CollectorsLens_FourCardChoices()
        {
            var run = At(RoomType.Battle, seed: 4);
            run.State.Relics.Add(RelicIds.CollectorsLens);
            WinBattle(run);
            Assert.That(run.State.Rewards.Single(r => r.Kind == RewardKind.Cards).Cards.Count, Is.EqualTo(4));
        }

        [Test]
        public void Campfire_RestSmithMeditatePurge()
        {
            var run = At(RoomType.Campfire);
            run.State.Hp = 30;
            Pick(run, "rest");
            Assert.That(run.State.Hp, Is.EqualTo(30 + 21));
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Map));

            run.EnterNode(7, 0, RoomType.Campfire);
            Pick(run, "smith");
            Pick(run, "skip");
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Campfire), "取消锻造回到篝火");
            Pick(run, "smith");
            Pick(run, "deck:0");
            Assert.That(run.State.Deck[0], Is.EqualTo("B01+"));
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Map));

            run.EnterNode(8, 0, RoomType.Campfire);
            Assert.That(run.Options().Any(o => o.Tag == "purge"), Is.False);
            Pick(run, "meditate");
            Assert.That(run.State.MaxSanity, Is.EqualTo(11));

            run.State.Deck.Add(CardIds.Whispers);
            run.EnterNode(9, 0, RoomType.Campfire);
            Pick(run, "purge");
            Assert.That(run.State.Deck, Does.Not.Contain(CardIds.Whispers));
        }

        [Test]
        public void CollapseCurses_AreAddedToDeckAfterBattle()
        {
            var run = At(RoomType.Battle, seed: 6);
            var b = run.Battle;
            b.Player.Sanity = 1;
            b.Player.Hand.Clear();
            b.Player.Hand.Add(b.NewCard("B03"));
            Assert.That(run.Execute(RunCommand.Fight(BattleCommand.Play(0, -1)), out var err), Is.True, err);
            Assert.That(b.State.Stats.CursesGained, Is.EqualTo(1));
            WinBattle(run);
            Assert.That(run.State.Deck.Count(c => c == CardIds.Whispers), Is.EqualTo(1));
            Assert.That(run.State.Hp, Is.EqualTo(70 - 7));
        }

        [Test]
        public void BossVictory_EndsTheRun_DefeatToo()
        {
            var run = RunEngine.NewRun(8);
            run.EnterNode(run.State.Map.Height, 0, RoomType.Boss);
            Assert.That(run.State.Stats.HpAtBoss, Is.EqualTo(70));
            WinBattle(run);
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Victory));
            Assert.That(run.Execute(RunCommand.Pick(0), out var error), Is.False);
            Assert.That(error, Is.EqualTo("这一局已经结束"));

            var lost = At(RoomType.Battle, seed: 9);
            lost.Battle.Player.Hp = 1;
            foreach (var en in lost.Battle.State.Enemies) en.Intent = en.Behaviour.Moves.First(m => m.Damage > 0);
            lost.Battle.Player.Hand.Clear();
            Assert.That(lost.Execute(RunCommand.Fight(BattleCommand.End()), out _), Is.True);
            Assert.That(lost.State.Phase, Is.EqualTo(RunPhase.Defeat));
            Assert.That(lost.State.Stats.DeathEncounter, Is.Not.Null);
        }

        // ---------------- 事件

        static RunEngine AtEvent(string id, ulong seed = 12)
        {
            var run = RunEngine.NewRun(seed);
            run.State.Floor = 10;
            run.State.Event = new EventState { Id = id };
            run.State.Phase = RunPhase.Event;
            return run;
        }

        [Test]
        public void Events_AllOptionsWork()
        {
            foreach (var def in EventLibrary.All)
                foreach (var opt in def.Options)
                {
                    var run = AtEvent(def.Id);
                    run.State.Gold = 500;
                    run.State.Deck.Add(CardIds.Whispers);
                    var options = run.Options();
                    int i = options.FindIndex(o => o.Tag == opt.Key);
                    Assert.That(options[i].Enabled, Is.True, $"{def.Id}.{opt.Key}：{options[i].Reason}");
                    Assert.That(run.Execute(RunCommand.Pick(i), out var error), Is.True, $"{def.Id}.{opt.Key}：{error}");
                    // 有二级选择就选第一项
                    for (int k = 0; k < 3 && (run.State.Phase == RunPhase.Select || run.State.Phase == RunPhase.Event); k++)
                        Assert.That(run.Execute(RunCommand.Pick(0), out error), Is.True, $"{def.Id}.{opt.Key} 后续：{error}");
                    Assert.That(run.State.Phase, Is.AnyOf(RunPhase.Map, RunPhase.Battle), $"{def.Id}.{opt.Key}");
                }
        }

        [Test]
        public void ForbiddenBook_ReadCostsMaxSanityForARare()
        {
            var run = AtEvent("forbidden_book");
            int deck = run.State.Deck.Count;
            Pick(run, "Read");
            Assert.That(run.State.MaxSanity, Is.EqualTo(8));
            Assert.That(run.State.Deck.Count, Is.EqualTo(deck + 1));
            Assert.That(CardLibrary.Get(run.State.Deck.Last()).Rarity, Is.EqualTo(Rarity.Rare));
        }

        [Test]
        public void Altar_GoldOptionNeedsGold_AndRemovesACard()
        {
            var run = AtEvent("seaside_altar");
            run.State.Gold = 50;
            Assert.That(run.Options().Single(o => o.Tag == "Gold").Enabled, Is.False);
            run.State.Gold = 100;
            int deck = run.State.Deck.Count;
            Pick(run, "Gold");
            Assert.That(run.Options().Any(o => o.Tag == "skip"), Is.False, "付了钱就必须删");
            Pick(run, "deck:3");
            Assert.That(run.State.Deck.Count, Is.EqualTo(deck - 1));
            Assert.That(run.State.Gold, Is.EqualTo(20));
        }

        [Test]
        public void Asylum_TreatmentOffersPurgeWhenCursed()
        {
            var run = AtEvent("asylum_ward");
            run.State.Gold = 60;
            run.State.Deck.Add(CardIds.Whispers);
            Pick(run, "Treat");
            Assert.That(run.State.Phase, Is.EqualTo(RunPhase.Event));
            Pick(run, "purge");
            Assert.That(run.State.Deck, Does.Not.Contain(CardIds.Whispers));
            Assert.That(run.State.Gold, Is.EqualTo(10));
        }

        [Test]
        public void DreamMentor_OnlyAfterFloorEight_TransformsStrike()
        {
            var run = RunEngine.NewRun(13);
            run.State.Floor = 5;
            for (int i = 0; i < 20; i++)
            {
                run.EnterNode(4, 0, RoomType.Event);
                if (run.State.Phase == RunPhase.Event) Assert.That(run.State.Event.Id, Is.Not.EqualTo("dream_mentor"));
                if (run.State.Phase == RunPhase.Battle) break;   // 事件抽光了退回战斗
                run.GoToMap();
                run.State.Floor = 5;
            }
            var mentor = AtEvent("dream_mentor");
            mentor.State.Deck[0] = "B01+";
            Pick(mentor, "Follow");
            Assert.That(mentor.State.Deck[0], Is.EqualTo("C06+"));
        }

        [Test]
        public void CultGathering_MingleEitherPaysOrStartsAHardFight()
        {
            bool sawGold = false, sawFight = false;
            for (ulong seed = 1; seed <= 20 && !(sawGold && sawFight); seed++)
            {
                var run = AtEvent("cult_gathering", seed);
                int gold = run.State.Gold;
                Pick(run, "Mingle");
                if (run.State.Phase == RunPhase.Battle)
                {
                    sawFight = true;
                    Assert.That(EncounterLibrary.Get(run.State.Battle.EncounterId).Pool, Is.EqualTo(EncounterPool.Hard));
                }
                else
                {
                    sawGold = true;
                    Assert.That(run.State.Gold, Is.EqualTo(gold + 100));
                }
            }
            Assert.That(sawGold && sawFight, Is.True);
        }

        [Test]
        public void RealData_HasEightEvents_AndRelicPoolsForEveryRarity()
        {
            Assert.That(EventLibrary.All.Count, Is.EqualTo(8));
            foreach (var r in new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop })
                Assert.That(RelicLibrary.All.Any(d => d.Rarity == r), Is.True, r.ToString());
        }
    }
}
