using System;
using System.Text.RegularExpressions;
using Deeptide.Core.Cards;
using Deeptide.View.Battle;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deeptide.View.Run
{
    /// <summary>跑局界面共用的部件（策划/10 §8「奖励 / 牌组 / 选牌：纸面卷轴，纯 UI」）：画布、牌匾按钮、卷轴面板、可点的卡牌。</summary>
    public static class RunUi
    {
        /// <summary>和战斗界面同一套画布设置：相机空间、1920 × 1080 参考分辨率，按窗口比例对齐宽或高（CanvasFit）。</summary>
        public static RectTransform Canvas(string name, Camera camera, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = Ui.Layer };
            var canvas = go.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<CanvasFit>();
            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        /// <summary>牌匾按钮（UI 套件 ui_plaque；没画好时一道墨边）。不可用时变淡，悬停显示原因。</summary>
        public static Button Button(Transform parent, string name, string label, Vector2 pos, Vector2 size, Action click,
            bool enabled = true, string reason = null, float fontSize = 28)
        {
            var btn = Ui.TextButton(name, parent, label, Palette.PanelLight, () => click?.Invoke());
            var rt = btn.GetComponent<RectTransform>();
            rt.Place(new Vector2(0.5f, 0.5f), pos, size);
            var bg = (Image)btn.targetGraphic;
            bg.sprite = Shapes.RoundRect(14);
            bg.type = Image.Type.Sliced;
            if (ArtSprites.Frame(bg.transform, "ui_plaque", ArtSprites.PlaqueBorder, Palette.Ink, 1.4f) == null) Ui.Border(bg, Palette.Ink, 2);
            var text = btn.GetComponentInChildren<TextMeshProUGUI>();
            text.richText = true;
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = 16;
            text.margin = new Vector4(14, 4, 14, 4);
            if (!enabled)
            {
                btn.interactable = false;
                var group = btn.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0.45f;
                if (!string.IsNullOrEmpty(reason)) TooltipTrigger.Add(bg, () => reason);
            }
            return btn;
        }

        /// <summary>卷轴面板（UI 套件 ui_scroll；没画好时一道墨边）。</summary>
        public static Image Panel(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var box = Ui.Box(name, parent, Palette.Paper, true);
            box.rectTransform.Place(new Vector2(0.5f, 0.5f), pos, size);
            if (ArtSprites.Frame(box.transform, "ui_scroll", ArtSprites.ScrollBorder, Palette.Ink, 1.2f, -6) == null) Ui.Border(box, Palette.Ink, 3);
            return box;
        }

        /// <summary>挡住下层点击的半透明墨色遮罩。</summary>
        public static Image Dim(Transform parent, float alpha = 0.72f)
        {
            var dim = Ui.Box("Dim", parent, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, alpha), true);
            dim.rectTransform.Fill();
            return dim;
        }

        /// <summary>宋体粗体的标题。</summary>
        public static TextMeshProUGUI Title(Transform parent, string text, Vector2 pos, Vector2 size, float fontSize = 52, Color? color = null)
        {
            var t = Ui.Text("Title", parent, text, fontSize, color ?? Palette.Text);
            t.font = Fonts.Serif;
            t.fontStyle = FontStyles.Bold;
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), pos, size);
            return t;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, Vector2 pos, Vector2 size, float fontSize = 26,
            Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = Ui.Text("Label", parent, text, fontSize, color ?? Palette.Text, align);
            t.richText = true;
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), pos, size);
            return t;
        }

        static readonly Regex HpCost = new Regex(@"((失去|恢复) )?\d+ (最大)?生命");
        static readonly Regex SanityCost = new Regex(@"理智上限( ?[−+]\d+|恢复到 \d+)?");
        static readonly Regex GoldCost = new Regex(@"((支付|获得) )?\d+ 金币");

        /// <summary>选项文字里的代价用功能色（10 §8 事件）：生命血色、理智上限深渊青、金币清明金。</summary>
        public static string ColorCosts(string label)
        {
            label = HpCost.Replace(label, m => $"<color={Ui.Hex(Palette.Blood)}>{m.Value}</color>");
            label = SanityCost.Replace(label, m => $"<color={Ui.Hex(Palette.Teal)}>{m.Value}</color>");
            label = GoldCost.Replace(label, m => $"<color={Ui.Hex(Palette.Gold)}>{m.Value}</color>");
            return label;
        }

        /// <summary>
        /// 一张插图（ArtDatabase 的插图表，10 §8）：RawImage + 自己的一份 Etching 材质（清醒面配色，癫狂层着深渊青，_Reveal 从 0 开始），
        /// uv 截取一部分（商店只用雾中商贩的左半）。还没有这张图时返回 null，调用方用别的东西顶上。
        /// </summary>
        public static Illustration Plate(Transform parent, string id, Vector2 pos, Vector2 size, Rect? uv = null)
        {
            var tex = ArtDatabase.Illustration(id);
            var shader = Shader.Find("Deeptide/UI/Etching");
            if (tex == null || shader == null) return null;
            var frame = Ui.Box($"Illustration {id}", parent, Palette.Ink);
            frame.rectTransform.Place(new Vector2(0.5f, 0.5f), pos, size);
            var art = Ui.Rect("Art", frame.transform).Fill(6).gameObject.AddComponent<RawImage>();
            art.texture = tex;
            art.raycastTarget = false;
            if (uv.HasValue) art.uvRect = uv.Value;
            var mat = new Material(shader) { hideFlags = HideFlags.DontSave, name = $"Illustration {id}" };
            mat.SetColor("_PaperColor", Palette.Paper);
            mat.SetColor("_InkColor", Palette.Ink);
            mat.SetColor("_AbyssColor", Palette.Teal);
            mat.SetColor("_MadColor", Palette.Teal);
            mat.SetFloat("_Reveal", 0);
            art.material = mat;
            var ill = frame.gameObject.AddComponent<Illustration>();
            ill.Init(art, mat);
            return ill;
        }

        /// <summary>牌组里的一张牌（ID 带 + 表示升级）画成卡面数据：不在战斗里，画清醒面、不带动态说明。</summary>
        public static CardFace.Data Face(string deckId, bool mad = false) =>
            CardView.Describe(CardLibrary.Create(deckId, 0), null, mad, 0, false);
    }

    /// <summary>一张插图：持有自己的材质，癫狂层可以慢慢浮出（悬停会扣理智的选项时，10 §1.3「理智低时看见不该看的东西」）。</summary>
    public sealed class Illustration : MonoBehaviour
    {
        static readonly int RevealId = Shader.PropertyToID("_Reveal");

        public RawImage Art { get; private set; }
        public float Reveal => _mat != null ? _mat.GetFloat(RevealId) : 0;

        Material _mat;

        public void Init(RawImage art, Material mat)
        {
            Art = art;
            _mat = mat;
        }

        /// <summary>癫狂层浮出到 target（0 = 不显示），0.35 秒补间。</summary>
        public void RevealTo(float target)
        {
            if (_mat == null) return;
            Tween.StopAll(_mat);
            Tween.Custom(_mat, _mat.GetFloat(RevealId), target, 0.35f, (m, v) => m.SetFloat(RevealId, v), Ease.OutQuad);
        }

        /// <summary>悬停 target 时浮出癫狂层，移开时退回去。</summary>
        public void RevealOnHover(Graphic target, float amount = 0.75f)
        {
            var hover = target.gameObject.AddComponent<HoverHook>();
            hover.Enter = () => RevealTo(amount);
            hover.Exit = () => RevealTo(0);
        }

        void OnDestroy()
        {
            if (_mat == null) return;
            Tween.StopAll(_mat);
            Destroy(_mat);
        }
    }

    /// <summary>通用的悬停回调。</summary>
    public sealed class HoverHook : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Enter, Exit;
        public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
        public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
        void OnDisable() => Exit?.Invoke();
    }

    /// <summary>
    /// 跑局界面里一张可点的卡（奖励选牌、商店、牌组）：正式卡面，悬停放大一点；双面牌悬停时在旁边并排显示癫狂面（10 §4.3），
    /// 指定了 HoverAlt 时（锻造）显示那张牌（升级后的样子）。
    /// </summary>
    public sealed class CardTile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public string DeckId { get; private set; }
        public bool Enabled { get; private set; } = true;
        /// <summary>悬停时的旁边预览是否在显示（测试用）。</summary>
        public bool PreviewVisible => _preview != null && _preview.gameObject.activeSelf;

        Action _click;
        RectTransform _rt, _preview;
        string _altId, _altLabel;
        Vector2 _size;

        public static CardTile Create(Transform parent, string deckId, Vector2 pos, float width, Action click, bool enabled = true,
            string altId = null, string altLabel = null)
        {
            var size = new Vector2(width, width * CardFace.H / CardFace.W);
            var hit = Ui.Box($"Card {deckId}", parent, new Color(0, 0, 0, 0), true);
            hit.rectTransform.Place(new Vector2(0.5f, 0.5f), pos, size);
            var tile = hit.gameObject.AddComponent<CardTile>();
            tile._rt = hit.rectTransform;
            tile._size = size;
            tile.DeckId = deckId;
            tile._click = click;
            tile.Enabled = enabled;
            tile._altId = altId;
            tile._altLabel = altLabel;
            CardFace.Create(hit.transform, width / CardFace.W).Apply(RunUi.Face(deckId));
            if (!enabled)
            {
                var dim = Ui.Box("Dim", hit.transform, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.4f));
                dim.sprite = Shapes.RoundRect(12);
                dim.type = Image.Type.Sliced;
                dim.rectTransform.Fill();
            }
            return tile;
        }

        /// <summary>悬停时放大到约 290 宽（商店、牌组里的小卡放大到能看清正文），大卡只放大一点。</summary>
        float HoverScale => Mathf.Clamp(290f / _size.x, 1.06f, 1.5f);

        public void OnPointerEnter(PointerEventData e)
        {
            // 放大后会盖住相邻的卡：挪到同级最后
            transform.SetAsLastSibling();
            Tween.StopAll(_rt);
            Tween.Scale(_rt, HoverScale, 0.12f);
            ShowPreview(true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            Tween.StopAll(_rt);
            Tween.Scale(_rt, 1f, 0.12f);
            ShowPreview(false);
        }

        public void OnPointerClick(PointerEventData e) => Click();

        public void Click()
        {
            if (_click != null) _click();
        }

        void OnDisable()
        {
            if (_rt != null) _rt.localScale = Vector3.one;
        }

        // 点了之后面板马上重画，这张卡会在悬停放大的补间中途被销毁
        void OnDestroy() => Tween.StopAll(_rt);

        /// <summary>旁边的预览：锻造时是升级后的这张牌，否则双面牌显示癫狂面。放在右边，靠右的牌放左边。</summary>
        public void ShowPreview(bool on)
        {
            bool dual = CardLibrary.Get(DeckId.TrimEnd('+')).Dual;
            if (on && _preview == null && (_altId != null || dual))
            {
                _preview = Ui.Rect("Preview", transform);
                _preview.anchorMin = _preview.anchorMax = new Vector2(0.5f, 0.5f);
                var size = _size * 0.85f;
                _preview.sizeDelta = size;
                var face = CardFace.Create(_preview, size.x / CardFace.W);
                face.Apply(_altId != null ? RunUi.Face(_altId) : RunUi.Face(DeckId, true));
                var label = Ui.Text("Label", _preview, _altLabel ?? "癫狂面", 20, Palette.Pale);
                label.outlineWidth = 0.2f;
                label.outlineColor = Palette.Ink;
                label.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, 18), new Vector2(size.x, 30));
            }
            if (_preview == null) return;
            if (on)
            {
                var root = GetComponentInParent<UnityEngine.Canvas>().rootCanvas.transform as RectTransform;
                float x = root != null ? root.InverseTransformPoint(_rt.position).x : 0;
                float side = x > 300 ? -1 : 1;
                _preview.anchoredPosition = new Vector2(side * (_size.x / 2 + _size.x * 0.425f + 14), 0);
                _preview.SetAsLastSibling();
            }
            _preview.gameObject.SetActive(on);
        }
    }
}
