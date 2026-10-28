using System;
using System.Collections.Generic;
using System.Linq;

namespace Deeptide.Core.Enemies
{
    public enum EncounterPool { Easy, Hard, Elite, Boss }

    public static class EncounterIds
    {
        /// <summary>溺亡司祭：加 Boss 三选一之前唯一的 Boss，旧存档没有 Boss 字段时按它算；打倒它才解锁附言 D（策划/12、15 §9）。</summary>
        public const string Priest = "boss_priest";
    }

    /// <summary>一组遭遇，对应 Data/encounters.csv 的一行（策划/04 §3）。</summary>
    public sealed class EncounterDef
    {
        public string Id;
        public string Name;
        public EncounterPool Pool;
        public List<string> Enemies = new List<string>();
    }

    /// <summary>遭遇注册表，内容由 Data.GameData 从 encounters.csv 载入。</summary>
    public static class EncounterLibrary
    {
        static readonly Dictionary<string, EncounterDef> Defs = new Dictionary<string, EncounterDef>();
        static readonly List<EncounterDef> Ordered = new List<EncounterDef>();

        public static IReadOnlyList<EncounterDef> All => Ordered;

        public static IEnumerable<EncounterDef> InPool(EncounterPool pool) => Ordered.Where(d => d.Pool == pool);

        public static bool Exists(string id) => Defs.ContainsKey(id);

        public static EncounterDef Get(string id)
        {
            if (Defs.TryGetValue(id, out var def)) return def;
            if (Defs.Count == 0) throw new InvalidOperationException("遭遇数据还没有载入（先调用 GameData.Load）");
            throw new KeyNotFoundException($"未知遭遇：{id}");
        }

        internal static void Clear()
        {
            Defs.Clear();
            Ordered.Clear();
        }

        internal static void Add(EncounterDef def)
        {
            Defs.Add(def.Id, def);
            Ordered.Add(def);
        }
    }

    /// <summary>纸面原型的三个测试遭遇（原型/阶段0 规则第 4 条），对应遭遇表里的三组。</summary>
    public static class Encounters
    {
        public static readonly IReadOnlyDictionary<string, string> PrototypeIds = new Dictionary<string, string>
        {
            ["1"] = "easy_acolyte",
            ["2"] = "hard_acolyte_whisperer",
            ["3"] = "hard_townsfolk",
        };

        /// <summary>原型编号 1 / 2 / 3，或者遭遇表里的 ID。</summary>
        public static EncounterDef Resolve(string idOrPrototype) =>
            EncounterLibrary.Get(PrototypeIds.TryGetValue(idOrPrototype, out var id) ? id : idOrPrototype);

        public static IReadOnlyDictionary<string, string[]> Prototype =>
            PrototypeIds.ToDictionary(kv => kv.Key, kv => EncounterLibrary.Get(kv.Value).Enemies.ToArray());
    }
}
