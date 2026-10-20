using System.Collections.Generic;
using Deeptide.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 敌人意图（策划/10 §6.3）。灰盒图标 = 墨圆 + 一个汉字（10 §10）：攻 / 防 / 增 / 减 / 神 / 召 / ?。
    /// 清醒：图标 + 数值，攻击按单段伤害分 3 档大小。癫狂：数值换成墨渍，图标固定中档大小、以 12 帧 / 秒抖动并带青 / 血色差。
    /// 点亮提灯、鲸油灯让意图在癫狂时也看得清，这时图标旁加一个「灯」角标。
    /// </summary>
    public sealed class IntentBadge : MonoBehaviour
    {
        const float Icon = 56;

        RectTransform _shake, _iconRoot;
        Image _icon, _ghostA, _ghostB, _cross, _blot, _lantern;
        TextMeshProUGUI _glyph, _number;
        readonly List<GameObject> _chips = new List<GameObject>();
        float _nextJitter;

        /// <summary>当前是否因癫狂而模糊（测试用）。</summary>
        public bool Blurred { get; private set; }
        /// <summary>当前显示的数值和招式名（测试用；模糊、隐藏时为空）。</summary>
        public string NumberText => _number.gameObject.activeSelf ? _number.text : "";

        public static IntentBadge Create(Transform parent)
        {
            var rt = Ui.Rect("Intent", parent);
            rt.sizeDelta = new Vector2(200, 70);
            var badge = rt.gameObject.AddComponent<IntentBadge>();
            badge.Build();
            return badge;
        }

        void Build()
        {
            var root = (RectTransform)transform;
            _shake = Ui.Rect("Shake", root).Fill();

            _iconRoot = Ui.Rect("Icon", _shake).Place(new Vector2(0.5f, 0.5f), new Vector2(-34, 0), new Vector2(Icon, Icon));
            _ghostA = Circle("GhostTeal", _iconRoot, Palette.Lunacy, new Vector2(-3, 0));
            _ghostB = Circle("GhostBlood", _iconRoot, Palette.BloodLight, new Vector2(3, 0));
            _icon = Circle("Disc", _iconRoot, Palette.Ink, Vector2.zero);
            _glyph = Ui.Text("Glyph", _icon.transform, "", 30, Palette.Paper);
            _glyph.font = Fonts.Serif;
            _glyph.fontStyle = FontStyles.Bold;
            _glyph.rectTransform.Fill();

            _cross = Ui.Rect("Cross", _iconRoot).Fill().gameObject.AddComponent<Image>();
            _cross.color = new Color(0, 0, 0, 0);
            _cross.raycastTarget = false;
            foreach (float angle in new[] { 45f, -45f })
            {
                var bar = Ui.Box("Bar", _cross.transform, Palette.Blood);
                bar.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Icon * 1.25f, 6));
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            }

            _number = Ui.Text("Number", _shake, "", 34, Palette.Blood, TextAlignmentOptions.Left);
            _number.font = Fonts.Serif;
            _number.fontStyle = FontStyles.Bold;
            _number.richText = true;
            _number.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(112, 0), new Vector2(220, 44));

            _blot = Ui.Box("Blot", _shake, Palette.Ink);
            _blot.sprite = Shapes.Blot;
            _blot.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(26, -2), new Vector2(54, 43));

            _lantern = Circle("Lantern", _iconRoot, Palette.Paper, new Vector2(-Icon / 2, Icon / 2));
            _lantern.rectTransform.sizeDelta = new Vector2(26, 26);
            var ring = Ui.Box("Ring", _lantern.transform, Palette.Gold);
            ring.sprite = Shapes.Ring(28);
            ring.rectTransform.Fill();
            var lt = Ui.Text("Glyph", _lantern.transform, "灯", 15, Palette.Gold);
            lt.rectTransform.Fill();
            TooltipTrigger.Add(_lantern, () => "提灯照亮：本回合意图不受癫狂模糊");
            Clear();
        }

        static Image Circle(string name, Transform parent, Color color, Vector2 offset)
        {
            var img = Ui.Box(name, parent, color);
            img.sprite = Shapes.Circle;
            img.rectTransform.Place(new Vector2(0.5f, 0.5f), offset, new Vector2(Icon, Icon));
            return img;
        }

        public void Clear()
        {
            _shake.gameObject.SetActive(false);
            Blurred = false;
        }

        /// <param name="playerLunatic">玩家此刻是否癫狂：癫狂但意图没被模糊，说明有提灯类效果在照亮。</param>
        public void Show(IntentView v, bool playerLunatic)
        {
            _shake.gameObject.SetActive(true);
            _shake.anchoredPosition = Vector2.zero;
            foreach (var c in _chips) Destroy(c);
            _chips.Clear();

            Blurred = v.Blurred && !v.Hidden && !v.Stunned;
            bool attack = (v.Type & IntentType.Attack) != 0;
            var main = v.Hidden ? IntentType.Unknown : MainType(v.Type);
            _glyph.text = Glyph(main);
            _cross.gameObject.SetActive(v.Stunned);

            // 攻击按单段伤害分档；模糊时固定中档，否则图标大小本身就泄露了数值
            float scale = 1;
            if (attack && !Blurred && !v.Hidden) scale = v.Damage < 10 ? 0.85f : v.Damage < 20 ? 1f : 1.2f;
            _iconRoot.localScale = Vector3.one * scale;

            // 复合意图：除主类型以外的类型做成小角标，从图标右下往左排
            int k = 0;
            if (!v.Hidden)
                foreach (var t in new[] { IntentType.Attack, IntentType.Mind, IntentType.Debuff, IntentType.Buff, IntentType.Defend, IntentType.Summon })
                {
                    if ((v.Type & t) == 0 || t == main) continue;
                    _chips.Add(Chip(t, new Vector2(Icon / 2 - 4 - k * 24, -Icon / 2 + 6)));
                    k++;
                }

            string number = "";
            if (v.Stunned) number = $"<color={Ui.Hex(Palette.Ink)}><size=70%>震慑</size></color>";
            else if (!v.Hidden && !Blurred)
            {
                var parts = new List<string>();
                if (attack && v.Damage > 0) parts.Add(v.Hits > 1 ? $"{v.Damage}×{v.Hits}" : v.Damage.ToString());
                if ((v.Type & IntentType.Mind) != 0 && v.SanityDamage > 0)
                    parts.Add($"<color={Ui.Hex(Palette.Teal)}>{(attack ? "<size=60%>神</size>" : "")}{v.SanityDamage}</color>");
                number = string.Join("  ", parts);
            }
            // 招式名跟在数值后面，小一号的淡墨；模糊、隐藏、震慑时不显示
            string move = v.Hidden || Blurred || v.Stunned ? "" : v.MoveName ?? "";
            if (move.Length > 0)
                number += (number.Length > 0 ? "  " : "") + $"<size=50%><color={Ui.Hex(Palette.InkMid)}>{move}</color></size>";
            _number.text = number;
            _number.gameObject.SetActive(number.Length > 0);
            _blot.gameObject.SetActive(Blurred && HasNumber(v));
            _ghostA.gameObject.SetActive(Blurred);
            _ghostB.gameObject.SetActive(Blurred);
            _lantern.gameObject.SetActive(playerLunatic && !v.Blurred && !v.Hidden && !v.Stunned);
        }

        static bool HasNumber(IntentView v) => (v.Type & (IntentType.Attack | IntentType.Mind)) != 0;

        GameObject Chip(IntentType t, Vector2 pos)
        {
            var chip = Ui.Box("Chip", _iconRoot, Palette.Paper);
            chip.sprite = Shapes.Circle;
            chip.rectTransform.Place(new Vector2(0.5f, 0.5f), pos, new Vector2(26, 26));
            var ring = Ui.Box("Ring", chip.transform, Palette.Ink);
            ring.sprite = Shapes.Ring(26);
            ring.rectTransform.Fill();
            var text = Ui.Text("Glyph", chip.transform, Glyph(t), 15, t == IntentType.Mind ? Palette.Teal : Palette.Ink);
            text.rectTransform.Fill();
            return chip.gameObject;
        }

        void Update()
        {
            if (!Blurred || Time.unscaledTime < _nextJitter) return;
            _nextJitter = Time.unscaledTime + 1f / 12;
            _shake.anchoredPosition = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f));
            _ghostA.rectTransform.anchoredPosition = new Vector2(-2 - Random.value * 1.5f, 0);
            _ghostB.rectTransform.anchoredPosition = new Vector2(2 + Random.value * 1.5f, 0);
        }

        static IntentType MainType(IntentType t)
        {
            foreach (var x in new[] { IntentType.Attack, IntentType.Mind, IntentType.Debuff, IntentType.Buff, IntentType.Defend, IntentType.Summon })
                if ((t & x) != 0) return x;
            return IntentType.Unknown;
        }

        static string Glyph(IntentType t)
        {
            switch (t)
            {
                case IntentType.Attack: return "攻";
                case IntentType.Defend: return "防";
                case IntentType.Buff: return "增";
                case IntentType.Debuff: return "减";
                case IntentType.Mind: return "神";
                case IntentType.Summon: return "召";
                default: return "?";
            }
        }
    }
}
