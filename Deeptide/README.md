# Deeptide（Unity 工程）

目前有内核源码（`Assets/_Project/Scripts/Core/`）、内容数据（`Assets/_Project/Data/*.csv`）和一个空的表现层程序集（`Scripts/View/Game.View.asmdef`），还没有场景。

## 第一次打开

本机已装 **Unity 6.3 LTS**（6000.3.26f1，含 Windows Build Support (IL2CPP) 和简体中文）。`Packages/manifest.json` 已经写好，不用再走模板建工程。

1. 在 Unity Hub 登录，到「偏好设置 → 许可证」添加免费的 Personal 许可。没有许可证时连 batchmode 都打不开（`No valid Unity Editor license found`）
2. Hub 里选「Add → Add project from disk」，选中本目录 `Deeptide/`，编辑器版本选 6000.3.26f1
3. 输入后端已设为 Input System（`ProjectSettings.asset` 的 `activeInputHandler: 1`）。第一次用到 TextMeshPro 时会提示导入 TMP Essential Resources，点 Import
4. URP 2D：Assets → Create → Rendering → URP Asset (with 2D Renderer)，再到 Project Settings → Graphics 把它设为 Default Render Pipeline
5. Unity 会生成 `Library/`、`ProjectSettings/`、`Packages/packages-lock.json` 和所有文件的 `.meta`。`Library/` 已在 `.gitignore`，其余都要提交

## 包（`Packages/manifest.json`）

| 包 | 版本 | 用途 |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.3.0 | 2D 渲染器 + Shader Graph（翻面扭曲、癫狂后处理） |
| `com.unity.ugui` | 2.0.0 | uGUI + TextMeshPro。Unity 6 起 TMP 并入 ugui，`com.unity.textmeshpro` 只剩兼容空壳，不要再装 |
| `com.unity.inputsystem` | 1.20.0 | 拖拽出牌、悬停 |
| `com.kyrylokuzyk.primetween` | 1.4.11 | 补间动画，走 npm 作用域注册表。1.5.0 于 2026-10-14 发布，稳定后再升 |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | 存档、遥测。DLL 自动引用，asmdef 里不用写 |
| `com.unity.test-framework` | 1.6.0 | EditMode 集成测试 |
| `com.unity.2d.sprite` | 1.0.0 | Sprite Editor |
| `com.unity.ide.visualstudio` | 2.0.26 | VS Code / Visual Studio 集成 |

内置模块只开了 2D 卡牌用得到的（animation、audio、imageconversion、imgui、jsonserialize、particlesystem、physics2d、ui、uielements、unitywebrequest）；3D 相关的没加，需要时在 Package Manager → Built-in 里启用。

## 程序集

| asmdef | 目录 | 引用 |
|---|---|---|
| `Game.Core` | `Scripts/Core/` | 无（`noEngineReferences`） |
| `Game.View` | `Scripts/View/` | `Game.Core`、`Unity.InputSystem`、`Unity.TextMeshPro`、`UnityEngine.UI`、`PrimeTween.Runtime` |

## 目录约定（策划/08 §8）

```
Assets/_Project/
├─ Scripts/
│  ├─ Core/    ← Game.Core.asmdef（noEngineReferences）：纯逻辑，不能引用 UnityEngine
│  ├─ View/    ← 阶段 2：表现层（MonoBehaviour）
│  └─ Editor/  ← 数据校验、卡牌预览等编辑器工具
├─ Data/       ← 阶段 3：cards.csv / enemies.csv …
└─ Art/ Audio/ Prefabs/ Shaders/ Scenes/
```

Core 的测试和模拟器在 `../Tools/`，不需要打开 Unity 就能运行。
