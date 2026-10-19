using System;
using System.Linq;
using Deeptide.Core.Actions;
using Deeptide.Core.Cards;

namespace Deeptide.Core.Enemies
{
    // 批次 3.4：精英和 Boss（策划/04 §4–5）。百分比权重换成整数：40 / 40 / 20 → 2 / 2 / 1，45 / 35 / 20 → 9 / 7 / 4。

    /// <summary>E01 灯塔守望者：被动「刺目强光」每回合开始让你失去理智（光环：心防能挡，银铃不改写）。考理智维持 + 输出。</summary>
    public sealed class LighthouseKeeper : EnemyBehaviour
    {
        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "OilSplash", Intent = IntentType.Attack | IntentType.Debuff, Damage = V("Oil"), Note = $"施加 {V("OilFrail")} 层脆弱" });
            AddMove(new EnemyMove { Id = "Sweep", Intent = IntentType.Attack, Damage = V("Sweep"), Hits = V("SweepHits") });
            AddMove(new EnemyMove { Id = "Relight", Intent = IntentType.Defend | IntentType.Buff, Block = V("RelightBlock"), Note = $"获得 {V("RelightStrength")} 力量" });
            FirstMove = "OilSplash";
            AddOption("OilSplash", 2);
            AddOption("Sweep", 2);
            AddOption("Relight", 1, maxConsecutive: 1);
        }

        public override string Passive => $"刺目强光：每回合开始，你失去 {V("Glare")} 理智";

        public override void OnPlayerTurnStart(BattleEngine e, EnemyState self) => Aura(e, V("Glare"));

        public override int TurnStartSanityLoss(EnemyState self) => V("Glare");

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            switch (m.Id)
            {
                case "OilSplash":
                    Attack(e, self, m);
                    Debuff(e, StatusId.Frail, V("OilFrail"));
                    break;
                case "Sweep":
                    Attack(e, self, m);
                    break;
                case "Relight":
                    GainBlock(e, self, m.Block);
                    Buff(e, self, StatusId.Strength, V("RelightStrength"));
                    break;
            }
        }
    }

    /// <summary>E02 无面修女：意图始终显示「?」，被震慑一次之后本场恢复可见。考不确定性下的决策 + 恐惧控制。</summary>
    public sealed class FacelessNun : EnemyBehaviour
    {
        const string Revealed = "Revealed";

        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Rosary", Intent = IntentType.Attack | IntentType.Debuff, Damage = V("Rosary"), Note = $"施加 {V("RosaryWeak")} 层虚弱" });
            AddMove(new EnemyMove { Id = "Kiss", Intent = IntentType.Mind, SanityDamage = V("Kiss") });
            AddMove(new EnemyMove { Id = "Absolution", Intent = IntentType.Defend | IntentType.Buff, Block = V("HealBlock"), Note = $"恢复 {V("Heal")} 生命" });
            AddOption("Rosary", 9);
            AddOption("Kiss", 7);
            AddOption("Absolution", 4, maxConsecutive: 1,
                condition: (e, self) => self.Hp * 100 <= self.MaxHp * V("HealBelowPercent"));
        }

        public override string Passive => "不可名状：意图看不见，被震慑一次之后本场恢复可见";

        public override bool IntentHidden(BattleEngine e, EnemyState self) => self.Counter(Revealed) == 0;

        public override void OnStunned(BattleEngine e, EnemyState self)
        {
            if (self.Counter(Revealed) != 0) return;
            e.SetCounter(self, Revealed, 1);
            e.Log.Add(BattleEventType.PowerTriggered, $"{self.Name} 的面容第一次显现：之后的意图都看得见了", self.Name).Entity = self.Index;
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            switch (m.Id)
            {
                case "Rosary":
                    Attack(e, self, m);
                    Debuff(e, StatusId.Weak, V("RosaryWeak"));
                    break;
                case "Kiss":
                    MindAttack(e, m.SanityDamage);
                    break;
                case "Absolution":
                    Heal(e, self, V("Heal"));
                    GainBlock(e, self, m.Block);
                    break;
            }
        }
    }

    /// <summary>
    /// E03 网缚渔夫：固定循环 抛网 → 鱼叉 → 收网 → 鱼叉。抛网把缠网洗进抽牌堆；
    /// 收网按抽牌堆 + 手牌 + 弃牌堆里的缠网数量，每张 3 伤害（多段，意图实时更新）。考牌组质量 / 过牌。
    /// </summary>
    public sealed class NetFisherman : EnemyBehaviour
    {
        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Cast", Intent = IntentType.Debuff, Note = $"将 {V("Webs")} 张缠网洗入你的抽牌堆" });
            AddMove(new EnemyMove { Id = "Harpoon", Intent = IntentType.Attack, Damage = V("Harpoon") });
            AddMove(new EnemyMove { Id = "Haul", Intent = IntentType.Attack, Damage = V("HaulPer"), Note = "每张缠网一段" });
            AddOption("Cast", 1);
            AddOption("Harpoon", 1);
            AddOption("Haul", 1);
        }

        protected override string ForcedMove(BattleEngine e, EnemyState self) => Cycle(self, "Cast", "Harpoon", "Haul", "Harpoon");

        public override int IntentHits(BattleEngine e, EnemyState self, EnemyMove move) =>
            move.Id == "Haul" ? WebCount(e) : move.Hits;

        static int WebCount(BattleEngine e)
        {
            var p = e.Player;
            return p.DrawPile.Concat(p.Hand).Concat(p.DiscardPile).Count(c => c.Id == CardIds.Web);
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            if (m.Id == "Cast") AddCards(e, CardIds.Web, PileKind.Draw, V("Webs"));
            else Attack(e, self, m);
        }
    }

    /// <summary>
    /// Boss 溺亡司祭（策划/04 §5）。计数器：Tide = 潮位 0–TideMax，Phase = 阶段 1 / 2。
    /// 潮位：自己回合结束 +1（大潮那回合除外）；每有一只海底触须倒下 −1；到 TideMax 时下一次行动强制大潮，大潮后清零；
    /// 大潮被震慑取消时潮位不变，下回合再试。生命 ≤ 50% 时褪去人形进入第二阶段：清除负面状态、+2 力量、召唤 1 只触须，
    /// 之后常驻「深海之歌」（每回合开始你额外失去 1 理智，光环）。
    /// </summary>
    public sealed class DrownedPriest : EnemyBehaviour
    {
        public const string Tide = "Tide";
        public const string Phase = "Phase";

        bool _tidedThisTurn;

        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Summon", Intent = IntentType.Summon, Note = $"召唤海底触须，补到 {V("MaxTentacles")} 只" });
            AddMove(new EnemyMove { Id = "TidePrayer", Intent = IntentType.Defend | IntentType.Buff, Block = V("PrayerBlock"), Note = "潮位 +1" });
            AddMove(new EnemyMove { Id = "Baptism", Intent = IntentType.Attack | IntentType.Mind, Damage = V("Baptism"), SanityDamage = V("BaptismMind") });
            AddMove(new EnemyMove { Id = "HighTide", Intent = IntentType.Attack | IntentType.Mind, Damage = V("HighTide"), SanityDamage = V("HighTideMind"), Note = "潮位清零" });
            AddMove(new EnemyMove { Id = "Devour", Intent = IntentType.Attack, Damage = V("Devour"), Hits = V("DevourHits") });
            AddMove(new EnemyMove { Id = "SeaCurse", Intent = IntentType.Debuff | IntentType.Defend, Block = V("CurseBlock"), Note = $"将 {V("CurseCards")} 张疯语洗入你的抽牌堆" });
            AddMove(new EnemyMove { Id = "HighTide2", Intent = IntentType.Attack | IntentType.Mind, Damage = V("HighTide2"), SanityDamage = V("HighTideMind"), Note = "潮位清零" });
            FirstMove = "Summon";

            Func<BattleEngine, EnemyState, bool> p1 = (e, s) => s.Counter(Phase) <= 1;
            Func<BattleEngine, EnemyState, bool> p2 = (e, s) => s.Counter(Phase) == 2;
            AddOption("Summon", 4, condition: (e, s) => p1(e, s) && AliveTentacles(e) < V("MaxTentacles"));
            AddOption("TidePrayer", 3, condition: p1);
            AddOption("Baptism", 3, condition: p1);
            AddOption("Devour", 9, condition: p2);
            AddOption("SeaCurse", 5, maxUses: 2, condition: p2);
            AddOption("Summon", 6, condition: (e, s) => p2(e, s) && AliveTentacles(e) < V("MaxTentacles"));
        }

        public override string Passive => $"潮位到 {V("TideMax")} 时必定大潮；击杀触须潮位 −1。第二阶段：深海之歌（每回合开始你额外失去 {V("Song")} 理智）";

        public override string CounterText(EnemyState self) =>
            $"潮位 {self.Counter(Tide)}/{V("TideMax")}" + (self.Counter(Phase) == 2 ? " · 第二阶段" : "");

        public override int TurnStartSanityLoss(EnemyState self) => self.Counter(Phase) == 2 ? V("Song") : 0;

        static int AliveTentacles(BattleEngine e) => e.State.Enemies.Count(x => !x.IsDead && x.Def.Id == EnemyIds.Tentacle);

        public override void OnSpawned(BattleEngine e, EnemyState self)
        {
            e.SetCounter(self, Tide, 0);
            e.SetCounter(self, Phase, 1);
        }

        protected override string ForcedMove(BattleEngine e, EnemyState self) =>
            self.Counter(Tide) >= V("TideMax") ? (self.Counter(Phase) == 2 ? "HighTide2" : "HighTide") : null;

        public override void OnPlayerTurnStart(BattleEngine e, EnemyState self)
        {
            if (self.Counter(Phase) == 2) Aura(e, V("Song"));
        }

        public override void OnOwnTurnEnd(BattleEngine e, EnemyState self)
        {
            if (_tidedThisTurn)
            {
                _tidedThisTurn = false;
                return;
            }
            e.SetCounter(self, Tide, Math.Min(V("TideMax"), self.Counter(Tide) + 1));
        }

        public override void OnEnemyDied(BattleEngine e, EnemyState self, EnemyState dead)
        {
            if (dead.Def.Id != EnemyIds.Tentacle || self.Counter(Tide) <= 0) return;
            e.SetCounter(self, Tide, self.Counter(Tide) - 1);
            e.Log.Add(BattleEventType.PowerTriggered, $"{dead.Name} 沉入水底，潮位降到 {self.Counter(Tide)}", self.Name).Entity = self.Index;
        }

        public override void OnDamaged(BattleEngine e, EnemyState self)
        {
            if (self.Counter(Phase) != 1 || self.Hp * 2 > self.MaxHp) return;
            e.SetCounter(self, Phase, 2);
            e.Log.Add(BattleEventType.PowerTriggered, $"{self.Name} 褪去人形！清除负面状态，获得 {V("ShedStrength")} 力量，召唤触须；深海之歌响起", self.Name).Entity = self.Index;
            e.Queue.AddToTop(
                new DelegateAction(en =>
                {
                    foreach (var id in StatusSet.TurnBased) en.SetStatus(self, id, 0);
                }),
                new ApplyStatusAction(self, StatusId.Strength, V("ShedStrength")),
                new SummonAction(self, EnemyIds.Tentacle));
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            switch (m.Id)
            {
                case "Summon":
                    for (int i = AliveTentacles(e); i < V("MaxTentacles"); i++) Summon(e, self, EnemyIds.Tentacle);
                    break;
                case "TidePrayer":
                    e.SetCounter(self, Tide, Math.Min(V("TideMax"), self.Counter(Tide) + 1));
                    GainBlock(e, self, m.Block);
                    break;
                case "Baptism":
                    Attack(e, self, m);
                    MindAttack(e, m.SanityDamage);
                    break;
                case "HighTide":
                case "HighTide2":
                    Attack(e, self, m);
                    MindAttack(e, m.SanityDamage);
                    e.SetCounter(self, Tide, 0);
                    _tidedThisTurn = true;
                    break;
                case "Devour":
                    Attack(e, self, m);
                    break;
                case "SeaCurse":
                    AddCards(e, CardIds.Whispers, PileKind.Draw, V("CurseCards"));
                    GainBlock(e, self, m.Block);
                    break;
            }
        }
    }
}
