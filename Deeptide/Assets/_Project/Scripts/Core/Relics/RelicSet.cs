using Deeptide.Core.Actions;

namespace Deeptide.Core.Relics
{
    // 遗物实例每场战斗新建一次，所以「每场战斗第一次」类的标记直接放在字段里（策划/05 §1）。

    // ==================== 普通 ====================

    /// <summary>旧烟斗：战斗开始时获得心防。</summary>
    public sealed class OldPipe : Relic
    {
        public override void OnCombatStart(BattleEngine e)
        {
            Trigger(e, $"获得 {V("Ward")} 层心防");
            e.Queue.AddToBottom(new ApplyStatusAction(e.Player, StatusId.Ward, V("Ward")));
        }
    }

    /// <summary>盐圈：第一回合开始时获得格挡（不是卡牌格挡）。</summary>
    public sealed class SaltCircle : Relic
    {
        public override void OnPlayerTurnStart(BattleEngine e)
        {
            if (e.State.Turn != 1) return;
            Trigger(e, $"获得 {V("Block")} 格挡");
            e.Queue.AddToBottom(new GainBlockAction(e.Player, V("Block"), false));
        }
    }

    /// <summary>黑蜡烛：战斗开始时对所有敌人施加恐惧。</summary>
    public sealed class BlackCandle : Relic
    {
        public override void OnCombatStart(BattleEngine e)
        {
            Trigger(e, $"所有敌人 +{V("Fear")} 恐惧");
            e.Queue.AddToBottom(new FearAllEnemiesAction(V("Fear")));
        }
    }

    /// <summary>鲸油灯：癫狂时敌人意图不再模糊。</summary>
    public sealed class WhaleOilLamp : Relic
    {
        public override bool RevealsIntents(BattleEngine e) => true;
    }

    /// <summary>破旧祈祷书：每场战斗第一次恢复理智时额外恢复。</summary>
    public sealed class WornPrayerBook : Relic
    {
        bool _used;

        public override int ModifyRestore(BattleEngine e, int amount)
        {
            if (_used) return amount;
            _used = true;
            Trigger(e, $"额外恢复 {V("Bonus")} 理智");
            return amount + V("Bonus");
        }
    }

    /// <summary>铅制护符：崩溃造成的生命损失减半（向下取整）。</summary>
    public sealed class LeadAmulet : Relic
    {
        public override void ModifyCollapse(BattleEngine e, CollapseCost cost)
        {
            if (cost.HpLoss <= 0) return;
            cost.HpLoss /= 2;
            Trigger(e, "崩溃的生命损失减半");
        }
    }

    /// <summary>渔夫的护身符：战斗胜利时，若本场没有崩溃过，恢复生命。</summary>
    public sealed class FishermansCharm : Relic
    {
        public override void OnCombatEnd(BattleEngine e, bool victory)
        {
            if (!victory || e.State.Stats.Collapses > 0) return;
            Trigger(e, $"本场没有崩溃，恢复 {V("Heal")} 生命");
            e.Queue.AddToBottom(new HealAction(e.Player, V("Heal")));
        }
    }

    // ==================== 罕见 ====================

    /// <summary>破碎怀表：战斗开始时失去理智、获得力量。</summary>
    public sealed class BrokenWatch : Relic
    {
        public override void OnCombatStart(BattleEngine e)
        {
            Trigger(e, $"失去 {V("Loss")} 理智，获得 {V("Strength")} 力量");
            e.Queue.AddToBottom(new LoseSanityAction(V("Loss"), SanitySource.Effect));
            e.Queue.AddToBottom(new ApplyStatusAction(e.Player, StatusId.Strength, V("Strength")));
        }
    }

    /// <summary>第三只眼：癫狂时每回合多抽牌（抽牌那一刻判定）。</summary>
    public sealed class ThirdEye : Relic
    {
        public override int BonusDraw(BattleEngine e) => e.Player.IsLunatic ? V("Draw") : 0;
    }

    /// <summary>银十字：回合结束时若清明，获得格挡。</summary>
    public sealed class SilverCross : Relic
    {
        public override void OnPlayerTurnEnd(BattleEngine e) =>
            e.Queue.AddToBottom(new DelegateAction(en =>
            {
                if (!en.Player.IsClarity) return;
                Trigger(en, $"清明，获得 {V("Block")} 格挡");
                en.Queue.AddToTop(new GainBlockAction(en.Player, V("Block"), false));
            }));
    }

    /// <summary>恐惧面具：每当敌人被震慑，获得能量（回合结束后获得的顺延到下回合）。</summary>
    public sealed class FearMask : Relic
    {
        public override void OnEnemyStunned(BattleEngine e, EnemyState enemy)
        {
            Trigger(e, $"{enemy.Name} 被震慑，+{V("Energy")} 能量");
            e.Queue.AddToTop(new GainEnergyAction(V("Energy")));
        }
    }

    /// <summary>祝圣银铃：每场战斗第一次受到精神攻击时，改为恢复等量理智（先于心防判定）。</summary>
    public sealed class ConsecratedBell : Relic
    {
        bool _used;

        public override bool InterceptMindAttack(BattleEngine e, int amount)
        {
            if (_used) return false;
            _used = true;
            Trigger(e, $"精神攻击 {amount} 改为恢复 {amount} 理智");
            e.Queue.AddToTop(new RestoreSanityAction(amount));
            return true;
        }
    }

    // ==================== 稀有 ====================

    /// <summary>守夜人提灯：回合结束时若清明，下回合多抽牌。</summary>
    public sealed class WatchersLantern : Relic
    {
        bool _pending;

        public override void OnPlayerTurnEnd(BattleEngine e) =>
            e.Queue.AddToBottom(new DelegateAction(en => _pending = en.Player.IsClarity));

        public override int BonusDraw(BattleEngine e)
        {
            if (!_pending) return 0;
            _pending = false;
            return V("Draw");
        }
    }

    /// <summary>深渊残章：本场战斗每累计失去 3 点理智，获得力量。</summary>
    public sealed class AbyssalFragment : Relic
    {
        int _lost;

        public override void OnSanityLost(BattleEngine e, int amount, SanitySource source)
        {
            _lost += amount;
            int n = _lost / V("Per");
            _lost %= V("Per");
            if (n <= 0) return;
            Trigger(e, $"累计失去理智，获得 {n * V("Strength")} 力量");
            e.Queue.AddToTop(new ApplyStatusAction(e.Player, StatusId.Strength, n * V("Strength")));
        }
    }

    /// <summary>恐怖王冠：被震慑的敌人在下次行动前受到的伤害 +50%（乘法阶段）。</summary>
    public sealed class CrownOfDread : Relic
    {
        public override void DamageMultiplier(BattleEngine e, Combatant source, Combatant target, DamageKind kind, ref long num, ref long den)
        {
            if (!(target is EnemyState en) || !en.Stunned) return;
            num *= 100 + V("Percent");
            den *= 100;
        }
    }

    /// <summary>溺者之戒：每场战斗第一次崩溃时，不失去生命，也不获得疯语。</summary>
    public sealed class DrownedRing : Relic
    {
        bool _used;

        public override void ModifyCollapse(BattleEngine e, CollapseCost cost)
        {
            if (_used) return;
            _used = true;
            cost.HpLoss = 0;
            cost.AddCurse = false;
            Trigger(e, "这次崩溃没有代价");
        }
    }

    // ==================== 商店专属（只作用在局层，战斗里没有效果） ====================

    /// <summary>典当行的天平：商店价格降低（RunState 读取）。</summary>
    public sealed class PawnbrokersScale : Relic { }

    /// <summary>炼金腰带：药水栏 +1（RunState 读取）。</summary>
    public sealed class AlchemistBelt : Relic { }

    /// <summary>收藏家的放大镜：卡牌奖励多 1 个选项（RunState 读取）。</summary>
    public sealed class CollectorsLens : Relic { }

    // ==================== 狼人（策划/14 §2.7） ====================

    /// <summary>起始遗物「狼牙吊坠」：每轮第一次变身时抽牌，第一次复归时获得格挡（遗物格挡，不减半）。</summary>
    public sealed class WolfFangPendant : Relic
    {
        public override void OnTransform(BattleEngine e)
        {
            Trigger(e, $"变身，抽 {V("Draw")} 张牌");
            e.Queue.AddToTop(new DrawCardsAction(V("Draw")));
        }

        public override void OnRevert(BattleEngine e)
        {
            Trigger(e, $"复归，获得 {V("Block")} 格挡");
            e.Queue.AddToTop(new GainBlockAction(e.Player, V("Block"), false));
        }
    }

    /// <summary>狼毛斗篷：回合结束时若处于兽形，获得格挡（遗物格挡，不减半）。</summary>
    public sealed class WolfPeltCloak : Relic
    {
        public override void OnPlayerTurnEnd(BattleEngine e) =>
            e.Queue.AddToBottom(new DelegateAction(en =>
            {
                if (!en.Player.IsLunatic) return;
                Trigger(en, $"兽形，获得 {V("Block")} 格挡");
                en.Queue.AddToTop(new GainBlockAction(en.Player, V("Block"), false));
            }));
    }

    /// <summary>血碗：敌人因流血死亡时恢复生命。</summary>
    public sealed class BloodBowl : Relic
    {
        public override void OnEnemyBled(BattleEngine e, EnemyState enemy, int amount)
        {
            if (!enemy.IsDead) return;
            Trigger(e, $"{enemy.Name} 流血而死，恢复 {V("Heal")} 生命");
            e.Heal(e.Player, V("Heal"));   // 当场回血：死的是最后一个敌人时战斗立刻结束，排队的效果会被清掉
        }
    }

    // ==================== 角色特性（隐藏遗物，策划/14） ====================

    /// <summary>
    /// 狼人特性「兽血」（策划/14 §2.2）：癫狂即兽形。兽形时攻击每段 +Damage（和力量一样每段都加，卡面预览同步）；
    /// 打出的牌获得的格挡减半，向下取整（在敏捷、脆弱之后）。减半是规则本身，不当作数值调。
    /// 都按结算那一刻的形态判断：先挡后变身，格挡是满的；人狼同体在人形时结算兽形面，也拿不到 +Damage。
    /// </summary>
    public sealed class BeastBlood : Relic
    {
        public override int DamageBonus(BattleEngine e, Combatant source, Combatant target, DamageKind kind) =>
            kind == DamageKind.Attack && source == e.Player && e.Player.IsLunatic ? V("Damage") : 0;

        public override void CardBlockMultiplier(BattleEngine e, Combatant target, ref long num, ref long den)
        {
            if (target == e.Player && e.Player.IsLunatic) den *= 2;
        }
    }
}
