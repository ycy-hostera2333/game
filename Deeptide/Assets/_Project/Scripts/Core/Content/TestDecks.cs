using System;
using System.Collections.Generic;
using System.Linq;

namespace Deeptide.Core.Content
{
    /// <summary>
    /// 测试牌组：起始牌组 + 6 张流派牌，代表「打到困难池时」的中期构筑。纸面原型用的是 A / B0 / C0；
    /// 内容填充（策划/09 M3）后 B / C 换成用上新卡的版本，新增传染流 D。
    /// </summary>
    public static class TestDecks
    {
        /// <summary>起始牌组：打击 ×5、防御 ×3、祷告 ×1、禁忌一瞥 ×1（策划/03 §1，模拟调参后用祷告换掉 1 张防御）。</summary>
        public static readonly string[] Starter =
        {
            "B01", "B01", "B01", "B01", "B01",
            "B02", "B02", "B02", "C01",
            "B03",
        };

        public static readonly string[] WatchExtra = { "C01", "C02", "C03", "C04", "U02", "R05" };
        public static readonly string[] FallExtra = { "C06", "C07", "C08", "C09", "U07", "U09" };
        public static readonly string[] MixedExtra = { "C01", "C03", "C08", "C14", "C15", "U09" };
        public static readonly string[] ContagionExtra = { "C11", "C12", "C13", "U11", "U12", "U13" };
        public static readonly string[] PrototypeFallExtra = { "C06", "C07", "C08", "C09", "C10", "U09" };
        public static readonly string[] PrototypeMixedExtra = { "C01", "C03", "R05", "C08", "C06", "U09" };

        public static readonly string[] Names = { "starter", "A", "B", "C", "D", "B0", "C0" };

        public static List<string> Get(string name)
        {
            switch (name)
            {
                case "starter": return Starter.ToList();
                case "A": return Starter.Concat(WatchExtra).ToList();
                case "B": return Starter.Concat(FallExtra).ToList();
                case "C": return Starter.Concat(MixedExtra).ToList();
                case "D": return Starter.Concat(ContagionExtra).ToList();
                case "B0": return Starter.Concat(PrototypeFallExtra).ToList();
                case "C0": return Starter.Concat(PrototypeMixedExtra).ToList();
                default: throw new ArgumentException($"未知牌组：{name}（可选 {string.Join(" / ", Names)}）");
            }
        }

        public static string Describe(string name)
        {
            switch (name)
            {
                case "starter": return "起始牌组";
                case "A": return "A 守夜";
                case "B": return "B 堕落";
                case "C": return "C 混合";
                case "D": return "D 传染";
                case "B0": return "B0 原型堕落";
                case "C0": return "C0 原型混合";
                default: return name;
            }
        }
    }
}
