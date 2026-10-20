using UnityEngine;

namespace Deeptide.View
{
    /// <summary>
    /// 打包贴图（策划/10 §11.2）：画师交分层 PNG，这里把它们压成一张 RGBA，颜色全部交给着色器（Deeptide/UI/Etching）。
    /// <list type="bullet">
    /// <item>R = 墨（必需，<c>_ink</c> 层）</item>
    /// <item>G = 深渊遮罩，着深渊青（可选，<c>_abyss</c> 层）</item>
    /// <item>B = 癫狂层的墨，只在癫狂面显现（可选，<c>_mad</c> 层）</item>
    /// <item>A = 本体轮廓（可选，<c>_fill</c> 层；卡图这种不透明素材没有这一层，恒为 1）</item>
    /// </list>
    /// 每一层都按「墨的覆盖率」读：覆盖率 = alpha × (1 − 亮度)。所以透明底上的黑线、白底上的黑线、灰色排线都能直接用。
    /// 放在运行时程序集里，编辑器工具（ArtPacker）和 PlayMode 测试共用同一套规则。
    /// </summary>
    public static class CardArt
    {
        /// <summary>卡图（插图窗）的导出尺寸，10 §11.1。</summary>
        public const int Width = 624, Height = 432;

        public static float Coverage(Color32 c) =>
            c.a / 255f * (1f - (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f);

        /// <summary>把同尺寸的各层压成一张。abyss / mad / fill 可以为 null。</summary>
        public static Color32[] Pack(Color32[] ink, Color32[] abyss, Color32[] mad, Color32[] fill)
        {
            var o = new Color32[ink.Length];
            for (int i = 0; i < ink.Length; i++)
            {
                float r = Coverage(ink[i]);
                float g = abyss != null ? Coverage(abyss[i]) : 0;
                float b = mad != null ? Coverage(mad[i]) : 0;
                float a = fill != null ? Mathf.Max(fill[i].a / 255f, ink[i].a / 255f) : 1;
                o[i] = new Color32(Byte(r), Byte(g), Byte(b), Byte(a));
            }
            return o;
        }

        /// <summary>运行时直接生成打包贴图（测试用）。打包贴图是数据不是颜色，所以是线性贴图。</summary>
        public static Texture2D ToTexture(Color32[] packed, int width, int height, string name = "card_art")
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, true, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            tex.SetPixels32(packed);
            tex.Apply(true);
            return tex;
        }

        static byte Byte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255);

        // ------------------------------------------------------------------ 卡面用的两套材质

        static Material _sane, _mad;

        /// <summary>
        /// 清醒面：纸底墨线，深渊遮罩着深渊青，癫狂层不显示。
        /// 癫狂面（10 §4.3）：反相，纸 → 深渊底，墨 → 发光青，深渊遮罩 → 月白，癫狂层显现。
        /// 两套材质全局共用；插图窗的遮罩（Mask）会自动派生带模板测试的版本。
        /// </summary>
        public static Material For(bool madFace)
        {
            if (madFace) return _mad != null ? _mad : _mad = Make(Palette.AbyssCard, Palette.Lunacy, Palette.Pale, Palette.Lunacy, 1);
            return _sane != null ? _sane : _sane = Make(Palette.Paper, Palette.Ink, Palette.Teal, Palette.Ink, 0);
        }

        static Material Make(Color paper, Color ink, Color abyss, Color madInk, float reveal)
        {
            var shader = Shader.Find("Deeptide/UI/Etching");
            if (shader == null)
            {
                Debug.LogWarning("找不到着色器 Deeptide/UI/Etching，卡图按原始通道显示");
                return null;
            }
            var m = new Material(shader) { hideFlags = HideFlags.DontSave, name = reveal > 0 ? "CardArt (mad)" : "CardArt (sane)" };
            m.SetColor("_PaperColor", paper);
            m.SetColor("_InkColor", ink);
            m.SetColor("_AbyssColor", abyss);
            m.SetColor("_MadColor", madInk);
            m.SetFloat("_Reveal", reveal);
            return m;
        }
    }
}
