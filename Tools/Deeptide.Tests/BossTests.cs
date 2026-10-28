using System.Collections.Generic;
using System.Linq;
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
    /// 第一章的三个候选：溺亡司祭、鳞母、倒影（7.4 进表）。
    /// </summary>
    public class BossTests
    {
        static readonly string[] Bosses = { EncounterIds.Priest, "boss_scale_mother", "boss_mirror" };

        static ulong SeedWith(System.Func<string, bool> boss) =>
            Enumerable.Range(1, 100).Select(i => (ulong)i).First(s => boss(RunEngine.NewRun(s).State.Boss));

        [Test]
        public void ThreeBosses_EachAboutAThird_AndSameSeedSameBoss()
        {
            Assert.That(EncounterLibrary.InPool(EncounterPool.Boss).Select(d => d.Id), Is.EquivalentTo(Bosses));
            var counts = Bosses.ToDictionary(b => b, b => 0);
            for (ulong seed = 1; seed <= 1000; seed++) counts[RunEngine.NewRun(seed).State.Boss]++;
            foreach (var kv in counts) Assert.That(kv.Value, Is.InRange(280, 390), $"{kv.Key} 出现 {kv.Value} 次");
            Assert.That(RunEngine.NewRun(77).State.Boss, Is.EqualTo(RunEngine.NewRun(77).State.Boss));
        }

        [Test]
        public void ConsecutiveNights_NeverRepeatTheBoss()
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
            Assert.That(seen, Is.EquivalentTo(Bosses));
        }

        [Test]
        public void BossRoom_FightsTheDrawnBoss_AndSaveLoadKeepsIt()
        {
            foreach (var target in Bosses)
            {
                var run = RunEngine.NewRun(SeedWith(b => b == target));
                var loaded = RunEngine.Load(RunSaves.FromJson(RunSaves.ToJson(run.SaveState())));
                Assert.That((loaded.State.Boss, loaded.Hash()), Is.EqualTo((target, run.Hash())));

                loaded.EnterNode(loaded.State.Map.Height, 0, RoomType.Boss);
                Assert.That(loaded.State.Battle.EncounterId, Is.EqualTo(target));
                Assert.That(loaded.Title, Does.Contain(EncounterLibrary.Get(target).Name), "进门揭晓");
            }
        }

        [Test]
        public void OldSaveWithoutBoss_ReadsAsPriest()
        {
            var run = RunEngine.NewRun(SeedWith(b => b == EncounterIds.Priest));
            var json = JObject.Parse(RunSaves.ToJson(run.SaveState()));
            Assert.That(json.Remove("Boss"), Is.True);
            var loaded = RunEngine.Load(RunSaves.FromJson(json.ToString()));
            Assert.That(loaded.State.Boss, Is.EqualTo(EncounterIds.Priest));
            Assert.That(loaded.Hash(), Is.EqualTo(run.Hash()));
        }

        [Test]
        public void SeaChart_RevealsTheBossFromTheStart()
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
        }

        [Test]
        public void Story_OnlyThePriestCountsForPostscriptD_AndTelemetryHasBoss()
        {
            foreach (var boss in Bosses)
            {
                var state = new RunState { Boss = boss, Phase = RunPhase.Victory };
                var profile = new Profile();
                Story.Record(profile, state);
                Assert.That(profile.BeatPriest, Is.EqualTo(boss == EncounterIds.Priest), boss);
                Assert.That(RunTelemetry.From(state).Boss, Is.EqualTo(boss));
            }
        }
    }
}
