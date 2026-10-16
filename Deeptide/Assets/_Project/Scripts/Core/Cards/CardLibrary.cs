using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Deeptide.Core.Cards
{
    public static class CardIds
    {
        public const string Whispers = "S01";
    }

    /// <summary>卡牌注册表。注册时校验文本占位符，相当于 策划/08 §4 说的「启动校验」。</summary>
    public static class CardLibrary
    {
        static readonly Dictionary<string, CardDef> Defs = new Dictionary<string, CardDef>();
        static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        static CardLibrary()
        {
            InvestigatorCards.Register();
        }

        public static IEnumerable<CardDef> All => Defs.Values;

        public static bool Exists(string id) => Defs.ContainsKey(StripUpgrade(id, out _));

        public static CardDef Get(string id)
        {
            if (!Defs.TryGetValue(id, out var def)) throw new KeyNotFoundException($"未知卡牌：{id}");
            return def;
        }

        /// <summary>id 末尾带「+」表示升级版，如「C06+」。</summary>
        public static Card Create(string id, int uid)
        {
            var def = Get(StripUpgrade(id, out bool upgraded));
            var card = def.Factory();
            card.Init(def, uid, upgraded);
            return card;
        }

        internal static void Add(CardDef def)
        {
            if (Defs.ContainsKey(def.Id)) throw new InvalidOperationException($"卡牌 ID 重复：{def.Id}");
            if (def.Dual && string.IsNullOrEmpty(def.TextMad)) throw new InvalidOperationException($"{def.Id} 是双面牌但没有癫狂面文本");
            foreach (var text in new[] { def.TextSane, def.TextMad })
            {
                if (text == null) continue;
                foreach (Match m in Placeholder.Matches(text))
                    if (!def.Vars.ContainsKey(m.Groups[1].Value))
                        throw new InvalidOperationException($"{def.Id} 的文本引用了未定义的变量 {{{m.Groups[1].Value}}}");
            }
            foreach (var key in def.UpgradeVars.Keys)
                if (!def.Vars.ContainsKey(key))
                    throw new InvalidOperationException($"{def.Id} 的升级变量 {key} 在基础变量里不存在");
            Defs.Add(def.Id, def);
        }

        /// <summary>解析「Damage=6;Hits=2」格式，与 CSV 的 vars 列一致。</summary>
        public static Dictionary<string, int> ParseVars(string text)
        {
            var result = new Dictionary<string, int>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (var pair in text.Split(';'))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2) throw new FormatException($"变量格式错误：{pair}");
                result[kv[0].Trim()] = int.Parse(kv[1].Trim(), CultureInfo.InvariantCulture);
            }
            return result;
        }

        static string StripUpgrade(string id, out bool upgraded)
        {
            upgraded = id.EndsWith("+", StringComparison.Ordinal);
            return upgraded ? id.Substring(0, id.Length - 1) : id;
        }
    }
}
