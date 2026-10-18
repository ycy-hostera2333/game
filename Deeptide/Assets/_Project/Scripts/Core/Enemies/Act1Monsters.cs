using Deeptide.Core.Cards;

namespace Deeptide.Core.Enemies
{
    // 批次 3.3：其余 5 种普通敌人（策划/04 §2）。权重按六面骰的面数写：60% / 40% = 3 / 2，50% / 50% = 3 / 3。

    /// <summary>N02 溺尸：固定循环 积水 → 溺水拥抱。考读意图：大招回合要叠格挡。</summary>
    public sealed class DrownedCorpse : EnemyBehaviour
    {
        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Soak", Intent = IntentType.Defend, Block = V("Soak") });
            AddMove(new EnemyMove { Id = "Embrace", Intent = IntentType.Attack, Damage = V("Embrace") });
            AddOption("Soak", 1);
            AddOption("Embrace", 1);
        }

        protected override string ForcedMove(BattleEngine e, EnemyState self) => Cycle(self, "Soak", "Embrace");

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            if (m.Id == "Soak") GainBlock(e, self, m.Block);
            else Attack(e, self, m);
        }
    }

    /// <summary>N03 鳞民幼体：撕咬 3 面 / 刺鳞 2 面（伤害 + 易伤）。成群出现，考 AOE。</summary>
    public sealed class Scaleling : EnemyBehaviour
    {
        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Bite", Intent = IntentType.Attack, Damage = V("Bite") });
            AddMove(new EnemyMove { Id = "Spines", Intent = IntentType.Attack | IntentType.Debuff, Damage = V("Spines"), Note = $"施加 {V("SpinesVulnerable")} 层易伤" });
            AddOption("Bite", 3);
            AddOption("Spines", 2);
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            Attack(e, self, m);
            if (m.Id == "Spines") Debuff(e, StatusId.Vulnerable, V("SpinesVulnerable"));
        }
    }

    /// <summary>N05 腐鸦群：群啄（多段）3 面 / 聒噪（精神攻击 + 虚弱）2 面。考多段伤害对格挡的压力。</summary>
    public sealed class CrowSwarm : EnemyBehaviour
    {
        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Peck", Intent = IntentType.Attack, Damage = V("Peck"), Hits = V("PeckHits") });
            AddMove(new EnemyMove { Id = "Caw", Intent = IntentType.Mind | IntentType.Debuff, SanityDamage = V("CawMind"), Note = $"施加 {V("CawWeak")} 层虚弱" });
            AddOption("Peck", 3);
            AddOption("Caw", 2);
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            if (m.Id == "Peck") Attack(e, self, m);
            else
            {
                MindAttack(e, m.SanityDamage);
                Debuff(e, StatusId.Weak, V("CawWeak"));
            }
        }
    }

    /// <summary>N07 海底触须：无心智；缠绕（伤害 + 束缚）和抽打交替，起手随机。也是 Boss 的召唤物。</summary>
    public sealed class SeaTentacle : EnemyBehaviour
    {
        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Entangle", Intent = IntentType.Attack | IntentType.Debuff, Damage = V("Entangle"), Note = $"施加 {V("EntangleBound")} 层束缚" });
            AddMove(new EnemyMove { Id = "Lash", Intent = IntentType.Attack, Damage = V("Lash") });
            AddOption("Entangle", 1);
            AddOption("Lash", 1);
        }

        protected override string ForcedMove(BattleEngine e, EnemyState self)
        {
            if (self.MoveHistory.Count == 0) return null;   // 起手随机
            return self.MoveHistory[self.MoveHistory.Count - 1] == "Entangle" ? "Lash" : "Entangle";
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            Attack(e, self, m);
            if (m.Id == "Entangle") Debuff(e, StatusId.Bound, V("EntangleBound"));
        }
    }

    /// <summary>N08 疫鼠：啮咬 / 乱咬（2×2）各半。小怪群，单体牌效率低。</summary>
    public sealed class PlagueRat : EnemyBehaviour
    {
        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Gnaw", Intent = IntentType.Attack, Damage = V("Gnaw") });
            AddMove(new EnemyMove { Id = "Frenzy", Intent = IntentType.Attack, Damage = V("Frenzy"), Hits = V("FrenzyHits") });
            AddOption("Gnaw", 3);
            AddOption("Frenzy", 3);
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m) => Attack(e, self, m);
    }
}
