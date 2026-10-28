using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Cards;

namespace Deeptide.Core.Run
{
    public enum RunOutcome
    {
        /// <summary>还在打（存档还在）。</summary>
        Unfinished,
        Victory,
        Defeat,
        /// <summary>中途放弃（菜单里放弃，或者有存档时开了新局）。</summary>
        Abandoned,
    }

    /// <summary>
    /// 试玩遥测（策划/07 §6.2）：一局一份，外层序列化成 JSON。Core 只把 RunState 摘成纯数据；版本号、写文件交给外层。
    /// 一局还没打完时也会写（Outcome = Unfinished），之后同一个文件不断覆盖，所以中途关掉游戏也留得下记录。
    /// Tools 的 <c>telemetry</c> 子命令读一个文件夹里的全部记录，按 07 §5 的目标区间汇总。
    /// </summary>
    public sealed class RunTelemetry
    {
        public const int CurrentFormat = 1;

        public int Format = CurrentFormat;
        /// <summary>游戏版本（外层填，编辑器里打的带「editor」）。</summary>
        public string GameVersion;
        public string StartedAt;
        /// <summary>写这份记录的时间（外层填）。</summary>
        public string UpdatedAt;
        public int RunIndex;
        /// <summary>第几夜（策划/12 §3.5）：试玩数据可以按第一夜 / 之后的夜分开看。0 = 加局外档之前的局。</summary>
        public int Night;
        public ulong Seed;
        /// <summary>角色（characters.csv 的 id）。加角色之前的记录没有这个字段，读进来按调查员算。</summary>
        public string Character = CharacterIds.Investigator;
        public RunOutcome Outcome;
        /// <summary>到达的层数。</summary>
        public int Floor;
        /// <summary>倒在哪：遭遇 ID，或 event:事件 ID。</summary>
        public string DeathCause;
        public double PlaySeconds;
        /// <summary>按最终牌组判断的流派：守夜 / 堕落 / 传染（狼人是狂猎 / 月相 / 血宴）/ 混合（见 <see cref="MainArchetype"/>）。</summary>
        public string Archetype;

        public int MaxHp, Hp, MaxSanity, Gold;
        public List<string> Deck = new List<string>();
        public List<string> Relics = new List<string>();
        /// <summary>选过的回响。加回响之前的记录没有这个字段，读进来是空的。</summary>
        public List<string> Echoes = new List<string>();
        public List<string> Potions = new List<string>();

        public int HpAtBoss = -1;
        public int GoldEarned, ShopVisits, CardsRemoved;
        public List<NodeRecord> Nodes = new List<NodeRecord>();
        public List<BattleRecord> Battles = new List<BattleRecord>();
        public List<CardChoiceRecord> CardChoices = new List<CardChoiceRecord>();
        public List<EchoChoiceRecord> EchoChoices = new List<EchoChoiceRecord>();

        /// <summary>从当前局摘出遥测。abandoned 只对还没结束的局有效（结算界面的「回到标题」不算放弃）。列表和 RunState 共用，调用方应立即序列化。</summary>
        public static RunTelemetry From(RunState s, bool abandoned = false)
        {
            var stats = s.Stats;
            var outcome = s.Phase == RunPhase.Victory ? RunOutcome.Victory
                : s.Phase == RunPhase.Defeat ? RunOutcome.Defeat
                : abandoned ? RunOutcome.Abandoned : RunOutcome.Unfinished;
            string death = null;
            if (outcome == RunOutcome.Defeat)
                death = stats.DeathEncounter ?? (s.Event != null ? "event:" + s.Event.Id : null);
            return new RunTelemetry
            {
                StartedAt = stats.StartedAt,
                RunIndex = stats.RunIndex,
                Night = stats.Night,
                Seed = s.Seed,
                Character = s.Character,
                Outcome = outcome,
                Floor = s.Floor,
                DeathCause = death,
                PlaySeconds = stats.PlaySeconds,
                Archetype = MainArchetype(s.Deck),
                MaxHp = s.MaxHp,
                Hp = s.Hp,
                MaxSanity = s.MaxSanity,
                Gold = s.Gold,
                Deck = s.Deck,
                Relics = s.Relics,
                Echoes = s.Echoes,
                Potions = s.Potions.Where(p => p != null).ToList(),
                HpAtBoss = stats.HpAtBoss,
                GoldEarned = stats.GoldEarned,
                ShopVisits = stats.ShopVisits,
                CardsRemoved = stats.CardsRemoved,
                Nodes = stats.Nodes,
                Battles = stats.Battles,
                CardChoices = stats.CardChoices,
                EchoChoices = stats.EchoChoices,
            };
        }

        /// <summary>不算流派的 archetype 值：基础牌和各角色的通用牌。状态牌、诅咒按类型排除。</summary>
        internal static readonly string[] NotArchetypes = { "基础", "通用" };

        /// <summary>
        /// 牌组的主流派：只数流派牌（调查员是守夜 / 堕落 / 传染，狼人是狂猎 / 月相 / 血宴），某一类占一半以上且至少 3 张就算它，
        /// 否则算混合（含只有起始牌的牌组）。真人的构筑没有标签，这是给汇总分组用的粗略口径。
        /// </summary>
        public static string MainArchetype(IEnumerable<string> deck)
        {
            var counts = new Dictionary<string, int>();
            foreach (var id in deck)
            {
                var def = CardLibrary.Get(CardLibrary.StripUpgrade(id, out _));
                if (def.Type == CardType.Curse || def.Type == CardType.Status) continue;
                if (string.IsNullOrEmpty(def.Archetype) || NotArchetypes.Contains(def.Archetype)) continue;
                counts[def.Archetype] = counts.TryGetValue(def.Archetype, out int n) ? n + 1 : 1;
            }
            if (counts.Count == 0) return "混合";
            int total = counts.Values.Sum();
            var top = counts.OrderByDescending(kv => kv.Value).First();
            return top.Value >= 3 && top.Value * 2 > total ? top.Key : "混合";
        }
    }
}
