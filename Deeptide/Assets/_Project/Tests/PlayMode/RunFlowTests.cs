using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Map;
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
            yield return null;

            Assert.IsNotNull(Screen);
            Assert.IsNull(_boot.Title);
            Assert.AreEqual(RunPhase.Map, Screen.Shown);
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
        /// 一路打到 Boss 结算：生命给到 999，保证简单策略也能走完整局；路线预先规划好，把商店、事件、篝火、宝箱、精英都走一遍。
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator FullRunReachesBossVictory()
        {
            // 找一张有路线能走遍 6 种房间的地图（Boss 必经）
            ulong seed = 2026;
            while (PlanPath(RunEngine.NewRun(seed).State.Map).Kinds < 6 && seed < 2026 + 200) seed++;
            Assert.AreEqual(6, PlanPath(RunEngine.NewRun(seed).State.Map).Kinds, "200 个种子里没有能走遍 6 种房间的地图");
            _boot.NewRun(seed, new RunConfig { MaxHp = 999 });
            yield return null;
            var seenRooms = new HashSet<RoomType>();
            var shots = new HashSet<string>();
            bool revealChecked = false;
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
                if (shot != null && shots.Add(shot)) yield return Shot(shot);
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
                        seenRooms.Add(node.Room);
                        Assert.IsTrue(Screen.Map.ClickNode(node.Row, node.Col), $"点不了节点 {node.Row},{node.Col}");
                        break;
                    case RunPhase.Battle:
                        Assert.AreEqual(RunPhase.Battle, Screen.Shown);
                        Assert.IsNotNull(Screen.Battle);
                        yield return BattleStep();
                        break;
                    default:
                        Assert.IsTrue(Screen.ClickOption(ChooseOption()), $"{s.Phase} 的选项点不了");
                        break;
                }
                yield return null;
            }
            yield return WaitSettled();
            Assert.AreEqual(RunPhase.Victory, Run.State.Phase, $"没有通关：{Run.Title}");
            Assert.AreEqual(RunPhase.Victory, Screen.Shown);
            Assert.IsNotNull(Screen.Panel, "通关后应该显示结算面板");
            Assert.IsFalse(File.Exists(TestSave), "一局结束要删档");
            var tel = ReadTelemetry();
            Assert.AreEqual(RunOutcome.Victory, tel.Outcome);
            Assert.AreEqual(Run.State.Path.Count, tel.Nodes.Count, "遥测里每个节点一条");
            Assert.AreEqual(Run.State.Stats.Battles.Count, tel.Battles.Count);
            Assert.Greater(tel.PlaySeconds, 0, "单局用时");
            Assert.That(tel.Nodes.Select(n => n.Seconds), Is.Ordered, "节点的进入时间递增");
            // 通关文案（策划/12 §8.3）；局外档记下打倒过司祭（下次附言 D 才有资格出现）
            Assert.IsTrue(Texts().Any(t => t.text == Story.VictoryTitle), "结算标题应该是「天还没亮」");
            Assert.IsTrue(Texts().Any(t => t.name == "Marks" && t.text.Contains(Story.ActEnd) && t.text.Contains("第 1 夜")));
            Assert.IsTrue(_boot.Profile.BeatPriest);
            Assert.IsTrue(ProfileFiles.Read().BeatPriest, "局外档要写到盘上");
            yield return Shot("29_victory");
            Debug.Log($"[RunFlowTests] 种子 {seed}，{steps} 步通关，走过的房间：{string.Join("、", seenRooms)}；牌组 {Run.State.Deck.Count} 张");
            foreach (var room in new[] { RoomType.Battle, RoomType.Elite, RoomType.Campfire, RoomType.Treasure, RoomType.Shop, RoomType.Event, RoomType.Boss })
                Assert.Contains(room, seenRooms.ToList(), $"这条路线没走到 {room}");

            // 结算 → 回到标题，没有存档了；回到标题不会把通关记成放弃
            Assert.IsTrue(Screen.Panel.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == "Back"));
            Screen.ExitToTitle(true);
            yield return null;
            Assert.IsNotNull(_boot.Title);
            Assert.IsFalse(_boot.ClickTitle("Continue"));
            Assert.AreEqual(RunOutcome.Victory, ReadTelemetry().Outcome);
        }

        /// <summary>战斗中保存退出 → 继续：回到这场战斗的开头，和刚进入时完全一样；打完在奖励界面退出 → 继续：还在奖励界面。</summary>
        [UnityTest]
        public IEnumerator ContinueResumesSavedRun()
        {
            _boot.NewRun(77);
            yield return null;
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
            Screen.CloseModal();    // 收起开局的信，截图里要看地图
            yield return null;
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

        /// <summary>和 BattleContentTests 一样的出牌策略：有选择选第一项；能打攻击先打攻击，目标是下标最小的活敌人；都打不了结束回合。</summary>
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
            int pick = -1;
            for (int i = 0; i < hand.Count && pick < 0; i++)
                if (hand[i].Def.Type == CardType.Attack && engine.CanPlay(i, out _)) pick = i;
            for (int i = 0; i < hand.Count && pick < 0; i++)
                if (engine.CanPlay(i, out _)) pick = i;
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
                case RunPhase.Select: return s.Select.Purpose == SelectPurpose.AddCard ? "26_select_card" : s.Select.Purpose == SelectPurpose.DiscardPotion ? null : "26_select_deck";
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
