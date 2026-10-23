"""标题字（策划/10 §8「主视觉 + Logo」）：「深潮」两个字，单色精灵，游戏里着墨色叠在主视觉上。

字形取思源宋体（Noto Serif SC，OFL）的 Black 字重做底，再按版画的办法加工：字里刻满横向的波浪排线（潮水），
笔画边缘一道内描的白线，外面一圈粗墨边；字下面一道浪线，右端卷成一个触须的小圈。
"""
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

from etch import spline

SCALE = 1

FONT = 'C:/Windows/Fonts/NotoSerifSC-VF.ttf'


def _glyphs(c, text, x, y, size):
    img = Image.new('L', (c.w, c.h), 0)
    try:
        f = ImageFont.truetype(FONT, size)
        f.set_variation_by_name('Black')
    except OSError:
        f = ImageFont.truetype('C:/Windows/Fonts/simsun.ttc', size)
    ImageDraw.Draw(img).text((x, y), text, fill=255, font=f, anchor='mm')
    return np.asarray(img, np.float32) / 255


def logo(c):
    W, H = c.w, c.h
    m = _glyphs(c, '深潮', W / 2, H * 0.42, int(H * 0.68))
    inside = ndi.distance_transform_edt(m > 0.5).astype(np.float32)
    # 字里：横向的波浪排线，笔画越粗的地方线越粗（涨线）
    ph = c.yy + 7 * np.sin(c.xx / 46) + 3 * c.noise(60, 1)
    hatch = c.lines(ph, 9, 3.6 + 3.4 * np.clip(inside / 14, 0, 1))
    c.add(m * hatch)
    # 笔画边缘：外面一圈实墨，往里 7 像素一道留白的细线
    edge = np.clip(m - ndi.binary_erosion(m > 0.5, iterations=6).astype(np.float32), 0, 1)
    c.add(edge)
    inner = (inside > 8) & (inside < 11)
    c.erase(inner.astype(np.float32))
    outer = ndi.binary_dilation(m > 0.5, iterations=5).astype(np.float32) - (m > 0.5)
    c.add(ndi.gaussian_filter(np.clip(outer, 0, 1), 0.7))
    # 字下面一道浪线，右端卷成触须的小圈
    base = H * 0.86
    pts = [(W * 0.08, base), (W * 0.22, base - 16), (W * 0.36, base + 4), (W * 0.5, base - 18), (W * 0.64, base + 2), (W * 0.78, base - 14),
           (W * 0.86, base - 4)]
    curl = [(W * 0.86, base - 4), (W * 0.91, base - 40), (W * 0.88, base - 76), (W * 0.84, base - 56), (W * 0.865, base - 36)]
    c.stroke(spline(pts + curl[1:]), 16, taper=(0.3, 0.4))
    c.stroke(spline([(W * 0.12, base + 26), (W * 0.3, base + 14), (W * 0.48, base + 28), (W * 0.66, base + 16)]), 7, taper=0.4)


SPRITES = {
    'logo': (1600, 620, logo),
}
