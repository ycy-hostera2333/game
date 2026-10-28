"""LR04 饱餐：退潮的沙滩上一副被啃空的大骨架，脊骨弯成一道拱，肋骨一根根翘着；远处的海平线上一弯残月（击杀加最大生命，消耗）。"""
import math

import numpy as np

from etch import STRUCT, spline
from cards import _wolf as w

NAME = '饱餐'


def draw(c):
    w.night(c, top=0.8, bottom=0.45, horizon=470, seed=311)
    w.moon(c, 1010, 170, 70, phase=(-30, -10), glow=1.8, seed=311)
    w.water(c, 470, tone=0.3, seed=311, mask=np.clip(c.yy - 470, 0, 1) * (c.yy < 540))
    w.ground(c, 540, tone=0.3, seed=311)
    # 脊骨：一节一节的椎骨沿一条拱线
    t = np.linspace(0, 1, 20)
    xs = 220 + 820 * t
    ys = 720 - 220 * np.sin(np.pi * t) ** 0.8
    for i in range(len(t)):
        r = 22 + 10 * math.sin(math.pi * t[i])
        m = c.ellipse(xs[i], ys[i], r, r * 0.8)
        c.clear(m)
        lam = c.relief(m, radius=r * 0.6, blur=1)
        c.engrave(m, np.clip(0.55 - 0.6 * lam, 0, 1), [c.ph_lin(60)], spacing=6, wmax=2.2)
        c.add(c.stroke(w.drop_pts(xs[i], ys[i], r * 0.5)[:0] if False else np.array([[xs[i] - r, ys[i]], [xs[i] + r, ys[i]]]), 0.1, add=False))
        c.stroke(np.array([[xs[i] + r * math.cos(a), ys[i] + r * 0.8 * math.sin(a)] for a in np.linspace(0, 2 * math.pi, 40)]), 3.4,
                 taper=0, closed=True)
    # 肋骨：从脊骨两侧往下弯到沙里，近侧的粗、远侧的细
    for i in range(3, 16, 2):
        x, y = xs[i], ys[i]
        h = 720 - y + 60
        for sd, wd in ((1, 13), (-1, 8)):
            pts = [(x, y), (x + sd * 70 + 20, y + h * 0.35), (x + sd * 60 + 40, y + h * 0.75), (x + sd * 20 + 50, y + h)]
            c.erase(c.line(pts, w=wd + 8, add=False, taper=(0.0, 0.6)))
            c.line(pts, w=wd, taper=(0.0, 0.6))
            if sd > 0:
                c.erase(c.line(pts[:3], w=wd * 0.35, add=False, taper=0.4))
    # 头骨：左端，长吻
    skull = [(230, 690), (160, 650), (60, 660), (20, 690), (60, 720), (160, 730)]
    m = c.mask(spline(skull, closed=True))
    c.clear(m)
    c.engrave(m, np.clip(0.45 - 0.5 * c.relief(m, radius=30), 0, 1), [c.ph_lin(30)], spacing=6.5)
    c.outline(skull, w=STRUCT)
    c.add(c.ellipse(170, 680, 18, 12))
    for x in (60, 90, 120):
        c.line([(x, 718), (x + 4, 740)], w=4, taper=(0.0, 0.8))
