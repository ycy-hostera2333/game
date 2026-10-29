using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View
{
    /// <summary>
    /// 显示设置：全屏（无边框，跟随显示器分辨率）或窗口（能拖边框缩放，Player Settings 里开了 Resizable Window）。
    /// 全屏模式和分辨率 Unity 自己记在注册表里，下次启动沿用；这里另记一份窗口大小，从全屏切回窗口时用它。
    /// Screen.SetResolution 到这一帧末尾才生效（编辑器里不生效），所以刚改完的一两帧（编辑器里一直）先报要的值。
    /// </summary>
    public static class DisplaySettings
    {
        /// <summary>窗口大小的几档（16:9），比显示器小的才列出来。</summary>
        static readonly Vector2Int[] Presets = { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) };
        const string WidthPref = "Deeptide.Window.Width", HeightPref = "Deeptide.Window.Height";
        const int SnapMargin = 40;

        static bool _askedWindowed;
        static int _askedFrame = int.MinValue / 2;

        static bool Settled => !Application.isEditor && Time.frameCount > _askedFrame + 1;

        public static bool Windowed => Settled ? Screen.fullScreenMode == FullScreenMode.Windowed : _askedWindowed;

        /// <summary>窗口大小：窗口模式下是当前大小（可能拖过），全屏时是切回窗口会用的大小。</summary>
        public static Vector2Int WindowSize => Windowed && Settled ? new Vector2Int(Screen.width, Screen.height) : SavedSize;

        public static string ModeLabel => Windowed ? "窗口" : "全屏";

        public static string SizeLabel
        {
            get
            {
                var s = WindowSize;
                return $"{s.x} × {s.y}";
            }
        }

        public static void SetWindowed(bool windowed)
        {
            if (windowed == Windowed) return;
            // 拖过的窗口大小记下来，切回窗口时还是这么大
            if (!windowed && Settled) Save(new Vector2Int(Screen.width, Screen.height));
            Ask(windowed);
            if (windowed)
            {
                var s = SavedSize;
                Screen.SetResolution(s.x, s.y, FullScreenMode.Windowed);
            }
            else
            {
                var d = DisplaySize();
                Screen.SetResolution(d.x, d.y, FullScreenMode.FullScreenWindow);
            }
        }

        /// <summary>窗口大小切到下一档（比现在明显宽的第一档，到头了回到最小）。只在窗口模式下有用。</summary>
        public static void CycleWindowSize()
        {
            if (!Windowed) return;
            var fits = Fitting();
            // 拖过的窗口差几个像素就到某一档时（1278 → 1280）算已经在这一档，直接跳下一档
            int width = WindowSize.x + SnapMargin;
            var next = fits.FirstOrDefault(p => p.x > width);
            if (next == default) next = fits[0];
            Save(next);
            Ask(true);
            Screen.SetResolution(next.x, next.y, FullScreenMode.Windowed);
        }

        static void Ask(bool windowed)
        {
            _askedWindowed = windowed;
            _askedFrame = Time.frameCount;
        }

        static Vector2Int SavedSize
        {
            get
            {
                var fits = Fitting();
                var fallback = fits.Length > 1 ? fits[1] : fits[0];   // 默认 1600 × 900，屏幕小就 1280 × 720
                return new Vector2Int(PlayerPrefs.GetInt(WidthPref, fallback.x), PlayerPrefs.GetInt(HeightPref, fallback.y));
            }
        }

        static void Save(Vector2Int size)
        {
            PlayerPrefs.SetInt(WidthPref, size.x);
            PlayerPrefs.SetInt(HeightPref, size.y);
            PlayerPrefs.Save();
        }

        static Vector2Int DisplaySize()
        {
            var info = Screen.mainWindowDisplayInfo;
            if (info.width > 0 && info.height > 0) return new Vector2Int(info.width, info.height);
            var r = Screen.currentResolution;
            return new Vector2Int(r.width, r.height);
        }

        /// <summary>放得进显示器的几档（窗口还有标题栏和任务栏，和显示器一样大的不算）；一档都放不下时留最小的。</summary>
        static Vector2Int[] Fitting()
        {
            var d = DisplaySize();
            var fits = Presets.Where(p => p.x < d.x && p.y < d.y).ToArray();
            return fits.Length > 0 ? fits : new[] { Presets[0] };
        }
    }

    /// <summary>
    /// 画布跟着窗口比例缩放：比 16:9 窄就按宽对齐，比 16:9 宽就按高对齐，1920 × 1080 的参考区域总是完整显示，
    /// 窗口拖成别的比例时多出来的地方只露背景，顶栏两头的按钮不会被裁掉。
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class CanvasFit : MonoBehaviour
    {
        const float Reference = 16f / 9f;
        CanvasScaler _scaler;

        void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            Update();
        }

        void Update() => _scaler.matchWidthOrHeight = (float)Screen.width / Mathf.Max(1, Screen.height) < Reference ? 0 : 1;
    }
}
