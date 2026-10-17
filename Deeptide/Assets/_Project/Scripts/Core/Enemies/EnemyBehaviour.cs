using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Actions;

namespace Deeptide.Core.Enemies
{
    public sealed class EnemyDef
    {
        public string Id;
        public string Name;
        public int HpMin;
        public int HpMax;
        /// <summary>意志；≤ 0 表示无心智（免疫恐惧）。</summary>
        public int Will;
        /// <summary>招式数值（对应以后 enemies.csv 的 vars 列），由 EnemyBehaviour.V() 读取。</summary>
        public Dictionary<string, int> Vars = new Dictionary<string, int>();
        public Func<EnemyBehaviour> Factory;
    }

    /// <summary>一个招式：意图显示所需的信息。实际效果写在 EnemyBehaviour.Perform 里。</summary>
    public sealed class EnemyMove
    {
        public string Id;
        public string Name;
        public IntentType Intent;
        public int Damage;
        public int Hits = 1;
        public int SanityDamage;
        public int Block;
        public string Note;
    }

    /// <summary>招式表中的一行（策划/02 §6.2）。</summary>
    public sealed class MoveOption
    {
        public string MoveId;
        public int Weight;
        /// <summary>同一招式最多连续使用次数，默认 2。</summary>
        public int MaxConsecutive = 2;
        /// <summary>每场最多使用次数，0 = 不限。</summary>
        public int MaxUses;
        public Func<BattleEngine, EnemyState, bool> Condition;
    }

    /// <summary>
    /// 敌人 AI 基类。选招顺序（策划/02 §6.2）：强制招式 → 条件过滤 → 重复限制 → 加权随机（aiRng）。
    /// </summary>
    public abstract class EnemyBehaviour
    {
        readonly Dictionary<string, EnemyMove> _moves = new Dictionary<string, EnemyMove>();
        readonly List<MoveOption> _options = new List<MoveOption>();

        /// <summary>第 1 回合固定使用的招式，null = 不固定。</summary>
        protected string FirstMove;

        protected EnemyDef Def { get; private set; }

        /// <summary>由引擎在创建后调用：记住定义，再按定义里的数值搭建招式表。</summary>
        internal void Init(EnemyDef def)
        {
            Def = def;
            Build();
        }

        /// <summary>用 AddMove / AddOption 搭建招式表，数值通过 V() 从定义里读取。</summary>
        protected abstract void Build();

        protected int V(string key)
        {
            if (!Def.Vars.TryGetValue(key, out var v)) throw new KeyNotFoundException($"{Def.Id} 缺少数值 {key}");
            return v;
        }

        protected void AddMove(EnemyMove move) => _moves.Add(move.Id, move);

        protected void AddOption(string moveId, int weight, int maxConsecutive = 2, int maxUses = 0,
                                 Func<BattleEngine, EnemyState, bool> condition = null)
        {
            if (!_moves.ContainsKey(moveId)) throw new InvalidOperationException($"招式表引用了未定义的招式 {moveId}");
            _options.Add(new MoveOption { MoveId = moveId, Weight = weight, MaxConsecutive = maxConsecutive, MaxUses = maxUses, Condition = condition });
        }

        public EnemyMove GetMove(string id) => _moves[id];

        public IEnumerable<EnemyMove> Moves => _moves.Values;

        /// <summary>攻击伤害的额外加值（如失魂镇民的「嗅到疯狂」），在伤害公式的「加法」阶段生效。</summary>
        public virtual int BonusAttackDamage(BattleEngine e, EnemyState self) => 0;

        public abstract void Perform(BattleEngine e, EnemyState self, EnemyMove move);

        public EnemyMove ChooseNext(BattleEngine e, EnemyState self)
        {
            if (self.MoveHistory.Count == 0 && FirstMove != null) return _moves[FirstMove];

            var candidates = _options.Where(o => Allowed(o, e, self, true)).ToList();
            if (candidates.Count == 0) candidates = _options.Where(o => Allowed(o, e, self, false)).ToList();
            if (candidates.Count == 0) candidates = _options;

            int roll = e.Rng.Ai.NextInt(candidates.Sum(o => o.Weight));
            foreach (var o in candidates)
            {
                if (roll < o.Weight) return _moves[o.MoveId];
                roll -= o.Weight;
            }
            return _moves[candidates[candidates.Count - 1].MoveId];
        }

        static bool Allowed(MoveOption o, BattleEngine e, EnemyState self, bool checkRepeat)
        {
            if (o.Condition != null && !o.Condition(e, self)) return false;
            if (o.MaxUses > 0 && self.UsesOf(o.MoveId) >= o.MaxUses) return false;
            if (checkRepeat && TrailingCount(self.MoveHistory, o.MoveId) >= o.MaxConsecutive) return false;
            return true;
        }

        static int TrailingCount(List<string> history, string moveId)
        {
            int n = 0;
            for (int i = history.Count - 1; i >= 0 && history[i] == moveId; i--) n++;
            return n;
        }

        // ---------------- 常用效果

        protected static void Attack(BattleEngine e, EnemyState self, EnemyMove m) =>
            e.Queue.AddToBottom(new DealDamageAction(self, e.Player, m.Damage, m.Hits));

        protected static void MindAttack(BattleEngine e, int amount) =>
            e.Queue.AddToBottom(new LoseSanityAction(amount, SanitySource.Enemy));

        protected static void GainBlock(BattleEngine e, EnemyState self, int amount) =>
            e.Queue.AddToBottom(new GainBlockAction(self, amount, false));
    }
}
