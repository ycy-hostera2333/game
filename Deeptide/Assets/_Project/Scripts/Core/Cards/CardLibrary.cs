using System;
using System.Collections.Generic;

namespace Deeptide.Core.Cards
{
    public static class CardIds
    {
        public const string Whispers = "S01";
        public const string Web = "S02";
    }

    /// <summary>卡牌注册表，内容由 Data.GameData 从 cards.csv 载入。按表格行序保存，随机抽牌时顺序固定。</summary>
    public static class CardLibrary
    {
        static readonly Dictionary<string, CardDef> Defs = new Dictionary<string, CardDef>();
        static readonly List<CardDef> Ordered = new List<CardDef>();

        public static IReadOnlyList<CardDef> All => Ordered;

        public static bool Exists(string id) => Defs.ContainsKey(StripUpgrade(id, out _));

        public static CardDef Get(string id)
        {
            if (Defs.TryGetValue(id, out var def)) return def;
            if (Defs.Count == 0) throw new InvalidOperationException("卡牌数据还没有载入（先调用 GameData.Load）");
            throw new KeyNotFoundException($"未知卡牌：{id}");
        }

        /// <summary>id 末尾带「+」表示升级版，如「C06+」。</summary>
        public static Card Create(string id, int uid)
        {
            var def = Get(StripUpgrade(id, out bool upgraded));
            var card = def.Factory();
            card.Init(def, uid, upgraded);
            return card;
        }

        internal static void Clear()
        {
            Defs.Clear();
            Ordered.Clear();
        }

        internal static void Add(CardDef def)
        {
            Defs.Add(def.Id, def);
            Ordered.Add(def);
        }

        public static string StripUpgrade(string id, out bool upgraded)
        {
            upgraded = id.EndsWith("+", StringComparison.Ordinal);
            return upgraded ? id.Substring(0, id.Length - 1) : id;
        }
    }
}
