using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Data;
using Deeptide.Core.Enemies;
using Deeptide.Core.Map;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;
using Deeptide.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Deeptide.Tests
{
    /// <summary>
    /// Boss 三选一、不公示（策划/09 M7.3、15 §2.1）：开局用地图流抽、读档不变、连续两局不重复、海图开局揭晓、遥测记 Boss。
    /// 鳞母、倒影要到 7.4 才进表，这里用只在测试里存在的两个「Boss」（拿精英的敌人凑）代替。
    /// </summary>
    public class BossTests
    {
        static readonly string[] TestBosses = { EncounterIds.Priest, "boss_test_a", "boss_test_b" };

        static void WithThreeBosses(System.Action body)
        {
            var src = DataFiles.Read();
            src.Encounters += "boss_test_a,Boss,测试 Boss 甲,lighthouse\nboss_test_b,Boss,测试 Boss 乙,nun\n";
            try
            {
                GameData.Load(src);
                body();
            }
            finally
            {
                DataFiles.Reload();
            }
        }

        [Test]
        public void OnlyPriest_AlwaysPriest_EvenWhenAvoided()
        {
            Assert.That(RunEngine.NewRun(1).State.Boss, Is.EqualTo(EncounterIds.Priest));
            Assert.That(RunEngine.NewRun(2, new RunConfig { AvoidBoss = EncounterIds.Priest }).State.Boss, Is.EqualTo(EncounterIds.Priest), "只剩它一个时不避");
        }

        [Test]
        public void ThreeBosses_EachAboutAThird_AndSameSeedSameBoss() => WithThreeBosses(() =>
        {
            var counts = TestBosses.ToDictionary(b => b, b => 0);
            for (ulong seed = 1; seed <= 1000; seed++) counts[RunEngine.NewRun(seed).State.Boss]++;
            foreach (var kv in counts) Assert.That(kv.Value, Is.InRange(280, 390), $"{kv.Key} 出现 {kv.Value} 次");
            Assert.That(RunEngine.NewRun(77).State.Boss, Is.EqualTo(RunEngine.NewRun(77).State.Boss));
            Assert.That(RunEngine.NewRun(77).State.Map.Nodes.Select(n => n.Room), Is.EqualTo(RunEngine.NewRun(77).State.Map.Nodes.Select(n => n.Room)));
        });

        [Test]
        public void ConsecutiveNights_NeverRepeatTheBoss() => WithThreeBosses(() =>
        {
            var profile = new Profile();
            string last = null;
            var seen = new HashSet<string>();
            for (ulong night = 1; night <= 300; night++)
            {
                var run = RunEngine.NewRun(night * 31, new RunConfig { AvoidBoss = profile.LastBoss });
                Story.BeginNight(profile, run.State);
                Assert.That(run.State.Boss, Is.Not.EqualTo(last), $"第 {night} 夜");
                Assert.That(profile.LastBoss, Is.EqualTo(run.State.Boss), "开局就记进局外档");
                last = run.State.Boss;
                seen.Add(last);
            }
            Assert.That(seen, Is.EquivalentTo(TestBosses));
        });

        [Test]
        public void BossRoom_FightsTheDrawnBoss_AndSaveLoadKeepsIt() => WithThreeBosses(() =>
        {
            ulong seed = Enumerable.Range(1, 50).Select(i => (ulong)i).First(s => RunEngine.NewRun(s).State.Boss != EncounterIds.Priest);
            var run = RunEngine.NewRun(seed);
            string boss = run.State.Boss;
            var loaded = RunEngine.Load(RunSaves.FromJson(RunSaves.ToJson(run.SaveState())));
            Assert.That((loaded.State.Boss, loaded.Hash()), Is.EqualTo((boss, run.Hash())));

            loaded.EnterNode(loaded.State.Map.Height, 0, RoomType.Boss);
            Assert.That(loaded.State.Battle.EncounterId, Is.EqualTo(boss));
            Assert.That(loaded.Title, Does.Contain(EncounterLibrary.Get(boss).Name), "进门揭晓");
        });

        [Test]
        public void OldSaveWithoutBoss_ReadsAsPriest()
        {
            var run = RunEngine.NewRun(9);
            var json = JObject.Parse(RunSaves.ToJson(run.SaveState()));
            Assert.That(json.Remove("Boss"), Is.True);
            var loaded = RunEngine.Load(RunSaves.FromJson(json.ToString()));
            Assert.That(loaded.State.Boss, Is.EqualTo(EncounterIds.Priest));
            Assert.That(loaded.Hash(), Is.EqualTo(run.Hash()));
        }

        [Test]
        public void SeaChart_RevealsTheBossFromTheStart() => WithThreeBosses(() =>
        {
            var run = RunEngine.NewRun(5);
            var s = run.State;
            Assert.That(run.RevealedBoss, Is.Null, "不公示");
            s.Row = s.Map.Height - 1;
            s.Col = s.Map.InRow(s.Row).First().Col;
            var hidden = run.Options().Single(o => o.Tag == "node:Boss");
            Assert.That((hidden.Content, hidden.Label.Contains(EncounterLibrary.Get(s.Boss).Name)), Is.EqualTo(((string)null, false)));

            s.Echoes.Add(EchoIds.SeaChart);
            Assert.That(run.RevealedBoss, Is.EqualTo(s.Boss));
            var shown = run.Options().Single(o => o.Tag == "node:Boss");
            Assert.That(shown.Content, Is.EqualTo(s.Boss));
            Assert.That(shown.Label, Does.Contain(EncounterLibrary.Get(s.Boss).Name));
        });

        [Test]
        public void Story_OnlyThePriestCountsForPostscriptD_AndTelemetryHasBoss() => WithThreeBosses(() =>
        {
            foreach (var boss in TestBosses)
            {
                var state = new RunState { Boss = boss, Phase = RunPhase.Victory };
                var profile = new Profile();
                Story.Record(profile, state);
                Assert.That(profile.BeatPriest, Is.EqualTo(boss == EncounterIds.Priest), boss);
                Assert.That(RunTelemetry.From(state).Boss, Is.EqualTo(boss));
            }
        });
    }
}
