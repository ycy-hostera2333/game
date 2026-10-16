using System.Collections.Generic;

namespace Deeptide.Core
{
    public enum BattleEventType
    {
        CombatStarted,
        TurnStarted,
        CardPlayed,
        CardsDrawn,
        Shuffled,
        Damage,
        BlockGained,
        HpLost,
        EnergyGained,
        SanityLost,
        SanityRestored,
        WardConsumed,
        LunacyEntered,
        LunacyLeft,
        Collapse,
        StatusApplied,
        FearApplied,
        EnemyStunned,
        EnemyStunSkipped,
        EnemyMove,
        IntentChosen,
        EnemyDied,
        CardDiscarded,
        CardAdded,
        RelicTriggered,
        Victory,
        Defeat,
    }

    /// <summary>结构化事件：表现层按顺序消费它来播放动画；Message 是给控制台和日志看的中文描述。</summary>
    public sealed class BattleEvent
    {
        public int Turn;
        public BattleEventType Type;
        public string Actor;
        public string Target;
        public int Amount;
        public string Message;

        public override string ToString() => Message;
    }

    public sealed class BattleLog
    {
        public readonly List<BattleEvent> Events = new List<BattleEvent>();
        public int CurrentTurn;

        public BattleEvent Add(BattleEventType type, string message, string actor = null, string target = null, int amount = 0)
        {
            var ev = new BattleEvent
            {
                Turn = CurrentTurn,
                Type = type,
                Actor = actor,
                Target = target,
                Amount = amount,
                Message = message,
            };
            Events.Add(ev);
            return ev;
        }
    }
}
