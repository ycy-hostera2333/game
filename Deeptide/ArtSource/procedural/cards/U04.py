"""U04 默祷：空教堂长椅间跪着的人（策划/10 §4.6）。

从教堂后面沿中间的过道往前看：两排长椅向远处的祭坛收拢，祭坛后面是一扇高高的尖拱窗，
窗里斜射进几道光柱，落在过道上；过道中间一个小小的人跪着，低着头。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, smoothstep, spline
from cards._common import person

NAME = '默祷'

VX, VY = 624, 330      # 灭点


def draw(c):
    # ------------------------------------------------------------ 墙、地面、拱顶：透视
    c.engrave(np.ones((c.h, c.w), np.float32), 0.78 + 0.05 * c.noise(80, 1),
              [c.ph_lin(85, seed=1), c.ph_lin(5, seed=2), c.ph_lin(-40, seed=3)], spacing=9, swell=1.1)
    floor = c.mask(np.array([(VX - 70, VY + 110), (VX + 70, VY + 110), (c.w + 200, c.h), (-200, c.h)]))
    c.engrave(floor, 0.45, [c.ph_lin(0, wobble=0.8, seed=4), c.ph_lin(60, seed=5)], spacing=9)
    # 远处的尖拱窗 + 光柱
    win = [(VX - 60, VY + 60), (VX - 60, VY - 120), (VX, VY - 210, 'c'), (VX + 60, VY - 120), (VX + 60, VY + 60)]
    wm = c.mask(spline(win, closed=True))
    c.clear(wm)
    c.stroke(spline(win, closed=True), 4, closed=True, taper=0)
    c.stroke([(VX, VY - 200), (VX, VY + 60)], 3, taper=0)
    c.stroke([(VX - 60, VY - 60), (VX + 60, VY - 60)], 3, taper=0)
    beam = c.mask(np.array([(VX - 50, VY - 100), (VX + 50, VY - 100), (VX + 260, c.h), (VX - 140, c.h)]))
    c.erase(beam * smoothstep(-0.2, 1, 0.5 + 0.5 * np.sin((c.xx - VX - (c.yy - VY) * 0.25) / 14)) * 0.75)
    # 祭坛
    altar = c.rect(VX - 90, VY + 60, VX + 90, VY + 120)
    c.engrave(altar, 0.6, [c.ph_lin(0), c.ph_lin(80)], spacing=6)
    c.stroke([(VX - 90, VY + 60), (VX + 90, VY + 60), (VX + 90, VY + 120), (VX - 90, VY + 120), (VX - 90, VY + 60)], 3, taper=0)
    c.stroke([(VX, VY - 10), (VX, VY + 58)], 4, taper=0)
    c.stroke([(VX - 18, VY + 10), (VX + 18, VY + 10)], 4, taper=0)
    # 柱子：两边一排排，近大远小
    for sd in (-1, 1):
        for k in range(5):
            t = (k + 1) / 6
            x = VX + sd * (110 + 600 * t ** 1.6)
            w = 14 + 70 * t ** 1.6
            top, bot = VY - 200 - 300 * t, VY + 110 + 500 * t ** 1.6
            col = c.rect(x - w / 2, max(top, 0), x + w / 2, min(bot, c.h))
            c.engrave(col, 0.55 + 0.3 * (sd > 0), [c.ph_lin(90, wobble=0.4), c.ph_lin(20)], spacing=7)
            c.stroke([(x - w / 2, top), (x - w / 2, bot)], 2 + 3 * t, taper=0)
            c.stroke([(x + w / 2, top), (x + w / 2, bot)], 2 + 3 * t, taper=0)

    # ------------------------------------------------------------ 长椅：两排，向灭点收
    for sd in (-1, 1):
        for k in range(9):
            t = (k + 1) / 10
            y = VY + 120 + 600 * t ** 1.5
            inner = VX + sd * (60 + 300 * t ** 1.5)
            outer = VX + sd * (260 + 1300 * t ** 1.5)
            h = 12 + 120 * t ** 1.5
            seat = c.mask(np.array([(inner, y - h), (outer, y - h * 1.1), (outer, y), (inner, y)]))
            c.engrave(seat, 0.7, [c.ph_lin(0, wobble=0.3), c.ph_lin(80)], spacing=6 + 4 * t)
            c.stroke([(inner, y - h), (outer, y - h * 1.1)], 1.5 + 4 * t, taper=0)
            c.stroke([(inner, y - h), (inner, y)], 1.5 + 4 * t, taper=0)
            c.erase(c.stroke([(inner, y - h + 3 + t * 4), (outer, y - h * 1.1 + 3 + t * 4)], 1 + 2 * t, add=False, taper=0))

    # ------------------------------------------------------------ 跪着的人：过道中间
    kx, ky = VX + 10, VY + 420
    m = c.mask(spline([(kx - 46, ky), (kx - 50, ky - 60), (kx - 36, ky - 120), (kx + 30, ky - 130), (kx + 44, ky - 80),
                       (kx + 70, ky - 10), (kx + 80, ky)], closed=True))
    c.fill(m, value=0.96)
    c.fill(c.ellipse(kx - 4, ky - 148, 22, 26, rot=20), value=0.96)              # 低着的头
    c.erase(c.stroke([(kx - 30, ky - 110), (kx - 34, ky - 40)], 2.4, add=False, taper=0.3))   # 背上一道光
    c.fill(c.ellipse(kx + 10, ky + 4, 110, 12), value=0.85)                       # 影子
