using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Content;
using Deeptide.Core.Enemies;
using Deeptide.Core.Map;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;

namespace Deeptide.Sim
{
    /// <summary>
    /// 运行期数值覆盖，格式：--set "erosion=3;lunacy=5;hpHard=1.15;starter=prayer;U09.PerPoint=3"
    ///   erosion  侵蚀起始回合            lunacy   癫狂线（理智 ≤ 此值）
    ///   hp       所有遭遇的敌人生命倍率    hpHard   只对困难池（遭遇 2、3）生效的生命倍率
    ///   starter  base = 打击5 防御4 一瞥1；prayer = 用 1 张祷告换掉 1 张防御
    ///   clarity  清明余量（清明 = 理智 ≥ 上限 − 余量）
    ///   卡牌ID.变量名  改卡牌数值（只改未升级版）；卡牌ID.cost / 卡牌ID.cost_up 改费用    敌人ID.hp=最小-最大  改某种敌人的生命区间
    ///   敌人ID.数值名  改敌人招式数值，如 acolyte.Dagger=6
    ///   map.房间类型  改地图房间权重（Battle / Event / Campfire / Elite / Shop），如 map.Elite=12
    /// 只在本进程内生效，不改源码。
    /// </summary>
    static class Tuning
    {
        public static readonly BattleConfig Config = new BattleConfig();
        public static readonly MapConfig Map = new MapConfig();
        public static double HpEasy = 1.0;
        public static double HpHard = 1.0;
        /// <summary>starter=base|prayer 改过的调查员起始牌组；null = 用角色表（characters.csv）里的。</summary>
        public static List<string> StarterDeck;

        static readonly Dictionary<string, (int Min, int Max)> OriginalHp =
            EnemyLibrary.All.ToDictionary(d => d.Id, d => (d.HpMin, d.HpMax));

        public static void Apply(string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return;
            foreach (var part in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length != 2) throw new ArgumentException($"--set 格式错误：{part}");
                string key = kv[0].Trim(), val = kv[1].Trim();
                switch (key)
                {
                    case "erosion": Config.ErosionStartTurn = int.Parse(val); break;
                    case "lunacy": Config.LunacyThreshold = int.Parse(val); break;
                    case "clarity": Config.ClarityMargin = int.Parse(val); break;
                    case "hp": HpEasy = HpHard = double.Parse(val, CultureInfo.InvariantCulture); break;
                    case "hpHard": HpHard = double.Parse(val, CultureInfo.InvariantCulture); break;
                    case "starter":
                        if (val != "base" && val != "prayer") throw new ArgumentException("starter 只能是 base 或 prayer");
                        TestDecks.Starter[8] = val == "prayer" ? "C01" : "B02";
                        StarterDeck = TestDecks.Starter.ToList();
                        break;
                    default:
                        int dot = key.IndexOf('.');
                        if (dot < 0) throw new ArgumentException($"未知参数：{key}");
                        string owner = key.Substring(0, dot);
                        if (owner == "map")
                        {
                            if (!Enum.TryParse<RoomType>(key.Substring(dot + 1), out var room) || !Map.Weights.ContainsKey(room))
                                throw new ArgumentException($"没有这种房间权重：{key}");
                            Map.Weights[room] = int.Parse(val);
                            break;
                        }
                        if (key.EndsWith(".hp", StringComparison.Ordinal))
                        {
                            var range = val.Split('-');
                            OriginalHp[owner] = (int.Parse(range[0]), int.Parse(range[1]));
                            break;
                        }
                        if (OriginalHp.ContainsKey(owner))
                        {
                            var enemy = EnemyLibrary.Get(owner);
                            string varName = key.Substring(dot + 1);
                            if (!enemy.Vars.ContainsKey(varName)) throw new ArgumentException($"{owner} 没有数值 {varName}");
                            enemy.Vars[varName] = int.Parse(val);
                            break;
                        }
                        if (RelicLibrary.Exists(owner))
                        {
                            // 遗物数值，如 wolf_fang_pendant.Block=6（狼人调参用）
                            var relic = RelicLibrary.Get(owner);
                            string relicVar = key.Substring(dot + 1);
                            if (!relic.Vars.ContainsKey(relicVar)) throw new ArgumentException($"{owner} 没有数值 {relicVar}");
                            relic.Vars[relicVar] = int.Parse(val);
                            break;
                        }
                        var def = CardLibrary.Get(key.Substring(0, dot));
                        string name = key.Substring(dot + 1);
                        if (name == "cost") { def.Cost = int.Parse(val); break; }
                        if (name == "cost_up") { def.UpgradedCost = int.Parse(val); break; }
                        if (!def.Vars.ContainsKey(name)) throw new ArgumentException($"{def.Id} 没有变量 {name}");
                        def.Vars[name] = int.Parse(val);
                        break;
                }
            }
        }

        /// <summary>整局模式：生命覆盖（敌人ID.hp=…）和倍率对所有敌人生效（简单池用 hp 倍率，其余用 hpHard）。</summary>
        public static void ApplyHpForRuns()
        {
            foreach (var d in EnemyLibrary.All)
            {
                if (d.Id == EnemyIds.Dummy) continue;
                var (min, max) = OriginalHp[d.Id];
                bool easy = EncounterLibrary.InPool(EncounterPool.Easy).Any(x => x.Enemies.Contains(d.Id)) &&
                            !EncounterLibrary.All.Any(x => x.Pool != EncounterPool.Easy && x.Enemies.Contains(d.Id));
                double scale = easy ? HpEasy : HpHard;
                d.HpMin = (int)Math.Round(min * scale);
                d.HpMax = (int)Math.Round(max * scale);
            }
        }

        /// <summary>按遭遇所在的池子设置敌人生命（简单池用 hp，其余用 hpHard）。</summary>
        public static void ApplyHpFor(string enc) => ScaleEnemyHp(Encounters.Resolve(enc).Pool == EncounterPool.Easy ? HpEasy : HpHard);

        static void ScaleEnemyHp(double scale)
        {
            foreach (var d in EnemyLibrary.All)
            {
                if (d.Id == EnemyIds.Dummy) continue;
                var (min, max) = OriginalHp[d.Id];
                d.HpMin = (int)Math.Round(min * scale);
                d.HpMax = (int)Math.Round(max * scale);
            }
        }
    }

    sealed class MatchupStats
    {
        public string Deck, Enc;
        public PlayStyle Style;
        public int N;
        public double WinRate, Turns, HpLost, Collapses, MinSanity, LunacyFightShare, FirstLunacy, LunacyTurnShare, ClarityTurnShare;
    }

    static class Matchups
    {
        public static BattleSetup Setup(string deck, string enc, ulong seed)
        {
            EncounterDef def;
            try { def = Encounters.Resolve(enc); }
            catch (KeyNotFoundException) { throw new ArgumentException($"未知遭遇：{enc}（可选 1 / 2 / 3 或遭遇表里的 ID）"); }
            var setup = new BattleSetup { Seed = seed, Deck = TestDecks.Get(deck), Enemies = def.Enemies.ToList(), Config = Tuning.Config };
            string character = TestDecks.CharacterOf(deck);
            if (character != CharacterIds.Investigator)
            {
                // 狼人的牌组用狼人的生命、理智上限、起始遗物和特性；调查员沿用 BattleSetup 的默认值，结果和加角色之前一致
                var ch = CharacterLibrary.Get(character);
                setup.MaxHp = setup.Hp = ch.MaxHp;
                setup.MaxSanity = ch.MaxSanity;
                setup.Relics = new List<string> { ch.StartingRelic };
                if (ch.Trait.Length > 0) setup.Traits = new List<string> { ch.Trait };
            }
            return setup;
        }

        /// <summary>用种子 1..n 跑 n 场（不同参数之间共用同一组种子，比较时噪声更小）。</summary>
        public static MatchupStats Run(string deck, string enc, int n, PlayStyle style)
        {
            Tuning.ApplyHpFor(enc);
            var won = new bool[n];
            var turns = new int[n];
            var hpLost = new int[n];
            var collapses = new int[n];
            var minSanity = new int[n];
            var firstLunacy = new int[n];
            var lunacyTurns = new int[n];
            var clarityTurns = new int[n];

            Parallel.For(0, n, i =>
            {
                var e = BattleEngine.Start(Setup(deck, enc, (ulong)(i + 1)));
                var ai = new Autoplayer(style);
                while (e.State.Outcome == BattleOutcome.Ongoing && e.State.Turn <= 40) ai.PlayTurn(e);
                var s = e.State.Stats;
                won[i] = e.State.Outcome == BattleOutcome.Victory;
                turns[i] = e.State.Turn;
                hpLost[i] = e.Setup.Hp - e.Player.Hp;
                collapses[i] = s.Collapses;
                minSanity[i] = s.MinSanity;
                firstLunacy[i] = s.FirstLunacyTurn;
                lunacyTurns[i] = s.LunacyTurns;
                clarityTurns[i] = s.ClarityTurns;
            });

            double totalTurns = turns.Sum();
            var entered = firstLunacy.Where(t => t > 0).ToList();
            return new MatchupStats
            {
                Deck = deck,
                Enc = enc,
                Style = style,
                N = n,
                WinRate = won.Count(x => x) / (double)n,
                Turns = turns.Average(),
                HpLost = hpLost.Average(),
                Collapses = collapses.Average(),
                MinSanity = minSanity.Average(),
                LunacyFightShare = entered.Count / (double)n,
                FirstLunacy = entered.Count > 0 ? entered.Average() : 0,
                LunacyTurnShare = lunacyTurns.Sum() / totalTurns,
                ClarityTurnShare = clarityTurns.Sum() / totalTurns,
            };
        }
    }

    /// <summary>
    /// 自动调参：在参数网格上跑全部「牌组 × 遭遇」，按 策划/07 §5 的目标区间打分（越低越好）。
    /// </summary>
    static class Tuner
    {
        public sealed class Evaluation
        {
            public string Spec;
            public double Score;
            public List<MatchupStats> Main = new List<MatchupStats>();
            public MatchupStats[] MixedSane, MixedMad;
            public Dictionary<string, double> Penalties = new Dictionary<string, double>();
        }

        static double OutOfRange(double v, double lo, double hi) => v < lo ? lo - v : v > hi ? v - hi : 0;

        /// <summary>
        /// 评估口径：遭遇 1 是第一层的战斗，用起始牌组衡量；遭遇 2、3 属于困难池，用 A/B/C 三套中期牌组衡量。
        /// 起始牌组也会打遭遇 2、3，只要求能赢（对应第 4–5 层刚进困难池时）。
        /// </summary>
        public static Evaluation Evaluate(string spec, int n)
        {
            var ev = new Evaluation { Spec = spec };
            foreach (var enc in new[] { "1", "2", "3" })
                ev.Main.Add(Matchups.Run("starter", enc, n, PlayStyle.Sane));
            foreach (var deck in new[] { "A", "B", "C" })
            foreach (var enc in new[] { "2", "3" })
                ev.Main.Add(Matchups.Run(deck, enc, n, Autoplayer.StyleFor(deck)));
            ev.MixedSane = new[] { "2", "3" }.Select(enc => Matchups.Run("C", enc, n, PlayStyle.Sane)).ToArray();
            ev.MixedMad = new[] { "2", "3" }.Select(enc => Matchups.Run("C", enc, n, PlayStyle.Mad)).ToArray();

            var p = ev.Penalties;
            var starter = ev.Main.Where(m => m.Deck == "starter").ToList();
            var mid = ev.Main.Where(m => m.Deck != "starter").ToList();

            // 1) 简单池：起始牌组打遭遇 1，3–4 回合、掉 2–6 血
            var s1 = starter.Single(m => m.Enc == "1");
            Add(p, "回合数", 2.0 * OutOfRange(s1.Turns, 3, 4));
            Add(p, "掉血", 0.5 * OutOfRange(s1.HpLost, 2, 6));
            // 起始牌组要能打过困难池（胜率 ≥ 80%）
            foreach (var m in starter) Add(p, "胜率", 20 * Math.Max(0, (m.Enc == "1" ? 0.95 : 0.8) - m.WinRate));
            // 2) 困难池：中期牌组 4–6 回合、掉 4–9 血、胜率 ≥ 90%
            foreach (var m in mid)
            {
                Add(p, "回合数", 2.0 * OutOfRange(m.Turns, 4, 6));
                Add(p, "掉血", 0.5 * OutOfRange(m.HpLost, 4, 9));
                Add(p, "胜率", 20 * Math.Max(0, 0.9 - m.WinRate));
            }
            // 3) 守夜：清明回合占比 ≥ 50%
            Add(p, "守夜清明", 10 * Math.Max(0, 0.5 - mid.Where(m => m.Deck == "A").Average(m => m.ClarityTurnShare)));
            // 4) 堕落：进入癫狂 ≥ 80%，崩溃 0.3–0.8 次 / 场
            var fall = mid.Where(m => m.Deck == "B").ToList();
            Add(p, "堕落入癫", 5 * Math.Max(0, 0.8 - fall.Average(m => m.LunacyFightShare)));
            Add(p, "堕落崩溃", 2 * OutOfRange(fall.Average(m => m.Collapses), 0.3, 0.8));
            // 起始牌组刚进困难池时不该场场崩溃（每次崩溃都会永久塞一张诅咒）
            Add(p, "起始崩溃", 4 * Math.Max(0, starter.Where(m => m.Enc != "1").Average(m => m.Collapses) - 0.5));
            // 5) 混合：进入癫狂的战斗占 30–70%；「一直保持清醒」和「主动坠落」两条路的掉血差距要小
            Add(p, "混合入癫", 5 * OutOfRange(mid.Where(m => m.Deck == "C").Average(m => m.LunacyFightShare), 0.3, 0.7));
            double gap = ev.MixedMad.Average(m => m.HpLost) - ev.MixedSane.Average(m => m.HpLost);
            Add(p, "两条路差距", 0.5 * Math.Abs(gap));
            // 6) 中期牌组整体进入癫狂的比例 40–70%（策划/07 §5.3）
            Add(p, "整体入癫", 5 * OutOfRange(mid.Average(m => m.LunacyFightShare), 0.4, 0.7));

            ev.Score = p.Values.Sum();
            return ev;
        }

        static void Add(Dictionary<string, double> p, string key, double v) => p[key] = (p.TryGetValue(key, out var x) ? x : 0) + v;

        public static string Summary(Evaluation ev)
        {
            var mid = ev.Main.Where(m => m.Deck != "starter").ToList();
            var s1 = ev.Main.Single(m => m.Deck == "starter" && m.Enc == "1");
            string Avg(string deck, Func<MatchupStats, double> f) => mid.Where(m => m.Deck == deck).Average(f).ToString("F1");
            string Pc(string deck, Func<MatchupStats, double> f) => mid.Where(m => m.Deck == deck).Average(f).ToString("P0");
            return $"{ev.Score,6:F2}  起始牌组打遭遇1 {s1.Turns:F1} 回合/掉 {s1.HpLost:F1}" +
                   $"  困难池回合 A/B/C {Avg("A", m => m.Turns)}/{Avg("B", m => m.Turns)}/{Avg("C", m => m.Turns)}" +
                   $"  掉血 {Avg("A", m => m.HpLost)}/{Avg("B", m => m.HpLost)}/{Avg("C", m => m.HpLost)}" +
                   $"  最低胜率 {ev.Main.Min(m => m.WinRate):P0}" +
                   $"  入癫 A/B/C {Pc("A", m => m.LunacyFightShare)}/{Pc("B", m => m.LunacyFightShare)}/{Pc("C", m => m.LunacyFightShare)}" +
                   $"  B崩溃 {Avg("B", m => m.Collapses)}  A清明 {Pc("A", m => m.ClarityTurnShare)}" +
                   $"  C清醒/坠落掉血 {ev.MixedSane.Average(m => m.HpLost):F1}/{ev.MixedMad.Average(m => m.HpLost):F1}";
        }

        public static IEnumerable<string> Grid(Dictionary<string, string[]> axes)
        {
            IEnumerable<string> specs = new[] { "" };
            foreach (var axis in axes)
                specs = specs.SelectMany(s => axis.Value.Select(v => (s == "" ? "" : s + ";") + axis.Key + "=" + v)).ToList();
            return specs;
        }
    }
}
