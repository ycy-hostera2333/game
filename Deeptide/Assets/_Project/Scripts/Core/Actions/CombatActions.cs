using Deeptide.Core.Cards;

namespace Deeptide.Core.Actions
{
    /// <summary>造成伤害：每段单独走伤害公式；目标死亡后剩余段数作废。</summary>
    public sealed class DealDamageAction : GameAction
    {
        readonly Combatant _source;
        readonly Combatant _target;
        readonly int _baseAmount;
        readonly int _hits;

        public DealDamageAction(Combatant source, Combatant target, int baseAmount, int hits = 1)
        {
            _source = source;
            _target = target;
            _baseAmount = baseAmount;
            _hits = hits;
        }

        public override void Execute(BattleEngine e)
        {
            for (int i = 0; i < _hits; i++)
            {
                if (_target == null || _target.IsDead) return;
                if (_source != null && _source.IsDead) return;
                int dmg = DamageCalculator.Compute(e, _source, _target, _baseAmount);
                e.ApplyDamage(_source, _target, dmg);
            }
        }
    }

    public sealed class GainBlockAction : GameAction
    {
        readonly Combatant _target;
        readonly int _baseAmount;
        readonly bool _fromCard;

        public GainBlockAction(Combatant target, int baseAmount, bool fromCard)
        {
            _target = target;
            _baseAmount = baseAmount;
            _fromCard = fromCard;
        }

        public override void Execute(BattleEngine e)
        {
            if (_target.IsDead) return;
            int amount = BlockCalculator.Compute(_target, _baseAmount, _fromCard);
            if (amount <= 0) return;
            _target.Block += amount;
            e.Log.Add(BattleEventType.BlockGained, $"{_target.Name} 获得 {amount} 格挡（共 {_target.Block}）", _target.Name, amount: amount);
        }
    }

    /// <summary>直接失去生命（不经过格挡），如自伤、崩溃。</summary>
    public sealed class LoseHpAction : GameAction
    {
        readonly Combatant _target;
        readonly int _amount;

        public LoseHpAction(Combatant target, int amount)
        {
            _target = target;
            _amount = amount;
        }

        public override void Execute(BattleEngine e) => e.LoseHp(_target, _amount);
    }

    public sealed class GainEnergyAction : GameAction
    {
        readonly int _amount;

        public GainEnergyAction(int amount) { _amount = amount; }

        public override void Execute(BattleEngine e)
        {
            if (e.State.EnemyTurn)
            {
                // 回合开始会重置能量，敌人回合里获得的能量顺延到下回合，避免白白浪费（试玩发现 #3）
                e.Player.EnergyNextTurn += _amount;
                e.Log.Add(BattleEventType.EnergyGained, $"获得 {_amount} 能量（敌人回合中获得，下回合开始时生效）", amount: _amount);
                return;
            }
            e.Player.Energy += _amount;
            e.Log.Add(BattleEventType.EnergyGained, $"获得 {_amount} 能量（现有 {e.Player.Energy}）", amount: _amount);
        }
    }

    public sealed class ApplyStatusAction : GameAction
    {
        readonly Combatant _target;
        readonly StatusId _status;
        readonly int _amount;

        public ApplyStatusAction(Combatant target, StatusId status, int amount)
        {
            _target = target;
            _status = status;
            _amount = amount;
        }

        public override void Execute(BattleEngine e)
        {
            if (_target.IsDead || _amount == 0) return;
            _target.Statuses.Add(_status, _amount);
            e.Log.Add(BattleEventType.StatusApplied,
                $"{_target.Name} 获得 {_amount} 层{StatusSet.NameOf(_status)}（共 {_target.Statuses.Get(_status)}）",
                target: _target.Name, amount: _amount);
        }
    }

    /// <summary>施加恐惧（策划/02 §3.4）：恐惧 ≥ 意志时立即震慑，恐惧清零，意志 ×1.5 向上取整。</summary>
    public sealed class ApplyFearAction : GameAction
    {
        readonly EnemyState _target;
        readonly int _amount;

        public ApplyFearAction(EnemyState target, int amount)
        {
            _target = target;
            _amount = amount;
        }

        public override void Execute(BattleEngine e)
        {
            var en = _target;
            if (en == null || en.IsDead || _amount <= 0) return;
            if (en.Mindless)
            {
                e.Log.Add(BattleEventType.FearApplied, $"{en.Name} 没有心智，免疫恐惧", target: en.Name);
                return;
            }
            if (en.Stunned) return;

            en.Fear += _amount;
            e.Log.Add(BattleEventType.FearApplied, $"{en.Name} 恐惧 +{_amount}（{en.Fear}/{en.Will}）", target: en.Name, amount: _amount);
            if (en.Fear < en.Will) return;

            var cfg = e.Config;
            en.Stunned = true;
            en.Fear = 0;
            en.Will = (en.Will * cfg.WillGrowthNum + cfg.WillGrowthDen - 1) / cfg.WillGrowthDen;
            e.Log.Add(BattleEventType.EnemyStunned, $"{en.Name} 被震慑！本次意图作废，意志升至 {en.Will}", target: en.Name);
            e.Notify(l => l.OnEnemyStunned(e, en));
        }
    }
}
