using System.Collections.Generic;
using Deeptide.Core;
using Deeptide.Core.Cards;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 灰盒战斗界面：搭布局、把每条事件分发给对应的视图、播完一批后和内核对账。布局按 策划/10 §6.1（1920 × 1080，地平线 y = 440）。
    /// 层级从下到上：背景 → 背景癫狂层 → 战场（角色）→ 癫狂暗角 → 顶栏 / 手牌 / 飘字 / 面板 → 崩溃反相。
    /// 癫狂效果只作用在暗角以下的舞台层，卡牌和数字不受影响（10 §1.2 支柱 4，实现见 LunacyFx）。
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
        public TopBar TopBar => _topBar;
        public ChoicePanel Choice => _choice;
        /// <summary>正在等待选目标的药水栏，-1 = 没有。</summary>
        public int PendingPotion => _pendingPotion;

        const float EnemyGap = 30;

        System.Action _restart, _openLauncher;
        RectTransform _root, _overlay, _floats;
        TopBar _topBar;
        ChoicePanel _choice;
        Image _potionCatcher;
        TextMeshProUGUI _hint;
        int _pendingPotion = -1;
        bool _started;
        readonly HashSet<EnemyView> _placed = new HashSet<EnemyView>();
        HandView _hand;
        PlayerView _player;
        readonly List<EnemyView> _enemies = new List<EnemyView>();
        RectTransform _enemyRow;
        TextMeshProUGUI _log, _energyText, _drawText, _discardText, _exhaustText, _toast, _banner;
        LunacyFx _fx;
        Button _endTurn;
        Image _arrow;
        readonly Queue<string> _logLines = new Queue<string>();
        int _energy, _drawCount, _discardCount, _exhaustCount;

        // ------------------------------------------------------------------ 搭建

        public static BattleScreen Create(Camera camera, BattleSetup setup, System.Action restart, System.Action openLauncher = null)
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
            screen._openLauncher = openLauncher;
            screen.Director = go.AddComponent<BattleDirector>();
            screen.Director.Bind(screen);
            screen.Build();
            screen.Controller = new BattleController(setup, screen.Director);
            screen.Controller.Rejected += screen.Toast;
            screen._topBar.Bind(screen.Controller.Engine);
            screen.Controller.Begin();
            return screen;
        }

        void Build()
        {
            _root = (RectTransform)transform;
            var background = Ui.Box("Background", _root, Palette.Background);
            background.rectTransform.Fill();
            var backdrop = Ui.Rect("Backdrop", _root).Fill();

            // 战场：原点在底边中点。角色中心放在 y = 600，脚底（中心下方 160）正好踩在地平线 y = 440 上（10 §6.1）
            var field = Ui.Rect("Field", _root).Fill();
            field.pivot = new Vector2(0.5f, 0);
            _player = PlayerView.Create(field);
            var playerRt = (RectTransform)_player.transform;
            playerRt.Place(new Vector2(0.5f, 0), new Vector2(-460, 600), playerRt.sizeDelta);
            // 敌人区 x = -60..860（10 §6.1、§5.1）
            _enemyRow = Ui.Rect("Enemies", field).Place(new Vector2(0.5f, 0), new Vector2(400, 600), new Vector2(920, 470));

            // 底部：能量、牌堆、结束回合
            var orb = Ui.Box("Energy", field, Palette.Energy);
            orb.sprite = Shapes.Circle;
            orb.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(-760, 300), new Vector2(110, 110));
            var orbRing = Ui.Box("Ring", orb.transform, Palette.GoldLight);
            orbRing.sprite = Shapes.Ring(10);
            orbRing.rectTransform.Fill(5);
            _energyText = Ui.Text("Value", orb.transform, "", 38, Palette.Paper);
            _energyText.font = Fonts.Serif;
            _energyText.fontStyle = FontStyles.Bold;
            _energyText.rectTransform.Fill();
            _drawText = PileLabel(field, "Draw", new Vector2(-860, 80));
            _discardText = PileLabel(field, "Discard", new Vector2(860, 80));
            _exhaustText = PileLabel(field, "Exhaust", new Vector2(860, 210));
            _endTurn = Ui.TextButton("EndTurn", field, "结束回合", Palette.PanelLight, () => Controller.EndTurn());
            var endTurnBg = (Image)_endTurn.targetGraphic;
            endTurnBg.sprite = Shapes.RoundRect(14);
            endTurnBg.type = Image.Type.Sliced;
            Ui.Border(endTurnBg, Palette.Ink, 3);
            var endTurnLabel = _endTurn.GetComponentInChildren<TextMeshProUGUI>();
            endTurnLabel.font = Fonts.Serif;
            endTurnLabel.fontStyle = FontStyles.Bold;
            _endTurn.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(790, 300), new Vector2(220, 80));

            // 癫狂表现：暗角盖在战场上，背景癫狂层垫在战场下（10 §7）
            _fx = LunacyFx.Create(_root);
            _fx.BuildBackdrop(backdrop);
            _fx.SetBackground(background);
            _fx.AddShakeTarget(field);

            // 顶部：顶栏（生命、药水、遗物、回合、选择对局 / 重开），日志框挪到顶栏下面
            // 日志是调试用的，默认收起，点顶栏「日志」展开（阶段 2 验收要求不看日志也能打完一场）
            var logBox = Ui.Box("Log", _root, Palette.Panel);
            logBox.rectTransform.Place(new Vector2(0, 1), new Vector2(340, -TopBar.Height - 106), new Vector2(640, 180));
            _log = Ui.Text("Text", logBox.transform, "", 19, Palette.Text, TextAlignmentOptions.BottomLeft);
            _log.rectTransform.Fill(12);
            logBox.gameObject.SetActive(false);
            _topBar = TopBar.Create(_root, PotionClicked, () => _openLauncher?.Invoke(), () => _restart?.Invoke(),
                () => logBox.gameObject.SetActive(!logBox.gameObject.activeSelf));

            // 手牌层、箭头 / 飘字 / 提示层
            _hand = Ui.Rect("Hand", _root).Fill().gameObject.AddComponent<HandView>();
            ((RectTransform)_hand.transform).pivot = new Vector2(0.5f, 0);
            _hand.Init(this);
            _fx.AddShakeTarget((RectTransform)_hand.transform);

            _overlay = Ui.Rect("Overlay", _root).Fill();
            _overlay.pivot = new Vector2(0.5f, 0);
            _arrow = Ui.Box("Arrow", _overlay, Palette.Ink);
            _arrow.rectTransform.pivot = new Vector2(0, 0.5f);
            _arrow.gameObject.SetActive(false);
            _floats = Ui.Rect("Floats", _root).Fill();

            _toast = Ui.Text("Toast", _root, "", 32, Palette.Blood);
            _toast.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(800, 50));
            _banner = Ui.Text("Banner", _root, "", 72, Palette.Text);
            _banner.font = Fonts.Serif;
            _banner.fontStyle = FontStyles.Bold;
            _banner.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(1000, 110));

            // 药水选目标：全屏透明遮罩接住点击，点到敌人就用药水，点别处取消
            _potionCatcher = Ui.Box("PotionTarget", _root, new Color(0, 0, 0, 0), true);
            _potionCatcher.rectTransform.Fill();
            _potionCatcher.gameObject.AddComponent<ClickHandler>().OnClick = e =>
            {
                var enemy = EnemyUnder(e);
                if (enemy != null) EnemyClicked(enemy.Index);
                else CancelPotion();
            };
            _potionCatcher.gameObject.SetActive(false);
            _hint = Ui.Text("Hint", _root, "", 30, Palette.Accent);
            _hint.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1200, 50));

            _choice = ChoicePanel.Create(_root);
            Tooltip.Create(_root);
            _fx.BuildOverlay(_root);
        }

        static TextMeshProUGUI PileLabel(Transform parent, string name, Vector2 pos)
        {
            var box = Ui.Box(name, parent, Palette.Panel);
            box.sprite = Shapes.RoundRect(12);
            box.type = Image.Type.Sliced;
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
                    _topBar.SetHp(e.Value, e.Value2);
                    _player.SetSanity(e.Amount, e.Amount, engine.Config.LunacyThreshold);
                    return 0;

                case BattleEventType.EnemySpawned:
                {
                    var view = EnemyView.Create(_enemyRow, e.Entity, e.Actor, e.Id);
                    // 尺寸由 EnemyView 自己按档位定，这里只管水平排位（中心对齐，y 在敌人行中线）
                    var rt = (RectTransform)view.transform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    _enemies.Add(view);
                    view.SetHp(e.Value, e.Value2);
                    LayoutEnemies(_started ? 0.25f : 0);
                    return _started ? 0.3f : 0.05f;
                }

                case BattleEventType.HpChanged:
                {
                    var view = ViewOf(e.Entity);
                    int delta = view.SetHp(e.Value, e.Value2);
                    if (e.Entity == BattleEvent.PlayerEntity) _topBar.SetHp(e.Value, e.Value2);
                    if (delta < 0)
                    {
                        // 10 §2.3：伤害血色 + 墨描边，单次 ≥ 20 时放大
                        Float(view, delta.ToString(), Palette.Blood, delta <= -20 ? 64 : 48, true);
                        view.Shake();
                        return 0.25f;
                    }
                    if (delta > 0) Float(view, "+" + delta, Palette.Moss);
                    return delta > 0 ? 0.15f : 0;
                }

                case BattleEventType.BlockChanged:
                {
                    var view = ViewOf(e.Entity);
                    int delta = view.SetBlock(e.Value);
                    if (delta <= 0) return 0;
                    Float(view, $"+{delta} 格挡", Palette.Ink);
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
                    // 10 §2.3：失去理智深渊青，恢复理智清明金
                    if (delta != 0) Float(_player, delta > 0 ? $"+{delta} 理智" : $"{delta} 理智", delta > 0 ? Palette.Gold : Palette.Teal);
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

                case BattleEventType.FearChanged:
                    if (e.Entity >= 0 && e.Entity < _enemies.Count && e.Value > _enemies[e.Entity].Fear)
                        Float(_enemies[e.Entity], $"+{e.Value - _enemies[e.Entity].Fear} 恐惧", Palette.Dread, 36);
                    RefreshStatuses();
                    return 0;

                case BattleEventType.Collapse:
                    if (_fx != null) _fx.Collapse();
                    return 0.6f;

                case BattleEventType.StatusChanged:
                case BattleEventType.StunChanged:
                case BattleEventType.PowerChanged:
                    RefreshStatuses();
                    return 0;

                case BattleEventType.TurnStarted:
                    _started = true;
                    _topBar.SetTurn(e.Value);
                    ShowBanner($"第 {e.Value} 回合", 0.7f);
                    return 0.45f;

                case BattleEventType.EnemyMove:
                    if (e.Entity >= 0 && e.Entity < _enemies.Count) _enemies[e.Entity].Lunge();
                    return 0.3f;

                case BattleEventType.EnemyDied:
                    if (e.Entity >= 0 && e.Entity < _enemies.Count)
                    {
                        var dead = _enemies[e.Entity];
                        dead.Die();
                        // 淡出后收起来，剩下的敌人重新排开（Boss 会反复召唤触须，下标一直往后加）
                        Tween.Delay(dead, 0.6f, () => dead.gameObject.SetActive(false), warnIfTargetDestroyed: false);
                        LayoutEnemies(0.35f);
                    }
                    return 0.35f;

                case BattleEventType.PotionChanged:
                    _topBar.SyncPotions();
                    return 0;

                case BattleEventType.RelicTriggered:
                    _topBar.FlashRelic(e.Id);
                    return 0.15f;

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
            _topBar.SetHp(p.Hp, p.MaxHp);
            _topBar.SetTurn(engine.State.Turn);
            _topBar.SyncPotions();
            _player.SetBlock(p.Block);
            _player.SetSanity(p.Sanity, p.MaxSanity, p.LunacyThreshold);
            _player.RefreshGauge(engine);
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
            _player.RefreshGauge(Controller.Engine);
            _endTurn.interactable = Controller.AcceptsInput;
            if (Controller.AwaitingChoice) _choice.Show(Controller.Engine.State.Choice, Controller.Engine, option => Controller.Choose(option));
            else if (_choice.IsOpen) _choice.Hide();
        }

        // ------------------------------------------------------------------ 敌人排位

        /// <summary>活着的敌人按下标顺序在敌人行里居中排开，宽度用各自的 sizeDelta.x（尺寸档由 EnemyView 决定）。只改 x。</summary>
        void LayoutEnemies(float duration)
        {
            var live = new List<EnemyView>();
            float total = 0;
            foreach (var v in _enemies)
            {
                if (v.IsDead) continue;
                live.Add(v);
                total += ((RectTransform)v.transform).sizeDelta.x;
            }
            if (live.Count == 0) return;
            float gap = live.Count > 1 ? Mathf.Min(EnemyGap, (_enemyRow.rect.width - total) / (live.Count - 1)) : 0;
            float x = -(total + gap * (live.Count - 1)) / 2;
            foreach (var v in live)
            {
                var rt = (RectTransform)v.transform;
                float w = rt.sizeDelta.x;
                float target = x + w / 2;
                x += w + gap;
                bool first = _placed.Add(v);
                if (duration <= 0 || first) rt.anchoredPosition = new Vector2(target, rt.anchoredPosition.y);
                else Tween.UIAnchoredPositionX(rt, target, duration, Ease.OutCubic);
            }
        }

        // ------------------------------------------------------------------ 药水

        /// <summary>点顶栏的药水：不需要目标的直接用；需要目标的进入选目标状态（只剩一个敌人时直接用在它身上）。</summary>
        public void PotionClicked(int slot)
        {
            if (_pendingPotion == slot)
            {
                CancelPotion();
                return;
            }
            CancelPotion();
            if (!Controller.AcceptsInput) return;
            var engine = Controller.Engine;
            if (!engine.CanUsePotion(slot, out var error))
            {
                Toast(error);
                return;
            }
            var potion = engine.Player.Potions[slot];
            if (potion.Def.Target != CardTarget.Enemy)
            {
                Controller.UsePotion(slot);
                return;
            }
            int count = 0, only = -1;
            foreach (var en in engine.State.AliveEnemies)
            {
                count++;
                only = en.Index;
            }
            if (count == 1)
            {
                Controller.UsePotion(slot, only);
                return;
            }
            _pendingPotion = slot;
            _topBar.HighlightPotion(slot);
            _hint.text = $"点击一个敌人使用{potion.Name}（点别处取消）";
            _potionCatcher.gameObject.SetActive(true);
            _potionCatcher.transform.SetAsLastSibling();
        }

        /// <summary>选目标状态下点了敌人。</summary>
        public void EnemyClicked(int index)
        {
            if (_pendingPotion < 0) return;
            int slot = _pendingPotion;
            CancelPotion();
            Controller.UsePotion(slot, index);
        }

        public void CancelPotion()
        {
            _pendingPotion = -1;
            _topBar.HighlightPotion(-1);
            _hint.text = "";
            _potionCatcher.gameObject.SetActive(false);
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
            _arrow.color = target != null ? Palette.Blood : Palette.Ink;
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

        /// <summary>飘字（10 §2.3）：数字用宋体，伤害加 2px 墨描边。</summary>
        void Float(CombatantView view, string text, Color color, float size = 40, bool outline = false)
        {
            var t = Ui.Text("Float", _floats, text, size, color);
            t.font = Fonts.Serif;
            t.fontStyle = FontStyles.Bold;
            if (outline)
            {
                t.outlineWidth = 0.18f;
                t.outlineColor = Palette.Ink;
            }
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
