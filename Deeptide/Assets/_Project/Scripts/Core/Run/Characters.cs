using System;
using System.Collections.Generic;

namespace Deeptide.Core.Run
{
    public static class CharacterIds
    {
        /// <summary>默认角色：RunConfig 不指定、旧存档没有角色字段时都按调查员算。</summary>
        public const string Investigator = "investigator";
    }

    /// <summary>角色静态数据，对应 Data/characters.csv 的一行（策划/14、09 M6.1）。开局时 RunConfig 没指定的参数都从这里取。</summary>
    public sealed class CharacterDef
    {
        public string Id;
        public string Name;
        /// <summary>职业名，选角界面的副标题（狼人 / 巫师 / 战士）。</summary>
        public string Title;
        public int MaxHp;
        public int MaxSanity;
        public int Gold;
        public string StartingRelic;
        /// <summary>特性：relics.csv 里 rarity = Trait 的一行，开战时作为隐藏遗物挂上（BattleSetup.Traits）。空 = 没有（调查员）。</summary>
        public string Trait = "";
        /// <summary>事件「梦中的导师」把一张打击变成的牌（调查员是梦魇之刃，狼人是撕咬）。</summary>
        public string DreamCard;
        /// <summary>起始牌组，按表里写的顺序展开（顺序决定开局的卡牌编号，改顺序会让同种子的整局结果变掉）。</summary>
        public List<string> Deck = new List<string>();
        /// <summary>特性说明（策划/14 的「特性」），选角界面用。</summary>
        public string Text;
    }

    /// <summary>角色注册表，内容由 Data.GameData 从 characters.csv 载入，按表格行序保存。</summary>
    public static class CharacterLibrary
    {
        static readonly Dictionary<string, CharacterDef> Defs = new Dictionary<string, CharacterDef>();
        static readonly List<CharacterDef> Ordered = new List<CharacterDef>();

        public static IReadOnlyList<CharacterDef> All => Ordered;

        public static bool Exists(string id) => id != null && Defs.ContainsKey(id);

        public static CharacterDef Get(string id)
        {
            if (id != null && Defs.TryGetValue(id, out var def)) return def;
            if (Defs.Count == 0) throw new InvalidOperationException("角色数据还没有载入（先调用 GameData.Load）");
            throw new KeyNotFoundException($"未知角色：{id}");
        }

        /// <summary>
        /// 卡牌 / 遗物 / 药水的归属列（owner）是否允许出现在这个角色的局里：空 = 通用，所有角色都能拿到；否则只给这个角色。
        /// 奖励、商店、事件、遗物和药水掉落都按它过滤（策划/14 §1.1）。
        /// </summary>
        public static bool Allows(string owner, string character) => string.IsNullOrEmpty(owner) || owner == character;

        internal static void Clear()
        {
            Defs.Clear();
            Ordered.Clear();
        }

        internal static void Add(CharacterDef def)
        {
            Defs.Add(def.Id, def);
            Ordered.Add(def);
        }
    }
}
