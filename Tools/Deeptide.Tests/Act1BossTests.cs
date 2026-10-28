using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Enemies;
using NUnit.Framework;
using static Deeptide.Tests.TestUtil;

namespace Deeptide.Tests
{
    /// <summary>第一章新 Boss 鳞母、倒影（策划/15 §2.3、09 M7.4）：每条规则一个测试。</summary>
    public class Act1BossTests
    {
        static BattleEngine Fight(string enemy, ulong seed = 1) => BattleEngine.Start(new BattleSetup
        {
            Seed = seed,
            Deck = Enumerable.Repeat("B02", 10).ToList(),
            Enemies = new List<string> { enemy },
            MaxHp = 999,
            Hp = 999,
            Relics = new List<string>(),
        });

        static List<EnemyState> Alive(BattleEngine e, string id) => e.State.AliveEnemies.Where(x => x.Def.Id == id).ToList();

        /// <summary>表里的数值（7.5 调过参，测试不写死）。</summary>
        static int Var(string enemy, string key) => EnemyLibrary.Get(enemy).Vars[key];

        /// <summary>倒影抄来的每段伤害：按 CopyPercent 放大、向下取整。</summary>
        static int Copied(int perHit) => perHit * Var(EnemyIds.Mirror, "CopyPercent") / 100;

        // ---------------- 鳞母

        [Test]
        public void ScaleMother_OpensWithTwoMinionScalelings_AndKillingHerWins()
        {
            var e = Fight(EnemyIds.ScaleMother);
            var mother = e.State.Enemies[0];
            Assert.That((mother.Hp, mother.Will), Is.EqualTo((EnemyLibrary.Get(EnemyIds.ScaleMother).HpMin, 22)));
            var young = Alive(e, EnemyIds.Scaleling);
            Assert.That(young.Count, Is.EqualTo(2));
            Assert.That(young.All(x => x.Minion && x.Intent != null), Is.True, "召唤物，开场就有意图");

            SetHand(e, "B01");
            mother.Hp = 1;
            Play(e, 0, mother.Index);
            Assert.That(e.State.Outcome, Is.EqualTo(BattleOutcome.Victory), "幼体还活着也算赢");
        }

        [Test]
        public void ScaleMother_LaysAnEggEveryTwoTurns_UpToFourBrood()
        {
            var e = Fight(EnemyIds.ScaleMother);
            EndTurn(e);
            Assert.That(Alive(e, EnemyIds.ScaleEgg), Is.Empty, "第 1 回合不产");
            EndTurn(e);
            var egg = Alive(e, EnemyIds.ScaleEgg).Single();
            Assert.That((egg.Hp, egg.Mindless, egg.Minion), Is.EqualTo((8, true, true)));

            for (int i = 0; i < 8; i++) EndTurn(e);
            int brood = Alive(e, EnemyIds.Scaleling).Count + Alive(e, EnemyIds.ScaleEgg).Count;
            Assert.That(brood, Is.EqualTo(4), "卵 + 幼体最多 4 个");
        }

        [Test]
        public void Egg_HatchesAfterTwoEnemyTurns_WithoutCountingAsAKill()
        {
            var e = Fight(EnemyIds.ScaleMother);
            var mother = e.State.Enemies[0];
            EndTurns(e, 2);
            var egg = Alive(e, EnemyIds.ScaleEgg).Single();
            Assert.That(egg.Counter(ScaleEgg.Hatch), Is.EqualTo(2));
            EndTurn(e);
            Assert.That((egg.IsDead, egg.Counter(ScaleEgg.Hatch)), Is.EqualTo((false, 1)), "活过 1 个敌人回合");
            int strength = mother.Statuses.Get(StatusId.Strength);
            EndTurn(e);
            Assert.That(egg.IsDead, Is.True, "活过 2 个敌人回合孵化");
            Assert.That(Alive(e, EnemyIds.Scaleling).Count, Is.EqualTo(3));
            Assert.That(mother.Statuses.Get(StatusId.Strength), Is.EqualTo(strength), "孵化不算击杀，不触发护崽");
            Assert.That(e.Log.Events.Any(x => x.Type == BattleEventType.EnemyDied && x.Entity == egg.Index && x.Message.Contains("孵化")), Is.True);
        }

        [Test]
        public void Egg_HatchesOnItsFirstTurn_WhileYouAreLunatic()
        {
            var e = Fight(EnemyIds.ScaleMother);
            EndTurns(e, 2);
            var egg = Alive(e, EnemyIds.ScaleEgg).Single();
            e.SetSanity(3);
            EndTurn(e);
            Assert.That(egg.IsDead, Is.True, "癫狂时 1 个回合就孵化");
        }

        [Test]
        public void ScaleMother_GainsStrengthWhenAScalelingIsKilled_NotAnEgg()
        {
            var e = Fight(EnemyIds.ScaleMother);
            var mother = e.State.Enemies[0];
            var young = Alive(e, EnemyIds.Scaleling)[0];
            young.Hp = 1;
            SetHand(e, "B01", "B01");
            Play(e, 0, young.Index);
            Assert.That(mother.Statuses.Get(StatusId.Strength), Is.EqualTo(1), "护崽");

            EndTurns(e, 2);
            var egg = Alive(e, EnemyIds.ScaleEgg).Single();
            SetHand(e, "B01", "B01");
            e.SetEnergy(3);
            Play(e, 0, egg.Index);
            Play(e, 0, egg.Index);
            Assert.That(egg.IsDead, Is.True);
            Assert.That(mother.Statuses.Get(StatusId.Strength), Is.EqualTo(1), "打碎卵不算");
        }

        [Test]
        public void Scalelings_HitHarderWhileYouAreLunatic_OnlyNearTheirMother()
        {
            var e = Fight(EnemyIds.ScaleMother);
            var young = Alive(e, EnemyIds.Scaleling)[0];
            Assert.That(DamageCalculator.Compute(e, young, e.Player, 5), Is.EqualTo(5));
            e.SetSanity(4);
            Assert.That(DamageCalculator.Compute(e, young, e.Player, 5), Is.EqualTo(5 + Var(EnemyIds.ScaleMother, "Scent")), "同族的气味");

            var plain = Fight(EnemyIds.Scaleling);
            plain.SetSanity(4);
            Assert.That(DamageCalculator.Compute(plain, plain.State.Enemies[0], plain.Player, 5), Is.EqualTo(5), "困难池里的幼体没有鳞母");
        }

        [Test]
        public void ScaleMother_CallBuffsAllScalelingsAndBlocks()
        {
            var e = Fight(EnemyIds.ScaleMother);
            var mother = e.State.Enemies[0];
            SetIntent(e, 0, "Call");
            EndTurn(e);
            Assert.That(Alive(e, EnemyIds.Scaleling).All(x => x.Statuses.Get(StatusId.Strength) == 2), Is.True);
            Assert.That(mother.Block, Is.EqualTo(10));
        }

        // ---------------- 倒影

        [Test]
        public void Mirror_WillFollowsYourSanity_AndStunsTheMomentFearCatchesUp()
        {
            var e = Fight(EnemyIds.Mirror);
            var mirror = e.State.Enemies[0];
            Assert.That(mirror.Will, Is.EqualTo(30), "理智 10 × 3");
            e.SetSanity(5);
            Assert.That(mirror.Will, Is.EqualTo(15));

            e.SetFear(mirror, 14, mirror.Will);
            e.SetSanity(4);
            Assert.That(mirror.Stunned, Is.True, "意志跌到 12，恐惧 14 压过它");
            Assert.That(mirror.Will, Is.EqualTo(18), "震慑后照常 ×1.5");
            e.SetSanity(6);
            Assert.That(mirror.Will, Is.EqualTo(27), "之后仍跟着理智：6 × 3 × 1.5");
        }

        [Test]
        public void Mirror_DoesNotStunAtZeroSanity_BeforeTheCollapseReset()
        {
            var e = Fight(EnemyIds.Mirror);
            var mirror = e.State.Enemies[0];
            e.SetFear(mirror, 5, mirror.Will);
            e.SetSanity(0);
            Assert.That(mirror.Stunned, Is.False);
            Assert.That(mirror.Will, Is.EqualTo(30), "理智 0 时不重算");
        }

        [Test]
        public void Mirror_GazesFirst_ThenCopiesYourFirstAttackAtItsPlayedValue()
        {
            var e = Fight(EnemyIds.Mirror);
            var mirror = e.State.Enemies[0];
            Assert.That(mirror.Intent.Id, Is.EqualTo("Gaze"), "第一回合固定凝视");

            e.SetStatus(e.Player, StatusId.Strength, 3);
            SetHand(e, "B01", "C06");
            Play(e, 0, 0);   // 打击 6 + 3 力量 = 9
            Play(e, 0, 0);
            e.SetStatus(e.Player, StatusId.Strength, 0);   // 之后力量没了，抄的仍是打出时的 9
            EndTurn(e);
            Assert.That(mirror.Intent.Id, Is.EqualTo("Echo"));
            Assert.That((mirror.Counter(Mirror.Copy1), mirror.Counter(Mirror.Copy1Hits)), Is.EqualTo((Copied(9), 1)));
            Assert.That(e.IncomingDamage(), Is.EqualTo(Copied(9)), "意图预告和实际一致");

            int hp = e.Player.Hp;
            SetHand(e);
            e.SetBlock(e.Player, 0);
            EndTurn(e);
            Assert.That(hp - e.Player.Hp, Is.EqualTo(Copied(9)));
            Assert.That(mirror.Intent.Id, Is.EqualTo("Gaze"), "这一回合没打攻击牌：凝视");
        }

        [Test]
        public void Mirror_CracksAtHalfHp_AndCopiesTheFirstTwoAttacks()
        {
            var e = Fight(EnemyIds.Mirror);
            var mirror = e.State.Enemies[0];
            mirror.Hp = mirror.MaxHp / 2 + 1;
            SetHand(e, "B01", "C06");
            Play(e, 0, 0);
            Assert.That(mirror.Counter(Mirror.Phase), Is.EqualTo(2), "生命 ≤ 50% 龟裂");
            Play(e, 0, 0);
            EndTurn(e);
            Assert.That(mirror.Intent.Id, Is.EqualTo("Echo2"));
            Assert.That((mirror.Counter(Mirror.Copy1), mirror.Counter(Mirror.Copy2)), Is.EqualTo((Copied(6), Copied(8))), "打击 6、梦魇之刃 8");
            Assert.That(e.IncomingDamage(), Is.EqualTo(Copied(6) + Copied(8)));

            int hp = e.Player.Hp;
            SetHand(e);
            e.SetBlock(e.Player, 0);
            EndTurn(e);
            Assert.That(hp - e.Player.Hp, Is.EqualTo(Copied(6) + Copied(8)), "分两次打");
        }

        static void EndTurns(BattleEngine e, int n) { for (int i = 0; i < n; i++) EndTurn(e); }
    }
}
