"""U03 驱散：高举的提灯，光里的影子四散（策划/10 §4.6）。

一只手从下方把提灯举到拱窗的正中；灯光是一大圈放射的白线。光圈边上一圈黑影（细长的手、爪、半个人形）
被光推开，一边往外逃一边拉成墨丝。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, smoothstep, spline
from cards.C02 import lantern
from cards._common import burst, wraith

NAME = '驱散'

LAMP = (600, 420)


def draw(c):
    rng = np.random.default_rng(3)
    lx, ly = LAMP
    r = np.hypot((c.xx - lx) / 1.15, c.yy - ly)
    # ------------------------------------------------------------ 光：中间亮，外面黑
    c.engrave(np.ones((c.h, c.w), np.float32), np.clip(0.05 + 0.92 * smoothstep(150, 520, r) + 0.04 * c.noise(40, 1), 0, 1),
              [c.ph_ring(lx, ly, sx=1.15), c.ph_ray(lx, ly, r0=300), c.ph_lin(30, seed=2)], spacing=9, swell=1.2)
    burst(c, lx, ly, 180, 640, n=72, width=4.0, erase=True, seed=1)

    # ------------------------------------------------------------ 影子：光圈外往外逃的人形，背后拉出墨丝
    for x, y, h, lean, flip in ((250, 880, 520, 22, True), (90, 800, 400, 30, True), (1000, 900, 470, 22, False),
                                (1160, 860, 360, 30, False)):
        m = wraith(c, x, y, h, lean=lean, flip=flip)
        away = -1 if flip else 1
        for j in range(8):                                       # 往外拉出的墨丝
            yy = y - rng.uniform(0.25, 0.9) * h
            x0 = x + away * rng.uniform(0.0, 0.15) * h
            c.stroke([(x0, yy), (x0 + away * rng.uniform(120, 260), yy + rng.uniform(-30, 30))], rng.uniform(2, 5),
                     taper=(0.2, 0.9))
    # 从上方缩回去的两只长手
    for (bx, by, ang) in ((300, 120, -150), (870, 60, -40)):
        d = np.array([math.cos(math.radians(ang)), math.sin(math.radians(ang))])
        n = np.array([-d[1], d[0]])
        base = np.array([bx, by])
        c.stroke(spline([tuple(base), tuple(base + d * 140 + n * 14), tuple(base + d * 320)]), 24, taper=(0.5, 0.0))
        for j in range(-2, 3):
            tip = base - d * 70 + n * j * 26
            c.stroke(spline([tuple(base), tuple(base - d * 34 + n * j * 12), tuple(tip)]), 7, taper=(0.0, 0.8))

    # ------------------------------------------------------------ 手臂 + 提灯
    A = Frame((lx + 6, ly - 300), rot=58)                        # +x 指向右下（肘），原点在握住提环的拳头
    sleeve = [(120, -52), (420, -62), (1100, -90), (1100, 90), (420, 60), (120, 52)]
    m = c.mask(spline(A(sleeve), closed=True))
    lam = c.relief(m, radius=50, blur=3)
    c.engrave(m, np.clip(0.98 - 0.5 * lam, 0.5, 1), [c.ph_axis(A.p(120, 0), A.p(1100, 0), bow=-16, half=60), c.ph_lin(A.angle(20)),
                                                    c.ph_lin(A.angle(-40))], spacing=8, swell=1)
    c.outline(A.cp(sleeve), w=OUTLINE, light=0.4)
    for pts in ([(240, -50), (260, -10), (250, 40)], [(380, -58), (420, 0), (400, 56)], [(520, -64), (540, -20)]):
        c.erase(c.stroke(spline(A(pts)), 3.4, add=False, taper=0.4))          # 袖子的褶：亮线
    c.line(A.cp([(150, -50), (160, 0), (150, 50)]), w=STRUCT, taper=0.1)       # 袖口
    fore = [(0, -40), (130, -46), (130, 48), (0, 40)]
    m = c.mask(spline(A(fore), closed=True))
    lam = c.relief(m, radius=24, blur=2)
    c.engrave(m, np.clip(0.4 - 0.4 * lam, 0, 1), [c.ph_lin(A.angle(90)), c.ph_lin(A.angle(10))], spacing=7.5)
    c.outline(A.cp(fore), w=STRUCT + 1, light=0.4)
    lantern(c, Frame((lx, ly + 110), scale=1.05))
    # 拳头：攥着提环，四个指节朝观者
    fist = [(-40, -50), (30, -54), (60, -30), (64, 30), (30, 54), (-40, 50)]
    fm = c.mask(spline(A(fist), closed=True))
    lam = c.relief(fm, radius=24, blur=1.5)
    c.engrave(fm, np.clip(0.55 - 0.6 * lam, 0, 1), [c.ph_lin(A.angle(90)), c.ph_lin(A.angle(20))], spacing=6.5, wmax=2.4)
    c.outline(A.cp(fist), w=STRUCT + 1, light=0.4)
    for y in (-34, -12, 10, 32):
        c.line(A.cp([(-26, y - 8), (-38, y), (-26, y + 8)]), w=2.6, taper=0.3)
