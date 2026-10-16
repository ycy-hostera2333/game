using System;

namespace Deeptide.Core.Actions
{
    /// <summary>所有游戏效果的最小单位，由 ActionQueue 按顺序执行。</summary>
    public abstract class GameAction
    {
        public abstract void Execute(BattleEngine e);
    }

    /// <summary>执行时才计算的效果（如「按当前理智计算伤害」「清明：」判定）。</summary>
    public sealed class DelegateAction : GameAction
    {
        readonly Action<BattleEngine> _fn;

        public DelegateAction(Action<BattleEngine> fn) { _fn = fn; }

        public override void Execute(BattleEngine e) => _fn(e);
    }
}
