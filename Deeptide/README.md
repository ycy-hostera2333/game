# Deeptide（Unity 工程）

内核源码（`Assets/_Project/Scripts/Core/`）、内容数据（`Assets/_Project/Data/*.csv`），以及阶段 2 的灰盒战斗：`Scenes/Battle_Greybox.unity`，打击 + 防御对教团侍僧。

## 第一次打开

本机已装 **Unity 6.3 LTS**（6000.3.26f1，含 Windows Build Support (IL2CPP) 和简体中文），Personal 许可已激活，工程设置都已提交。换一台电脑时：

1. 在 Unity Hub 登录，到「偏好设置 → 许可证」添加免费的 Personal 许可。没有许可证时连 batchmode 都打不开（`No valid Unity Editor license found`）
2. Hub 里选「Add → Add project from disk」，选中本目录 `Deeptide/`，编辑器版本选 6000.3.26f1
3. 第一次打开要下载包、导入资源，几分钟

已经设好、不用再动的：输入后端 = Input System（`activeInputHandler: 1`）；默认渲染管线 = `Settings/URP-2D.asset`（2D 渲染器）；TMP 基础资源已导入（`Assets/TextMesh Pro/`）。这几项都可以用菜单「Deeptide → 工程设置」重做，脚本是 `Scripts/Editor/ProjectSetup.cs`，已经设好的会跳过。

## 灰盒战斗

打开 `Assets/_Project/Scenes/Battle_Greybox.unity`，点 Play。

- 拖牌出牌：指向型的牌拖到敌人身上（会画瞄准线），其余的拖过手牌上方就打出；能量不够会提示
- 顶栏（策划/10 §6.1）：生命、药水栏、遗物、回合。点药水使用，需要目标的药水再点一个敌人（点别处取消）；遗物悬停看说明，触发时会跳一下
- 需要选择的牌（查阅资料、禁书残页、心灵之锚）弹出选择面板，和牌有关的选项画成卡牌
- 右下「结束回合」；顶栏右侧「重开」用同样的配置换下一个种子，「选择对局」打开启动面板：测试牌组（起始 / A 守夜 / B 堕落 / C 混合 / D 传染）× 全部 15 组遭遇 × 遗物 × 药水（最多 3 瓶）× 种子，任意组合都能直接开打
- 默认配置在场景里 `BattleBootstrap` 的 Inspector 上改：`DeckName` / `Encounter` 留空时用下面的 `Deck` / `Enemies`（ID 见 `Data/`）
- 画面按 策划/10：纸面色板、卡面版式、理智条（悬停代价牌会预告）、癫狂时的暗角 / 色差 / 意图模糊 / 双面翻面，实现记录见 10 §12.6。图形都是运行时程序化生成的（`View/Common/Shapes.cs`），着色器在 `Shaders/Resources/`
- 卡图：分层 PNG 放进 `ArtSource/cards/`，菜单「Deeptide → 美术 → 打包卡图」打包并登记进 `ArtDatabase`，这张牌的插图窗就从灰盒首字换成卡图。规则见 `ArtSource/README.md`，排期见 策划/11
- 中文用系统字体动态生成，字体文件不进仓库：正文微软雅黑，卡名和数字优先用系统里的 Noto Serif SC / 思源宋体（按文件加载），没有时用华文宋体；正式版随包带思源字体

结构按 策划/08 §2：`BattleController` 把输入变成 `BattleCommand` 交给内核，内核瞬时结算；`BattleDirector` 逐条回放这次新产生的事件，`BattleScreen` 把事件分发给 `HandView` / `CardView` / `EnemyView` / `PlayerView`；一批事件播完后再按内核状态对一次账。界面目前全部由代码搭出来（`View/Common/Ui.cs`），换美术时再做成预制体。

## 测试

PlayMode 测试在 `Assets/_Project/Tests/PlayMode/`：通过表现层入口出牌，检查界面显示和内核状态一致；关键节点把画面存成 `Logs/Screenshots/*.png`（不进仓库）。

- `BattleGreyboxTests`：打击 + 防御的基本流程、能量不足被拒、一路打到胜利
- `BattleContentTests`：15 组遭遇用混合牌组逐个打到分出胜负，每一步核对手牌、牌堆、能量、生命、格挡、每个敌人、药水栏和选择面板；启动面板换成 Boss 战；药水选目标；查阅资料的选择面板

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -projectPath Deeptide -runTests -testPlatform PlayMode -testResults Logs/playmode.xml -logFile Logs/playmode.log
```

编辑器里打开着这个工程时，batchmode 会因为工程被占用而失败，先关掉编辑器，或者直接用 Window → General → Test Runner。

## 包（`Packages/manifest.json`）

| 包 | 版本 | 用途 |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.3.0 | 2D 渲染器 + Shader Graph（翻面扭曲、癫狂后处理） |
| `com.unity.ugui` | 2.0.0 | uGUI + TextMeshPro。Unity 6 起 TMP 并入 ugui，`com.unity.textmeshpro` 只剩兼容空壳，不要再装 |
| `com.unity.inputsystem` | 1.20.0 | 拖拽出牌、悬停 |
| `com.kyrylokuzyk.primetween` | 1.4.11 | 补间动画，走 npm 作用域注册表。1.5.0 于 2026-10-14 发布，稳定后再升 |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | 存档、遥测。DLL 自动引用，asmdef 里不用写 |
| `com.unity.test-framework` | 1.6.0 | PlayMode / EditMode 测试 |
| `com.unity.2d.sprite` | 1.0.0 | Sprite Editor |
| `com.unity.ide.visualstudio` | 2.0.26 | VS Code / Visual Studio 集成 |

内置模块只开了 2D 卡牌用得到的（animation、audio、imageconversion、imgui、jsonserialize、particlesystem、physics2d、ui、uielements、unitywebrequest）；3D 相关的没加，需要时在 Package Manager → Built-in 里启用。

注意：工程没有 `ProjectSettings/` 时第一次打开，Unity 会往 manifest 里自动塞一批默认包（Purchasing、Analytics 等）；改了 manifest 之后用 batchmode `-quit` 打开可能因为走缓存而取消解析（exit 1），删掉 `Library/PackageManager` 再跑就好。

## 程序集

| asmdef | 目录 | 引用 |
|---|---|---|
| `Game.Core` | `Scripts/Core/` | 无（`noEngineReferences`） |
| `Game.View` | `Scripts/View/` | `Game.Core`、`Unity.InputSystem`、`Unity.TextMeshPro`、`UnityEngine.UI`、`PrimeTween.Runtime` |
| `Game.Editor` | `Scripts/Editor/` | `Game.Core`、`Game.View`、`Unity.InputSystem`、URP Runtime（只在编辑器） |
| `Game.View.Tests` | `Tests/PlayMode/` | `Game.Core`、`Game.View`、Test Runner（`UNITY_INCLUDE_TESTS`） |

## 目录约定（策划/08 §8）

```
Assets/_Project/
├─ Scripts/
│  ├─ Core/      ← Game.Core.asmdef（noEngineReferences）：纯逻辑，不能引用 UnityEngine
│  ├─ View/      ← Game.View.asmdef：Battle/（战斗界面）、Common/（UI 搭建、字体、配色）
│  └─ Editor/    ← Game.Editor.asmdef：工程设置，以后放数据校验、卡牌预览
├─ Data/         ← cards.csv / enemies.csv …（场景里 BattleBootstrap 引用这些 TextAsset）
├─ Scenes/       ← Battle_Greybox（阶段 2 灰盒）；以后 Boot、MainMenu、Run
├─ Settings/     ← URP-2D 渲染管线资源
├─ Tests/        ← PlayMode 测试
└─ Art/ Audio/ Prefabs/ Shaders/
```

Core 的测试和模拟器在 `../Tools/`，不需要打开 Unity 就能运行。
