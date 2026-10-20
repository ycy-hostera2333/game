using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        BattleBootstrap _boot;
        BattleEngine Engine => _screen.Controller.Engine;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Battle_Greybox");
            yield return null;
            var boot = Object.FindFirstObjectByType<BattleBootstrap>();
            Assert.IsNotNull(boot, "场景里没有 BattleBootstrap");
            while (boot.Screen == null) yield return null;
            _boot = boot;
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
                // 理智条的侵蚀预告（策划/10 §6.2）：第 3 回合起，下回合开始会失去 1 理智
                if (Engine.State.Outcome == BattleOutcome.Ongoing && Engine.State.Phase == BattlePhase.PlayerAction)
                {
                    bool erosionNext = Engine.State.Turn + 1 >= Engine.Config.ErosionStartTurn;
                    Assert.AreEqual(erosionNext, _screen.Player.Gauge.Tags.Any(t => t.Contains("侵蚀")), $"第 {Engine.State.Turn} 回合的侵蚀预告");
                }
                Assert.AreEqual(Engine.Player.Hand.Count, _screen.Hand.Cards.Count);
                Assert.AreEqual(Engine.Player.DrawPile.Count, _screen.DrawCount);
                Assert.AreEqual(Engine.Player.DiscardPile.Count, _screen.DiscardCount);
            }
            Assert.AreEqual(BattleOutcome.Victory, Engine.State.Outcome);
            Assert.IsTrue(_screen.Enemies[0].IsDead);
            yield return Shot("04_victory");
        }

        /// <summary>
        /// 癫狂表现（策划/10 §4.3、§6.2、§6.3、§7）：悬停代价牌时理智条预告；理智跌破癫狂线后双面牌全部翻到癫狂面、
        /// 意图模糊（数值换成墨渍）、全局 _Lunacy 升起；悬停双面牌并排显示另一面；理智归 0 时崩溃，理智重置为 3。
        /// </summary>
        [UnityTest]
        public IEnumerator LunacyFlipsCardsAndBlursIntents()
        {
            _boot.Deck = new[] { "C08", "C08", "C08", "C08", "C07", "C07", "B03", "B03", "C06", "C06" };
            _boot.Enemies = new[] { "acolyte" };
            _boot.Launch(new BattleLaunch { Seed = 5 });
            yield return null;
            _screen = _boot.Screen;
            _screen.Director.Speed = 6;
            yield return WaitIdle();
            Assert.AreEqual(10, Engine.Player.Sanity);
            Assert.IsFalse(_screen.Enemies[0].Intent.Blurred);

            var costly = _screen.Hand.Cards.First(v => Engine.FindCard(v.Uid).SanityCost(false) > 0);
            int cost = Engine.FindCard(costly.Uid).SanityCost(false);
            _screen.Hand.Hover(costly, true);
            Assert.AreEqual(cost, _screen.Player.Gauge.CostPreview, "悬停代价牌：理智条预告要付的理智");
            yield return Shot("05_hover_cost");
            _screen.Hand.Hover(costly, false);
            Assert.AreEqual(-1, _screen.Player.Gauge.CostPreview);

            bool checkedLunacy = false, collapsed = false;
            for (int guard = 0; guard < 20; guard++)
            {
                int pick = Engine.Player.Hand.FindIndex(c => c.SanityCost(Engine.UsesMadFace(c)) > 0 && Engine.CanPlay(Engine.Player.Hand.IndexOf(c), out _));
                if (pick < 0) break;
                int sanityBefore = Engine.Player.Sanity;
                int collapsesBefore = Engine.Log.Events.Count(ev => ev.Type == BattleEventType.Collapse);
                Assert.IsTrue(_screen.Controller.Play(pick, Engine.Player.Hand[pick].Def.Target == CardTarget.Enemy ? 0 : -1));
                yield return WaitIdle();
                if (Engine.State.Choice != null && _screen.Controller.AwaitingChoice)
                {
                    _screen.Controller.Choose(0);
                    yield return WaitIdle();
                }

                if (!checkedLunacy && Engine.Player.IsLunatic)
                {
                    checkedLunacy = true;
                    foreach (var view in _screen.Hand.Cards)
                        Assert.AreEqual(Engine.UsesMadFace(Engine.FindCard(view.Uid)), view.ShowsMadFace, $"{view.CardId} 的卡面");
                    Assert.AreEqual(Engine.IntentsBlurred, _screen.Enemies[0].Intent.Blurred);
                    Assert.IsTrue(_screen.Enemies[0].Intent.Blurred, "癫狂时意图模糊");
                    Assert.AreEqual("", _screen.Enemies[0].Intent.NumberText, "模糊时不显示数值");
                    LunacyFx.Instance.Settle();
                    Assert.Greater(Shader.GetGlobalFloat("_Lunacy"), 0.3f);

                    var dual = _screen.Hand.Cards.FirstOrDefault(v => Engine.FindCard(v.Uid).Def.Dual);
                    if (dual != null)
                    {
                        _screen.Hand.Hover(dual, true);
                        Assert.IsTrue(dual.PreviewVisible, "悬停双面牌显示另一面");
                        yield return Shot("06_lunacy_hover_dual");
                        _screen.Hand.Hover(dual, false);
                        Assert.IsFalse(dual.PreviewVisible);
                    }
                    else yield return Shot("06_lunacy");
                }

                if (Engine.Log.Events.Count(ev => ev.Type == BattleEventType.Collapse) > collapsesBefore)
                {
                    collapsed = true;
                    Assert.AreEqual(Engine.Config.CollapseSanityReset, Engine.Player.Sanity, $"崩溃前理智 {sanityBefore}");
                    Assert.AreEqual(Engine.Player.Sanity, _screen.Player.Sanity);
                    yield return Shot("07_collapse");
                    break;
                }
            }
            Assert.IsTrue(checkedLunacy, "这套牌第 1 回合应该能打进癫狂");
            Assert.IsTrue(collapsed, "这套牌第 1 回合应该能打到崩溃");
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
