using System;
using System.Collections;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Potions;
using Deeptide.Core.Run;
using Deeptide.View.Battle;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View.Run
{
    /// <summary>
    /// 跑局界面（策划/08 §8「地图、战斗、商店、事件都是 Run 场景里切换的 UI 面板」）：持有 RunEngine，所有操作都变成 RunCommand。
    /// 层级从下到上：地图 → 战斗 → 面板（奖励、商店、篝火、事件、选择、结算）→ 顶栏 → 弹窗（牌组、菜单、丢药水）→ 提示框。
    /// 面板每次都按 RunEngine.Options() 重新画；选项按钮一律叫「Option 下标」。战斗打完后等横幅播完再换面板，奖励叠在战场上。
    /// 存档点（进入节点、战斗结束、回到地图）一变就调用 save；退出到标题时另存一次当前状态。
    /// </summary>
    public sealed class RunScreen : MonoBehaviour
    {
        /// <summary>战斗回放倍速（测试调快用）。</summary>
        public static float BattleSpeed = 1;
        /// <summary>战斗分出胜负后，等横幅显示多久再切到奖励 / 结算（秒，按 BattleSpeed 缩短）。</summary>
        const float AfterBattleDelay = 0.9f;

        public RunEngine Run { get; private set; }
        public MapView Map { get; private set; }
        /// <summary>当前的战斗界面；打完后在奖励面板下面留到回地图为止。</summary>
        public BattleScreen Battle { get; private set; }
        public TopBar TopBar { get; private set; }
        /// <summary>当前画出来的是哪个阶段（战斗刚打完、还在等横幅时仍是 Battle）。</summary>
        public RunPhase Shown { get; private set; }
        /// <summary>当前面板的根节点（地图和战斗阶段为 null）。</summary>
        public RectTransform Panel => _panel;
        /// <summary>当前弹窗的名字（Deck / Menu / Potion），没有弹窗为 null。</summary>
        public string ModalName => _modal != null ? _modal.name : null;
        /// <summary>战斗打完、正在等横幅（这段时间不接受操作）。</summary>
        public bool Transitioning => _transition;
        /// <summary>最近一条提示（测试用）。</summary>
        public string LastToast { get; private set; }

        Camera _camera;
        RectTransform _root, _battleLayer, _panelLayer, _modalLayer, _panel, _modal;
        Tooltip _tooltip;
        TextMeshProUGUI _toast;
        Action<RunEngine> _save;
        Action<bool> _exit;
        int _savedPoints;
        bool _transition, _confirmAbandon;

        /// <param name="save">存档点变了时调用（跑局结束时也调用一次，由外层删档）。</param>
        /// <param name="exit">回到标题：参数为 true 表示放弃这一局（删档），false 表示保存后退出。</param>
        public static RunScreen Create(Camera camera, RunEngine run, Action<RunEngine> save, Action<bool> exit)
        {
            var root = RunUi.Canvas("RunScreen", camera);
            var screen = root.gameObject.AddComponent<RunScreen>();
            screen._camera = camera;
            screen.Run = run;
            screen._save = save;
            screen._exit = exit;
            screen._savedPoints = run.SavePoints;
            screen.Build();
            screen.Show();
            return screen;
        }

        void Build()
        {
            _root = (RectTransform)transform;
            Ui.Box("Background", _root, Palette.Background).rectTransform.Fill();
            Map = MapView.Create(_root, this);
            _battleLayer = Ui.Rect("Battle", _root).Fill();
            _panelLayer = Ui.Rect("Panels", _root).Fill();
            TopBar = TopBar.CreateForRun(_root, PotionClicked, () => { if (Battle != null) Battle.ToggleLog(); }, OpenDeck, OpenMenu);
            _modalLayer = Ui.Rect("Modals", _root).Fill();
            _toast = Ui.Text("Toast", _root, "", 32, Palette.Blood);
            _toast.outlineWidth = 0.2f;
            _toast.outlineColor = Palette.Paper;
            _toast.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -TopBar.Height - 50), new Vector2(1200, 50));
            _tooltip = Tooltip.Create(_root);
        }

        // ------------------------------------------------------------------ 按阶段换界面

        void Show()
        {
            CloseModal();
            ClearPanel();
            var s = Run.State;

            if (s.Phase == RunPhase.Battle)
            {
                if (Battle == null || Battle.Controller.Engine != Run.Battle)
                {
                    // 金币、层数按局里的状态；药水和遗物马上换成绑这场战斗的引擎
                    TopBar.Bind(s);
                    StartBattle();
                }
                Shown = RunPhase.Battle;
                return;
            }
            TopBar.Bind(s);

            // 奖励、选奖励牌、结算叠在刚打完的战场上；其余界面回到地图层
            bool overBattle = s.Phase == RunPhase.Reward || s.Phase == RunPhase.Select || Run.IsOver;
            if (Battle != null && !overBattle) DestroyBattle();
            Map.gameObject.SetActive(Battle == null);
            if (Battle == null) Map.Refresh(Run);

            _panel = RunPanels.Build(_panelLayer, this);
            Shown = s.Phase;
        }

        void ClearPanel()
        {
            if (_panel != null) Destroy(_panel.gameObject);
            _panel = null;
        }

        void StartBattle()
        {
            DestroyBattle();
            Map.gameObject.SetActive(false);
            Battle = BattleScreen.Create(_camera, _battleLayer, Run.Battle, ExecuteBattle, TopBar);
            Battle.Director.Speed = BattleSpeed;
            Battle.Finished += () => StartCoroutine(AfterBattle());
            // 战斗界面自己建了一个提示框，抢回来：提示要画在顶栏和弹窗上面
            _tooltip.MakeCurrent();
        }

        void DestroyBattle()
        {
            if (Battle == null) return;
            Destroy(Battle.gameObject);
            Battle = null;
        }

        bool ExecuteBattle(BattleCommand cmd, out string error)
        {
            if (!Run.Execute(RunCommand.Fight(cmd), out error)) return false;
            CheckSave();
            return true;
        }

        IEnumerator AfterBattle()
        {
            _transition = true;
            yield return new WaitForSeconds(AfterBattleDelay / Mathf.Max(0.01f, BattleSpeed));
            _transition = false;
            // 奖励 / 结算叠在战场上，剩下的手牌和「胜利」横幅收起来，免得和面板叠在一起
            if (Battle != null)
            {
                Battle.Hand.gameObject.SetActive(false);
                Battle.ClearBanner();
            }
            Show();
        }

        // ------------------------------------------------------------------ 命令

        /// <summary>选当前界面的第 index 项（地图节点、奖励、商品……）。被拒绝时弹提示并返回 false。</summary>
        public bool Pick(int index)
        {
            if (_transition || Run.State.Phase == RunPhase.Battle) return false;
            if (!Run.Execute(RunCommand.Pick(index), out var error))
            {
                Toast(error);
                return false;
            }
            CheckSave();
            Show();
            return true;
        }

        public bool DropPotion(int slot)
        {
            if (!Run.Execute(RunCommand.DropPotion(slot), out var error))
            {
                Toast(error);
                return false;
            }
            Show();
            return true;
        }

        void CheckSave()
        {
            if (Run.SavePoints == _savedPoints && !Run.IsOver) return;
            _savedPoints = Run.SavePoints;
            _save?.Invoke(Run);
        }

        /// <summary>点当前面板里名为「Option 下标」的按钮或卡牌（和鼠标点一样，测试用）。按钮不可用时返回 false。</summary>
        public bool ClickOption(int index)
        {
            var target = _panel != null ? Find(_panel, $"Option {index}") : null;
            if (target == null) return false;
            var tile = target.GetComponent<CardTile>();
            if (tile != null)
            {
                tile.Click();
                return true;
            }
            var btn = target.GetComponent<Button>();
            if (btn == null || !btn.interactable) return false;
            btn.onClick.Invoke();
            return true;
        }

        /// <summary>当前面板里画出来的选项个数。</summary>
        public int CountOptions() => _panel == null ? 0 : _panel.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Option ", StringComparison.Ordinal));

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------------ 顶栏

        void PotionClicked(int slot)
        {
            if (Run.State.Phase == RunPhase.Battle && Battle != null)
            {
                Battle.PotionClicked(slot);
                return;
            }
            if (_transition || slot >= Run.State.Potions.Count || Run.State.Potions[slot] == null) return;
            OpenPotion(slot);
        }

        void OpenPotion(int slot)
        {
            var potion = PotionLibrary.Create(Run.State.Potions[slot]);
            var box = OpenModal("Potion", new Vector2(620, 330));
            RunUi.Title(box, potion.Name, new Vector2(0, 110), new Vector2(560, 60), 44);
            RunUi.Label(box, potion.Description, new Vector2(0, 30), new Vector2(540, 90), 26);
            RunUi.Label(box, "药水只能在战斗中使用。", new Vector2(0, -35), new Vector2(540, 36), 22, Palette.InkMid);
            RunUi.Button(box, "Drop", "丢掉", new Vector2(-120, -110), new Vector2(200, 64), () => DropPotion(slot));
            RunUi.Button(box, "Close", "留着", new Vector2(120, -110), new Vector2(200, 64), CloseModal);
        }

        /// <summary>牌组（顶栏「牌组」）：整副牌按获得顺序排开，可以卷动。</summary>
        public void OpenDeck()
        {
            if (_modal != null && _modal.name == "Deck")
            {
                CloseModal();
                return;
            }
            CloseModal();
            _modal = Ui.Rect("Deck", _modalLayer).Fill();
            RunUi.Dim(_modal, 0.88f).gameObject.AddComponent<ClickHandler>().OnClick = _ => CloseModal();
            var deck = Run.State.Deck;
            RunUi.Title(_modal, $"牌组（{deck.Count} 张）", new Vector2(0, 410), new Vector2(800, 70), 48, Palette.Pale);
            RunPanels.CardGrid(_modal, new Vector2(0, -50), new Vector2(1640, 800),
                deck.Select((id, i) => new RunPanels.GridCard { Id = id, Name = $"Deck {i}" }).ToList(), null);
            RunUi.Button(_modal, "Close", "关闭", new Vector2(820, 410), new Vector2(160, 60), CloseModal);
        }

        void OpenMenu()
        {
            CloseModal();
            _confirmAbandon = false;
            var box = OpenModal("Menu", new Vector2(520, 420));
            RunUi.Title(box, "菜单", new Vector2(0, 150), new Vector2(400, 60), 44);
            RunUi.Label(box, $"种子 {Run.State.Seed}", new Vector2(0, 95), new Vector2(400, 34), 22, Palette.InkMid);
            RunUi.Button(box, "Resume", "回到游戏", new Vector2(0, 25), new Vector2(360, 66), CloseModal);
            RunUi.Button(box, "Exit", "保存并回到标题", new Vector2(0, -55), new Vector2(360, 66), () => _exit?.Invoke(false));
            TextMeshProUGUI abandonLabel = null;
            var abandon = RunUi.Button(box, "Abandon", "放弃这一局", new Vector2(0, -135), new Vector2(360, 66), () =>
            {
                if (!_confirmAbandon)
                {
                    _confirmAbandon = true;
                    abandonLabel.text = "再点一次：删档放弃";
                    abandonLabel.color = Palette.Blood;
                    return;
                }
                _exit?.Invoke(true);
            });
            abandonLabel = abandon.GetComponentInChildren<TextMeshProUGUI>();
        }

        /// <summary>居中的卷轴弹窗，下面一层墨色遮罩（点遮罩关闭）。</summary>
        RectTransform OpenModal(string name, Vector2 size)
        {
            CloseModal();
            _modal = Ui.Rect(name, _modalLayer).Fill();
            RunUi.Dim(_modal, 0.6f).gameObject.AddComponent<ClickHandler>().OnClick = _ => CloseModal();
            return RunUi.Panel(_modal, "Box", Vector2.zero, size).rectTransform;
        }

        public void CloseModal()
        {
            if (_freshInk != null) Tween.StopAll(_freshInk);
            _freshInk = null;
            if (_modal != null) Destroy(_modal.gameObject);
            _modal = null;
        }

        // ------------------------------------------------------------------ 开局的信

        /// <summary>这一夜新多出来的那条附言（正在洇出来）。</summary>
        TextMeshProUGUI _freshInk;

        /// <summary>
        /// 开局的信（策划/12 §8.1）：夜色里一页信纸盖住地图。正文每一夜都一样；下面是导师后来添上的附言，深渊青的墨，
        /// 这一夜新多出来的那条慢慢洇出来。点「下船」收起（地图上走第一步也会收起：Show 会关掉弹窗）。
        /// </summary>
        public void ShowLetter(Profile profile, Postscript fresh)
        {
            CloseModal();
            _modal = Ui.Rect("Letter", _modalLayer).Fill();
            Ui.Box("Dark", _modal, Palette.Abyss, true).rectTransform.Fill();
            var sheet = RunUi.Panel(_modal, "Sheet", Vector2.zero, new Vector2(1040, 400)).rectTransform;
            const float Width = 860, Margin = 70, ButtonGap = 30, ButtonH = 76;

            // 从信纸顶端往下排，排完再按内容定信纸的高度：第 1 夜只有正文，附言越多信纸越长
            float y = Margin, end = Margin;
            TextMeshProUGUI Line(string name, string text, float size, Color color, TextAlignmentOptions align, float lineSpacing, float gapAfter, float width = Width)
            {
                var t = RunUi.Label(sheet, text, Vector2.zero, new Vector2(width, 40), size, color, align);
                t.font = Fonts.Serif;
                t.lineSpacing = lineSpacing;
                t.name = name;
                float h = t.GetPreferredValues(text, width, 0).y;
                t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -y - h / 2), new Vector2(width, h));
                end = y + h;
                y = end + gapAfter;
                return t;
            }

            if (profile.Nights >= 2) Line("Night", Story.NightLabel(profile.Nights), 24, Palette.InkMid, TextAlignmentOptions.Right, 0, 4);
            Line("Body", string.Join("\n", Story.Letter.Select(l => "　　" + l)), 34, Palette.Ink, TextAlignmentOptions.TopLeft, 28, 16);
            Line("Signature", Story.Signature, 30, Palette.Ink, TextAlignmentOptions.Right, 0, 36);
            // 附言是后来挤在信尾的小字，排得宽一点：最长的一条（B）正好一行
            foreach (var ps in Story.Shown(profile))
            {
                var t = Line("Postscript " + ps.Id, ps.Text, 25, Palette.Teal, TextAlignmentOptions.TopLeft, 0, 12, 920);
                if (ps != fresh) continue;
                t.alpha = 0;
                _freshInk = t;
                Tween.Alpha(t, 1, 2.4f, startDelay: 0.8f);
            }

            // 信纸和「下船」作为一个整体在屏幕上居中
            float height = end + Margin;
            float center = (ButtonGap + ButtonH) / 2;
            sheet.Place(new Vector2(0.5f, 0.5f), new Vector2(0, center), new Vector2(1040, height));
            RunUi.Button(_modal, "Disembark", "下船", new Vector2(0, center - height / 2 - ButtonGap - ButtonH / 2), new Vector2(260, ButtonH), CloseModal, true, null, 34);
        }

        /// <summary>点模态里名为 name 的按钮（测试用）。</summary>
        public bool ClickModal(string name)
        {
            var target = _modal != null ? Find(_modal, name) : null;
            var btn = target != null ? target.GetComponent<Button>() : null;
            if (btn == null || !btn.interactable) return false;
            btn.onClick.Invoke();
            return true;
        }

        public void ExitToTitle(bool abandon) => _exit?.Invoke(abandon);

        void OnDestroy() => Tween.StopAll(_toast);

        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            LastToast = message;
            Tween.StopAll(_toast);
            _toast.text = message;
            _toast.alpha = 1;
            _toast.transform.SetAsLastSibling();
            Tween.Alpha(_toast, 0, 0.6f, startDelay: 1.2f);
        }
    }
}
