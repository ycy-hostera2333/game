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
