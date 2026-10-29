using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deeptide.View.Audio;
using UnityEditor;
using UnityEngine;

namespace Deeptide.EditorTools
{
    /// <summary>
    /// 音频打包（策划/13 §6.3–§6.4）：把 <c>AudioSource/sfx/*.wav</c> 复制成 <c>Assets/_Project/Audio/SFX/sfx_&lt;名字&gt;.wav</c>，
    /// 设好导入参数（短于 1 秒的 Decompress On Load + ADPCM；更长的 Compressed In Memory + Vorbis 0.7），再按源文件名登记进 AudioDatabase。
    /// 源文件由 <c>AudioSource/procedural/build.py</c> 合成；改了素材重跑这个就行，同名的覆盖。
    /// 菜单「Deeptide/音频/打包音效」；batchmode：Unity.exe -batchmode -projectPath Deeptide -executeMethod Deeptide.EditorTools.AudioPacker.Batch -quit
    /// </summary>
    public static class AudioPacker
    {
        public const string SfxOutputDir = "Assets/_Project/Audio/SFX";
        public const string DatabasePath = "Assets/_Project/Audio/Resources/AudioDatabase.asset";
        const float ShortClip = 1f;

        public static string SfxSourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "AudioSource", "sfx"));

        [MenuItem("Deeptide/音频/打包音效")]
        public static void Pack() => Debug.Log($"[AudioPacker] 音效：{PackSfx(SfxSourceDir)}");

        public static void Batch()
        {
            try
            {
                Pack();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        public static string PackSfx(string sourceDir)
        {
            if (!Directory.Exists(sourceDir)) return $"没有源文件目录 {sourceDir}，跳过";
            var files = Directory.GetFiles(sourceDir, "*.wav").OrderBy(p => p, StringComparer.Ordinal).ToList();
            if (files.Count == 0) return $"{sourceDir} 下没有 .wav";

            Directory.CreateDirectory(SfxOutputDir);
            var copied = new List<(string Id, string AssetPath)>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var src in files)
                {
                    string id = Path.GetFileNameWithoutExtension(src);
                    string assetPath = $"{SfxOutputDir}/sfx_{id}.wav";
                    File.Copy(src, assetPath, true);
                    copied.Add((id, assetPath));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var db = LoadOrCreateDatabase();
            int shortCount = 0;
            foreach (var (id, assetPath) in copied)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null) throw new InvalidOperationException($"{assetPath} 没有导入成 AudioClip");
                bool isShort = clip.length < ShortClip;
                if (isShort) shortCount++;
                if (ConfigureImporter(assetPath, isShort)) clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                db.SetSfx(id, clip);
            }
            // 源文件删掉的，登记也去掉
            var ids = new HashSet<string>(copied.Select(c => c.Id));
            db.Sfx.RemoveAll(e => !ids.Contains(e.Id));
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AudioDatabase.Reload();
            return $"{copied.Count} 个（{shortCount} 个短音效 ADPCM，{copied.Count - shortCount} 个 Vorbis）→ {SfxOutputDir}";
        }

        /// <summary>按 13 §6.3 设导入参数；改了返回 true（要重新导入）。</summary>
        static bool ConfigureImporter(string assetPath, bool isShort)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(assetPath);
            var s = importer.defaultSampleSettings;
            var want = s;
            want.loadType = isShort ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            want.compressionFormat = isShort ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis;
            want.quality = isShort ? 1f : 0.7f;
            want.preloadAudioData = true;
            bool changed = want.loadType != s.loadType || want.compressionFormat != s.compressionFormat
                || Math.Abs(want.quality - s.quality) > 0.001f || want.preloadAudioData != s.preloadAudioData;
            if (!changed) return false;
            importer.defaultSampleSettings = want;
            importer.SaveAndReimport();
            return true;
        }

        static AudioDatabase LoadOrCreateDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<AudioDatabase>(DatabasePath);
            if (db != null) return db;
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            db = ScriptableObject.CreateInstance<AudioDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            return db;
        }
    }
}
