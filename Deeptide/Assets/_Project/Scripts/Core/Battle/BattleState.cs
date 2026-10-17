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
        public BattleConfig Config = new BattleConfig();
    }

    /// <summary>战斗内的独立随机流：洗牌、敌人 AI、其他（随机目标、随机弃牌、敌人生命）。</summary>
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
    }

    /// <summary>战斗统计，供记录表 / 遥测 / 模拟器使用（策划/07 §6.2）。</summary>
    public sealed class BattleStats
    {
        public int Collapses;
        /// <summary>本场因崩溃永久加入牌组的疯语数，由跑局层读取。</summary>
        public int CursesGained;
        /// <summary>首次进入癫狂的回合，0 = 从未进入。</summary>
        public int FirstLunacyTurn;
        /// <summary>玩家行动阶段开始时处于癫狂的回合数。</summary>
        public int LunacyTurns;
        /// <summary>回合结束时仍处于清明的回合数。</summary>
        public int ClarityTurns;
        public int MinSanity;
        public int CardsPlayed;
        /// <summary>按卡牌 ID 统计的打出次数（记录表「打得最多的牌」、遥测「每张牌打出次数」）。</summary>
        public readonly Dictionary<string, int> PlaysByCard = new Dictionary<string, int>();
    }

    public sealed class BattleState
    {
        public PlayerState Player;
        public readonly List<EnemyState> Enemies = new List<EnemyState>();
        public int Turn;
        public BattlePhase Phase = BattlePhase.NotStarted;
        public BattleOutcome Outcome = BattleOutcome.Ongoing;
        /// <summary>正在结算敌人回合（此时获得的能量顺延到下回合）。</summary>
        public bool EnemyTurn;
        public readonly BattleStats Stats = new BattleStats();

        public IEnumerable<EnemyState> AliveEnemies => Enemies.Where(e => !e.IsDead);
    }
}
