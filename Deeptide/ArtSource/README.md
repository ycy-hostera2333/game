# ArtSource（美术源文件）

美术源文件放这里。目录在 `Assets/` 外面，Unity 不会导入；`.png`、`.psd`、`.kra` 走 Git LFS（见根目录 `.gitattributes`，用 Clip Studio 的话补一行 `*.clip`）。规格见 [策划/10 §11](../../策划/10_美术策划.md)，排期见 [策划/11](../../策划/11_卡图排期.md)。

```
ArtSource/
├─ style/        风格锚点：通过验收的样张、线宽笔刷、色板（10 §12.4）
├─ cards/        卡图分层 PNG + 源文件（.psd / .kra）
├─ actors/       角色图（敌人、调查员）分层 PNG
├─ backgrounds/  战斗背景、地图底图的分层 PNG（_ink 淡墨、_mad 癫狂层）
├─ illustrations/ 插图：事件 8 张、篝火、主视觉（全对比度，_ink / _abyss / _mad）
├─ sprites/      单色精灵：图标、卡框套件、UI 套件、地图节点、特效帧（白色 + alpha，子目录只用来分类）
└─ CREDITS.md    公有领域素材登记（10 §12.3）
```

## 卡图怎么交

1. 工作画布 1248 × 864，按 10 §3 的线宽画；导出 **624 × 432** 的分层 PNG，放进 `cards/`：

   | 文件 | 内容 | 必需 |
   |---|---|---|
   | `C06_ink.png` | 墨线（插图本体） | ✓ |
   | `C06_abyss.png` | 深渊遮罩：要着深渊青的地方（深海之物的眼、触须的荧光） | |
   | `C06_mad.png` | 癫狂层：只在癫狂面显现的墨线（10 §4.6「癫狂层」一列）。双面牌才有 | |

2. **每一层都只用黑色画**：透明底上的黑线、白底上的黑线、灰色排线都行。打包时按「覆盖率 = 不透明度 × (1 − 亮度)」读取，不需要上色，颜色由着色器给（清醒面纸底墨线，癫狂面深渊底青线）
3. 在 Unity 里点菜单「Deeptide → 美术 → 打包卡图和角色」：生成 `Assets/_Project/Art/Cards/card_C06.png`，并登记进 `Art/Resources/ArtDatabase.asset`。进游戏后这张牌的插图窗就从灰盒首字换成卡图
4. 在 [策划/11 §4](../../策划/11_卡图排期.md) 里把这张的状态改成「完成」，填实测工时；用了公有领域素材的，登记进 `CREDITS.md`

命令行打包（编辑器没开着时）：

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -projectPath Deeptide -executeMethod Deeptide.EditorTools.ArtPacker.Batch -quit
```

## 程序化卡图

`procedural/` 下是用代码画的卡图（策划/11 §6）：`etch.py` 是版画画笔（排线、变宽勾线、光源在左上，线宽按 10 §3），`cards/<ID>.py` 每张一个 `draw(c)`，在 1248 × 864 的工作画布上画墨、深渊遮罩、癫狂层三层。

```bash
cd Deeptide/ArtSource/procedural
python build.py C06 B01      # 不给 ID 就画全部
```

输出 `cards/<ID>_ink.png` 等分层 PNG（覆盖已有的同名文件），预览图在 `procedural/preview/`（不进仓库）：按插图窗裁切的清醒面 / 癫狂面、单独的癫狂层和手牌尺寸缩略图。之后照常跑上面的打包。依赖 numpy、scipy、Pillow；全部 42 张约 6 分钟。

- `python peek.py C05 U03`：只画这几张，把清醒面 / 癫狂面拼成一张 `preview/_peek.png`，改图时用
- `cards/_common.py`：几张共用的画法（触须、眼睛、远处的小人、往后缩的影子、放射光）；`cards/C02.py` 里的 `lantern` 也被点亮提灯、驱散、惊惧失神复用
- 双面牌的癫狂层只在癫狂面显现，而癫狂面里墨会变成发光的青色，所以癫狂层要画在原图**浅**的地方，压在暗部上会被吃掉

## 角色图怎么交

和卡图一样是分层 PNG，放在 `actors/`，文件名用敌人定义 ID（`Data/enemies.csv` 的 id 列，如 `acolyte_ink.png`），调查员是 `player`，Boss 第二阶段是 `drowned_priest_p2`：

| 文件 | 内容 | 必需 |
|---|---|---|
| `<ID>_ink.png` | 墨线 | ✓ |
| `<ID>_fill.png` | 轮廓（本体的剪影，透明底上涂黑）；角色是透明底素材，没有这层整张图都是纸 | ✓ |
| `<ID>_abyss.png` | 深渊遮罩：任何时候都着深渊青（深海之物的眼、鳃） | |
| `<ID>_mad.png` | 癫狂层：按 `_Lunacy` 显现，在舞台上是发光青，可以长出轮廓之外（影子里的触须） | |

- 尺寸 = 1080p 显示尺寸 × 2（10 §5.1）：中档 680 高、小档 420 高（兽形按「视觉体量」再 × 0.55）、精英 860、Boss 940；宽度随意
- **脚底在图的底边往上 4% 处、左右居中**（`ActorArt.FeetPad`），游戏按这个把脚踩在地平线上
- 敌人面朝左，调查员面朝右；光源在左上
- 打包后输出到 `Assets/_Project/Art/Actors/actor_<ID>.png`，登记进 ArtDatabase 的角色表；没有图的角色继续用灰盒剪影

程序化角色图在 `procedural/actors/<ID>.py`（`SIZE` 是导出尺寸），`python build_actors.py acolyte` 导出分层 PNG，预览在 `procedural/preview/actor_<ID>.png`：清醒、癫狂、剪影测试、1080p 实际大小，红线是脚底线。

## 背景和单色精灵

- **战斗背景** `backgrounds/<ID>_ink.png`（+ 可选 `_mad.png`），2560 × 1440，打包成和卡图一样的打包贴图。游戏里墨着淡墨（10 §3：背景墨覆盖率 ≤ 40%，比角色浅），癫狂层着深渊青、按 `_Lunacy` 显现。`docks` 是普通战斗，`chapel` 是 Boss 战；`map` 是地图底图（1920 × 3400）
- **单色精灵** `sprites/**/<ID>.png`：白色 + alpha，颜色由 Image 给（换色板不用重画）。按 ID 取用：
  - 意图 `intent_*`、状态 `status_*`、被动 `passive_*`、格挡 `icon_block`、遗物 `relic_<遗物 ID>`
  - 卡框 `frame_basic / common / uncommon / rare / erosion`（整卡 720 × 1008 的覆盖层）、窗线脚 `window_*`、流派徽记 `seal_*`
  - UI 套件 `ui_*`（九宫格边距写在 `ArtSprites.cs`）、`icon_heart / draw / discard / exhaust`
  - 地图节点 `node_*`；特效帧 `vfx_hit_a_0 … vfx_hit_c_5`、`vfx_break_0 … 5`、`vfx_crack`
- 没有某个 ID 的图时，游戏继续用灰盒（几何 + 汉字），所以可以一张张替换

## 插图（事件、篝火、主视觉）

10 §8 的 P2：每个事件一张（`event_<事件 ID>`，1920 × 1280，事件界面左边显示 880 × 620）、篝火 `campfire`（同尺寸）、主视觉 `keyart`（3840 × 2160，标题画面的背景）。
商店里的商贩直接用 `event_fog_peddler` 的左半（商贩整个画在 x < 960 里）。

- 和卡图一样是全对比度的版画，分层 `_ink`（必需）、`_abyss`（深渊遮罩，任何时候都着深渊青）、`_mad`（癫狂层）。游戏里用 `Deeptide/UI/Etching` 着清醒面；
  事件界面悬停会扣理智上限的选项时，癫狂层以深渊青浮出来
- 打包进 ArtDatabase 的插图表（`Art/Illustrations/ill_<ID>.png`），和卡图同一个菜单 / batchmode 命令
- 程序化的源在 `procedural/illustrations/<ID>.py`（`python build_illustrations.py event_mirror campfire …`，不给 ID 就画全部，一张 40–80 秒，主视觉约 3 分钟），
  共用画法在 `illustrations/_kit.py`（线宽、夜空、海面、礁石、调查员背影、兜帽人、火苗）。预览 `preview/ill_<ID>.png`：清醒 / 癫狂层浮出 / 游戏里的显示尺寸
- 插图在游戏里缩小一半显示，**线距不要小于 7 像素**，否则缩小后会出摩尔纹；放射排线（`ph_ray`）的 r0 用 `_kit.ray_r0` 取整，不然左侧水平方向会有一道接缝
- 标题字 `logo` 是单色精灵（`procedural/sprites/title.py`，思源宋体 Black 做底再刻排线），在 `sprites/title/`

程序化的源在 `procedural/backgrounds/`（`python build_backgrounds.py docks`）和 `procedural/sprites/`（`python build_sprites.py icons frames ui nodes vfx vfx_big`）。

