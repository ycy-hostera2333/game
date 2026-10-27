using System.Linq;

namespace Deeptide.Core.Actions
{
    /// <summary>造成伤害：每段单独走伤害公式；目标死亡后剩余段数作废。</summary>
    public sealed class DealDamageAction : GameAction
    {
        readonly Combatant _source;
        readonly Combatant _target;
        readonly int _baseAmount;
        readonly int _hits;
        readonly DamageKind _kind;

        public DealDamageAction(Combatant source, Combatant target, int baseAmount, int hits = 1, DamageKind kind = DamageKind.Attack)
        {
            _source = source;
            _target = target;
            _baseAmount = baseAmount;
            _hits = hits;
            _kind = kind;
        }

        public override void Execute(BattleEngine e)
        {
            for (int i = 0; i < _hits; i++)
            {
                if (_target == null || _target.IsDead) return;
                if (_source != null && _source.IsDead) return;
                int dmg = DamageCalculator.Compute(e, _source, _target, _baseAmount, _kind);
                int hpLoss = e.ApplyDamage(_source, _target, dmg);
                if (_kind == DamageKind.Attack) e.AttackHit(_source, _target, hpLoss);
            }
        }
    }

    /// <summary>对所有敌人造成伤害：每一段打遍当时活着的所有敌人。</summary>
    public sealed class DamageAllEnemiesAction : GameAction
    {
        readonly Combatant _source;
        readonly int _baseAmount;
        readonly int _hits;
        readonly DamageKind _kind;

        public DamageAllEnemiesAction(Combatant source, int baseAmount, int hits = 1, DamageKind kind = DamageKind.Attack)
        {
            _source = source;
            _baseAmount = baseAmount;
            _hits = hits;
            _kind = kind;
        }

        public override void Execute(BattleEngine e)
        {
            for (int i = 0; i < _hits; i++)
            {
                if (_source != null && _source.IsDead) return;
                foreach (var en in e.State.AliveEnemies.ToList())
                {
                    if (en.IsDead) continue;
                    int hpLoss = e.ApplyDamage(_source, en, DamageCalculator.Compute(e, _source, en, _baseAmount, _kind));
                    if (_kind == DamageKind.Attack) e.AttackHit(_source, en, hpLoss);
                }
            }
        }
    }

    /// <summary>对随机一个活着的敌人造成伤害（misc 流）。</summary>
    public sealed class DamageRandomEnemyAction : GameAction
    {
        readonly Combatant _source;
        readonly int _baseAmount;
        readonly DamageKind _kind;

        public DamageRandomEnemyAction(Combatant source, int baseAmount, DamageKind kind)
        {
            _source = source;
            _baseAmount = baseAmount;
            _kind = kind;
        }

        public override void Execute(BattleEngine e)
        {
            var alive = e.State.AliveEnemies.ToList();
            if (alive.Count == 0) return;
            var target = alive[e.Rng.Misc.NextInt(alive.Count)];
            int hpLoss = e.ApplyDamage(_source, target, DamageCalculator.Compute(e, _source, target, _baseAmount, _kind));
            if (_kind == DamageKind.Attack) e.AttackHit(_source, target, hpLoss);
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
            int amount = BlockCalculator.Compute(e, _target, _baseAmount, _fromCard);
            if (amount <= 0) return;
            e.SetBlock(_target, _target.Block + amount);
            e.Log.Add(BattleEventType.BlockGained, $"{_target.Name} 获得 {amount} 格挡（共 {_target.Block}）", _target.Name, amount: amount)
                .Entity = _target.EntityId;
        }
    }

    /// <summary>
    /// 流血结算（策划/14 §1.4）：敌人回合开始时（行动前）失去等同于流血层数的生命，无视格挡，然后流血 −1。
    /// 流血死了的敌人不再行动（EnemyAct 在这个动作之后才让它出招）。
    /// </summary>
    public sealed class BleedAction : GameAction
    {
        readonly EnemyState _target;

        public BleedAction(EnemyState target) { _target = target; }

        public override void Execute(BattleEngine e)
        {
            var en = _target;
            int bleed = en.Statuses.Get(StatusId.Bleed);
            if (en.IsDead || bleed <= 0) return;
            e.Log.Add(BattleEventType.Bled, $"{en.Name} 流血 {bleed}", target: en.Name, amount: bleed).Entity = en.Index;
            int before = en.Hp;
            e.LoseHp(en, bleed);
            int lost = before - en.Hp;
            if (!en.IsDead) e.AddStatus(en, StatusId.Bleed, -1);
            if (lost > 0) e.Notify(l => l.OnEnemyBled(e, en, lost));
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

    public sealed class HealAction : GameAction
    {
        readonly Combatant _target;
        readonly int _amount;

        public HealAction(Combatant target, int amount)
        {
            _target = target;
            _amount = amount;
        }

        public override void Execute(BattleEngine e) => e.Heal(_target, _amount);
    }

    public sealed class GainEnergyAction : GameAction
    {
        readonly int _amount;

        public GainEnergyAction(int amount) { _amount = amount; }

        public override void Execute(BattleEngine e)
        {
            if (_amount <= 0) return;
            if (e.State.EnemyTurn)
            {
                // 回合开始会重置能量，敌人回合里获得的能量顺延到下回合，避免白白浪费（试玩发现 #3）
                e.SetEnergyNextTurn(e.Player.EnergyNextTurn + _amount);
                e.Log.Add(BattleEventType.EnergyGained, $"获得 {_amount} 能量（敌人回合中获得，下回合开始时生效）", amount: _amount);
                return;
            }
            e.SetEnergy(e.Player.Energy + _amount);
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
            if (_target == null || _target.IsDead || _amount == 0) return;
            e.AddStatus(_target, _status, _amount);
            var ev = e.Log.Add(BattleEventType.StatusApplied,
                $"{_target.Name} 获得 {_amount} 层{StatusSet.NameOf(_status)}（共 {_target.Statuses.Get(_status)}）",
                target: _target.Name, amount: _amount);
            ev.Entity = _target.EntityId;
            ev.Status = _status;
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
                e.Log.Add(BattleEventType.FearApplied, $"{en.Name} 没有心智，免疫恐惧", target: en.Name).Entity = en.Index;
                return;
            }
            if (en.Stunned) return;

            e.SetFear(en, en.Fear + _amount, en.Will);
            e.Log.Add(BattleEventType.FearApplied, $"{en.Name} 恐惧 +{_amount}（{en.Fear}/{en.Will}）", target: en.Name, amount: _amount).Entity = en.Index;
            if (en.Fear < en.Will) return;
            Stun(e, en);
        }

        /// <summary>震慑：当前意图作废，恐惧清零，意志按配置增长。惊惧失神等效果也走这里。</summary>
        public static void Stun(BattleEngine e, EnemyState en)
        {
            var cfg = e.Config;
            e.SetStunned(en, true);
            e.SetFear(en, 0, (en.Will * cfg.WillGrowthNum + cfg.WillGrowthDen - 1) / cfg.WillGrowthDen);
            e.Log.Add(BattleEventType.EnemyStunned, $"{en.Name} 被震慑！本次意图作废，意志升至 {en.Will}", target: en.Name).Entity = en.Index;
            en.Behaviour.OnStunned(e, en);
            e.Notify(l => l.OnEnemyStunned(e, en));
        }
    }

    /// <summary>对所有活着的敌人施加恐惧。</summary>
    public sealed class FearAllEnemiesAction : GameAction
    {
        readonly int _amount;

        public FearAllEnemiesAction(int amount) { _amount = amount; }

        public override void Execute(BattleEngine e)
        {
            if (_amount <= 0) return;
            var actions = e.State.AliveEnemies.Select(en => (GameAction)new ApplyFearAction(en, _amount)).ToArray();
            e.Queue.AddToTop(actions);
        }
    }

    /// <summary>对所有活着的敌人施加状态。</summary>
    public sealed class StatusAllEnemiesAction : GameAction
    {
        readonly StatusId _status;
        readonly int _amount;

        public StatusAllEnemiesAction(StatusId status, int amount)
        {
            _status = status;
            _amount = amount;
        }

        public override void Execute(BattleEngine e)
        {
            var actions = e.State.AliveEnemies.Select(en => (GameAction)new ApplyStatusAction(en, _status, _amount)).ToArray();
            e.Queue.AddToTop(actions);
        }
    }

    /// <summary>召唤敌人（召唤物），立即决定它的意图。</summary>
    public sealed class SummonAction : GameAction
    {
        readonly EnemyState _summoner;
        readonly string _enemyId;

        public SummonAction(EnemyState summoner, string enemyId)
        {
            _summoner = summoner;
            _enemyId = enemyId;
        }

        public override void Execute(BattleEngine e)
        {
            if (_summoner != null && _summoner.IsDead) return;
            int ordinal = e.State.Enemies.Count(x => x.Def.Id == _enemyId) + 1;
            var def = Enemies.EnemyLibrary.Get(_enemyId);
            var en = e.SpawnEnemy(_enemyId, true, $"{def.Name} {(char)('A' + (ordinal - 1) % 26)}");
            var ev = e.Log.Add(BattleEventType.EnemySummoned, $"{_summoner?.Name ?? "深处"} 召唤了 {en.Name}（{en.Hp}）", _summoner?.Name, en.Name);
            ev.Entity = en.Index;
            ev.Source = _summoner?.Index ?? BattleEvent.NoEntity;
            e.RollIntent(en);
        }
    }
}
