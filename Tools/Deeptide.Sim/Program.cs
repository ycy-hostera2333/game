using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Content;
using Deeptide.Core.Data;
using Deeptide.Core.Enemies;
using Deeptide.Tools;

namespace Deeptide.Sim
{
    /// <summary>
    /// 深潮战斗内核的控制台工具：
    ///   play   手动对战（--deck A|B|C|starter --enc 1|2|3 --seed N）
    ///   auto   AI 自动打一场并输出完整日志
    ///   batch  批量模拟并统计（--n 200 --deck all|A|B|C|starter --enc all|1|2|3）
    /// </summary>
    static class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            string mode = args.Length > 0 ? args[0] : "help";
            var opts = ParseOptions(args.Skip(1).ToArray());
            try { DataFiles.EnsureLoaded(); }
            catch (DataValidationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
            if (opts.TryGetValue("set", out var spec)) Tuning.Apply(spec);
            if (opts.TryGetValue("peek", out var peek) && peek == "1") Autoplayer.IgnoreBlur = true;
            if (opts.TryGetValue("ai", out var aiKind)) Autoplayer.UseBeam = aiKind != "greedy";
            if (opts.TryGetValue("battle-style", out var bs))
                RunAutoplayer.BattleStyleOverride = bs == "sane" ? PlayStyle.Sane : bs == "mad" ? PlayStyle.Mad : PlayStyle.Mixed;
            try
            {
                switch (mode)
                {
                    case "play": return Play(opts);
                    case "auto": return Auto(opts);
                    case "batch": return Batch(opts);
                    case "replay": return Replay(opts);
                    case "tune": return Tune(opts);
                    case "map": return RunModes.Map(opts);
                    case "run": return RunModes.Run(opts);
                    case "runreplay": return RunModes.RunReplay(opts);
                    case "runauto": return RunModes.RunAuto(opts);
                    case "runview": return RunModes.RunView(opts);
                    case "runbatch": return RunModes.RunBatch(opts);
                    default:
                        PrintHelp();
                        return 0;
                }
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("深潮 · 战斗内核控制台");
            Console.WriteLine("  play  [--deck A|B|C|starter] [--enc 1|2|3] [--seed N]   手动对战");
            Console.WriteLine("  auto  [--deck A|B|C|starter] [--enc 1|2|3] [--seed N]   AI 打一场，输出完整日志");
            Console.WriteLine("  batch [--n 200] [--deck all|A,B,C,D|starter] [--enc all|1|2|3|pools|pool:hard|遭遇ID,...]   批量模拟统计");
            Console.WriteLine("  replay --deck X --enc N --seed N --cmds \"play 4 1, end, ...\"   重放一局，输出完整日志");
            Console.WriteLine("  tune  [--n 150] [--top 5] [--confirm 500] [--grid \"erosion=3,4;U09.PerPoint=2,3\"]   网格搜索最优参数");
            Console.WriteLine("所有模式都支持 --set \"erosion=3;lunacy=5;hp=1.15;U09.PerPoint=3\" 临时覆盖参数；batch / auto 支持 --style sane|mad|mixed");
            Console.WriteLine("  map   [--seed N] | [--n 1000]   预览一张地图 / 批量生成统计房间分布和约束违规");
            Console.WriteLine("  run   [--seed N] [--load run_save.json] [--save run_save.json] [--arch mixed]   文字版完整一局（进入节点时自动存档）");
            Console.WriteLine("  runreplay --seed N --cmds-file run_commands.txt   重放整局");
            Console.WriteLine("  runauto [--seed N] [--arch mixed] [--log boss|elite|all|none]   AI 打一局，打印每个节点的选择和战斗日志");
            Console.WriteLine("  runview --seed N --cmds-file playtest.txt [--from K] [--deck 1] [--map 1]   重放命令文件并显示当前界面（逐步试玩用）");
            Console.WriteLine("  runbatch [--n 200] [--arch watch,fall,contagion,mixed] [--cards 1]   批量跑整局，输出生命账本、各项指标和拾取率");
            Console.WriteLine("遭遇：1 = 教团侍僧，2 = 侍僧 + 呓语者，3 = 失魂镇民 ×2，或遭遇表里的 ID");
        }

        static Dictionary<string, string> ParseOptions(string[] args)
        {
            var d = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i++)
                if (args[i].StartsWith("--") && i + 1 < args.Length) d[args[i].Substring(2)] = args[++i];
            return d;
        }

        static string Opt(Dictionary<string, string> o, string key, string fallback) => o.TryGetValue(key, out var v) ? v : fallback;

        static BattleSetup MakeSetup(string deck, string enc, ulong seed)
        {
            Tuning.ApplyHpFor(enc);
            return Matchups.Setup(deck, enc, seed);
        }

        static ulong SeedOpt(Dictionary<string, string> o) =>
            o.TryGetValue("seed", out var s) ? ulong.Parse(s) : (ulong)DateTime.Now.Ticks % 1_000_000;

        // ------------------------------------------------------------ play

        static int Play(Dictionary<string, string> o)
        {
            string deck = Opt(o, "deck", "C"), enc = Opt(o, "enc", "1");
            ulong seed = SeedOpt(o);
            var e = BattleEngine.Start(MakeSetup(deck, enc, seed));
            var ai = new Autoplayer(Autoplayer.StyleFor(deck));
            Console.WriteLine($"牌组 {TestDecks.Describe(deck)}，遭遇 {enc}，种子 {seed}");
            Console.WriteLine(ConsoleView.InputHelp + " | a 让 AI 打这一回合 | l 看完整日志 | q 退出");
            int logCursor = 0;
            logCursor = Flush(e, logCursor);

            while (e.State.Outcome == BattleOutcome.Ongoing)
            {
                Console.Write(ConsoleView.Render(e));
                Console.Write("> ");
                string line = Console.ReadLine();
                if (line == null) return 0;
                line = line.Trim().TrimStart('﻿');   // 管道输入可能带 BOM
                if (line == "q") return 0;
                if (line == "") continue;

                if (line == "l")
                {
                    foreach (var ev in e.Log.Events.Where(x => !x.IsStateChange)) Console.WriteLine("  " + ev.Message);
                    continue;
                }
                if (line == "a")
                {
                    ai.PlayTurn(e);
                    logCursor = Flush(e, logCursor);
                    continue;
                }

                if (!ConsoleView.TryParseCommand(e, line, Console.ReadLine, out var cmd, out var parseError))
                {
                    Console.WriteLine(parseError);
                    continue;
                }

                if (!e.Execute(cmd, out var error)) Console.WriteLine($"不行：{error}");
                logCursor = Flush(e, logCursor);
            }

            PrintSummary(e);
            Console.WriteLine("重放这一局：");
            Console.WriteLine($"  dotnet run --project Deeptide.Sim -- replay --deck {deck} --enc {enc} --seed {seed} --cmds \"{string.Join(", ", e.History)}\"");
            return 0;
        }

        static int Flush(BattleEngine e, int cursor)
        {
            for (; cursor < e.Log.Events.Count; cursor++)
            {
                var ev = e.Log.Events[cursor];
                if (ev.Type == BattleEventType.IntentChosen || ev.IsStateChange) continue;
                Console.WriteLine("  · " + ev.Message);
            }
            return cursor;
        }

        static void PrintSummary(BattleEngine e)
        {
            var s = e.State.Stats;
            Console.WriteLine();
            Console.WriteLine(e.State.Outcome == BattleOutcome.Victory ? "【胜利】" : "【失败】");
            Console.WriteLine($"回合数 {e.State.Turn}，剩余生命 {e.Player.Hp}，最低理智 {s.MinSanity}，" +
                              $"首次癫狂 {(s.FirstLunacyTurn == 0 ? "无" : "第 " + s.FirstLunacyTurn + " 回合")}，" +
                              $"崩溃 {s.Collapses} 次，清明回合 {s.ClarityTurns}");
            var top = s.PlaysByCard.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
                .Select(kv => $"{CardLibrary.Get(kv.Key).Name} ×{kv.Value}");
            Console.WriteLine($"出牌 {s.CardsPlayed} 张：{string.Join("、", top)}");
        }

        // ------------------------------------------------------------ replay

        static int Replay(Dictionary<string, string> o)
        {
            string deck = Opt(o, "deck", "C"), enc = Opt(o, "enc", "1");
            if (!o.TryGetValue("seed", out var seedText)) throw new ArgumentException("replay 需要 --seed");
            string cmdText = Opt(o, "cmds", "");
            int fromTurn = int.Parse(Opt(o, "from-turn", "1"));
            var commands = cmdText.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(BattleCommand.Parse).ToList();
            ulong seed = ulong.Parse(seedText);

            var e = BattleEngine.Replay(MakeSetup(deck, enc, seed), commands);
            Console.WriteLine($"牌组 {TestDecks.Describe(deck)}，遭遇 {enc}，种子 {seed}，共 {commands.Count} 条命令");
            foreach (var ev in e.Log.Events)
                if (ev.Type != BattleEventType.IntentChosen && !ev.IsStateChange && ev.Turn >= fromTurn) Console.WriteLine("  · " + ev.Message);
            // 战斗没打完：显示当前局面，可以接着往 --cmds 里追加命令（逐步对战 / 复盘用）
            if (e.State.Outcome == BattleOutcome.Ongoing) Console.Write(ConsoleView.Render(e));
            else PrintSummary(e);
            return 0;
        }

        // ------------------------------------------------------------ auto

        static int Auto(Dictionary<string, string> o)
        {
            string deck = Opt(o, "deck", "C"), enc = Opt(o, "enc", "1");
            ulong seed = SeedOpt(o);
            var e = BattleEngine.Start(MakeSetup(deck, enc, seed));
            var ai = new Autoplayer(StyleOpt(o, deck));
            Console.WriteLine($"牌组 {TestDecks.Describe(deck)}，遭遇 {enc}，种子 {seed}");
            while (e.State.Outcome == BattleOutcome.Ongoing && e.State.Turn <= 40) ai.PlayTurn(e);
            foreach (var ev in e.Log.Events)
                if (ev.Type != BattleEventType.IntentChosen && !ev.IsStateChange) Console.WriteLine("  · " + ev.Message);
            PrintSummary(e);
            return 0;
        }

        // ------------------------------------------------------------ batch

        static PlayStyle StyleOpt(Dictionary<string, string> o, string deck)
        {
            if (!o.TryGetValue("style", out var s)) return Autoplayer.StyleFor(deck);
            switch (s)
            {
                case "sane": return PlayStyle.Sane;
                case "mad": return PlayStyle.Mad;
                case "mixed": return PlayStyle.Mixed;
                default: throw new ArgumentException($"未知 AI 风格：{s}（可选 sane / mad / mixed）");
            }
        }

        static int Batch(Dictionary<string, string> o)
        {
            int n = int.Parse(Opt(o, "n", "200"));
            string deckOpt = Opt(o, "deck", "all"), encOpt = Opt(o, "enc", "all");
            var decks = deckOpt == "all" ? new[] { "A", "B", "C", "D" } : deckOpt.Split(',');
            var encs = encOpt == "all" ? new[] { "1", "2", "3" }
                : encOpt == "pools" ? EncounterLibrary.All.Select(x => x.Id).ToArray()
                : encOpt.StartsWith("pool:") ? EncounterLibrary.InPool(Enum.Parse<EncounterPool>(encOpt.Substring(5), true)).Select(x => x.Id).ToArray()
                : encOpt.Split(',');

            Console.WriteLine($"每组 {n} 场，种子 1..{n}，AI = 一步贪心（只适合做相对比较）");
            if (o.TryGetValue("set", out var spec)) Console.WriteLine($"参数覆盖：{spec}");
            Console.WriteLine();
            Console.WriteLine("牌组      遭遇  胜率    平均回合  平均掉血  崩溃/场  最低理智  进入癫狂  首次癫狂  癫狂回合占比  清明回合占比");
            foreach (var deck in decks)
            foreach (var enc in encs)
            {
                var m = Matchups.Run(deck, enc, n, StyleOpt(o, deck));
                Console.WriteLine(
                    $"{TestDecks.Describe(deck),-8}  {enc,3}  {m.WinRate,6:P0}  {m.Turns,8:F1}  {m.HpLost,8:F1}  " +
                    $"{m.Collapses,7:F2}  {m.MinSanity,8:F1}  {m.LunacyFightShare,8:P0}  " +
                    $"{(m.FirstLunacy > 0 ? m.FirstLunacy.ToString("F1") : "-"),8}  {m.LunacyTurnShare,12:P0}  {m.ClarityTurnShare,12:P0}");
            }
            return 0;
        }

        // ------------------------------------------------------------ tune

        /// <summary>默认搜索网格；也可以用 --grid "erosion=3,4;U09.PerPoint=2,3" 自己指定。</summary>
        static readonly Dictionary<string, string[]> DefaultGrid = new Dictionary<string, string[]>
        {
            ["erosion"] = new[] { "3", "4" },
            ["lunacy"] = new[] { "4", "5" },
            ["hpHard"] = new[] { "1.0", "1.15", "1.3" },
            ["starter"] = new[] { "base", "prayer" },
            ["U09.PerPoint"] = new[] { "2", "3" },
            ["R05.Mult"] = new[] { "2", "3" },
        };

        static int Tune(Dictionary<string, string> o)
        {
            int n = int.Parse(Opt(o, "n", "150"));
            int top = int.Parse(Opt(o, "top", "5"));
            int confirmN = int.Parse(Opt(o, "confirm", "500"));
            var axes = o.TryGetValue("grid", out var g)
                ? g.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Split('='))
                    .ToDictionary(a => a[0].Trim(), a => a[1].Split(',').Select(v => v.Trim()).ToArray())
                : DefaultGrid;
            var specs = Tuner.Grid(axes).ToList();
            Console.WriteLine($"网格 {specs.Count} 组参数 × 13 个对局组合 × {n} 场；前 {top} 名再用 {confirmN} 场复核");

            var results = new List<Tuner.Evaluation>();
            foreach (var spec in specs)
            {
                Tuning.Apply(spec);
                var ev = Tuner.Evaluate(spec, n);
                results.Add(ev);
                Console.WriteLine($"  {ev.Score,6:F2}  {spec}");
            }

            Console.WriteLine();
            Console.WriteLine($"前 {top} 名复核（{confirmN} 场）：");
            var confirmed = new List<Tuner.Evaluation>();
            foreach (var ev in results.OrderBy(r => r.Score).Take(top))
            {
                Tuning.Apply(ev.Spec);
                var c = Tuner.Evaluate(ev.Spec, confirmN);
                confirmed.Add(c);
                Console.WriteLine($"  {c.Spec}");
                Console.WriteLine($"    {Tuner.Summary(c)}");
            }

            var best = confirmed.OrderBy(r => r.Score).First();
            Console.WriteLine();
            Console.WriteLine($"最优：{best.Spec}（得分 {best.Score:F2}）");
            foreach (var kv in best.Penalties.Where(kv => kv.Value > 0.001).OrderByDescending(kv => kv.Value))
                Console.WriteLine($"  扣分 {kv.Key}：{kv.Value:F2}");
            Console.WriteLine();
            Console.WriteLine("牌组      遭遇  胜率    平均回合  平均掉血  崩溃/场  进入癫狂  癫狂回合占比  清明回合占比");
            foreach (var m in best.Main.Concat(best.MixedSane).Concat(best.MixedMad))
                Console.WriteLine(
                    $"{TestDecks.Describe(m.Deck) + (m.Style == Autoplayer.StyleFor(m.Deck) ? "" : "/" + m.Style),-12}  {m.Enc,3}  {m.WinRate,6:P0}  {m.Turns,8:F1}  {m.HpLost,8:F1}  " +
                    $"{m.Collapses,7:F2}  {m.LunacyFightShare,8:P0}  {m.LunacyTurnShare,12:P0}  {m.ClarityTurnShare,12:P0}");
            return 0;
        }

        static string Pct(double part, double whole) => whole <= 0 ? "-" : (part / whole).ToString("P0");
    }
}
