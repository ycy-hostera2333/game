using System;
using System.Linq;
using Deeptide.Core.Actions;

namespace Deeptide.Core.Enemies
{
    // 第一章另外两个 Boss（策划/15 §2.3、09 M7.4）：和溺亡司祭三选一、打到才知道。

    /// <summary>
    /// Boss 鳞母：群体型，压堕落。开场带 2 只鳞民幼体（召唤物，打死鳞母就赢）。
    /// 产卵：自己回合结束时计数 +1，满 EggEvery 产 1 枚卵；场上卵 + 幼体最多 MaxBrood 个，满了这次不产、计数照样清零。
    /// 护崽：每有一只幼体被击杀，+Guard 力量（卵被打碎、卵孵化都不算）。同族的气味（你癫狂时幼体攻击 +Scent）写在 Scaleling 里。
    /// </summary>
    public sealed class ScaleMother : EnemyBehaviour
    {
        public const string EggTimer = "EggTimer";

        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Spit", Intent = IntentType.Attack | IntentType.Debuff, Damage = V("Spit"), Note = $"施加 {V("SpitWeak")} 层虚弱" });
            AddMove(new EnemyMove { Id = "Slam", Intent = IntentType.Attack, Damage = V("Slam") });
            AddMove(new EnemyMove { Id = "Call", Intent = IntentType.Buff | IntentType.Defend, Block = V("CallBlock"), Note = $"所有幼体 +{V("CallStrength")} 力量" });
            AddOption("Spit", 3);
            AddOption("Slam", 3);
            AddOption("Call", 2, condition: (e, s) => Count(e, EnemyIds.Scaleling) > 0);
        }

        public override string Passive =>
            $"每 {V("EggEvery")} 回合产 1 枚卵，卵和幼体最多 {V("MaxBrood")} 个；幼体被击杀时 +{V("Guard")} 力量；你癫狂时幼体攻击 +{V("Scent")}、卵 1 回合就孵化";

        public override string CounterText(EnemyState self) => $"产卵 {self.Counter(EggTimer)}/{V("EggEvery")}";

        static int Count(BattleEngine e, string id) => e.State.Enemies.Count(x => !x.IsDead && x.Def.Id == id);

        public override void OnSpawned(BattleEngine e, EnemyState self)
        {
            e.SetCounter(self, EggTimer, 0);
            for (int i = 0; i < V("StartBrood"); i++) Summon(e, self, EnemyIds.Scaleling);
        }

        public override void OnOwnTurnEnd(BattleEngine e, EnemyState self)
        {
            int timer = self.Counter(EggTimer) + 1;
            if (timer < V("EggEvery"))
            {
                e.SetCounter(self, EggTimer, timer);
                return;
            }
            e.SetCounter(self, EggTimer, 0);
            if (Count(e, EnemyIds.Scaleling) + Count(e, EnemyIds.ScaleEgg) >= V("MaxBrood")) return;
            Summon(e, self, EnemyIds.ScaleEgg);
        }

        public override void OnEnemyDied(BattleEngine e, EnemyState self, EnemyState dead)
        {
            if (dead.Def.Id != EnemyIds.Scaleling) return;
            e.Log.Add(BattleEventType.PowerTriggered, $"{dead.Name} 倒下，{self.Name} 护崽：+{V("Guard")} 力量", self.Name).Entity = self.Index;
            e.Queue.AddToTop(new ApplyStatusAction(self, StatusId.Strength, V("Guard")));
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            switch (m.Id)
            {
                case "Spit":
                    Attack(e, self, m);
                    Debuff(e, StatusId.Weak, V("SpitWeak"));
                    break;
                case "Slam":
                    Attack(e, self, m);
                    break;
                case "Call":
                    foreach (var y in e.State.AliveEnemies.Where(x => x.Def.Id == EnemyIds.Scaleling).ToList())
                        Buff(e, y, StatusId.Strength, V("CallStrength"));
                    GainBlock(e, self, m.Block);
                    break;
            }
        }
    }

    /// <summary>
    /// 鳞卵：无心智、不行动。每个自己的回合孵化倒计时 −1，到 0 孵成鳞民幼体（卵消失，不算被击倒）；你癫狂时轮到它就直接孵化。
    /// 卵是在鳞母回合结束时产的，当回合不行动，所以默认「活过 2 个敌人回合」才孵化，癫狂时 1 个。
    /// </summary>
    public sealed class ScaleEgg : EnemyBehaviour
    {
        public const string Hatch = "Hatch";

        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Incubate", Intent = IntentType.Summon, Note = "倒计时到 0 孵成鳞民幼体" });
            AddOption("Incubate", 1, maxConsecutive: int.MaxValue);
        }

        public override string Passive => "不行动；倒计时到 0 孵成鳞民幼体，你癫狂时直接孵化";

        public override string CounterText(EnemyState self) => $"孵化 {self.Counter(Hatch)}";

        public override void OnSpawned(BattleEngine e, EnemyState self) => e.SetCounter(self, Hatch, V("Hatch"));

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            int left = e.Player.IsLunatic ? 0 : self.Counter(Hatch) - 1;
            e.SetCounter(self, Hatch, Math.Max(0, left));
            if (left > 0) return;
            var mother = e.State.AliveEnemies.FirstOrDefault(x => x.Def.Id == EnemyIds.ScaleMother);
            e.Queue.AddToBottom(new DelegateAction(en => en.Despawn(self, $"{self.Name} 孵化了")));
            e.Queue.AddToBottom(new SummonAction(mother, EnemyIds.Scaleling));
        }
    }

    /// <summary>
    /// Boss 倒影：理智型，压传染。
    /// 慢半拍：每次决定意图时，看你这一回合打出的第一张攻击牌（龟裂后是前两张），按你打出时的数值记进计数器，下个回合照着打你；
    /// 你这一回合没打攻击牌就「凝视」（精神攻击）。第一回合固定凝视。龟裂后抄两张时，意图把两张的总伤害显示成一段（实际分两次打）。
    /// 镜面：意志 = 你的当前理智 × WillPerSanity，每被震慑一次再按配置 ×1.5；你的理智一变就重算，恐惧 ≥ 意志立刻震慑。
    /// 理智掉到 0（崩溃前那一刻）不重算，等崩溃重置完再算，免得崩溃顺手把它吓住。
    /// 生命 ≤ 50% 龟裂（第二阶段）。
    /// </summary>
    public sealed class Mirror : EnemyBehaviour
    {
        public const string Phase = "Phase";
        public const string Stuns = "Stuns";
        public const string Copy1 = "Copy1", Copy1Hits = "Copy1Hits", Copy2 = "Copy2", Copy2Hits = "Copy2Hits";

        string _name1, _name2;

        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Gaze", Intent = IntentType.Mind, SanityDamage = V("Gaze") });
            AddMove(new EnemyMove { Id = "Echo", Intent = IntentType.Attack, Note = "重复你上回合打出的第一张攻击牌" });
            AddMove(new EnemyMove { Id = "Echo2", Intent = IntentType.Attack, Note = "重复你上回合打出的前两张攻击牌" });
            FirstMove = "Gaze";
            AddOption("Gaze", 1, maxConsecutive: int.MaxValue);
        }

        public override string Passive =>
            $"慢半拍：重复你上回合打出的第一张攻击牌，按你打出时的数值；没打就凝视。镜面：意志 = 你的理智 × {V("WillPerSanity")}。生命一半以下龟裂：抄前两张";

        public override string CounterText(EnemyState self)
        {
            string phase = self.Counter(Phase) == 2 ? "龟裂 · " : "";
            if (self.Intent?.Id == "Echo") return $"{phase}抄：{_name1} {self.Counter(Copy1)}×{self.Counter(Copy1Hits)}";
            if (self.Intent?.Id == "Echo2")
                return $"{phase}抄：{_name1} {self.Counter(Copy1)}×{self.Counter(Copy1Hits)}、{_name2} {self.Counter(Copy2)}×{self.Counter(Copy2Hits)}";
            return phase.Length > 0 ? "龟裂" : null;
        }

        public override void OnSpawned(BattleEngine e, EnemyState self)
        {
            e.SetCounter(self, Phase, 1);
            e.SetCounter(self, Stuns, 0);
            UpdateWill(e, self, false);
        }

        public override void OnPlayerSanityChanged(BattleEngine e, EnemyState self) => UpdateWill(e, self, true);

        public override void OnStunned(BattleEngine e, EnemyState self)
        {
            e.SetCounter(self, Stuns, self.Counter(Stuns) + 1);
            UpdateWill(e, self, false);
        }

        /// <summary>镜面：按当前理智和震慑次数重算意志；check = true 时恐惧够了立刻震慑。</summary>
        void UpdateWill(BattleEngine e, EnemyState self, bool check)
        {
            if (e.Player.Sanity <= 0) return;
            var cfg = e.Config;
            int will = Math.Max(1, e.Player.Sanity * V("WillPerSanity"));
            for (int i = 0; i < self.Counter(Stuns); i++) will = (will * cfg.WillGrowthNum + cfg.WillGrowthDen - 1) / cfg.WillGrowthDen;
            if (will != self.Will) e.SetFear(self, self.Fear, will);
            if (!check || self.Stunned || self.Fear < self.Will) return;
            e.Log.Add(BattleEventType.PowerTriggered, $"{self.Name} 的意志跟着你的理智跌到 {self.Will}，恐惧 {self.Fear} 压过了它", self.Name).Entity = self.Index;
            ApplyFearAction.Stun(e, self);
        }

        protected override string ForcedMove(BattleEngine e, EnemyState self)
        {
            var atks = e.State.AttacksThisTurn;
            bool cracked = self.Counter(Phase) == 2;
            var a = atks.Count > 0 ? atks[0] : null;
            var b = cracked && atks.Count > 1 ? atks[1] : null;
            e.SetCounter(self, Copy1, a?.PerHit ?? 0);
            e.SetCounter(self, Copy1Hits, a?.Hits ?? 0);
            e.SetCounter(self, Copy2, b?.PerHit ?? 0);
            e.SetCounter(self, Copy2Hits, b?.Hits ?? 0);
            _name1 = a?.Name;
            _name2 = b?.Name;
            return a == null ? "Gaze" : b == null ? "Echo" : "Echo2";
        }

        public override int IntentDamage(BattleEngine e, EnemyState self, EnemyMove move) =>
            move.Id == "Echo" ? self.Counter(Copy1)
            : move.Id == "Echo2" ? self.Counter(Copy1) * self.Counter(Copy1Hits) + self.Counter(Copy2) * self.Counter(Copy2Hits)
            : move.Damage;

        public override int IntentHits(BattleEngine e, EnemyState self, EnemyMove move) =>
            move.Id == "Echo" ? self.Counter(Copy1Hits) : move.Id == "Echo2" ? 1 : move.Hits;

        public override void OnDamaged(BattleEngine e, EnemyState self)
        {
            if (self.Counter(Phase) != 1 || self.Hp * 2 > self.MaxHp) return;
            e.SetCounter(self, Phase, 2);
            e.Log.Add(BattleEventType.PowerTriggered, $"{self.Name} 龟裂了！之后抄你上回合的前两张攻击牌", self.Name).Entity = self.Index;
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            switch (m.Id)
            {
                case "Gaze":
                    MindAttack(e, m.SanityDamage);
                    break;
                case "Echo":
                    e.Queue.AddToBottom(new DealDamageAction(self, e.Player, self.Counter(Copy1), self.Counter(Copy1Hits)));
                    break;
                case "Echo2":
                    e.Queue.AddToBottom(new DealDamageAction(self, e.Player, self.Counter(Copy1), self.Counter(Copy1Hits)));
                    e.Queue.AddToBottom(new DealDamageAction(self, e.Player, self.Counter(Copy2), self.Counter(Copy2Hits)));
                    break;
            }
        }
    }
}
