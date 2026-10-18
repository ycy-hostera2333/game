using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Map;
using Deeptide.Core.Potions;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;

namespace Deeptide.Sim
{
    /// <summary>跑局 AI 的流派偏好：决定抓什么牌、战斗里用哪种打法（策划/07 §5.3「三流派胜率差」）。</summary>
    enum Archetype { Watch, Fall, Contagion, Mixed }

    /// <summary>
    /// 跑局 AI（策划/09 M4.8）：战斗交给一步贪心的 Autoplayer，其余界面用简单规则——
    /// 按流派给卡打分决定拿不拿、商店先删卡再买高分牌、篝火血少休息否则锻造、事件按流派选。
    /// 规则粗糙、只适合做相对比较；它的「拾取率」反映的是这套打分，不是真人的偏好。
    /// </summary>
    sealed class RunAutoplayer
    {
        readonly Archetype _arch;
        readonly Autoplayer _battle;
        int _potionTurn = -1;

        /// <summary>调试开关（--battle-style）：所有流派的战斗都用同一种打法，用来区分「牌不行」还是「打法不行」。</summary>
        public static PlayStyle? BattleStyleOverride;

        public RunAutoplayer(Archetype arch)
        {
            _arch = arch;
            _battle = new Autoplayer(BattleStyleOverride ??
                (arch == Archetype.Watch ? PlayStyle.Sane : arch == Archetype.Fall ? PlayStyle.Mad : PlayStyle.Mixed));
        }

        public static Archetype ParseArchetype(string s)
        {
            switch (s)
            {
                case "watch": return Archetype.Watch;
                case "fall": return Archetype.Fall;
                case "contagion": return Archetype.Contagion;
                case "mixed": return Archetype.Mixed;
                default: throw new ArgumentException($"未知流派：{s}（可选 watch / fall / contagion / mixed）");
            }
        }

        public static string ArchetypeName(Archetype a) =>
            a == Archetype.Watch ? "守夜" : a == Archetype.Fall ? "堕落" : a == Archetype.Contagion ? "传染" : "混合";

        /// <summary>打完整局（或到达命令上限），返回执行的命令数。</summary>
        public int PlayRun(RunEngine run, int maxCommands = 20000)
        {
            int n = 0;
            while (!run.IsOver && n < maxCommands)
            {
                var cmd = Choose(run);
                if (!run.Execute(cmd, out var error))
                    throw new InvalidOperationException($"跑局 AI 选了非法命令 {cmd}（{run.State.Phase}）：{error}");
                n++;
            }
            return n;
        }

        public RunCommand Choose(RunEngine run)
        {
            var s = run.State;
            if (s.Phase == RunPhase.Battle) return ChooseBattle(run);
            var options = run.Options();
            int pick;
            switch (s.Phase)
            {
                case RunPhase.Map: pick = ChooseNode(run, options); break;
                case RunPhase.Reward: pick = ChooseReward(run, options); break;
                case RunPhase.Shop: pick = ChooseShop(run, options); break;
                case RunPhase.Campfire: pick = ChooseCampfire(run, options); break;
                case RunPhase.Event: pick = ChooseEvent(run, options); break;
                case RunPhase.Select: pick = ChooseSelect(run, options); break;
                default: pick = 0; break;
            }
            return RunCommand.Pick(pick);
        }

        static int IndexOf(List<RunOption> options, Func<RunOption, bool> pred)
        {
            for (int i = 0; i < options.Count; i++)
                if (options[i].Enabled && pred(options[i])) return i;
            return -1;
        }

        static int Tagged(List<RunOption> options, string tag) => IndexOf(options, o => o.Tag == tag);

        static int Last(List<RunOption> options) => options.Count - 1;

        // ------------------------------------------------------------------ 战斗

        RunCommand ChooseBattle(RunEngine run)
        {
            var b = run.Battle;
            if (b.State.Phase == BattlePhase.PlayerAction && _potionTurn != b.State.Turn * 1000 + run.State.BattlesFought)
            {
                var potion = ChoosePotion(run, b);
                if (potion.HasValue) return RunCommand.Fight(potion.Value);
                _potionTurn = b.State.Turn * 1000 + run.State.BattlesFought;
            }
            return RunCommand.Fight(_battle.Choose(b));
        }

        /// <summary>药水：精英 / Boss 第一回合用增益和群伤；快死的时候用防御类；理智见底用嗅盐。每回合最多想一次。</summary>
        BattleCommand? ChoosePotion(RunEngine run, BattleEngine b)
        {
            var p = b.Player;
            var room = run.State.Battle.Room;
            bool big = room == RoomType.Elite || room == RoomType.Boss;
            int incoming = b.IncomingDamage();
            var alive = b.State.AliveEnemies.ToList();
            for (int i = 0; i < p.Potions.Count; i++)
            {
                var pot = p.Potions[i];
                if (pot == null) continue;
                int target = alive.OrderByDescending(x => x.Hp).First().Index;
                switch (pot.Id)
                {
                    case "berserk_tincture":
                    case "fear_powder":
                    case "acid_vial":
                        if (big && b.State.Turn == 1 && b.State.CardsPlayedThisTurn == 0) return BattleCommand.Potion(i, target);
                        break;
                    case "powder_flask":
                        if ((big && b.State.Turn == 1) || alive.Count >= 3 || p.Hp <= 15) return BattleCommand.Potion(i, target);
                        break;
                    case "iron_tonic":
                        if (incoming - p.Block >= Math.Min(20, p.Hp / 2)) return BattleCommand.Potion(i, target);
                        break;
                    case "warding_draught":
                        if (big && b.State.Turn == 1) return BattleCommand.Potion(i, target);
                        break;
                    case "smelling_salts":
                        if (p.Sanity <= 2 || (_arch == Archetype.Watch && p.Sanity <= p.LunacyThreshold + 1)) return BattleCommand.Potion(i, target);
                        break;
                    case "hallucinogen":
                        if (_arch == Archetype.Fall && b.State.Turn == 1 && p.Sanity > 6) return BattleCommand.Potion(i, target);
                        if (p.Hp <= 12) return BattleCommand.Potion(i, target);
                        break;
                }
                if (p.Hp <= 10 && incoming >= p.Hp + p.Block) return BattleCommand.Potion(i, target);
            }
            return null;
        }

        // ------------------------------------------------------------------ 卡牌打分

        /// <summary>
        /// AI 对每张牌强弱的粗略估计（1.0 = 一张合格的普通牌）。这是 AI 的知识，不是游戏数据，所以写在这里不进 CSV。
        /// 依据 策划/07 的效率区间：输出牌和过牌偏高，纯回复 / 条件苛刻的偏低。
        /// </summary>
        static readonly Dictionary<string, double> Quality = new Dictionary<string, double>
        {
            ["B01"] = 0.4, ["B02"] = 0.4, ["B03"] = 0.6,
            ["C01"] = 1.0, ["C02"] = 1.1, ["C03"] = 1.3, ["C04"] = 0.7, ["C05"] = 1.3, ["C06"] = 1.4, ["C07"] = 1.3, ["C08"] = 1.0,
            ["C09"] = 1.3, ["C10"] = 1.1, ["C11"] = 1.2, ["C12"] = 1.3, ["C13"] = 1.2, ["C14"] = 1.3, ["C15"] = 1.1, ["C16"] = 1.0,
            ["U01"] = 1.0, ["U02"] = 1.3, ["U03"] = 1.6, ["U04"] = 0.6, ["U05"] = 1.4, ["U06"] = 1.3, ["U07"] = 1.3, ["U08"] = 1.5,
            ["U09"] = 1.5, ["U10"] = 1.0, ["U11"] = 1.4, ["U12"] = 1.4, ["U13"] = 1.2, ["U14"] = 1.0, ["U15"] = 1.2,
            ["R01"] = 1.2, ["R02"] = 1.2, ["R03"] = 1.8, ["R04"] = 1.3, ["R05"] = 2.0, ["R06"] = 0.9,
        };

        /// <summary>按流派给一张牌打分（越高越想要）。≥ 阈值才拿。</summary>
        public double CardScore(RunState s, string deckId)
        {
            var def = CardLibrary.Get(CardLibrary.StripUpgrade(deckId, out bool up));
            if (def.Type == CardType.Curse || def.Type == CardType.Status) return -5;
            double v = Quality.TryGetValue(def.Id, out var q) ? q : 1.0;
            // 牌组里输出不够时，攻击牌加分（Boss 有 240 血）
            int attacks = s.Deck.Count(x => CardLibrary.Get(CardLibrary.StripUpgrade(x, out _)).Type == CardType.Attack && !x.StartsWith("B01"));
            if (def.Type == CardType.Attack && attacks < 5) v += 0.4;
            string a = def.Archetype;
            switch (_arch)
            {
                case Archetype.Watch: v += a == "守夜" ? 1.5 : a == "通用" ? 0.5 : a == "传染" ? 0.2 : -1.0; break;
                case Archetype.Fall: v += a == "堕落" ? 1.5 : a == "通用" ? 0.5 : a == "传染" ? 0.2 : -1.0; break;
                case Archetype.Contagion: v += a == "传染" ? 1.5 : a == "通用" ? 0.5 : -0.3; break;
                default: v += a == "基础" ? 0 : 0.6; break;
            }
            int copies = s.Deck.Count(x => CardLibrary.StripUpgrade(x, out _) == def.Id);
            v -= 0.6 * copies;
            if (def.Type == CardType.Power && copies > 0) v -= 0.8;
            if (s.Deck.Count >= 22) v -= 0.5;
            if (def.Id == "B01") v -= 1.0;
            if (up) v += 0.3;
            return v;
        }

        const double TakeThreshold = 1.6;

        /// <summary>调试：打印选路时每个候选的得分。</summary>
        public static bool Debug;

        // ------------------------------------------------------------------ 地图

        double RoomScore(RunState s, RoomType room)
        {
            double hp = s.Hp / (double)s.MaxHp;
            switch (room)
            {
                case RoomType.Battle: return s.Floor < 4 ? 1.4 : 1.0;
                case RoomType.Elite: return hp > 0.6 && s.Floor >= 5 ? 4.5 : hp > 0.45 ? 1.0 : -2.5;
                case RoomType.Campfire: return hp < 0.5 ? 3.0 : 0.9;
                case RoomType.Shop: return s.Gold >= 160 ? 2.2 : s.Gold >= 100 ? 1.2 : 0.2;
                case RoomType.Event: return 1.1;
                case RoomType.Treasure: return 3.0;
                default: return 0;
            }
        }

        /// <summary>往后看 depth 层的最好路线（越远的节点权重越低）。</summary>
        double PathValue(RunState s, MapData map, MapNode n, int depth)
        {
            double v = RoomScore(s, n.Room);
            if (depth <= 1 || n.Children.Count == 0) return v;
            return v + 0.7 * n.Children.Max(c => PathValue(s, map, map.Get(n.Row + 1, c), depth - 1));
        }

        int ChooseNode(RunEngine run, List<RunOption> options)
        {
            var nodes = run.NextNodes().ToList();
            if (nodes.Count == 0) return 0;   // Boss
            var map = run.State.Map;
            double best = double.NegativeInfinity;
            int pick = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                double v = PathValue(run.State, map, nodes[i], 4);
                if (Debug) Console.WriteLine($"      候选 {nodes[i].Floor} 层 {nodes[i].Room} 第 {nodes[i].Col} 列：{v:F2}");
                if (v > best + 1e-9)
                {
                    best = v;
                    pick = i;
                }
            }
            return pick;
        }

        // ------------------------------------------------------------------ 奖励 / 选择

        int ChooseReward(RunEngine run, List<RunOption> options)
        {
            int i = Tagged(options, "gold");
            if (i >= 0) return i;
            i = IndexOf(options, o => o.Tag != null && o.Tag.StartsWith("relic:"));
            if (i >= 0) return i;
            i = IndexOf(options, o => o.Tag != null && o.Tag.StartsWith("potion:") && run.State.Potions.Contains(null));
            if (i >= 0) return i;
            i = Tagged(options, "cards");
            if (i >= 0) return i;
            return Tagged(options, "leave");
        }

        int ChooseSelect(RunEngine run, List<RunOption> options)
        {
            var sel = run.State.Select;
            switch (sel.Purpose)
            {
                case SelectPurpose.AddCard:
                {
                    double best = TakeThreshold;
                    int pick = Tagged(options, "skip");
                    for (int i = 0; i < options.Count; i++)
                    {
                        if (options[i].CardId == null) continue;
                        double v = CardScore(run.State, options[i].CardId);
                        if (v > best) { best = v; pick = i; }
                    }
                    return pick;
                }
                case SelectPurpose.RemoveCard:
                {
                    int pick = -1;
                    double worst = double.PositiveInfinity;
                    for (int i = 0; i < options.Count; i++)
                    {
                        if (options[i].CardId == null) continue;
                        double v = options[i].CardId == CardIds.Whispers ? -10 : CardScore(run.State, options[i].CardId) - (options[i].CardId == "B02" ? 0.5 : 0);
                        if (v < worst) { worst = v; pick = i; }
                    }
                    return pick >= 0 ? pick : Last(options);
                }
                case SelectPurpose.UpgradeCard:
                {
                    int pick = -1;
                    double best = double.NegativeInfinity;
                    for (int i = 0; i < options.Count; i++)
                    {
                        if (options[i].CardId == null) continue;
                        var def = CardLibrary.Get(options[i].CardId);
                        double v = CardScore(run.State, options[i].CardId) + (def.Type == CardType.Power ? 0.8 : 0) + (def.Rarity == Rarity.Basic ? -0.5 : 0);
                        if (v > best) { best = v; pick = i; }
                    }
                    return pick >= 0 ? pick : Last(options);
                }
                default:
                    return Tagged(options, "decline");
            }
        }

        // ------------------------------------------------------------------ 商店 / 篝火 / 事件

        int ChooseShop(RunEngine run, List<RunOption> options)
        {
            var s = run.State;
            bool hasJunk = s.Deck.Any(x => x == CardIds.Whispers || x == "B01" || x == "B02");
            int remove = Tagged(options, "remove");
            if (remove >= 0 && hasJunk) return remove;

            double best = 2.3;
            int pick = -1;
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                if (!o.Enabled || o.Tag == null) continue;
                double v;
                if (o.Tag.StartsWith("shop:card:")) v = CardScore(s, o.CardId);
                else if (o.Tag.StartsWith("shop:relic:")) v = RelicScore(o.Tag.Substring("shop:relic:".Length));
                else if (o.Tag.StartsWith("shop:potion:")) v = s.Gold > 150 ? 2.4 : 0;
                else continue;
                if (v > best) { best = v; pick = i; }
            }
            return pick >= 0 ? pick : Tagged(options, "leave");
        }

        double RelicScore(string id)
        {
            var def = RelicLibrary.Get(id);
            if (def.Rarity == RelicRarity.Shop) return id == RelicIds.AlchemistBelt ? 2.0 : 2.4;
            string t = def.Tendency;
            bool match = (_arch == Archetype.Watch && t == "守夜") || (_arch == Archetype.Fall && t == "堕落") ||
                         (_arch == Archetype.Contagion && t == "传染") || t == "中立";
            return (match ? 3.0 : 1.5) + (def.Rarity == RelicRarity.Rare ? 0.4 : 0);
        }

        int ChooseCampfire(RunEngine run, List<RunOption> options)
        {
            var s = run.State;
            double hp = s.Hp / (double)s.MaxHp;
            bool beforeBoss = s.Floor >= s.Map.Height;
            if (hp < 0.5 || (beforeBoss && hp < 0.85)) return Tagged(options, "rest");
            int purge = Tagged(options, "purge");
            if (purge >= 0 && s.Deck.Count(x => x == CardIds.Whispers) >= 2) return purge;
            if (_arch == Archetype.Watch && s.MaxSanity < 10) return Tagged(options, "meditate");
            int smith = Tagged(options, "smith");
            return smith >= 0 ? smith : Tagged(options, "rest");
        }

        int ChooseEvent(RunEngine run, List<RunOption> options)
        {
            var s = run.State;
            double hp = s.Hp / (double)s.MaxHp;
            var prefs = new List<string>();
            switch (s.Event.Id)
            {
                case "forbidden_book":
                    prefs.AddRange(_arch == Archetype.Fall ? new[] { "Read", "Copy" } : _arch == Archetype.Watch ? new[] { "Burn" } : new[] { "Copy", "Burn" });
                    break;
                case "drowning_sailor":
                    if (s.Hp > 30) prefs.Add("Rescue");
                    prefs.Add("Leave");
                    break;
                case "seaside_altar":
                    if (s.Hp > 45) prefs.Add("Blood");
                    if (s.Deck.Count > 12) prefs.Add("Gold");
                    prefs.Add("Leave");
                    break;
                case "mirror":
                    if (_arch == Archetype.Fall) prefs.Add("Gaze");
                    if (s.Hp > 30) prefs.Add("Shatter");
                    prefs.Add("Leave");
                    break;
                case "asylum_ward":
                    prefs.Add("purge");
                    if (_arch == Archetype.Fall || _arch == Archetype.Contagion) prefs.Add("Steal");
                    if (s.Gold >= 120 || s.Deck.Contains(CardIds.Whispers)) prefs.Add("Treat");
                    prefs.Add("meditate");
                    prefs.Add("Leave");
                    break;
                case "cult_gathering":
                    prefs.Add(hp > 0.55 ? "Mingle" : "Sneak");
                    prefs.Add("Sneak");
                    break;
                case "fog_peddler":
                    if (s.MaxHp > 60) prefs.Add("Blood");
                    prefs.Add("Barter");
                    prefs.Add("Leave");
                    break;
                case "dream_mentor":
                    prefs.Add(_arch == Archetype.Watch && (s.MaxSanity < 10 || hp < 0.6) ? "Wake" : "Follow");
                    prefs.Add("Wake");
                    break;
            }
            foreach (var tag in prefs)
            {
                int i = Tagged(options, tag);
                if (i >= 0) return i;
            }
            return IndexOf(options, o => true);
        }
    }
}
