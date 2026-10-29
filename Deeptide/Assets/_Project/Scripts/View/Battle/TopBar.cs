using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Potions;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 顶栏（策划/10 §6.1，屏幕最上面 64px）：生命、药水栏（点击使用）、遗物（悬停看说明，触发时闪一下）、回合、选择对局 / 重开。
    /// 数值由 BattleScreen 按事件推进；药水和遗物的内容直接读内核（只在事件回放时刷新）。
    /// 跑局里只有一条顶栏（<see cref="CreateForRun"/>）：多出金币、层数、牌组、菜单；战斗中绑 BattleEngine，其余界面绑 RunState。
    /// </summary>
    public sealed class TopBar : MonoBehaviour
    {
        public const float Height = 64;
        const float SlotSize = 50, RelicSize = 46, SlotStep = SlotSize + 10;

        TextMeshProUGUI _hp, _turn, _gold, _floor;
        RectTransform _relicRow;
        readonly List<Image> _potionSlots = new List<Image>();
        readonly List<TextMeshProUGUI> _potionLabels = new List<TextMeshProUGUI>();
        readonly Dictionary<string, Image> _relics = new Dictionary<string, Image>();
        Action<int> _onPotion;
        BattleEngine _engine;
        Func<int, Potion> _potionAt;
        /// <summary>第一格药水的 x；遗物排在药水后面，右边界之前（跑局模式右边按钮多，界限更靠左）。</summary>
        float _potionsLeft = -660, _relicsRight = 250;

        /// <summary>每格药水栏显示的药水 ID（空格为 null），测试用。</summary>
        public readonly List<string> PotionIds = new List<string>();
        /// <summary>遗物栏里的遗物 ID（按显示顺序），测试用。</summary>
        public IReadOnlyCollection<string> RelicIds => _relics.Keys;
        public string GoldText => _gold != null ? _gold.text : "";
        public string FloorText => _floor != null ? _floor.text : "";

        public static TopBar Create(RectTransform root, Action<int> onPotion, Action openLauncher, Action restart, Action toggleLog)
        {
            var bar = CreateBar(root, onPotion);
            var rt = (RectTransform)bar.transform;
            bar._turn = Ui.Text("Turn", rt, "", 28, Palette.Text);
            bar._turn.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(335, 0), new Vector2(150, 50));
            bar.FxButton(new Vector2(470, 0), 130);
            Button(rt, "日志", new Vector2(590, 0), new Vector2(90, 50), toggleLog);
            Button(rt, "选择对局", new Vector2(720, 0), new Vector2(160, 50), openLauncher);
            Button(rt, "重开", new Vector2(870, 0), new Vector2(110, 50), restart);
            return bar;
        }

        /// <summary>跑局的顶栏：♥ 生命 ◎ 金币 [药水] 遗物……  第 N 层  第 N 回合 [特效][日志][牌组][菜单]（10 §6.1）。</summary>
        public static TopBar CreateForRun(RectTransform root, Action<int> onPotion, Action toggleLog, Action openDeck, Action openMenu)
        {
            var bar = CreateBar(root, onPotion);
            var rt = (RectTransform)bar.transform;
            bar._potionsLeft = -545;
            bar._relicsRight = 170;
            bar._gold = Ui.Text("Gold", rt, "", 28, Palette.Gold, TextAlignmentOptions.Left);
            bar._gold.fontStyle = FontStyles.Bold;
            bar._gold.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-622, 0), new Vector2(110, 50));
            TooltipTrigger.Add(bar._gold, () => "金币：商店、事件里花");
            bar._floor = Ui.Text("Floor", rt, "", 24, Palette.Text);
            bar._floor.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(240, 0), new Vector2(110, 50));
            bar._turn = Ui.Text("Turn", rt, "", 24, Palette.Text);
            bar._turn.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(365, 0), new Vector2(140, 50));
            bar.FxButton(new Vector2(505, 0), 130);
            Button(rt, "日志", new Vector2(620, 0), new Vector2(80, 50), toggleLog);
            Button(rt, "牌组", new Vector2(730, 0), new Vector2(120, 50), openDeck);
            Button(rt, "菜单", new Vector2(865, 0), new Vector2(120, 50), openMenu);
            return bar;
        }

        /// <summary>两种顶栏共用的部分：底板、生命、饰带、遗物行。</summary>
        static TopBar CreateBar(RectTransform root, Action<int> onPotion)
        {
            var bg = Ui.Box("TopBar", root, Palette.Panel, true);
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0, Height);

            var bar = bg.gameObject.AddComponent<TopBar>();
            bar._onPotion = onPotion;
            bar._hp = Ui.Text("Hp", rt, "", 28, Palette.Hp, TextAlignmentOptions.Left);
            bar._hp.fontStyle = FontStyles.Bold;
            bar._hp.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-820, 0), new Vector2(200, 50));
            // UI 套件：生命永远配心形（10 §2.2），顶栏下沿一道铜版饰带
            var heart = Ui.Box("Heart", rt, Palette.Hp);
            if (ArtSprites.Apply(heart, "icon_heart", Palette.Hp))
            {
                heart.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-912, 0), new Vector2(32, 32));
                bar._hp.rectTransform.anchoredPosition = new Vector2(-786, 0);
            }
            else Destroy(heart.gameObject);
            var rule = Ui.Box("Rule", rt, Palette.InkMid);
            if (ArtSprites.Apply(rule, "ui_rule", Palette.InkMid, default, 2))
            {
                rule.type = Image.Type.Tiled;
                rule.raycastTarget = false;
                rule.rectTransform.anchorMin = new Vector2(0, 0);
                rule.rectTransform.anchorMax = new Vector2(1, 0);
                rule.rectTransform.pivot = new Vector2(0.5f, 1);
                rule.rectTransform.anchoredPosition = new Vector2(0, 6);
                rule.rectTransform.sizeDelta = new Vector2(0, 12);
            }
            else Destroy(rule.gameObject);

            bar._relicRow = Ui.Rect("Relics", rt).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, Height));
            return bar;
        }

        /// <summary>「降低癫狂特效」（策划/10 §7.4）：设置里也有，顶栏留一个战斗中随手切的开关，选择记在 PlayerPrefs 里。</summary>
        void FxButton(Vector2 pos, float width)
        {
            TextMeshProUGUI fxLabel = null;
            fxLabel = Button(transform, FxLabel(), pos, new Vector2(width, 50), () =>
            {
                LunacyFx.Reduced = !LunacyFx.Reduced;
                fxLabel.text = FxLabel();
            });
            TooltipTrigger.Add(fxLabel.transform.parent.GetComponent<Image>(), () => "降低癫狂特效：暗角、色差强度 × 0.3，关掉波纹和崩溃时的反相闪烁");
        }

        static TextMeshProUGUI Button(Transform parent, string label, Vector2 pos, Vector2 size, Action click)
        {
            var btn = Ui.TextButton(label, parent, label, Palette.PanelLight, () => click?.Invoke());
            btn.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), pos, size);
            var text = btn.GetComponentInChildren<TextMeshProUGUI>();
            text.fontSize = 24;
            return text;
        }

        static string FxLabel() => LunacyFx.Reduced ? "特效：减弱" : "特效：标准";

        /// <summary>战斗开场时按内核建药水栏和遗物栏。</summary>
        public void Bind(BattleEngine engine)
        {
            _engine = engine;
            Build(engine.Player.Potions.Count, i => _engine.Player.Potions[i], engine.Player.Relics);
        }

        /// <summary>跑局的地图、商店等界面：药水和遗物读 RunState（不在战斗里，点药水是丢掉）。状态变了就重新调用一次。</summary>
        public void Bind(RunState state)
        {
            _engine = null;
            var potions = state.Potions.Select(id => id == null ? null : PotionLibrary.Create(id)).ToList();
            Build(potions.Count, i => potions[i], state.Relics.Select(RelicLibrary.Create).ToList());
            SetHp(state.Hp, state.MaxHp);
            SetGold(state.Gold);
            SetFloor(state.Floor);
            _turn.text = "";
        }

        /// <summary>按当前的药水栏和遗物重建两行（遗物、药水栏格数在跑局里会变）。</summary>
        void Build(int slots, Func<int, Potion> potionAt, IReadOnlyList<Relic> relics)
        {
            _potionAt = potionAt;
            foreach (var img in _potionSlots) Destroy(img.gameObject);
            foreach (var img in _relics.Values) Destroy(img.gameObject);
            _potionSlots.Clear();
            _potionLabels.Clear();
            _relics.Clear();
            PotionIds.Clear();

            var rt = (RectTransform)transform;
            for (int i = 0; i < slots; i++)
            {
                int slot = i;
                var img = Ui.Box($"Potion {i}", rt, Palette.PanelLight, true);
                img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(_potionsLeft + i * SlotStep, 0), new Vector2(SlotSize, SlotSize));
                Ui.Border(img, Palette.TextDim, 1.5f);
                var label = Ui.Text("Label", img.transform, "", 24, Palette.Text);
                label.rectTransform.Fill();
                img.gameObject.AddComponent<ClickHandler>().OnClick = _ => _onPotion?.Invoke(slot);
                TooltipTrigger.Add(img, () => PotionTooltip(slot));
                _potionSlots.Add(img);
                _potionLabels.Add(label);
                PotionIds.Add(null);
            }
            SyncPotions();

            // 遗物从药水栏右边排到右边界，放不下时挤在一起（间距缩小）
            float left = _potionsLeft + slots * SlotStep + 60;
            float step = relics.Count <= 1 ? 0 : Mathf.Min(RelicSize + 6, (_relicsRight - left - RelicSize) / (relics.Count - 1));
            for (int i = 0; i < relics.Count; i++)
            {
                var relic = relics[i];
                if (_relics.ContainsKey(relic.Id)) continue;
                var img = Ui.Box($"Relic {relic.Id}", _relicRow, Palette.PanelLight, true);
                img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(left + RelicSize / 2 + i * step, 0), new Vector2(RelicSize, RelicSize));
                Ui.Border(img, Palette.Gold, 1.5f);
                // 遗物图标（10 §8）：衬一枚纸色圆片，没画好的继续用名字首字
                var icon = Ui.Box("Icon", img.transform, Palette.Ink);
                if (ArtSprites.Apply(icon, $"relic_{relic.Id}", Palette.Ink)) icon.rectTransform.Fill(3);
                else
                {
                    Destroy(icon.gameObject);
                    Ui.Text("Label", img.transform, relic.Name.Substring(0, 1), 24, Palette.Text).rectTransform.Fill();
                }
                TooltipTrigger.Add(img, () => $"{relic.Name}\n{relic.Description}");
                _relics[relic.Id] = img;
            }
        }

        public void SetHp(int hp, int max) => _hp.text = $"生命 {hp}/{max}";

        public void SetTurn(int turn) => _turn.text = $"第 {turn} 回合";

        public void SetGold(int gold)
        {
            if (_gold != null) _gold.text = $"◎ {gold}";
        }

        public void SetFloor(int floor)
        {
            if (_floor != null) _floor.text = floor <= 0 ? "出发" : $"第 {floor} 层";
        }

        /// <summary>按内核刷新药水栏。</summary>
        public void SyncPotions()
        {
            if (_potionAt == null) return;
            for (int i = 0; i < _potionSlots.Count; i++)
            {
                var potion = _potionAt(i);
                PotionIds[i] = potion?.Def.Id;
                _potionLabels[i].text = potion == null ? "" : potion.Name.Substring(0, 1);
                _potionSlots[i].color = potion == null ? Palette.Panel : Palette.PanelLight;
            }
        }

        /// <summary>药水栏高亮（等待选目标时）。slot = -1 取消高亮。</summary>
        public void HighlightPotion(int slot)
        {
            for (int i = 0; i < _potionSlots.Count; i++)
                _potionSlots[i].GetComponent<Outline>().effectColor = i == slot ? Palette.Accent : Palette.TextDim;
        }

        public void FlashRelic(string id)
        {
            if (id == null || !_relics.TryGetValue(id, out var img)) return;
            var rt = img.rectTransform;
            Tween.StopAll(rt);
            rt.localScale = Vector3.one;
            Tween.PunchScale(rt, new Vector3(0.45f, 0.45f, 0), 0.4f, 2);
        }

        string PotionTooltip(int slot)
        {
            var potion = _potionAt?.Invoke(slot);
            if (potion == null) return "空药水栏";
            string how = _engine == null ? "战斗中才能用；点击可以丢掉"
                : potion.Def.Target == CardTarget.Enemy ? "点击后再点一个敌人使用" : "点击使用";
            return $"{potion.Name}\n{potion.Description}\n（{how}）";
        }
    }

    /// <summary>通用点击回调（灰盒里给方块、全屏遮罩加点击用）。</summary>
    public sealed class ClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public Action<PointerEventData> OnClick;
        public void OnPointerClick(PointerEventData e) => OnClick?.Invoke(e);
    }
}
