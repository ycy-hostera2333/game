using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Random;
using Deeptide.Core.Relics;

namespace Deeptide.Core
{
    /// <summary>开一场战斗所需的全部输入。同一份 Setup + 同一串命令 = 完全相同的结果。</summary>
    public sealed class BattleSetup
    {
        public ulong Seed;
        public List<string> Deck = new List<string>();
        public List<string> Enemies = new List<string>();
        public int MaxHp = 70;
        public int Hp = 70;
        public int MaxSanity = 10;
        public List<string> Relics = new List<string> { RelicIds.InvestigatorNotes };
        /// <summary>角色特性等隐藏遗物（relics.csv 里 rarity = Trait 的行）：和遗物一样监听，但不在遗物栏里（策划/14、09 M6.2）。</summary>
        public List<string> Traits = new List<string>();
        /// <summary>药水栏的内容，null = 空位。长度不足 PotionSlots 时补空位。</summary>
        public List<string> Potions = new List<string>();
        public int PotionSlots = 3;
        public BattleConfig Config = new BattleConfig();
    }

    /// <summary>战斗内的独立随机流：洗牌、敌人 AI、其他（随机目标、随机弃牌、敌人生命、战斗中生成的牌）。</summary>
    public sealed class BattleRng
    {
        public readonly Pcg32 Shuffle;
        public readonly Pcg32 Ai;
        public readonly Pcg32 Misc;

        public BattleRng(ulong seed)
        {
            Shuffle = new Pcg32(seed, 1);
            Ai = new Pcg32(seed, 2);
            Misc = new Pcg32(seed, 3);
        }

        BattleRng(Pcg32 shuffle, Pcg32 ai, Pcg32 misc)
        {
            Shuffle = shuffle;
            Ai = ai;
            Misc = misc;
        }

        internal BattleRng Copy() => new BattleRng(
            Pcg32.FromRaw(Shuffle.State, Shuffle.Increment), Pcg32.FromRaw(Ai.State, Ai.Increment), Pcg32.FromRaw(Misc.State, Misc.Increment));
    }

    /// <summary>战斗统计，供记录表 / 遥测 / 模拟器使用（策划/07 §6.2）。</summary>
    public sealed class BattleStats
    {
        public int Collapses;
        /// <summary>本场因崩溃永久加入牌组的疯语数，由跑局层读取。</summary>
        public int CursesGained;
        /// <summary>本场永久增加的最大生命（饱餐），跑局层直接抄战斗结束时的最大生命。</summary>
        public int MaxHpGained;
        /// <summary>算数的变身 / 复归次数（每轮第一次，策划/14 §8「每场变身 + 复归 ≥ 3」）。</summary>
        public int Transforms, Reverts;
        /// <summary>首次进入癫狂的回合，0 = 从未进入。</summary>
        public int FirstLunacyTurn;
        /// <summary>玩家行动阶段开始时处于癫狂的回合数。</summary>
        public int LunacyTurns;
        /// <summary>回合结束时仍处于清明的回合数。</summary>
        public int ClarityTurns;
        public int MinSanity;
        public int CardsPlayed;
        public int PotionsUsed;
        /// <summary>按卡牌 ID 统计的打出次数（记录表「打得最多的牌」、遥测「每张牌打出次数」）。</summary>
        public readonly Dictionary<string, int> PlaysByCard = new Dictionary<string, int>();
    }

    /// <summary>需要玩家做选择时的提示（策划/08 §3.1）。表现层弹出选项，玩家用 BattleCommand.Choose 回答。</summary>
    public sealed class PendingChoice
    {
        public string Prompt;
        public readonly List<ChoiceOption> Options = new List<ChoiceOption>();
        internal Action<BattleEngine, int> OnChosen;
    }

    public sealed class ChoiceOption
    {
        public string Label;
        /// <summary>选项对应的卡牌（选手牌 / 发现），0 = 不是牌。</summary>
        public int CardUid;
        /// <summary>选项对应的卡牌 ID（带 + 表示升级），表现层用来画卡面。</summary>
        public string CardId;
        /// <summary>非卡牌选项的含义（由发起选择的效果自己约定）。</summary>
        public int Value;
    }

    public sealed class BattleState
    {
        public PlayerState Player;
        public readonly List<EnemyState> Enemies = new List<EnemyState>();
        public int Turn;
        public BattlePhase Phase = BattlePhase.NotStarted;
        public BattleOutcome Outcome = BattleOutcome.Ongoing;
        /// <summary>玩家已经结束回合（回合结束效果和敌人回合期间）：此时获得的能量顺延到下回合。</summary>
        public bool EnemyTurn;
        /// <summary>本回合已经打出的牌数（左轮射击等「本回合第一张」判定）。</summary>
        public int CardsPlayedThisTurn;
        /// <summary>本回合已经打出的攻击牌数（含正在结算的这张；围猎）。</summary>
        public int AttacksPlayedThisTurn;
        /// <summary>本回合敌人意图不受癫狂模糊（点亮提灯）。</summary>
        public bool IntentsRevealedThisTurn;
        /// <summary>
        /// 本轮已经变身 / 复归过（每轮第一次进入 / 离开癫狂，策划/14 §1.2）。一轮 = 玩家回合开始到下一次玩家回合开始，
        /// 中间的敌人回合算这一轮；回合开始的侵蚀算新的一轮。同一轮里再跨线照常翻面，只是不再触发「变身时 / 复归时」。
        /// </summary>
        public bool TransformedThisRound;
        public bool RevertedThisRound;
        /// <summary>Phase = WaitingForChoice 时的选择内容。</summary>
        public PendingChoice Choice;
        public readonly BattleStats Stats = new BattleStats();

        public IEnumerable<EnemyState> AliveEnemies => Enemies.Where(e => !e.IsDead);
    }
}
