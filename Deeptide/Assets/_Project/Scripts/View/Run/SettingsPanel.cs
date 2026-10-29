using System;
using Deeptide.View.Audio;
using Deeptide.View.Battle;
using TMPro;
using UnityEngine;

namespace Deeptide.View.Run
{
    /// <summary>
    /// 设置（标题画面和跑局菜单里都能打开）：显示模式（全屏 / 窗口）、窗口大小、主音量、降低癫狂特效。
    /// 改一项就整块重画（窗口大小只在窗口模式下能点）。按钮名 Display / WindowSize / Sound / Fx / Back，测试用。
    /// </summary>
    public static class SettingsPanel
    {
        public static readonly Vector2 Size = new Vector2(640, 600);

        /// <summary>往卷轴面板 box 里画设置项；back 是「返回」。</summary>
        public static void Build(RectTransform box, Action back) => Draw(box, null, back);

        static void Draw(RectTransform box, RectTransform old, Action back)
        {
            // 旧的一层先藏起来、摘下来再删（Destroy 到帧末才生效，这一帧里按名字找按钮别找到旧的）
            if (old != null)
            {
                old.gameObject.SetActive(false);
                old.SetParent(null, false);
                UnityEngine.Object.Destroy(old.gameObject);
            }
            var content = Ui.Rect("Content", box).Fill();
            void Redraw() => Draw(box, content, back);

            RunUi.Title(content, "设置", new Vector2(0, 240), new Vector2(400, 60), 44);
            Row(content, "Display", "显示", DisplaySettings.ModeLabel, 160, () =>
            {
                DisplaySettings.SetWindowed(!DisplaySettings.Windowed);
                Redraw();
            });
            Row(content, "WindowSize", "窗口大小", DisplaySettings.SizeLabel, 80, () =>
            {
                DisplaySettings.CycleWindowSize();
                Redraw();
            }, DisplaySettings.Windowed, "全屏时跟随显示器分辨率，切到窗口模式再选");
            Row(content, "Sound", "声音", VolumeLabel(), 0, () =>
            {
                AudioDirector.CycleMaster();
                AudioDirector.Ui("U07");
                Redraw();
            });
            Row(content, "Fx", "癫狂特效", LunacyFx.Reduced ? "减弱" : "标准", -80, () =>
            {
                LunacyFx.Reduced = !LunacyFx.Reduced;
                Redraw();
            });
            RunUi.Label(content, "窗口模式下可以拖动边框随意缩放；Alt + Enter 也能切换全屏", new Vector2(0, -160), new Vector2(560, 60), 20, Palette.InkMid);
            RunUi.Button(content, "Back", "返回", new Vector2(0, -240), new Vector2(240, 64), back);
        }

        static string VolumeLabel()
        {
            float v = AudioDirector.GetVolume(AudioDirector.Volume.Master);
            return v <= 0 ? "关" : $"{Mathf.RoundToInt(v * 100)}%";
        }

        static void Row(RectTransform parent, string name, string label, string value, float y, Action click, bool enabled = true, string reason = null)
        {
            RunUi.Label(parent, label, new Vector2(-165, y), new Vector2(220, 60), 28, Palette.Ink, TextAlignmentOptions.Left);
            RunUi.Button(parent, name, value, new Vector2(115, y), new Vector2(320, 64), click, enabled, reason);
        }
    }
}
