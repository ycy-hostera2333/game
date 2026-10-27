using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Content;
using Deeptide.Core.Enemies;
using NUnit.Framework;

namespace Deeptide.Tests
{
    /// <summary>事件流完整性（策划/09 M1.7）：只读事件流重建出的状态，每条命令之后都和内核状态一致。</summary>
    public class EventStreamTests
    {
        /// <summary>简单脚本：等待选择时选第一项；否则按 seed 轮换着从能打的牌里挑一张（攻击打第一个活着的敌人），都打不了就结束回合。</summary>
        internal static BattleCommand Scripted(BattleEngine e, int step)
        {
            if (e.State.Phase == BattlePhase.WaitingForChoice) return BattleCommand.Choose(step % e.State.Choice.Options.Count);
            var playable = Enumerable.Range(0, e.Player.Hand.Count).Where(i => e.CanPlay(i, out _)).ToList();
            if (playable.Count == 0) return BattleCommand.End();
            int idx = playable[step % playable.Count];
            var alive = e.State.Enemies.Where(x => !x.IsDead).ToList();
            int target = alive[step % alive.Count].Index;
            return BattleCommand.Play(idx, target);
        }

        static IEnumerable<TestCaseData> Matchups()
        {
            Deeptide.Tools.DataFiles.EnsureLoaded();   // 用例在测试发现阶段生成，早于 SetUpFixture
            foreach (var deck in new[] { "starter", "B", "C", "D", "WH", "WM", "WB" })
            foreach (var enc in EncounterLibrary.All)
                yield return new TestCaseData(deck, enc.Id, (ulong)(enc.Id.Length * 7 + deck.Length)).SetName($"Mirror_{deck}_{enc.Id}");
        }

        [TestCaseSource(nameof(Matchups))]
        public void MirrorMatchesKernel(string deck, string enc, ulong seed)
        {
            var setup = new BattleSetup
            {
                Seed = seed,
                Deck = TestDecks.Get(deck),
                Enemies = EncounterLibrary.Get(enc).Enemies.ToList(),
                Potions = new List<string> { "iron_tonic", null, "fear_powder" },
            };
            if (TestDecks.CharacterOf(deck) == "werewolf")
            {
                // 狼人：上限 6、兽血、狼牙吊坠、血腥诱饵——流血、变身 / 复归、饱餐都要能只靠事件流重建
                setup.MaxHp = setup.Hp = 75;
                setup.MaxSanity = 6;
                setup.Relics = new List<string> { "wolf_fang_pendant" };
                setup.Traits = new List<string> { "beast_blood" };
                setup.Potions = new List<string> { "iron_tonic", null, "bloody_bait" };
            }
            var e = BattleEngine.Start(setup);
            var mirror = new EventMirror();
            mirror.Consume(e.Log);
            Assert.That(mirror.Diff(e), Is.Null);

            for (int step = 0; step < 600 && e.State.Outcome == BattleOutcome.Ongoing; step++)
            {
                var cmd = step == 4 && e.State.Phase == BattlePhase.PlayerAction ? BattleCommand.Potion(0) : Scripted(e, step);
                Assert.That(e.Execute(cmd, out var error), Is.True, $"{cmd}：{error}");
                mirror.Consume(e.Log);
                var diff = mirror.Diff(e);
                Assert.That(diff, Is.Null, $"第 {step} 条命令 {cmd} 之后：\n{diff}");
            }
        }
    }
}
