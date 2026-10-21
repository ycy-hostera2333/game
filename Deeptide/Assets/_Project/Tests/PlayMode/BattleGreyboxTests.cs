using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Enemies;
using Deeptide.View;
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

        /// <summary>Boss 的全屏效果（策划/10 §7.3）：潮位满了水面从底部漫上来；第二阶段深海之歌。顺带截一张 Boss 第二阶段的图。</summary>
        [UnityTest]
        public IEnumerator BossTideFloodsTheScreen()
        {
            _boot.Launch(new BattleLaunch { Encounter = "boss_priest", Seed = 3 });
            yield return null;
            _screen = _boot.Screen;
            _screen.Director.Speed = 6;
            yield return WaitIdle();
            var boss = Engine.State.Enemies.First(en => en.Def.Id == EnemyIds.Priest);
            Assert.AreEqual(0f, LunacyFx.Instance.Tide, 0.001f, "开场潮位 0，没有潮水");

            Engine.SetCounter(boss, DrownedPriest.Tide, boss.Def.Vars["TideMax"]);
            Engine.SetCounter(boss, DrownedPriest.Phase, 2);
            _screen.SyncAll();
            yield return new WaitForSeconds(1f);
            Assert.Greater(LunacyFx.Instance.Tide, 0.9f, "潮位满了，水面应该漫上来");
            yield return Shot("08_boss_tide");
        }

        /// <summary>
        /// 卡图接入（策划/11 §3）：分层图按 CardArt.Pack 打包、登记进 ArtDatabase 后，这张牌的插图窗换成卡图，其他牌保持灰盒；
        /// 进入癫狂后同一张图换成癫狂面的配色并显现癫狂层。测试图是程序化画的，不进仓库。
        /// </summary>
        [UnityTest]
        public IEnumerator PackedArtReplacesGreybox()
        {
            ArtDatabase.SetOverride("C06", TestArt());
            try
            {
                _boot.Deck = new[] { "C06", "C06", "C06", "C06", "C06", "C06", "C14", "C14", "C11", "C11" };
                _boot.Enemies = new[] { "acolyte" };
                _boot.Launch(new BattleLaunch { Seed = 7, Potions = { "hallucinogen" } });
                yield return null;
                _screen = _boot.Screen;
                _screen.Director.Speed = 6;
                yield return WaitIdle();

                var blade = _screen.Hand.Cards.FirstOrDefault(v => v.CardId == "C06");
                Assert.IsNotNull(blade, "种子 7 的起手应该有梦魇之刃");
                Assert.IsTrue(blade.ShowsArt, "登记了卡图的牌显示卡图");
                // 打击、防御等已经有正式卡图（ArtDatabase），这里只检查还没画的牌
                var plain = _screen.Hand.Cards.FirstOrDefault(v => v.CardId != "C06" && ArtDatabase.Card(v.CardId) == null);
                if (plain != null) Assert.IsFalse(plain.ShowsArt, "没有卡图的牌保持灰盒");
                _screen.Hand.Hover(blade, true);
                yield return Shot("09_card_art_sane");
                _screen.Hand.Hover(blade, false);

                // 迷幻剂：理智设为 4，直接进入癫狂
                Assert.IsTrue(_screen.Controller.UsePotion(0));
                yield return WaitIdle();
                Assert.IsTrue(Engine.Player.IsLunatic);
                blade = _screen.Hand.Cards.First(v => v.CardId == "C06");
                Assert.IsTrue(blade.ShowsMadFace && blade.ShowsArt, "癫狂面也用卡图");
                _screen.Hand.Hover(blade, true);
                yield return Shot("10_card_art_mad");
                _screen.Hand.Hover(blade, false);
            }
            finally
            {
                ArtDatabase.ClearOverrides();
            }
        }

        /// <summary>
        /// 角色图（策划/10 §5、§11.2）：登记了图的角色（敌人 / 调查员）身体换成打包贴图，没有图的继续用灰盒剪影。
        /// 测试图是程序化的，用运行时替换，不依赖仓库里有没有画好的角色。
        /// </summary>
        [UnityTest]
        public IEnumerator ActorArtReplacesSilhouette()
        {
            var art = TestActor();
            ArtDatabase.SetActorOverride("acolyte", art);
            ArtDatabase.SetActorOverride(ArtDatabase.PlayerId, art);
            try
            {
                _boot.Deck = new[] { "B01", "B01", "B01", "B01", "B01", "B02", "B02", "B02", "B02", "B02" };
                _boot.Enemies = new[] { "acolyte", "dummy" };
                _boot.Launch(new BattleLaunch { Seed = 3 });
                yield return null;
                _screen = _boot.Screen;
                _screen.Director.Speed = 6;
                yield return WaitIdle();

                Assert.IsTrue(_screen.Enemies[0].HasArt, "登记了角色图的敌人用角色图");
                Assert.IsFalse(_screen.Enemies[1].HasArt, "稻草人没有角色图，保持灰盒剪影");
                Assert.IsTrue(_screen.Player.HasArt, "调查员用角色图");
            }
            finally
            {
                ArtDatabase.ClearOverrides();
            }
        }

        /// <summary>
        /// 战斗背景和 UI 套件（策划/10 §6、§12.1）：有背景图时换成背景图，Boss 战用沉没礼拜堂；图标、框饰没画好时退回灰盒。
        /// 不依赖仓库里有没有图：只检查「有图就用图」和场景能正常打一回合。
        /// </summary>
        [UnityTest]
        public IEnumerator BackdropAndUiKitFollowArtDatabase()
        {
            _boot.Deck = new[] { "B01", "B01", "B01", "B01", "B01", "B02", "B02", "B02", "B02", "B02" };
            _boot.Enemies = new[] { "drowned_priest" };
            _boot.Launch(new BattleLaunch { Seed = 5 });
            yield return null;
            _screen = _boot.Screen;
            _screen.Director.Speed = 6;
            yield return WaitIdle();

            Assert.AreEqual("chapel", BattleScreen.BackdropFor(new BattleSetup { Enemies = { "drowned_priest" } }));
            Assert.AreEqual("docks", BattleScreen.BackdropFor(new BattleSetup { Enemies = { "acolyte" } }));
            Assert.AreEqual(ArtDatabase.Background("chapel") != null, _screen.HasBackdropArt, "有背景图就用背景图");

            // 打一张打击：命中特效、意图图标、格挡框这些路径都要走一遍，不能报错
            int strike = Engine.Player.Hand.FindIndex(c => c.Id == "B01");
            Assert.GreaterOrEqual(strike, 0);
            Assert.IsTrue(_screen.Controller.Play(strike, 0));
            yield return WaitIdle();
            _screen.Controller.EndTurn();
            yield return WaitIdle();
            yield return Shot("14_backdrop_boss");
        }

        /// <summary>程序化的测试角色图（64 × 128）：一个椭圆的轮廓，里面一半排线。</summary>
        static Texture2D TestActor()
        {
            const int w = 64, h = 128;
            var ink = new Color32[w * h];
            var fill = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float body = Mathf.Sqrt((x - 32f) * (x - 32f) / (28f * 28f) + (y - 64f) * (y - 64f) / (58f * 58f)) - 1;
                float a = Cov(body * 28);
                fill[i] = new Color32(0, 0, 0, Byte(a));
                ink[i] = new Color32(0, 0, 0, Byte(a * (x > 32 ? 0.8f : 0.2f)));
            }
            return CardArt.ToTexture(CardArt.Pack(ink, null, null, fill), w, h, "test_actor");
        }

        /// <summary>程序化的测试卡图（半尺寸 312 × 216）：墨层是刀和排线，深渊遮罩是一只眼，癫狂层是刀背上的一排牙。</summary>
        static Texture2D TestArt()
        {
            const int w = CardArt.Width / 2, h = CardArt.Height / 2;
            var ink = new Color32[w * h];
            var abyss = new Color32[w * h];
            var mad = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                // 刀：从左下到右上的一条粗线，刀柄更粗；背景是稀疏的斜排线
                float blade = Segment(x, y, 70, 40, 250, 170) - 9;
                float handle = Segment(x, y, 40, 18, 72, 42) - 14;
                float hatch = Mathf.Abs(Mathf.Repeat(x - y, 14f) - 7f) - 5.6f;
                float c = Mathf.Max(Cov(Mathf.Min(blade, handle)), Cov(hatch) * 0.55f);
                ink[i] = new Color32(0, 0, 0, Byte(c));
                abyss[i] = new Color32(0, 0, 0, Byte(Cov(Mathf.Sqrt((x - 250f) * (x - 250f) + (y - 60f) * (y - 60f)) - 12)));
                // 牙：沿刀背每隔 18px 一颗
                float teeth = 99;
                for (int k = 0; k < 9; k++)
                {
                    float t = (k + 0.5f) / 9f;
                    float tx = Mathf.Lerp(70, 250, t) - 10, ty = Mathf.Lerp(40, 170, t) + 14;
                    teeth = Mathf.Min(teeth, Mathf.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty)) - 5);
                }
                mad[i] = new Color32(0, 0, 0, Byte(Cov(teeth)));
            }
            return CardArt.ToTexture(CardArt.Pack(ink, abyss, mad, null), w, h, "test_C06");
        }

        static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            return Mathf.Sqrt((px - dx * t) * (px - dx * t) + (py - dy * t) * (py - dy * t));
        }

        static float Cov(float d) => Mathf.Clamp01(0.5f - d);
        static byte Byte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255);

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
