using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deeptide.View;
using UnityEditor;
using UnityEngine;

namespace Deeptide.EditorTools
{
    /// <summary>
    /// 美术打包（策划/10 §11.2–§11.5，11 §3）：把 <c>ArtSource/cards/</c>（卡图）和 <c>ArtSource/actors/</c>（角色）下的分层 PNG 打成一张 RGBA，
    /// 导出到 <c>Assets/_Project/Art/Cards/card_&lt;ID&gt;.png</c> / <c>Art/Actors/actor_&lt;ID&gt;.png</c>，设好导入参数（关 sRGB、开 mipmap、BC7），再登记进 ArtDatabase。
    /// 分层命名：<c>C06_ink.png</c>（必需）、<c>C06_abyss.png</c>、<c>C06_mad.png</c>、<c>C06_fill.png</c>（可选），各层尺寸不同时按 _ink 重采样。
    /// 角色图透明底，必须有 <c>_fill</c>（轮廓），否则整张图都是不透明的纸。
    /// 菜单「Deeptide/美术/打包卡图」；batchmode：Unity.exe -batchmode -projectPath Deeptide -executeMethod Deeptide.EditorTools.ArtPacker.Batch -quit
    /// </summary>
    public static class ArtPacker
    {
        public const string OutputDir = "Assets/_Project/Art/Cards";
        public const string ActorOutputDir = "Assets/_Project/Art/Actors";
        public const string BackgroundOutputDir = "Assets/_Project/Art/Backgrounds";
        public const string SpriteOutputDir = "Assets/_Project/Art/Sprites";
        public const string DatabasePath = "Assets/_Project/Art/Resources/ArtDatabase.asset";

        /// <summary>源文件目录：工程根目录（Assets 的上一级）下的 ArtSource/cards，不进 Assets，Unity 不会去导入。</summary>
        public static string SourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtSource", "cards"));
        public static string ActorSourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtSource", "actors"));
        public static string BackgroundSourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtSource", "backgrounds"));
        public static string SpriteSourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtSource", "sprites"));

        [MenuItem("Deeptide/美术/打包卡图和角色")]
        public static void PackCards()
        {
            Debug.Log($"[ArtPacker] 卡图：{PackAll(SourceDir)}");
            Debug.Log($"[ArtPacker] 角色：{PackAll(ActorSourceDir, ActorOutputDir, "actor_", (db, id, tex) => db.SetActor(id, tex))}");
            Debug.Log($"[ArtPacker] 背景：{PackAll(BackgroundSourceDir, BackgroundOutputDir, "bg_", (db, id, tex) => db.SetBackground(id, tex))}");
            Debug.Log($"[ArtPacker] 单色精灵：{CopySprites(SpriteSourceDir)}");
        }

        public static void Batch()
        {
            try
            {
                PackCards();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>打包 sourceDir 下的全部卡图，返回一行报告。</summary>
        public static string PackAll(string sourceDir) =>
            PackAll(sourceDir, OutputDir, "card_", (db, id, tex) => db.SetCard(id, tex));

        /// <summary>打包 sourceDir 下的全部分层图到 outputDir/&lt;prefix&gt;&lt;ID&gt;.png，用 register 登记，返回一行报告。</summary>
        public static string PackAll(string sourceDir, string outputDir, string prefix, Action<ArtDatabase, string, Texture2D> register)
        {
            if (!Directory.Exists(sourceDir)) return $"没有源文件目录 {sourceDir}，跳过";
            var inks = Directory.GetFiles(sourceDir, "*_ink.png").OrderBy(p => p, StringComparer.Ordinal).ToList();
            if (inks.Count == 0) return $"{sourceDir} 下没有 *_ink.png";

            Directory.CreateDirectory(outputDir);
            var db = LoadOrCreateDatabase();
            var packed = new List<string>();
            var errors = new List<string>();
            foreach (var inkPath in inks)
            {
                string id = Path.GetFileNameWithoutExtension(inkPath);
                id = id.Substring(0, id.Length - "_ink".Length);
                try
                {
                    string assetPath = PackOne(sourceDir, id, outputDir, prefix);
                    register(db, id, AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
                    packed.Add(id);
                }
                catch (Exception ex)
                {
                    errors.Add($"{id}：{ex.Message}");
                }
            }
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            ArtDatabase.Invalidate();

            string summary = $"打包 {packed.Count} 张：{string.Join(" ", packed)}";
            if (errors.Count > 0)
            {
                foreach (var e in errors) Debug.LogError($"[ArtPacker] {e}");
                summary += $"；失败 {errors.Count} 张（见错误日志）";
            }
            return summary;
        }

        /// <summary>
        /// 单色精灵（图标、框饰、UI 墨线、特效帧）：源文件已经是白色 + alpha，直接拷进 Art/Sprites，
        /// 导入为 sRGB、不开 mipmap（10 §11.3），登记进 ArtDatabase。子目录只用来分类，ID 是文件名。
        /// </summary>
        public static string CopySprites(string sourceDir)
        {
            if (!Directory.Exists(sourceDir)) return $"没有源文件目录 {sourceDir}，跳过";
            var files = Directory.GetFiles(sourceDir, "*.png", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).ToList();
            if (files.Count == 0) return $"{sourceDir} 下没有 png";
            Directory.CreateDirectory(SpriteOutputDir);
            var db = LoadOrCreateDatabase();
            foreach (var file in files)
            {
                string id = Path.GetFileNameWithoutExtension(file);
                string assetPath = $"{SpriteOutputDir}/{id}.png";
                File.Copy(file, assetPath, true);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
                db.SetSprite(id, AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
            }
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            ArtDatabase.Invalidate();
            return $"{files.Count} 张";
        }

        /// <summary>打包一张卡图，返回导出的资源路径。</summary>
        public static string PackOne(string sourceDir, string id) => PackOne(sourceDir, id, OutputDir, "card_");

        /// <summary>打包一张，返回导出的资源路径。</summary>
        public static string PackOne(string sourceDir, string id, string outputDir, string prefix)
        {
            var ink = Load(Path.Combine(sourceDir, id + "_ink.png"));
            if (ink == null) throw new FileNotFoundException($"缺少 {id}_ink.png");
            int w = ink.width, h = ink.height;
            if (prefix == "card_" && Mathf.Abs((float)w / h - (float)CardArt.Width / CardArt.Height) > 0.02f)
                Debug.LogWarning($"[ArtPacker] {id} 的比例是 {w}×{h}，卡图应为 {CardArt.Width}×{CardArt.Height}（3:2 附近，10 §11.1），会被拉伸");

            var packed = CardArt.Pack(
                ink.GetPixels32(),
                Layer(sourceDir, id, "abyss", w, h),
                Layer(sourceDir, id, "mad", w, h),
                Layer(sourceDir, id, "fill", w, h));
            UnityEngine.Object.DestroyImmediate(ink);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(packed);
            tex.Apply(false);
            string assetPath = $"{outputDir}/{prefix}{id}.png";
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(assetPath, prefix == "bg_" ? 4096 : 1024);
            return assetPath;
        }

        /// <summary>10 §11.3：打包贴图是数据不是颜色，关 sRGB；显示时会缩放，开 mipmap；BC7。</summary>
        static void ConfigureImporter(string assetPath, int maxSize)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = false;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Trilinear;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        static Color32[] Layer(string sourceDir, string id, string layer, int w, int h)
        {
            var tex = Load(Path.Combine(sourceDir, $"{id}_{layer}.png"));
            if (tex == null) return null;
            var px = tex.width == w && tex.height == h ? tex.GetPixels32() : Resample(tex, w, h);
            UnityEngine.Object.DestroyImmediate(tex);
            return px;
        }

        static Texture2D Load(string path)
        {
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                throw new InvalidDataException($"{Path.GetFileName(path)} 不是有效的 PNG");
            }
            return tex;
        }

        static Color32[] Resample(Texture2D src, int w, int h)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = src.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
            return px;
        }

        static ArtDatabase LoadOrCreateDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ArtDatabase>(DatabasePath);
            if (db != null) return db;
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            db = ScriptableObject.CreateInstance<ArtDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            return db;
        }
    }
}
