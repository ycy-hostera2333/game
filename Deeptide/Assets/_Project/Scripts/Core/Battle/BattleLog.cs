using System.Collections.Generic;

namespace Deeptide.Core
{
    public enum BattleEventType
    {
        // ---- 叙述事件：带中文 Message，表现层用来播放动画 / 飘字，控制台直接打印
        CombatStarted,
        TurnStarted,
        CardPlayed,
        CardsDrawn,
        Shuffled,
        Damage,
        BlockGained,
        HpLost,
        Healed,
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
        EnemySummoned,
        CardDiscarded,
        CardAdded,
        RelicTriggered,
        PowerApplied,
        PowerTriggered,
        PotionUsed,
        ChoiceRequested,
        ChoiceMade,
        Victory,
        Defeat,

        // ---- 状态事件：Message 为空，携带实体编号和变化后的绝对值。按顺序应用就能重建整个战斗状态（策划/09 M1.7）
        PlayerInit,
        EnemySpawned,
        HpChanged,
        BlockChanged,
        EnergyChanged,
        SanityChanged,
        StatusChanged,
        FearChanged,
        StunChanged,
        IntentChanged,
        CardMoved,
        CardCostChanged,
        PileShuffled,
        PowerChanged,
        PotionChanged,
        CounterChanged,
        /// <summary>本回合意图是否不受癫狂模糊（点亮提灯）。Value = 1 / 0。</summary>
        IntentRevealChanged,
    }

    /// <summary>
    /// 战斗事件。叙述事件给人看（Message），状态事件给表现层同步数值（实体编号 + 绝对值）。
    /// 实体编号：Entity = PlayerEntity 表示调查员，≥ 0 表示 State.Enemies 里的下标（召唤出来的敌人追加在末尾，下标不变）。
    /// </summary>
    public sealed class BattleEvent
    {
        public const int PlayerEntity = -1;
        public const int NoEntity = -2;

        public int Turn;
        public BattleEventType Type;
        /// <summary>显示名（叙述事件用）。</summary>
        public string Actor;
        public string Target;
        public int Amount;
        public string Message;

        /// <summary>受影响的实体（玩家 / 敌人下标）。</summary>
        public int Entity = NoEntity;
        /// <summary>造成这个事件的实体。</summary>
        public int Source = NoEntity;
        /// <summary>相关卡牌的 Uid（0 = 无）。</summary>
        public int CardUid;
        /// <summary>按事件类型不同：卡牌 ID（带 + 表示升级）、招式 ID、能力 ID、药水 ID、计数器名、敌人定义 ID。</summary>
        public string Id;
        public PileKind From = PileKind.None;
        public PileKind To = PileKind.None;
        public StatusId Status;
        /// <summary>变化后的绝对值（生命、格挡、能量、理智、层数……）。</summary>
        public int Value;
        /// <summary>第二个绝对值（最大生命、意志、插入位置……按事件类型约定）。</summary>
        public int Value2;
        /// <summary>按事件类型约定的开关：EnemySpawned = 召唤物；CardPlayed = 使用癫狂面。</summary>
        public bool Flag;

        public bool IsStateChange => Type >= BattleEventType.PlayerInit;

        public override string ToString() => Message ?? $"[{Type}] e={Entity} id={Id} v={Value}/{Value2} card={CardUid} {From}->{To}";
    }

    public sealed class BattleLog
    {
        public readonly List<BattleEvent> Events = new List<BattleEvent>();
        public int CurrentTurn;

        /// <summary>叙述事件。</summary>
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

        /// <summary>状态事件（没有 Message）。</summary>
        public BattleEvent State(BattleEventType type, int entity, int value, string id = null, int value2 = 0)
        {
            var ev = new BattleEvent
            {
                Turn = CurrentTurn,
                Type = type,
                Entity = entity,
                Value = value,
                Value2 = value2,
                Id = id,
            };
            Events.Add(ev);
            return ev;
        }
    }
}
