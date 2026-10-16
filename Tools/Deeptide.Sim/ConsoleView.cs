using System;
using System.Linq;
using System.Text;
using Deeptide.Core;

namespace Deeptide.Sim
{
    /// <summary>把战斗状态画成文字，供控制台对战使用。</summary>
    static class ConsoleView
    {
        public static string Render(BattleEngine e)
        {
            var sb = new StringBuilder();
            var p = e.Player;
            string state = p.Sanity == 0 ? "崩溃" : p.IsClarity ? "清明" : p.IsLunatic ? "癫狂" : "清醒";
            sb.AppendLine();
            sb.AppendLine($"══ 第 {e.State.Turn} 回合 ══  生命 {p.Hp}/{p.MaxHp}  格挡 {p.Block}  能量 {p.Energy}  理智 {p.Sanity}/{p.MaxSanity}【{state}】{Statuses(p)}");
            sb.AppendLine($"抽牌堆 {p.DrawPile.Count} | 弃牌堆 {p.DiscardPile.Count} | 消耗 {p.ExhaustPile.Count}");
            sb.AppendLine("敌人：");
            foreach (var en in e.State.Enemies)
            {
                if (en.IsDead) { sb.AppendLine($"  [{en.Index}] {en.Name}  已倒下"); continue; }
                string fear = en.Mindless ? "无心智" : $"恐惧 {en.Fear}/{en.Will}";
                sb.AppendLine($"  [{en.Index}] {en.Name}  生命 {en.Hp}/{en.MaxHp}  格挡 {en.Block}  {fear}{Statuses(en)}   意图：{Intent(e, en)}");
            }
            sb.AppendLine("手牌：");
            for (int i = 0; i < p.Hand.Count; i++)
            {
                var c = p.Hand[i];
                bool mad = e.UsesMadFace(c);
                string face = c.Def.Dual ? (mad ? " ◐癫狂面" : " ◐清醒面") : "";
                string cost = c.Playable ? $"{c.Cost}费" : "—";
                string mark = e.CanPlay(i, out _) ? " " : "×";
                sb.AppendLine($" {mark}{i + 1}) {c.Name}（{cost}{face}）  {c.Describe(mad)}");
            }
            return sb.ToString();
        }

        public static string Intent(BattleEngine e, EnemyState en)
        {
            var v = e.GetIntent(en);
            if (v.Stunned) return "被震慑（跳过）";
            string types = TypeNames(v.Type);
            if (v.Blurred) return $"{types}（癫狂中看不清）";
            var parts = new System.Collections.Generic.List<string>();
            if (v.Damage > 0) parts.Add(v.Hits > 1 ? $"{v.Damage}×{v.Hits} 伤害" : $"{v.Damage} 伤害");
            if (v.SanityDamage > 0) parts.Add($"精神攻击 {v.SanityDamage}");
            if (v.Block > 0) parts.Add($"格挡 {v.Block}");
            if (!string.IsNullOrEmpty(v.Note)) parts.Add(v.Note);
            return $"{v.MoveName}［{types}］{string.Join("，", parts)}";
        }

        static string TypeNames(IntentType t)
        {
            var names = new System.Collections.Generic.List<string>();
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
    }
}
