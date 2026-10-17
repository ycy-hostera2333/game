using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Content;
using Deeptide.Core.Enemies;

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
            try
            {
                switch (mode)
                {
                    case "play": return Play(opts);
                    case "auto": return Auto(opts);
                    case "batch": return Batch(opts);
                    case "replay": return Replay(opts);
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
            Console.WriteLine("  batch [--n 200] [--deck all|A|B|C|starter] [--enc all|1|2|3]   批量模拟统计");
            Console.WriteLine("  replay --deck X --enc N --seed N --cmds \"play 4 1, end, ...\"   重放一局，输出完整日志");
            Console.WriteLine("遭遇：1 = 教团侍僧，2 = 侍僧 + 呓语者，3 = 失魂镇民 ×2");
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
            if (!Encounters.Prototype.TryGetValue(enc, out var enemies)) throw new ArgumentException($"未知遭遇：{enc}（可选 1 / 2 / 3）");
            return new BattleSetup { Seed = seed, Deck = TestDecks.Get(deck), Enemies = enemies.ToList() };
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
            Console.WriteLine("输入：牌号 [目标]（如「2」或「2 1」）出牌 | e 结束回合 | a 让 AI 打这一回合 | l 看完整日志 | q 退出");
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
                    foreach (var ev in e.Log.Events) Console.WriteLine("  " + ev.Message);
                    continue;
                }
                if (line == "a")
                {
                    ai.PlayTurn(e);
                    logCursor = Flush(e, logCursor);
                    continue;
                }

                BattleCommand cmd;
                if (line == "e") cmd = BattleCommand.End();
                else
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (!int.TryParse(parts[0], out int n)) { Console.WriteLine("看不懂这条输入"); continue; }
                    int target = parts.Length > 1 && int.TryParse(parts[1], out int t) ? t : -1;
                    if (target < 0 && n >= 1 && n <= e.Player.Hand.Count && e.Player.Hand[n - 1].Def.Target == CardTarget.Enemy)
                    {
                        var alive = e.State.Enemies.Where(x => !x.IsDead).ToList();
                        if (alive.Count == 1) target = alive[0].Index;
                        else
                        {
                            Console.Write($"目标？（{string.Join(" / ", alive.Select(x => $"{x.Index}={x.Name}"))}）> ");
                            if (!int.TryParse(Console.ReadLine(), out target)) continue;
                        }
                    }
                    cmd = BattleCommand.Play(n - 1, target);
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
                if (ev.Type == BattleEventType.IntentChosen) continue;
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
            if (!o.TryGetValue("cmds", out var cmdText)) throw new ArgumentException("replay 需要 --cmds，内容就是对战结束时打印的回放命令");
            var commands = cmdText.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(BattleCommand.Parse).ToList();
            ulong seed = ulong.Parse(seedText);

            var e = BattleEngine.Replay(MakeSetup(deck, enc, seed), commands);
            Console.WriteLine($"牌组 {TestDecks.Describe(deck)}，遭遇 {enc}，种子 {seed}，共 {commands.Count} 条命令");
            foreach (var ev in e.Log.Events)
                if (ev.Type != BattleEventType.IntentChosen) Console.WriteLine("  · " + ev.Message);
            if (e.State.Outcome == BattleOutcome.Ongoing) Console.WriteLine("（回放结束时战斗还没打完）");
            else PrintSummary(e);
            return 0;
        }

        // ------------------------------------------------------------ auto

        static int Auto(Dictionary<string, string> o)
        {
            string deck = Opt(o, "deck", "C"), enc = Opt(o, "enc", "1");
            ulong seed = SeedOpt(o);
            var e = BattleEngine.Start(MakeSetup(deck, enc, seed));
            var ai = new Autoplayer(Autoplayer.StyleFor(deck));
            Console.WriteLine($"牌组 {TestDecks.Describe(deck)}，遭遇 {enc}，种子 {seed}");
            while (e.State.Outcome == BattleOutcome.Ongoing && e.State.Turn <= 40) ai.PlayTurn(e);
            foreach (var ev in e.Log.Events)
                if (ev.Type != BattleEventType.IntentChosen) Console.WriteLine("  · " + ev.Message);
            PrintSummary(e);
            return 0;
        }

        // ------------------------------------------------------------ batch

        sealed class Result
        {
            public bool Won;
            public int Turns, HpLost, Collapses, MinSanity, FirstLunacy, LunacyTurns, ClarityTurns;
        }

        static int Batch(Dictionary<string, string> o)
        {
            int n = int.Parse(Opt(o, "n", "200"));
            string deckOpt = Opt(o, "deck", "all"), encOpt = Opt(o, "enc", "all");
            var decks = deckOpt == "all" ? new[] { "A", "B", "C" } : new[] { deckOpt };
            var encs = encOpt == "all" ? new[] { "1", "2", "3" } : new[] { encOpt };

            Console.WriteLine($"每组 {n} 场，种子 1..{n}，AI = 一步贪心（只适合做相对比较）");
            Console.WriteLine();
            Console.WriteLine("牌组      遭遇  胜率    平均回合  平均掉血  崩溃/场  最低理智  进入癫狂  首次癫狂  癫狂回合占比  清明回合占比");
            foreach (var deck in decks)
            foreach (var enc in encs)
            {
                var results = new Result[n];
                Parallel.For(0, n, i =>
                {
                    var e = BattleEngine.Start(MakeSetup(deck, enc, (ulong)(i + 1)));
                    var ai = new Autoplayer(Autoplayer.StyleFor(deck));
                    while (e.State.Outcome == BattleOutcome.Ongoing && e.State.Turn <= 40) ai.PlayTurn(e);
                    var s = e.State.Stats;
                    results[i] = new Result
                    {
                        Won = e.State.Outcome == BattleOutcome.Victory,
                        Turns = e.State.Turn,
                        HpLost = e.Setup.Hp - e.Player.Hp,
                        Collapses = s.Collapses,
                        MinSanity = s.MinSanity,
                        FirstLunacy = s.FirstLunacyTurn,
                        LunacyTurns = s.LunacyTurns,
                        ClarityTurns = s.ClarityTurns,
                    };
                });

                var lunatic = results.Where(r => r.FirstLunacy > 0).ToList();
                double turns = results.Sum(r => r.Turns);
                Console.WriteLine(
                    $"{TestDecks.Describe(deck),-8}  {enc,3}  {Pct(results.Count(r => r.Won), n),6}  " +
                    $"{results.Average(r => r.Turns),8:F1}  {results.Average(r => r.HpLost),8:F1}  " +
                    $"{results.Average(r => r.Collapses),7:F2}  {results.Average(r => r.MinSanity),8:F1}  " +
                    $"{Pct(lunatic.Count, n),8}  {(lunatic.Count > 0 ? lunatic.Average(r => r.FirstLunacy).ToString("F1") : "-"),8}  " +
                    $"{Pct(results.Sum(r => r.LunacyTurns), turns),12}  {Pct(results.Sum(r => r.ClarityTurns), turns),12}");
            }
            return 0;
        }

        static string Pct(double part, double whole) => whole <= 0 ? "-" : (part / whole).ToString("P0");
    }
}
