using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Deeptide.EditorTools
{
    /// <summary>
    /// 打包 Windows 试玩版，菜单「Deeptide/打包 Windows」：Build Settings 里启用的场景（Run 在第一个），64 位，
    /// 脚本后端按工程设置（默认 Mono，不需要装 VC++ 工具链）。输出到 Deeptide/Builds/Deeptide_Windows/（不进仓库），
    /// 每次先清空旧的；Burst 的调试符号目录（*_DoNotShip）打完删掉；BuildExtras/ 下的文件（试玩说明）复制进去；
    /// 最后把整个文件夹压成 Builds/Deeptide_版本号.zip，直接发给试玩的人。
    /// 发新包之前先改 Player Settings 的 Version：遥测里按它区分是哪个包打的（TelemetryFiles.GameVersion）。
    /// batchmode：Unity.exe -batchmode -quit -projectPath Deeptide -executeMethod Deeptide.EditorTools.BuildPlayer.Batch -logFile Logs/build.log
    /// </summary>
    public static class BuildPlayer
    {
        public const string OutDir = "Builds/Deeptide_Windows";
        const string ExtrasDir = "BuildExtras";

        [MenuItem("Deeptide/打包 Windows")]
        public static void Menu()
        {
            var exe = Build();
            if (exe != null) EditorUtility.RevealInFinder(exe);
        }

        public static void Batch() => EditorApplication.Exit(Build() != null ? 0 : 1);

        /// <summary>返回 exe 的完整路径，失败返回 null。</summary>
        static string Build()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildPlayer] Build Settings 里没有启用的场景");
                return null;
            }
            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutDir, PlayerSettings.productName + ".exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });
            var s = report.summary;
            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[BuildPlayer] 打包失败：{s.result}，{s.totalErrors} 个错误");
                return null;
            }
            foreach (var dir in Directory.GetDirectories(OutDir, "*_DoNotShip")) Directory.Delete(dir, true);
            if (Directory.Exists(ExtrasDir))
                foreach (var file in Directory.GetFiles(ExtrasDir)) File.Copy(file, Path.Combine(OutDir, Path.GetFileName(file)), true);

            string zip = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(OutDir) ?? "", $"Deeptide_{PlayerSettings.bundleVersion}.zip"));
            if (File.Exists(zip)) File.Delete(zip);
            // 压缩包里带一层 Deeptide_Windows/ 目录，解压后不会把文件撒一地
            // 文件名用 UTF-8 写，再补上 UTF-8 标记：Unity 的 Mono 不指定编码时按系统代码页（GBK）写「试玩说明.txt」却打 UTF-8 标记，
            // 指定 UTF-8 时又不打标记，两种解压出来都是乱码
            ZipFile.CreateFromDirectory(OutDir, zip, System.IO.Compression.CompressionLevel.Optimal, true, new System.Text.UTF8Encoding(false));
            MarkUtf8Names(zip);

            string exe = Path.GetFullPath(s.outputPath);
            Debug.Log($"[BuildPlayer] 打包完成：{exe}，版本 {PlayerSettings.bundleVersion}，{s.totalSize / 1048576f:F0} MB，用时 {s.totalTime.TotalSeconds:F0} 秒，" +
                      $"场景 {string.Join(" / ", scenes.Select(Path.GetFileNameWithoutExtension))}；压缩包 {zip}（{new FileInfo(zip).Length / 1048576f:F0} MB）");
            return exe;
        }

        /// <summary>给 zip 里每一项的「通用标志」补上第 11 位（文件名是 UTF-8）：中央目录和本地文件头各一处。</summary>
        static void MarkUtf8Names(string zip)
        {
            const ushort Utf8Flag = 0x800;
            var bytes = File.ReadAllBytes(zip);
            int eocd = -1;
            for (int i = bytes.Length - 22; i >= 0 && i >= bytes.Length - 22 - 65535; i--)
                if (BitConverter.ToUInt32(bytes, i) == 0x06054b50) { eocd = i; break; }
            if (eocd < 0) throw new InvalidDataException($"{zip} 找不到中央目录结尾");
            int count = BitConverter.ToUInt16(bytes, eocd + 10);
            int at = (int)BitConverter.ToUInt32(bytes, eocd + 16);
            for (int k = 0; k < count; k++)
            {
                if (BitConverter.ToUInt32(bytes, at) != 0x02014b50) throw new InvalidDataException($"{zip} 第 {k} 个中央目录项不对");
                SetFlag(bytes, at + 8, Utf8Flag);
                int local = (int)BitConverter.ToUInt32(bytes, at + 42);
                if (BitConverter.ToUInt32(bytes, local) != 0x04034b50) throw new InvalidDataException($"{zip} 第 {k} 项的本地文件头不对");
                SetFlag(bytes, local + 6, Utf8Flag);
                at += 46 + BitConverter.ToUInt16(bytes, at + 28) + BitConverter.ToUInt16(bytes, at + 30) + BitConverter.ToUInt16(bytes, at + 32);
            }
            File.WriteAllBytes(zip, bytes);
        }

        static void SetFlag(byte[] bytes, int offset, ushort flag)
        {
            ushort v = (ushort)(BitConverter.ToUInt16(bytes, offset) | flag);
            bytes[offset] = (byte)v;
            bytes[offset + 1] = (byte)(v >> 8);
        }
    }
}
