using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deeptide.Core.Cards;
using Deeptide.Core.Map;
using Deeptide.Core.Run;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Deeptide.Sim
{
    /// <summary>
    /// telemetry 子命令：读一个文件夹（含子文件夹）里的试玩遥测 JSON（策划/07 §6.2，游戏每局写一份），按 07 §5 的目标区间汇总。
    /// 和 runbatch 共用一套口径（RunModes.PrintMetrics），区别是这里是真人的数据：样本少，按局逐条列出来更有用。
    /// </summary>
    static class TelemetryReport
    {
        /// <summary>游戏写遥测的默认位置：%USERPROFILE%\AppData\LocalLow\DefaultCompany\Deeptide\telemetry（编辑器和 exe 共用）。</summary>
        static string DefaultDir()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(Path.GetDirectoryName(local) ?? local, "LocalLow", "DefaultCompany", "Deeptide", "telemetry");
        }

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { Converters = { new StringEnumConverter() } };

        public static int Run(Dictionary<string, string> o)
        {
            string dir = o.TryGetValue("dir", out var d) ? d : DefaultDir();
            bool editor = !o.TryGetValue("editor", out var e) || e != "0";
            if (!Directory.Exists(dir))
            {
                Console.Error.WriteLine($"没有这个文件夹：{dir}");
                return 1;
            }

            var files = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories);
            var runs = new List<RunTelemetry>();
            foreach (var f in files)
            {
                try
                {
                    var t = JsonConvert.DeserializeObject<RunTelemetry>(File.ReadAllText(f), Settings);
                    if (t != null && t.Format == RunTelemetry.CurrentFormat) runs.Add(t);
                    else Console.WriteLine($"  跳过 {Path.GetFileName(f)}：格式版本 {t?.Format}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  跳过 {Path.GetFileName(f)}：{ex.Message}");
                }
            }
            // 同一局被收集了两次（换了文件夹名重发）：只留最后写的那份
            runs = runs.GroupBy(t => (t.StartedAt, t.Seed)).Select(g => g.OrderBy(t => t.UpdatedAt).Last()).ToList();
            if (!editor) runs = runs.Where(t => t.GameVersion == null || !t.GameVersion.Contains("editor")).ToList();
            runs = runs.OrderBy(t => t.StartedAt).ToList();

            Console.WriteLine($"试玩遥测：{dir}");
            Console.WriteLine($"  {runs.Count} 局（通关 {Count(runs, RunOutcome.Victory)} · 倒下 {Count(runs, RunOutcome.Defeat)} · 放弃 {Count(runs, RunOutcome.Abandoned)} · 没打完 {Count(runs, RunOutcome.Unfinished)}）" +
                              $"；版本 {string.Join("、", runs.GroupBy(t => t.GameVersion ?? "?").Select(g => $"{g.Key} ×{g.Count()}"))}" +
                              (editor ? "" : "（已排除编辑器里打的）"));
            if (runs.Count == 0) return 0;
            Console.WriteLine();

            PrintRuns(runs);
            var finished = runs.Where(t => t.Outcome == RunOutcome.Victory || t.Outcome == RunOutcome.Defeat).ToList();
            if (finished.Count > 0)
            {
                Console.WriteLine($"══ 打完的 {finished.Count} 局");
                var first = finished.Where(t => t.RunIndex == 1).ToList();
                Console.WriteLine($"  胜率 {Count(finished, RunOutcome.Victory) / (double)finished.Count:P0}" +
                                  (first.Count > 0 ? $"（第 1 局 {Count(first, RunOutcome.Victory)}/{first.Count}，目标新手 10–25%、熟练 40–60%）" : "") +
                                  $"  单局用时中位数 {Median(finished.Select(t => t.PlaySeconds)) / 60:F0} 分钟（目标 35–50）");
                RunModes.PrintMetrics(finished.Select(t => new RunModes.RunSample(t)).ToList(), finished.Count);
                Console.WriteLine();
            }
            PrintArchetypes(runs);
            PrintChoices(runs);
            PrintNodeTimes(runs);
            PrintPlays(runs);
            RunModes.PrintPickRates(runs.SelectMany(t => t.CardChoices), "真人的选择，样本少时只看极端值");
            return 0;
        }

        static int Count(IEnumerable<RunTelemetry> runs, RunOutcome outcome) => runs.Count(t => t.Outcome == outcome);

        static string OutcomeName(RunOutcome o) =>
            o == RunOutcome.Victory ? "通关" : o == RunOutcome.Defeat ? "倒下" : o == RunOutcome.Abandoned ? "放弃" : "没打完";

        static void PrintRuns(List<RunTelemetry> runs)
        {
            Console.WriteLine("逐局（按开局时间）：");
            Console.WriteLine("  开局时间             第几局  流派  结果    层数  用时    Boss 前生命  死因");
            foreach (var t in runs)
                Console.WriteLine($"  {t.StartedAt ?? "?",-19}  {t.RunIndex,4}    {t.Archetype,-2}  {OutcomeName(t.Outcome),-4}  {t.Floor,4}  {t.PlaySeconds / 60,4:F0} 分  " +
                                  $"{(t.HpAtBoss >= 0 ? t.HpAtBoss.ToString() : "-"),8}     {(t.DeathCause != null ? RunModes.DeathName(t.DeathCause) : "")}");
            Console.WriteLine();
        }

        /// <summary>按最终牌组的流派分组：07 §5.3 的三流派胜率差和三项理智指标。战斗算全部局的（含没打完的）。</summary>
        static void PrintArchetypes(List<RunTelemetry> runs)
        {
            Console.WriteLine("══ 按流派（看最终牌组；战斗统计含没打完的局）");
            foreach (var g in runs.GroupBy(t => t.Archetype).OrderBy(g => g.Key))
            {
                var battles = g.SelectMany(t => t.Battles).ToList();
                int done = g.Count(t => t.Outcome == RunOutcome.Victory || t.Outcome == RunOutcome.Defeat);
                Console.Write($"  {g.Key}  {g.Count()} 局（打完 {done}，通关 {Count(g, RunOutcome.Victory)}）  战斗 {battles.Count} 场");
                if (battles.Count == 0)
                {
                    Console.WriteLine();
                    continue;
                }
                Console.WriteLine();
                RunModes.PrintSanity("    ", battles);
            }
            Console.WriteLine("  目标：三流派胜率差 ≤ 10 个百分点；堕落每场崩溃 0.3–0.8；守夜清明回合 ≥ 50%");
            Console.WriteLine();
        }

        static void PrintChoices(List<RunTelemetry> runs)
        {
            var nodes = runs.SelectMany(t => t.Nodes).ToList();
            Console.WriteLine("══ 局层选择");
            var fires = nodes.Where(n => n.Room == RoomType.Campfire && n.Picks.Count > 0).ToList();
            if (fires.Count > 0)
            {
                // 篝火的最后一个选择才是落定的（锻造取消后可能改成休息）
                string Final(NodeRecord n) => n.Picks.LastOrDefault(p => p == "rest" || p == "meditate" || p == "purge" || p.StartsWith("upgrade:")) ?? "?";
                var rest = fires.Where(n => Final(n) == "rest").ToList();
                Console.WriteLine($"  篝火 {fires.Count} 次：休息 {rest.Count}" + (rest.Count > 0 ? $"（进入时生命 {rest.Average(n => n.Hp / (double)Math.Max(1, n.MaxHp)):P0}）" : "") +
                                  $" · 锻造 {fires.Count(n => Final(n).StartsWith("upgrade:"))} · 静思 {fires.Count(n => Final(n) == "meditate")} · 净化疯语 {fires.Count(n => Final(n) == "purge")}");
            }
            var shops = nodes.Where(n => n.Room == RoomType.Shop).ToList();
            if (shops.Count > 0)
            {
                var picks = shops.SelectMany(n => n.Picks).ToList();
                Console.WriteLine($"  商店 {shops.Count} 次（进店时金币平均 {shops.Average(n => n.Gold):F0}）：买牌 {picks.Count(p => p.StartsWith("shop:card:"))} · 遗物 {picks.Count(p => p.StartsWith("shop:relic:"))}" +
                                  $" · 药水 {picks.Count(p => p.StartsWith("shop:potion:"))} · 删卡 {picks.Count(p => p.StartsWith("remove:"))} · 什么都没买 {shops.Count(n => n.Picks.All(p => p == "leave" || p == "remove" || p == "skip"))}");
            }
            var events = nodes.Where(n => n.Room == RoomType.Event && n.Content != null && EventExists(n.Content) && n.Picks.Count > 0).ToList();
            foreach (var g in events.GroupBy(n => n.Content).OrderBy(g => g.Key))
            {
                var def = EventLibrary.Get(g.Key);
                var counts = g.GroupBy(n => n.Picks[0]).Select(x => $"{OptionName(def, x.Key)} {x.Count()}");
                Console.WriteLine($"  {def.Name}：{string.Join(" · ", counts)}");
            }
            Console.WriteLine();
        }

        static bool EventExists(string id)
        {
            try { EventLibrary.Get(id); return true; }
            catch (KeyNotFoundException) { return false; }
        }

        /// <summary>选项文本里括号之前的部分（「施救（失去 6 生命……）」→「施救」）。</summary>
        static string OptionName(EventDef def, string id)
        {
            var text = def.Options.FirstOrDefault(kv => kv.Key == id).Value;
            if (string.IsNullOrEmpty(text)) return id;
            int cut = text.IndexOfAny(new[] { '（', '(', '：' });
            return cut > 0 ? text.Substring(0, cut) : text;
        }

        /// <summary>每类节点从进入到离开的真实用时（下一个节点的进入时间 − 这个节点的；最后一个节点用整局用时收尾，只算打完的局）。</summary>
        static void PrintNodeTimes(List<RunTelemetry> runs)
        {
            var spans = new Dictionary<RoomType, List<double>>();
            foreach (var t in runs)
                for (int i = 0; i < t.Nodes.Count; i++)
                {
                    bool last = i + 1 == t.Nodes.Count;
                    if (last && t.Outcome != RunOutcome.Victory && t.Outcome != RunOutcome.Defeat) continue;
                    double end = last ? t.PlaySeconds : t.Nodes[i + 1].Seconds;
                    double span = end - t.Nodes[i].Seconds;
                    if (span <= 0) continue;   // 没计时的记录（模拟器、编辑器里改过的局）
                    if (!spans.TryGetValue(t.Nodes[i].Room, out var list)) spans[t.Nodes[i].Room] = list = new List<double>();
                    list.Add(span);
                }
            if (spans.Count == 0) return;
            Console.WriteLine("══ 节点用时（分钟，中位数 / 场次）");
            Console.WriteLine("  " + string.Join(" · ", spans.OrderBy(kv => kv.Key).Select(kv => $"{RunEngine.RoomName(kv.Key)} {Median(kv.Value) / 60:F1}（{kv.Value.Count}）")));
            Console.WriteLine();
        }

        static void PrintPlays(List<RunTelemetry> runs)
        {
            var plays = new Dictionary<string, int>();
            foreach (var b in runs.SelectMany(t => t.Battles))
                foreach (var kv in b.PlaysByCard)
                {
                    string id = CardLibrary.StripUpgrade(kv.Key, out _);
                    plays[id] = plays.TryGetValue(id, out var x) ? x + kv.Value : kv.Value;
                }
            if (plays.Count == 0) return;
            Console.WriteLine("══ 打出最多的牌（升级前后合并）");
            Console.WriteLine("  " + string.Join(" · ", plays.OrderByDescending(kv => kv.Value).Take(12).Select(kv => $"{CardLibrary.Get(kv.Key).Name} {kv.Value}")));
            Console.WriteLine();
        }

        static double Median(IEnumerable<double> values)
        {
            var list = values.OrderBy(x => x).ToList();
            return list.Count == 0 ? 0 : list[list.Count / 2];
        }
    }
}
