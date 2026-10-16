using Deeptide.Core;
using NUnit.Framework;
using static Deeptide.Tests.TestUtil;

namespace Deeptide.Tests
{
    /// <summary>策划/02 §3.1–3.2 的伤害与格挡公式。</summary>
    public class FormulaTests
    {
        [Test]
        public void Strike_DealsBaseDamage()
        {
            var e = Battle();
            SetHand(e, "B01");
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(194));
        }

        [Test]
        public void Strength_AppliesToEveryHit()
        {
            var e = Battle();
            e.Player.Statuses.Add(StatusId.Strength, 2);
            SetHand(e, "C09");   // 4 伤害 ×2
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(200 - 2 * 6));
        }

        [TestCase(6, false, false, 6)]
        [TestCase(6, true, false, 4)]     // 4.5 → 4
        [TestCase(7, false, true, 10)]    // 10.5 → 10
        [TestCase(10, true, true, 11)]    // 11.25 → 11
        public void Damage_WeakAndVulnerable_FloorAtEnd(int baseDamage, bool weak, bool vulnerable, int expected)
        {
            var e = Battle();
            var enemy = e.State.Enemies[0];
            if (weak) e.Player.Statuses.Add(StatusId.Weak, 1);
            if (vulnerable) enemy.Statuses.Add(StatusId.Vulnerable, 1);
            Assert.That(DamageCalculator.Compute(e, e.Player, enemy, baseDamage), Is.EqualTo(expected));
        }

        [Test]
        public void Damage_NeverNegative()
        {
            var e = Battle();
            e.Player.Statuses.Add(StatusId.Strength, -10);
            Assert.That(DamageCalculator.Compute(e, e.Player, e.State.Enemies[0], 6), Is.EqualTo(0));
        }

        [Test]
        public void Block_AbsorbsBeforeHp()
        {
            var e = Battle();
            e.State.Enemies[0].Block = 4;
            SetHand(e, "B01");
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Block, Is.EqualTo(0));
            Assert.That(e.State.Enemies[0].Hp, Is.EqualTo(198));
        }

        [TestCase(0, false, 5)]
        [TestCase(2, false, 7)]
        [TestCase(0, true, 3)]    // 3.75 → 3
        [TestCase(2, true, 5)]    // 5.25 → 5
        public void CardBlock_DexterityAndFrail(int dex, bool frail, int expected)
        {
            var e = Battle();
            if (dex != 0) e.Player.Statuses.Add(StatusId.Dexterity, dex);
            if (frail) e.Player.Statuses.Add(StatusId.Frail, 1);
            SetHand(e, "B02");
            Play(e, 0);
            Assert.That(e.Player.Block, Is.EqualTo(expected));
        }
    }
}
