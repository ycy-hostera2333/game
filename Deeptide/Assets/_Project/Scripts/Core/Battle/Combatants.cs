using System.Collections.Generic;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using Deeptide.Core.Potions;
using Deeptide.Core.Relics;

namespace Deeptide.Core
{
    public abstract class Combatant
    {
        public string Name;
        public int Hp;
        public int MaxHp;
        public int Block;
        public readonly StatusSet Statuses = new StatusSet();

        public bool IsDead => Hp <= 0;

        /// <summary>事件里的实体编号：玩家 = BattleEvent.PlayerEntity，敌人 = 下标。</summary>
        public abstract int EntityId { get; }
    }

    public sealed class PlayerState : Combatant
    {
        public int Energy;
        public int Sanity;
        public int MaxSanity;
        public int LunacyThreshold;
        public int ClarityMargin;
        /// <summary>敌人回合中获得的能量，保留到下个玩家回合开始时加上。</summary>
        public int EnergyNextTurn;

        /// <summary>抽牌堆，列表末尾是牌堆顶。</summary>
        public readonly List<Card> DrawPile = new List<Card>();
        public readonly List<Card> Hand = new List<Card>();
        public readonly List<Card> DiscardPile = new List<Card>();
        public readonly List<Card> ExhaustPile = new List<Card>();
        public readonly List<Relic> Relics = new List<Relic>();
        /// <summary>角色特性（隐藏遗物，见 BattleSetup.Traits）。</summary>
        public readonly List<Relic> Traits = new List<Relic>();
        /// <summary>已打出的能力（同名能力叠加 Amount）。</summary>
        public readonly List<Power> Powers = new List<Power>();
        /// <summary>药水栏，null = 空位。</summary>
        public readonly List<Potion> Potions = new List<Potion>();

        public bool IsLunatic => Sanity <= LunacyThreshold;
        public bool IsClarity => Sanity >= MaxSanity - ClarityMargin;
        public int ClarityThreshold => MaxSanity - ClarityMargin;
        public int MissingSanity => MaxSanity - Sanity;

        public override int EntityId => BattleEvent.PlayerEntity;

        public Power FindPower(string id) => Powers.Find(p => p.Id == id);
        public int PowerAmount(string id) => FindPower(id)?.Amount ?? 0;
    }

    public sealed class EnemyState : Combatant
    {
        public int Index;
        public EnemyDef Def;
        public EnemyBehaviour Behaviour;

        /// <summary>无心智：免疫恐惧。</summary>
        public bool Mindless;
        public int Will;
        public int Fear;
        /// <summary>被震慑：跳过下一次行动，当前意图作废。</summary>
        public bool Stunned;
        /// <summary>召唤物：召唤者全部倒下后战斗直接胜利，不用把召唤物也打死。</summary>
        public bool Minion;

        public EnemyMove Intent;
        public readonly List<string> MoveHistory = new List<string>();
        public readonly Dictionary<string, int> MoveUses = new Dictionary<string, int>();
        /// <summary>敌人自己的计数器（Boss 的潮位、阶段，无面修女是否已现形……），进哈希。</summary>
        public readonly SortedDictionary<string, int> Counters = new SortedDictionary<string, int>(System.StringComparer.Ordinal);

        public int UsesOf(string moveId) => MoveUses.TryGetValue(moveId, out var n) ? n : 0;
        public int Counter(string key) => Counters.TryGetValue(key, out var v) ? v : 0;

        public override int EntityId => Index;
    }
}
