"""LU03 撕碎：挂在钩子上的一件粗呢大衣，被爪子从上到下撕成了一条一条，下摆的布条垂着、打着卷（四段攻击）。"""
import numpy as np

from etch import OUTLINE, spline
from cards import _wolf as w

NAME = '撕碎'


def draw(c):
    w.night(c, top=0.85, bottom=0.7, seed=181)
    c.line([(624, 20), (624, 70), (600, 96), (580, 80)], w=10, taper=0)    # 钩子
    collar = [(470, 150), (560, 96), (690, 96), (780, 150), (720, 200), (530, 200)]
    m = c.mask(spline(collar, closed=True))
    c.engrave(m, 0.6, [c.ph_lin(10, seed=182), c.ph_lin(70, seed=183)], spacing=7)
    c.outline(collar, w=OUTLINE)
    rng = np.random.default_rng(181)
    n = 9
    x0, x1 = 360, 900
    for k in range(n):                                                     # 布条：从肩部往下越来越散、越来越窄
        xa = x0 + (x1 - x0) * k / n + 8
        xb = x0 + (x1 - x0) * (k + 1) / n - 8
        ln = rng.uniform(500, 680)
        sway = rng.normal(0, 40)
        t = np.linspace(0, 1, 30)
        cx = (xa + xb) / 2 + (xa + xb - 1260) / 2 * 0.35 * t ** 1.5 + sway * t ** 2
        cy = 180 + ln * t
        half = (xb - xa) / 2 * (1 - 0.55 * t) + 2 * np.sin(t * 40 + k)
        left = np.stack([cx - half, cy], 1)
        right = np.stack([cx + half, cy], 1)
        tip = (cx[-1] + rng.normal(0, 8), cy[-1] + rng.uniform(20, 50))
        pts = np.vstack([left, [tip], right[::-1]])
        sm = c.mask(pts)
        shade = 0.45 + 0.15 * (k % 2) + 0.3 * c.grad(0, 200, 0, 860)
        c.engrave(sm, np.clip(shade, 0, 1), [c.ph_lin(88, seed=184 + k), c.ph_lin(30, seed=190 + k)], spacing=7, swell=1.0)
        c.stroke(left, 3.6, taper=0)
        c.stroke(right, 4.6, taper=0)
        for j in range(3):                                                 # 毛边的线头
            y = cy[-1] - rng.uniform(0, 60)
            x = cx[-1] + rng.uniform(-half[-1], half[-1])
            c.line([(x, y), (x + rng.normal(0, 8), y + rng.uniform(20, 50))], w=1.6, taper=(0, 0.8))
