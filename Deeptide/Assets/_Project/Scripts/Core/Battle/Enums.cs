using System;

namespace Deeptide.Core
{
    public enum CardType { Attack, Skill, Power, Status, Curse }

    public enum Rarity { Basic, Common, Uncommon, Rare, Special }

    public enum CardTarget { None, Enemy }

    /// <summary>
    /// 理智损失的来源。心防抵消 Enemy（精神攻击）和 Aura（精英 / Boss 的被动光环）两种敌方来源（策划/01 §2.3）；
    /// 祝圣银铃只改写 Enemy（精神攻击）。
    /// </summary>
    public enum SanitySource { Card, Enemy, Erosion, Curse, Effect, Aura }

    /// <summary>新状态加在末尾：顺序决定显示和哈希的排列。Bleed = 流血（敌人，策划/14 §1.4）。</summary>
    public enum StatusId { Strength, Dexterity, Vulnerable, Weak, Frail, Bound, Ward, Bleed }

    /// <summary>牌所在的位置。Play = 正在结算的牌；None = 不在任何牌堆（刚生成，或能力牌打出后移出战斗）。</summary>
    public enum PileKind { Draw, Hand, Discard, Exhaust, Play, None }

    public enum BattlePhase { NotStarted, PlayerAction, Resolving, WaitingForChoice, Ended }

    public enum BattleOutcome { Ongoing, Victory, Defeat }

    /// <summary>攻击伤害走完整公式（力量 / 虚弱 / 易伤）；效果伤害（圣痕、疯狂本能、火药瓶）只吃遗物修正和格挡。</summary>
    public enum DamageKind { Attack, Effect }

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
