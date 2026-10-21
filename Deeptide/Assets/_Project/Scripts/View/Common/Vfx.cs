using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View
{
    /// <summary>
    /// 序列帧特效（策划/10 §9）：一组单色精灵 &lt;id&gt;_0 … &lt;id&gt;_N，按 12 帧 / 秒（「一拍二」）播一遍，播完自己销毁。
    /// 帧图由 ArtDatabase 的单色精灵表提供，颜色由调用方给（命中的墨点是墨色、格挡碎片是墨色）。
    /// 没画好（第 0 帧不存在）时什么都不做，返回 false。
    /// </summary>
    public sealed class Vfx : MonoBehaviour
    {
        public const float Fps = 12;

        Image _image;
        string _id;
        int _frames, _frame = -1;
        float _t;

        /// <summary>在 parent 下 position 处播一组帧。</summary>
        public static bool Play(RectTransform parent, Vector2 position, string id, int frames, float size, Color color, float rotation = 0)
        {
            if (parent == null || !ArtSprites.Has($"{id}_0")) return false;
            var img = Ui.Box($"Vfx {id}", parent, color);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0, 0, rotation);
            var fx = img.gameObject.AddComponent<Vfx>();
            fx._image = img;
            fx._id = id;
            fx._frames = frames;
            fx.Step();
            return true;
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t * Fps >= _frame + 1) Step();
        }

        void Step()
        {
            _frame++;
            if (_frame >= _frames)
            {
                Destroy(gameObject);
                return;
            }
            var sprite = ArtSprites.Get($"{_id}_{_frame}");
            if (sprite == null)
            {
                Destroy(gameObject);
                return;
            }
            _image.sprite = sprite;
        }
    }
}
