using System;
using System.Collections.Generic;
using System.Linq;

namespace Deeptide.Core.Content
{
    /// <summary>纸面原型的测试牌组（原型/阶段0 规则第 3 条），数字版用同一套组合做对照。</summary>
    public static class TestDecks
    {
        public static readonly string[] Starter =
        {
            "B01", "B01", "B01", "B01", "B01",
            "B02", "B02", "B02", "B02",
            "B03",
        };

        public static readonly string[] WatchExtra = { "C01", "C02", "C03", "C04", "U02", "R05" };
        public static readonly string[] FallExtra = { "C06", "C07", "C08", "C09", "C10", "U09" };
        public static readonly string[] MixedExtra = { "C01", "C03", "R05", "C08", "C06", "U09" };

        public static readonly string[] Names = { "starter", "A", "B", "C" };

        public static List<string> Get(string name)
        {
            switch (name)
            {
                case "starter": return Starter.ToList();
                case "A": return Starter.Concat(WatchExtra).ToList();
                case "B": return Starter.Concat(FallExtra).ToList();
                case "C": return Starter.Concat(MixedExtra).ToList();
                default: throw new ArgumentException($"未知牌组：{name}（可选 starter / A / B / C）");
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
                default: return name;
            }
        }
    }
}
