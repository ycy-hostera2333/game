using System.Collections.Generic;
using Deeptide.Core.Map;
using Deeptide.Core.Random;

namespace Deeptide.Core.Run
{
    /// <summary>跑局所处的界面。每个界面都把可做的事列成选项，玩家用 RunCommand.Pick 选。</summary>
    public enum RunPhase
    {
        /// <summary>在地图上选下一个节点。</summary>
        Map,
        Battle,
        /// <summary>战斗 / 宝箱的奖励列表。</summary>
        Reward,
        Shop,
        Campfire,
        Event,
        /// <summary>通用的二级选择：从几张牌里选一张、从牌组里选一张删 / 升级、药水栏满了丢哪瓶……</summary>
        Select,
        Victory,
        Defeat,
    }

    /// <summary>
    /// 跑局的 7 个独立随机流（策划/08 §5）：地图、遭遇、奖励、商店、事件、其他，加上「战斗种子」（每场战斗用 局种子 + 战斗序号 派生，
    /// 战斗内部再分洗牌 / AI / 杂项三个流）。多看一次奖励不会改变后面的地图或敌人行为。
    /// </summary>
    public sealed class RunRng
    {
        public Pcg32 Map, Encounter, Reward, Shop, Event, Misc;

        public RunRng(ulong seed)
        {
            Map = new Pcg32(seed, 11);
            Encounter = new Pcg32(seed, 12);
            Reward = new Pcg32(seed, 13);
            Shop = new Pcg32(seed, 14);
            Event = new Pcg32(seed, 15);
            Misc = new Pcg32(seed, 16);
        }

        RunRng() { }

        public ulong[] Save() => new[]
        {
            Map.State, Map.Increment, Encounter.State, Encounter.Increment, Reward.State, Reward.Increment,
            Shop.State, Shop.Increment, Event.State, Event.Increment, Misc.State, Misc.Increment,
        };

        public static RunRng Load(ulong[] s) => new RunRng
        {
            Map = Pcg32.FromRaw(s[0], s[1]),
            Encounter = Pcg32.FromRaw(s[2], s[3]),
            Reward = Pcg32.FromRaw(s[4], s[5]),
            Shop = Pcg32.FromRaw(s[6], s[7]),
            Event = Pcg32.FromRaw(s[8], s[9]),
            Misc = Pcg32.FromRaw(s[10], s[11]),
        };
    }

    /// <summary>当前这场战斗的输入（不含战斗中的进度：读档时战斗从头开始，策划/06 §8）。</summary>
    public sealed class BattleInfo
    {
        public string EncounterId;
        public ulong Seed;
        public RoomType Room;
    }

    public sealed class RewardItem
    {
        public RewardKind Kind;
        /// <summary>金币数量，或卡牌选项的数量。</summary>
        public int Amount;
        /// <summary>遗物 / 药水的 ID。</summary>
        public string Id;
        /// <summary>卡牌奖励的候选（带 + 表示升级）。</summary>
        public List<string> Cards = new List<string>();
        public bool Taken;
    }

    public enum RewardKind { Gold, Cards, Relic, Potion }

    public sealed class ShopItem
    {
        public ShopItemKind Kind;
        public string Id;
        public int Price;
        public bool Sold;
    }

    public enum ShopItemKind { Card, Relic, Potion }

    public sealed class ShopState
    {
        public List<ShopItem> Items = new List<ShopItem>();
        public bool RemovalUsed;
    }

    public sealed class EventState
    {
        public string Id;
        /// <summary>事件内部的阶段，由事件类自己约定（0 = 初始选项）。</summary>
        public int Stage;
    }

    /// <summary>二级选择要做什么。</summary>
    public enum SelectPurpose
    {
        /// <summary>从 Cards 里选一张加入牌组（可以跳过）。</summary>
        AddCard,
        /// <summary>从牌组里选一张删掉。</summary>
        RemoveCard,
        /// <summary>从牌组里选一张升级。</summary>
        UpgradeCard,
        /// <summary>药水栏满了：选一瓶丢掉换成 PendingPotion，或者放弃新药水。</summary>
        DiscardPotion,
    }

    public sealed class SelectState
    {
        public SelectPurpose Purpose;
        public string Title;
        /// <summary>AddCard 的候选牌。</summary>
        public List<string> Cards = new List<string>();
        /// <summary>RemoveCard / UpgradeCard 时允许选的牌组下标（空 = 全部）。</summary>
        public List<int> DeckIndices = new List<int>();
        public bool CanSkip = true;
        /// <summary>选完要扣的金币（商店删卡、献金）。</summary>
        public int GoldCost;
        public string PendingPotion;
        /// <summary>选完之后去哪个界面（Map = 这个节点结束，回地图）。</summary>
        public RunPhase Return;
        /// <summary>跳过 / 取消之后去哪个界面。</summary>
        public RunPhase SkipReturn;
        /// <summary>这是商店的删卡服务（选完要累计删卡次数）。</summary>
        public bool ShopRemoval;
        /// <summary>卡牌奖励来自奖励列表的第几项（选完标记已领取）。</summary>
        public int RewardIndex = -1;
    }

    /// <summary>整局的统计（遥测 / runbatch 用，策划/07 §6.2）。</summary>
    public sealed class RunStats
    {
        public List<BattleRecord> Battles = new List<BattleRecord>();
        public List<CardChoiceRecord> CardChoices = new List<CardChoiceRecord>();
        public List<int> NodeHp = new List<int>();
        public int GoldEarned;
        public int ShopVisits;
        public int CardsRemoved;
        public int HpAtBoss = -1;
        public string DeathEncounter;
    }

    public sealed class BattleRecord
    {
        public int Floor;
        public string EncounterId;
        public RoomType Room;
        public bool Won;
        public int Turns;
        public int HpLost;
        public int MinSanity;
        public int Collapses;
        public int LunacyTurns;
        public int ClarityTurns;
        public bool EnteredLunacy;
        public int PotionsUsed;
        /// <summary>战斗结束时非召唤物敌人剩余的生命（输了的时候看差多少）。</summary>
        public int EnemyHpLeft;
        public Dictionary<string, int> PlaysByCard = new Dictionary<string, int>();
    }

    public sealed class CardChoiceRecord
    {
        public int Floor;
        public List<string> Offered = new List<string>();
        /// <summary>选中的牌，null = 跳过。</summary>
        public string Picked;
    }

    /// <summary>
    /// 一局的全部状态（策划/08 §6）：纯数据，没有任何逻辑对象。存档就是把它序列化成 JSON（Core 不做序列化，交给外层）。
    /// </summary>
    public sealed class RunState
    {
        public int Version = RunEngine.SaveVersion;
        public ulong Seed;
        public ulong[] Rng;

        public int MaxHp;
        public int Hp;
        public int MaxSanity;
        public int Gold;
        public List<string> Deck = new List<string>();
        public List<string> Relics = new List<string>();
        /// <summary>药水栏，null = 空位；长度 = 栏数。</summary>
        public List<string> Potions = new List<string>();

        public MapData Map;
        /// <summary>当前所在节点（-1 = 还没出发）。</summary>
        public int Row = -1;
        public int Col = -1;
        public int Floor;

        public RunPhase Phase;
        public BattleInfo Battle;
        public List<RewardItem> Rewards = new List<RewardItem>();
        public ShopState Shop;
        public EventState Event;
        public SelectState Select;

        // ---- 跨节点的局层计数
        public int BattlesFought;
        public int NormalFights;
        public List<string> RecentEncounters = new List<string>();
        public List<string> ElitesSeen = new List<string>();
        public List<string> EventsSeen = new List<string>();
        /// <summary>稀有卡保底偏移（百分点，策划/06 §4.1）。</summary>
        public int RarePity = -5;
        /// <summary>药水掉率（百分比，策划/06 §4.2）。</summary>
        public int PotionChance = 40;
        /// <summary>整局用过几次商店删卡（价格每次 +25）。</summary>
        public int Removals;

        public RunStats Stats = new RunStats();
    }
}
