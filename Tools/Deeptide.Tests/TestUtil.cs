using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Enemies;
using NUnit.Framework;

namespace Deeptide.Tests
{
    /// <summary>测试工具：开一场战斗后把手牌、理智、敌人意图直接摆成想要的样子。</summary>
    static class TestUtil
    {
        public static BattleEngine Battle(string[] enemies = null, string[] deck = null, ulong seed = 1, BattleConfig config = null)
        {
            return BattleEngine.Start(new BattleSetup
            {
                Seed = seed,
                Deck = (deck ?? Enumerable.Repeat("B02", 10).ToArray()).ToList(),
                Enemies = (enemies ?? new[] { EnemyIds.Dummy }).ToList(),
                Config = config ?? new BattleConfig(),
            });
        }

        /// <summary>清空手牌，换成指定的牌（按顺序，下标从 0 开始）。</summary>
        public static void SetHand(BattleEngine e, params string[] ids)
        {
            e.Player.Hand.Clear();
            foreach (var id in ids) e.Player.Hand.Add(e.NewCard(id));
        }

        /// <summary>把抽牌堆换成指定的牌（第一个参数最先被抽到）。</summary>
        public static void SetDrawPile(BattleEngine e, params string[] ids)
        {
            e.Player.DrawPile.Clear();
            for (int i = ids.Length - 1; i >= 0; i--) e.Player.DrawPile.Add(e.NewCard(ids[i]));
        }

        public static void Play(BattleEngine e, int handIndex, int target = 0)
        {
            Assert.That(e.Execute(BattleCommand.Play(handIndex, target), out var error), Is.True, error);
        }

        public static void EndTurn(BattleEngine e)
        {
            Assert.That(e.Execute(BattleCommand.End(), out var error), Is.True, error);
        }

        public static void SetIntent(BattleEngine e, int enemyIndex, string moveId)
        {
            var en = e.State.Enemies[enemyIndex];
            en.Intent = en.Behaviour.GetMove(moveId);
        }

        public static int Count(this IEnumerable<BattleEvent> events, BattleEventType type) => events.Count(x => x.Type == type);
    }
}
