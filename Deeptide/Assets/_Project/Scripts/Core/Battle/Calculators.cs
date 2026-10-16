using System;

namespace Deeptide.Core
{
    /// <summary>
    /// 伤害公式（策划/02 §3.1）：基础值 + 力量 → ×0.75（攻击者虚弱）→ ×1.5（目标易伤）→ 向下取整。
    /// 用分子 / 分母做整数运算，结果精确且跨平台一致。卡面预览和实际结算都走这里。
    /// </summary>
    public static class DamageCalculator
    {
        public static int Compute(BattleEngine engine, Combatant source, Combatant target, int baseAmount)
        {
            long num = baseAmount;
            long den = 1;
            if (source != null)
            {
                num += source.Statuses.Get(StatusId.Strength);
                if (source is EnemyState enemy && enemy.Behaviour != null)
                    num += enemy.Behaviour.BonusAttackDamage(engine, enemy);
                if (source.Statuses.Get(StatusId.Weak) > 0) { num *= 3; den *= 4; }
            }
            if (target != null && target.Statuses.Get(StatusId.Vulnerable) > 0) { num *= 3; den *= 2; }
            return num <= 0 ? 0 : (int)(num / den);
        }
    }

    /// <summary>格挡公式（策划/02 §3.2）：只有卡牌给的格挡受敏捷和脆弱影响。</summary>
    public static class BlockCalculator
    {
        public static int Compute(Combatant target, int baseAmount, bool fromCard)
        {
            if (!fromCard) return Math.Max(0, baseAmount);
            long num = baseAmount + target.Statuses.Get(StatusId.Dexterity);
            long den = 1;
            if (target.Statuses.Get(StatusId.Frail) > 0) { num *= 3; den *= 4; }
            return num <= 0 ? 0 : (int)(num / den);
        }
    }
}
