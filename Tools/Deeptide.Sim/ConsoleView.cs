using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Deeptide.Core;

namespace Deeptide.Sim
{
    /// <summary>把战斗状态画成文字，并把控制台输入翻译成 BattleCommand（play 和 run 模式共用）。</summary>
    static class ConsoleView
    {
        public const string InputHelp = "输入：牌号 [目标]（如「2」或「2 1」）出牌 | p药水栏 [目标]（如「p1」）用药水 | e 结束回合 | 等待选择时输入选项编号";

        public static string Render(BattleEngine e)
        {
            var sb = new StringBuilder();
            var p = e.Player;
            string state = p.Sanity == 0 ? "崩溃" : p.IsClarity ? "清明" : p.IsLunatic ? "癫狂" : "清醒";
            sb.AppendLine();
            sb.AppendLine($"══ 第 {e.State.Turn} 回合 ══  生命 {p.Hp}/{p.MaxHp}  格挡 {p.Block}  能量 {p.Energy}  理智 {p.Sanity}/{p.MaxSanity}【{state}】{Statuses(p)}");
            if (p.Powers.Count > 0) sb.AppendLine($"能力：{string.Join("、", p.Powers.Select(x => $"{x.Name} {x.Amount}"))}");
            if (p.Potions.Any(x => x != null))
                sb.AppendLine($"药水：{string.Join("  ", p.Potions.Select((x, i) => $"p{i + 1} {(x == null ? "（空）" : x.Name + "：" + x.Description)}"))}");
            sb.AppendLine($"抽牌堆 {p.DrawPile.Count} | 弃牌堆 {p.DiscardPile.Count} | 消耗 {p.ExhaustPile.Count}");
            sb.AppendLine("敌人：");
            foreach (var en in e.State.Enemies)
            {
                if (en.IsDead) { sb.AppendLine($"  [{en.Index}] {en.Name}  已倒下"); continue; }
                string fear = en.Mindless ? "无心智" : $"恐惧 {en.Fear}/{en.Will}";
                string counters = string.Concat(en.Counters.Where(kv => !kv.Key.StartsWith("_")).Select(kv => $"  {CounterName(kv.Key)} {kv.Value}"));
                sb.AppendLine($"  [{en.Index}] {en.Name}  生命 {en.Hp}/{en.MaxHp}  格挡 {en.Block}  {fear}{Statuses(en)}{counters}   意图：{Intent(e, en)}");
            }
            sb.AppendLine("手牌：");
            for (int i = 0; i < p.Hand.Count; i++)
            {
                var c = p.Hand[i];
                bool mad = e.UsesMadFace(c);
                string face = c.Def.Dual ? (mad ? " ◐癫狂面" : " ◐清醒面") : "";
                string cost = !c.Playable ? "—" : c.Def.XCost ? "X费" : $"{e.CostOf(c)}费";
                string mark = e.CanPlay(i, out _) ? " " : "×";
                sb.AppendLine($" {mark}{i + 1}) {c.Name}（{cost}{face}）  {c.Describe(e)}");
            }
            if (e.State.Phase == BattlePhase.WaitingForChoice && e.State.Choice != null)
            {
                sb.AppendLine($"【请选择】{e.State.Choice.Prompt}");
                for (int i = 0; i < e.State.Choice.Options.Count; i++) sb.AppendLine($"   {i + 1}) {e.State.Choice.Options[i].Label}");
            }
            return sb.ToString();
        }

        static string CounterName(string key)
        {
            switch (key)
            {
                case "Tide": return "潮位";
                case "Phase": return "阶段";
                default: return key;
            }
        }

        public static string Intent(BattleEngine e, EnemyState en)
        {
            var v = e.GetIntent(en);
            if (v.Stunned) return "被震慑（跳过）";
            if (v.Hidden) return "?（不可名状）";
            string types = TypeNames(v.Type);
            if (v.Blurred) return $"{types}（癫狂中看不清）";
            var parts = new List<string>();
            if (v.Damage > 0) parts.Add(v.Hits > 1 ? $"{v.Damage}×{v.Hits} 伤害" : $"{v.Damage} 伤害");
            if (v.SanityDamage > 0) parts.Add($"精神攻击 {v.SanityDamage}");
            if (v.Block > 0) parts.Add($"格挡 {v.Block}");
            if (!string.IsNullOrEmpty(v.Note)) parts.Add(v.Note);
            return $"{v.MoveName}［{types}］{string.Join("，", parts)}";
        }

        static string TypeNames(IntentType t)
        {
            var names = new List<string>();
            if ((t & IntentType.Attack) != 0) names.Add("攻击");
            if ((t & IntentType.Defend) != 0) names.Add("防御");
            if ((t & IntentType.Buff) != 0) names.Add("增益");
            if ((t & IntentType.Debuff) != 0) names.Add("减益");
            if ((t & IntentType.Mind) != 0) names.Add("精神");
            if ((t & IntentType.Summon) != 0) names.Add("召唤");
            if (names.Count == 0) names.Add("?");
            return string.Join("+", names);
        }

        static string Statuses(Combatant c)
        {
            var list = c.Statuses.Ordered().Select(kv => $"{StatusSet.NameOf(kv.Key)} {kv.Value}").ToList();
            return list.Count == 0 ? "" : "  " + string.Join(" ", list);
        }

        /// <summary>
        /// 把一行输入翻译成命令。需要目标但没给、且有多个敌人时，用 readLine 追问一次。
        /// 等待选择时，数字表示选项编号（从 1 开始）。
        /// </summary>
        public static bool TryParseCommand(BattleEngine e, string line, Func<string> readLine, out BattleCommand cmd, out string error)
        {
            cmd = default;
            error = null;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) { error = "看不懂这条输入"; return false; }

            if (e.State.Phase == BattlePhase.WaitingForChoice)
            {
                if (!int.TryParse(parts[0], out int opt)) { error = "请输入选项编号"; return false; }
                cmd = BattleCommand.Choose(opt - 1);
                return true;
            }
            if (parts[0] == "e")
            {
                cmd = BattleCommand.End();
                return true;
            }

            bool potion = parts[0].StartsWith("p", StringComparison.Ordinal);
            if (!int.TryParse(potion ? parts[0].Substring(1) : parts[0], out int n)) { error = "看不懂这条输入"; return false; }
            int target = parts.Length > 1 && int.TryParse(parts[1], out int t) ? t : -1;
            bool needsTarget = potion
                ? n >= 1 && n <= e.Player.Potions.Count && e.Player.Potions[n - 1]?.Def.Target == CardTarget.Enemy
                : n >= 1 && n <= e.Player.Hand.Count && e.Player.Hand[n - 1].Def.Target == CardTarget.Enemy;
            if (target < 0 && needsTarget)
            {
                var alive = e.State.Enemies.Where(x => !x.IsDead).ToList();
                if (alive.Count == 1) target = alive[0].Index;
                else
                {
                    Console.Write($"目标？（{string.Join(" / ", alive.Select(x => $"{x.Index}={x.Name}"))}）> ");
                    if (!int.TryParse(readLine(), out target)) { error = "没有选目标"; return false; }
                }
            }
            cmd = potion ? BattleCommand.Potion(n - 1, target) : BattleCommand.Play(n - 1, target);
            return true;
        }
    }
}
