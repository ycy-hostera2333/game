"""LC02 割喉（双面）：挂着的油布被一爪从右上划到左下，三道口子后面是黑的。癫狂层：口子下沿往下淌的血（兽形面施加流血）。"""
import math

import numpy as np

from etch import Frame
from cards import _wolf as w

NAME = '割喉'


def draw(c):
    phase = c.xx / 34 + 0.8 * np.sin(c.yy / 300)                                # 油布的竖褶
    fold = np.sin(phase)
    cloth = np.ones((c.h, c.w), np.float32)
    c.engrave(cloth, np.clip(0.22 - 0.18 * fold + 0.18 * c.grad(0, 0, 0, 864) + 0.04 * c.noise(60, 51), 0, 1),
              [phase * 34 + 2 * c.noise(40, 52), c.ph_lin(30, seed=53)], spacing=10, swell=0.9)
    cut = w.claw_marks(c, (900, 120), (380, 640), n=3, gap=70, width=40, seed=51)
    # 癫狂层：每道口子的下沿垂下几条血
    rng = np.random.default_rng(52)
    ys, xs = np.nonzero(cut > 0.5)
    for k in range(9):
        i = rng.integers(len(xs))
        x, y = float(xs[i]), float(ys[i])
        ln = rng.uniform(80, 220)
        w.drip(c, [(x, y), (x + rng.normal(0, 4), y + ln * 0.5), (x + rng.normal(0, 6), y + ln)], w=rng.uniform(9, 15), layer='mad')
