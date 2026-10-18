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
        /// <summary>招式数值（enemies.csv 的 vars 列），由 EnemyBehaviour.V() 读取。</summary>
        public Dictionary<string, int> Vars = new Dictionary<string, int>();
        /// <summary>招式 ID → 显示名（enemies.csv 的 moves 列）。代码搭建招式表时只能用这里声明过的招式。</summary>
        public Dictionary<string, string> MoveNames = new Dictionary<string, string>();
        public Func<EnemyBehaviour> Factory;
    }

    /// <summary>敌人注册表，内容由 Data.GameData 从 enemies.csv 载入。</summary>
    public static class EnemyLibrary
    {
        static readonly Dictionary<string, EnemyDef> Defs = new Dictionary<string, EnemyDef>();
        static readonly List<EnemyDef> Ordered = new List<EnemyDef>();

        public static IReadOnlyList<EnemyDef> All => Ordered;

        public static bool Exists(string id) => Defs.ContainsKey(id);

        public static EnemyDef Get(string id)
        {
            if (Defs.TryGetValue(id, out var def)) return def;
            if (Defs.Count == 0) throw new InvalidOperationException("敌人数据还没有载入（先调用 GameData.Load）");
            throw new KeyNotFoundException($"未知敌人：{id}");
        }

        /// <summary>按定义创建并搭建好招式表的 AI 实例。</summary>
        public static EnemyBehaviour CreateBehaviour(EnemyDef def)
        {
            var b = def.Factory();
            b.Init(def);
            return b;
        }

        internal static void Clear()
        {
            Defs.Clear();
            Ordered.Clear();
        }

        internal static void Add(EnemyDef def)
        {
            Defs.Add(def.Id, def);
            Ordered.Add(def);
        }
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

        /// <summary>复制战斗时用：招式表搭好后不再变化，可以共享；子类自己的字段都是值类型，浅复制就够了。</summary>
        internal EnemyBehaviour CloneBehaviour() => (EnemyBehaviour)MemberwiseClone();

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

        /// <summary>招式的显示名从 enemies.csv 的 moves 列读取；没有声明过的招式直接报错。</summary>
        protected void AddMove(EnemyMove move)
        {
            if (!Def.MoveNames.TryGetValue(move.Id, out var name))
                throw new InvalidOperationException($"{Def.Id} 的招式 {move.Id} 没有在 enemies.csv 的 moves 列里声明");
            move.Name = name;
            _moves.Add(move.Id, move);
        }

        protected void AddOption(string moveId, int weight, int maxConsecutive = 2, int maxUses = 0,
                                 Func<BattleEngine, EnemyState, bool> condition = null)
        {
            if (!_moves.ContainsKey(moveId)) throw new InvalidOperationException($"招式表引用了未定义的招式 {moveId}");
            _options.Add(new MoveOption { MoveId = moveId, Weight = weight, MaxConsecutive = maxConsecutive, MaxUses = maxUses, Condition = condition });
        }

        public EnemyMove GetMove(string id) => _moves[id];

        public IEnumerable<EnemyMove> Moves => _moves.Values;

        /// <summary>被动能力的说明（显示在敌人身上），没有返回 null。</summary>
        public virtual string Passive => null;

        /// <summary>攻击伤害的额外加值（如失魂镇民的「嗅到疯狂」），在伤害公式的「加法」阶段生效。</summary>
        public virtual int BonusAttackDamage(BattleEngine e, EnemyState self) => 0;

        public abstract void Perform(BattleEngine e, EnemyState self, EnemyMove move);

        // ---------------- 扩展点（策划/09 M1.5）。通知类钩子要产生效果时往队列里加动作；计数器可以直接用 e.SetCounter 改

        /// <summary>刚生成时（开场或被召唤）。</summary>
        public virtual void OnSpawned(BattleEngine e, EnemyState self) { }
        /// <summary>玩家回合开始时（光环类被动：灯塔守望者、深海之歌）。在侵蚀之后入队。</summary>
        public virtual void OnPlayerTurnStart(BattleEngine e, EnemyState self) { }
        /// <summary>自己的回合结束、决定下回合意图之前（无论这回合是否被震慑）。</summary>
        public virtual void OnOwnTurnEnd(BattleEngine e, EnemyState self) { }
        /// <summary>任意敌人倒下时，活着的敌人各收到一次（Boss 的潮位）。</summary>
        public virtual void OnEnemyDied(BattleEngine e, EnemyState self, EnemyState dead) { }
        /// <summary>自己受到伤害、失去生命且还活着时（阶段转换）。</summary>
        public virtual void OnDamaged(BattleEngine e, EnemyState self) { }
        /// <summary>自己被震慑时（无面修女现形）。</summary>
        public virtual void OnStunned(BattleEngine e, EnemyState self) { }

        /// <summary>意图对玩家完全隐藏（显示「?」，连类型都看不到）。</summary>
        public virtual bool IntentHidden(BattleEngine e, EnemyState self) => false;
        /// <summary>招式的每段基础伤害；随场面变化的招式（收网）重写。</summary>
        public virtual int IntentDamage(BattleEngine e, EnemyState self, EnemyMove move) => move.Damage;
        public virtual int IntentHits(BattleEngine e, EnemyState self, EnemyMove move) => move.Hits;
        /// <summary>强制招式（固定循环、计数器触发），返回 null 表示走正常的招式表。首回合固定招式之后检查。</summary>
        protected virtual string ForcedMove(BattleEngine e, EnemyState self) => null;

        public EnemyMove ChooseNext(BattleEngine e, EnemyState self)
        {
            if (self.MoveHistory.Count == 0 && FirstMove != null) return _moves[FirstMove];
            var forced = ForcedMove(e, self);
            if (forced != null) return _moves[forced];

            var candidates = _options.Where(o => Allowed(o, e, self, true, true)).ToList();
            if (candidates.Count == 0) candidates = _options.Where(o => Allowed(o, e, self, false, true)).ToList();
            if (candidates.Count == 0) candidates = _options.Where(o => Allowed(o, e, self, false, false)).ToList();
            if (candidates.Count == 0) candidates = _options;

            int roll = e.Rng.Ai.NextInt(candidates.Sum(o => o.Weight));
            foreach (var o in candidates)
            {
                if (roll < o.Weight) return _moves[o.MoveId];
                roll -= o.Weight;
            }
            return _moves[candidates[candidates.Count - 1].MoveId];
        }

        static bool Allowed(MoveOption o, BattleEngine e, EnemyState self, bool checkRepeat, bool checkUses)
        {
            if (o.Condition != null && !o.Condition(e, self)) return false;
            if (checkUses && o.MaxUses > 0 && self.UsesOf(o.MoveId) >= o.MaxUses) return false;
            if (checkRepeat && TrailingCount(self.MoveHistory, o.MoveId) >= o.MaxConsecutive) return false;
            return true;
        }

        protected static int TrailingCount(List<string> history, string moveId)
        {
            int n = 0;
            for (int i = history.Count - 1; i >= 0 && history[i] == moveId; i--) n++;
            return n;
        }

        /// <summary>固定循环：按 cycle 顺序轮流（历史长度对 cycle 取余）。</summary>
        protected static string Cycle(EnemyState self, params string[] cycle) => cycle[self.MoveHistory.Count % cycle.Length];

        // ---------------- 常用效果

        /// <summary>按当前（可能随场面变化的）伤害和段数攻击玩家。</summary>
        protected void Attack(BattleEngine e, EnemyState self, EnemyMove m) =>
            e.Queue.AddToBottom(new DealDamageAction(self, e.Player, IntentDamage(e, self, m), IntentHits(e, self, m)));

        protected static void MindAttack(BattleEngine e, int amount) =>
            e.Queue.AddToBottom(new LoseSanityAction(amount, SanitySource.Enemy));

        /// <summary>光环：敌方来源的理智损失，心防可以抵消，祝圣银铃不改写。</summary>
        protected static void Aura(BattleEngine e, int amount) =>
            e.Queue.AddToBottom(new LoseSanityAction(amount, SanitySource.Aura));

        protected static void GainBlock(BattleEngine e, EnemyState self, int amount) =>
            e.Queue.AddToBottom(new GainBlockAction(self, amount, false));

        protected static void Debuff(BattleEngine e, StatusId status, int amount) =>
            e.Queue.AddToBottom(new ApplyStatusAction(e.Player, status, amount));

        protected static void Buff(BattleEngine e, EnemyState self, StatusId status, int amount) =>
            e.Queue.AddToBottom(new ApplyStatusAction(self, status, amount));

        protected static void Heal(BattleEngine e, EnemyState self, int amount) =>
            e.Queue.AddToBottom(new HealAction(self, amount));

        protected static void Summon(BattleEngine e, EnemyState self, string enemyId) =>
            e.Queue.AddToBottom(new SummonAction(self, enemyId));

        protected static void AddCards(BattleEngine e, string cardId, PileKind pile, int count)
        {
            for (int i = 0; i < count; i++) e.Queue.AddToBottom(new AddCardToPileAction(cardId, pile));
        }
    }
}
