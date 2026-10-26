# AudioSource（音频源文件）

音频源文件放这里。目录在 `Assets/` 外面，Unity 不会导入；`.wav`、`.ogg` 走 Git LFS（根目录 `.gitattributes`）。规格见 [策划/13 §6](../../策划/13_音乐策划.md)。

```
AudioSource/
├─ music/       可循环曲目的分轨：M02_base / main / clarity / deep（等长、首尾相接）
├─ stingers/    短乐句：S02_bells（回潮的后半段，前半段的倒放是运行时做的）
├─ sfx/         音效：R01_<剩下的理智>、R02_<恢复后的理智>、C01_<档>_<变体>
├─ procedural/  合成脚本（下面）
└─ CREDITS.md   采样来源登记（13 §7.3）
```

全部是 48 kHz / 24 bit WAV。文件名里的 ID 和策划/13 一致。

## 程序化合成

`procedural/` 下是用代码合成的音频（策划/13 §7.2 路线 E）。不用任何采样，所以 `CREDITS.md` 目前是空的。

| 文件 | 内容 |
|---|---|
| `dsp.py` | 底层：音高、包络、滤波、混响冲激响应、声像、BS.1770 响度表、真峰值、24 bit WAV 读写 |
| `instruments.py` | 调色板（13 §2.1）：潮钟、旧钢琴、玻璃、簧风琴、低音提琴拨弦、闷鼓、小提琴泛音、深潮的呼吸、弓擦金属、浪、木头吱呀、灯芯、闷击 |
| `mixer.py` | 离线模拟游戏里的混音：叠夜拷贝、按理智分级的低通 / 混响 / 深渊层 / 总线补偿、理智音效压低音乐。**游戏里的 AudioDirector 要用同一组常数**（`NIGHTS`、`LEVELS`） |
| `cues/M02.py` | 战斗曲「潮下」样张：10 小节、76 BPM、四条分轨 |
| `cues/S02.py` | 回潮：潮钟七下 + 浪；预览里接上倒放 |
| `cues/sanity.py` | R01 / R02 / C01 |

```bash
cd Deeptide/AudioSource/procedural
python build.py                  # 全做，约 4 分钟；也可以只做几个：python build.py sanity S02
python build.py --spectro        # 顺便画频谱图（preview/_spec_*.png）
python build.py --calibrate      # 实测各档理智的整体响度，给出 mixer.LEVELS 的总线补偿建议值
```

依赖 numpy、scipy（画频谱图要 matplotlib）。Windows 终端要先 `set PYTHONIOENCODING=utf-8`（PowerShell 是 `$env:PYTHONIOENCODING='utf-8'`），不然中文输出是乱码。

输出：素材写到 `music/`、`stingers/`、`sfx/`（覆盖同名文件）；试听预览写到 `procedural/preview/`（不进仓库）：

- `listen.html`：试听页。三件样张、各档理智的对照、闭眼听理智的小测验。双击用浏览器打开
- `M02_sweep.wav`：理智 10 → 1 → 5 的扫描，混了理智音效；`M02_L<理智>.wav` 各档单独一个循环；`M02_L1_5min.wav` 打到第 5 分钟时的错位
- `S02_demo.wav`：一段战斗 → 致命一击 → 倒放 → 静 → 钟
- `report.txt`：每个文件的长度、整体响度、最大瞬时响度、真峰值，循环曲的接缝落差

`build.py` 的检查：写文件时峰值超过 0 dBFS 直接报错（不偷偷削波）；报告里真峰值超过 −1 dBTP 会标出来；接缝落差 < 1× 就不会有咔哒声。

## 改的时候注意

- `mixer.LEVELS` 最后一列「音乐总线补偿」是实测出来的：改了分轨的响度、叠夜的音量或混响送量以后，跑一次 `--calibrate`，把建议值抄回去，让各档的整体响度保持一致（13 支柱 4）
- `mixer.ir()` 的混响校准到响度增益 0 dB；换混响参数后不用手动调，它会自己按粉红噪声重新校准
- 「主」分轨会被运行时拷贝、错位、变调后叠在自己身上（13 §3.2）。往里加东西时，旋律要稀疏、长音多；密集的快速音型叠起来会糊成噪声
- 理智音效（R 系列）按最大瞬时响度 −16 LUFS 定电平，比音乐（各档约 −24 LUFS）高 6 dB 以上；R01 的灯芯嘶声单独压到 −10 dBFS 峰值，并提前 30 ms 出声，免得和琴音的起音叠成尖峰
