using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using Deeptide.Core.Relics;
using Deeptide.View.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deeptide.View.Tests
{
    /// <summary>
    /// 全部内容在灰盒场景里都能打（策划/09 阶段 3 验收「所有内容都能在测试场景里单独调出来」）：
    /// 15 组遭遇逐个开打到分出胜负，每一步检查界面（手牌、牌堆、能量、生命、敌人、药水栏）和内核一致；
    /// 另外覆盖启动面板、药水选目标、选择面板。截图存到 Logs/Screenshots/（不进仓库）。
    /// </summary>
    public sealed class BattleContentTests
    {
        static string ShotDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Screenshots"));

        BattleBootstrap _boot;
        BattleScreen Screen => _boot.Screen;
        BattleEngine Engine => Screen.Controller.Engine;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Battle_Greybox");
            yield return null;
            _boot = Object.FindFirstObjectByType<BattleBootstrap>();
            Assert.IsNotNull(_boot, "场景里没有 BattleBootstrap");
            while (_boot.Screen == null) yield return null;
            yield return WaitIdle();
        }

        [UnityTest]
        public IEnumerator EveryEncounterPlaysThrough()
        {
            var results = new List<string>();
            foreach (var enc in EncounterLibrary.All)
            {
                yield return Launch(new BattleLaunch
                {
                    DeckName = "C", Encounter = enc.Id, Seed = 7,
                    Relics = { RelicIds.InvestigatorNotes },
                    Potions = { "acid_vial", "powder_flask", "smelling_salts" },
                });
                Assert.AreEqual(Engine.State.Enemies.Count, Screen.Enemies.Count, enc.Id);   // 含开场就召唤的（鳞母的 2 只幼体）
                AssertInSync(enc.Id);

                int steps = 0;
                for (; steps < 300 && Engine.State.Outcome == BattleOutcome.Ongoing; steps++)
                {
                    yield return Step();
                    yield return WaitIdle();
                    AssertInSync($"{enc.Id} 第 {steps + 1} 步");
                }
                Assert.AreNotEqual(BattleOutcome.Ongoing, Engine.State.Outcome, $"{enc.Id} 300 步内没打完");
                results.Add($"{enc.Id}：{Engine.State.Outcome}，{Engine.State.Turn} 回合，{steps} 步，生命 {Engine.Player.Hp}");
                if (enc.Pool == EncounterPool.Boss) yield return Shot("10_boss_end");
            }
            Debug.Log("[BattleContentTests] " + string.Join("\n", results));
        }

        [UnityTest]
        public IEnumerator LauncherStartsChosenBattle()
        {
            Screen.TopBar.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "选择对局").onClick.Invoke();
            yield return null;
            var panel = LauncherPanel.Current;
            Assert.IsNotNull(panel, "点「选择对局」应该打开启动面板");
            panel.PickDeck("B");
            panel.PickEncounter("boss_priest");
            panel.TogglePotion("fear_powder");
            panel.ToggleRelic("broken_watch");
            panel.SetSeed(3);
            yield return Shot("11_launcher");
            panel.Begin();
            yield return null;
            while (_boot.Screen == null) yield return null;
            yield return WaitIdle();

            Assert.IsNull(LauncherPanel.Current);
            Assert.AreEqual(EnemyIds.Priest, Engine.State.Enemies[0].Def.Id);
            Assert.AreEqual("fear_powder", Screen.TopBar.PotionIds[0]);
            Assert.IsTrue(Engine.Player.Relics.Any(r => r.Id == "broken_watch"));
            Assert.AreEqual(3UL, Engine.Setup.Seed);
            yield return Shot("12_boss_start");
        }

        [UnityTest]
        public IEnumerator TargetedPotionWaitsForEnemyClick()
        {
            yield return Launch(new BattleLaunch
            {
                DeckName = "starter", Encounter = "hard_acolyte_whisperer", Seed = 5,
                Relics = { RelicIds.InvestigatorNotes }, Potions = { "acid_vial", "iron_tonic" },
            });
            Screen.PotionClicked(0);
            Assert.AreEqual(0, Screen.PendingPotion, "两个敌人时要先选目标");
            Assert.AreEqual("acid_vial", Screen.TopBar.PotionIds[0], "选目标之前不消耗药水");
            Screen.EnemyClicked(1);
            yield return WaitIdle();
            Assert.AreEqual(-1, Screen.PendingPotion);
            Assert.AreEqual(3, Engine.State.Enemies[1].Statuses.Get(StatusId.Vulnerable));
            Assert.IsNull(Screen.TopBar.PotionIds[0]);

            // 选目标时再点同一格是取消；不需要目标的药水点了直接生效
            Screen.PotionClicked(1);
            yield return WaitIdle();
            Assert.AreEqual(12, Engine.Player.Block);
            Assert.IsNull(Screen.TopBar.PotionIds[1]);
            AssertInSync("药水");
        }

        [UnityTest]
        public IEnumerator ChoicePanelShowsCards()
        {
            _boot.Deck = new[] { "C15", "C15", "C15", "C15", "C15", "U15", "U15", "U15", "B01", "B02" };
            yield return Launch(new BattleLaunch { DeckName = "", Encounter = "easy_acolyte", Seed = 11, Relics = { RelicIds.InvestigatorNotes } });

            int research = Engine.Player.Hand.FindIndex(c => c.Id == "C15");
            Assert.GreaterOrEqual(research, 0, "起手应该有查阅资料");
            Assert.IsTrue(Screen.Controller.Play(research));
            yield return WaitIdle();
            Assert.IsTrue(Screen.Choice.IsOpen, "查阅资料要选一张弃掉");
            Assert.AreEqual(Screen.Choice.OptionCount, Screen.Choice.CardOptions, "弃牌选项都应该画成卡牌");
            yield return Shot("13_choice_discard");

            int hand = Engine.Player.Hand.Count;
            Screen.Choice.Pick(0);
            yield return WaitIdle();
            Assert.IsFalse(Screen.Choice.IsOpen);
            Assert.AreEqual(hand - 1, Engine.Player.Hand.Count);
            AssertInSync("选择之后");
        }

        // ------------------------------------------------------------------ 工具

        IEnumerator Launch(BattleLaunch launch)
        {
            _boot.Launch(launch);
            yield return null;
            Screen.Director.Speed = 40;
            yield return WaitIdle();
        }

        /// <summary>
        /// 简单的出牌策略，只为把每场打完、走遍各种事件：有选择先选第一项；第 1 回合先用药水（走顶栏 → 选目标的完整流程）；
        /// 能打的牌优先攻击，目标是下标最小的活敌人；都打不了就结束回合。
        /// </summary>
        IEnumerator Step()
        {
            var engine = Engine;
            if (Screen.Controller.AwaitingChoice)
            {
                Assert.IsTrue(Screen.Choice.IsOpen, "等待选择时应该显示选择面板");
                Screen.Choice.Pick(0);
                yield break;
            }

            var target = engine.State.Enemies.FirstOrDefault(en => !en.IsDead);
            int targetIndex = target?.Index ?? -1;
            if (engine.State.Turn == 1)
            {
                for (int slot = 0; slot < engine.Player.Potions.Count; slot++)
                {
                    if (!engine.CanUsePotion(slot, out _)) continue;
                    Screen.PotionClicked(slot);
                    if (Screen.PendingPotion == slot) Screen.EnemyClicked(targetIndex);
                    yield break;
                }
            }

            var hand = engine.Player.Hand;
            int pick = -1;
            for (int i = 0; i < hand.Count && pick < 0; i++)
                if (hand[i].Def.Type == CardType.Attack && engine.CanPlay(i, out _)) pick = i;
            for (int i = 0; i < hand.Count && pick < 0; i++)
                if (engine.CanPlay(i, out _)) pick = i;

            if (pick >= 0)
            {
                int t = hand[pick].Def.Target == CardTarget.Enemy ? targetIndex : -1;
                string name = hand[pick].Name;   // 打出之后这张牌就离开手牌了，先记下名字
                Assert.IsTrue(Screen.Controller.Play(pick, t), $"出牌失败：{name}");
            }
            else Assert.IsTrue(Screen.Controller.EndTurn());
        }

        void AssertInSync(string where)
        {
            var p = Engine.Player;
            Assert.AreEqual(p.Hand.Count, Screen.Hand.Cards.Count, $"{where}：手牌");
            Assert.AreEqual(p.DrawPile.Count, Screen.DrawCount, $"{where}：抽牌堆");
            Assert.AreEqual(p.DiscardPile.Count, Screen.DiscardCount, $"{where}：弃牌堆");
            Assert.AreEqual(p.Energy, Screen.Energy, $"{where}：能量");
            Assert.AreEqual(p.Hp, Screen.Player.Hp, $"{where}：生命");
            Assert.AreEqual(p.Block, Screen.Player.Block, $"{where}：格挡");
            Assert.AreEqual(Engine.State.Enemies.Count, Screen.Enemies.Count, $"{where}：敌人数");
            for (int i = 0; i < Engine.State.Enemies.Count; i++)
            {
                var en = Engine.State.Enemies[i];
                Assert.AreEqual(en.IsDead, Screen.Enemies[i].IsDead, $"{where}：敌人 {i} 死亡");
                if (!en.IsDead) Assert.AreEqual(en.Hp, Screen.Enemies[i].Hp, $"{where}：敌人 {i} 生命");
            }
            for (int i = 0; i < p.Potions.Count; i++)
                Assert.AreEqual(p.Potions[i]?.Def.Id, Screen.TopBar.PotionIds[i], $"{where}：药水栏 {i}");
            Assert.AreEqual(Engine.State.Phase == BattlePhase.WaitingForChoice && Engine.State.Outcome == BattleOutcome.Ongoing,
                Screen.Choice.IsOpen, $"{where}：选择面板");
        }

        IEnumerator WaitIdle(float timeout = 60)
        {
            float t = 0;
            while (!Screen.Director.IsIdle)
            {
                t += Time.unscaledDeltaTime;
                if (t > timeout) Assert.Fail("事件回放超时");
                yield return null;
            }
            yield return null;
        }

        /// <summary>把主相机渲染到 1920×1080 的 RenderTexture 存成 PNG（同 BattleGreyboxTests）。</summary>
        IEnumerator Shot(string name)
        {
            yield return new WaitForSeconds(0.5f);
            var cam = Camera.main;
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
