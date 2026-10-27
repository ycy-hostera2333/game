using System;

namespace Deeptide.Core
{
    /// <summary>
    /// 伤害公式（策划/02 §3.1）：基础值 + 力量 + 加值修正 → ×0.75（攻击者虚弱）→ ×1.5（目标易伤）→ 乘法修正 → 向下取整。
    /// 效果伤害（DamageKind.Effect）不吃力量、虚弱、易伤，只吃遗物 / 能力的修正。
    /// 用分子 / 分母做整数运算，结果精确且跨平台一致。卡面预览、意图显示和实际结算都走这里。
    /// </summary>
    public static class DamageCalculator
    {
        public static int Compute(BattleEngine engine, Combatant source, Combatant target, int baseAmount, DamageKind kind = DamageKind.Attack)
        {
            long num = baseAmount;
            long den = 1;
            if (kind == DamageKind.Attack && source != null)
            {
                num += source.Statuses.Get(StatusId.Strength);
                if (source is EnemyState enemy && enemy.Behaviour != null)
                    num += enemy.Behaviour.BonusAttackDamage(engine, enemy);
            }
            if (engine != null)
                foreach (var l in engine.Listeners) num += l.DamageBonus(engine, source, target, kind);
            if (kind == DamageKind.Attack)
            {
                if (source != null && source.Statuses.Get(StatusId.Weak) > 0) { num *= 3; den *= 4; }
                if (target != null && target.Statuses.Get(StatusId.Vulnerable) > 0) { num *= 3; den *= 2; }
            }
            if (engine != null)
                foreach (var l in engine.Listeners) l.DamageMultiplier(engine, source, target, kind, ref num, ref den);
            return num <= 0 ? 0 : (int)(num / den);
        }
    }

    /// <summary>
    /// 格挡公式（策划/02 §3.2）：只有卡牌给的格挡受敏捷、脆弱和监听器的乘法修正影响（狼人兽形减半，策划/14 §2.2）。
    /// 卡面预览和实际结算都走这里。
    /// </summary>
    public static class BlockCalculator
    {
        public static int Compute(Combatant target, int baseAmount, bool fromCard) => Compute(null, target, baseAmount, fromCard);

        public static int Compute(BattleEngine engine, Combatant target, int baseAmount, bool fromCard)
        {
            if (!fromCard) return Math.Max(0, baseAmount);
            long num = baseAmount + target.Statuses.Get(StatusId.Dexterity);
            long den = 1;
            if (target.Statuses.Get(StatusId.Frail) > 0) { num *= 3; den *= 4; }
            if (engine != null)
                foreach (var l in engine.Listeners) l.CardBlockMultiplier(engine, target, ref num, ref den);
            return num <= 0 ? 0 : (int)(num / den);
        }
    }
}
