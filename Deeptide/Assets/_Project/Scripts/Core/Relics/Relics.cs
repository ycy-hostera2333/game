using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Deeptide.Core.Actions;

namespace Deeptide.Core.Relics
{
    public static class RelicIds
    {
        public const string InvestigatorNotes = "investigator_notes";
        public const string PawnbrokersScale = "pawnbrokers_scale";
        public const string AlchemistBelt = "alchemist_belt";
        public const string CollectorsLens = "collectors_lens";
    }

    public enum RelicRarity { Starter, Common, Uncommon, Rare, Shop }

    /// <summary>遗物静态数据，对应 Data/relics.csv 的一行。</summary>
    public sealed class RelicDef
    {
        public string Id;
        public string Name;
        public RelicRarity Rarity;
        /// <summary>流派倾向（守夜 / 堕落 / 传染 / 中立），只用于统计和奖励偏好。</summary>
        public string Tendency;
        public Dictionary<string, int> Vars = new Dictionary<string, int>();
        public string Text;
        public Func<Relic> Factory;
    }

    public abstract class Relic : BattleListener
    {
        static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        public RelicDef Def { get; private set; }

        internal void Init(RelicDef def) => Def = def;

        public string Id => Def.Id;
        public string Name => Def.Name;
        public string Description => Placeholder.Replace(Def.Text ?? "", m => V(m.Groups[1].Value).ToString());

        /// <summary>记一条「遗物触发」的叙述事件。</summary>
        protected void Trigger(BattleEngine e, string what)
        {
            var ev = e.Log.Add(BattleEventType.RelicTriggered, $"{Name}：{what}", Name);
            ev.Id = Id;
        }

        public int V(string key)
        {
            if (!Def.Vars.TryGetValue(key, out var v)) throw new KeyNotFoundException($"{Def.Id} 缺少数值 {key}");
            return v;
        }
    }

    /// <summary>遗物注册表，内容由 Data.GameData 从 relics.csv 载入。</summary>
    public static class RelicLibrary
    {
        static readonly Dictionary<string, RelicDef> Defs = new Dictionary<string, RelicDef>();
        static readonly List<RelicDef> Ordered = new List<RelicDef>();

        public static IReadOnlyList<RelicDef> All => Ordered;

        public static bool Exists(string id) => Defs.ContainsKey(id);

        public static RelicDef Get(string id)
        {
            if (Defs.TryGetValue(id, out var def)) return def;
            if (Defs.Count == 0) throw new InvalidOperationException("遗物数据还没有载入（先调用 GameData.Load）");
            throw new KeyNotFoundException($"未知遗物：{id}");
        }

        public static Relic Create(string id)
        {
            var def = Get(id);
            var relic = def.Factory();
            relic.Init(def);
            return relic;
        }

        internal static void Clear()
        {
            Defs.Clear();
            Ordered.Clear();
        }

        internal static void Add(RelicDef def)
        {
            Defs.Add(def.Id, def);
            Ordered.Add(def);
        }
    }

    /// <summary>遗物类注册表：relics.csv 的每一行按 ID 找到这里的工厂（显式表，理由同 CardClasses）。</summary>
    public static class RelicClasses
    {
        public static readonly IReadOnlyDictionary<string, Func<Relic>> Factories = new Dictionary<string, Func<Relic>>
        {
            [RelicIds.InvestigatorNotes] = () => new InvestigatorNotes(),
            ["old_pipe"] = () => new OldPipe(),
            ["salt_circle"] = () => new SaltCircle(),
            ["black_candle"] = () => new BlackCandle(),
            ["whale_oil_lamp"] = () => new WhaleOilLamp(),
            ["worn_prayer_book"] = () => new WornPrayerBook(),
            ["lead_amulet"] = () => new LeadAmulet(),
            ["fishermans_charm"] = () => new FishermansCharm(),
            ["broken_watch"] = () => new BrokenWatch(),
            ["third_eye"] = () => new ThirdEye(),
            ["silver_cross"] = () => new SilverCross(),
            ["fear_mask"] = () => new FearMask(),
            ["consecrated_bell"] = () => new ConsecratedBell(),
            ["watchers_lantern"] = () => new WatchersLantern(),
            ["abyssal_fragment"] = () => new AbyssalFragment(),
            ["crown_of_dread"] = () => new CrownOfDread(),
            ["drowned_ring"] = () => new DrownedRing(),
            [RelicIds.PawnbrokersScale] = () => new PawnbrokersScale(),
            [RelicIds.AlchemistBelt] = () => new AlchemistBelt(),
            [RelicIds.CollectorsLens] = () => new CollectorsLens(),
        };
    }

    /// <summary>起始遗物「调查笔记」：每场战斗首次进入癫狂时，获得 1 能量并抽 2 张牌（策划/03 §1）。</summary>
    public sealed class InvestigatorNotes : Relic
    {
        public bool TriggeredThisCombat { get; private set; }

        public override void OnCombatStart(BattleEngine e) => TriggeredThisCombat = false;

        public override void OnLunacyChanged(BattleEngine e, bool entered)
        {
            if (!entered || TriggeredThisCombat) return;
            TriggeredThisCombat = true;
            e.Log.Add(BattleEventType.RelicTriggered, $"调查笔记触发：+{V("Energy")} 能量，抽 {V("Draw")} 张牌", Name).Id = Id;
            e.Queue.AddToTop(new GainEnergyAction(V("Energy")), new DrawCardsAction(V("Draw")));
        }
    }
}
