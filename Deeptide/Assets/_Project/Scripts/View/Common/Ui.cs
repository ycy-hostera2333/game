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

        /// <summary>给图形加一圈描边（uGUI Outline，灰盒用来画边框）。</summary>
        public static Outline Border(Graphic g, Color color, float width = 2)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(width, -width);
            return o;
        }

        /// <summary>颜色转成 TMP 富文本用的 #RRGGBBAA。</summary>
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);
    }

    /// <summary>
    /// 中文字体：灰盒阶段直接用系统字体动态生成 TMP 字体资源（AtlasPopulationMode.DynamicOS），字体文件不进仓库。
    /// 策划/10 §6.5：卡名、费用、数字、标题用宋体（Serif），正文用黑体（Main）。正式版换成随包的思源宋体 Heavy / 思源黑体 Medium。
    /// </summary>
    public static class Fonts
    {
        static readonly (string Family, string Style)[] SansCandidates =
        {
            ("Microsoft YaHei", "Regular"), ("Microsoft YaHei UI", "Regular"), ("SimHei", "Regular"),
            ("Noto Sans CJK SC", "Regular"), ("Source Han Sans SC", "Regular"), ("PingFang SC", "Regular"),
        };
        static readonly (string Family, string Style)[] SerifCandidates =
        {
            ("Noto Serif SC", "Bold"), ("Noto Serif SC", "Regular"), ("Source Han Serif SC", "Heavy"), ("Source Han Serif SC", "Bold"),
            ("Noto Serif CJK SC", "Bold"), ("STSong", "Regular"), ("SimSun", "Regular"),
        };
        static TMP_FontAsset _main, _serif;
        static bool _mainResolved, _serifResolved;

        public static TMP_FontAsset Main
        {
            get
            {
                if (_mainResolved) return _main;
                _mainResolved = true;
                _main = Find(SansCandidates);
                if (_main == null)
                {
                    _main = TMP_Settings.defaultFontAsset;
                    Debug.LogWarning("没有找到中文系统字体，中文会显示成方块");
                }
                return _main;
            }
        }

        /// <summary>
        /// 系统字体目录里按文件名找的宋体（思源宋体 / Noto Serif SC）。可变字体（NotoSerifSC-VF）按族名查不到，只能按文件加载。
        /// </summary>
        static readonly string[] SerifFiles =
        {
            "SourceHanSerifSC-Heavy.otf", "SourceHanSerifSC-Bold.otf", "NotoSerifSC-Bold.otf", "NotoSerifSC-VF.ttf", "NotoSerifSC-Regular.otf",
        };

        /// <summary>宋体；系统里没有时退回 Main。缺字（× − ◐ 等符号）落到 Main。</summary>
        public static TMP_FontAsset Serif
        {
            get
            {
                if (_serifResolved) return _serif;
                _serifResolved = true;
                _serif = FromFile(SerifFiles) ?? Find(SerifCandidates);
                if (_serif == null) return _serif = Main;
                if (Main != null && Main != _serif) _serif.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { Main };
                return _serif;
            }
        }

        static TMP_FontAsset FromFile(string[] names)
        {
            var dirs = new[]
            {
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts),
                System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"),
            };
            foreach (var name in names)
            foreach (var dir in dirs)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                string path = System.IO.Path.Combine(dir, name);
                if (!System.IO.File.Exists(path)) continue;
                var asset = TMP_FontAsset.CreateFontAsset(path, 0, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
                if (asset != null) return asset;
            }
            return null;
        }

        static TMP_FontAsset Find((string Family, string Style)[] candidates)
        {
            foreach (var (family, style) in candidates)
            {
                var asset = TMP_FontAsset.CreateFontAsset(family, style);
                if (asset != null) return asset;
            }
            return null;
        }
    }

    /// <summary>
    /// 策划/10 §2.1 色板。亮底 = 纸面（清醒态的卡、面板），暗底 = 深渊（癫狂面的卡、深色弹窗）。
    /// 深渊青只给理智、癫狂和深海之物用；金 / 血 / 恐惧紫 / 苔绿只用于数字、小图标、徽记（10 §2.2）。
    /// 上半部分是 10 的色板 token，下半部分是灰盒阶段留下的旧名字，映射到同一套色板。
    /// </summary>
    public static class Palette
    {
        // ---------------- 10 §2.1 色板
        public static readonly Color Paper = new Color32(0xE8, 0xE0, 0xCC, 0xFF);
        public static readonly Color PaperShade = new Color32(0xCF, 0xC6, 0xB0, 0xFF);
        /// <summary>纸和纸影之间，按钮、格子底用。</summary>
        public static readonly Color PaperMid = new Color32(0xDC, 0xD3, 0xBE, 0xFF);
        public static readonly Color Ink = new Color32(0x1C, 0x1A, 0x17, 0xFF);
        public static readonly Color InkMid = new Color32(0x4A, 0x45, 0x3C, 0xFF);
        /// <summary>深渊底（屏幕、理智条的癫狂段）。</summary>
        public static readonly Color Abyss = new Color32(0x0F, 0x1B, 0x1B, 0xFF);
        /// <summary>深渊底（癫狂面的卡面）。</summary>
        public static readonly Color AbyssCard = new Color32(0x13, 0x24, 0x23, 0xFF);
        /// <summary>月白：暗底上的正文。</summary>
        public static readonly Color Pale = new Color32(0xD8, 0xE4, 0xDF, 0xFF);
        /// <summary>深渊青（亮底）。</summary>
        public static readonly Color Teal = new Color32(0x1A, 0x61, 0x5E, 0xFF);
        /// <summary>深渊青（暗底的发光色）：理智条癫狂段点亮、癫狂面的线条。</summary>
        public static readonly Color Lunacy = new Color32(0x4F, 0xC2, 0xB0, 0xFF);
        /// <summary>清明金（亮底）：清明段描边、卡面「清明：」句、理智恢复、守夜徽记。</summary>
        public static readonly Color Gold = new Color32(0x7D, 0x59, 0x14, 0xFF);
        public static readonly Color GoldLight = new Color32(0xD9, 0xA8, 0x4A, 0xFF);
        /// <summary>血：生命、伤害、堕落徽记。</summary>
        public static readonly Color Blood = new Color32(0x8E, 0x2A, 0x2A, 0xFF);
        public static readonly Color BloodLight = new Color32(0xE0, 0x73, 0x6A, 0xFF);
        /// <summary>恐惧紫：恐惧、意志、传染徽记。</summary>
        public static readonly Color Dread = new Color32(0x5B, 0x4A, 0x6E, 0xFF);
        public static readonly Color DreadLight = new Color32(0xA8, 0x98, 0xC0, 0xFF);
        /// <summary>苔绿：只用于被增益修正过的卡面数字。</summary>
        public static readonly Color Moss = new Color32(0x3D, 0x6B, 0x2E, 0xFF);
        public static readonly Color MossLight = new Color32(0x8F, 0xC2, 0x7A, 0xFF);

        /// <summary>回响档位的底色和字色（策划/15 §7.2）：浅 = 浅灰纸、深 = 深青、渊 = 渊黑。</summary>
        public static (Color Back, Color Text) EchoTier(Deeptide.Core.Relics.EchoTier tier) =>
            tier == Deeptide.Core.Relics.EchoTier.Shallow ? (PaperMid, Ink) : tier == Deeptide.Core.Relics.EchoTier.Deep ? (Teal, Pale) : (AbyssCard, Pale);

        /// <summary>流派色（10 §4.2）：守夜 = 金，堕落 = 血，传染 = 紫，其余淡墨。</summary>
        public static Color Faction(string archetype, bool dark = false)
        {
            switch (archetype)
            {
                case "守夜": return dark ? GoldLight : Gold;
                case "堕落": return dark ? BloodLight : Blood;
                case "传染": return dark ? DreadLight : Dread;
                default: return dark ? Pale : InkMid;
            }
        }

        // ---------------- 旧名字（灰盒第一步），映射到上面的色板
        public static readonly Color Background = Paper;
        public static readonly Color Panel = PaperShade;
        public static readonly Color PanelLight = PaperMid;
        public static readonly Color Text = Ink;
        public static readonly Color TextDim = InkMid;
        public static readonly Color Accent = Teal;
        public static readonly Color AccentDark = AbyssCard;

        public static readonly Color Hp = Blood;
        public static readonly Color HpBack = new Color32(0xD8, 0xC9, 0xB6, 0xFF);
        public static readonly Color Block = Ink;
        public static readonly Color Energy = Ink;
        public static readonly Color Heal = Moss;
        public static readonly Color Bad = Blood;

        public static readonly Color Attack = Blood;
        public static readonly Color Skill = InkMid;
        public static readonly Color Power = Gold;
        public static readonly Color Curse = Dread;
        public static readonly Color Status = InkMid;
    }
}
