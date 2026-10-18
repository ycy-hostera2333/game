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
            foreach (var deck in TestDecks.Names)
            foreach (var enc in Encounters.Prototype.Keys)
            foreach (var seed in new ulong[] { 3, 17 })
                yield return new TestCaseData(deck, enc, seed).SetName($"Mirror_{deck}_{enc}_{seed}");
        }

        [TestCaseSource(nameof(Matchups))]
        public void MirrorMatchesKernel(string deck, string enc, ulong seed)
        {
            var e = BattleEngine.Start(new BattleSetup
            {
                Seed = seed,
                Deck = TestDecks.Get(deck),
                Enemies = Encounters.Prototype[enc].ToList(),
                Potions = new List<string> { "iron_tonic", null, "fear_powder" },
            });
            var mirror = new EventMirror();
            mirror.Consume(e.Log);
            Assert.That(mirror.Diff(e), Is.Null);

            for (int step = 0; step < 300 && e.State.Outcome == BattleOutcome.Ongoing; step++)
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
