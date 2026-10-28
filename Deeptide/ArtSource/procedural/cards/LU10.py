"""LU10 撕开动脉：一爪划开一道深口，血分成两股往外喷成弧线，在空中散成血点（目标的流血翻倍，消耗）。"""
import math

import numpy as np

from cards import _wolf as w

NAME = '撕开动脉'


def draw(c):
    # 底：皮肉的弧面，上亮下暗
    skin = np.ones((c.h, c.w), np.float32)
    c.engrave(skin, np.clip(0.2 + 0.55 * c.grad(0, 200, 0, 864) + 0.05 * c.noise(60, 261), 0, 1),
              [c.ph_axis((0, 600), (1248, 600), bow=-120, half=600, seed=262), c.ph_lin(70, seed=263)], spacing=9, swell=1.0)
    w.claw_marks(c, (300, 640), (760, 500), n=1, width=70, seed=261, jag=10, curve=0.05)
    # 两股喷出来的血：从口子往右上喷的两道弧，越往外越散
    rng = np.random.default_rng(261)
    for k, (bend, reach) in enumerate(((-0.6, 620), (-0.35, 520))):
        x0, y0 = 600 + 40 * k, 540 + 10 * k
        t = np.linspace(0, 1, 40)
        xs = x0 + reach * t
        ys = y0 - 360 * math.sin(math.pi * 0.45) * t + 420 * (t ** 2) * (0.6 + 0.4 * k) + bend * 200 * t
        wd = 30 * (1 - t) ** 1.2 + 4
        c.stroke(np.stack([xs[:26], ys[:26]], 1), 30 - 6 * k, taper=(0.0, 0.7))
        for i in range(18, 40):
            if rng.random() < 0.7:
                r = wd[i] * rng.uniform(0.3, 0.7)
                c.add(c.ellipse(xs[i] + rng.normal(0, 14), ys[i] + rng.normal(0, 14), r, r * 0.8))
    w.splatter(c, 1100, 760, 30, n=20, seed=262)
