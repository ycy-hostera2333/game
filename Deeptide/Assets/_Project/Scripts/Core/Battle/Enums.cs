using System;

namespace Deeptide.Core
{
    public enum CardType { Attack, Skill, Power, Status, Curse }

    public enum Rarity { Basic, Common, Uncommon, Rare, Special }

    public enum CardTarget { None, Enemy }

    /// <summary>理智损失的来源。心防只抵消 Enemy 来源（策划/01 §2.3）。</summary>
    public enum SanitySource { Card, Enemy, Erosion, Curse, Effect }

    public enum StatusId { Strength, Dexterity, Vulnerable, Weak, Frail, Bound, Ward }

    public enum PileKind { Draw, Hand, Discard }

    public enum BattlePhase { NotStarted, PlayerAction, Resolving, Ended }

    public enum BattleOutcome { Ongoing, Victory, Defeat }

    [Flags]
    public enum IntentType
    {
        None = 0,
        Attack = 1,
        Defend = 2,
        Buff = 4,
        Debuff = 8,
        Mind = 16,
        Summon = 32,
        Unknown = 64,
    }
}
