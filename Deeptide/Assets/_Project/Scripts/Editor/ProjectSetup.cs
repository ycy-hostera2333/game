using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deeptide.Core.Data;
using Deeptide.View;
using Deeptide.View.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Deeptide.EditorTools
{
    /// <summary>
    /// 一次性的工程设置，菜单「Deeptide/工程设置」里也能手动执行。都是幂等的：已经设好的跳过。
    /// batchmode（不要加 -quit，导入 unitypackage 是异步的，Batch 自己在导入完成后退出）：
    /// Unity.exe -batchmode -projectPath Deeptide -executeMethod Deeptide.EditorTools.ProjectSetup.Batch
    /// </summary>
    public static class ProjectSetup
    {
        static bool _importPending;
        const string SettingsDir = "Assets/_Project/Settings";
        const string UrpAssetPath = SettingsDir + "/URP-2D.asset";
        const string UrpRendererPath = SettingsDir + "/URP-2D_Renderer.asset";
        const string DataDir = "Assets/_Project/Data";
        const string ScenesDir = "Assets/_Project/Scenes";
        public const string GreyboxScene = ScenesDir + "/Battle_Greybox.unity";
        const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        public static void Batch()
        {
            RunAll();
            AssetDatabase.SaveAssets();
            if (!_importPending)
            {
                EditorApplication.Exit(0);
                return;
            }

            double deadline = EditorApplication.timeSinceStartup + 120;
            AssetDatabase.importPackageCompleted += _ => Finish(0);
            AssetDatabase.importPackageFailed += (_, error) =>
            {
                Debug.LogError($"[ProjectSetup] 导入失败：{error}");
                Finish(1);
            };
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup < deadline) return;
                Debug.LogError("[ProjectSetup] 等待导入超时");
                Finish(1);
            };
        }

        static void Finish(int code)
        {
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(code);
        }

        [MenuItem("Deeptide/工程设置/全部执行")]
        public static void RunAll()
        {
            EnsureUrp2D();
            EnsureTmpEssentials();
            if (!File.Exists(GreyboxScene)) BuildGreyboxScene();
            EnsureInBuildSettings(GreyboxScene);
        }

        /// <summary>建一套「URP Asset (with 2D Renderer)」放到 Settings/，设为默认渲染管线（策划/08：2D URP）。</summary>
        [MenuItem("Deeptide/工程设置/URP 2D 渲染管线")]
        public static void EnsureUrp2D()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (asset == null)
            {
                Directory.CreateDirectory(SettingsDir);
                AssetDatabase.Refresh();
                var before = new HashSet<string>(AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset")
                    .Concat(AssetDatabase.FindAssets("t:Renderer2DData")));

                // 走 URP 自己的菜单，渲染器的默认设置和手动创建完全一样。没有 Project 窗口时它直接用默认名字建在 Assets/ 下
                if (!EditorApplication.ExecuteMenuItem("Assets/Create/Rendering/URP Asset (with 2D Renderer)"))
                    throw new InvalidOperationException("找不到菜单 Assets/Create/Rendering/URP Asset (with 2D Renderer)");
                AssetDatabase.Refresh();

                foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset").Where(g => !before.Contains(g)))
                    Move(AssetDatabase.GUIDToAssetPath(guid), UrpAssetPath);
                foreach (var guid in AssetDatabase.FindAssets("t:Renderer2DData").Where(g => !before.Contains(g)))
                    Move(AssetDatabase.GUIDToAssetPath(guid), UrpRendererPath);

                asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
                if (asset == null) throw new InvalidOperationException("URP 资源没有生成");
            }

            if (GraphicsSettings.defaultRenderPipeline != asset)
            {
                GraphicsSettings.defaultRenderPipeline = asset;
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[ProjectSetup] 默认渲染管线：{UrpAssetPath}");
        }

        static void Move(string from, string to)
        {
            if (from == to) return;
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException($"移动 {from} → {to} 失败：{error}");
        }

        /// <summary>TMP 基础资源（TMP Settings、默认字体、着色器）。界面里第一次用 TMP 时会弹窗问，这里直接导入。</summary>
        [MenuItem("Deeptide/工程设置/导入 TMP 基础资源")]
        public static void EnsureTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return;
            _importPending = true;
            AssetDatabase.importPackageCompleted += name => Debug.Log($"[ProjectSetup] 已导入 {name}");
            AssetDatabase.ImportPackage(TmpEssentials, false);
        }

        /// <summary>灰盒战斗场景：相机 + EventSystem + BattleBootstrap，界面在运行时由代码搭出来。会覆盖已有的同名场景。</summary>
        [MenuItem("Deeptide/工程设置/重建灰盒战斗场景")]
        public static void BuildGreyboxScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData)) { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var boot = new GameObject("BattleBootstrap").AddComponent<BattleBootstrap>();
            boot.DataFiles = DataSource.FileNames
                .Select(f => AssetDatabase.LoadAssetAtPath<TextAsset>($"{DataDir}/{f}") ?? throw new FileNotFoundException($"{DataDir}/{f}"))
                .ToArray();

            Directory.CreateDirectory(ScenesDir);
            if (!EditorSceneManager.SaveScene(scene, GreyboxScene)) throw new InvalidOperationException($"保存 {GreyboxScene} 失败");
            EnsureInBuildSettings(GreyboxScene);
            Debug.Log($"[ProjectSetup] 已生成 {GreyboxScene}");
        }

        static void EnsureInBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
