using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View
{
    /// <summary>
    /// 单色精灵（策划/10 §11.3「图标：单通道墨 + alpha，由 Image 着色」）：图标、卡面框饰、UI 墨线、特效序列帧都是白色 + alpha 的 PNG，
    /// 颜色由 Image.color 从色板给，换色板不用重画。ArtDatabase 里只存贴图，这里按需做成精灵（可带九宫格边距）并缓存。
    /// 没有画好的返回 null，调用方继续用灰盒（几何 + 汉字）。
    /// </summary>
    public static class ArtSprites
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <param name="border">九宫格边距（像素，左 下 右 上）；全 0 表示不切片。</param>
        public static Sprite Get(string id, Vector4 border = default)
        {
            // 能力等没有图标的状态传 null 进来，直接走灰盒
            if (string.IsNullOrEmpty(id)) return null;
            string key = border == Vector4.zero ? id : $"{id}#{border}";
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = ArtDatabase.SpriteTexture(id);
            if (tex == null) return null;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = id;
            s.hideFlags = HideFlags.DontSave;
            Cache[key] = s;
            return s;
        }

        public static bool Has(string id) => ArtDatabase.SpriteTexture(id) != null;

        /// <summary>给 Image 换上精灵（有的话），返回是否换上了。九宫格边距非 0 时设成 Sliced。</summary>
        public static bool Apply(Image image, string id, Color color, Vector4 border = default, float pixelsPerUnitMultiplier = 1)
        {
            if (image == null) return false;
            var sprite = Get(id, border);
            if (sprite == null) return false;
            image.sprite = sprite;
            image.color = color;
            image.type = border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            return true;
        }

        public static void Invalidate() => Cache.Clear();

        // ------------------------------------------------------------------ 战斗 UI 套件（10 §12.1）：九宫格边框，边距和源图一致

        /// <summary>牌匾框（按钮、牌堆）：源图 160，四边 28。</summary>
        public static readonly Vector4 PlaqueBorder = new Vector4(28, 28, 28, 28);
        /// <summary>卷轴框（提示框）：源图 200，四边 30。</summary>
        public static readonly Vector4 ScrollBorder = new Vector4(30, 30, 30, 30);
        /// <summary>生命条框：源图 64 × 32，四边 12。</summary>
        public static readonly Vector4 BarBorder = new Vector4(12, 12, 12, 12);
        /// <summary>理智柱：源图 96 × 420，柱础 60、柱头 64，左右不切。</summary>
        public static readonly Vector4 GaugeBorder = new Vector4(0, 60, 0, 64);

        /// <summary>在 host 里铺一层九宫格边框（盖在内容上面，不挡点击）；没画好时返回 null。</summary>
        public static Image Frame(Transform host, string id, Vector4 border, Color color, float pixelsPerUnitMultiplier = 1, float pad = 0)
        {
            if (!Has(id)) return null;
            var img = Ui.Box(id, host, color);
            Apply(img, id, color, border, pixelsPerUnitMultiplier);
            img.rectTransform.Fill(pad);
            img.raycastTarget = false;
            return img;
        }
    }
}
