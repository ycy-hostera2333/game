using System.Linq;
using Deeptide.Core.Data;
using Deeptide.Core.Run;
using Deeptide.Tools;
using NUnit.Framework;

namespace Deeptide.Tests
{
    /// <summary>循环叙事（策划/12 §8–§9）：局外档的夜数、附言解锁顺序、事件的循环变体。</summary>
    public class StoryTests
    {
        /// <summary>开一夜新的，返回这一夜新出现的附言 ID（没有为 null）。</summary>
        static string Night(Profile p, RunPhase end = RunPhase.Map, params string[] events)
        {
            var run = RunEngine.NewRun((ulong)p.Nights + 1);
            var ps = Story.BeginNight(p, run.State);
            Assert.That(run.State.Stats.Night, Is.EqualTo(p.Nights));
            run.State.EventsSeen.AddRange(events);
            run.State.Phase = end;
            if (end == RunPhase.Victory) run.State.Boss = Deeptide.Core.Enemies.EncounterIds.Priest;   // 附言 D 只认打倒司祭；这里测的是附言顺序
            Story.Record(p, run.State);
            return ps?.Id;
        }

        [Test]
        public void Postscripts_AppearInOrder_OnePerNight()
        {
            var p = new Profile();
            Assert.That(Night(p, RunPhase.Defeat), Is.Null, "第 1 夜信上没有附言");
            Assert.That(Night(p, RunPhase.Defeat), Is.EqualTo("A"));
            Assert.That(Night(p, RunPhase.Defeat, "dream_mentor"), Is.EqualTo("B"), "死过一次：第 3 夜出现 B");
            Assert.That(Night(p, RunPhase.Victory), Is.EqualTo("C"), "见过梦中的导师：下一夜出现 C");
            Assert.That(Night(p), Is.EqualTo("D"), "打倒过司祭：下一夜出现 D");
            Assert.That(Night(p), Is.Null, "附言出完了");
            Assert.That(Story.Shown(p).Select(ps => ps.Id), Is.EqualTo(new[] { "A", "B", "C", "D" }));
        }

        [Test]
        public void Postscripts_FirstNightWinner_DoesNotSkipAhead()
        {
            var p = new Profile();
            Assert.That(Night(p, RunPhase.Victory), Is.Null);
            Assert.That(p.BeatPriest, Is.True);
            Assert.That(Night(p, RunPhase.Victory), Is.EqualTo("A"), "通关过也要从 A 开始");
            Assert.That(Night(p, RunPhase.Victory), Is.Null, "没死过、不到第 4 夜：B 还不出现");
            Assert.That(Night(p, RunPhase.Victory), Is.EqualTo("B"), "第 4 夜");
            Assert.That(Night(p), Is.Null);
            Assert.That(Night(p), Is.EqualTo("C"), "没见过导师：第 6 夜");
            Assert.That(Night(p), Is.EqualTo("D"));
        }

        [Test]
        public void Record_ReportsChangesOnlyOnce()
        {
            var p = new Profile();
            var run = RunEngine.NewRun(3);
            Story.BeginNight(p, run.State);
            Assert.That(Story.Record(p, run.State), Is.False, "开局什么都没发生");
            run.State.EventsSeen.Add("mirror");
            Assert.That(Story.Record(p, run.State), Is.True);
            Assert.That(Story.Record(p, run.State), Is.False);
            run.State.Phase = RunPhase.Defeat;
            Assert.That(Story.Record(p, run.State), Is.True);
            Assert.That(p.Died, Is.True);
            Assert.That(p.EventsSeen, Is.EqualTo(new[] { "mirror" }));

            // 下一夜：前几夜见过的事件抄进这一局，这一局里新见的不影响
            var next = RunEngine.NewRun(4);
            Story.BeginNight(p, next.State);
            Assert.That(next.State.Stats.EventsBefore, Is.EqualTo(new[] { "mirror" }));
            Assert.That(next.State.Stats.Night, Is.EqualTo(2));
        }

        static string DescriptionAt(string id, int night, bool seenBefore = false)
        {
            var run = RunEngine.NewRun(12);
            run.State.Stats.Night = night;
            if (seenBefore) run.State.Stats.EventsBefore.Add(id);
            run.State.Event = new EventState { Id = id };
            run.State.Phase = RunPhase.Event;
            return run.Description;
        }

        [Test]
        public void EventVariants_ReplaceTextFromTheirNight()
        {
            var book = EventLibrary.Get("forbidden_book");
            Assert.That(DescriptionAt("forbidden_book", 0), Is.EqualTo(book.Text), "没有局外档（控制台）一律用原文");
            Assert.That(DescriptionAt("forbidden_book", 2), Is.EqualTo(book.Text));
            Assert.That(DescriptionAt("forbidden_book", 3), Is.EqualTo(book.LoopText));
            Assert.That(DescriptionAt("fog_peddler", 2), Is.EqualTo(EventLibrary.Get("fog_peddler").LoopText));

            var mentor = EventLibrary.Get("dream_mentor");
            Assert.That(mentor.LoopAfterSeen, Is.True);
            Assert.That(DescriptionAt("dream_mentor", 9), Is.EqualTo(mentor.Text), "没见过导师：再晚也是原文");
            Assert.That(DescriptionAt("dream_mentor", 2, seenBefore: true), Is.EqualTo(mentor.LoopText));

            foreach (var def in EventLibrary.All)
                Assert.That(def.LoopText, Is.Not.Empty, $"{def.Id} 缺少循环变体（12 §7.2）");
        }

        [Test]
        public void Validation_LoopTextNeedsLoopFrom()
        {
            var src = DataFiles.Read();
            src.Events = src.Events.Replace(",seen,", ",,");
            var errors = GameData.Validate(src);
            Assert.That(errors.Any(e => e.Contains("loop_text 和 loop_from")), Is.True, string.Join("\n", errors));
        }

        [Test]
        public void Telemetry_RecordsNight()
        {
            var p = new Profile { Nights = 4 };
            var run = RunEngine.NewRun(8);
            Story.BeginNight(p, run.State);
            Assert.That(RunTelemetry.From(run.State).Night, Is.EqualTo(5));
        }
    }
}
