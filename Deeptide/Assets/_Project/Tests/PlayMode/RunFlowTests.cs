using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using Deeptide.Core.Map;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;
using Deeptide.View.Battle;
using Deeptide.View.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deeptide.View.Tests
{
    /// <summary>
    /// 跑局界面（策划/09 阶段 4 的表现部分）：标题 → 地图 → 各种节点 → Boss 结算，全部通过界面入口操作（点节点、点「Option 下标」的按钮和卡牌），
    /// 每一步检查面板把内核的选项全画出来了、顶栏和 RunState 一致；另外覆盖存档继续、牌组、丢药水、地图悬停。
    /// 存档写到临时目录，不碰玩家的存档。截图存到 Logs/Screenshots/（不进仓库）。
    /// </summary>
    public sealed class RunFlowTests
    {
        static string ShotDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Screenshots"));
        static string TestSave => Path.Combine(Application.temporaryCachePath, "run_test.json");
        static string TestTelemetry => Path.Combine(Application.temporaryCachePath, "telemetry_test");
        static string TestProfile => Path.Combine(Application.temporaryCachePath, "profile_test.json");

        RunBootstrap _boot;
        RunScreen Screen => _boot.Screen;
        RunEngine Run => Screen.Run;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            RunBootstrap.SavePathOverride = TestSave;
            RunSaves.Delete(TestSave);
            TelemetryFiles.DirOverride = TestTelemetry;
            if (Directory.Exists(TestTelemetry)) Directory.Delete(TestTelemetry, true);
            ProfileFiles.PathOverride = TestProfile;
            ProfileFiles.Delete();
            RunScreen.BattleSpeed = 40;
            yield return LoadRunScene();
        }

        /// <summary>
        /// 载入 Run 场景，等到新场景的 RunBootstrap 显示出标题画面。换场景要到下一帧的某个时候才生效，
        /// 这之前 FindFirstObjectByType 可能还拿到上一个场景里那个（标题已经关掉的）RunBootstrap，所以要认准不是旧的那个。
        /// </summary>
        IEnumerator LoadRunScene()
        {
            var old = Object.FindFirstObjectByType<RunBootstrap>();
            SceneManager.LoadScene("Run");
            float t = 0;
            do
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                _boot = Object.FindFirstObjectByType<RunBootstrap>();
            }
            while ((_boot == null || _boot == old || _boot.Title == null) && t < 10);
            Assert.IsNotNull(_boot, "场景里没有 RunBootstrap");
            Assert.IsNotNull(_boot.Title, "10 秒内没出现标题画面");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            // 先销毁 RunBootstrap 再撤掉临时路径：不然退出 Play 时它的 OnApplicationQuit 会把测试局存进玩家真正的存档和遥测目录
            if (_boot != null) Object.Destroy(_boot.gameObject);
            yield return null;
            RunScreen.BattleSpeed = 1;
            RunSaves.Delete(TestSave);
            RunBootstrap.SavePathOverride = null;
            if (Directory.Exists(TestTelemetry)) Directory.Delete(TestTelemetry, true);
            TelemetryFiles.DirOverride = null;
            ProfileFiles.Delete();
            ProfileFiles.PathOverride = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TitleStartsRunOnMap()
        {
            Assert.IsFalse(_boot.ClickTitle("Continue"), "没有存档时不该有「继续」");
            Assert.IsNotNull(ArtDatabase.Illustration("keyart"), "主视觉应该已经打包");
            Assert.IsTrue(_boot.Title.GetComponentsInChildren<UnityEngine.UI.Image>().Any(i => i.name == "Logo"), "标题画面应该有 Logo");
            yield return Shot("20_title");
            _boot.NextSeed = 12;
            Assert.IsTrue(_boot.ClickTitle("NewRun"));
            yield return null;
            // 先选角（09 M6.5）：每个角色一张卷轴，「返回」回到标题
            Assert.IsNotNull(_boot.CharacterSelect, "「新的一局」先弹出选角");
            Assert.IsNull(Screen, "选角之前还没开局");
            foreach (var ch in CharacterLibrary.All)
                Assert.IsTrue(_boot.CharacterSelect.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == $"Pick_{ch.Id}"), $"没有 {ch.Name} 的按钮");
            Assert.IsTrue(_boot.ClickTitle("PickBack"));
            yield return null;
            Assert.IsNull(_boot.CharacterSelect, "「返回」关掉选角");
            Assert.IsTrue(_boot.ClickTitle("NewRun"));
            yield return null;
            Assert.IsTrue(_boot.ClickTitle("Pick_investigator"));
            yield return null;
            yield return null;

            Assert.IsNotNull(Screen);
            Assert.AreEqual(CharacterIds.Investigator, Run.State.Character);
            Assert.IsNull(_boot.Title);
            Assert.AreEqual(RunPhase.Select, Screen.Shown, "信下面压着回响选择（09 M7.6）");
            Assert.AreEqual(SelectPurpose.Echo, Run.State.Select.Purpose);
            Assert.IsTrue(File.Exists(TestSave), "开局就该存一次档");
            var t0 = ReadTelemetry();
            Assert.AreEqual(RunOutcome.Unfinished, t0.Outcome);
            Assert.AreEqual(1, t0.RunIndex);
            Assert.AreEqual(12UL, t0.Seed);
            Assert.AreEqual(1, t0.Night);

            // 开局的信（策划/12 §8.1）：第 1 夜只有正文，没有附言；「下船」收起
            Assert.AreEqual("Letter", Screen.ModalName, "开局先盖一封信");
            Assert.AreEqual(1, _boot.Profile.Nights);
            Assert.IsTrue(File.Exists(TestProfile), "开局就写局外档");
            Assert.IsFalse(Texts().Any(t => t.name.StartsWith("Postscript")), "第 1 夜信上没有附言");
            Assert.IsTrue(Texts().Any(t => t.name == "Body" && t.text.Contains(Story.Letter[0])));
            yield return Shot("20_letter");
            Assert.IsTrue(Screen.ClickModal("Disembark"));
            yield return null;
            Assert.IsNull(Screen.ModalName);

            // 回响（策划/15 §3）：三张大卡 + 重抽；重抽一次换一组，按钮变灰；选一个进地图
            Assert.AreEqual(Run.Options().Count, Screen.CountOptions(), "三张回响卡和重抽都要画出来");
            Assert.AreEqual(RunEngine.EchoChoices, Run.State.Select.Echoes.Count);
            Assert.AreEqual(RunEngine.EchoChoices, Texts().Count(t => t.name == "Tendency"), "每张卡写出倾向");
            yield return Shot("20_echo");
            var before = Run.State.Select.Echoes.ToList();
            Assert.IsTrue(Screen.ClickOption(Run.Options().FindIndex(o => o.Tag == "reroll")));
            yield return null;
            CollectionAssert.AreNotEqual(before, Run.State.Select.Echoes, "重抽换了一组");
            Assert.IsFalse(Screen.ClickOption(Run.Options().FindIndex(o => o.Tag == "reroll")), "每局只能重抽一次");
            string echo = Run.State.Select.Echoes[0];
            Assert.IsTrue(Screen.ClickOption(0));
            yield return null;
            Assert.AreEqual(RunPhase.Map, Screen.Shown);
            CollectionAssert.AreEqual(new[] { echo }, Run.State.Echoes);
            Assert.AreEqual(echo != EchoIds.SeaChart, Screen.Map.BossFogged, "Boss 节点被雾遮着，除非拿了海图");
            var map = Run.State.Map;
            Assert.AreEqual(map.Nodes.Count + 1, Screen.Map.NodeCount, "每个节点 + Boss 都要画出来");
            CollectionAssert.AreEquivalent(map.InRow(0).Select(n => (n.Row, n.Col)), Screen.Map.Choosable);
            AssertTopBar("开局");

            // 第 1 层应该卷在视口里
            yield return null;
            foreach (var n in map.InRow(0))
            {
                float y = Screen.Map.NodeY(0, n.Col);
                Assert.That(y, Is.InRange(Screen.Map.ViewTop, Screen.Map.ViewTop + Screen.Map.ViewHeight), $"第 1 层第 {n.Col} 列不在视口里");
            }
            yield return Shot("21_map_start");
        }

        /// <summary>
        /// 狼人（09 M6.5）：标题 → 选角 → 狼人开局；战斗里理智条 6 格、癫狂那一侧叫「兽形」，人名是狼人的；
        /// 嚎月变身后立绘换成兽形、手里打击的数字 +2 显示成增益色、防御减半显示成减益色；血腥诱饵给敌人挂上「血」状态；
        /// 银链复归后变回人形。截图 40–43。
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator WerewolfSelectBeastFormAndBleed()
        {
            _boot.NextSeed = 33;
            Assert.IsTrue(_boot.ClickTitle("NewRun"));
            yield return null;
            Assert.IsNotNull(_boot.CharacterSelect);
            yield return Shot("40_character_select");
            Assert.IsTrue(_boot.ClickTitle("Pick_werewolf"));
            yield return null;
            yield return null;
            Assert.AreEqual("werewolf", Run.State.Character);
            Assert.AreEqual(6, Run.State.MaxSanity);
            Assert.IsTrue(Screen.ClickModal("Disembark"));
            yield return null;
            yield return PickEcho(EchoIds.SeaChart, EchoIds.Satchel, EchoIds.TidalRange);   // 后面要核对兽形的数值：别拿不眠、深痕
            Run.State.Potions[0] = "bloody_bait";   // 给流血状态一个来源（开战时药水栏抄进战斗）
            yield return null;
            var node = Run.State.Map.InRow(0).First(n => n.Room == RoomType.Battle);
            Assert.IsTrue(Screen.Map.ClickNode(node.Row, node.Col));
            yield return WaitSettled();

            var battle = Screen.Battle;
            var engine = battle.Controller.Engine;
            var player = battle.Player;
            Assert.AreEqual("werewolf", player.Character);
            Assert.AreEqual(6, player.MaxSanity, "理智条按上限 6 排格");
            Assert.AreEqual("兽形", player.Gauge.LunacyName);
            Assert.IsFalse(player.BeastForm);
            Assert.AreEqual(CharacterLibrary.Get("werewolf").Name, engine.Player.Name);

            // 嚎月：10 张牌、每回合 5 张，前两回合一定摸到
            for (int turn = 0; turn < 3 && engine.Player.Hand.All(c => c.Id != "LB02"); turn++)
            {
                Assert.IsTrue(battle.Controller.EndTurn());
                yield return WaitSettled();
            }
            int howl = engine.Player.Hand.FindIndex(c => c.Id == "LB02");
            Assert.GreaterOrEqual(howl, 0, "没摸到嚎月");
            yield return Shot("41_wolf_human");
            Assert.IsTrue(battle.Controller.Play(howl));
            yield return WaitSettled();
            Assert.IsTrue(engine.Player.IsLunatic);
            Assert.IsTrue(player.BeastForm, "变身后立绘换成兽形");

            // 手牌数字：打击 6 + 2 显示增益色，防御 5 减半成 2 显示减益色（10 §4.4 的上色规则，兽血走同一条预览管线）
            var strike = battle.Hand.Cards.FirstOrDefault(c => c.CardId == "B01");
            var defend = battle.Hand.Cards.FirstOrDefault(c => c.CardId == "B02");
            if (strike != null) StringAssert.Contains(">8<", strike.FaceText, "兽形打击 8，带颜色");
            if (defend != null) StringAssert.Contains(">2<", defend.FaceText, "兽形防御 2，带颜色");

            Assert.IsTrue(battle.Controller.UsePotion(0, engine.State.AliveEnemies.First().Index));
            yield return WaitSettled();
            var bled = battle.Enemies.First(v => !v.IsDead);
            Assert.AreEqual(5, bled.StatusAmount(StatusId.Bleed), "敌人身上挂着流血");
            yield return Shot("42_wolf_beast");

            // 银链：复归，变回人形（没摸到就跳过这一段）
            int chain = engine.Player.Hand.FindIndex(c => c.Id == "LB03");
            if (chain >= 0 && engine.CanPlay(chain, out _))
            {
                Assert.IsTrue(battle.Controller.Play(chain));
                yield return WaitSettled();
                Assert.IsFalse(engine.Player.IsLunatic);
                Assert.IsFalse(player.BeastForm, "复归后变回人形");
                yield return Shot("43_wolf_revert");
            }
        }

        /// <summary>
        /// 一路打到 Boss 结算：生命给到 999，保证简单策略也能走完整局；路线预先规划好，把商店、事件、篝火、宝箱、精英都走一遍。
        /// 从信之后的回响开始（09 M7.6）；Boss 是三选一抽的，这里固定成 boss。顺路检查：回响图标挂在理智条旁边、
        /// 地图上 Boss 节点被雾遮着（拿了海图就写名字）、进 Boss 房间的揭晓、鳞卵的孵化倒计时。
        /// </summary>
        IEnumerator PlayFullRun(string character, string boss, string tag)
        {
            // 找一张有路线能走遍 6 种房间的地图（Boss 必经）。地图只看种子，和角色无关
            ulong seed = 2026;
            while (PlanPath(RunEngine.NewRun(seed).State.Map).Kinds < 6 && seed < 2026 + 200) seed++;
            Assert.AreEqual(6, PlanPath(RunEngine.NewRun(seed).State.Map).Kinds, "200 个种子里没有能走遍 6 种房间的地图");
            _boot.NewRun(seed, new RunConfig { MaxHp = 999, Character = character });
            Run.State.Boss = boss;
            Screen.CloseModal();
            yield return null;
            string bossName = EncounterLibrary.Get(boss).Name;
            _seenRooms.Clear();
            var shots = new HashSet<string>();
            bool revealChecked = false, fogChecked = false, iconsChecked = false, bossRevealed = false;
            _eggSeen = false;
            int steps = 0;
            while (!Run.IsOver && steps++ < 8000)
            {
                yield return WaitSettled();
                var s = Run.State;
                if (s.Phase != RunPhase.Battle)
                {
                    AssertTopBar($"第 {s.Floor} 层 {s.Phase}");
                    if (s.Phase != RunPhase.Map)
                        Assert.AreEqual(Run.Options().Count, Screen.CountOptions(), $"{s.Phase}：面板应该把每个选项都画出来");
                }
                if (steps % 200 == 0) Debug.Log($"[RunFlowTests] 第 {steps} 步：第 {s.Floor} 层 {s.Phase}，生命 {s.Hp}/{s.MaxHp}");
                string shot = ShotName(s);
                if (shot != null && shots.Add(shot)) yield return Shot($"{shot}_{tag}");
                if (s.Phase == RunPhase.Map && s.Row == s.Map.Height - 1 && shots.Add("22_map_fog")) yield return Shot($"22_map_boss_{tag}");
                if (s.Phase == RunPhase.Map && s.Row >= 0 && !fogChecked)
                {
                    fogChecked = true;
                    bool known = Run.RevealedBoss != null;
                    Assert.AreEqual(!known, Screen.Map.BossFogged, "地图上的 Boss 节点：不知道是谁就被雾遮着");
                    Assert.AreEqual(known ? bossName : null, Screen.Map.BossNameShown);
                }
                if (s.Phase == RunPhase.Event || s.Phase == RunPhase.Shop || s.Phase == RunPhase.Campfire)
                {
                    var plate = Screen.Panel.GetComponentInChildren<Illustration>();
                    Assert.IsNotNull(plate, $"{s.Phase}：应该显示插图（ArtDatabase 的插图表）");
                    Assert.IsNotNull(plate.Art.texture);
                    var hook = Screen.Panel.GetComponentInChildren<HoverHook>();
                    if (hook != null && !revealChecked)
                    {
                        // 悬停会扣理智上限的选项：插图的癫狂层浮出来，移开退回去
                        revealChecked = true;
                        hook.Enter();
                        yield return new WaitForSeconds(0.5f);
                        Assert.Greater(plate.Reveal, 0.5f, "悬停扣理智的选项时癫狂层应该浮出");
                        yield return Shot("28_event_reveal");
                        hook.Exit();
                        yield return new WaitForSeconds(0.5f);
                        Assert.Less(plate.Reveal, 0.05f);
                    }
                }

                switch (s.Phase)
                {
                    case RunPhase.Map:
                        var node = ChooseNode();
                        _seenRooms.Add(node.Room);
                        Assert.IsTrue(Screen.Map.ClickNode(node.Row, node.Col), $"点不了节点 {node.Row},{node.Col}");
                        if (node.Room == RoomType.Boss)
                        {
                            yield return null;
                            Assert.AreEqual(bossName, Screen.LastReveal, "进 Boss 房间揭晓是谁");
                            bossRevealed = true;
                            yield return Shot($"50_reveal_{tag}");
                        }
                        break;
                    case RunPhase.Battle:
                        Assert.AreEqual(RunPhase.Battle, Screen.Shown);
                        Assert.IsNotNull(Screen.Battle);
                        if (!iconsChecked)
                        {
                            iconsChecked = true;
                            Assert.AreEqual(Run.State.Echoes.Count, Screen.Battle.Player.Gauge.EchoIcons.Count, "回响图标挂在理智条旁边");
                        }
                        var egg = Screen.Battle.Enemies.FirstOrDefault(v => v.DefId == EnemyIds.ScaleEgg && v.NameShown.Contains("孵化"));
                        if (egg != null && !_eggSeen)
                        {
                            _eggSeen = true;
                            yield return Shot($"51_eggs_{tag}");
                        }
                        yield return BattleStep();
                        break;
                    default:
                        Assert.IsTrue(Screen.ClickOption(ChooseOption()), $"{s.Phase} 的选项点不了");
                        break;
                }
                yield return null;
            }
            yield return WaitSettled();
            Assert.IsTrue(bossRevealed, "这一局走到了 Boss 房间");
            Assert.AreEqual(1, Run.State.Echoes.Count, "开局选了一个回响");
            Assert.AreEqual(RunPhase.Victory, Run.State.Phase, $"没有通关：{Run.Title}");
            Assert.AreEqual(RunPhase.Victory, Screen.Shown);
            Assert.IsNotNull(Screen.Panel, "通关后应该显示结算面板");
            Assert.IsFalse(File.Exists(TestSave), "一局结束要删档");
            var tel = ReadTelemetry();
            Assert.AreEqual(RunOutcome.Victory, tel.Outcome);
            Assert.AreEqual(boss, tel.Boss);
            CollectionAssert.AreEqual(Run.State.Echoes, tel.Echoes);
            Assert.AreEqual(Run.State.Path.Count, tel.Nodes.Count, "遥测里每个节点一条");
            Assert.AreEqual(Run.State.Stats.Battles.Count, tel.Battles.Count);
            Assert.Greater(tel.PlaySeconds, 0, "单局用时");
            Assert.That(tel.Nodes.Select(n => n.Seconds), Is.Ordered, "节点的进入时间递增");
            Assert.IsTrue(Texts().Any(t => t.text == Story.VictoryTitle), "结算标题应该是「天还没亮」");
            Assert.IsTrue(Texts().Any(t => t.name == "Marks" && t.text.Contains(Story.ActEnd) && t.text.Contains("第 1 夜")));
            yield return Shot($"29_victory_{tag}");
            Debug.Log($"[RunFlowTests] {character} 对 {bossName}：种子 {seed}，{steps} 步通关，回响 {Run.State.Echoes[0]}，走过的房间：{string.Join("、", _seenRooms)}；牌组 {Run.State.Deck.Count} 张");
            foreach (var room in new[] { RoomType.Battle, RoomType.Elite, RoomType.Campfire, RoomType.Treasure, RoomType.Shop, RoomType.Event, RoomType.Boss })
                Assert.Contains(room, _seenRooms.ToList(), $"这条路线没走到 {room}");
        }

        readonly HashSet<RoomType> _seenRooms = new HashSet<RoomType>();
        bool _eggSeen;

        /// <summary>调查员打到溺亡司祭：局外档记下打倒过司祭（附言 D 的条件），回到标题不会把通关记成放弃。</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator FullRunReachesBossVictory()
        {
            yield return PlayFullRun(CharacterIds.Investigator, EncounterIds.Priest, "priest");
            Assert.IsTrue(_boot.Profile.BeatPriest);
            Assert.IsTrue(ProfileFiles.Read().BeatPriest, "局外档要写到盘上");

            // 结算 → 回到标题，没有存档了；回到标题不会把通关记成放弃
            Assert.IsTrue(Screen.Panel.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == "Back"));
            Screen.ExitToTitle(true);
            yield return null;
            Assert.IsNotNull(_boot.Title);
            Assert.IsFalse(_boot.ClickTitle("Continue"));
            Assert.AreEqual(RunOutcome.Victory, ReadTelemetry().Outcome);
        }

        /// <summary>狼人打到鳞母（09 M7.6）：卵的孵化倒计时写在名字后面；打倒的不是司祭，附言 D 的条件不记。</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator WerewolfFullRunBeatsScaleMother()
        {
            yield return PlayFullRun("werewolf", "boss_scale_mother", "mother");
            Assert.IsTrue(_eggSeen, "鳞母产过卵，卵的名字后面写着孵化倒计时");
            Assert.IsFalse(_boot.Profile.BeatPriest, "打倒鳞母不算打倒司祭");
        }

        /// <summary>
        /// 倒影（09 M7.6）：拿海图开局就知道 Boss 是谁，地图上 Boss 节点写名字、没有雾；直接走到最后一层进 Boss 房间，揭晓；
        /// 打的时候名字后面写着它下回合抄哪张牌。生命给到 999，打到结算。
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator SeaChartRevealsMirror_AndItShowsWhatItCopies()
        {
            // 开局就给海图的种子（新的局外档：没有要避开的 Boss，开局和直接 NewRun 一样）
            ulong seed = Enumerable.Range(1, 400).Select(i => (ulong)i)
                .First(x => RunEngine.NewRun(x, new RunConfig { Echoes = true }).State.Select.Echoes.Contains(EchoIds.SeaChart));
            _boot.NewRun(seed, new RunConfig { MaxHp = 999 });
            Run.State.Boss = "boss_mirror";
            yield return null;
            yield return PickEcho(EchoIds.SeaChart);
            Assert.IsFalse(Screen.Map.BossFogged, "拿了海图：雾散了");
            Assert.AreEqual("倒影", Screen.Map.BossNameShown);

            // 直接站到最后一层，下一步就是 Boss
            var s = Run.State;
            s.Row = s.Map.Height - 1;
            s.Col = s.Map.InRow(s.Row).First().Col;
            s.Floor = s.Row + 1;
            Screen.Map.Refresh(Run);
            yield return null;
            yield return Shot("22_map_boss_known");
            // 揭晓按正常速度播，截图里才看得到雾
            RunScreen.BattleSpeed = 1;
            Assert.IsTrue(Screen.Map.ClickNode(s.Map.Height, 0));
            yield return null;
            Assert.AreEqual("倒影", Screen.LastReveal);
            yield return Shot("50_reveal_mirror");
            yield return new WaitForSeconds(2.6f);
            Screen.Battle.Director.Speed = RunScreen.BattleSpeed = 40;

            bool copySeen = false;
            int steps = 0;
            while (Run.State.Phase == RunPhase.Battle && steps++ < 3000)
            {
                yield return WaitSettled();
                var battle = Screen.Battle;
                if (battle != null && !copySeen && battle.Enemies[0].NameShown.Contains("抄："))
                {
                    copySeen = true;
                    yield return Shot("51_mirror_copy");
                }
                yield return BattleStep();
                yield return null;
            }
            yield return WaitSettled();
            Assert.IsTrue(copySeen, "倒影的名字后面要写出它下回合抄哪张牌");
            Assert.AreEqual(RunPhase.Victory, Run.State.Phase, $"没有打倒倒影：{Run.Title}");
            Assert.AreEqual("boss_mirror", ReadTelemetry().Boss);
        }

        /// <summary>菜单 → 设置：全屏 / 窗口来回切，窗口大小只在窗口模式下能点；「返回」回到菜单。标题画面上也能打开设置。</summary>
        [UnityTest]
        public IEnumerator SettingsTogglesWindowMode()
        {
            _boot.NewRun(5);
            yield return null;
            yield return PickEcho();
            Screen.TopBar.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "菜单").onClick.Invoke();
            Assert.IsTrue(Screen.ClickModal("Settings"));
            Assert.AreEqual("Settings", Screen.ModalName);
            yield return Shot("35_settings");

            bool windowed = DisplaySettings.Windowed;
            bool CanResize() => Screen.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "WindowSize").interactable;
            Assert.AreEqual(windowed, CanResize());
            Assert.IsTrue(Screen.ClickModal("Display"));
            Assert.AreEqual(!windowed, DisplaySettings.Windowed);
            Assert.AreEqual(!windowed, CanResize(), "切换后重画，窗口大小跟着能点 / 不能点");
            Assert.IsTrue(Screen.ClickModal("Display"));
            Assert.AreEqual(windowed, DisplaySettings.Windowed);
            Assert.IsTrue(Screen.ClickModal("Back"));
            Assert.AreEqual("Menu", Screen.ModalName);

            Screen.ExitToTitle(false);
            yield return null;
            Assert.IsTrue(_boot.ClickTitle("Settings"));
            Assert.IsNotNull(_boot.Title.Find("SettingsLayer"));
            Assert.IsTrue(_boot.ClickTitle("Back"));
            yield return null;
            Assert.IsNull(_boot.Title.Find("SettingsLayer"));
        }

        /// <summary>战斗中保存退出 → 继续：回到这场战斗的开头，和刚进入时完全一样；打完在奖励界面退出 → 继续：还在奖励界面。</summary>
        [UnityTest]
        public IEnumerator ContinueResumesSavedRun()
        {
            _boot.NewRun(77);
            yield return null;
            yield return PickEcho();
            var first = Run.State.Map.InRow(0).First();
            Assert.IsTrue(Screen.Map.ClickNode(0, first.Col));
            yield return WaitSettled();
            Assert.AreEqual(RunPhase.Battle, Run.State.Phase);
            string atStart = Run.Hash();
            var hand = Screen.Battle.Controller.Engine.Player.Hand.Select(c => c.Id).ToList();
            for (int i = 0; i < 3; i++)
            {
                yield return BattleStep();
                yield return WaitSettled();
            }

            // 菜单 → 保存并回到标题 → 继续
            Screen.TopBar.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "菜单").onClick.Invoke();
            Assert.AreEqual("Menu", Screen.ModalName);
            Assert.IsTrue(Screen.ClickModal("Exit"));
            yield return null;
            Assert.IsNotNull(_boot.Title);
            yield return Shot("30_title_continue");
            Assert.IsTrue(_boot.ClickTitle("Continue"));
            yield return WaitSettled();
            Assert.AreEqual(RunPhase.Battle, Run.State.Phase);
            Assert.AreEqual(atStart, Run.Hash(), "读档后战斗应该从头开始，和第一次进入时一模一样");
            CollectionAssert.AreEqual(hand, Screen.Battle.Controller.Engine.Player.Hand.Select(c => c.Id).ToList());

            // 打完这场，到奖励界面；换一次场景（相当于关掉游戏重开）再继续
            for (int i = 0; i < 300 && Run.State.Phase == RunPhase.Battle; i++)
            {
                yield return BattleStep();
                yield return WaitSettled();
            }
            Assert.AreEqual(RunPhase.Reward, Run.State.Phase);
            Assert.AreEqual(RunPhase.Reward, Screen.Shown);
            string atReward = Run.Hash();
            yield return LoadRunScene();
            Assert.IsTrue(_boot.ClickTitle("Continue"));
            yield return WaitSettled();
            Assert.AreEqual(RunPhase.Reward, Screen.Shown);
            Assert.AreEqual(atReward, Run.Hash());
            Assert.AreEqual(Run.Options().Count, Screen.CountOptions());
            var tel = ReadTelemetry();
            Assert.AreEqual(RunOutcome.Unfinished, tel.Outcome);
            Assert.AreEqual(1, tel.Battles.Count, "读档前打完的战斗在遥测里");

            // 读档继续不算新的一夜
            Assert.AreEqual(1, _boot.Profile.Nights);
            Assert.AreEqual(1, Run.State.Stats.Night);

            // 放弃这一局：记成 Abandoned；再开一局是本机第 2 局，也是第 2 夜：信上多出附言 A，标题画面显示第几夜
            Screen.ExitToTitle(true);
            yield return null;
            Assert.AreEqual(RunOutcome.Abandoned, ReadTelemetry().Outcome);
            _boot.NewRun(78);
            yield return null;
            Assert.AreEqual(2, Directory.GetFiles(TestTelemetry, "*.json").Length);
            Assert.AreEqual(2, Run.State.Stats.RunIndex);
            Assert.AreEqual(2, Run.State.Stats.Night);
            Assert.AreEqual("Letter", Screen.ModalName);
            var ps = Texts().Where(t => t.name.StartsWith("Postscript")).ToList();
            Assert.AreEqual(1, ps.Count, "第 2 夜多出一条附言");
            Assert.AreEqual(Story.Postscripts[0].Text, ps[0].text);
            yield return new WaitForSeconds(3.5f);
            Assert.Greater(ps[0].alpha, 0.95f, "新附言洇出来之后是实色");
            yield return Shot("32_letter_postscript");
            Screen.ExitToTitle(false);
            yield return null;
            Assert.IsTrue(_boot.Title.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.name == "Night" && t.text == "第 2 夜"), "第 2 夜起标题画面显示第几夜");
            yield return Shot("33_title_night");
        }

        /// <summary>附言全部出现时信纸最长：信纸和「下船」都要在屏幕里。</summary>
        [UnityTest]
        public IEnumerator LetterWithAllPostscriptsFitsOnScreen()
        {
            var profile = new Profile { Nights = 9, Died = true, BeatPriest = true };
            profile.Postscripts.AddRange(Story.Postscripts.Take(3).Select(ps => ps.Id));
            ProfileFiles.Write(profile);
            yield return LoadRunScene();
            _boot.NewRun(40);
            yield return null;
            Assert.AreEqual(10, Run.State.Stats.Night);
            Assert.AreEqual(4, Texts().Count(t => t.name.StartsWith("Postscript")), "第 10 夜四条附言都在（D 是这一夜新出现的）");
            yield return new WaitForSeconds(3.5f);
            yield return Shot("34_letter_full");
            var corners = new Vector3[4];
            foreach (var name in new[] { "Sheet", "Disembark" })
            {
                var rt = Screen.GetComponentsInChildren<RectTransform>().First(r => r.name == name);
                var canvas = (RectTransform)Screen.transform;
                rt.GetWorldCorners(corners);
                foreach (var c in corners)
                {
                    var local = canvas.InverseTransformPoint(c);
                    Assert.That(Mathf.Abs(local.y), Is.LessThanOrEqualTo(canvas.rect.height / 2 + 1), $"{name} 超出了屏幕");
                }
            }
        }

        /// <summary>跑局界面上所有的字（含弹窗和面板）。</summary>
        List<TMPro.TextMeshProUGUI> Texts() => Screen.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).ToList();

        /// <summary>这一局的遥测文件（TelemetryFiles 写的 JSON）。</summary>
        RunTelemetry ReadTelemetry()
        {
            var state = Screen != null ? Run.State : null;
            string path = state != null ? TelemetryFiles.PathOf(state)
                : Directory.GetFiles(TestTelemetry, "*.json").OrderBy(File.GetLastWriteTimeUtc).Last();
            Assert.IsTrue(File.Exists(path), $"没有遥测文件 {path}");
            return Newtonsoft.Json.JsonConvert.DeserializeObject<RunTelemetry>(File.ReadAllText(path), new Newtonsoft.Json.Converters.StringEnumConverter());
        }

        [UnityTest]
        public IEnumerator DeckModalAndPotionDrop()
        {
            var run = RunEngine.NewRun(5);
            run.State.Potions[0] = "acid_vial";
            _boot.Play(run);
            yield return null;
            Assert.AreEqual("acid_vial", Screen.TopBar.PotionIds[0]);

            Screen.OpenDeck();
            yield return null;
            Assert.AreEqual("Deck", Screen.ModalName);
            int tiles = Object.FindObjectsByType<CardTile>(FindObjectsSortMode.None).Count(t => t.name.StartsWith("Deck "));
            Assert.AreEqual(Run.State.Deck.Count, tiles, "牌组弹窗里每张牌都要画出来");
            yield return Shot("31_deck");
            Assert.IsTrue(Screen.ClickModal("Close"));
            yield return null;
            Assert.IsNull(Screen.ModalName);

            // 不在战斗里点药水：弹出说明，可以丢掉
            Screen.TopBar.transform.Find("Potion 0").GetComponent<ClickHandler>().OnClick(null);
            Assert.AreEqual("Potion", Screen.ModalName);
            Assert.IsTrue(Screen.ClickModal("Drop"));
            yield return null;
            Assert.IsNull(Run.State.Potions[0]);
            Assert.IsNull(Screen.TopBar.PotionIds[0]);
        }

        [UnityTest]
        public IEnumerator MapHoverHighlightsPaths()
        {
            _boot.NewRun(31);
            yield return PickEcho();    // 收起开局的信、选回响，截图里要看地图
            yield return null;
            var map = Run.State.Map;
            var target = map.InRow(3).First();
            Screen.Map.Hover(Screen.Map.NodeAt(target.Row, target.Col), true);
            int hot = Screen.Map.HighlightedEdges;
            Assert.Greater(hot, 3, "悬停第 4 层的节点，至少要亮起从出发点到它的 4 段路");
            yield return Shot("22_map_hover");
            Screen.Map.Hover(Screen.Map.NodeAt(target.Row, target.Col), false);
            Assert.AreEqual(0, Screen.Map.HighlightedEdges);

            // 走一步之后，走过的路是实线
            Assert.IsTrue(Screen.Map.ClickNode(0, map.InRow(0).First().Col));
            yield return WaitSettled();
            Assert.AreEqual(RunPhase.Battle, Run.State.Phase);
            Assert.IsFalse(Screen.Map.gameObject.activeSelf, "战斗时地图收起来");
        }

        // ------------------------------------------------------------------ 策略

        List<int> _plan;

        /// <summary>按开局时规划好的路线走（<see cref="PlanPath"/>）；最后一层之后是 Boss。</summary>
        MapNode ChooseNode()
        {
            var map = Run.State.Map;
            if (_plan == null) _plan = PlanPath(map).Path;
            int row = Run.State.Row + 1;
            if (row >= map.Height) return new MapNode { Row = map.Height, Col = 0, Room = RoomType.Boss };
            return map.Get(row, _plan[row]);
        }

        /// <summary>在地图上找一条经过房间类型最多的路线：按行往上推「到这个节点、见过哪些类型」的全部组合（7 种类型 = 128 种组合），再倒推回来。</summary>
        static (List<int> Path, int Kinds) PlanPath(MapData map)
        {
            int Bit(RoomType room) => 1 << (int)room;
            var from = new Dictionary<(int Row, int Col, int Mask), (int Col, int Mask)>();
            foreach (var n in map.InRow(0)) from[(0, n.Col, Bit(n.Room))] = (-1, 0);
            for (int r = 0; r < map.Height - 1; r++)
                foreach (var key in from.Keys.Where(k => k.Row == r).ToList())
                    foreach (var c in map.Get(r, key.Col).Children)
                    {
                        var next = (r + 1, c, key.Mask | Bit(map.Get(r + 1, c).Room));
                        if (!from.ContainsKey(next)) from[next] = (key.Col, key.Mask);
                    }
            int Count(int mask) => Enumerable.Range(0, 8).Count(b => (mask & (1 << b)) != 0);
            var cur = from.Keys.Where(k => k.Row == map.Height - 1).OrderByDescending(k => Count(k.Mask)).First();
            int kinds = Count(cur.Mask);   // 下面倒推时 cur 会一路走回第 1 层，先记下终点的覆盖数
            var path = new List<int>();
            while (true)
            {
                path.Add(cur.Col);
                var prev = from[cur];
                if (prev.Col < 0) break;
                cur = (cur.Row - 1, prev.Col, prev.Mask);
            }
            path.Reverse();
            return (path, kinds);
        }

        /// <summary>
        /// 非战斗界面的简单策略：奖励全领；选牌拿第一张；商店先买第一张买得起的牌、再删一张、然后离开；
        /// 篝火能锻造就锻造；事件选第一个能选的；药水栏满了放弃新药水。
        /// </summary>
        int ChooseOption()
        {
            var options = Run.Options();
            int First(System.Func<RunOption, bool> f) => options.FindIndex(o => o.Enabled && f(o));
            switch (Run.State.Phase)
            {
                case RunPhase.Reward:
                    return 0;
                case RunPhase.Select:
                    var sel = Run.State.Select;
                    if (sel.Purpose == SelectPurpose.DiscardPotion) return options.Count - 1;
                    return 0;
                case RunPhase.Shop:
                    if (!_boughtCard)
                    {
                        _boughtCard = true;
                        int buy = First(o => o.CardId != null);
                        if (buy >= 0) return buy;
                    }
                    int remove = First(o => o.Tag == "remove");
                    if (remove >= 0) return remove;
                    return options.FindIndex(o => o.Tag == "leave");
                case RunPhase.Campfire:
                    int smith = First(o => o.Tag == "smith");
                    return smith >= 0 ? smith : options.FindIndex(o => o.Tag == "rest");
                default:
                    return First(o => true);
            }
        }

        bool _boughtCard;

        /// <summary>
        /// 和 BattleContentTests 一样的出牌策略：有选择选第一项；能打攻击先打攻击，目标是下标最小的活敌人；都打不了结束回合。
        /// 会把自己打崩溃的代价牌不打：崩溃扣 10% 最大生命，给 999 血也挡不住理智上限只有 6 的狼人一直崩。
        /// </summary>
        IEnumerator BattleStep()
        {
            var battle = Screen.Battle;
            var engine = battle.Controller.Engine;
            if (engine.State.Outcome != BattleOutcome.Ongoing) yield break;
            if (battle.Controller.AwaitingChoice)
            {
                battle.Choice.Pick(0);
                yield break;
            }
            var target = engine.State.Enemies.FirstOrDefault(en => !en.IsDead);
            int targetIndex = target?.Index ?? -1;
            var hand = engine.Player.Hand;
            bool Safe(int i) => engine.CanPlay(i, out _) &&
                hand[i].SanityCost(engine.UsesMadFace(hand[i]), hand[i].Def.XCost ? engine.Player.Energy : 0) < engine.Player.Sanity;
            int pick = -1;
            for (int i = 0; i < hand.Count && pick < 0; i++)
                if (hand[i].Def.Type == CardType.Attack && Safe(i)) pick = i;
            for (int i = 0; i < hand.Count && pick < 0; i++)
                if (Safe(i)) pick = i;
            if (pick >= 0) Assert.IsTrue(battle.Controller.Play(pick, hand[pick].Def.Target == CardTarget.Enemy ? targetIndex : -1));
            else Assert.IsTrue(battle.Controller.EndTurn());
        }

        // ------------------------------------------------------------------ 工具


        void AssertTopBar(string where)
        {
            var s = Run.State;
            Assert.AreEqual($"◎ {s.Gold}", Screen.TopBar.GoldText, $"{where}：金币");
            Assert.AreEqual(s.Floor <= 0 ? "出发" : $"第 {s.Floor} 层", Screen.TopBar.FloorText, $"{where}：层数");
            for (int i = 0; i < s.Potions.Count; i++) Assert.AreEqual(s.Potions[i], Screen.TopBar.PotionIds[i], $"{where}：药水栏 {i}");
            CollectionAssert.AreEquivalent(s.Relics.Distinct(), Screen.TopBar.RelicIds, $"{where}：遗物");
        }

        /// <summary>
        /// 开局的回响（09 M7.6）：收起信，选 prefer 里第一个出现的；都没有就重抽一次再找，还没有就选第一张。
        /// </summary>
        IEnumerator PickEcho(params string[] prefer)
        {
            Screen.CloseModal();
            yield return null;
            Assert.AreEqual(SelectPurpose.Echo, Run.State.Select?.Purpose, "开局先选回响");
            int Find() => Run.Options().FindIndex(o => prefer.Any(id => o.Tag == "echo:" + id));
            int pick = Find();
            if (pick < 0 && prefer.Length > 0)
            {
                Assert.IsTrue(Screen.ClickOption(Run.Options().FindIndex(o => o.Tag == "reroll")));
                yield return null;
                pick = Find();
            }
            Assert.IsTrue(Screen.ClickOption(System.Math.Max(0, pick)));
            yield return null;
            Assert.AreEqual(RunPhase.Map, Screen.Shown);
        }

        /// <summary>等到能操作：战斗事件播完、打完后的横幅等待结束。</summary>
        IEnumerator WaitSettled(float timeout = 60)
        {
            float t = 0;
            while (Screen.Transitioning || (Screen.Battle != null && !Screen.Battle.Director.IsIdle))
            {
                t += Time.unscaledDeltaTime;
                if (t > timeout) Assert.Fail("等待超时");
                yield return null;
            }
            yield return null;
        }

        static string ShotName(RunState s)
        {
            switch (s.Phase)
            {
                case RunPhase.Map: return s.Row < 0 ? null : "23_map";
                case RunPhase.Battle: return "24_battle";
                case RunPhase.Reward: return "25_reward";
                case RunPhase.Select:
                    return s.Select.Purpose == SelectPurpose.AddCard ? "26_select_card" : s.Select.Purpose == SelectPurpose.Echo ? null
                        : s.Select.Purpose == SelectPurpose.DiscardPotion ? null : "26_select_deck";
                case RunPhase.Shop: return "27_shop";
                case RunPhase.Campfire: return "28_campfire";
                case RunPhase.Event: return "28_event";
                default: return null;
            }
        }

        /// <summary>把主相机渲染到 1920×1080 的 RenderTexture 存成 PNG（同 BattleGreyboxTests）。</summary>
        IEnumerator Shot(string name)
        {
            yield return new WaitForSeconds(0.3f);
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
