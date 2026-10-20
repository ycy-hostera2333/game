# ArtSource（美术源文件）

美术源文件放这里。目录在 `Assets/` 外面，Unity 不会导入；`.png`、`.psd`、`.kra` 走 Git LFS（见根目录 `.gitattributes`，用 Clip Studio 的话补一行 `*.clip`）。规格见 [策划/10 §11](../../策划/10_美术策划.md)，排期见 [策划/11](../../策划/11_卡图排期.md)。

```
ArtSource/
├─ style/        风格锚点：通过验收的样张、线宽笔刷、色板（10 §12.4）
├─ cards/        卡图分层 PNG + 源文件（.psd / .kra）
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
3. 在 Unity 里点菜单「Deeptide → 美术 → 打包卡图」：生成 `Assets/_Project/Art/Cards/card_C06.png`，并登记进 `Art/Resources/ArtDatabase.asset`。进游戏后这张牌的插图窗就从灰盒首字换成卡图
4. 在 [策划/11 §4](../../策划/11_卡图排期.md) 里把这张的状态改成「完成」，填实测工时；用了公有领域素材的，登记进 `CREDITS.md`

命令行打包（编辑器没开着时）：

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -projectPath Deeptide -executeMethod Deeptide.EditorTools.ArtPacker.Batch -quit
```
