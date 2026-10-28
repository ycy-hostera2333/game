using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Actions;
using Deeptide.Core.Data;
using Deeptide.Core.Enemies;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;
using Deeptide.Tools;
using NUnit.Framework;
using static Deeptide.Tests.TestUtil;

namespace Deeptide.Tests
{
    /// <summary>回响内核（策划/09 M7.1、15 §3）：echoes.csv 的校验、规则参数覆盖、开局 3 选 1 和重抽、存读档。</summary>
    public class EchoTests
    {
        static BattleEngine WithEchoes(params string[] echoes) => BattleEngine.Start(new BattleSetup
        {
            Seed = 1,
            Deck = Enumerable.Repeat("B02", 10).ToList(),
            Enemies = new List<string> { EnemyIds.Dummy },
            Relics = new List<string>(),
            Echoes = echoes.ToList(),
        });

        // ---------------- 数据

        [Test]
        public void Table_FiltersByCharacter_AndTendencyFallsBackToNeutral()
        {
            Assert.That(CharacterLibrary.Get("investigator").Archetypes, Is.EquivalentTo(new[] { "守夜", "堕落", "传染" }));
            Assert.That(CharacterLibrary.Get("werewolf").Archetypes, Is.EquivalentTo(new[] { "狂猎", "月相", "血宴" }));
            var sleepless = EchoLibrary.Get(EchoIds.Sleepless);
            Assert.That(EchoLibrary.TendencyFor(sleepless, "investigator"), Is.EqualTo("堕落"));
            Assert.That(EchoLibrary.TendencyFor(sleepless, "werewolf"), Is.EqualTo(EchoDef.Neutral), "不眠对狼人算中立（15 §3.3）");
            Assert.That(EchoLibrary.Pool(EchoTier.Shallow, "werewolf").Select(d => d.Id), Does.Not.Contain(EchoIds.Wick).And.Not.Contain(EchoIds.Aftershock));
        }

        [Test]
        public void Validation_ReportsBadTierCharacterAndTendency()
        {
            var src = DataFiles.Read();
            src.Echoes = src.Echoes.Replace("wick,灯芯,Shallow,", "wick,灯芯,Shallo,")
                                   .Replace("shoal,浅滩,Shallow,investigator,", "shoal,浅滩,Shallow,nobody,")
                                   .Replace("aftershock,余悸,Shallow,investigator,传染,", "aftershock,余悸,Shallow,investigator,狂猎,")
                                   .Replace("sleepless,不眠,Shallow,,堕落,", "sleepless,不眠,Shallow,,随便,");
            var errors = string.Join("\n", GameData.Validate(src));
            Assert.That(errors, Does.Contain("tier 不能是「Shallo」"));
            Assert.That(errors, Does.Contain("不存在的角色 nobody"));
            Assert.That(errors, Does.Contain("aftershock 的倾向「狂猎」不对"), "专属回响只能用本角色的流派");
            Assert.That(errors, Does.Contain("sleepless 的倾向「随便」不对"));
        }

        // ---------------- 规则参数

        [Test]
        public void Wick_And_Shoal_MoveTheLines_WithoutTouchingSetupConfig()
        {
            var e = WithEchoes(EchoIds.Wick, EchoIds.Shoal);
            Assert.That(e.Player.ClarityThreshold, Is.EqualTo(8));
            Assert.That(e.Player.LunacyThreshold, Is.EqualTo(5));
            Assert.That(e.Setup.Config.ClarityMargin, Is.EqualTo(1), "传进来的配置不被改，几场战斗可以共用");
            var f = e.Fork();
            Assert.That((f.Player.ClarityThreshold, f.Player.LunacyThreshold, f.Config.LunacyThreshold), Is.EqualTo((8, 5, 5)));
        }

        [Test]
        public void Sleepless_StartsBelowMax_AndEventStreamStillRebuilds()
        {
            var e = WithEchoes(EchoIds.Sleepless);
            Assert.That(e.Player.Sanity, Is.EqualTo(7));
            Assert.That(e.State.Stats.MinSanity, Is.EqualTo(7));
            var mirror = new EventMirror();
            mirror.Consume(e.Log);
            Assert.That(mirror.Diff(e), Is.Null);

            var wolf = BattleEngine.Start(new BattleSetup
            {
                Seed = 1, Deck = Enumerable.Repeat("B01", 10).ToList(), Enemies = new List<string> { EnemyIds.Dummy },
                MaxHp = 75, Hp = 75, MaxSanity = 6, Relics = new List<string>(),
                Traits = new List<string> { RelicIds.BeastBlood }, Echoes = new List<string> { EchoIds.Sleepless },
            });
            Assert.That(wolf.Player.Sanity, Is.EqualTo(3));
            Assert.That(wolf.Player.IsLunatic, Is.True, "狼人起手就是兽形");
            Assert.That(wolf.State.Stats.Transforms, Is.EqualTo(0), "开战就在兽形不算变身");
        }

        [Test]
        public void Aftershock_KeepsHalfFear_CappedBelowNewWill()
        {
            foreach (var (echo, expected) in new[] { (false, 0), (true, 5) })
            {
                var e = echo ? WithEchoes(EchoIds.Aftershock) : WithEchoes();
                SetHand(e, "C11", "C11");
                Play(e, 0);
                Play(e, 0);
                var en = e.State.Enemies[0];
                Assert.That((en.Stunned, en.Will, en.Fear), Is.EqualTo((true, 15, expected)), echo ? "余悸" : "默认清零");
            }
            var big = WithEchoes(EchoIds.Aftershock);
            var dummy = big.State.Enemies[0];
            big.SetFear(dummy, 40, 10);
            ApplyFearAction.Stun(big, dummy);
            Assert.That(dummy.Fear, Is.EqualTo(14), "一半是 20，最多留到新意志 15 − 1");
        }

        // ---------------- 浅档的其余 8 个（09 M7.2）

        static BattleEngine Inv(string[] echoes, string[] enemies = null, string[] relics = null) => BattleEngine.Start(new BattleSetup
        {
            Seed = 1,
            Deck = Enumerable.Repeat("B02", 10).ToList(),
            Enemies = (enemies ?? new[] { EnemyIds.Dummy }).ToList(),
            Relics = (relics ?? new[] { RelicIds.InvestigatorNotes }).ToList(),
            Echoes = echoes.ToList(),
        });

        static BattleEngine Wolf(params string[] echoes) => BattleEngine.Start(new BattleSetup
        {
            Seed = 1,
            Deck = Enumerable.Repeat("B02", 10).ToList(),
            Enemies = new List<string> { EnemyIds.Dummy },
            MaxHp = 75, Hp = 75, MaxSanity = 6,
            Relics = new List<string>(),
            Traits = new List<string> { RelicIds.BeastBlood },
            Echoes = echoes.ToList(),
        });

        static void EndTurns(BattleEngine e, int n) { for (int i = 0; i < n; i++) EndTurn(e); }

        [Test]
        public void Baffle_StartsWithWard_AndWardStopsErosion()
        {
            // 第 4 回合开始侵蚀：挡板的心防挡住，旧烟斗的心防挡不住（默认心防只挡敌人）
            var baffle = Inv(new[] { EchoIds.Baffle }, relics: new string[0]);
            Assert.That(baffle.Player.Statuses.Get(StatusId.Ward), Is.EqualTo(1));
            EndTurns(baffle, 3);
            Assert.That((baffle.State.Turn, baffle.Player.Sanity, baffle.Player.Statuses.Get(StatusId.Ward)), Is.EqualTo((4, 10, 0)));

            var pipe = Inv(new string[0], relics: new[] { "old_pipe" });
            EndTurns(pipe, 3);
            Assert.That((pipe.Player.Sanity, pipe.Player.Statuses.Get(StatusId.Ward)), Is.EqualTo((9, 1)));
        }

        [Test]
        public void Spread_SplitsFearOfTheDeadAmongTheRest()
        {
            var e = Inv(new[] { EchoIds.Spread }, new[] { EnemyIds.Dummy, EnemyIds.Dummy, EnemyIds.Dummy });
            SetHand(e, "C11", "B01");
            Play(e, 0, 0);   // 恐惧 5
            e.State.Enemies[0].Hp = 1;
            Play(e, 0, 0);   // 打死
            Assert.That(e.State.Enemies.Skip(1).Select(x => x.Fear), Is.EqualTo(new[] { 3, 2 }), "5 分给两个：各 2，余 1 给前一个");

            var plain = Inv(new string[0], new[] { EnemyIds.Dummy, EnemyIds.Dummy });
            SetHand(plain, "C11", "B01");
            Play(plain, 0, 0);
            plain.State.Enemies[0].Hp = 1;
            Play(plain, 0, 0);
            Assert.That(plain.State.Enemies[1].Fear, Is.EqualTo(0));
        }

        [Test]
        public void NotesAddendum_FiresNotesAgainOnFirstRevert()
        {
            (int Energy, int Hand) Run(params string[] echoes)
            {
                var e = Inv(echoes);
                e.SetSanity(5);
                SetHand(e, "C08", "C04", "C08", "C04");
                Play(e, 0);   // 5 → 3：进入癫狂，调查笔记触发
                Play(e, 0);   // 3 → 5：回到清醒，笔记补页
                Play(e, 0);
                Play(e, 0);   // 第二次回到清醒：不再触发
                return (e.Player.Energy, e.Player.Hand.Count);
            }
            var with = Run(EchoIds.NotesAddendum);
            var without = Run();
            Assert.That((with.Energy - without.Energy, with.Hand - without.Hand), Is.EqualTo((1, 2)), "多 1 能量、多抽 2 张，只一次");

            var noNotes = Inv(new[] { EchoIds.NotesAddendum }, relics: new string[0]);
            noNotes.SetSanity(5);
            SetHand(noNotes, "C08", "C04");
            int energy = noNotes.Player.Energy;
            Play(noNotes, 0);
            Play(noNotes, 0);
            Assert.That(noNotes.Player.Energy, Is.EqualTo(energy + 1), "没有调查笔记：只有坠落自己的 +1");
        }

        [Test]
        public void DeepScar_BeastFormAddsThreePerHit()
        {
            var e = Wolf(EchoIds.DeepScar);
            SetHand(e, "B01", "B01");
            var target = e.State.Enemies[0];
            Assert.That(e.Player.Hand[0].PreviewValue(e, "Damage", target), Is.EqualTo(6), "人形不加");
            e.SetSanity(4);
            Assert.That(e.Player.Hand[0].PreviewValue(e, "Damage", target), Is.EqualTo(9));
            int hp = target.Hp;
            Play(e, 0);
            Assert.That(hp - target.Hp, Is.EqualTo(9), "预览和实际一致");
        }

        [Test]
        public void TidalRange_CountsTwoCrossingsEachWayPerRound()
        {
            foreach (var (echoes, counted) in new[] { (new[] { EchoIds.TidalRange }, 2), (new string[0], 1) })
            {
                var e = Wolf(echoes);
                SetHand(e, "C08", "C04", "C08", "C04", "C08", "C04");
                for (int i = 0; i < 6; i++) Play(e, 0);
                Assert.That((e.State.Stats.Transforms, e.State.Stats.Reverts), Is.EqualTo((counted, counted)));
                EndTurn(e);
                SetHand(e, "C08");
                Play(e, 0);
                Assert.That(e.State.Stats.Transforms, Is.EqualTo(counted + 1), "新的一轮重新计数");
            }
        }

        [Test]
        public void BloodToSea_BleedDecaysEveryOtherTurn()
        {
            foreach (var (echoes, after) in new[] { (new[] { EchoIds.BloodToSea }, new[] { 4, 3, 3, 2 }), (new string[0], new[] { 3, 2, 1, 0 }) })
            {
                var e = Wolf(echoes);
                var dummy = e.State.Enemies[0];
                e.SetStatus(dummy, StatusId.Bleed, 4);
                var seen = new List<int>();
                for (int i = 0; i < 4; i++)
                {
                    EndTurn(e);
                    seen.Add(dummy.Statuses.Get(StatusId.Bleed));
                }
                Assert.That(seen, Is.EqualTo(after));
            }
        }

        [Test]
        public void WerewolfPool_ExcludesTheFiveInvestigatorOnly()
        {
            var excluded = new[] { EchoIds.Wick, EchoIds.Shoal, EchoIds.Aftershock, EchoIds.Spread, EchoIds.NotesAddendum };
            var pool = EchoLibrary.Pool(EchoTier.Shallow, "werewolf").Select(d => d.Id).ToList();
            Assert.That(pool, Has.Count.EqualTo(7), "3 专属 + 4 通用（15 §3.3）");
            Assert.That(pool.Intersect(excluded), Is.Empty);
            Assert.That(EchoLibrary.Pool(EchoTier.Shallow, "investigator").Count(), Is.EqualTo(9));
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var run = RunEngine.NewRun(seed, Echoes("werewolf"));
                Assert.That(run.State.Select.Echoes.Intersect(excluded), Is.Empty, $"种子 {seed}");
                Assert.That(run.State.Select.Echoes.Select(id => EchoLibrary.TendencyFor(EchoLibrary.Get(id), "werewolf")).Distinct().Count(), Is.EqualTo(3));
            }
        }

        /// <summary>开一局，选上指定的回响（第一组没有就重抽一次；两组都没有换下一个种子）。</summary>
        static RunEngine RunWith(string echo, string character = "investigator", ulong from = 1)
        {
            for (ulong seed = from; seed < from + 200; seed++)
            {
                var run = RunEngine.NewRun(seed, Echoes(character));
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    int i = run.Options().FindIndex(o => o.Tag == "echo:" + echo);
                    if (i >= 0)
                    {
                        Assert.That(run.Execute(RunCommand.Pick(i), out var error), Is.True, error);
                        return run;
                    }
                    if (attempt == 0) run.Execute(RunCommand.Pick(run.Options().FindIndex(o => o.Tag == "reroll")), out _);
                }
            }
            Assert.Fail($"200 个种子里都没抽到 {echo}");
            return null;
        }

        [Test]
        public void Satchel_AddsSlotAndPotion()
        {
            var run = RunWith(EchoIds.Satchel);
            Assert.That(run.State.Potions, Has.Count.EqualTo(4));
            Assert.That(run.State.Potions.Count(p => p != null), Is.EqualTo(1));
            Assert.That(RunEngine.Replay(run.State.Seed, run.History, Echoes()).Hash(), Is.EqualTo(run.Hash()));
        }

        [Test]
        public void SeaChart_PreviewMatchesWhatTheNodeTurnsOutToBe()
        {
            int checkedNodes = 0;
            for (ulong from = 1; from <= 60; from += 3)
            {
                var run = RunWith(EchoIds.SeaChart, from: from);
                var plain = RunEngine.NewRun(run.State.Seed);
                var nodes = run.State.Map.Nodes.Where(n => n.Room == Deeptide.Core.Map.RoomType.Event || n.Room == Deeptide.Core.Map.RoomType.Elite).Take(2);
                foreach (var node in nodes)
                {
                    Assert.That(plain.PreviewNode(node, out _), Is.Null, "没有海图看不到");
                    var probe = RunEngine.Replay(run.State.Seed, run.History, Echoes());
                    string preview = probe.PreviewNode(node, out string name);
                    Assert.That(name, Is.Not.Null.And.Not.Empty);
                    probe.EnterNode(node.Row, node.Col, node.Room);
                    string actual = probe.State.Event?.Id ?? probe.State.Battle.EncounterId;
                    Assert.That(actual, Is.EqualTo(preview), $"种子 {run.State.Seed} 第 {node.Floor} 层 {node.Room}");
                    checkedNodes++;
                }
            }
            Assert.That(checkedNodes, Is.GreaterThan(20));

            var first = RunWith(EchoIds.SeaChart);
            Assert.That(first.Options().All(o => o.Content == null), Is.True, "第 1 层全是普通战斗，不显示");
        }

        // ---------------- 跑局

        static RunConfig Echoes(string character = "investigator") => new RunConfig { Character = character, Echoes = true };

        [Test]
        public void Off_ByDefault()
        {
            var run = RunEngine.NewRun(3);
            Assert.That((run.State.Phase, run.State.Echoes.Count), Is.EqualTo((RunPhase.Map, 0)));
        }

        [TestCase("investigator")]
        [TestCase("werewolf")]
        public void Offer_DistinctTendencies_NoSkip_RerollOnce(string character)
        {
            var pool = EchoLibrary.Pool(EchoTier.Shallow, character).ToList();
            int expected = System.Math.Min(RunEngine.EchoChoices, pool.Count);
            int kinds = System.Math.Min(expected, pool.Select(d => EchoLibrary.TendencyFor(d, character)).Distinct().Count());
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var run = RunEngine.NewRun(seed, Echoes(character));
                var sel = run.State.Select;
                Assert.That((run.State.Phase, sel.Purpose), Is.EqualTo((RunPhase.Select, SelectPurpose.Echo)));
                Assert.That(sel.Echoes.Count, Is.EqualTo(expected));
                Assert.That(sel.Echoes.Select(id => EchoLibrary.TendencyFor(EchoLibrary.Get(id), character)).Distinct().Count(), Is.EqualTo(kinds), $"种子 {seed}");
                Assert.That(run.Options().Any(o => o.Tag == "skip"), Is.False, "回响不能跳过");

                var before = sel.Echoes.ToList();
                int reroll = run.Options().FindIndex(o => o.Tag == "reroll");
                Assert.That(run.Execute(RunCommand.Pick(reroll), out var error), Is.True, error);
                Assert.That(run.State.EchoRerolls, Is.EqualTo(1));
                if (pool.Any(d => !before.Contains(d.Id)))
                    Assert.That(run.State.Select.Echoes.Any(id => !before.Contains(id)), Is.True, $"种子 {seed}：重抽至少换上一个没给过的");
                Assert.That(run.Execute(RunCommand.Pick(run.Options().FindIndex(o => o.Tag == "reroll")), out _), Is.False, "每局只能重抽一次");
            }
        }

        [Test]
        public void Pick_GoesToMap_ArmsBattles_ReplayAndSaveLoadMatch()
        {
            var run = RunEngine.NewRun(11, Echoes());
            var json0 = RunSaves.ToJson(run.SaveState());
            int pick = run.Options().FindIndex(o => o.Tag == "echo:" + EchoIds.Sleepless);
            if (pick < 0)
            {
                Assert.That(run.Execute(RunCommand.Pick(run.Options().FindIndex(o => o.Tag == "reroll")), out _), Is.True);
                pick = run.Options().FindIndex(o => o.Tag == "echo:" + EchoIds.Sleepless);
            }
            Assume.That(pick, Is.GreaterThanOrEqualTo(0), "种子 11 两组里都没抽到不眠");
            Assert.That(run.Execute(RunCommand.Pick(pick), out var err), Is.True, err);
            Assert.That((run.State.Phase, run.State.Echoes.Single()), Is.EqualTo((RunPhase.Map, EchoIds.Sleepless)));
            Assert.That(run.State.Stats.EchoChoices.Single().Picked, Is.EqualTo(EchoIds.Sleepless));

            Assert.That(run.Execute(RunCommand.Pick(0), out err), Is.True, err);   // 第 1 层固定是普通战斗
            Assert.That(run.Battle.Player.Sanity, Is.EqualTo(7));
            for (int step = 0; step < 400 && run.State.Phase == RunPhase.Battle; step++)
                Assert.That(run.Execute(RunCommand.Fight(EventStreamTests.Scripted(run.Battle, step)), out err), Is.True, err);

            var replayed = RunEngine.Replay(11, run.History, Echoes());
            Assert.That(replayed.Hash(), Is.EqualTo(run.Hash()));
            var loaded = RunEngine.Load(RunSaves.FromJson(json0), Echoes());
            foreach (var cmd in run.History) Assert.That(loaded.Execute(cmd, out err), Is.True, err);
            Assert.That(loaded.Hash(), Is.EqualTo(run.Hash()), "在回响选择界面存档、读档后打完，和不中断一致");
        }

        [Test]
        public void Version1Save_ReadsAsChosenNothing()
        {
            var run = RunEngine.NewRun(9);
            var state = RunSaves.FromJson(RunSaves.ToJson(run.SaveState()));
            state.Version = 1;
            state.Rng = state.Rng.Take(12).ToArray();
            state.Echoes = new List<string>();
            var loaded = RunEngine.Load(state);
            Assert.That((loaded.State.Version, loaded.State.Phase, loaded.State.Echoes.Count), Is.EqualTo((RunEngine.SaveVersion, RunPhase.Map, 0)));
            Assert.That(loaded.Hash(), Is.EqualTo(run.Hash()), "补上的回响流和新开局的一样");
        }
    }
}
