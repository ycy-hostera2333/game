using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Content;
using Deeptide.Core.Enemies;
using NUnit.Framework;

namespace Deeptide.Tests
{
    /// <summary>BattleEngine.Fork（AI 预演用）：复制品和原战斗之后的结算完全一致，而且互不影响。</summary>
    public class ForkTests
    {
        static IEnumerable<TestCaseData> Cases()
        {
            Deeptide.Tools.DataFiles.EnsureLoaded();
            foreach (var deck in new[] { "B", "C", "D" })
            foreach (var enc in EncounterLibrary.All.Where(x => x.Pool != EncounterPool.Easy))
                yield return new TestCaseData(deck, enc.Id).SetName($"Fork_{deck}_{enc.Id}");
        }

        [TestCaseSource(nameof(Cases))]
        public void ForkBehavesExactlyLikeOriginal(string deck, string enc)
        {
            var setup = new BattleSetup
            {
                Seed = (ulong)(deck.Length * 31 + enc.Length),
                Deck = TestDecks.Get(deck).Concat(new[] { "U07", "R06", "U01", "C15" }).ToList(),
                Enemies = EncounterLibrary.Get(enc).Enemies.ToList(),
                Relics = new List<string> { "investigator_notes", "abyssal_fragment", "worn_prayer_book", "watchers_lantern", "drowned_ring" },
                Potions = new List<string> { "iron_tonic", "fear_powder" },
            };
            var e = BattleEngine.Start(setup);
            for (int step = 0; step < 400 && e.State.Outcome == BattleOutcome.Ongoing; step++)
            {
                if (e.State.Phase == BattlePhase.PlayerAction && step % 3 == 0)
                {
                    string before = StateHasher.Hash(e);
                    var fork = e.Fork();
                    Assert.That(StateHasher.Hash(fork), Is.EqualTo(before), $"第 {step} 步复制出来的状态不一致");
                    // 在复制品上往前多走几步，再和从头重放的结果比；原战斗不受影响
                    for (int k = 0; k < 6 && fork.State.Outcome == BattleOutcome.Ongoing; k++)
                        Assert.That(fork.Execute(EventStreamTests.Scripted(fork, step + k), out var ferr), Is.True, ferr);
                    Assert.That(StateHasher.Hash(e), Is.EqualTo(before), "复制品的结算影响了原战斗");
                    var replayed = BattleEngine.Replay(setup, fork.History);
                    Assert.That(StateHasher.Hash(fork), Is.EqualTo(StateHasher.Hash(replayed)), $"第 {step} 步之后的复制品和重放不一致");
                }
                Assert.That(e.Execute(EventStreamTests.Scripted(e, step), out var error), Is.True, error);
            }
        }
    }
}
