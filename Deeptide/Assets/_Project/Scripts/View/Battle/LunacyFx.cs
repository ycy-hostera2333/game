using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 癫狂表现（策划/10 §7）：把理智换算成全局 _Lunacy（0–1），驱动暗角、色差、波纹、背景癫狂层；崩溃时反相 + 裂纹 + 震屏。
    /// 全部效果只放在战斗舞台这一层（背景、角色），卡牌和数字所在的 UI 层不受影响（10 §1.2 支柱 4）。
    /// 10 §11.4 原写的是 URP Volume 后处理；灰盒阶段界面和舞台在同一张画布上，后处理会连 UI 一起扭曲，
    /// 所以改成舞台元素自己的着色器（Deeptide/UI/Stage）+ 一层暗角图，效果和分级不变。
    /// </summary>
    public sealed class LunacyFx : MonoBehaviour
    {
        static readonly int LunacyId = Shader.PropertyToID("_Lunacy");
        static readonly int ReduceId = Shader.PropertyToID("_ReduceFx");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int WashId = Shader.PropertyToID("_Wash");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");

        public static LunacyFx Instance { get; private set; }

        /// <summary>当前（补间中的）癫狂强度，0 = 清醒。</summary>
        public static float Current => Instance != null ? Instance._current : 0;

        /// <summary>设置里的「降低癫狂特效」（10 §7.4）：强度 × 0.3，关掉波纹和崩溃反相。</summary>
        public static bool Reduced
        {
            get => _reduced;
            set
            {
                _reduced = value;
                Shader.SetGlobalFloat(ReduceId, value ? 1 : 0);
            }
        }
        static bool _reduced;

        float _current, _target, _pulse;
        bool _clarity;
        Image _vignette, _flash, _crack, _background;
        Material _vignetteMat, _flashMat;
        readonly List<Image> _shadows = new List<Image>();
        readonly List<float> _shadowRot = new List<float>();
        readonly List<RectTransform> _shakeTargets = new List<RectTransform>();
        readonly List<Vector2> _shakeHome = new List<Vector2>();

        /// <summary>
        /// 理智 → 强度（10 §7.2）：理智高于癫狂线为 0；癫狂线上 0.4，每低 1 点 +0.15（4 → 0.4，3 → 0.55，2 → 0.7，1 → 0.85）。
        /// </summary>
        public static float ForSanity(int sanity, int lunacyLine)
        {
            if (sanity > lunacyLine) return 0;
            return Mathf.Clamp01(0.4f + (lunacyLine - Mathf.Max(sanity, 1)) * 0.15f);
        }

        // ------------------------------------------------------------------ 搭建（由 BattleScreen 按层级分别调用）

        /// <summary>暗角层：放在战斗舞台之上、手牌之下。</summary>
        public static LunacyFx Create(Transform parent)
        {
            var rt = Ui.Rect("LunacyFx", parent).Fill();
            var fx = rt.gameObject.AddComponent<LunacyFx>();
            Instance = fx;
            Shader.SetGlobalFloat(LunacyId, 0);
            Shader.SetGlobalFloat(ReduceId, _reduced ? 1 : 0);

            var shader = Shader.Find("Deeptide/UI/InkVignette");
            fx._vignette = Ui.Box("Vignette", rt, Color.white);
            fx._vignette.rectTransform.Fill();
            if (shader != null)
            {
                fx._vignetteMat = new Material(shader) { hideFlags = HideFlags.DontSave };
                fx._vignetteMat.SetColor("_InkColor", Palette.Ink);
                fx._vignetteMat.SetColor("_WashColor", Palette.Teal);
                fx._vignette.material = fx._vignetteMat;
            }
            else
            {
                Debug.LogWarning("找不到着色器 Deeptide/UI/InkVignette，癫狂暗角不显示");
                fx._vignette.enabled = false;
            }
            return fx;
        }

        /// <summary>背景癫狂层：几条只在癫狂时显现的触须影子（10 §1.2 支柱 3），放在背景之上、角色之下。</summary>
        public void BuildBackdrop(Transform parent)
        {
            var layer = Ui.Rect("LunacyBackdrop", parent).Fill();
            layer.pivot = new Vector2(0.5f, 0);
            var spots = new[]
            {
                (new Vector2(-150, 380), 620f, -8f), (new Vector2(560, 360), 720f, 6f),
                (new Vector2(880, 300), 560f, 14f), (new Vector2(-900, 420), 520f, -16f),
            };
            foreach (var (pos, height, rot) in spots)
            {
                var img = Ui.Box("Shadow", layer, Palette.InkMid);
                img.sprite = Shapes.Silhouette(SilhouetteKind.Tentacle);
                var r = img.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0);
                r.pivot = new Vector2(0.5f, 0.02f);
                r.anchoredPosition = pos;
                r.sizeDelta = new Vector2(height / 3f, height);
                r.localRotation = Quaternion.Euler(0, 0, rot);
                img.color = new Color(Palette.InkMid.r, Palette.InkMid.g, Palette.InkMid.b, 0);
                _shadows.Add(img);
                _shadowRot.Add(rot);
            }
        }

        /// <summary>崩溃用的反相层和裂纹层：放在最上面。</summary>
        public void BuildOverlay(Transform parent)
        {
            _crack = Ui.Box("Crack", parent, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0));
            _crack.rectTransform.Fill();
            _crack.sprite = CrackSprite();
            _crack.gameObject.SetActive(false);

            _flash = Ui.Box("Invert", parent, new Color(1, 1, 1, 0));
            _flash.rectTransform.Fill();
            var shader = Shader.Find("Deeptide/UI/Invert");
            if (shader != null)
            {
                _flashMat = new Material(shader) { hideFlags = HideFlags.DontSave };
                _flash.material = _flashMat;
            }
            _flash.gameObject.SetActive(false);
        }

        /// <summary>清明时纸色略微提亮（10 §7.2 第一行）。</summary>
        public void SetBackground(Image background) => _background = background;

        /// <summary>崩溃时一起震的节点（战场、手牌）。</summary>
        public void AddShakeTarget(RectTransform target)
        {
            if (target == null) return;
            _shakeTargets.Add(target);
            _shakeHome.Add(target.anchoredPosition);
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Shader.SetGlobalFloat(LunacyId, 0);
            }
            if (_vignetteMat != null) Destroy(_vignetteMat);
            if (_flashMat != null) Destroy(_flashMat);
        }

        // ------------------------------------------------------------------ 驱动

        public void SetSanity(int sanity, int max, int lunacyLine, int clarityMargin)
        {
            float target = ForSanity(sanity, lunacyLine);
            if (_target <= 0 && target > 0) _pulse = 1;   // 刚进入癫狂：墨从四角涌进来，再退到这一档的强度
            _target = target;
            _clarity = sanity >= max - clarityMargin;
        }

        /// <summary>测试和截图用：直接跳到目标强度，不等补间。</summary>
        public void Settle()
        {
            _current = _target;
            _pulse = 0;
            Apply();
        }

        void Update()
        {
            _current = Mathf.MoveTowards(_current, _target, Time.deltaTime * 1.2f);
            _pulse = Mathf.MoveTowards(_pulse, 0, Time.deltaTime / 0.6f);
            Apply();
        }

        void Apply()
        {
            Shader.SetGlobalFloat(LunacyId, _current);
            float scale = _reduced ? 0.3f : 1f;

            if (_vignetteMat != null)
            {
                // 10 §7.2：理智 4 / 3 / 2 / 1 的暗角 0.25 / 0.32 / 0.40 / 0.48
                float intensity = _current <= 0.001f ? 0 : 0.25f + (_current - 0.4f) * (0.23f / 0.45f);
                intensity = Mathf.Max(0, intensity + _pulse * 0.22f * Mathf.Min(1, _target * 3)) * scale;
                _vignetteMat.SetFloat(IntensityId, intensity);
                _vignetteMat.SetFloat(WashId, 0.07f * _current * scale);
                var rect = _vignette.rectTransform.rect;
                if (rect.height > 0) _vignetteMat.SetFloat(AspectId, rect.width / rect.height);
            }

            float shadowAlpha = Mathf.Clamp01((_current - 0.3f) / 0.55f) * 0.2f * (_reduced ? 0.5f : 1f);
            float sway = _current >= 0.84f && !_reduced ? Mathf.Sin(Time.time * 0.7f) * 3f : 0;
            for (int i = 0; i < _shadows.Count; i++)
            {
                var c = _shadows[i].color;
                c.a = shadowAlpha;
                _shadows[i].color = c;
                _shadows[i].rectTransform.localRotation = Quaternion.Euler(0, 0, _shadowRot[i] + sway * (i % 2 == 0 ? 1 : -1));
            }

            if (_background != null)
                _background.color = _clarity && _current <= 0 ? Color.Lerp(Palette.Paper, Color.white, 0.12f) : Palette.Paper;
        }

        // ------------------------------------------------------------------ 崩溃

        /// <summary>崩溃（10 §7.2）：0.08 秒全屏反相 + 裂纹 0.6 秒淡出 + 震屏 12px。</summary>
        public void Collapse()
        {
            if (!_reduced && _flash != null && _flashMat != null)
            {
                _flash.gameObject.SetActive(true);
                _flash.color = new Color(1, 1, 1, 1);
                Tween.Delay(this, 0.08f, fx => fx._flash.gameObject.SetActive(false), warnIfTargetDestroyed: false);
            }
            if (_crack != null)
            {
                _crack.gameObject.SetActive(true);
                Tween.StopAll(_crack);
                _crack.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.85f);
                // 纯视觉收尾：重开 / 换对局时界面可能已经销毁，不需要报警
                Tween.Alpha(_crack, 0, 0.6f, startDelay: 0.08f).OnComplete(_crack, c => c.gameObject.SetActive(false), warnIfTargetDestroyed: false);
            }
            for (int i = 0; i < _shakeTargets.Count; i++)
            {
                var t = _shakeTargets[i];
                if (t == null) continue;
                // 震之前放回原位（铺满父节点的 RectTransform，localPosition 不是 0，只能还原 anchoredPosition）
                Tween.StopAll(t);
                t.anchoredPosition = _shakeHome[i];
                Tween.ShakeLocalPosition(t, new Vector3(12, 12, 0) * (_reduced ? 0.4f : 1f), 0.3f, 25);
            }
        }

        static Sprite _crackSprite;

        /// <summary>程序化的裂纹：从画面中心偏上的一点向外的几条折线，带分叉。</summary>
        static Sprite CrackSprite()
        {
            if (_crackSprite != null) return _crackSprite;
            const int w = 960, h = 540;
            var alpha = new float[w * h];
            var rng = new System.Random(7);
            var origin = new Vector2(w * 0.48f, h * 0.58f);
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2 / 9 + (float)rng.NextDouble() * 0.5f;
                Walk(alpha, w, h, origin, angle, 3.2f, rng, 0);
            }
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha[i]) * 255));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "crack", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _crackSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100);
            _crackSprite.hideFlags = HideFlags.DontSave;
            return _crackSprite;
        }

        static void Walk(float[] alpha, int w, int h, Vector2 p, float angle, float width, System.Random rng, int depth)
        {
            while (width > 0.6f && p.x >= 0 && p.x < w && p.y >= 0 && p.y < h)
            {
                float len = 18 + (float)rng.NextDouble() * 34;
                angle += ((float)rng.NextDouble() - 0.5f) * 0.9f;
                var next = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * len;
                Stamp(alpha, w, h, p, next, width);
                if (depth < 2 && rng.NextDouble() < 0.18)
                    Walk(alpha, w, h, next, angle + (rng.NextDouble() < 0.5 ? -0.8f : 0.8f), width * 0.6f, rng, depth + 1);
                p = next;
                width *= 0.93f;
            }
        }

        static void Stamp(float[] alpha, int w, int h, Vector2 a, Vector2 b, float width)
        {
            float length = Vector2.Distance(a, b);
            int steps = Mathf.CeilToInt(length * 2);
            int r = Mathf.CeilToInt(width + 1);
            for (int s = 0; s <= steps; s++)
            {
                var c = Vector2.Lerp(a, b, s / (float)steps);
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = (int)c.x + dx, y = (int)c.y + dy;
                    if (x < 0 || x >= w || y < 0 || y >= h) continue;
                    float d = Mathf.Sqrt((x + 0.5f - c.x) * (x + 0.5f - c.x) + (y + 0.5f - c.y) * (y + 0.5f - c.y));
                    float v = Mathf.Clamp01(width * 0.5f + 0.5f - d);
                    int i = y * w + x;
                    if (v > alpha[i]) alpha[i] = v;
                }
            }
        }
    }
}
