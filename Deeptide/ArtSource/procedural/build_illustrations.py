"""画插图（策划/10 §8、§11.1 的 P2）：python build_illustrations.py event_mirror campfire …（不给 ID 就画全部）。

每张是 illustrations/<ID>.py：SIZE = 导出尺寸（事件插图、篝火 1920 × 1280，主视觉 3840 × 2160），draw(c) 在同尺寸的画布上画
（10 §11.1「工作画布同导出」）。和卡图一样是全对比度的版画：墨着纯墨，深渊遮罩着深渊青；不像战斗背景那样压成淡墨。
癫狂层平时不显示，游戏里悬停会扣理智的选项时浮出来（深渊青）。
导出 ../illustrations/<ID>_ink.png、_abyss.png、_mad.png，ArtPacker 打包进 ArtDatabase 的插图表。
预览 preview/ill_<ID>.png：清醒、癫狂层浮出，按 Deeptide/UI/Etching 着色，下面一排是游戏里的显示尺寸。
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
from build import INK, PAPER, TEAL, font, save_layer  # noqa: E402

OUT = HERE.parent / 'illustrations'


def view(ink, abyss, mad, reveal):
    """照搬 Etching 的清醒面着色；癫狂层按事件界面的做法着深渊青。"""
    rgb = PAPER * (1 - ink[..., None]) + INK * ink[..., None]
    rgb = rgb * (1 - abyss[..., None]) + TEAL * abyss[..., None]
    m = (mad * reveal)[..., None]
    return np.clip(rgb * (1 - m) + TEAL * m, 0, 1)


def build(iid, preview_dir):
    mod = importlib.import_module(f'illustrations.{iid}')
    importlib.reload(mod)
    w, h = mod.SIZE
    t0 = time.time()
    c = etch.Canvas(w, h, seed=sum(map(ord, iid)))
    mod.draw(c)
    ink, abyss, mad = (np.clip(c.layers[k], 0, 1) for k in ('ink', 'abyss', 'mad'))
    OUT.mkdir(parents=True, exist_ok=True)
    save_layer(ink, OUT / f'{iid}_ink.png')
    for k, arr in (('abyss', abyss), ('mad', mad)):
        p = OUT / f'{iid}_{k}.png'
        if arr.max() > 0.01:
            save_layer(arr, p)
        elif p.exists():
            p.unlink()

    # 预览：上面两张大图（清醒 / 癫狂层浮出），下面是游戏里的显示尺寸（事件 880 宽）和缩略图
    big = (w // 2, h // 2) if w <= 2000 else (w // 4, h // 4)
    sane = Image.fromarray((view(ink, abyss, mad, 0) * 255).astype(np.uint8))
    madv = Image.fromarray((view(ink, abyss, mad, 0.8) * 255).astype(np.uint8))
    pad = 16
    disp = (880, int(880 * h / w))
    thumb = (330, int(330 * h / w))
    sheet = Image.new('RGB', (big[0] * 2 + pad * 3, 40 + big[1] + pad * 2 + disp[1] + pad), (0x30, 0x2E, 0x2A))
    ImageDraw.Draw(sheet).text((pad, 8), f'{iid} {getattr(mod, "NAME", "")}  ·  清醒 / 癫狂层浮出  ·  下排：游戏里的显示尺寸、缩略图',
                               fill=(0xE8, 0xE0, 0xCC), font=font(22))
    sheet.paste(sane.resize(big, Image.LANCZOS), (pad, 40))
    sheet.paste(madv.resize(big, Image.LANCZOS), (pad * 2 + big[0], 40))
    y = 40 + big[1] + pad
    sheet.paste(sane.resize(disp, Image.LANCZOS), (pad, y))
    sheet.paste(sane.resize(thumb, Image.LANCZOS), (pad * 2 + disp[0], y))
    sheet.paste(madv.resize(thumb, Image.LANCZOS), (pad * 3 + disp[0] + thumb[0], y))
    preview_dir.mkdir(parents=True, exist_ok=True)
    sheet.save(preview_dir / f'ill_{iid}.png')
    print(f'{iid}: {time.time() - t0:.1f}s  {w}x{h}  ink mean {ink.mean():.2f}  abyss {abyss.max() > 0.01}  mad {mad.max() > 0.01}')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('ids', nargs='*')
    ap.add_argument('--preview', default=str(HERE / 'preview'))
    args = ap.parse_args()
    ids = args.ids or sorted(p.stem for p in (HERE / 'illustrations').glob('*.py') if not p.stem.startswith('_'))
    for i in ids:
        build(i, pathlib.Path(args.preview))


if __name__ == '__main__':
    main()
