"""U05 守夜人之誓：墙上刻满计数的划痕，下面一盏灯（策划/10 §4.6）。

能力牌圆窗。一面灰泥墙，上面一组组「四竖一斜」的划痕刻满了大半面墙，越往下越新、越深；
墙脚的木架上一盏小油灯，灯光把墙的下半照亮，上半沉在暗里。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, ellipse_pts, smoothstep, spline
from cards._common import burst

NAME = '守夜人之誓'

LAMP = (624, 700)


def draw(c):
    rng = np.random.default_rng(5)
    lx, ly = LAMP
    r = np.hypot((c.xx - lx) / 1.3, c.yy - ly)
    # ------------------------------------------------------------ 墙：灰泥，灯下亮、上面暗
    c.engrave(np.ones((c.h, c.w), np.float32), np.clip(0.12 + 0.75 * smoothstep(80, 620, r) + 0.06 * c.noise(30, 1), 0, 1),
              [c.ph_lin(4, wobble=2.5, seed=2), c.ph_lin(70, seed=3), c.ph_lin(-40, seed=4)], spacing=9.5, swell=1.1)
    for _ in range(14):                                                      # 墙皮的裂纹
        x, y = rng.uniform(200, 1050), rng.uniform(80, 560)
        pts = [(x, y)]
        a = rng.uniform(0, 2 * math.pi)
        for _ in range(5):
            a += rng.uniform(-0.8, 0.8)
            pts.append((pts[-1][0] + 16 * math.cos(a), pts[-1][1] + 16 * math.sin(a)))
        c.stroke(pts, 1.8, taper=(0.2, 0.8))

    # ------------------------------------------------------------ 计数：一行行「四竖一斜」
    y = 110
    row = 0
    while y < 600:
        x = 190 + rng.uniform(0, 30)
        depth = 0.55 + 0.45 * (y / 600)                                       # 越往下越新、越深
        while x < 1060:
            h = rng.uniform(40, 50)
            for k in range(4):
                xx = x + k * 13 + rng.uniform(-2, 2)
                c.erase(c.stroke([(xx + 2, y + rng.uniform(-2, 2)), (xx - 1, y + h)], 4.5 * depth, add=False, taper=0.2))
                c.stroke([(xx + 4, y + 2), (xx + 1, y + h + 2)], 2.2 * depth, taper=0.2)           # 刻痕的阴影边
            c.erase(c.stroke([(x - 8, y + h * 0.75), (x + 52, y + h * 0.25)], 4.0 * depth, add=False, taper=0.2))
            c.stroke([(x - 6, y + h * 0.75 + 3), (x + 54, y + h * 0.25 + 3)], 2.0 * depth, taper=0.2)
            x += 82 + rng.uniform(-4, 6)
        y += 68
        row += 1

    # ------------------------------------------------------------ 木架 + 油灯
    shelf = c.rect(380, ly + 70, 870, ly + 100)
    c.engrave(shelf, 0.55, [c.ph_lin(0, wobble=0.8), c.ph_lin(70)], spacing=7)
    c.stroke([(380, ly + 70), (870, ly + 70), (870, ly + 100), (380, ly + 100), (380, ly + 70)], STRUCT, taper=0)
    for x in (430, 820):
        c.stroke([(x, ly + 100), (x + 40, ly + 170)], 7, taper=0)
    body = [(lx - 90, ly + 70), (lx - 100, ly + 30), (lx - 60, ly + 4), (lx + 60, ly + 4), (lx + 130, ly + 20), (lx + 160, ly + 10),
            (lx + 150, ly + 34), (lx + 90, ly + 56), (lx + 80, ly + 70)]
    m = c.mask(spline(body, closed=True))
    lam = c.relief(m, radius=24, blur=2)
    c.engrave(m, np.clip(0.6 - 0.6 * lam, 0, 1), [c.ph_axis((lx - 100, ly + 40), (lx + 100, ly + 40), bow=8, half=40), c.ph_lin(70)],
              spacing=6.5)
    c.outline(body, w=STRUCT + 1, light=0.4)
    c.stroke(ellipse_pts(lx - 130, ly + 40, 28, 22, a0=60, a1=300), 6, taper=0)     # 把手
    fx, fy = lx + 150, ly - 40
    flame = [(fx, fy - 70), (fx + 22, fy - 20), (fx + 18, fy + 22), (fx, fy + 34), (fx - 18, fy + 22), (fx - 22, fy - 20)]
    c.clear(c.mask(spline(flame, closed=True)))
    c.outline(flame, w=3, light=0)
    burst(c, fx, fy, 60, 170, n=30, width=2.6, erase=True, seed=4)
