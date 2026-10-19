using System;
using System.Collections.Generic;
using Deeptide.Core;
using Deeptide.Core.Potions;
using Deeptide.Core.Relics;
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
    /// </summary>
    public sealed class TopBar : MonoBehaviour
    {
        public const float Height = 64;
        const float SlotSize = 50, RelicSize = 46;
        const float RelicsLeft = -420, RelicsRight = 300;

        TextMeshProUGUI _hp, _turn;
        RectTransform _relicRow;
        readonly List<Image> _potionSlots = new List<Image>();
        readonly List<TextMeshProUGUI> _potionLabels = new List<TextMeshProUGUI>();
        readonly Dictionary<string, Image> _relics = new Dictionary<string, Image>();
        Action<int> _onPotion;
        BattleEngine _engine;

        /// <summary>每格药水栏显示的药水 ID（空格为 null），测试用。</summary>
        public readonly List<string> PotionIds = new List<string>();

        public static TopBar Create(RectTransform root, Action<int> onPotion, Action openLauncher, Action restart, Action toggleLog)
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

            bar._relicRow = Ui.Rect("Relics", rt).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, Height));

            bar._turn = Ui.Text("Turn", rt, "", 28, Palette.Text);
            bar._turn.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(420, 0), new Vector2(170, 50));

            Button(rt, "日志", new Vector2(580, 0), new Vector2(100, 50), toggleLog);
            Button(rt, "选择对局", new Vector2(720, 0), new Vector2(160, 50), openLauncher);
            Button(rt, "重开", new Vector2(870, 0), new Vector2(110, 50), restart);
            return bar;
        }

        static void Button(Transform parent, string label, Vector2 pos, Vector2 size, Action click)
        {
            var btn = Ui.TextButton(label, parent, label, Palette.PanelLight, () => click?.Invoke());
            btn.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), pos, size);
            btn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 24;
        }

        /// <summary>开场时按内核建药水栏和遗物栏。</summary>
        public void Bind(BattleEngine engine)
        {
            _engine = engine;
            var rt = (RectTransform)transform;
            var p = engine.Player;
            for (int i = 0; i < p.Potions.Count; i++)
            {
                int slot = i;
                var img = Ui.Box($"Potion {i}", rt, Palette.PanelLight, true);
                img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-660 + i * (SlotSize + 10), 0), new Vector2(SlotSize, SlotSize));
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

            float step = p.Relics.Count <= 1 ? 0 : Mathf.Min(RelicSize + 6, (RelicsRight - RelicsLeft - RelicSize) / (p.Relics.Count - 1));
            for (int i = 0; i < p.Relics.Count; i++)
            {
                var relic = p.Relics[i];
                var img = Ui.Box($"Relic {relic.Id}", _relicRow, Palette.PanelLight, true);
                img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(RelicsLeft + RelicSize / 2 + i * step, 0), new Vector2(RelicSize, RelicSize));
                Ui.Border(img, Palette.Gold, 1.5f);
                Ui.Text("Label", img.transform, relic.Name.Substring(0, 1), 24, Palette.Text).rectTransform.Fill();
                TooltipTrigger.Add(img, () => $"{relic.Name}\n{relic.Description}");
                _relics[relic.Id] = img;
            }
        }

        public void SetHp(int hp, int max) => _hp.text = $"生命 {hp}/{max}";

        public void SetTurn(int turn) => _turn.text = $"第 {turn} 回合";

        /// <summary>按内核刷新药水栏。</summary>
        public void SyncPotions()
        {
            if (_engine == null) return;
            var potions = _engine.Player.Potions;
            for (int i = 0; i < _potionSlots.Count && i < potions.Count; i++)
            {
                var potion = potions[i];
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
            var potion = _engine?.Player.Potions[slot];
            if (potion == null) return "空药水栏";
            string target = potion.Def.Target == CardTarget.Enemy ? "点击后再点一个敌人使用" : "点击使用";
            return $"{potion.Name}\n{potion.Description}\n（{target}）";
        }
    }

    /// <summary>通用点击回调（灰盒里给方块、全屏遮罩加点击用）。</summary>
    public sealed class ClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public Action<PointerEventData> OnClick;
        public void OnPointerClick(PointerEventData e) => OnClick?.Invoke(e);
    }
}
