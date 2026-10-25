using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Deeptide.Core.Run;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace Deeptide.View.Run
{
    /// <summary>
    /// 试玩遥测（策划/07 §6.2）：每局一个 JSON，写在 <c>persistentDataPath/telemetry/</c>，文件名 = 开局时间_种子。
    /// 每次存档时一起覆盖写（Outcome 从 Unfinished 到 Victory / Defeat / Abandoned），中途关掉游戏也留得下记录。
    /// 只写本机文件，不联网；试玩的人把整个文件夹发回来，用 Tools 的 <c>telemetry --dir</c> 汇总。
    /// </summary>
    public static class TelemetryFiles
    {
        /// <summary>测试用：换一个目录，不碰玩家的记录。</summary>
        public static string DirOverride;
        public static string Dir => !string.IsNullOrEmpty(DirOverride) ? DirOverride : Path.Combine(Application.persistentDataPath, "telemetry");

        const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() },
        };

        public static string GameVersion => Application.version + (Application.isEditor ? " editor" : "");

        /// <summary>开局时调用：记下开局时间和这是本机第几局（= 已有记录数 + 1）。</summary>
        public static void Begin(RunState s)
        {
            s.Stats.StartedAt = DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture);
            s.Stats.RunIndex = Directory.Exists(Dir) ? Directory.GetFiles(Dir, "*.json").Length + 1 : 1;
        }

        public static string PathOf(RunState s)
        {
            string stamp = new string((s.Stats.StartedAt ?? "").Where(char.IsDigit).ToArray());
            return Path.Combine(Dir, $"{(stamp.Length > 0 ? stamp : "unknown")}_{s.Seed}.json");
        }

        /// <summary>写（覆盖）这一局的记录。写不了只打警告，不影响游戏。</summary>
        public static void Write(RunState s, bool abandoned = false)
        {
            try
            {
                if (s.Stats.StartedAt == null) Begin(s);    // 加遥测之前开的局：从现在算起
                var t = RunTelemetry.From(s, abandoned);
                t.GameVersion = GameVersion;
                t.UpdatedAt = DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture);
                Directory.CreateDirectory(Dir);
                string path = PathOf(s), tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(t, Settings));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Telemetry] 写不了遥测：{ex.Message}");
            }
        }

        /// <summary>在资源管理器里打开记录文件夹（标题画面的按钮）。</summary>
        public static void Reveal()
        {
            Directory.CreateDirectory(Dir);
            Application.OpenURL(new Uri(Dir).AbsoluteUri);
        }

        public static int Count => Directory.Exists(Dir) ? Directory.GetFiles(Dir, "*.json").Length : 0;
    }
}
