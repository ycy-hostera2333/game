"""LC03 血腥气味：三滴血落进夜里的海面，砸出涟漪；水下有几条长东西被引过来，眼睛是深渊色（流血打全体）。"""
import math

import numpy as np

from etch import Frame, ellipse_pts, spline
from cards import _wolf as w

NAME = '血腥气味'
SEA = 400


def eel(c, x, y, ln, ang, seed=0):
    """水下一条往上游的长鱼剪影：头朝 ang，身子摆成 S 形。"""
    F = Frame((x, y), rot=ang)
    t = np.linspace(0, 1, 30)
    spine = np.stack([-ln * t, 0.12 * ln * np.sin(t * 2 * math.pi + seed)], 1)
    pts = F(spine)
    m = c.stroke(pts, ln * 0.16, add=False, taper=(0.15, 0.95))
    c.engrave(m, 0.97, [c.ph_lin(ang + 90, seed=seed), c.ph_lin(ang + 30, seed=seed + 1)], spacing=7, swell=1.4)
    ex, ey = F.p(-ln * 0.05, -ln * 0.015)
    c.add(c.ellipse(ex, ey, 6, 4, ang), 'abyss')
    c.erase(c.ellipse(ex, ey, 5, 3, ang))


def draw(c):
    w.night(c, top=0.75, bottom=0.5, horizon=SEA, seed=61)
    w.water(c, SEA, tone=0.22, seed=61)
    # 水下的东西：从四周往中间游
    for x, y, ln, ang, s in ((300, 700, 380, -20, 1), (1000, 720, 400, 200, 2), (560, 820, 300, -75, 3), (820, 600, 260, 195, 4)):
        eel(c, x, y, ln, ang, s)
    # 三滴血和它们砸出的涟漪
    for (x, y, r, sy), k in zip(((470, 150, 22, 470), (660, 260, 28, 440), (820, 90, 18, 455)), range(3)):
        w.drop(c, x, y, r)
        w.ripples(c, x, sy, [40 + 10 * k, 80 + 10 * k, 130 + 10 * k], erase=True, w=3.2)
        c.add(c.ellipse(x, sy, 20, 6))
        # 往下散开的血丝
        for j in range(4):
            a = math.radians(70 + 15 * j + 10 * k)
            c.line([(x, sy + 6), (x + 40 * math.cos(a), sy + 70 * math.sin(a)), (x + 60 * math.cos(a) + 20, sy + 160 * math.sin(a))],
                   w=4.5, taper=(0.0, 0.9))
