"""画卡图：python build.py C06 B01 …（不给 ID 就画全部）。

每张卡图是 cards/<ID>.py 里的 draw(c)，在 1248 × 864 的工作画布上画。导出：
- ../cards/<ID>_ink.png、_abyss.png、_mad.png：624 × 432，透明底黑墨，给 ArtPacker 打包（ArtSource/README.md）
- 预览（--preview 目录，默认 ./preview）：按插图窗裁切、按色板着色的清醒面 / 癫狂面，加手牌尺寸缩略图
"""
from __future__ import annotations

import argparse
import importlib
import math
import pathlib
import sys
import time

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import etch  # noqa: E402

OUT = HERE.parent / 'cards'

# 10 §2.1 色板（和 Ui.cs 的 Palette 一致）
PAPER = np.array([0xE8, 0xE0, 0xCC]) / 255
INK = np.array([0x1C, 0x1A, 0x17]) / 255
TEAL = np.array([0x1A, 0x61, 0x5E]) / 255
ABYSS_CARD = np.array([0x13, 0x24, 0x23]) / 255
PALE = np.array([0xD8, 0xE4, 0xDF]) / 255
LUNACY = np.array([0x4F, 0xC2, 0xB0]) / 255

# 卡牌类型 → 插图窗（CardView.ApplyWindow）
CARD_TYPE = {}


def load_types():
    csv = HERE.parents[1] / 'Assets' / '_Project' / 'Data' / 'cards.csv'
    col = {}
    for line in csv.read_text(encoding='utf-8').splitlines():
        if not line or line.startswith('#'):
            continue
        f = line.split(',')
        if f[0] == 'id':
            col = {name: i for i, name in enumerate(f)}   # 按表头取列，表里加列不会错位
            continue
        CARD_TYPE[f[0]] = (f[col['type']], f[col['name']], f[col['dual']] == '1')


def window_mask(kind, w=624, h=432):
    """插图窗遮罩，照搬 Shapes.Window（它按一半尺寸、y 向上生成）。"""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    x = (xx + 0.5) / 2
    y = (h - (yy + 0.5)) / 2
    hw, hh = w / 2, h / 2
    if kind == 'Attack':                                   # 尖拱
        pad = 2
        half = hw / 2 - pad
        spring = hh * 0.62
        px = np.abs(x - hw / 2)
        u = np.clip(px / half, 0, 1)
        rise = hh - pad - spring
        top = spring + rise * (1 - u ** 1.5)
        slope = rise * 1.5 * np.sqrt(u) / half
        d = np.maximum(np.maximum((y - top) / np.sqrt(1 + slope * slope), px - half), pad - y)
    elif kind == 'Power':                                  # 圆窗（432 × 432 居中）
        d = np.hypot(x - hw / 2, y - hh / 2) - (hh / 2 - 2)
    elif kind == 'Curse':                                  # 墨渍破窗
        px, py = (x - hw / 2) / 1.4, y - hh / 2
        a = np.arctan2(py, px)
        r = 92 * (1 + 0.08 * np.sin(6 * a + 0.5) + 0.05 * np.sin(13 * a + 2) + 0.03 * np.sin(23 * a))
        d = np.hypot(px, py) - r
    else:                                                  # 倒角方窗（技能、状态）
        px, py = np.abs(x - hw / 2), np.abs(y - hh / 2)
        box = np.maximum(px - (hw / 2 - 2), py - (hh / 2 - 2))
        corner = (px + py - (hw / 2 + hh / 2 - 26)) * 0.7071
        d = np.maximum(box, corner)
    cov = np.clip(0.5 - d * 2, 0, 1)
    line = np.clip(1.5 - np.abs(d * 2 + 1.5), 0, 1)       # 窗框墨线
    return cov, line


def colorize(ink, abyss, mad, madface):
    """照搬 Deeptide/UI/Etching 的着色。"""
    if madface:
        paper, inkc, ab, md, reveal = ABYSS_CARD, LUNACY, PALE, LUNACY, 1.0
    else:
        paper, inkc, ab, md, reveal = PAPER, INK, TEAL, INK, 0.0
    rgb = paper * (1 - ink[..., None]) + inkc * ink[..., None]
    rgb = rgb * (1 - abyss[..., None]) + ab * abyss[..., None]
    m = (mad * reveal)[..., None]
    return rgb * (1 - m) + md * m


def card_view(ink, abyss, mad, kind, madface):
    rgb = colorize(ink, abyss, mad, madface)
    win, line = window_mask(kind)
    bg = (0x20, 0x24, 0x24) if madface else (0xCF, 0xC6, 0xB0)   # 卡面在窗外的颜色
    bg = np.array(bg) / 255
    out = rgb * win[..., None] + bg * (1 - win[..., None])
    lc = LUNACY if madface else INK
    out = out * (1 - line[..., None]) + lc * line[..., None]
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8))


def font(size):
    for f in ('C:/Windows/Fonts/NotoSerifSC-VF.ttf', 'C:/Windows/Fonts/simsun.ttc'):
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            pass
    return ImageFont.load_default()


def preview(card_id, name, ink, abyss, mad, kind, dual, path):
    sane = card_view(ink, abyss, mad, kind, False)
    madv = card_view(ink, abyss, mad, kind, True)
    raw = Image.fromarray(((1 - np.clip(ink, 0, 1)) * 255).astype(np.uint8)).convert('RGB')
    pad = 16
    W = 624 * 2 + pad * 3
    thumb_h = 144
    H = 40 + 432 * 2 + pad * 3 + thumb_h + pad
    sheet = Image.new('RGB', (W, H), (0x30, 0x2E, 0x2A))
    d = ImageDraw.Draw(sheet)
    d.text((pad, 8), f'{card_id} {name}  ·  {kind}{"  ·  双面" if dual else ""}', fill=(0xE8, 0xE0, 0xCC), font=font(24))
    sheet.paste(sane, (pad, 40))
    sheet.paste(madv, (pad * 2 + 624, 40))
    sheet.paste(raw, (pad, 40 + 432 + pad))
    # 癫狂层单独看
    m = np.clip(mad, 0, 1)
    a = np.clip(abyss, 0, 1)
    layers = np.ones((432, 624, 3)) * 0.92
    layers = layers * (1 - m[..., None]) + np.array([0.75, 0.1, 0.1]) * m[..., None]
    layers = layers * (1 - a[..., None]) + TEAL * a[..., None]
    sheet.paste(Image.fromarray((layers * 255).astype(np.uint8)), (pad * 2 + 624, 40 + 432 + pad))
    # 手牌尺寸缩略图（10 §3 缩略图测试：208 × 144）
    y = 40 + (432 + pad) * 2
    x = pad
    for im in (sane, madv):
        t = im.resize((208, 144), Image.LANCZOS)
        sheet.paste(t, (x, y))
        x += 208 + pad
    x += pad
    for im in (sane, madv):                                # 手牌实际是 240 × 336 卡面里的 1/3 插图：约 208 × 144
        t = im.resize((104, 72), Image.LANCZOS)
        sheet.paste(t, (x, y))
        x += 104 + pad
    sheet.save(path)


def save_layer(arr, path):
    a = (np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8)
    rgba = np.zeros(a.shape + (4,), np.uint8)
    rgba[..., 3] = a
    Image.fromarray(rgba, 'RGBA').save(path, optimize=True)


def build(card_id, preview_dir):
    mod = importlib.import_module(f'cards.{card_id}')
    importlib.reload(mod)
    t0 = time.time()
    c = etch.Canvas(seed=sum(map(ord, card_id)))
    mod.draw(c)
    ink, abyss, mad = (c.export(k) for k in ('ink', 'abyss', 'mad'))
    OUT.mkdir(parents=True, exist_ok=True)
    save_layer(ink, OUT / f'{card_id}_ink.png')
    for k, arr in (('abyss', abyss), ('mad', mad)):
        p = OUT / f'{card_id}_{k}.png'
        if arr.max() > 0.01:
            save_layer(arr, p)
        elif p.exists():
            p.unlink()
    kind, _, dual = CARD_TYPE.get(card_id, ('Skill', '', False))
    preview_dir.mkdir(parents=True, exist_ok=True)
    preview(card_id, getattr(mod, 'NAME', ''), ink, abyss, mad, kind, dual, preview_dir / f'{card_id}.png')
    print(f'{card_id}: {time.time() - t0:.1f}s  ink mean {ink.mean():.2f}  abyss {abyss.max() > 0.01}  mad {mad.max() > 0.01}')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('ids', nargs='*')
    ap.add_argument('--preview', default=str(HERE / 'preview'))
    args = ap.parse_args()
    load_types()
    ids = args.ids or sorted(p.stem for p in (HERE / 'cards').glob('*.py') if not p.stem.startswith('_'))
    for i in ids:
        build(i, pathlib.Path(args.preview))


if __name__ == '__main__':
    main()
