using System;
using System.Collections.Generic;

namespace Deeptide.Core
{
    /// <summary>
    /// 状态层数表（策划/02 §5）。
    /// 回合制状态（易伤 / 虚弱 / 脆弱）在持有者回合结束时 −1；束缚在抽牌时清空；心防用掉为止；
    /// 流血在敌人回合开始时结算后 −1（BleedAction，策划/14 §1.4）。
    /// </summary>
    public sealed class StatusSet
    {
        /// <summary>回合制状态：持有者回合结束时 −1（由 BattleEngine.TickStatuses 结算并写事件）。</summary>
        public static readonly StatusId[] TurnBased = { StatusId.Vulnerable, StatusId.Weak, StatusId.Frail };
        static readonly StatusId[] AllIds = (StatusId[])Enum.GetValues(typeof(StatusId));

        readonly Dictionary<StatusId, int> _amounts = new Dictionary<StatusId, int>();

        public int Get(StatusId id) => _amounts.TryGetValue(id, out var v) ? v : 0;

        public void Add(StatusId id, int amount) => Set(id, Get(id) + amount);

        public void Set(StatusId id, int amount)
        {
            if (amount == 0) _amounts.Remove(id);
            else _amounts[id] = amount;
        }

        /// <summary>按枚举顺序列出非零状态（显示和哈希都依赖这个固定顺序）。</summary>
        public IEnumerable<KeyValuePair<StatusId, int>> Ordered()
        {
            foreach (var id in AllIds)
            {
                int v = Get(id);
                if (v != 0) yield return new KeyValuePair<StatusId, int>(id, v);
            }
        }

        public static string NameOf(StatusId id)
        {
            switch (id)
            {
                case StatusId.Strength: return "力量";
                case StatusId.Dexterity: return "敏捷";
                case StatusId.Vulnerable: return "易伤";
                case StatusId.Weak: return "虚弱";
                case StatusId.Frail: return "脆弱";
                case StatusId.Bound: return "束缚";
                case StatusId.Ward: return "心防";
                case StatusId.Bleed: return "流血";
                default: return id.ToString();
            }
        }
    }
}
