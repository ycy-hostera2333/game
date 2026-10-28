"""LC10 月光（双面）：半开的百叶窗，一道斜斜的月光落在地板上（清醒面：格挡，然后掉理智）。
癫狂层：光斑里多出一个狼头的影子（兽形面：恢复理智再格挡）。"""
import math

import numpy as np

from etch import Frame, OUTLINE, poly
from cards import _wolf as w

NAME = '月光'


def draw(c):
    # 屋里很暗
    c.engrave(np.ones((c.h, c.w), np.float32), np.clip(0.82 + 0.1 * c.grad(0, 0, 1248, 864) + 0.04 * c.noise(80, 131), 0, 1),
              [c.ph_lin(-10, seed=131), c.ph_lin(60, seed=132), c.ph_lin(-55, seed=133)], spacing=9, swell=1.2)
    # 窗：左上，四片百叶之间漏光
    win = (120, 60, 420, 380)
    c.clear(c.rect(*win))
    c.engrave(c.rect(*win), 0.15, [c.ph_lin(0)], spacing=8, wmax=2)
    for k in range(6):
        y = 60 + 54 * k
        slat = c.mask(poly([(120, y + 8), (420, y + 8), (420, y + 36), (120, y + 36)]))
        c.engrave(slat, 0.7, [c.ph_lin(0, seed=134 + k)], spacing=6, swell=1.0)
        c.stroke(poly([(120, y + 36), (420, y + 36)]), 3, taper=0)
    c.stroke(poly([(120, 60), (420, 60), (420, 380), (120, 380), (120, 60)]), OUTLINE, taper=0)
    c.stroke(poly([(270, 60), (270, 380)]), 6, taper=0)
    # 光：从窗斜照到右下的地板，成一个平行四边形光斑
    beam = c.mask(poly([(120, 60), (420, 60), (1180, 640), (760, 860)]))
    patch = c.mask(poly([(540, 560), (980, 500), (1220, 760), (760, 860)]))
    c.erase(np.clip(beam * 0.55 * (1 - 0.6 * c.grad(120, 60, 1100, 800)) + patch, 0, 1))
    c.engrave(patch, 0.06 + 0.1 * c.grad(540, 560, 1220, 800), [c.ph_lin(-28, seed=137)], spacing=10, wmax=1.8)
    for k in range(5):                                                      # 地板缝
        y0 = 560 + 70 * k
        c.add(c.stroke(poly([(500, y0), (1248, y0 - 120)]), 2.4, add=False, taper=0) * patch)
    for k in range(6):                                                      # 光斑里百叶的影子
        a, b = 0.1 + 0.15 * k, 0.16 + 0.15 * k
        p = lambda t, u: ((540 + (980 - 540) * u) * (1 - t) + (760 + (1220 - 760) * u) * t,
                          (560 + (500 - 560) * u) * (1 - t) + (860 + (760 - 860) * u) * t)
        c.add(c.mask(poly([p(a, 0), p(a, 1), p(b, 1), p(b, 0)])) * patch * 0.9)
    # 癫狂层：光斑里的狼头影子
    w.wolf_head(c, Frame((850, 680), rot=-6, scale=0.9), jaw=14, layer='mad', eye_layer='mad', tone=0.5, scaled=False,
                gills=False, seed=138)
