using Deeptide.Core.Cards;

namespace Deeptide.Core
{
    /// <summary>崩溃的代价，由监听器在结算前修改（铅制护符减半、溺者之戒免除）。</summary>
    public sealed class CollapseCost
    {
        public int HpLoss;
        public bool AddCurse = true;
    }

    /// <summary>
    /// 战斗触发器基类：遗物、能力等按 Priority 从小到大收到通知（策划/08 §3.2）。
    /// 通知类方法（On...）需要产生效果时往 engine.Queue 里加动作，不要直接改状态。
    /// 修正类方法（Modify... / 返回值的方法）只读状态、返回修正结果，卡面预览和实际结算都调用它们（策划/08 §3.3）。
    /// 修正点按需要显式加虚方法，不做通用的效果 DSL（策划/09 M1.3）。
    /// </summary>
    public abstract class BattleListener
    {
        public virtual int Priority => 0;

        /// <summary>复制战斗时用：遗物 / 能力的字段都是值类型或只读引用，浅复制就够了。</summary>
        internal BattleListener CloneListener() => (BattleListener)MemberwiseClone();

        // ---------------- 通知
        public virtual void OnCombatStart(BattleEngine e) { }
        public virtual void OnPlayerTurnStart(BattleEngine e) { }
        public virtual void OnPlayerTurnEnd(BattleEngine e) { }
        public virtual void OnCardPlayed(BattleEngine e, Card card, bool madFace) { }
        public virtual void OnSanityLost(BattleEngine e, int amount, SanitySource source) { }
        /// <summary>amount 是牌面恢复量（含溢出），overflow 是超出上限的部分（策划/01 §2.3）。</summary>
        public virtual void OnSanityRestored(BattleEngine e, int amount, int overflow) { }
        public virtual void OnLunacyChanged(BattleEngine e, bool entered) { }
        /// <summary>每轮第一次进入癫狂（狼人的「变身」，策划/14 §1.2）。崩溃把理智重置到 3 不算。</summary>
        public virtual void OnTransform(BattleEngine e) { }
        /// <summary>每轮第一次离开癫狂（狼人的「复归」）。</summary>
        public virtual void OnRevert(BattleEngine e) { }
        /// <summary>敌人在自己回合开始时因流血失去生命（嗜血）。amount 是实际失去的生命。</summary>
        public virtual void OnEnemyBled(BattleEngine e, EnemyState enemy, int amount) { }
        /// <summary>攻击伤害每打完一段（双方都通知；效果伤害不通知）。hpLoss = 打穿格挡扣掉的生命（倒刺爪）。</summary>
        public virtual void OnAttackHit(BattleEngine e, Combatant source, Combatant target, int hpLoss) { }
        public virtual void OnEnemyStunned(BattleEngine e, EnemyState enemy) { }
        public virtual void OnEnemyDied(BattleEngine e, EnemyState enemy) { }
        /// <summary>战斗结束时（胜利或失败）。这里加进队列的动作会在战斗结束后立即结算。</summary>
        public virtual void OnCombatEnd(BattleEngine e, bool victory) { }

        // ---------------- 修正
        /// <summary>这张牌是否强制使用癫狂面（旧日注视）。</summary>
        public virtual bool ForcesMadFace(BattleEngine e, Card card) => false;
        /// <summary>这张双面牌是否两面都结算：先清醒面（人形面），再癫狂面（兽形面），各付各的代价（人狼同体，策划/14 §2.5）。</summary>
        public virtual bool ResolvesBothFaces(BattleEngine e, Card card) => false;
        /// <summary>癫狂时是否仍能看清敌人意图（鲸油灯）。</summary>
        public virtual bool RevealsIntents(BattleEngine e) => false;
        /// <summary>出牌费用修正（X 费牌不经过这里）。</summary>
        public virtual int ModifyCost(BattleEngine e, Card card, int cost) => cost;
        /// <summary>伤害公式「加法」阶段的额外加值（在力量之后、虚弱之前）。</summary>
        public virtual int DamageBonus(BattleEngine e, Combatant source, Combatant target, DamageKind kind) => 0;
        /// <summary>伤害公式「乘法」阶段：乘上 num / den（在易伤之后）。</summary>
        public virtual void DamageMultiplier(BattleEngine e, Combatant source, Combatant target, DamageKind kind, ref long num, ref long den) { }
        /// <summary>卡牌格挡的乘法修正：乘上 num / den（在脆弱之后；狼人兽形减半）。只作用于打出的牌给的格挡。</summary>
        public virtual void CardBlockMultiplier(BattleEngine e, Combatant target, ref long num, ref long den) { }
        /// <summary>玩家回合开始时额外抽的牌数（第三只眼、守夜人提灯）。</summary>
        public virtual int BonusDraw(BattleEngine e) => 0;
        /// <summary>恢复理智的数量修正（破旧祈祷书）。</summary>
        public virtual int ModifyRestore(BattleEngine e, int amount) => amount;
        /// <summary>崩溃代价修正（铅制护符、溺者之戒）。</summary>
        public virtual void ModifyCollapse(BattleEngine e, CollapseCost cost) { }
        /// <summary>精神攻击在心防之前的改写（祝圣银铃）：返回 true 表示这次精神攻击已被处理，不再扣理智。</summary>
        public virtual bool InterceptMindAttack(BattleEngine e, int amount) => false;
        /// <summary>理智最低能被扣到几（永夜 = 1：不会崩溃）。取所有监听器里最大的；0 = 没有限制。</summary>
        public virtual int SanityFloor(BattleEngine e) => 0;
    }
}
