using System.Collections.Generic;
using UnityEngine;

namespace Deeptide.View
{
    /// <summary>
    /// 角色图（策划/10 §5、§11.2）：和卡图一样的打包贴图，A 通道是轮廓。画在 Deeptide/UI/Stage 着色器的打包模式里，
    /// 颜色来自色板：纸、墨、深渊青；癫狂层按全局 _Lunacy 显现，用发光青（和灰盒里癫狂时亮起的青眼一致）。
    /// 约定：脚底在图的底边往上 <see cref="FeetPad"/> 处，左右居中；敌人面朝左，调查员面朝右（10 §3）。
    /// </summary>
    public static class ActorArt
    {
        /// <summary>脚底离图底边的距离（占图高的比例）。</summary>
        public const float FeetPad = 0.04f;

        static readonly Dictionary<Texture2D, Sprite> Sprites = new Dictionary<Texture2D, Sprite>();

        public static Sprite Sprite(Texture2D tex)
        {
            if (Sprites.TryGetValue(tex, out var s) && s != null) return s;
            s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, FeetPad), 100f, 0,
                SpriteMeshType.FullRect);
            s.name = tex.name;
            s.hideFlags = HideFlags.DontSave;
            Sprites[tex] = s;
            return s;
        }

        /// <summary>把 Stage 材质切到打包模式，设好色板。</summary>
        public static void Configure(Material m)
        {
            m.SetFloat("_Packed", 1);
            m.SetColor("_PaperColor", Palette.Paper);
            m.SetColor("_InkColor", Palette.Ink);
            m.SetColor("_AbyssColor", Palette.Teal);
            m.SetColor("_MadColor", Palette.Lunacy);
        }
    }
}
