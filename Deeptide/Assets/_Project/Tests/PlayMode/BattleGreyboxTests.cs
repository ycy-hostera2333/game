using System.Collections;
using System.IO;
using Deeptide.Core;
using Deeptide.View.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deeptide.View.Tests
{
    /// <summary>
    /// 灰盒战斗的 PlayMode 测试：通过表现层的入口（BattleController）出牌，检查界面显示和内核状态一致。
    /// 关键节点把相机画面存成 PNG（Logs/Screenshots/，不进仓库），方便不开编辑器也能看布局。
    /// batchmode：Unity.exe -batchmode -projectPath Deeptide -runTests -testPlatform PlayMode -testResults results.xml
    /// </summary>
    public sealed class BattleGreyboxTests
    {
        static string ShotDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Screenshots"));

        BattleScreen _screen;
        BattleEngine Engine => _screen.Controller.Engine;

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
        }

        [UnityTest]
        public IEnumerator StrikeDefendThenEndTurn()
        {
            Assert.AreEqual(1, Engine.State.Turn);
            Assert.AreEqual(5, _screen.Hand.Cards.Count);
            Assert.AreEqual(3, _screen.Energy);
            Assert.AreEqual(5, _screen.DrawCount);
            yield return Shot("01_turn1");

            var enemy = Engine.State.Enemies[0];
            int hp = enemy.Hp;
            int strike = Engine.Player.Hand.FindIndex(c => c.Id == "B01");
            Assert.GreaterOrEqual(strike, 0, "种子 42 的起手应该有打击");
            Assert.IsTrue(_screen.Controller.Play(strike, 0));
            yield return WaitIdle();
            Assert.AreEqual(hp - 6, enemy.Hp);
            Assert.AreEqual(enemy.Hp, _screen.Enemies[0].Hp);
            Assert.AreEqual(4, _screen.Hand.Cards.Count);
            Assert.AreEqual(2, _screen.Energy);
            Assert.AreEqual(1, _screen.DiscardCount);

            int defend = Engine.Player.Hand.FindIndex(c => c.Id == "B02");
            Assert.GreaterOrEqual(defend, 0, "种子 42 的起手应该有防御");
            Assert.IsTrue(_screen.Controller.Play(defend));
            yield return WaitIdle();
            Assert.AreEqual(5, Engine.Player.Block);
            Assert.AreEqual(5, _screen.Player.Block);
            Assert.AreEqual(1, _screen.Energy);

            _screen.Hand.Hover(_screen.Hand.Cards[1], true);
            yield return Shot("02_after_strike_defend_hover");
            _screen.Hand.Hover(_screen.Hand.Cards[1], false);

            Assert.IsTrue(_screen.Controller.EndTurn());
            yield return WaitIdle();
            Assert.AreEqual(2, Engine.State.Turn);
            Assert.AreEqual(5, _screen.Hand.Cards.Count);
            Assert.AreEqual(3, _screen.Energy);
            Assert.AreEqual(Engine.Player.Hp, _screen.Player.Hp);
            Assert.AreEqual(Engine.Player.Block, _screen.Player.Block);
            yield return Shot("03_turn2");
        }

        [UnityTest]
        public IEnumerator RejectsCardWithoutEnergy()
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(_screen.Controller.Play(0, 0));
                yield return WaitIdle();
            }
            Assert.AreEqual(0, _screen.Energy);
            string rejected = null;
            _screen.Controller.Rejected += r => rejected = r;
            Assert.IsFalse(_screen.Controller.Play(0, 0));
            Assert.AreEqual("能量不足", rejected);
            Assert.AreEqual(2, _screen.Hand.Cards.Count);
        }

        [UnityTest]
        public IEnumerator PlaysToVictory()
        {
            _screen.Director.Speed = 20;
            for (int guard = 0; guard < 40 && Engine.State.Outcome == BattleOutcome.Ongoing; guard++)
            {
                // 能打就打：先打击，再防御，没牌可打就结束回合
                int pick = Engine.Player.Hand.FindIndex(c => c.Id == "B01" && Engine.CanPlay(Engine.Player.Hand.IndexOf(c), out _));
                if (pick < 0) pick = Engine.Player.Hand.FindIndex(c => Engine.CanPlay(Engine.Player.Hand.IndexOf(c), out _));
                bool ok = pick >= 0 ? _screen.Controller.Play(pick, 0) : _screen.Controller.EndTurn();
                Assert.IsTrue(ok);
                yield return WaitIdle();
                Assert.AreEqual(Engine.Player.Hand.Count, _screen.Hand.Cards.Count);
                Assert.AreEqual(Engine.Player.DrawPile.Count, _screen.DrawCount);
                Assert.AreEqual(Engine.Player.DiscardPile.Count, _screen.DiscardCount);
            }
            Assert.AreEqual(BattleOutcome.Victory, Engine.State.Outcome);
            Assert.IsTrue(_screen.Enemies[0].IsDead);
            yield return Shot("04_victory");
        }

        IEnumerator WaitIdle(float timeout = 30)
        {
            float t = 0;
            while (!_screen.Director.IsIdle)
            {
                t += Time.unscaledDeltaTime;
                if (t > timeout) Assert.Fail("事件回放超时");
                yield return null;
            }
            yield return null;
        }

        /// <summary>把主相机渲染到 1920×1080 的 RenderTexture 存成 PNG。先等飘字、补间播完。</summary>
        IEnumerator Shot(string name)
        {
            yield return new WaitForSeconds(0.5f);
            var cam = _screen.Camera;
            var rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;

            Directory.CreateDirectory(ShotDir);
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
