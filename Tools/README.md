# Tools：战斗内核的测试与模拟器

战斗内核的源码在 `Deeptide/Assets/_Project/Scripts/Core/`（纯 C#，不引用 UnityEngine）。这里的工程直接链接那份源码，在 Unity 之外编译、测试、模拟。

| 工程 | 作用 |
|---|---|
| `Deeptide.Core` | netstandard2.1 + C# 9 编译内核。和 Unity 6 的语言版本一致：这里能编译过，Unity 里也能编译过 |
| `Deeptide.Tests` | NUnit 单元测试：公式、理智、恐惧、敌人 AI、回合流程、回放确定性 |
| `Deeptide.Sim` | 控制台：手动对战、AI 自动对战、批量模拟统计 |

需要 .NET 10 SDK。以下命令都在 `Tools/` 目录下执行。

## 跑测试

```bash
dotnet test
```

## 手动对战

```bash
dotnet run --project Deeptide.Sim -- play --deck C --enc 2 --seed 42
```

- `--deck`：`A` 守夜 / `B` 堕落 / `C` 混合 / `starter` 起始牌组，和纸面原型的牌组相同
- `--enc`：`1` 教团侍僧 / `2` 侍僧 + 呓语者 / `3` 失魂镇民 ×2
- `--seed`：随机种子。同一个种子加同样的操作，结果完全一样
- 对战中的输入：`2` 打第 2 张牌（需要目标时会提示）；`2 1` 打第 2 张牌，目标是 1 号敌人；`e` 结束回合；`a` 让 AI 打这一回合；`l` 看完整日志；`q` 退出

对战结束时会打印一条现成的 `replay` 命令，复制下来运行，就能看到这一局的完整日志：

```bash
dotnet run --project Deeptide.Sim -- replay --deck C --enc 2 --seed 42 --cmds "play 4 1, play 0 -1, end, ..."
```

## AI 自动对战（输出完整日志）

```bash
dotnet run --project Deeptide.Sim -- auto --deck B --enc 3 --seed 7
```

## 批量模拟

```bash
dotnet run --project Deeptide.Sim -- batch --n 500
```

统计每个「牌组 × 遭遇」组合的胜率、平均回合数、平均掉血、崩溃次数、癫狂回合占比和清明回合占比。

可以用 `--set` 临时改参数做对比，用 `--style sane|mad|mixed` 指定 AI 打法：

```bash
dotnet run --project Deeptide.Sim -- batch --deck C --style mad --set "erosion=3;U09.PerPoint=3"
```

`--set` 支持：`erosion`（侵蚀起始回合）、`lunacy`（癫狂线）、`hp` / `hpHard`（全部 / 困难池敌人生命倍率）、`starter=base|prayer`、`卡牌ID.变量名=值`、`敌人ID.hp=最小-最大`。只在这一次运行中生效，不改源码。

## 自动调参

```bash
dotnet run --project Deeptide.Sim -c Release -- tune --n 150 --top 5 --confirm 500
```

在参数网格上跑遍所有「牌组 × 遭遇」，按 策划/07 §7 的目标函数打分，前几名再用更多场次复核。用 `--grid "erosion=3,4;U09.PerPoint=2,3"` 可以自己指定网格；默认网格 96 组，大约 2 分钟。

AI 是一步贪心：通过重放预演每个候选出牌的结果，所以它能看到真实的抽牌顺序。它打得比真人更顺，**只适合做相对比较**（改动前后、A 牌组对 B 牌组），绝对数值不能直接当成平衡结论。
