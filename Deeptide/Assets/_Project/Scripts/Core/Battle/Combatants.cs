using System.Collections.Generic;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
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
    }

    public sealed class PlayerState : Combatant
    {
        public int Energy;
        public int Sanity;
        public int MaxSanity;
        public int LunacyThreshold;

        /// <summary>抽牌堆，列表末尾是牌堆顶。</summary>
        public readonly List<Card> DrawPile = new List<Card>();
        public readonly List<Card> Hand = new List<Card>();
        public readonly List<Card> DiscardPile = new List<Card>();
        public readonly List<Card> ExhaustPile = new List<Card>();
        public readonly List<Relic> Relics = new List<Relic>();

        public bool IsLunatic => Sanity <= LunacyThreshold;
        public bool IsClarity => Sanity >= MaxSanity;
        public int MissingSanity => MaxSanity - Sanity;
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

        public EnemyMove Intent;
        public readonly List<string> MoveHistory = new List<string>();
        public readonly Dictionary<string, int> MoveUses = new Dictionary<string, int>();

        public int UsesOf(string moveId) => MoveUses.TryGetValue(moveId, out var n) ? n : 0;
    }
}
