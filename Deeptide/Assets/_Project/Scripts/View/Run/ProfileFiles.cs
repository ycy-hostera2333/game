using System;
using System.IO;
using Deeptide.Core.Run;
using Newtonsoft.Json;
using UnityEngine;

namespace Deeptide.View.Run
{
    /// <summary>
    /// 局外档（策划/12 §9）：<c>persistentDataPath/profile.json</c>，记第几夜和剧情进度（附言、见过的事件、死过、打倒过司祭）。
    /// 和局存档一样先写临时文件再替换。读不了（没有、损坏）就当成第一次玩：最坏是信上的附言从头来一遍，不影响游戏。
    /// </summary>
    public static class ProfileFiles
    {
        /// <summary>测试用：换一个路径，不碰玩家的局外档。</summary>
        public static string PathOverride;
        public static string FilePath => !string.IsNullOrEmpty(PathOverride) ? PathOverride : Path.Combine(Application.persistentDataPath, "profile.json");

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public static Profile Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return new Profile();
                return JsonConvert.DeserializeObject<Profile>(File.ReadAllText(FilePath), Settings) ?? new Profile();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Profile] 局外档读不了，按第一次玩处理：{ex.Message}");
                return new Profile();
            }
        }

        /// <summary>写不了只打警告，不影响游戏。</summary>
        public static void Write(Profile p)
        {
            try
            {
                string path = FilePath, tmp = path + ".tmp";
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(tmp, JsonConvert.SerializeObject(p, Settings));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Profile] 写不了局外档：{ex.Message}");
            }
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
