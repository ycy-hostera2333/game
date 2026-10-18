# Tools：内核的测试与模拟器

内核源码在 `Deeptide/Assets/_Project/Scripts/Core/`（纯 C#，不引用 UnityEngine），内容数据在 `Deeptide/Assets/_Project/Data/*.csv`。这里的工程直接链接那份源码、从磁盘读那份数据，在 Unity 之外编译、测试、模拟。

| 工程 | 作用 |
|---|---|
| `Deeptide.Core` | netstandard2.1 + C# 9 编译内核。和 Unity 6 的语言版本一致：这里能编译过，Unity 里也能编译过 |
| `Deeptide.Tests` | NUnit 测试：公式、理智、恐惧、每张牌 / 遗物 / 药水 / 敌人、数据校验、事件流重建、战斗复制（Fork）、地图约束、整局回放、存读档 |
| `Deeptide.Sim` | 控制台：单场对战 / 批量模拟 / 调参，整局的文字版、AI 跑局和批量统计 |
| `Shared/` | 两个工程共用：从磁盘找 Data 目录并载入（`DataFiles`），存档 JSON 读写（`RunSaves`，Newtonsoft） |

需要 .NET 10 SDK。以下命令都在 `Tools/` 目录下执行。

## 跑测试

```bash
dotnet test
```

测试启动时载入真实的 `Data/*.csv`，数据校验有任何错误整套测试直接失败。每次推送到 GitHub 都会自动跑同样的检查（`.github/workflows/test.yml`）：构建（警告视为错误）、单元测试、批量模拟冒烟测试。

Claude Code 云端会话启动时，`.claude/hooks/session-start.sh` 会自动安装 .NET 10 SDK 并还原 NuGet 包，会话里可以直接运行这里的所有命令。

## 内容数据

| 文件 | 内容 | 对应的类注册表 |
|---|---|---|
| `cards.csv` | 42 张牌（40 张角色牌 + 疯语 + 缠网） | `CardClasses` |
| `enemies.csv` | 12 种敌人 + 调试用稻草人；`moves` 列声明招式名 | `EnemyClasses` |
| `encounters.csv` | 简单池 4 组、困难池 7 组、精英 3 组、Boss | — |
| `relics.csv` | 20 件遗物 | `RelicClasses` |
| `potions.csv` | 8 种药水 | `PotionClasses` |
| `events.csv` | 8 个事件；`options` 列是「选项ID=文本」，用 `|` 分隔 | `EventClasses` |

改数值只改 CSV；改逻辑要改类。载入时校验：每一行都有类、每个类都有行；文本里的 `{变量}` 都有定义；升级变量存在；敌人招式和事件选项和代码一一对应。所有错误一次性列出来。

## 单场战斗

```bash
dotnet run --project Deeptide.Sim -- play --deck C --enc 2 --seed 42      # 手动对战
dotnet run --project Deeptide.Sim -- auto --deck B --enc 3 --seed 7       # AI 打一场，输出完整日志
dotnet run --project Deeptide.Sim -- replay --deck C --enc 2 --seed 42 --cmds "play 4 1, play 0 -1, end, ..."
```

- `--deck`：`starter` 起始牌组 / `A` 守夜 / `B` 堕落 / `C` 混合 / `D` 传染；`B0` / `C0` 是纸面原型时期的旧版 B / C（用来和重构前对比）
- `--enc`：`1` 教团侍僧 / `2` 侍僧 + 呓语者 / `3` 失魂镇民 ×2，或者 `encounters.csv` 里的 ID（如 `elite_nun`、`boss_priest`）
- 对战输入：`2` 打第 2 张牌（需要目标时会追问）；`2 1` 打第 2 张、目标 1 号敌人；`p1` 用第 1 格药水；等待选择时输入选项编号；`e` 结束回合；`a` 让 AI 打这一回合；`l` 看完整日志；`q` 退出

## 批量模拟和调参

```bash
dotnet run --project Deeptide.Sim -c Release -- batch --n 500                                  # A/B/C/D × 原型遭遇 1/2/3
dotnet run --project Deeptide.Sim -c Release -- batch --deck A,B,C,D --enc pool:hard          # 整个困难池
dotnet run --project Deeptide.Sim -c Release -- batch --deck starter --enc pools               # 全部 15 组遭遇
dotnet run --project Deeptide.Sim -c Release -- tune --n 150 --top 5 --confirm 500            # 网格搜索（策划/07 §7）
```

统计每个「牌组 × 遭遇」的胜率、平均回合、掉血、崩溃次数、癫狂 / 清明回合占比。`--style sane|mad|mixed` 指定 AI 打法。

`--set` 临时改参数（只在这一次运行中生效，不改文件），所有模式都支持：`erosion`（侵蚀起始回合）、`lunacy`（癫狂线）、`clarity`（清明余量）、`hp` / `hpHard`（生命倍率）、`starter=base|prayer`、`卡牌ID.变量名=值`、`卡牌ID.cost=N` / `卡牌ID.cost_up=N`、`敌人ID.数值名=值`、`敌人ID.hp=最小-最大`。例：`--set "drowned_priest.hp=200-200;U05.cost=1"`。

## 整局

```bash
dotnet run --project Deeptide.Sim -- map --seed 3                     # 字符画预览一张地图（和 run 同一种子的地图一样）
dotnet run --project Deeptide.Sim -- map --n 1000                     # 批量生成：约束违规、房间占比、一条路线最多能打几个精英
dotnet run --project Deeptide.Sim -- run --seed 42                    # 文字版完整一局，进入节点 / 打完战斗 / 回到地图时自动存档
dotnet run --project Deeptide.Sim -- run --load run_save.json         # 读档继续（战斗中退出的话，这场战斗从头开始）
dotnet run --project Deeptide.Sim -- runview --seed 42 --cmds-file playtest.txt   # 重放命令文件并显示当前界面
dotnet run --project Deeptide.Sim -- runauto --seed 3 --arch watch --log boss     # AI 打一局，逐节点打印选择和 Boss 战日志
dotnet run --project Deeptide.Sim -c Release -- runbatch --n 1000     # 四个流派各 1000 局
```

- `run`：地图 / 奖励 / 商店 / 篝火 / 事件都是「输入选项编号」；`d2` 丢第 2 格药水；`deck` 看牌组；`map` 看地图；`a` 让 AI 走一步、`A` 让 AI 打完当前战斗；`q` 存档退出。打完会把整局命令写进 `run_commands.txt`，可以用 `runreplay --seed N --cmds-file run_commands.txt` 重放
- `runview`：不开交互进程，每次往命令文件里追加命令（`pick 2`、`play 0 1`、`end`、`choose 0`、`potion 1 -1`、`drop 0`，`#` 后面是注释）再运行一次。`--from K` 只打印第 K 条命令之后发生的事，`--deck 1` / `--map 1` 附带牌组和地图。Claude 的跑局试玩就是这样打的（`原型/试玩_跑局/`）
- `runbatch`：输出 策划/07 §5.2 的生命账本（到达 Boss 时的生命）、§5.3 的指标（三流派胜率差、进入癫狂的战斗占比、堕落崩溃次数、守夜清明回合占比）、各类战斗的回合和掉血、经济、死因和每张牌的拾取率。`--arch watch,fall,contagion,mixed` 选流派，`--cards 0` 不打印拾取率

## AI

| AI | 用在哪 | 做法 |
|---|---|---|
| 战斗 AI | 所有模式 | 默认**束搜索**：每回合开始时在复制出的战斗（`BattleEngine.Fork`）上搜索这一回合的出牌顺序（束宽 6、最多 8 张），按估值函数取最好的一条照着打完；`--ai greedy` 换回原来的一步贪心。预演看得到真实的抽牌结果，相当于知道牌序 |
| 跑局 AI | `runauto` / `runbatch` / `run` 里的 `a` | 规则：按流派偏好 + 一张卡牌强弱表给牌打分，分数够高才拿；商店先删打击 / 疯语再买高分牌；篝火血少休息、否则锻造；选路往后看 4 层；精英 / Boss 第一回合用增益药水，快死时用防御药水 |

两个 AI 都**只适合做相对比较**（改动前后、流派之间），绝对数值不能直接当成平衡结论：战斗 AI 知道牌序、算得比人准，跑局 AI 的构筑比熟练玩家差。调试开关：`--peek 1`（癫狂时偷看真实意图）、`--battle-style sane|mad|mixed`（所有流派用同一种战斗打法）、`runauto --debug 1`（打印选路得分）。
