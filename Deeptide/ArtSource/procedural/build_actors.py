"""画角色图：python build_actors.py acolyte player …（不给 ID 就画全部）。

每个角色是 actors/<ID>.py：SIZE = (宽, 高) 是导出尺寸（1080p 显示尺寸 × 2，10 §5.1、§11.1），draw(c) 在 2 倍的工作画布上画
墨 / 深渊遮罩 / 癫狂层 / 轮廓（fill）四层。约定（ActorArt.cs）：脚底在底边往上 4% 处、左右居中；敌人面朝左，调查员面朝右。
导出到 ../actors/<ID>_ink.png 等，给 ArtPacker 打包；预览在 preview/actor_<ID>.png：清醒、癫狂（_Lunacy = 0.85）、剪影测试、1080p 实际大小。
"""
from __future__ import annotations

import argparse
import importlib
import pathlib
import sys
import time

import numpy as np
from PIL import Image, ImageDraw

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import etch  # noqa: E402
from build import PAPER, INK, TEAL, LUNACY, font, save_layer  # noqa: E402

OUT = HERE.parent / 'actors'
FEET_PAD = 0.04


def stage_view(ink, abyss, mad, fill, lunacy):
    """照搬 Deeptide/UI/Stage 的打包模式，画在纸底上。"""
    rgb = PAPER * (1 - ink[..., None]) + INK * ink[..., None]
    rgb = rgb * (1 - abyss[..., None]) + TEAL * abyss[..., None]
    m = (mad * lunacy)[..., None]
    rgb = rgb * (1 - m) + LUNACY * m
    a = np.maximum(fill, mad * lunacy)[..., None]
    bg = PAPER * 0.97
    out = rgb * a + bg * (1 - a)
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8))


def build(aid, preview_dir):
    mod = importlib.import_module(f'actors.{aid}')
    importlib.reload(mod)
    ew, eh = mod.SIZE
    t0 = time.time()
    c = etch.Canvas(ew * 2, eh * 2, seed=sum(map(ord, aid)))
    mod.draw(c)
    # 墨只留在轮廓里（癫狂层可以长出去）
    c.layers['ink'] *= np.clip(c.layers['fill'] * 1.5, 0, 1)
    c.layers['abyss'] *= np.clip(c.layers['fill'] * 1.5, 0, 1)
    ink, abyss, mad, fill = (c.export(k) for k in ('ink', 'abyss', 'mad', 'fill'))
    OUT.mkdir(parents=True, exist_ok=True)
    save_layer(ink, OUT / f'{aid}_ink.png')
    save_layer(fill, OUT / f'{aid}_fill.png')
    for k, arr in (('abyss', abyss), ('mad', mad)):
        p = OUT / f'{aid}_{k}.png'
        if arr.max() > 0.01:
            save_layer(arr, p)
        elif p.exists():
            p.unlink()

    sane = stage_view(ink, abyss, mad, fill, 0.0)
    madv = stage_view(ink, abyss, mad, fill, 0.85)
    sil = Image.fromarray(((1 - np.clip(fill, 0, 1)) * 255).astype(np.uint8)).convert('RGB')
    small = sane.resize((max(ew // 2, 1), max(eh // 2, 1)), Image.LANCZOS)          # 1080p 实际显示大小
    pad = 16
    W = ew * 3 + small.width + pad * 5
    H = eh + pad * 2 + 36
    sheet = Image.new('RGB', (W, H), (0x30, 0x2E, 0x2A))
    d = ImageDraw.Draw(sheet)
    d.text((pad, 6), f'{aid} {getattr(mod, "NAME", "")}  ·  清醒 / 癫狂 / 剪影 / 1080p', fill=(0xE8, 0xE0, 0xCC), font=font(22))
    x = pad
    for im in (sane, madv, sil, small):
        sheet.paste(im, (x, 36 + pad + (eh - im.height)))
        x += im.width + pad
    feet = 36 + pad + eh - int(eh * FEET_PAD)
    d.line([(pad, feet), (W - pad, feet)], fill=(0x8E, 0x2A, 0x2A), width=1)          # 脚底线
    preview_dir.mkdir(parents=True, exist_ok=True)
    sheet.save(preview_dir / f'actor_{aid}.png')
    print(f'{aid}: {time.time() - t0:.1f}s  {ew}x{eh}  ink {ink[fill > 0.5].mean():.2f}  mad {mad.max() > 0.01}  abyss {abyss.max() > 0.01}')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('ids', nargs='*')
    ap.add_argument('--preview', default=str(HERE / 'preview'))
    args = ap.parse_args()
    ids = args.ids or sorted(p.stem for p in (HERE / 'actors').glob('*.py') if not p.stem.startswith('_'))
    for i in ids:
        build(i, pathlib.Path(args.preview))


if __name__ == '__main__':
    main()
