# Deeptide（Unity 工程）

内核源码（`Assets/_Project/Scripts/Core/`）、内容数据（`Assets/_Project/Data/*.csv`）、整局游戏 `Scenes/Run.unity`（标题 → 地图 → 战斗 / 商店 / 篝火 / 事件 → Boss 结算），以及单场测试用的灰盒战斗 `Scenes/Battle_Greybox.unity`。

## 第一次打开

本机已装 **Unity 6.3 LTS**（6000.3.26f1，含 Windows Build Support (IL2CPP) 和简体中文），Personal 许可已激活，工程设置都已提交。换一台电脑时：

1. 在 Unity Hub 登录，到「偏好设置 → 许可证」添加免费的 Personal 许可。没有许可证时连 batchmode 都打不开（`No valid Unity Editor license found`）
2. Hub 里选「Add → Add project from disk」，选中本目录 `Deeptide/`，编辑器版本选 6000.3.26f1
3. 第一次打开要下载包、导入资源，几分钟

已经设好、不用再动的：输入后端 = Input System（`activeInputHandler: 1`）；默认渲染管线 = `Settings/URP-2D.asset`（2D 渲染器）；TMP 基础资源已导入（`Assets/TextMesh Pro/`）。这几项都可以用菜单「Deeptide → 工程设置」重做，脚本是 `Scripts/Editor/ProjectSetup.cs`，已经设好的会跳过。

## 整局（Run 场景）

打开 `Assets/_Project/Scenes/Run.unity`，点 Play（打包后启动也进这个场景）。

- 标题画面：主视觉做背景，Logo 和按钮在左边的雾里。有存档时「继续」（显示层数、生命、金币），「新的一局」用下面的种子开局（「换一个」随机换），「单场战斗（灰盒测试）」跳到 `Battle_Greybox`。有存档时点「新的一局」要连点两次，第二次才覆盖存档
- 地图：竖向卷动的海图（滚轮或拖动），第 1 层在栈桥上方，最上面的漩涡是 Boss。能去的节点会轻轻跳动，点它进入；悬停任意节点，从当前位置通往它的所有路段加粗。走过的路是实墨线，没走的是点线。右下角是图例
- 战斗和灰盒场景是同一套界面；打完后奖励面板叠在战场上（金币、遗物、药水逐项点领，卡牌三选一可以跳过），「继续前进」回地图
- 商店：上排 5 张牌、下排遗物和药水（悬停看说明），右边删卡服务；买不起的价格标血色。篝火：休息 / 锻造（悬停看升级后的样子）/ 静思。事件：右边选项里的代价按功能色标出（生命血色、理智上限深渊青、金币金色）
- 顶栏：生命、金币、药水（不在战斗里点药水可以丢掉）、遗物、层数 / 回合；「牌组」看整副牌，「菜单」里可以保存并回到标题、放弃这一局
- 存档在 `Application.persistentDataPath/run.json`，格式和控制台 `run` 模式的存档一样（`View/Run/RunSaves.cs` 对应 `Tools/Shared/RunSaves.cs`）。进入节点、战斗结束、回到地图时自动写；回到标题、关游戏时再存一次当前状态；通关或倒下后删档。战斗中退出，读档后这场战斗从头开始，起手和意图都和第一次一样（06 §8）

- 遥测（策划/07 §6.2）：每次存档顺带写这一局的 `persistentDataPath/telemetry/开局时间_种子.json`（`View/Run/TelemetryFiles.cs`），放弃、开新局覆盖旧档时记成放弃。标题画面右下角显示版本号，「打开试玩记录」打开这个文件夹。汇总用 `Tools` 的 `telemetry` 子命令

结构：`RunBootstrap`（载入数据、标题画面、存档路径）→ `RunScreen`（持有 RunEngine，按阶段切面板，所有操作变成 `RunCommand`）→ `MapView` / `RunPanels`（奖励、选择、商店、篝火、事件、结算）/ `BattleScreen`（跑局模式：挂在跑局画布下，借用跑局的顶栏，命令经 RunEngine 转发）。面板每次都按 `RunEngine.Options()` 重画，规则判断都在内核里。事件、商店（商贩）、篝火、标题画面用 ArtDatabase 插图表里的图（10 §8 的 P2），没有图时退回节点图标和码头背景；事件界面悬停会扣理智上限的选项时，插图的癫狂层浮出来。

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
- `AudioTests`：战斗里出牌、防御、悬停、结束回合时 AudioDirector 收到的音效 ID；能量不足被拒；节流（同一 ID 80 ms 内最多 2 个）和没有素材时不报错；`BattleSounds` 的映射（命中三档、挡住 / 击破 / 清格挡、理智音阶和来源）
- `RunFlowTests`：标题开局、地图卷动和悬停；一局从出发打到 Boss 通关（生命给到 999，路线预先规划成走遍 6 种房间），每一步核对面板画出了内核的全部选项、顶栏和 RunState 一致；战斗中保存退出再继续（战斗从头开始，哈希和第一次进入时一致）、奖励界面换场景后继续；牌组弹窗、丢药水；遥测文件的结果（没打完 / 通关 / 放弃）、节点数和用时。存档和遥测都写到临时目录，不碰玩家的

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -projectPath Deeptide -runTests -testPlatform PlayMode -testResults Logs/playmode.xml -logFile Logs/playmode.log
```

编辑器里打开着这个工程时，batchmode 会因为工程被占用而失败，先关掉编辑器，或者直接用 Window → General → Test Runner。

## 声音

音效在 `Scripts/View/Audio/`（策划/13 §6.1）：`AudioDirector` 常驻、自带 AudioListener，`BattleSounds` 把战斗事件翻译成音效 ID，`AudioDatabase`（`Audio/Resources/AudioDatabase.asset`）查片段，没有素材的 ID 不出声。素材的源文件和合成脚本在 `AudioSource/`，改完用菜单「Deeptide → 音频 → 打包音效」导进来。「设置」面板里「声音」一档一档切主音量。音乐还没接。

## 打包试玩版

菜单「Deeptide → 打包 Windows」，或者关掉编辑器后：

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -quit -projectPath Deeptide -executeMethod Deeptide.EditorTools.BuildPlayer.Batch -logFile Deeptide/Logs/build.log
```

64 位、Mono 后端，输出 `Builds/Deeptide_Windows/`（不进仓库），复制 `BuildExtras/` 下的试玩说明，再压成 `Builds/Deeptide_版本号.zip`，发给试玩的人就是这个 zip。发新包前先改 Player Settings 的 Version，遥测按它区分版本。exe 和编辑器的存档、遥测在同一个目录（`AppData/LocalLow/DefaultCompany/Deeptide/`）。

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
│  ├─ View/      ← Game.View.asmdef：Battle/（战斗界面）、Run/（标题、地图、奖励 / 商店 / 篝火 / 事件、存档）、Common/（UI 搭建、字体、配色）
│  └─ Editor/    ← Game.Editor.asmdef：工程设置，以后放数据校验、卡牌预览
├─ Data/         ← cards.csv / enemies.csv …（场景里 BattleBootstrap 引用这些 TextAsset）
├─ Scenes/       ← Run（整局，Build Settings 第一个）、Battle_Greybox（单场灰盒）；主菜单以后单独做，现在标题画面在 Run 里
├─ Settings/     ← URP-2D 渲染管线资源
├─ Tests/        ← PlayMode 测试
└─ Art/ Audio/ Prefabs/ Shaders/
```

Core 的测试和模拟器在 `../Tools/`，不需要打开 Unity 就能运行。
