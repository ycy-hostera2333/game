using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deeptide.View
{
    /// <summary>
    /// 悬停提示框（策划/10 §6.5「提示框正文」）。每个界面根节点建一个，<see cref="TooltipTrigger"/> 悬停时显示在目标旁边。
    /// </summary>
    public sealed class Tooltip : MonoBehaviour
    {
        const float Width = 380;

        static Tooltip _current;
        RectTransform _root, _box;
        TextMeshProUGUI _text;
        RectTransform _owner;

        /// <summary>当前显示的文本（测试用）；没在显示时为 null。</summary>
        public static string Showing => _current != null && _current._box.gameObject.activeSelf ? _current._text.text : null;

        public static Tooltip Create(RectTransform root)
        {
            var host = Ui.Rect("Tooltip", root).Fill();
            var tip = host.gameObject.AddComponent<Tooltip>();
            tip._root = root;
            var box = Ui.Box("Box", host, Palette.Paper);
            // UI 套件：卷轴框；没画好时一道墨边
            if (ArtSprites.Frame(box.transform, "ui_scroll", ArtSprites.ScrollBorder, Palette.Ink, 2f, -4) == null) Ui.Border(box, Palette.Ink, 2f);
            tip._box = box.rectTransform;
            tip._box.anchorMin = tip._box.anchorMax = new Vector2(0.5f, 0.5f);
            tip._text = Ui.Text("Text", tip._box, "", 20, Palette.Text, TextAlignmentOptions.TopLeft);
            tip._text.rectTransform.Fill(12);
            tip._box.gameObject.SetActive(false);
            _current = tip;
            return tip;
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        public static void Show(RectTransform owner, string text)
        {
            if (_current == null || owner == null || string.IsNullOrEmpty(text)) return;
            _current.Place(owner, text);
        }

        public static void Hide(RectTransform owner)
        {
            if (_current == null || (_current._owner != owner && owner != null)) return;
            _current._owner = null;
            _current._box.gameObject.SetActive(false);
        }

        void Place(RectTransform owner, string text)
        {
            _owner = owner;
            _text.text = text;
            var size = _text.GetPreferredValues(text, Width - 24, 0);
            _box.sizeDelta = new Vector2(Width, size.y + 26);
            _box.SetAsLastSibling();
            transform.SetAsLastSibling();

            // 放在目标右侧，放不下就放左侧；上下夹在屏幕内
            var corners = new Vector3[4];
            owner.GetWorldCorners(corners);
            Vector2 min = _root.InverseTransformPoint(corners[0]), max = _root.InverseTransformPoint(corners[2]);
            var half = _root.rect.size / 2;
            float x = max.x + 12 + Width / 2;
            if (x + Width / 2 > half.x) x = min.x - 12 - Width / 2;
            float h = _box.sizeDelta.y;
            float y = Mathf.Clamp((min.y + max.y) / 2, -half.y + h / 2 + 8, half.y - h / 2 - 8);
            _box.anchoredPosition = new Vector2(Mathf.Clamp(x, -half.x + Width / 2, half.x - Width / 2), y);
            _box.gameObject.SetActive(true);
        }
    }

    /// <summary>挂在任意 UI 元素上：悬停时显示 Content 返回的文本（返回空就不显示）。</summary>
    public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Func<string> Content;

        public static TooltipTrigger Add(Graphic target, Func<string> content)
        {
            target.raycastTarget = true;
            // 不能用 ??：编辑器里 GetComponent 找不到时返回的是 Unity 的「假 null」
            var t = target.GetComponent<TooltipTrigger>();
            if (t == null) t = target.gameObject.AddComponent<TooltipTrigger>();
            t.Content = content;
            return t;
        }

        public void OnPointerEnter(PointerEventData e) => Tooltip.Show((RectTransform)transform, Content?.Invoke());
        public void OnPointerExit(PointerEventData e) => Tooltip.Hide((RectTransform)transform);
        void OnDisable() => Tooltip.Hide((RectTransform)transform);
    }
}
