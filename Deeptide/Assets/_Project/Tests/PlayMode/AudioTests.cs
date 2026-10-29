using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.View.Audio;
using Deeptide.View.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deeptide.View.Tests
{
    /// <summary>
    /// 声音（策划/13 §6.1「测试」）：不靠耳朵，检查 AudioDirector 收到了哪些音效 ID、节流、没有素材时不报错，
    /// 以及 BattleSounds 的映射：命中三档、挡住 / 击破、理智音阶和来源、清格挡不出声。
    /// </summary>
    public sealed class AudioTests
    {
        BattleScreen _screen;
        BattleEngine Engine => _screen.Controller.Engine;
        AudioDirector Audio => AudioDirector.Instance;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Battle_Greybox");
            yield return null;
            var boot = Object.FindFirstObjectByType<BattleBootstrap>();
            Assert.IsNotNull(boot, "场景里没有 BattleBootstrap");
            while (boot.Screen == null) yield return null;
            _screen = boot.Screen;
            _screen.Director.Speed = 3;
            yield return WaitIdle();
            yield return new WaitForSecondsRealtime(AudioDirector.ThrottleWindow);
            Audio.ClearLog();
        }

        [UnityTest]
        public IEnumerator Battle_StrikeDefendEndTurn_PlaysTheRightCues()
        {
            Assert.AreEqual(3, AudioDatabase.Variants("U04_attack").Length, "U04_attack 的三个变体没打包进 AudioDatabase（Deeptide → 音频 → 打包音效）");
            Assert.AreEqual(1, AudioDatabase.Variants("R01_4").Length);
            Assert.IsNotNull(Object.FindAnyObjectByType<AudioListener>(), "没有 AudioListener 就听不到");

            int strike = Engine.Player.Hand.FindIndex(c => c.Id == "B01");
            Assert.IsTrue(_screen.Controller.Play(strike, 0));
            yield return WaitIdle();
            CollectionAssert.IsSubsetOf(new[] { "U04_attack", "C01_light" }, Audio.Requested, string.Join(" ", Audio.Requested));

            Audio.ClearLog();
            int defend = Engine.Player.Hand.FindIndex(c => c.Id == "B02");
            Assert.IsTrue(_screen.Controller.Play(defend));
            yield return WaitIdle();
            CollectionAssert.IsSubsetOf(new[] { "U04_skill", "C03" }, Audio.Requested, string.Join(" ", Audio.Requested));

            Audio.ClearLog();
            _screen.Hand.Hover(_screen.Hand.Cards[0], true);
            _screen.Hand.Hover(_screen.Hand.Cards[0], false);
            Assert.Contains("U01", Audio.Requested);

            // 结束回合按钮：怀表合盖；敌人出招前冲；新回合翻纸最多 5 声
            Audio.ClearLog();
            var endTurn = _screen.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "EndTurn");
            endTurn.onClick.Invoke();
            yield return WaitIdle();
            Assert.AreEqual("U06", Audio.Requested[0]);
            Assert.Contains("C05", Audio.Requested);
            int draws = Audio.Requested.Count(id => id == "U02");
            Assert.That(draws, Is.InRange(1, BattleSounds.MaxDrawSounds));
            Assert.Greater(Audio.Played, 0, "有素材的音效应该真的播了");
        }

        [UnityTest]
        public IEnumerator Battle_RejectedCard_KnocksOnWood()
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(_screen.Controller.Play(0, 0));
                yield return WaitIdle();
            }
            Audio.ClearLog();
            Assert.IsFalse(_screen.Controller.Play(0, 0));
            Assert.AreEqual(new[] { "U05" }, Audio.Requested.ToArray());
        }

        [UnityTest]
        public IEnumerator Throttle_TwoPerIdWithin80ms_MissingClipIsSilent()
        {
            Assert.IsTrue(Audio.Play("U07"));
            Assert.IsTrue(Audio.Play("U07"));
            Assert.IsFalse(Audio.Play("U07"), "同一 ID 80 ms 内第三个被节流");
            Assert.IsTrue(Audio.Play("U09"), "别的 ID 不受影响");
            Assert.AreEqual(1, Audio.Throttled);
            yield return new WaitForSecondsRealtime(AudioDirector.ThrottleWindow + 0.02f);
            Assert.IsTrue(Audio.Play("U07"));

            int played = Audio.Played;
            Assert.IsTrue(Audio.Play("Z99_没有这个"), "没有素材的 ID 不出声、不报错");
            Assert.AreEqual(played, Audio.Played);
            Assert.Contains("Z99_没有这个", Audio.Requested);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Mapping_HitsBlocksSanity()
        {
            var s = new BattleSounds();
            const int P = BattleEvent.PlayerEntity;
            Map(s, new BattleEvent { Type = BattleEventType.PlayerInit, Entity = P, Value = 70, Value2 = 70, Amount = 10 });

            // 命中三档：按扣掉的生命分
            Assert.AreEqual("C01_light", BattleSounds.HitTier(9));
            Assert.AreEqual("C01_medium", BattleSounds.HitTier(10));
            Assert.AreEqual("C01_heavy", BattleSounds.HitTier(20));
            Assert.AreEqual("C01_medium", Map(s, State(BattleEventType.HpChanged, P, 58)).Single().Id);

            // 格挡：加 → 风衣；被打但没打穿 → 闷响；打穿 → 木板裂开；回合开始清格挡 → 不出声
            Assert.AreEqual("C03", Map(s, State(BattleEventType.BlockChanged, P, 8)).Single().Id);
            Assert.AreEqual("C02", Map(s, State(BattleEventType.BlockChanged, P, 3), State(BattleEventType.HpChanged, P, 58), Damage(P, 0)).Single().Id);
            Assert.AreEqual("C04", Map(s, State(BattleEventType.BlockChanged, P, 0), State(BattleEventType.HpChanged, P, 54), Damage(P, 4)).Single().Id);
            Map(s, State(BattleEventType.BlockChanged, P, 6));
            Assert.IsEmpty(Map(s, State(BattleEventType.BlockChanged, P, 0), new BattleEvent { Type = BattleEventType.TurnStarted, Value = 2 }));

            // 精神攻击扣 3：先贴耳低语，再一格一个音往下走，停在剩下的 7
            var lost = Map(s, State(BattleEventType.SanityChanged, P, 7), new BattleEvent { Type = BattleEventType.SanityLost, Id = nameof(SanitySource.Enemy), Value = 7, Amount = 3 });
            Assert.AreEqual(new[] { "R03", "R01_9", "R01_8", "R01_7" }, lost.Select(c => c.Id).ToArray());
            Assert.AreEqual(BattleSounds.SourceLead, lost[1].Delay, 1e-4);
            Assert.Greater(lost[3].Delay, lost[2].Delay);

            // 侵蚀 → 井里一滴水；代价牌（来源 Card）→ 只有 R01
            Assert.AreEqual("R05", Map(s, State(BattleEventType.SanityChanged, P, 6), new BattleEvent { Type = BattleEventType.SanityLost, Id = nameof(SanitySource.Erosion) })[0].Id);
            Assert.AreEqual(new[] { "R01_5" }, Map(s, State(BattleEventType.SanityChanged, P, 5), new BattleEvent { Type = BattleEventType.SanityLost, Id = nameof(SanitySource.Card) }).Select(c => c.Id).ToArray());

            // 恢复 2：往上走，停在恢复后的 7
            Assert.AreEqual(new[] { "R02_6", "R02_7" }, Map(s, State(BattleEventType.SanityChanged, P, 7)).Select(c => c.Id).ToArray());
        }

        static List<Cue> Map(BattleSounds s, BattleEvent e, params BattleEvent[] upcoming) => s.Map(e, null, upcoming);

        static BattleEvent State(BattleEventType type, int entity, int value) =>
            new BattleEvent { Type = type, Entity = entity, Value = value };

        static BattleEvent Damage(int entity, int hpLoss) =>
            new BattleEvent { Type = BattleEventType.Damage, Entity = entity, Value = hpLoss, Message = "伤害" };

        IEnumerator WaitIdle()
        {
            yield return null;
            while (!_screen.Director.IsIdle) yield return null;
        }
    }
}
