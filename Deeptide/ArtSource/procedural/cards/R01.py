"""R01 旧日注视：海面一叶小船，夜空中由星辰连成的巨眼（深渊遮罩）（策划/10 §4.6）。

能力牌圆窗。下三分之一是夜海，中间一叶小船和船上一个小人；上面的夜空里，星星沿着一只巨眼的眼睑、虹膜排开，
用细线连起来——这只眼和它的星座连线走深渊遮罩，在清醒面里也是唯一的青色。海面上有它破碎的倒影。
"""
import math

import numpy as np

from etch import STRUCT, ellipse_pts, smoothstep, spline
from cards._common import person

NAME = '旧日注视'

HORIZON = 560
EYE = (624, 290)


def draw(c):
    rng = np.random.default_rng(1)
    ex, ey = EYE
    sky = c.rect(0, 0, c.w, HORIZON)
    sea = 1 - sky
    # ------------------------------------------------------------ 夜空 + 普通的星
    c.engrave(sky, 0.5 - 0.2 * c.grad(0, 0, 0, HORIZON) + 0.04 * c.noise(100, 1),
              [c.ph_lin(0, wobble=1.2, seed=2), c.ph_lin(70, seed=3), c.ph_lin(-50, seed=4)], spacing=9, swell=1.1)
    for _ in range(110):
        x, y, r = rng.uniform(0, c.w), rng.uniform(0, HORIZON - 20), rng.uniform(1.5, 4)
        c.erase(c.ellipse(x, y, r + 1.5, r + 1.5))

    # ------------------------------------------------------------ 星辰巨眼：眼睑、虹膜上排着星，用细线连起来
    W, H = 330, 150
    upper = spline([(ex - W, ey, 'c'), (ex - W * 0.4, ey - H), (ex + W * 0.4, ey - H), (ex + W, ey, 'c')])
    lower = spline([(ex + W, ey, 'c'), (ex + W * 0.4, ey + H * 0.8), (ex - W * 0.4, ey + H * 0.8), (ex - W, ey, 'c')])
    stars = []
    for curve, n in ((upper, 11), (lower, 9)):
        idx = np.linspace(0, len(curve) - 1, n).astype(int)
        pts = curve[idx] + rng.normal(0, 4, (n, 2))
        stars += list(pts)
        for a, b in zip(pts[:-1], pts[1:]):
            c.stroke([tuple(a), tuple(b)], 3.4, layer='abyss', taper=0)
    for k in range(12):                                                      # 虹膜：一圈星
        q = 2 * math.pi * k / 12
        stars.append(np.array([ex + 92 * math.cos(q), ey + 92 * math.sin(q)]))
    c.stroke(ellipse_pts(ex, ey, 92, 92), 3.0, layer='abyss', closed=True)
    c.stroke(ellipse_pts(ex, ey, 44, 44), 1.6, layer='abyss', closed=True)
    for k in range(12):                                                      # 虹膜的放射线
        q = 2 * math.pi * (k + 0.5) / 12
        c.stroke([(ex + 48 * math.cos(q), ey + 48 * math.sin(q)), (ex + 88 * math.cos(q), ey + 88 * math.sin(q))], 1.4,
                 layer='abyss', taper=0.3)
    for i, p in enumerate(stars):
        r = 9 if i % 3 == 0 else 6.5
        c.erase(c.ellipse(p[0], p[1], r + 3, r + 3))
        c.add(c.ellipse(p[0], p[1], r, r), 'abyss')
        if i % 3 == 0:
            for a in (0, 90):
                q = math.radians(a + 15)
                c.add(c.stroke([(p[0] - 3 * r * math.cos(q), p[1] - 3 * r * math.sin(q)),
                                (p[0] + 3 * r * math.cos(q), p[1] + 3 * r * math.sin(q))], 2.0, add=False, taper=0.5), 'abyss')
    # 瞳孔：一块比夜空还黑的地方
    c.fill(c.ellipse(ex, ey, 26, 40), value=1.0)

    # ------------------------------------------------------------ 海：横波纹，有眼睛碎掉的青色倒影
    c.engrave(sea, 0.8 + 0.1 * c.grad(0, HORIZON, 0, c.h), [c.ph_wave(0, 4, 140, wobble=2, seed=5), c.ph_lin(20, seed=6)],
              spacing=8.5, swell=1.2)
    for _ in range(40):
        y = rng.uniform(HORIZON + 10, 860)
        x = ex + rng.normal(0, 120 + (y - HORIZON) * 0.5)
        ln = rng.uniform(20, 70)
        c.add(c.stroke([(x, y), (x + ln, y)], rng.uniform(2, 4), add=False, taper=0.4), 'abyss')
    c.stroke([(0, HORIZON), (c.w, HORIZON)], 2.6, taper=0)

    # ------------------------------------------------------------ 小船 + 人
    bx, by = 560, 640
    hull = [(bx - 120, by - 20), (bx + 130, by - 26), (bx + 100, by + 14), (bx - 90, by + 16)]
    c.fill(c.mask(spline(hull, closed=True)), value=0.95)
    c.erase(c.stroke([(bx - 116, by - 18), (bx + 126, by - 24)], 2.4, add=False, taper=0))
    person(c, bx + 10, by - 18, 90, tone=0.95, hat=True)
    c.stroke([(bx - 30, by - 30), (bx - 120, by + 40)], 4, taper=0)          # 桨
    for k in range(3):                                                       # 船边的涟漪
        c.erase(c.stroke(ellipse_pts(bx, by + 20, 170 + k * 50, 14 + k * 6, a0=0, a1=180), 2.0, add=False, taper=0.3))
