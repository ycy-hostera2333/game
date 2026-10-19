using System.Collections.Generic;
using Deeptide.Core;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 灰盒战斗界面：搭布局、把每条事件分发给对应的视图、播完一批后和内核对账。
    /// 画布用 Screen Space - Camera，后面的癫狂后处理（URP Volume）才能作用到卡牌上（策划/08 §9）。
    /// </summary>
    public sealed class BattleScreen : MonoBehaviour
    {
        public BattleController Controller { get; private set; }
        public BattleDirector Director { get; private set; }
        public Camera Camera { get; private set; }
        public HandView Hand => _hand;
        public PlayerView Player => _player;
        public IReadOnlyList<EnemyView> Enemies => _enemies;
        public int DrawCount => _drawCount;
        public int DiscardCount => _discardCount;
        public int Energy => _energy;

        System.Action _restart;
        RectTransform _root, _overlay, _floats;
        HandView _hand;
        PlayerView _player;
        readonly List<EnemyView> _enemies = new List<EnemyView>();
        RectTransform _enemyRow;
        TextMeshProUGUI _turn, _log, _energyText, _drawText, _discardText, _exhaustText, _toast, _banner;
        Button _endTurn;
        RectTransform _choicePanel;
        Image _arrow;
        readonly Queue<string> _logLines = new Queue<string>();
        int _energy, _drawCount, _discardCount, _exhaustCount;

        // ------------------------------------------------------------------ 搭建

        public static BattleScreen Create(Camera camera, BattleSetup setup, System.Action restart)
        {
            var go = new GameObject("BattleScreen", typeof(RectTransform)) { layer = Ui.Layer };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var screen = go.AddComponent<BattleScreen>();
            screen.Camera = camera;
            screen._restart = restart;
            screen.Director = go.AddComponent<BattleDirector>();
            screen.Director.Bind(screen);
            screen.Build();
            screen.Controller = new BattleController(setup, screen.Director);
            screen.Controller.Rejected += screen.Toast;
            screen.Controller.Begin();
            return screen;
        }

        void Build()
        {
            _root = (RectTransform)transform;
            Ui.Box("Background", _root, Palette.Background).rectTransform.Fill();

            // 顶部：回合、日志、重开
            _turn = Ui.Text("Turn", _root, "", 34, Palette.Text);
            _turn.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(400, 50));
            var logBox = Ui.Box("Log", _root, new Color(0, 0, 0, 0.35f));
            logBox.rectTransform.Place(new Vector2(0, 1), new Vector2(340, -110), new Vector2(640, 180));
            _log = Ui.Text("Text", logBox.transform, "", 19, Palette.TextDim, TextAlignmentOptions.BottomLeft);
            _log.rectTransform.Fill(12);
            Ui.TextButton("Restart", _root, "重开", Palette.PanelLight, () => _restart?.Invoke())
                .GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(-110, -50), new Vector2(160, 60));

            // 战场：原点在底边中点
            var field = Ui.Rect("Field", _root).Fill();
            field.pivot = new Vector2(0.5f, 0);
            _player = PlayerView.Create(field);
            ((RectTransform)_player.transform).Place(new Vector2(0.5f, 0), new Vector2(-560, 600), new Vector2(300, 470));
            _enemyRow = Ui.Rect("Enemies", field).Place(new Vector2(0.5f, 0), new Vector2(480, 600), new Vector2(900, 470));

            // 底部：能量、牌堆、结束回合
            var orb = Ui.Box("Energy", field, Palette.Energy);
            orb.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(-760, 300), new Vector2(110, 110));
            _energyText = Ui.Text("Value", orb.transform, "", 38, Palette.Background);
            _energyText.fontStyle = FontStyles.Bold;
            _energyText.rectTransform.Fill();
            _drawText = PileLabel(field, "Draw", new Vector2(-860, 80));
            _discardText = PileLabel(field, "Discard", new Vector2(860, 80));
            _exhaustText = PileLabel(field, "Exhaust", new Vector2(860, 210));
            _endTurn = Ui.TextButton("EndTurn", field, "结束回合", Palette.AccentDark, () => Controller.EndTurn());
            _endTurn.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(740, 330), new Vector2(220, 80));

            // 手牌层、箭头 / 飘字 / 提示层
            _hand = Ui.Rect("Hand", _root).Fill().gameObject.AddComponent<HandView>();
            ((RectTransform)_hand.transform).pivot = new Vector2(0.5f, 0);
            _hand.Init(this);

            _overlay = Ui.Rect("Overlay", _root).Fill();
            _overlay.pivot = new Vector2(0.5f, 0);
            _arrow = Ui.Box("Arrow", _overlay, Palette.Accent);
            _arrow.rectTransform.pivot = new Vector2(0, 0.5f);
            _arrow.gameObject.SetActive(false);
            _floats = Ui.Rect("Floats", _root).Fill();

            _toast = Ui.Text("Toast", _root, "", 32, Palette.Bad);
            _toast.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(800, 50));
            _banner = Ui.Text("Banner", _root, "", 72, Palette.Text);
            _banner.fontStyle = FontStyles.Bold;
            _banner.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(1000, 110));

            _choicePanel = Ui.Box("Choice", _root, new Color(0, 0, 0, 0.85f), true).rectTransform.Fill();
            _choicePanel.gameObject.SetActive(false);
        }

        static TextMeshProUGUI PileLabel(Transform parent, string name, Vector2 pos)
        {
            var box = Ui.Box(name, parent, Palette.Panel);
            box.rectTransform.Place(new Vector2(0.5f, 0), pos, new Vector2(120, 110));
            var t = Ui.Text("Value", box.transform, "", 22, Palette.Text);
            t.rectTransform.Fill(6);
            return t;
        }

        // ------------------------------------------------------------------ 事件分发

        /// <summary>处理一条事件，返回在下一条之前要等待的秒数。</summary>
        public float Apply(BattleEvent e)
        {
            var engine = Controller.Engine;
            if (!e.IsStateChange && !string.IsNullOrEmpty(e.Message)) Log(e.Message);

            switch (e.Type)
            {
                case BattleEventType.PlayerInit:
                    _player.SetHp(e.Value, e.Value2);
                    _player.SetSanity(e.Amount, e.Amount, engine.Config.LunacyThreshold);
                    return 0;

                case BattleEventType.EnemySpawned:
                {
                    var view = EnemyView.Create(_enemyRow, e.Entity, e.Actor);
                    ((RectTransform)view.transform).Place(new Vector2(0, 0.5f), new Vector2(160 + e.Entity * 320, 0), new Vector2(300, 470));
                    _enemies.Add(view);
                    view.SetHp(e.Value, e.Value2);
                    return 0.05f;
                }

                case BattleEventType.HpChanged:
                {
                    var view = ViewOf(e.Entity);
                    int delta = view.SetHp(e.Value, e.Value2);
                    if (delta < 0)
                    {
                        Float(view, delta.ToString(), Palette.Bad);
                        view.Shake();
                        return 0.25f;
                    }
                    if (delta > 0) Float(view, "+" + delta, Palette.Heal);
                    return delta > 0 ? 0.15f : 0;
                }

                case BattleEventType.BlockChanged:
                {
                    var view = ViewOf(e.Entity);
                    int delta = view.SetBlock(e.Value);
                    if (delta <= 0) return 0;
                    Float(view, $"+{delta} 格挡", Palette.Block);
                    return 0.12f;
                }

                case BattleEventType.EnergyChanged:
                    _energy = e.Value;
                    _energyText.text = $"{_energy}/{engine.Config.EnergyPerTurn}";
                    RefreshCards();
                    return 0;

                case BattleEventType.SanityChanged:
                {
                    int delta = _player.SetSanity(e.Value, e.Value2, engine.Config.LunacyThreshold);
                    Float(_player, delta > 0 ? $"+{delta} 理智" : $"{delta} 理智", Palette.Accent);
                    RefreshCards();
                    return 0.2f;
                }

                case BattleEventType.CardMoved:
                    CountPiles(e.From, -1);
                    CountPiles(e.To, +1);
                    UpdatePiles();
                    return _hand.OnCardMoved(e);

                case BattleEventType.PileShuffled:
                    _drawCount = e.Value;
                    _discardCount = 0;
                    UpdatePiles();
                    return 0.25f;

                case BattleEventType.IntentChanged:
                case BattleEventType.IntentRevealChanged:
                    foreach (var en in _enemies) en.SetIntent(engine);
                    return 0;

                case BattleEventType.StatusChanged:
                case BattleEventType.FearChanged:
                case BattleEventType.StunChanged:
                case BattleEventType.PowerChanged:
                    RefreshStatuses();
                    return 0;

                case BattleEventType.TurnStarted:
                    _turn.text = $"第 {e.Value} 回合";
                    ShowBanner($"第 {e.Value} 回合", 0.7f);
                    return 0.45f;

                case BattleEventType.EnemyMove:
                    if (e.Entity >= 0 && e.Entity < _enemies.Count) _enemies[e.Entity].Lunge();
                    return 0.3f;

                case BattleEventType.EnemyDied:
                    if (e.Entity >= 0 && e.Entity < _enemies.Count) _enemies[e.Entity].Die();
                    return 0.35f;

                case BattleEventType.CardPlayed:
                    return 0.05f;

                case BattleEventType.Victory:
                    ShowBanner("胜利", 0);
                    return 0.5f;

                case BattleEventType.Defeat:
                    ShowBanner("失败", 0);
                    return 0.5f;

                default:
                    return 0;
            }
        }

        CombatantView ViewOf(int entity) => entity == BattleEvent.PlayerEntity ? _player : (CombatantView)_enemies[entity];

        void CountPiles(PileKind pile, int d)
        {
            if (pile == PileKind.Draw) _drawCount += d;
            else if (pile == PileKind.Discard) _discardCount += d;
            else if (pile == PileKind.Exhaust) _exhaustCount += d;
        }

        void UpdatePiles()
        {
            _drawText.text = $"抽牌堆\n{_drawCount}";
            _discardText.text = $"弃牌堆\n{_discardCount}";
            _exhaustText.text = $"消耗\n{_exhaustCount}";
            _exhaustText.transform.parent.gameObject.SetActive(_exhaustCount > 0);
        }

        // ------------------------------------------------------------------ 对账

        /// <summary>一批事件播完后，直接按内核状态把所有显示对齐一遍（不播动画）。</summary>
        public void SyncAll()
        {
            var engine = Controller.Engine;
            var p = engine.Player;
            _player.SetHp(p.Hp, p.MaxHp);
            _player.SetBlock(p.Block);
            _player.SetSanity(p.Sanity, p.MaxSanity, p.LunacyThreshold);
            _energy = p.Energy;
            _energyText.text = $"{_energy}/{engine.Config.EnergyPerTurn}";
            _drawCount = p.DrawPile.Count;
            _discardCount = p.DiscardPile.Count;
            _exhaustCount = p.ExhaustPile.Count;
            UpdatePiles();
            foreach (var view in _enemies)
            {
                var en = engine.State.Enemies[view.Index];
                view.SetHp(en.Hp, en.MaxHp);
                view.SetBlock(en.Block);
                view.SetIntent(engine);
            }
            RefreshStatuses();
            _hand.Sync(engine);
        }

        public void OnDirectorIdle()
        {
            RefreshCards();
            _endTurn.interactable = Controller.AcceptsInput;
            if (Controller.AwaitingChoice) ShowChoice(Controller.Engine.State.Choice);
        }

        void RefreshStatuses()
        {
            var engine = Controller.Engine;
            _player.SetStatuses(engine.Player);
            foreach (var view in _enemies) view.SetStatuses(engine.State.Enemies[view.Index]);
        }

        void RefreshCards()
        {
            foreach (var card in _hand.Cards) RefreshCard(card);
        }

        public void RefreshCard(CardView card) => card.Refresh(Controller.Engine, _energy, Controller.AcceptsInput);

        // ------------------------------------------------------------------ 选择

        void ShowChoice(PendingChoice choice)
        {
            foreach (Transform child in _choicePanel) Destroy(child.gameObject);
            _choicePanel.gameObject.SetActive(true);
            Ui.Text("Prompt", _choicePanel, choice.Prompt, 40, Palette.Text)
                .rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1200, 60));
            int n = choice.Options.Count;
            for (int i = 0; i < n; i++)
            {
                int option = i;
                Ui.TextButton($"Option {i}", _choicePanel, choice.Options[i].Label, Palette.PanelLight, () =>
                    {
                        _choicePanel.gameObject.SetActive(false);
                        Controller.Choose(option);
                    })
                    .GetComponent<RectTransform>()
                    .Place(new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * 280, 40), new Vector2(260, 90));
            }
        }

        // ------------------------------------------------------------------ 拖拽瞄准

        /// <summary>画一条从 from 指向 to 的瞄准线（坐标原点在底边中点）。target 不为空时变成红色。</summary>
        public void Aim(Vector2 from, Vector2 to, EnemyView target)
        {
            var d = to - from;
            var rt = _arrow.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(d.magnitude, 10);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            _arrow.color = target != null ? Palette.Bad : Palette.Accent;
            _arrow.gameObject.SetActive(true);
        }

        public void StopAim() => _arrow.gameObject.SetActive(false);

        public EnemyView EnemyUnder(PointerEventData e)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(e, hits);
            foreach (var hit in hits)
            {
                var view = hit.gameObject.GetComponentInParent<EnemyView>();
                if (view != null && !view.IsDead) return view;
            }
            return null;
        }

        // ------------------------------------------------------------------ 提示

        void Log(string line)
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > 6) _logLines.Dequeue();
            _log.text = string.Join("\n", _logLines);
        }

        public void Toast(string message)
        {
            Tween.StopAll(_toast);
            _toast.text = message;
            _toast.alpha = 1;
            Tween.Alpha(_toast, 0, 0.6f, startDelay: 0.8f);
        }

        void ShowBanner(string text, float hold)
        {
            Tween.StopAll(_banner);
            _banner.text = text;
            _banner.alpha = 1;
            if (hold > 0) Tween.Alpha(_banner, 0, 0.3f, startDelay: hold);
        }

        void Float(CombatantView view, string text, Color color)
        {
            var t = Ui.Text("Float", _floats, text, 40, color);
            t.fontStyle = FontStyles.Bold;
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(300, 60);
            rt.position = view.WorldTop;
            rt.anchoredPosition += new Vector2(UnityEngine.Random.Range(-30f, 30f), 10);
            Tween.UIAnchoredPosition(rt, rt.anchoredPosition + new Vector2(0, 90), 0.8f, Ease.OutCubic);
            // 纯视觉收尾：场景切走时飘字可能已经被销毁，不需要报警
            Tween.Alpha(t, 0, 0.4f, startDelay: 0.45f).OnComplete(() => Destroy(t.gameObject), warnIfTargetDestroyed: false);
        }
    }
}
