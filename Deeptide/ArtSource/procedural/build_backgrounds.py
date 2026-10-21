"""画战斗背景 / 地图底图：python build_backgrounds.py docks chapel map …

每张是 backgrounds/<ID>.py：SIZE = 导出尺寸，draw(c) 在同尺寸的画布上画（背景是低对比的淡排线，10 §11.1「工作画布同导出」）。
墨层在游戏里着淡墨（10 §3：背景只用淡墨色阶，墨覆盖率 ≤ 40%），癫狂层着深渊青、按 _Lunacy 显现。
导出 ../backgrounds/<ID>_ink.png、_mad.png，给 ArtPacker 打包成打包贴图。预览 preview/bg_<ID>.png：清醒、癫狂，
战斗背景上叠一层版式参考（地平线、调查员和敌人区、顶栏、手牌区）。
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
from build import PAPER, TEAL, font, save_layer  # noqa: E402

OUT = HERE.parent / 'backgrounds'
INK_MID = np.array([0x4A, 0x45, 0x3C]) / 255
MAX_INK = 0.42


def view(ink, mad, lunacy):
    rgb = PAPER * (1 - ink[..., None]) + INK_MID * ink[..., None]
    m = (mad * lunacy)[..., None]
    return rgb * (1 - m) + TEAL * m


def guides(img, w, h):
    """1920 × 1080 的版式（BattleScreen）按比例画到背景上。"""
    d = ImageDraw.Draw(img)
    k = w / 1920
    y = lambda v: int(v * k)
    d.line([(0, y(640)), (w, y(640))], fill=(200, 60, 60), width=2)                       # 地平线（脚底）
    d.rectangle([y(500 - 110), y(640 - 340), y(500 + 110), y(640)], outline=(200, 60, 60), width=2)   # 调查员
    d.rectangle([y(900), y(640 - 470), y(1820), y(640)], outline=(60, 60, 200), width=2)  # 敌人区（Boss 高度）
    d.rectangle([0, 0, w, y(64)], outline=(120, 120, 120), width=2)                       # 顶栏
    d.rectangle([y(440), y(740), y(1480), h], outline=(120, 120, 120), width=2)          # 手牌


def build(bid, preview_dir):
    mod = importlib.import_module(f'backgrounds.{bid}')
    importlib.reload(mod)
    w, h = mod.SIZE
    t0 = time.time()
    c = etch.Canvas(w, h, seed=sum(map(ord, bid)))
    mod.draw(c)
    cap = getattr(mod, 'MAX_INK', MAX_INK)
    ink = np.clip(c.layers['ink'], 0, 1) * cap
    mad = np.clip(c.layers['mad'], 0, 1) * getattr(mod, 'MAX_MAD', 0.6)
    OUT.mkdir(parents=True, exist_ok=True)
    save_layer(ink, OUT / f'{bid}_ink.png')
    p = OUT / f'{bid}_mad.png'
    if mad.max() > 0.01:
        save_layer(mad, p)
    elif p.exists():
        p.unlink()
    small = (w // 3, h // 3) if h < w else (w // 4, h // 4)
    sane = Image.fromarray((view(ink, mad, 0) * 255).astype(np.uint8)).resize(small, Image.LANCZOS)
    madv = Image.fromarray((view(ink, mad, 0.85) * 255).astype(np.uint8)).resize(small, Image.LANCZOS)
    if getattr(mod, 'GUIDES', True):
        guides(sane, *small)
    pad = 16
    sheet = Image.new('RGB', (small[0] * 2 + pad * 3, small[1] + pad * 2 + 30), (0x30, 0x2E, 0x2A))
    ImageDraw.Draw(sheet).text((pad, 6), f'{bid} {getattr(mod, "NAME", "")}  ·  清醒（带版式参考） / 癫狂', fill=(0xE8, 0xE0, 0xCC),
                               font=font(20))
    sheet.paste(sane, (pad, 30 + pad))
    sheet.paste(madv, (pad * 2 + small[0], 30 + pad))
    preview_dir.mkdir(parents=True, exist_ok=True)
    sheet.save(preview_dir / f'bg_{bid}.png')
    print(f'{bid}: {time.time() - t0:.1f}s  {w}x{h}  ink mean {ink.mean():.3f} max {ink.max():.2f}  mad {mad.max() > 0.01}')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('ids', nargs='*')
    ap.add_argument('--preview', default=str(HERE / 'preview'))
    args = ap.parse_args()
    ids = args.ids or sorted(p.stem for p in (HERE / 'backgrounds').glob('*.py') if not p.stem.startswith('_'))
    for i in ids:
        build(i, pathlib.Path(args.preview))


if __name__ == '__main__':
    main()
