using System;
using System.IO;
using Deeptide.Core.Data;

namespace Deeptide.Tools
{
    /// <summary>
    /// 从磁盘读取 Unity 工程里的 Data/*.csv 并载入内核（Core 自己不读文件）。
    /// 从程序所在目录和当前目录逐级往上找 Deeptide/Assets/_Project/Data，所以在仓库里任何位置运行都行。
    /// </summary>
    static class DataFiles
    {
        const string RelativeDir = "Deeptide/Assets/_Project/Data";
        static readonly object Gate = new object();

        public static string FindDataDir()
        {
            foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, RelativeDir);
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            throw new DirectoryNotFoundException($"找不到 {RelativeDir}（需要在仓库目录里运行）");
        }

        public static DataSource Read(string dir = null)
        {
            dir ??= FindDataDir();
            return DataSource.From(name =>
            {
                string path = Path.Combine(dir, name);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            });
        }

        /// <summary>第一次调用时载入；之后直接返回。</summary>
        public static void EnsureLoaded()
        {
            lock (Gate)
            {
                if (!GameData.IsLoaded) GameData.Load(Read());
            }
        }

        /// <summary>重新载入，丢掉运行期对数值的临时修改（如 --set）。</summary>
        public static void Reload()
        {
            lock (Gate) GameData.Load(Read());
        }
    }
}
