using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Map;
using Deeptide.Core.Potions;
using Deeptide.Core.Random;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;
using Deeptide.Tools;

namespace Deeptide.Sim
{
    /// <summary>跑局相关的控制台模式：map（地图预览 / 统计）、run（文字版完整一局）、runreplay、runbatch（批量跑整局）。</summary>
    static class RunModes
    {
        static string Opt(Dictionary<string, string> o, string key, string fallback) => o.TryGetValue(key, out var v) ? v : fallback;

        static RunConfig Config()
        {
            Tuning.ApplyHpForRuns();
            return new RunConfig { Battle = Tuning.Config };
        }

        // ------------------------------------------------------------ map

        public static int Map(Dictionary<string, string> o)
        {
            int n = int.Parse(Opt(o, "n", "1"));
            if (n <= 1)
            {
                ulong seed = ulong.Parse(Opt(o, "seed", "1"));
                var map = MapGenerator.Generate(new Pcg32(seed, 11));
                Console.WriteLine($"种子 {seed}（和 run 模式同一种子的地图一样）");
                Console.Write(MapGenerator.Render(map));
                Console.WriteLine(MapGenerator.Legend);
                var v = MapGenerator.Violations(map);
                Console.WriteLine(v.Count == 0 ? "约束检查：全部满足" : "违规：\n  " + string.Join("\n  ", v));
                return 0;
            }

            var counts = new Dictionary<RoomType, long>();
            long weighted = 0, violations = 0, siblings = 0, fallbacks = 0, nodes = 0, maxElites = 0, minElites = 0;
            var examples = new List<string>();
            for (int i = 1; i <= n; i++)
            {
                var map = MapGenerator.Generate(new Pcg32((ulong)i, 11));
                nodes += map.Nodes.Count;
                foreach (var node in map.Nodes.Where(x => x.Floor != 1 && x.Floor != 9 && x.Floor != map.Height))
                {
                    counts[node.Room] = counts.TryGetValue(node.Room, out var c) ? c + 1 : 1;
                    weighted++;
                }
                var v = MapGenerator.Violations(map);
                violations += v.Count;
                if (v.Count > 0 && examples.Count < 5) examples.Add($"种子 {i}：{v[0]}");
                siblings += MapGenerator.SiblingDuplicates(map);
                fallbacks += map.Fallbacks;
                maxElites += map.InRow(0).Max(x => CountOnPath(map, x, RoomType.Elite, Math.Max));
                minElites += map.InRow(0).Min(x => CountOnPath(map, x, RoomType.Elite, Math.Min));
            }
            Console.WriteLine($"每张图上一条路线最多能打 {maxElites / (double)n:F2} 个精英，最少 {minElites / (double)n:F2} 个（目标每局 1–3 场）");
            Console.WriteLine($"{n} 张地图：平均 {nodes / (double)n:F1} 个节点，硬约束违规 {violations} 次，抽不到合适房间退回普通战斗 {fallbacks} 次，兄弟节点同类型 {siblings} 对");
            foreach (var e in examples) Console.WriteLine("  " + e);
            Console.WriteLine("非固定层的房间占比（权重：战斗 53 / 事件 22 / 篝火 12 / 精英 8 / 商店 5；第 2–5 层不出精英和篝火、第 14 层不出篝火、不和父节点同类，所以实际会偏离）：");
            foreach (var room in new[] { RoomType.Battle, RoomType.Event, RoomType.Campfire, RoomType.Elite, RoomType.Shop })
                Console.WriteLine($"  {RunEngine.RoomName(room),-6} {(counts.TryGetValue(room, out var x) ? x : 0) / (double)weighted:P1}");
            return violations == 0 ? 0 : 1;
        }

        /// <summary>从 node 出发到顶层的路线上，房间类型 room 出现次数的最大 / 最小值。</summary>
        static int CountOnPath(MapData map, MapNode node, RoomType room, Func<int, int, int> pick)
        {
            int self = node.Room == room ? 1 : 0;
            if (node.Children.Count == 0) return self;
            int best = CountOnPath(map, map.Get(node.Row + 1, node.Children[0]), room, pick);
            foreach (var c in node.Children.Skip(1)) best = pick(best, CountOnPath(map, map.Get(node.Row + 1, c), room, pick));
            return self + best;
        }

        // ------------------------------------------------------------ run（交互）

        public static int Run(Dictionary<string, string> o)
        {
            string savePath = Opt(o, "save", "run_save.json");
            RunEngine run;
            ulong seed;
            if (o.TryGetValue("load", out var load))
            {
                run = RunSaves.Read(load, Config());
                seed = run.State.Seed;
                Console.WriteLine($"读取存档 {load}：种子 {seed}，第 {run.State.Floor} 层");
            }
            else
            {
                seed = o.TryGetValue("seed", out var s) ? ulong.Parse(s) : (ulong)DateTime.Now.Ticks % 1_000_000;
                run = RunEngine.NewRun(seed, Config());
                Console.WriteLine($"新的一局，种子 {seed}");
            }
            var ai = new RunAutoplayer(RunAutoplayer.ParseArchetype(Opt(o, "arch", "mixed")));
            Console.WriteLine("地图 / 奖励 / 商店 / 篝火 / 事件：输入选项编号；d栏位 丢药水；deck 看牌组；map 看地图；a 让 AI 走一步；A 让 AI 打完当前战斗或节点；q 存档退出");
            Console.WriteLine("战斗中：" + ConsoleView.InputHelp);
            int saved = -1, logCursor = 0;
            BattleEngine shownBattle = null;

            while (!run.IsOver)
            {
                if (run.SavePoints != saved)
                {
                    RunSaves.Write(savePath, run);
                    saved = run.SavePoints;
                }
                if (run.Battle != shownBattle)
                {
                    shownBattle = run.Battle;
                    logCursor = 0;
                }
                if (run.Battle != null) logCursor = FlushBattle(run.Battle, logCursor);

                Console.WriteLine();
                Console.WriteLine(StatusLine(run));
                if (run.State.Phase == RunPhase.Battle) Console.Write(ConsoleView.Render(run.Battle));
                else
                {
                    Console.WriteLine($"【{run.Title}】");
                    if (run.Description != null) Console.WriteLine(run.Description);
                    var options = run.Options();
                    for (int i = 0; i < options.Count; i++)
                        Console.WriteLine($" {(options[i].Enabled ? " " : "×")}{i + 1}) {options[i].Label}{(options[i].Enabled ? "" : $"（{options[i].Reason}）")}");
                }
                Console.Write("> ");
                string line = Console.ReadLine();
                if (line == null || line.Trim() == "q")
                {
                    Console.WriteLine($"已存档到 {savePath}（读档会回到最近的存档点：进入这个节点时）");
                    return 0;
                }
                line = line.Trim().TrimStart('﻿');
                if (line == "") continue;
                if (line == "deck") { Console.WriteLine(DeckText(run.State)); continue; }
                if (line == "map")
                {
                    Console.Write(MapGenerator.Render(run.State.Map, run.State.Row, run.State.Col));
                    Console.WriteLine(MapGenerator.Legend);
                    continue;
                }
                if (line == "a" || line == "A")
                {
                    int node = run.SavePoints;
                    do
                    {
                        var c = ai.Choose(run);
                        if (!run.Execute(c, out var err)) { Console.WriteLine($"AI 出错：{err}"); break; }
                        if (run.Battle != null) logCursor = FlushBattle(run.Battle, logCursor);
                    } while (line == "A" && !run.IsOver && run.SavePoints == node && run.State.Phase == RunPhase.Battle);
                    continue;
                }

                RunCommand cmd;
                if (run.State.Phase == RunPhase.Battle)
                {
                    if (!ConsoleView.TryParseCommand(run.Battle, line, Console.ReadLine, out var bc, out var perr)) { Console.WriteLine(perr); continue; }
                    cmd = RunCommand.Fight(bc);
                }
                else if (line.StartsWith("d") && int.TryParse(line.Substring(1), out int slot)) cmd = RunCommand.DropPotion(slot - 1);
                else if (int.TryParse(line, out int pick)) cmd = RunCommand.Pick(pick - 1);
                else { Console.WriteLine("看不懂这条输入"); continue; }

                var before = run.Battle;
                if (!run.Execute(cmd, out var error)) Console.WriteLine($"不行：{error}");
                else if (before != null && run.Battle == null) FlushBattle(before, logCursor);
            }

            Console.WriteLine();
            Console.WriteLine(run.State.Phase == RunPhase.Victory ? "【通关】" : "【失败】");
            Console.WriteLine(Summary(run));
            if (File.Exists(savePath)) File.Delete(savePath);
            string log = Opt(o, "log", "run_commands.txt");
            File.WriteAllText(log, string.Join("\n", run.History));
            Console.WriteLine($"这一局的命令已写入 {log}，可以用 runreplay --seed {seed} --cmds-file {log} 重放");
            return 0;
        }

        static int FlushBattle(BattleEngine e, int cursor)
        {
            for (; cursor < e.Log.Events.Count; cursor++)
            {
                var ev = e.Log.Events[cursor];
                if (ev.Type == BattleEventType.IntentChosen || ev.IsStateChange) continue;
                Console.WriteLine("  · " + ev.Message);
            }
            return cursor;
        }

        static string StatusLine(RunEngine run)
        {
            var s = run.State;
            // 战斗中生命和药水以战斗里的为准（局层状态要等战斗结束才同步）
            var potionIds = run.Battle != null ? run.Battle.Player.Potions.Select(p => p?.Id).ToList() : s.Potions;
            var potions = potionIds.Select(p => p == null ? "空" : PotionLibrary.Get(p).Name).ToList();
            int hp = run.Battle?.Player.Hp ?? s.Hp;
            var relics = s.Relics.Select(r => RelicLibrary.Get(r).Name);
            return $"── 第 {s.Floor}/{s.Map.BossFloor} 层  生命 {hp}/{s.MaxHp}  理智上限 {s.MaxSanity}  金币 {s.Gold}  牌组 {s.Deck.Count} 张  " +
                   $"药水 [{string.Join(" | ", potions)}]  遗物：{string.Join("、", relics)}";
        }

        static string DeckText(RunState s) =>
            string.Join("\n", s.Deck.GroupBy(x => x).OrderBy(g => g.Key).Select(g => $"  {g.Count()} × {RunEngine.CardLabel(g.Key)}"));

        static string Summary(RunEngine run)
        {
            var s = run.State;
            var sb = new StringBuilder();
            sb.AppendLine($"到达第 {s.Floor} 层，剩余生命 {s.Hp}/{s.MaxHp}，打了 {s.Stats.Battles.Count} 场战斗，到达 Boss 时生命 {(s.Stats.HpAtBoss < 0 ? "-" : s.Stats.HpAtBoss.ToString())}");
            if (s.Stats.DeathEncounter != null) sb.AppendLine($"死于 {Deeptide.Core.Enemies.EncounterLibrary.Get(s.Stats.DeathEncounter).Name}");
            sb.AppendLine($"最终牌组 {s.Deck.Count} 张：{string.Join("、", s.Deck.Select(x => CardLibrary.Create(x, 0).Name))}");
            sb.Append($"遗物：{string.Join("、", s.Relics.Select(r => RelicLibrary.Get(r).Name))}");
            return sb.ToString();
        }

        // ------------------------------------------------------------ runauto

        /// <summary>AI 打一局，逐个节点打印经过；--log boss|all|none 控制打印哪些战斗的完整日志。</summary>
        public static int RunAuto(Dictionary<string, string> o)
        {
            ulong seed = ulong.Parse(Opt(o, "seed", "1"));
            var arch = RunAutoplayer.ParseArchetype(Opt(o, "arch", "mixed"));
            string logMode = Opt(o, "log", "boss");
            var run = RunEngine.NewRun(seed, Config());
            var ai = new RunAutoplayer(arch);
            RunAutoplayer.Debug = Opt(o, "debug", "0") == "1";
            Console.WriteLine($"种子 {seed}，AI 流派 {RunAutoplayer.ArchetypeName(arch)}");
            int lastBattles = 0, guard = 0;
            while (!run.IsOver && guard++ < 20000)
            {
                var phase = run.State.Phase;
                string title = run.Title;
                var options = run.Options();
                var cmd = ai.Choose(run);
                var battle = run.Battle;
                int hpBefore = run.State.Hp;
                if (!run.Execute(cmd, out var error)) throw new InvalidOperationException($"{cmd}：{error}");
                if (phase != RunPhase.Battle && cmd.Kind == RunCommandKind.Pick)
                    Console.WriteLine($"[第 {run.State.Floor,2} 层 生命 {hpBefore,2}] {title} → {options[cmd.Index].Label}");
                if (battle != null && run.Battle == null)
                {
                    var rec = run.State.Stats.Battles.Last();
                    Console.WriteLine($"    战斗结束：{(rec.Won ? "胜" : "败")}，{rec.Turns} 回合，掉血 {rec.HpLost}，最低理智 {rec.MinSanity}，崩溃 {rec.Collapses}");
                    bool show = logMode == "all" || (logMode == "boss" && rec.Room == RoomType.Boss) || (logMode == "elite" && rec.Room != RoomType.Battle);
                    if (show)
                        foreach (var ev in battle.Log.Events)
                            if (!ev.IsStateChange && ev.Type != BattleEventType.IntentChosen) Console.WriteLine("      · " + ev.Message);
                }
                lastBattles = run.State.Stats.Battles.Count;
            }
            Console.WriteLine();
            Console.WriteLine(run.State.Phase == RunPhase.Victory ? "【通关】" : "【失败】");
            Console.WriteLine(Summary(run));
            return 0;
        }

        // ------------------------------------------------------------ runview（一步步试玩用）

        /// <summary>
        /// 重放命令文件，打印 --from 之后发生的事情和当前界面（玩家能看到的信息，和 run 模式一样）。
        /// 试玩时往命令文件里追加命令，再运行一次就能接着打；不用开着一个交互进程。
        /// </summary>
        public static int RunView(Dictionary<string, string> o)
        {
            ulong seed = ulong.Parse(Opt(o, "seed", "1"));
            string file = Opt(o, "cmds-file", "playtest.txt");
            var cmds = File.Exists(file)
                ? File.ReadAllLines(file).Select(x => x.Split('#')[0].Trim()).Where(x => x.Length > 0).Select(RunCommand.Parse).ToList()
                : new List<RunCommand>();
            int from = int.Parse(Opt(o, "from", cmds.Count.ToString()));
            var run = RunEngine.NewRun(seed, Config());
            BattleEngine watched = null;
            int watchedFrom = 0;
            var lines = new List<string>();
            for (int i = 0; i < cmds.Count; i++)
            {
                if (i == from)
                {
                    watched = run.Battle;
                    watchedFrom = watched?.Log.Events.Count ?? 0;
                }
                var before = run.Battle;
                string title = run.Title;
                var options = run.State.Phase == RunPhase.Battle ? null : run.Options();
                if (!run.Execute(cmds[i], out var error))
                {
                    Console.WriteLine($"第 {i + 1} 条命令 {cmds[i]} 不合法：{error}");
                    break;
                }
                if (i >= from)
                {
                    if (options != null && cmds[i].Kind == RunCommandKind.Pick) lines.Add($"[{title}] → {options[cmds[i].Index].Label}");
                    if (before != null && before != watched) { watched = before; watchedFrom = 0; }
                    if (before != null && run.Battle != before)
                    {
                        lines.AddRange(Narrative(before, watchedFrom));
                        var rec = run.State.Stats.Battles.Last();
                        lines.Add($"战斗结束：{(rec.Won ? "胜" : "败")}，{rec.Turns} 回合，掉血 {rec.HpLost}");
                        watched = null;
                    }
                }
            }
            if (run.Battle != null)
            {
                if (watched != run.Battle) watchedFrom = run.Battle.Log.Events.Count > 0 && from >= cmds.Count ? 0 : 0;
                lines.AddRange(Narrative(run.Battle, watched == run.Battle ? watchedFrom : 0));
            }
            foreach (var l in lines) Console.WriteLine(l);
            Console.WriteLine();
            Console.WriteLine($"（已执行 {cmds.Count} 条命令）");
            Console.WriteLine(StatusLine(run));
            if (o.TryGetValue("deck", out var d) && d == "1") Console.WriteLine(DeckText(run.State));
            if (o.TryGetValue("map", out var m) && m == "1")
            {
                Console.Write(MapGenerator.Render(run.State.Map, run.State.Row, run.State.Col));
                Console.WriteLine(MapGenerator.Legend);
            }
            if (run.IsOver)
            {
                Console.WriteLine(run.State.Phase == RunPhase.Victory ? "【通关】" : "【失败】");
                Console.WriteLine(Summary(run));
            }
            else if (run.State.Phase == RunPhase.Battle) Console.Write(ConsoleView.Render(run.Battle));
            else
            {
                Console.WriteLine($"【{run.Title}】");
                if (run.Description != null) Console.WriteLine(run.Description);
                var options = run.Options();
                for (int i = 0; i < options.Count; i++)
                    Console.WriteLine($" {(options[i].Enabled ? " " : "×")}pick {i}) {options[i].Label}{(options[i].Enabled ? "" : $"（{options[i].Reason}）")}");
            }
            return 0;
        }

        static IEnumerable<string> Narrative(BattleEngine e, int from) =>
            e.Log.Events.Skip(from).Where(ev => !ev.IsStateChange && ev.Type != BattleEventType.IntentChosen).Select(ev => "  · " + ev.Message);

        // ------------------------------------------------------------ runreplay

        public static int RunReplay(Dictionary<string, string> o)
        {
            if (!o.TryGetValue("seed", out var seedText)) throw new ArgumentException("runreplay 需要 --seed");
            string text = o.TryGetValue("cmds-file", out var file) ? File.ReadAllText(file) : Opt(o, "cmds", "");
            var cmds = text.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0)
                .Select(RunCommand.Parse).ToList();
            var run = RunEngine.Replay(ulong.Parse(seedText), cmds, Config());
            Console.WriteLine($"重放 {cmds.Count} 条命令：{run.State.Phase}，哈希 {run.Hash()}");
            Console.WriteLine(Summary(run));
            return 0;
        }

        // ------------------------------------------------------------ runbatch

        sealed class RunResult
        {
            public ulong Seed;
            public bool Won;
            public int Floor;
            public RunState State;
            public string Error;
        }

        /// <summary>按流派各跑 n 局（种子 1..n），输出 策划/07 §5.2 的生命账本、§5.3 的指标和每张牌的拾取率。</summary>
        public static int RunBatch(Dictionary<string, string> o)
        {
            int n = int.Parse(Opt(o, "n", "200"));
            var archs = Opt(o, "arch", "watch,fall,contagion,mixed").Split(',').Select(RunAutoplayer.ParseArchetype).ToList();
            bool verbose = Opt(o, "cards", "1") == "1";
            Console.WriteLine($"每个流派 {n} 局（种子 1..{n}），跑局 AI = 规则选牌 + 束搜索战斗（只适合做相对比较）");
            if (o.TryGetValue("set", out var spec)) Console.WriteLine($"参数覆盖：{spec}");
            Console.WriteLine();

            var all = new Dictionary<Archetype, List<RunResult>>();
            foreach (var arch in archs)
            {
                var results = new RunResult[n];
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Parallel.For(0, n, i =>
                {
                    ulong seed = (ulong)(i + 1);
                    var r = new RunResult { Seed = seed };
                    try
                    {
                        var run = RunEngine.NewRun(seed, Config());
                        new RunAutoplayer(arch).PlayRun(run);
                        r.Won = run.State.Phase == RunPhase.Victory;
                        r.Floor = run.State.Floor;
                        r.State = run.State;
                        if (!run.IsOver) r.Error = "命令数超过上限";
                    }
                    catch (Exception ex)
                    {
                        r.Error = ex.GetType().Name + "：" + ex.Message;
                    }
                    results[i] = r;
                });
                all[arch] = results.ToList();
                PrintArchetype(arch, results, sw.Elapsed);
            }

            if (archs.Count > 1)
            {
                var rates = all.Select(kv => kv.Value.Count(r => r.Won) / (double)n).ToList();
                Console.WriteLine($"三流派胜率差（最高 − 最低，含混合）：{(rates.Max() - rates.Min()) * 100:F1} 个百分点（目标 ≤ 10）");
                var pure = all.Where(kv => kv.Key != Archetype.Mixed).Select(kv => kv.Value.Count(r => r.Won) / (double)n).ToList();
                if (pure.Count > 1) Console.WriteLine($"守夜 / 堕落 / 传染三流派胜率差：{(pure.Max() - pure.Min()) * 100:F1} 个百分点");
            }
            if (verbose) PrintPickRates(all.Values.SelectMany(x => x).Where(r => r.State != null).ToList());
            return all.Values.SelectMany(x => x).Any(r => r.Error != null) ? 1 : 0;
        }

        static void PrintArchetype(Archetype arch, RunResult[] results, TimeSpan elapsed)
        {
            int n = results.Length;
            var ok = results.Where(r => r.State != null).ToList();
            var errors = results.Where(r => r.Error != null).ToList();
            Console.WriteLine($"══ {RunAutoplayer.ArchetypeName(arch)}（{elapsed.TotalSeconds:F0} 秒）");
            Console.WriteLine($"  胜率 {results.Count(r => r.Won) / (double)n:P1}  平均到达层数 {ok.Average(r => r.Floor):F1}  异常 {errors.Count} 局");
            foreach (var e in errors.Take(5)) Console.WriteLine($"    种子 {e.Seed}：{e.Error}");
            if (ok.Count == 0) return;

            var battles = ok.SelectMany(r => r.State.Stats.Battles).ToList();
            var reachedBoss = ok.Where(r => r.State.Stats.HpAtBoss >= 0).ToList();
            Console.WriteLine($"  到达 Boss {reachedBoss.Count / (double)n:P0}，到达时生命中位数 {Median(reachedBoss.Select(r => r.State.Stats.HpAtBoss))}（目标 40–50）；Boss 战胜率 {(reachedBoss.Count == 0 ? 0 : results.Count(r => r.Won) / (double)reachedBoss.Count):P0}");
            foreach (var room in new[] { RoomType.Battle, RoomType.Elite, RoomType.Boss })
            {
                var bs = battles.Where(b => b.Room == room).ToList();
                if (bs.Count == 0) continue;
                Console.WriteLine($"  {RunEngine.RoomName(room),-4} 每局 {bs.Count / (double)ok.Count:F1} 场  回合 {bs.Average(b => b.Turns):F1}  掉血中位数 {Median(bs.Select(b => b.HpLost))}  平均 {bs.Average(b => b.HpLost):F1}  胜率 {bs.Count(b => b.Won) / (double)bs.Count:P0}" +
                                  (bs.Any(b => !b.Won) ? $"  输的时候敌人剩余生命 {bs.Where(b => !b.Won).Average(b => b.EnemyHpLeft):F0}" : ""));
            }
            var hard = battles.Where(b => b.Room == RoomType.Battle && b.EncounterId.StartsWith("hard")).ToList();
            var easy = battles.Where(b => b.Room == RoomType.Battle && b.EncounterId.StartsWith("easy")).ToList();
            if (easy.Count > 0 && hard.Count > 0)
                Console.WriteLine($"  其中简单池 回合 {easy.Average(b => b.Turns):F1} 掉血 {easy.Average(b => b.HpLost):F1}（目标 3–4 / 2–6），困难池 回合 {hard.Average(b => b.Turns):F1} 掉血 {hard.Average(b => b.HpLost):F1}（目标 4–6 / 4–9）");
            double lunacyShare = battles.Count(b => b.EnteredLunacy) / (double)battles.Count;
            double turns = battles.Sum(b => b.Turns);
            Console.WriteLine($"  进入过癫狂的战斗 {lunacyShare:P0}（目标 40–70%）  崩溃 {battles.Average(b => b.Collapses):F2} 次/场  清明回合占比 {battles.Sum(b => b.ClarityTurns) / turns:P0}  癫狂回合占比 {battles.Sum(b => b.LunacyTurns) / turns:P0}");
            Console.WriteLine($"  金币收入 {ok.Average(r => r.State.Stats.GoldEarned):F0}（目标 300–400，不含事件）  商店 {ok.Average(r => r.State.Stats.ShopVisits):F1} 次  删卡 {ok.Average(r => r.State.Stats.CardsRemoved):F1}  最终牌组 {ok.Average(r => r.State.Deck.Count):F1} 张（目标 18–24）  疯语 {ok.Average(r => r.State.Deck.Count(c => c == CardIds.Whispers)):F1} 张");
            Console.WriteLine($"  普通战斗 {ok.Average(r => r.State.Stats.Battles.Count(b => b.Room == RoomType.Battle)):F1} 场（目标 7–9） 精英 {ok.Average(r => r.State.Stats.Battles.Count(b => b.Room == RoomType.Elite)):F1} 场（目标 1–3）");
            var deaths = ok.Where(r => r.State.Stats.DeathEncounter != null).GroupBy(r => r.State.Stats.DeathEncounter)
                .OrderByDescending(g => g.Count()).Take(5).Select(g => $"{Deeptide.Core.Enemies.EncounterLibrary.Get(g.Key).Name} {g.Count()}");
            Console.WriteLine($"  死因：{string.Join("、", deaths)}");
            Console.WriteLine();
        }

        static void PrintPickRates(List<RunResult> runs)
        {
            var offered = new Dictionary<string, int>();
            var picked = new Dictionary<string, int>();
            foreach (var c in runs.SelectMany(r => r.State.Stats.CardChoices))
            {
                foreach (var id in c.Offered) offered[id] = offered.TryGetValue(id, out var x) ? x + 1 : 1;
                if (c.Picked != null) picked[c.Picked] = picked.TryGetValue(c.Picked, out var y) ? y + 1 : 1;
            }
            var rows = offered.Select(kv => (Id: kv.Key, Offered: kv.Value, Rate: (picked.TryGetValue(kv.Key, out var p) ? p : 0) / (double)kv.Value))
                .OrderBy(r => r.Id).ToList();
            Console.WriteLine("拾取率（被选中 / 出现在卡牌奖励里的次数；目标 10–60%，反映的是 AI 的打分规则）：");
            var sb = new StringBuilder();
            int col = 0;
            foreach (var r in rows)
            {
                string flag = r.Rate < 0.10 ? "↓" : r.Rate > 0.60 ? "↑" : " ";
                sb.Append($"  {CardLibrary.Get(r.Id).Name,-6}{r.Rate,5:P0}{flag}");
                if (++col % 6 == 0) sb.AppendLine();
            }
            Console.WriteLine(sb.ToString());
            Console.WriteLine($"  区间外：{rows.Count(r => r.Rate < 0.10 || r.Rate > 0.60)} / {rows.Count} 张");
        }

        static int Median(IEnumerable<int> values)
        {
            var list = values.OrderBy(x => x).ToList();
            return list.Count == 0 ? 0 : list[list.Count / 2];
        }
    }
}
