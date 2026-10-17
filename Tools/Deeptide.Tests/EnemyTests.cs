using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Enemies;
using NUnit.Framework;
using static Deeptide.Tests.TestUtil;

namespace Deeptide.Tests
{
    /// <summary>恐惧与震慑（策划/02 §3.4）以及敌人招式表（02 §6.2、04 §2）。</summary>
    public class EnemyTests
    {
        [Test]
        public void Fear_StunsAtWill_ResetsFear_GrowsWill_SkipsAction()
        {
            var e = Battle(new[] { EnemyIds.Acolyte });
            var acolyte = e.State.Enemies[0];
            Assert.That(acolyte.Will, Is.EqualTo(8));

            SetHand(e, "C11", "C11");
            Play(e, 0);
            Assert.That(acolyte.Fear, Is.EqualTo(4));
            Assert.That(acolyte.Stunned, Is.False);
            Play(e, 0);
            Assert.That(acolyte.Stunned, Is.True);
            Assert.That(acolyte.Fear, Is.EqualTo(0));
            Assert.That(acolyte.Will, Is.EqualTo(12));

            SetIntent(e, 0, "Dagger");
            EndTurn(e);
            Assert.That(e.Player.Hp, Is.EqualTo(70), "被震慑的敌人不该行动");
            Assert.That(acolyte.Stunned, Is.False);
            Assert.That(e.Log.Events.Count(BattleEventType.EnemyStunSkipped), Is.EqualTo(1));
        }

        [Test]
        public void Will_GrowsByHalf_RoundedUp()
        {
            var e = Battle(new[] { EnemyIds.Whisperer });   // 意志 5
            SetHand(e, "C11", "C11");
            Play(e, 0);
            Play(e, 0);
            Assert.That(e.State.Enemies[0].Will, Is.EqualTo(8));   // 7.5 → 8
        }

        [Test]
        public void Fear_HasNoEffectOnMindless()
        {
            var e = Battle();
            var dummy = e.State.Enemies[0];
            dummy.Mindless = true;
            SetHand(e, "C11");
            Play(e, 0);
            Assert.That(dummy.Fear, Is.EqualTo(0));
            Assert.That(dummy.Stunned, Is.False);
        }

        static List<string> RollMany(BattleEngine e, int count)
        {
            var en = e.State.Enemies[0];
            var seq = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var m = en.Behaviour.ChooseNext(e, en);
                en.MoveHistory.Add(m.Id);
                en.MoveUses[m.Id] = en.UsesOf(m.Id) + 1;
                seq.Add(m.Id);
            }
            return seq;
        }

        [Test]
        public void Acolyte_PraysFirst_NeverPraysTwiceInARow_NeverDaggersThrice()
        {
            var seq = RollMany(Battle(new[] { EnemyIds.Acolyte }, seed: 7), 2000);
            Assert.That(seq[0], Is.EqualTo("Prayer"));
            for (int i = 1; i < seq.Count; i++)
                Assert.That(seq[i] == "Prayer" && seq[i - 1] == "Prayer", Is.False, $"第 {i} 次连续祷文");
            for (int i = 2; i < seq.Count; i++)
                Assert.That(seq[i] == "Dagger" && seq[i - 1] == "Dagger" && seq[i - 2] == "Dagger", Is.False, $"第 {i} 次连续三次匕首");
            for (int i = 1; i < seq.Count; i++)
                Assert.That(seq[i] == "Heavy" && seq[i - 1] == "Heavy", Is.False, $"第 {i} 次连续重击");
            Assert.That(seq.Count(x => x == "Dagger"), Is.GreaterThan(seq.Count / 3));
            Assert.That(seq.Count(x => x == "Heavy"), Is.GreaterThan(seq.Count / 12));
        }

        [Test]
        public void EveryEnemyHasTwoMovesOfTheSameType()
        {
            // 癫狂时意图只显示类型；同类型至少两招、数值不同，模糊才有信息代价（试玩发现 #2）
            foreach (var id in new[] { EnemyIds.Acolyte, EnemyIds.Whisperer, EnemyIds.Townsfolk })
            {
                var moves = Battle(new[] { id }).State.Enemies[0].Behaviour.Moves.ToList();
                bool hasVariant = moves.GroupBy(m => m.Intent)
                    .Any(g => g.Select(m => m.Damage * 100 + m.SanityDamage).Distinct().Count() >= 2);
                Assert.That(hasVariant, Is.True, id);
            }
        }

        [Test]
        public void Whisperer_ConfusesAtMostTwicePerCombat()
        {
            var seq = RollMany(Battle(new[] { EnemyIds.Whisperer }, seed: 3), 300);
            Assert.That(seq.Count(x => x == "Confuse"), Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void Whisperer_Confuse_PutsWhispersInDiscard()
        {
            var e = Battle(new[] { EnemyIds.Whisperer });
            SetIntent(e, 0, "Confuse");
            SetHand(e);
            int before = e.Player.DiscardPile.Count(c => c.Id == "S01");
            EndTurn(e);
            int inAllPiles = e.Player.DiscardPile.Concat(e.Player.Hand).Concat(e.Player.DrawPile).Count(c => c.Id == "S01");
            Assert.That(inAllPiles, Is.EqualTo(before + 1));
        }

        [Test]
        public void Townsfolk_ScentOfMadness_AddsFourWhenLunatic()
        {
            var e = Battle(new[] { EnemyIds.Townsfolk });
            SetIntent(e, 0, "Hoe");
            Assert.That(e.GetIntent(e.State.Enemies[0]).Damage, Is.EqualTo(9));
            e.Player.Sanity = 4;
            Assert.That(e.GetIntent(e.State.Enemies[0]).Damage, Is.EqualTo(13));
            Assert.That(e.IncomingDamage(), Is.EqualTo(13));
            Assert.That(e.IntentsBlurred, Is.True);
        }

        [Test]
        public void Acolyte_Prayer_BuffsAllEnemies()
        {
            var e = Battle(new[] { EnemyIds.Acolyte, EnemyIds.Whisperer });
            SetIntent(e, 0, "Prayer");
            SetIntent(e, 1, "Whisper");
            SetHand(e);
            EndTurn(e);
            Assert.That(e.State.Enemies[0].Statuses.Get(StatusId.Strength), Is.EqualTo(2));
            Assert.That(e.State.Enemies[1].Statuses.Get(StatusId.Strength), Is.EqualTo(2));
        }
    }
}
