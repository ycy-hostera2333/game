using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 理智条（策划/10 §6.2）：竖向，每点理智一格。清明段描金、癫狂段深渊底；癫狂线青色虚线标在右侧，清明线金色虚线标在左侧，
    /// 进入清明时「清明」两字点亮；悬停理智条显示两条界线的数值和还差多少。
    /// 预告三件事：敌人本回合的精神攻击（虚线框，癫狂时不显示）、下回合开始的侵蚀 / 光环（斜线）、悬停代价牌要付的理智（闪烁）。
    /// 坐标原点在理智条中心，单位是 1920×1080 参考分辨率。
    /// </summary>
    public sealed class SanityGauge : MonoBehaviour
    {
        public const float Height = 360;
        const float Width = 46, Gap = 4;

        sealed class Segment
        {
            public RectTransform Rt;
            public Image Fill, Outline, Hatch, Dashed;
        }

        readonly List<Segment> _segs = new List<Segment>();
        RectTransform _column, _tags;
        TextMeshProUGUI _value;
        RectTransform _lunacyLine, _clarityLine;
        Image _clarityDash;
        TextMeshProUGUI _clarityLabel;
        Image _ward;
        TextMeshProUGUI _wardText;

        int _sanity, _max, _line = 4, _clarity;
        string _lunacyName = "癫狂", _lunacyEnter = "进入癫狂", _lunacyVerb = "进入";
        TextMeshProUGUI _lunacyLabel;
        int _mind, _erosion, _aura, _cost = -1;
        bool _mindVisible;

        public int Sanity => _sanity;
        /// <summary>「清明」标签是否点亮（测试用）。</summary>
        public bool ClarityLit => _sanity >= _clarity;
        /// <summary>悬停预告的代价（测试用）；没有预告时为 -1。</summary>
        public int CostPreview => _cost;
        /// <summary>当前显示的预告标签（测试用）。</summary>
        public IReadOnlyList<string> Tags => _tagTexts;
        readonly List<string> _tagTexts = new List<string>();

        public static SanityGauge Create(Transform parent)
        {
            var rt = Ui.Rect("SanityGauge", parent);
            rt.sizeDelta = new Vector2(Width, Height);
            var gauge = rt.gameObject.AddComponent<SanityGauge>();
            gauge.Build();
            return gauge;
        }

        void Build()
        {
            var root = (RectTransform)transform;
            var title = Ui.Text("Title", root, "理智", 22, Palette.InkMid);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, Height / 2 + 24), new Vector2(80, 30));

            // UI 套件：理智格立在一根柱子里（柱头在格子上方、柱础在下方），文字让开
            float valueY = -Height / 2 - 24;
            var pillar = Ui.Box("Pillar", root, Palette.InkMid);
            if (ArtSprites.Apply(pillar, "ui_gauge", Palette.InkMid, ArtSprites.GaugeBorder, 1.5f))
            {
                pillar.raycastTarget = false;
                pillar.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(Width + 28, Height + 84));
                title.rectTransform.anchoredPosition = new Vector2(0, Height / 2 + 64);
                valueY = -Height / 2 - 60;
            }
            else Destroy(pillar.gameObject);

            _column = Ui.Rect("Column", root).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height));

            _lunacyLine = Ui.Rect("LunacyLine", root);
            var dash = Ui.Box("Dash", _lunacyLine, Palette.Teal);
            dash.sprite = Shapes.Dash;
            dash.type = Image.Type.Tiled;
            dash.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(42, 0), new Vector2(84, 6));
            var label = Ui.Text("Label", _lunacyLine, _lunacyName, 17, Palette.Teal, TextAlignmentOptions.Left);
            label.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(116, 0), new Vector2(60, 22));
            _lunacyLabel = label;
            _lunacyLine.anchorMin = _lunacyLine.anchorMax = new Vector2(0.5f, 0.5f);
            _lunacyLine.pivot = new Vector2(0, 0.5f);
            _lunacyLine.sizeDelta = new Vector2(84, 6);

            // 清明线：和癫狂线对称，画在左侧（右侧上方是预告标签）
            _clarityLine = Ui.Rect("ClarityLine", root);
            _clarityDash = Ui.Box("Dash", _clarityLine, Palette.Gold);
            _clarityDash.sprite = Shapes.Dash;
            _clarityDash.type = Image.Type.Tiled;
            _clarityDash.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-42, 0), new Vector2(84, 6));
            _clarityLabel = Ui.Text("Label", _clarityLine, "清明", 17, Palette.Gold, TextAlignmentOptions.Right);
            _clarityLabel.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-116, 0), new Vector2(60, 22));
            _clarityLine.anchorMin = _clarityLine.anchorMax = new Vector2(0.5f, 0.5f);
            _clarityLine.pivot = new Vector2(1, 0.5f);
            _clarityLine.sizeDelta = new Vector2(84, 6);

            // 悬停理智条看界线（盖在格子上面，透明）
            var hit = Ui.Box("Hit", root, new Color(0, 0, 0, 0));
            hit.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width + 28, Height + 8));
            TooltipTrigger.Add(hit, Describe);

            _value = Ui.Text("Value", root, "", 26, Palette.Ink);
            _value.font = Fonts.Serif;
            _value.fontStyle = FontStyles.Bold;
            _value.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, valueY), new Vector2(110, 34));

            _ward = Ui.Box("Ward", root, Palette.Teal);
            if (!ArtSprites.Apply(_ward, "icon_block", Palette.Teal)) _ward.sprite = Shapes.Shield;
            _ward.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(46, Height / 2 + 26), new Vector2(34, 38));
            _wardText = Ui.Text("Value", _ward.transform, "", 19, Palette.Paper);
            _wardText.font = Fonts.Serif;
            _wardText.fontStyle = FontStyles.Bold;
            _wardText.rectTransform.Fill();
            TooltipTrigger.Add(_ward, () => $"心防 {_wardText.text}：每层抵消一次来自敌人的理智损失（精神攻击、光环）");
            _ward.gameObject.SetActive(false);

            _tags = Ui.Rect("Tags", root).Place(new Vector2(0.5f, 0.5f), new Vector2(Width / 2 + 14, Height / 2 - 20), new Vector2(160, 10));
            _tags.pivot = new Vector2(0, 1);
        }

        // ------------------------------------------------------------------ 数据

        public void SetValue(int sanity, int max, int lunacyLine, int clarityThreshold)
        {
            bool rebuild = max != _max;
            _sanity = sanity;
            _max = Mathf.Max(1, max);
            _line = lunacyLine;
            _clarity = clarityThreshold;
            if (rebuild) BuildSegments();
            Redraw();
        }

        public void SetForecast(int mind, bool mindVisible, int erosion, int aura)
        {
            _mind = mind;
            _mindVisible = mindVisible;
            _erosion = erosion;
            _aura = aura;
            Redraw();
        }

        /// <summary>
        /// 癫狂那一侧叫什么：调查员是「癫狂 / 进入癫狂」，狼人是「兽形 / 变身」（策划/14 §2.2）。规则一样，只换字。
        /// 格数跟着上限走（SetValue 里上限变了就重排），狼人上限 6 时清明线（≥ 5）和癫狂线（≤ 4）落在同一个格缝上，一左一右不打架。
        /// </summary>
        /// <param name="enter">代价预告标签里的说法（「打出后进入癫狂」「打出后变身」）。</param>
        /// <param name="verb">悬停说明里接在「再失去 N 点」后面的词（「进入」「变身」）。</param>
        public void SetLunacyName(string name, string enter, string verb)
        {
            _lunacyName = name;
            _lunacyEnter = enter;
            _lunacyVerb = verb;
            if (_lunacyLabel != null) _lunacyLabel.text = name;
            Redraw();
        }

        /// <summary>癫狂那一侧的名字（测试用）。</summary>
        public string LunacyName => _lunacyName;

        public void SetWard(int ward)
        {
            _ward.gameObject.SetActive(ward > 0);
            _wardText.text = ward.ToString();
        }

        /// <summary>悬停 / 拖动代价牌时预告要付的理智；cost ≤ 0 清掉预告。</summary>
        public void SetCostPreview(int cost)
        {
            _cost = cost > 0 ? cost : -1;
            Redraw();
        }

        // ------------------------------------------------------------------ 绘制

        void BuildSegments()
        {
            foreach (var s in _segs) Destroy(s.Rt.gameObject);
            _segs.Clear();
            float h = (Height - Gap * (_max - 1)) / _max;
            for (int i = 0; i < _max; i++)
            {
                var rt = Ui.Rect($"Seg {i + 1}", _column);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.anchoredPosition = new Vector2(0, i * (h + Gap));
                rt.sizeDelta = new Vector2(Width, h);
                var seg = new Segment { Rt = rt };
                seg.Fill = Sliced("Fill", rt, Shapes.RoundRect(6), 1);
                seg.Hatch = Ui.Box("Hatch", rt, Color.white);
                seg.Hatch.sprite = Shapes.Hatch;
                seg.Hatch.type = Image.Type.Tiled;
                seg.Hatch.pixelsPerUnitMultiplier = 2.5f;
                seg.Hatch.rectTransform.Fill(2);
                seg.Outline = Sliced("Outline", rt, Shapes.RoundRect(6, 2), 1);
                seg.Dashed = Sliced("Dashed", rt, Shapes.DashedRect(6, 2, 5), 1);
                seg.Dashed.rectTransform.Fill(-4);
                _segs.Add(seg);
            }
        }

        static Image Sliced(string name, Transform parent, Sprite sprite, float ppu)
        {
            var img = Ui.Box(name, parent, Color.white);
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppu;
            img.rectTransform.Fill();
            return img;
        }

        void Redraw()
        {
            if (_segs.Count == 0) return;
            int afterMind = _sanity - (_mindVisible ? _mind : 0);
            int afterTurn = afterMind - (_erosion + _aura);
            for (int i = 0; i < _segs.Count; i++)
            {
                var s = _segs[i];
                int level = i + 1;
                bool lit = level <= _sanity;
                bool lunacy = level <= _line;
                bool clarity = level >= _clarity;

                s.Fill.color = lit ? (lunacy ? Palette.Lunacy : Palette.Ink) : lunacy ? Palette.Abyss : new Color(0, 0, 0, 0);
                s.Outline.color = clarity ? Palette.Gold : lunacy ? Palette.Teal : Fade(Palette.InkMid, 0.5f);
                s.Dashed.enabled = _mindVisible && lit && level > afterMind;
                s.Dashed.color = Palette.Teal;
                s.Hatch.enabled = lit && level <= afterMind && level > afterTurn;
                s.Hatch.color = lunacy ? Fade(Palette.Abyss, 0.75f) : Fade(Palette.Paper, 0.8f);
            }

            var line = _lunacyLine;
            float h = (Height - Gap * (_max - 1)) / _max;
            line.gameObject.SetActive(_line > 0 && _line < _max);
            line.anchoredPosition = new Vector2(-Width / 2 - 4, -Height / 2 + _line * (h + Gap) - Gap / 2);

            // 清明线在第 _clarity 格的下沿
            _clarityLine.gameObject.SetActive(_clarity > 1 && _clarity <= _max);
            _clarityLine.anchoredPosition = new Vector2(Width / 2 + 4, -Height / 2 + (_clarity - 1) * (h + Gap) - Gap / 2);
            bool clear = ClarityLit;
            _clarityDash.color = clear ? Palette.Gold : Fade(Palette.Gold, 0.45f);
            _clarityLabel.color = clear ? Palette.Gold : Fade(Palette.Gold, 0.55f);
            _clarityLabel.fontStyle = clear ? FontStyles.Bold : FontStyles.Normal;

            bool lunatic = _sanity <= _line;
            _value.text = $"{_sanity}/{_max}";
            _value.color = lunatic ? Palette.Teal : Palette.Ink;

            _tagTexts.Clear();
            if (_mindVisible && _mind > 0) _tagTexts.Add($"<color={Ui.Hex(Palette.Teal)}>精神攻击 −{_mind}</color>");
            int turnLoss = _erosion + _aura;
            if (turnLoss > 0)
                _tagTexts.Add($"<color={Ui.Hex(Palette.Teal)}>{(_erosion > 0 ? "侵蚀" : "光环")} −{turnLoss}</color>");
            if (_cost > 0)
            {
                int after = _sanity - _cost;
                if (after <= 0) _tagTexts.Add($"<color={Ui.Hex(Palette.Blood)}><b>崩溃</b></color>");
                else if (!lunatic && after <= _line) _tagTexts.Add($"<color={Ui.Hex(Palette.Teal)}><b>打出后{_lunacyEnter}</b></color>");
            }
            DrawTags();
        }

        /// <summary>悬停理智条的说明：当前理智，清明 / 癫狂的界线和离它们还差多少。</summary>
        public string Describe()
        {
            string clarity = _sanity >= _clarity ? "已清明" : $"还差 {_clarity - _sanity} 点";
            string lunacy = _sanity <= _line ? $"已{_lunacyName}" : $"再失去 {_sanity - _line} 点{_lunacyVerb}";
            return $"理智 {_sanity}/{_max}\n" +
                   $"<color={Ui.Hex(Palette.Gold)}>清明</color>：理智 ≥ {_clarity}（{clarity}）\n" +
                   $"<color={Ui.Hex(Palette.Teal)}>{_lunacyName}</color>：理智 ≤ {_line}（{lunacy}）\n" +
                   "理智归 0 崩溃";
        }

        readonly List<RectTransform> _tagViews = new List<RectTransform>();

        void DrawTags()
        {
            foreach (var t in _tagViews) Destroy(t.gameObject);
            _tagViews.Clear();
            for (int i = 0; i < _tagTexts.Count; i++)
            {
                var chip = Ui.Box($"Tag {i}", _tags, Fade(Palette.PaperMid, 0.92f));
                chip.sprite = Shapes.RoundRect(8);
                chip.type = Image.Type.Sliced;
                var rt = chip.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                var text = Ui.Text("Text", rt, _tagTexts[i], 17, Palette.Ink, TextAlignmentOptions.Left);
                text.richText = true;
                float w = text.GetPreferredValues(_tagTexts[i]).x + 16;
                rt.sizeDelta = new Vector2(w, 26);
                rt.anchoredPosition = new Vector2(0, -i * 30);
                text.rectTransform.Fill();
                text.margin = new Vector4(8, 0, 4, 0);
                _tagViews.Add(rt);
            }
        }

        void Update()
        {
            if (_cost <= 0 || _segs.Count == 0) return;
            // 代价预告：要付的格子以 4Hz 闪烁
            float a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 8));
            for (int i = 0; i < _segs.Count; i++)
            {
                int level = i + 1;
                bool paying = level <= _sanity && level > _sanity - _cost;
                var c = _segs[i].Fill.color;
                c.a = paying ? a : (level <= _sanity || level <= _line ? 1 : 0);
                _segs[i].Fill.color = c;
            }
        }

        static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
