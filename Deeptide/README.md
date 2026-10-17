# Deeptide（Unity 工程）

目前只有战斗内核的源码（`Assets/_Project/Scripts/Core/`），还没有场景和表现层。

## 第一次打开

1. 安装 Unity Hub，再通过 Hub 安装 **Unity 6.3 LTS**（6000.3.26f1），模块勾选 Windows Build Support (IL2CPP) 和简体中文
2. 在 Hub 里选「Add → Add project from disk」，选中本目录 `Deeptide/`
3. Unity 会自动生成 `Library/`、`ProjectSettings/`、`Packages/` 等目录。`Library/` 已经写进 `.gitignore`，`ProjectSettings/` 和 `Packages/` 需要提交

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
