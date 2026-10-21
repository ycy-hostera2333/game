"""画单色精灵：python build_sprites.py icons nodes …（不给组名就画全部）。

单色精灵（策划/10 §11.3）：图标、卡面框饰、UI 墨线、特效序列帧。每组是 sprites/<组>.py，里面一个 SPRITES 字典：
ID → (导出宽, 导出高, draw(c))。draw 在 2 倍的工作画布上往 ink 层画墨（0–1 覆盖率）；导出成白色 + alpha 的 PNG
（../sprites/<组>/<ID>.png），颜色由游戏里的 Image.color 从色板给。组里可以有 SCALE = 1，表示工作画布等于导出尺寸（大图省时间）。
预览：preview/sprites_<组>.png，每张在纸底上画一遍墨色、在墨底上画一遍纸色（意图图标就是这样用的）。
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
from build import PAPER, INK, font  # noqa: E402

OUT = HERE.parent / 'sprites'


def render(w, h, fn, scale=2, seed=1):
    c = etch.Canvas(w * scale, h * scale, seed=seed)
    fn(c)
    L = np.clip(c.layers['ink'], 0, 1)
    if scale == 1:
        return L
    return L.reshape(h, scale, w, scale).mean((1, 3))


def save_mono(arr, path):
    a = (np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8)
    rgba = np.full(a.shape + (4,), 255, np.uint8)
    rgba[..., 3] = a
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, 'RGBA').save(path, optimize=True)


def build_group(group, preview_dir, only=None):
    mod = importlib.import_module(f'sprites.{group}')
    importlib.reload(mod)
    scale = getattr(mod, 'SCALE', 2)
    items = []
    t0 = time.time()
    for k, (sid, (w, h, fn)) in enumerate(mod.SPRITES.items()):
        if only and sid not in only:
            continue
        a = render(w, h, fn, scale, seed=k + 1)
        save_mono(a, OUT / group / f'{sid}.png')
        items.append((sid, a))
    # 预览：每张缩到不超过 200 高，纸底墨色 + 墨底纸色
    pad, cell = 12, 200
    tiles = []
    for sid, a in items:
        h, w = a.shape
        k = min(1.0, cell / max(h, w))
        tw, th = max(int(w * k), 1), max(int(h * k), 1)
        img = Image.fromarray((a * 255).astype(np.uint8)).resize((tw, th), Image.LANCZOS)
        al = np.asarray(img, np.float32)[..., None] / 255
        on_paper = PAPER * (1 - al) + INK * al
        on_ink = INK * (1 - al) + PAPER * al
        tiles.append((sid, Image.fromarray((on_paper * 255).astype(np.uint8)), Image.fromarray((on_ink * 255).astype(np.uint8))))
    cols = max(1, min(8, int(1800 / (cell * 2 + pad * 3))))
    rows = (len(tiles) + cols - 1) // cols
    W = cols * (cell * 2 + pad * 3)
    H = rows * (cell + 40) + pad
    sheet = Image.new('RGB', (W, max(H, 60)), (0x30, 0x2E, 0x2A))
    d = ImageDraw.Draw(sheet)
    for i, (sid, a, b) in enumerate(tiles):
        x = (i % cols) * (cell * 2 + pad * 3) + pad
        y = (i // cols) * (cell + 40) + pad
        d.text((x, y), sid, fill=(0xE8, 0xE0, 0xCC), font=font(16))
        sheet.paste(a, (x, y + 24))
        sheet.paste(b, (x + cell + pad, y + 24))
    preview_dir.mkdir(parents=True, exist_ok=True)
    sheet.save(preview_dir / f'sprites_{group}.png')
    print(f'{group}: {len(items)} 张  {time.time() - t0:.1f}s')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('groups', nargs='*')
    ap.add_argument('--only', nargs='*')
    ap.add_argument('--preview', default=str(HERE / 'preview'))
    args = ap.parse_args()
    groups = args.groups or sorted(p.stem for p in (HERE / 'sprites').glob('*.py') if not p.stem.startswith('_'))
    for g in groups:
        build_group(g, pathlib.Path(args.preview), args.only)


if __name__ == '__main__':
    main()
