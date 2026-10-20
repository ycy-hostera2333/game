using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    /// 一张牌（策划/10 §4）。卡面按 720 × 1008 的源尺寸搭（<see cref="CardFace"/>），手牌里缩到 1/3（240 × 336），悬停 1.5 倍。
    /// 文本走和结算同一条预览管线（Card.PreviewValue），被修正过的数字上色：增益苔绿、减益血色（10 §4.4）。
    /// 双面牌换面时播放墨渗翻面（Deeptide/UI/InkDissolve），悬停时在旁边以 70% 大小并排显示另一面（10 §4.3）。
    /// 输入（悬停、拖拽）转交给 HandView 处理，卡面自己不做规则判断。
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static readonly Vector2 Size = new Vector2(240, 336);
        const float FaceScale = 1 / 3f;
        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int RectId = Shader.PropertyToID("_Rect");

        public int Uid { get; private set; }
        public string CardId { get; private set; }
        public RectTransform Rect { get; private set; }
        public CanvasGroup Group { get; private set; }
        /// <summary>当前显示的是不是癫狂面（测试用）。</summary>
        public bool ShowsMadFace { get; private set; }
        /// <summary>另一面预览是否在显示（测试用）。</summary>
        public bool PreviewVisible => _preview != null && _previewRoot.gameObject.activeSelf;
        /// <summary>当前卡面正文（富文本，测试用）。</summary>
        public string FaceText => _face.Text;
        /// <summary>插图窗里是不是正式卡图（而不是灰盒首字，测试用）。</summary>
        public bool ShowsArt => _face.HasArt;

        HandView _hand;
        CardFace _face, _preview;
        RectTransform _previewRoot;
        TextMeshProUGUI _previewLabel, _mainLabel;
        Image _glow, _dim, _ink;
        Material _inkMat;
        BattleEngine _engine;
        bool _shown, _flipping, _hovered;
        CardFace.Data _pending;
        Sequence _flip;

        public static CardView Create(Transform parent, HandView hand, int uid, string cardId)
        {
            var rt = Ui.Rect($"Card {cardId} #{uid}", parent);
            // HandView 的坐标原点在底边中点
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.sizeDelta = Size;
            var view = rt.gameObject.AddComponent<CardView>();
            view.Build(hand, uid, cardId);
            return view;
        }

        void Build(HandView hand, int uid, string cardId)
        {
            _hand = hand;
            Uid = uid;
            CardId = cardId;
            Rect = (RectTransform)transform;
            Group = gameObject.AddComponent<CanvasGroup>();

            // 整张牌接收射线，悬停和拖拽都从这里进
            var hit = gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            _glow = Ui.Box("Glow", transform, Palette.GoldLight);
            _glow.sprite = Shapes.RoundRect(16);
            _glow.type = Image.Type.Sliced;
            _glow.rectTransform.Fill(-9);
            _glow.gameObject.SetActive(false);

            _face = CardFace.Create(transform, FaceScale);

            _dim = Ui.Box("Dim", transform, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.36f));
            _dim.sprite = Shapes.RoundRect(12);
            _dim.type = Image.Type.Sliced;
            _dim.rectTransform.Fill();
            _dim.gameObject.SetActive(false);

            _ink = Ui.Box("Ink", transform, Palette.Ink);
            _ink.sprite = Shapes.RoundRect(12);
            _ink.type = Image.Type.Sliced;
            _ink.rectTransform.Fill(-1);
            var shader = Shader.Find("Deeptide/UI/InkDissolve");
            if (shader != null)
            {
                _inkMat = new Material(shader) { hideFlags = HideFlags.DontSave };
                _ink.material = _inkMat;
            }
            _ink.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_flip.isAlive) _flip.Stop();
            if (_inkMat != null) Destroy(_inkMat);
        }

        /// <summary>按内核当前状态刷新卡面。energy 是表现层此刻显示的能量（事件回放中途可能还没追上内核）。</summary>
        public void Refresh(BattleEngine engine, int energy, bool canAct)
        {
            _engine = engine;
            var card = engine.FindCard(Uid);
            if (card == null) return;

            bool mad = engine.UsesMadFace(card);
            var data = Describe(card, engine, mad, energy, true);

            if (_shown && mad != ShowsMadFace && card.Def.Dual && _inkMat != null && isActiveAndEnabled) Flip(data);
            else if (_flipping) _pending = data;
            else _face.Apply(data);
            ShowsMadFace = mad;
            _shown = true;

            bool affordable = data.Cost != CardFace.CostState.Unaffordable && card.Playable;
            _dim.gameObject.SetActive(!affordable);
            // 「清明：」条件满足时外缘一圈金色微光（10 §4.4）
            _glow.gameObject.SetActive(affordable && data.ClarityActive);
            if (_hovered) UpdatePreview(card, engine, mad, energy);
        }

        /// <summary>HandView 悬停时调用：双面牌在旁边显示另一面。</summary>
        public void SetHover(bool on)
        {
            _hovered = on;
            var card = _engine?.FindCard(Uid);
            if (!on || card == null || !card.Def.Dual)
            {
                if (_previewRoot != null) _previewRoot.gameObject.SetActive(false);
                if (_mainLabel != null) _mainLabel.gameObject.SetActive(false);
                return;
            }
            UpdatePreview(card, _engine, _engine.UsesMadFace(card), _engine.Player.Energy);
        }

        void UpdatePreview(Card card, BattleEngine engine, bool mad, int energy)
        {
            if (!card.Def.Dual) return;
            if (_previewRoot == null)
            {
                _previewRoot = Ui.Rect("OtherFace", transform);
                _previewRoot.anchorMin = _previewRoot.anchorMax = new Vector2(0.5f, 0.5f);
                _previewRoot.sizeDelta = Size * 0.7f;
                _preview = CardFace.Create(_previewRoot, FaceScale * 0.7f);
                _previewLabel = Label(_previewRoot, Size.y * 0.35f + 16);
                _mainLabel = Label(transform, Size.y / 2 + 16);
            }
            // 放在右边；靠右的牌放左边，免得出屏
            float side = Rect.anchoredPosition.x > 420 ? -1 : 1;
            _previewRoot.anchoredPosition = new Vector2(side * (Size.x / 2 + Size.x * 0.35f + 12), -Size.y * 0.15f);
            _preview.Apply(Describe(card, engine, !mad, energy, false));
            _previewLabel.text = mad ? "清醒面" : "癫狂面";
            _mainLabel.text = mad ? "癫狂面 · 当前" : "清醒面 · 当前";
            _previewRoot.gameObject.SetActive(true);
            _mainLabel.gameObject.SetActive(true);
        }

        static TextMeshProUGUI Label(Transform parent, float y)
        {
            var t = Ui.Text("FaceLabel", parent, "", 15, Palette.InkMid);
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(200, 22));
            t.outlineWidth = 0.2f;
            t.outlineColor = Palette.Paper;
            return t;
        }

        /// <summary>翻面（10 §4.3）：墨从底部涨上来盖住卡面（0.15 秒），换面，再往上退去（0.15 秒）。</summary>
        void Flip(CardFace.Data data)
        {
            _pending = data;
            if (_flipping) return;
            _flipping = true;
            var r = _ink.rectTransform.rect;
            _inkMat.SetVector(RectId, new Vector4(r.xMin, r.yMin, r.width, r.height));
            _inkMat.SetFloat(ProgressId, 0);
            _ink.gameObject.SetActive(true);
            _ink.transform.SetAsLastSibling();
            _flip = Sequence.Create()
                .Chain(Tween.Custom(_inkMat, 0f, 1f, 0.15f, (m, v) => m.SetFloat(ProgressId, v)))
                .ChainCallback(this, v => v._face.Apply(v._pending), warnIfTargetDestroyed: false)
                .Chain(Tween.Custom(_inkMat, 1f, 2f, 0.15f, (m, v) => m.SetFloat(ProgressId, v)))
                .ChainCallback(this, v =>
                {
                    v._flipping = false;
                    v._ink.gameObject.SetActive(false);
                    v._face.Apply(v._pending);
                }, warnIfTargetDestroyed: false);
        }

        // ------------------------------------------------------------------ 文本

        static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");
        static readonly Regex CostWord = new Regex(@"代价(\d+|X)");
        static readonly Regex ClarityClause = new Regex(@"清明：[^。]*。?");

        /// <summary>把一张牌的某一面转成卡面数据。withNote：带上「（当前 21）」这类动态说明（只有当前生效的一面有）。</summary>
        public static CardFace.Data Describe(Card card, BattleEngine e, bool mad, int energy, bool withNote)
        {
            var def = card.Def;
            bool dark = mad && def.Dual;
            bool clarity = e != null && e.Player.IsClarity;
            string raw = card.RawText(dark) ?? "";

            string text = Placeholder.Replace(raw, m =>
            {
                string key = m.Groups[1].Value;
                int baseValue = card.V(key);
                int v = e != null ? card.PreviewValue(e, key) : baseValue;
                if (v > baseValue) return Colored(v.ToString(), dark ? Palette.MossLight : Palette.Moss);
                if (v < baseValue) return Colored(v.ToString(), dark ? Palette.BloodLight : Palette.Blood);
                return v.ToString();
            });
            if (withNote && e != null)
            {
                string plain = Placeholder.Replace(raw, m => card.PreviewValue(e, m.Groups[1].Value).ToString());
                string full = card.Describe(e);
                if (full.Length > plain.Length && full.StartsWith(plain)) text += full.Substring(plain.Length);
            }
            text = CostWord.Replace(text, m => $"<b>{Colored(m.Value, dark ? Palette.Lunacy : Palette.Teal)}</b>");
            text = ClarityClause.Replace(text, m => clarity
                ? Colored(m.Value, dark ? Palette.GoldLight : Palette.Gold)
                : $"<alpha=#80>{m.Value}<alpha=#FF>");

            var cost = CardFace.CostState.Normal;
            string costText;
            if (!card.Playable)
            {
                costText = "—";
                cost = CardFace.CostState.None;
            }
            else if (def.XCost) costText = "X";
            else
            {
                int c = e != null ? e.CostOf(card) : card.Cost;
                costText = c.ToString();
                if (e != null && c > energy) cost = CardFace.CostState.Unaffordable;
            }

            return new CardFace.Data
            {
                Name = card.Name,
                Glyph = def.Name.Length > 0 ? def.Name.Substring(0, 1) : "?",
                CostText = costText,
                Cost = cost,
                TypeLine = TypeLine(def),
                Text = text,
                Archetype = def.Archetype,
                Type = def.Type,
                Rarity = def.Rarity,
                Dual = def.Dual,
                Mad = dark,
                ClarityActive = clarity && raw.Contains("清明："),
                Id = card.DeckId,
            };
        }

        static string Colored(string s, Color c) => $"<color={Ui.Hex(c)}>{s}</color>";

        static string TypeLine(CardDef def)
        {
            string type = TypeName(def.Type);
            switch (def.Archetype)
            {
                case "守夜":
                case "堕落":
                case "传染":
                case "通用": return $"{type} · {def.Archetype}";
                default: return type;
            }
        }

        public static string TypeName(CardType t)
        {
            switch (t)
            {
                case CardType.Attack: return "攻击";
                case CardType.Skill: return "技能";
                case CardType.Power: return "能力";
                case CardType.Curse: return "诅咒";
                default: return "状态";
            }
        }

        public void OnPointerEnter(PointerEventData e) => _hand.Hover(this, true);
        public void OnPointerExit(PointerEventData e) => _hand.Hover(this, false);
        public void OnBeginDrag(PointerEventData e) => _hand.BeginDrag(this, e);
        public void OnDrag(PointerEventData e) => _hand.Drag(this, e);
        public void OnEndDrag(PointerEventData e) => _hand.EndDrag(this, e);
    }

    /// <summary>
    /// 卡面版式（策划/10 §4.1–§4.2），按 720 × 1008 的源尺寸搭，整体再缩放。纵向：头部 0–132、插图窗 132–564、类型行 564–612、正文 624–948、底栏 948–1008。
    /// 类型看窗形（攻击尖拱 / 技能倒角方窗 / 能力圆窗 / 诅咒墨渍 / 状态虚线），稀有度看框饰，流派看顶部色条和卡名左侧徽记。
    /// 癫狂面反相：深渊底、发光青线条、月白正文，边框长出藤壶（10 §4.3）。灰盒阶段插图窗里放卡名首字。
    /// </summary>
    public sealed class CardFace
    {
        public const float W = 720, H = 1008;

        public enum CostState { Normal, Unaffordable, None }

        public struct Data
        {
            public string Name, Glyph, CostText, TypeLine, Text, Archetype, Id;
            public CostState Cost;
            public CardType Type;
            public Rarity Rarity;
            public bool Dual, Mad, ClarityActive;
        }

        RectTransform _root;
        Image _fill, _outer, _inner, _strip, _seal, _banner, _bannerBack, _orb, _orbRing, _badge, _badgeRing, _badgeHalf;
        Image _window, _windowLine, _windowDash;
        RawImage _hatch, _art;
        TextMeshProUGUI _cost, _name, _glyph, _type, _text, _id;
        readonly List<Image> _rivets = new List<Image>();
        readonly List<Image> _barnacles = new List<Image>();
        WindowShape _shape = (WindowShape)(-1);

        public string Text => _text.text;
        public bool HasArt => _art.gameObject.activeSelf;

        public static CardFace Create(Transform parent, float scale)
        {
            var face = new CardFace();
            face.Build(parent, scale);
            return face;
        }

        void Build(Transform parent, float scale)
        {
            _root = Ui.Rect("Face", parent);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(W, H);
            _root.localScale = Vector3.one * scale;

            _fill = Sliced("Fill", _root, Shapes.RoundRect(36), 1);
            _fill.rectTransform.Fill();
            _outer = Sliced("Frame", _root, Shapes.RoundRect(36, 6), 1);
            _outer.rectTransform.Fill();
            _inner = Sliced("Inner", _root, Shapes.RoundRect(24, 3), 1);
            _inner.rectTransform.Fill(16);
            foreach (var p in new[] { new Vector2(40, 40), new Vector2(W - 40, 40) })
                _rivets.Add(At(Ui.Box("Rivet", _root, Palette.Ink), p, new Vector2(18, 18), Shapes.Circle));

            // 侵蚀变体（只在癫狂面）：边框上的藤壶
            var rng = new System.Random(3);
            for (int i = 0; i < 10; i++)
            {
                float x = i % 2 == 0 ? 10 + (float)rng.NextDouble() * 10 : W - 10 - (float)rng.NextDouble() * 10;
                float y = 180 + (float)rng.NextDouble() * 760;
                float d = 12 + (float)rng.NextDouble() * 14;
                _barnacles.Add(At(Ui.Box("Barnacle", _root, Palette.Lunacy), new Vector2(x, y), new Vector2(d, d), Shapes.Circle));
            }

            _strip = Ui.Box("Strip", _root, Palette.InkMid);
            Top(_strip.rectTransform, 40, 12, W - 80, 12);

            _bannerBack = Sliced("BannerBack", _root, Shapes.RoundRect(12, 3), 1);
            Top(_bannerBack.rectTransform, 204, 46, 430, 68);
            _banner = Sliced("Banner", _root, Shapes.RoundRect(12), 1);
            Top(_banner.rectTransform, 196, 38, 430, 68);
            var bannerLine = Sliced("Line", _banner.rectTransform, Shapes.RoundRect(12, 3), 1);
            bannerLine.rectTransform.Fill();
            bannerLine.color = Palette.Ink;

            _orb = At(Ui.Box("Cost", _root, Palette.Paper), new Vector2(82, 74), new Vector2(115, 115), Shapes.Circle);
            _orbRing = Ui.Box("Ring", _orb.transform, Palette.Ink);
            _orbRing.sprite = Shapes.Ring(14);
            _orbRing.rectTransform.Fill();
            _cost = Ui.Text("Value", _orb.transform, "", 72, Palette.Ink);
            _cost.font = Fonts.Serif;
            _cost.fontStyle = FontStyles.Bold;
            _cost.rectTransform.Fill();

            _seal = At(Ui.Box("Seal", _root, Palette.Gold), new Vector2(180, 74), new Vector2(44, 44), Shapes.Circle);

            _name = Ui.Text("Name", _root, "", 48, Palette.Ink, TextAlignmentOptions.Left);
            _name.font = Fonts.Serif;
            _name.fontStyle = FontStyles.Bold;
            _name.enableAutoSizing = true;
            _name.fontSizeMin = 32;
            _name.fontSizeMax = 48;
            Top(_name.rectTransform, 214, 34, 400, 80);

            _badge = At(Ui.Box("Dual", _root, Palette.Paper), new Vector2(W - 70, 74), new Vector2(58, 58), Shapes.Circle);
            _badgeHalf = Ui.Box("Half", _badge.transform, Palette.Teal);
            _badgeHalf.sprite = Shapes.Circle;
            _badgeHalf.type = Image.Type.Filled;
            _badgeHalf.fillMethod = Image.FillMethod.Horizontal;
            _badgeHalf.fillAmount = 0.5f;
            _badgeHalf.rectTransform.Fill();
            _badgeRing = Ui.Box("Ring", _badge.transform, Palette.Ink);
            _badgeRing.sprite = Shapes.Ring(20);
            _badgeRing.rectTransform.Fill();

            // 插图窗：遮罩 = 窗形；里面是斜排线 + 卡名首字（灰盒，和纸面原型的印章框一样）
            _window = Ui.Box("Window", _root, Palette.PaperMid);
            _window.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _hatch = Ui.Rect("Hatch", _window.transform).Fill().gameObject.AddComponent<RawImage>();
            _hatch.texture = Shapes.HatchTexture;
            _hatch.raycastTarget = false;
            // 正式卡图（11 §3）：始终按 624 × 432 摆在窗心，圆窗（能力牌）由遮罩裁掉两边
            _art = Ui.Rect("Art", _window.transform).gameObject.AddComponent<RawImage>();
            _art.rectTransform.anchorMin = _art.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _art.rectTransform.sizeDelta = new Vector2(CardArt.Width, CardArt.Height);
            _art.raycastTarget = false;
            _art.gameObject.SetActive(false);
            _glyph = Ui.Text("Glyph", _window.transform, "", 260, Palette.Gold);
            _glyph.font = Fonts.Serif;
            _glyph.fontStyle = FontStyles.Bold;
            _glyph.rectTransform.Fill();
            _windowLine = Ui.Box("WindowLine", _root, Palette.Ink);
            _windowDash = Sliced("WindowDash", _root, Shapes.DashedRect(10, 3, 12), 1);

            _type = Ui.Text("Type", _root, "", 28, Palette.InkMid);
            Top(_type.rectTransform, 48, 566, W - 96, 44);

            _text = Ui.Text("Text", _root, "", 40, Palette.Ink);
            _text.richText = true;
            _text.enableAutoSizing = true;
            _text.fontSizeMin = 30;
            _text.fontSizeMax = 40;
            _text.lineSpacing = -8;
            Top(_text.rectTransform, 72, 624, W - 144, 324);

            _id = Ui.Text("Id", _root, "", 24, Palette.InkMid, TextAlignmentOptions.Right);
            Top(_id.rectTransform, W - 220, 950, 170, 40);
            _id.gameObject.SetActive(Debug.isDebugBuild);
        }

        public void Apply(Data d)
        {
            bool mad = d.Mad;
            Color line = mad ? Palette.Lunacy : Palette.Ink;
            Color paper = mad ? Palette.AbyssCard : Palette.Paper;
            Color text = mad ? Palette.Pale : Palette.Ink;
            Color faction = Palette.Faction(d.Archetype, mad);
            bool special = d.Type == CardType.Curse || d.Type == CardType.Status;

            _fill.color = paper;
            _outer.color = line;
            _outer.sprite = Shapes.RoundRect(36, d.Rarity == Rarity.Rare ? 10 : 6);
            bool doubleFrame = d.Rarity == Rarity.Uncommon || d.Rarity == Rarity.Rare;
            _inner.gameObject.SetActive(doubleFrame);
            _inner.color = line;
            foreach (var r in _rivets)
            {
                r.gameObject.SetActive(d.Rarity != Rarity.Basic && !special);
                r.color = line;
                r.sprite = d.Rarity == Rarity.Rare ? Shapes.Diamond : Shapes.Circle;
            }
            foreach (var b in _barnacles) b.gameObject.SetActive(mad);

            _strip.color = special ? (mad ? Palette.Pale : Palette.Ink) : faction;
            _banner.gameObject.SetActive(doubleFrame);
            _banner.color = mad ? Palette.Abyss : Palette.PaperMid;
            _banner.transform.GetChild(0).GetComponent<Image>().color = line;
            _bannerBack.gameObject.SetActive(d.Rarity == Rarity.Rare);
            _bannerBack.color = mad ? Palette.Teal : Palette.InkMid;

            _orb.color = mad ? Palette.Abyss : Palette.Paper;
            _orbRing.color = d.Cost == CostState.Unaffordable ? (mad ? Palette.BloodLight : Palette.Blood) : line;
            _orbRing.sprite = d.Cost == CostState.None ? Shapes.Ring(6) : Shapes.Ring(14);
            _cost.text = d.CostText;
            _cost.color = d.Cost == CostState.None ? Palette.InkMid : text;

            switch (d.Archetype)
            {
                case "守夜": _seal.sprite = Shapes.Circle; break;
                case "堕落": _seal.sprite = Shapes.Diamond; break;
                case "传染": _seal.sprite = Shapes.Ring(40); break;
                default: _seal.sprite = null; break;
            }
            _seal.gameObject.SetActive(_seal.sprite != null);
            _seal.color = faction;

            _name.text = d.Name;
            _name.color = text;

            _badge.gameObject.SetActive(d.Dual);
            _badge.color = mad ? Palette.Lunacy : Palette.Paper;
            _badgeHalf.color = mad ? Palette.AbyssCard : Palette.Teal;
            _badgeHalf.fillOrigin = mad ? (int)Image.OriginHorizontal.Left : (int)Image.OriginHorizontal.Right;
            _badgeRing.color = line;

            ApplyWindow(d, mad, faction);

            _type.text = d.TypeLine;
            _type.color = mad ? new Color(Palette.Pale.r, Palette.Pale.g, Palette.Pale.b, 0.75f) : Palette.InkMid;
            _text.text = d.Text;
            _text.color = text;
            _id.text = d.Id;
            _id.color = mad ? Palette.Pale : Palette.InkMid;
        }

        void ApplyWindow(Data d, bool mad, Color faction)
        {
            var shape = d.Type == CardType.Attack ? WindowShape.Arch
                : d.Type == CardType.Power ? WindowShape.Circle
                : d.Type == CardType.Curse ? WindowShape.Blot
                : WindowShape.Bevel;
            if (shape != _shape)
            {
                _shape = shape;
                var size = shape == WindowShape.Circle ? new Vector2(432, 432) : new Vector2(624, 432);
                float x = (W - size.x) / 2;
                _window.sprite = Shapes.Window(shape);
                Top(_window.rectTransform, x, 132, size.x, size.y);
                _windowLine.sprite = Shapes.Window(shape, 3);
                Top(_windowLine.rectTransform, x, 132, size.x, size.y);
                Top(_windowDash.rectTransform, x, 132, size.x, size.y);
                _hatch.uvRect = new Rect(0, 0, size.x / 36f, size.y / 36f);
            }
            bool status = d.Type == CardType.Status;
            _window.color = mad ? Palette.Abyss : status ? Palette.PaperShade : Palette.PaperMid;
            _hatch.color = mad ? new Color(Palette.Lunacy.r, Palette.Lunacy.g, Palette.Lunacy.b, 0.14f) : new Color(Palette.PaperShade.r, Palette.PaperShade.g, Palette.PaperShade.b, 0.9f);
            _glyph.text = d.Glyph;
            _glyph.color = mad ? Palette.Lunacy : new Color(faction.r, faction.g, faction.b, 0.85f);

            // 有卡图就换掉灰盒（斜排线 + 首字）；清醒面和癫狂面各用一套材质（CardArt.For）
            var art = ArtDatabase.Card(d.Id);
            _art.gameObject.SetActive(art != null);
            _hatch.gameObject.SetActive(art == null);
            _glyph.gameObject.SetActive(art == null);
            if (art != null)
            {
                _art.texture = art;
                _art.material = CardArt.For(mad);
            }
            _windowLine.gameObject.SetActive(!status);
            _windowDash.gameObject.SetActive(status);
            _windowLine.color = mad ? Palette.Lunacy : Palette.Ink;
            _windowDash.color = Palette.InkMid;
        }

        static Image Sliced(string name, Transform parent, Sprite sprite, float ppu)
        {
            var img = Ui.Box(name, parent, Color.white);
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppu;
            return img;
        }

        /// <summary>按「从左上角量」的坐标摆放（卡面版式表就是这么写的）。</summary>
        static void Top(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + w / 2, -(y + h / 2));
            rt.sizeDelta = new Vector2(w, h);
        }

        static Image At(Image img, Vector2 center, Vector2 size, Sprite sprite)
        {
            img.sprite = sprite;
            Top(img.rectTransform, center.x - size.x / 2, center.y - size.y / 2, size.x, size.y);
            return img;
        }
    }
}
