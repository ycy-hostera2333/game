using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Deeptide.Core.Cards;

namespace Deeptide.Core
{
    /// <summary>
    /// 战斗状态指纹：把所有会影响后续结算的状态按固定顺序拼成字符串，再做 FNV-1a 64 位哈希。
    /// 回放测试用它判断两次结算是否完全一致（策划/08 §7）。
    /// </summary>
    public static class StateHasher
    {
        public static string Hash(BattleEngine e) => Fnv1a64(Describe(e)).ToString("x16", CultureInfo.InvariantCulture);

        public static string Describe(BattleEngine e)
        {
            var sb = new StringBuilder();
            var s = e.State;
            var p = s.Player;
            sb.Append("T").Append(N(s.Turn)).Append('|').Append(s.Phase).Append('|').Append(s.Outcome).Append('\n');
            sb.Append("P ").Append(N(p.Hp)).Append('/').Append(N(p.MaxHp))
              .Append(" B").Append(N(p.Block)).Append(" E").Append(N(p.Energy)).Append('+').Append(N(p.EnergyNextTurn))
              .Append(" S").Append(N(p.Sanity)).Append('/').Append(N(p.MaxSanity));
            AppendStatuses(sb, p.Statuses);
            AppendPile(sb, "draw", p.DrawPile);
            AppendPile(sb, "hand", p.Hand);
            AppendPile(sb, "disc", p.DiscardPile);
            AppendPile(sb, "exh", p.ExhaustPile);
            foreach (var en in s.Enemies)
            {
                sb.Append("\nE").Append(N(en.Index)).Append(' ').Append(N(en.Hp)).Append('/').Append(N(en.MaxHp))
                  .Append(" B").Append(N(en.Block)).Append(" F").Append(N(en.Fear)).Append('/').Append(N(en.Will))
                  .Append(en.Stunned ? " stun" : "").Append(" I:").Append(en.Intent?.Id ?? "-");
                AppendStatuses(sb, en.Statuses);
            }
            sb.Append("\nRNG ").Append(e.Rng.Shuffle.State).Append(',').Append(e.Rng.Ai.State).Append(',').Append(e.Rng.Misc.State);
            return sb.ToString();
        }

        static void AppendStatuses(StringBuilder sb, StatusSet set)
        {
            foreach (var kv in set.Ordered()) sb.Append(' ').Append(kv.Key).Append('=').Append(N(kv.Value));
        }

        static void AppendPile(StringBuilder sb, string name, List<Card> pile)
        {
            sb.Append("\n").Append(name).Append(':');
            foreach (var c in pile) sb.Append(' ').Append(c.Id).Append(c.Upgraded ? "+" : "").Append('#').Append(N(c.Uid));
        }

        static string N(int v) => v.ToString(CultureInfo.InvariantCulture);

        static ulong Fnv1a64(string text)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash = unchecked(hash * prime);
            }
            return hash;
        }
    }
}
