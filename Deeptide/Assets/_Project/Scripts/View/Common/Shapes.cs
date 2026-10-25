using System.Collections.Generic;
using UnityEngine;

namespace Deeptide.View
{
    /// <summary>
    /// 灰盒阶段的程序化图形：用有向距离场在运行时画出白色 + alpha 的精灵，颜色由 Image.color 决定。
    /// 不进仓库、不用美术资源；正式美术到位后，这里的形状由 策划/10 §11 的贴图替换。
    /// 所有精灵第一次用到时生成并缓存。
    /// </summary>
    public static class Shapes
    {
        delegate float Sdf(float x, float y);

        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        // ------------------------------------------------------------------ 基础形状

        public static Sprite Circle => Get("circle", 256, 256, (x, y) => Len(x - 128, y - 128) - 126);

        /// <summary>圆环；stroke 是相对直径 256 的线宽（像素）。</summary>
        public static Sprite Ring(int stroke) =>
            Get("ring" + stroke, 256, 256, (x, y) => Stroke(Len(x - 128, y - 128) - 126, stroke));

        /// <summary>圆角矩形（九宫格）。radius、stroke 以像素计；Image.type = Sliced 时圆角和线宽不随尺寸变化。stroke = 0 为实心。</summary>
        public static Sprite RoundRect(int radius, int stroke = 0)
        {
            int size = radius * 2 + 4;
            int border = radius + 2;
            return Get($"rr{radius}_{stroke}", size, size,
                (x, y) => Shape(RoundBox(x - size / 2f, y - size / 2f, size / 2f - 1, size / 2f - 1, radius), stroke),
                new Vector4(border, border, border, border));
        }

        /// <summary>虚线圆角矩形边框（九宫格）。</summary>
        public static Sprite DashedRect(int radius, int stroke, int dash)
        {
            int size = radius * 2 + dash * 4;
            int border = radius + dash * 2 - 1;
            return Get($"dash{radius}_{stroke}_{dash}", size, size, (x, y) =>
            {
                float d = Stroke(RoundBox(x - size / 2f, y - size / 2f, size / 2f - 1, size / 2f - 1, radius), stroke);
                // 沿边方向的坐标取 x + y：横边上随 x 变，竖边上随 y 变，都能断成虚线
                float phase = Mathf.Repeat(x + y, dash * 2f);
                return phase < dash ? d : Mathf.Max(d, 0.6f);
            }, new Vector4(border, border, border, border));
        }

        /// <summary>斜排线平铺图（135°，和纸面原型印章框一样），配 Image.type = Tiled 或 RawImage.uvRect 平铺。</summary>
        public static Sprite Hatch => Get("hatch", 32, 32, (x, y) =>
        {
            float t = Mathf.Repeat(x + y, 16f);
            return Mathf.Min(Mathf.Abs(t - 4f), Mathf.Abs(t - 20f)) - 3f;
        }, wrap: TextureWrapMode.Repeat);

        /// <summary>横向短划，平铺成虚线（理智条的癫狂线）。</summary>
        public static Sprite Dash => Get("dashline", 24, 8, (x, y) => Mathf.Max(Mathf.Abs(x - 8) - 6, Mathf.Abs(y - 4) - 2.5f),
            wrap: TextureWrapMode.Repeat);

        /// <summary>横向圆点，平铺成点线（地图上没走过的路，10 §8）。</summary>
        public static Sprite Dots => Get("dotline", 16, 8, (x, y) => Len(x - 6, y - 4) - 2.8f, wrap: TextureWrapMode.Repeat);

        /// <summary>水面的波峰（横向平铺）：曲线以下实心。潮水的上沿用。</summary>
        public static Sprite Wave => Get("wave", 64, 24, (x, y) => y - (13 + 6 * Mathf.Sin(x / 64f * Mathf.PI * 2)),
            wrap: TextureWrapMode.Repeat);

        public static Sprite Diamond => Get("diamond", 128, 128, (x, y) => (Mathf.Abs(x - 64) + Mathf.Abs(y - 64) - 60) * 0.7071f);

        /// <summary>盾形：上方圆角，下方收成尖。</summary>
        public static Sprite Shield => Get("shield", 128, 144, (x, y) =>
        {
            float px = x - 64, py = y - 80;
            float box = RoundBox(px, py, 52, 56, 14);
            float v = (Mathf.Abs(px) * 0.83f - (py + 62)) * 0.77f;
            return Mathf.Max(box, v);
        });

        /// <summary>墨渍：边缘起伏的圆。癫狂时盖住意图数值，也用作诅咒牌的破窗。</summary>
        public static Sprite Blot => Get("blot", 160, 128, (x, y) =>
        {
            float px = (x - 80) / 1.25f, py = y - 64;
            float a = Mathf.Atan2(py, px);
            float r = 50 * (1 + 0.13f * Mathf.Sin(5 * a + 1) + 0.07f * Mathf.Sin(11 * a + 2) + 0.05f * Mathf.Sin(17 * a + 4));
            float d = Len(px, py) - r;
            // 两滴溅出去的小墨点
            d = Mathf.Min(d, Len(x - 146, y - 98) - 7);
            d = Mathf.Min(d, Len(x - 18, y - 30) - 5);
            return d;
        });

        // ------------------------------------------------------------------ 卡面插图窗（10 §4.2：攻击 = 尖拱 / 技能 = 倒角方窗 / 能力 = 圆窗）

        /// <summary>插图窗，按卡面 624 × 432 的一半尺寸生成。stroke 以生成尺寸计（卡面上是两倍）。</summary>
        public static Sprite Window(WindowShape shape, int stroke = 0)
        {
            const int w = 312, h = 216;
            switch (shape)
            {
                case WindowShape.Arch:
                    return Get("win_arch" + stroke, w, h, (x, y) => Shape(Arch(x, y, w, h), stroke));
                case WindowShape.Circle:
                    return Get("win_circle" + stroke, h, h, (x, y) => Shape(Len(x - h / 2f, y - h / 2f) - (h / 2f - 2), stroke));
                case WindowShape.Blot:
                    return Get("win_blot" + stroke, w, h, (x, y) =>
                    {
                        float px = (x - w / 2f) / 1.4f, py = y - h / 2f;
                        float a = Mathf.Atan2(py, px);
                        float r = 92 * (1 + 0.08f * Mathf.Sin(6 * a + 0.5f) + 0.05f * Mathf.Sin(13 * a + 2) + 0.03f * Mathf.Sin(23 * a));
                        return Shape(Len(px, py) - r, stroke);
                    });
                default:
                    return Get("win_bevel" + stroke, w, h, (x, y) =>
                    {
                        float px = Mathf.Abs(x - w / 2f), py = Mathf.Abs(y - h / 2f);
                        float box = Mathf.Max(px - (w / 2f - 2), py - (h / 2f - 2));
                        float corner = (px + py - (w / 2f + h / 2f - 26)) * 0.7071f;
                        return Shape(Mathf.Max(box, corner), stroke);
                    });
            }
        }

        /// <summary>尖拱：直墙 + 两段弧收成的尖顶（四心拱的近似）。</summary>
        static float Arch(float x, float y, float w, float h)
        {
            const float pad = 2;
            float half = w / 2f - pad;
            float spring = h * 0.62f;
            float px = Mathf.Abs(x - w / 2f);
            float u = Mathf.Clamp01(px / half);
            float rise = h - pad - spring;
            float top = spring + rise * (1 - Mathf.Pow(u, 1.5f));
            float slope = rise * 1.5f * Mathf.Sqrt(u) / half;
            float dTop = (y - top) / Mathf.Sqrt(1 + slope * slope);
            float dSide = px - half;
            float dBottom = pad - y;
            return Mathf.Max(Mathf.Max(dTop, dSide), dBottom);
        }

        // ------------------------------------------------------------------ 剪影（10 §10：敌人用纯墨剪影占位）

        /// <summary>剪影精灵；脚底在精灵底边（pivot 在底边中点）。四周留出空白，给色差和波纹留位置。</summary>
        public static Sprite Silhouette(SilhouetteKind kind)
        {
            switch (kind)
            {
                case SilhouetteKind.Beast:
                    return Get("sil_beast", 320, 176, (x, y) =>
                    {
                        float body = Ellipse(x - 176, y - 70, 112, 54);
                        float head = Len(x - 70, y - 86) - 40;
                        float ear = Len(x - 84, y - 128) - 13;
                        float tail = Capsule(x, y, 280, 48, 312, 92, 6);
                        float legs = Mathf.Min(Capsule(x, y, 130, 40, 124, 12, 10), Capsule(x, y, 226, 40, 232, 12, 10));
                        return SmoothMin(SmoothMin(SmoothMin(body, head, 14), Mathf.Min(ear, tail), 6), legs, 8);
                    }, pivot: new Vector2(0.5f, 12f / 176));
                case SilhouetteKind.Tentacle:
                    return Get("sil_tentacle", 128, 384, (x, y) =>
                    {
                        float t = Mathf.Clamp01((y - 8) / 360f);
                        float cx = 64 + 22 * Mathf.Sin(t * 4.2f) * t;
                        float r = 34 * Mathf.Pow(1 - t, 0.85f) + 4;
                        float d = Mathf.Abs(x - cx) - r;
                        return Mathf.Max(d, Mathf.Max(8 - y, y - 372));
                    }, pivot: new Vector2(0.5f, 8f / 384));
                case SilhouetteKind.Swarm:
                    return Get("sil_swarm", 320, 256, (x, y) =>
                    {
                        float d = Ellipse(x - 150, y - 90, 70, 52);
                        d = SmoothMin(d, Ellipse(x - 92, y - 150, 48, 36), 10);
                        d = SmoothMin(d, Ellipse(x - 210, y - 160, 54, 40), 10);
                        d = SmoothMin(d, Ellipse(x - 236, y - 72, 40, 30), 10);
                        d = Mathf.Min(d, Ellipse(x - 160, y - 214, 36, 22));
                        d = Mathf.Min(d, Ellipse(x - 58, y - 64, 30, 20));
                        return d;
                    }, pivot: new Vector2(0.5f, 30f / 256));
                default:
                    return Get("sil_humanoid", 192, 384, (x, y) =>
                    {
                        float head = Len(x - 96, y - 318) - 36;
                        float body = Trapezoid(x - 96, y - 150, 74, 58, 138);
                        float neck = Capsule(x, y, 96, 270, 96, 300, 16);
                        return SmoothMin(SmoothMin(body, neck, 10), head, 10);
                    }, pivot: new Vector2(0.5f, 12f / 384));
            }
        }

        // ------------------------------------------------------------------ 生成

        static Sprite Get(string key, int w, int h, Sdf sdf, Vector4 border = default, Vector2? pivot = null,
            TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            if (Sprites.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = Bake(key, w, h, sdf, wrap);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
            sprite.name = key;
            sprite.hideFlags = HideFlags.DontSave;
            Sprites[key] = sprite;
            return sprite;
        }

        /// <summary>白色 + 抗锯齿 alpha 的贴图（RawImage 平铺用）。</summary>
        public static Texture2D HatchTexture => Bake("hatch_tex", 32, 32, (x, y) =>
        {
            float t = Mathf.Repeat(x + y, 16f);
            return Mathf.Min(Mathf.Abs(t - 4f), Mathf.Abs(t - 20f)) - 3f;
        }, TextureWrapMode.Repeat);

        static Texture2D Bake(string key, int w, int h, Sdf sdf, TextureWrapMode wrap)
        {
            if (Textures.TryGetValue(key, out var cached) && cached != null) return cached;
            // 带 mipmap、三线性：卡面整体缩到 1/3 显示在手牌里，不带 mip 的话细边框会采成锯齿
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = key,
                wrapMode = wrap,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f));
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            Textures[key] = tex;
            return tex;
        }

        // ------------------------------------------------------------------ 距离场工具

        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        /// <summary>把实心距离场变成向内的描边。</summary>
        static float Stroke(float d, float stroke) => Mathf.Abs(d + stroke * 0.5f) - stroke * 0.5f;

        static float Shape(float d, int stroke) => stroke > 0 ? Stroke(d, stroke) : d;

        static float RoundBox(float x, float y, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x) - hx + r, qy = Mathf.Abs(y) - hy + r;
            return Len(Mathf.Max(qx, 0), Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        static float Ellipse(float x, float y, float rx, float ry)
        {
            float k = Len(x / rx, y / ry);
            return (k - 1) * Mathf.Min(rx, ry);
        }

        static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            return Len(pax - bax * h, pay - bay * h) - r;
        }

        /// <summary>等腰梯形：上底半宽 top、下底半宽 bottom、半高 hh。</summary>
        static float Trapezoid(float x, float y, float top, float bottom, float hh)
        {
            x = Mathf.Abs(x);
            float halfW = Mathf.Lerp(bottom, top, Mathf.Clamp01((y + hh) / (2 * hh)));
            float side = (x - halfW) / Mathf.Sqrt(1 + Mathf.Pow((bottom - top) / (2 * hh), 2));
            return Mathf.Max(side, Mathf.Abs(y) - hh);
        }

        static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1 - h);
        }
    }

    public enum WindowShape { Bevel, Arch, Circle, Blot }

    public enum SilhouetteKind { Humanoid, Beast, Tentacle, Swarm }
}
