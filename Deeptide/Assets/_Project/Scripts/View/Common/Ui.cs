using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View
{
    /// <summary>
    /// 灰盒阶段用代码搭 UI：纯色矩形 + TMP 文本。坐标统一用 1920×1080 的参考分辨率。
    /// 换美术时再把这些结构做成预制体，表现逻辑不用动。
    /// </summary>
    public static class Ui
    {
        public const int Layer = 5;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = Layer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>锚在父节点的某一点上，pos 是相对锚点的偏移。</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>铺满父节点，四边留出 inset。</summary>
        public static RectTransform Fill(this RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Box(string name, Transform parent, Color color, bool raycast = false)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (Fonts.Main != null) t.font = Fonts.Main;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        public static Button TextButton(string name, Transform parent, string label, Color color, System.Action onClick)
        {
            var img = Box(name, parent, color, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            Text("Label", img.transform, label, 30, Palette.Text).rectTransform.Fill();
            return btn;
        }

        /// <summary>水平进度条：背景 + 前景，前景按比例缩短（不依赖精灵，灰盒够用）。</summary>
        public static RectTransform Bar(string name, Transform parent, Color back, Color fore, out RectTransform fill)
        {
            var bg = Box(name, parent, back).rectTransform;
            fill = Box("Fill", bg, fore).rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            return bg;
        }

        public static void SetFill(RectTransform fill, float ratio)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1);
        }
    }

    /// <summary>
    /// 中文字体：灰盒阶段直接用系统字体动态生成 TMP 字体资源（AtlasPopulationMode.DynamicOS），
    /// 字体文件不进仓库。正式版按 策划/10 换成思源宋体 / 思源黑体。
    /// </summary>
    public static class Fonts
    {
        static readonly string[] Candidates = { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "Noto Sans CJK SC", "Source Han Sans SC", "PingFang SC" };
        static TMP_FontAsset _main;
        static bool _resolved;

        public static TMP_FontAsset Main
        {
            get
            {
                if (_resolved) return _main;
                _resolved = true;
                foreach (var family in Candidates)
                {
                    _main = TMP_FontAsset.CreateFontAsset(family, "Regular");
                    if (_main != null) return _main;
                }
                _main = TMP_Settings.defaultFontAsset;
                Debug.LogWarning("没有找到中文系统字体，中文会显示成方块");
                return _main;
            }
        }
    }

    /// <summary>灰盒配色。强调色用深海青绿（策划/10），只给理智和癫狂相关的元素用。</summary>
    public static class Palette
    {
        public static readonly Color Background = new Color32(0x0E, 0x13, 0x17, 0xFF);
        public static readonly Color Panel = new Color32(0x1C, 0x24, 0x2A, 0xFF);
        public static readonly Color PanelLight = new Color32(0x2C, 0x36, 0x3D, 0xFF);
        public static readonly Color Text = new Color32(0xE8, 0xE2, 0xD6, 0xFF);
        public static readonly Color TextDim = new Color32(0x9A, 0x96, 0x8C, 0xFF);
        public static readonly Color Accent = new Color32(0x2F, 0xB8, 0xA6, 0xFF);
        public static readonly Color AccentDark = new Color32(0x15, 0x5E, 0x56, 0xFF);

        public static readonly Color Hp = new Color32(0xB0, 0x3A, 0x2E, 0xFF);
        public static readonly Color HpBack = new Color32(0x3A, 0x1A, 0x17, 0xFF);
        public static readonly Color Block = new Color32(0x4F, 0x7C, 0xB8, 0xFF);
        public static readonly Color Energy = new Color32(0xD9, 0xA4, 0x41, 0xFF);
        public static readonly Color Heal = new Color32(0x6B, 0xC0, 0x6B, 0xFF);
        public static readonly Color Bad = new Color32(0xE0, 0x55, 0x48, 0xFF);

        public static readonly Color Attack = new Color32(0x6E, 0x2B, 0x26, 0xFF);
        public static readonly Color Skill = new Color32(0x2A, 0x45, 0x5C, 0xFF);
        public static readonly Color Power = new Color32(0x6A, 0x56, 0x24, 0xFF);
        public static readonly Color Curse = new Color32(0x3A, 0x2A, 0x45, 0xFF);
        public static readonly Color Status = new Color32(0x44, 0x44, 0x44, 0xFF);
    }
}
