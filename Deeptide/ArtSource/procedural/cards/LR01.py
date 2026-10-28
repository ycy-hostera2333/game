"""LR01 永夜：被整个吃掉的月亮，只剩一圈刺眼的冕光；下面是一动不动的黑海（理智不会降到 1 以下）。圆窗。"""
import numpy as np

from etch import ellipse_pts
from cards import _wolf as w
from cards._common import burst

NAME = '永夜'


def draw(c):
    w.night(c, top=0.95, bottom=0.85, seed=281)
    c.erase(np.clip(1 - c.radial(624, 380, 200, 420), 0, 1) ** 1.3 * 0.9)   # 冕光：擦淡的一圈
    burst(c, 624, 380, 215, 380, n=90, width=3.2, erase=True, seed=281)
    burst(c, 624, 380, 215, 300, n=60, width=2.2, erase=True, seed=282)
    disc = c.ellipse(624, 380, 200, 200)
    c.fill(disc)
    c.stroke(ellipse_pts(624, 380, 206, 206), 4, closed=True)
    c.erase(c.stroke(ellipse_pts(624, 380, 210, 210, a0=120, a1=200), 6, add=False, taper=0.5))
    sea = np.clip(c.yy - 640, 0, 1).astype(np.float32)
    c.engrave(sea, np.clip(0.93 + 0.04 * c.noise(40, 283), 0, 1), [c.ph_lin(0, wobble=0.5, seed=284), c.ph_lin(30, seed=285)],
              spacing=8, swell=1.4)
    c.erase(c.lines(c.yy + 3 * c.noise(30, 286), 26, 1.6) * sea * (np.abs(c.xx - 624) < 120 + (c.yy - 640) * 0.6) * 0.8)
    c.line([(0, 640), (1248, 640)], w=4, taper=0)
