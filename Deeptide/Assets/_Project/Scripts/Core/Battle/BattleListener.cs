using Deeptide.Core.Cards;

namespace Deeptide.Core
{
    /// <summary>
    /// 战斗触发器基类：遗物、能力等按 Priority 从小到大收到通知（策划/08 §3.2）。
    /// 需要产生效果时往 engine.Queue 里加动作，不要直接改状态。
    /// </summary>
    public abstract class BattleListener
    {
        public virtual int Priority => 0;

        public virtual void OnCombatStart(BattleEngine e) { }
        public virtual void OnPlayerTurnStart(BattleEngine e) { }
        public virtual void OnPlayerTurnEnd(BattleEngine e) { }
        public virtual void OnCardPlayed(BattleEngine e, Card card, bool madFace) { }
        public virtual void OnSanityLost(BattleEngine e, int amount, SanitySource source) { }
        public virtual void OnSanityRestored(BattleEngine e, int amount, int overflow) { }
        public virtual void OnLunacyChanged(BattleEngine e, bool entered) { }
        public virtual void OnEnemyStunned(BattleEngine e, EnemyState enemy) { }
    }
}
