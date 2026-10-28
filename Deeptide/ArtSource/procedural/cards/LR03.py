"""LR03 倒刺爪：一根带倒钩的弯爪的特写，内弯上一排倒刺，每根刺尖挂着一滴血（攻击打穿格挡就施加流血）。圆窗。"""
import math

import numpy as np

from etch import Frame, STRUCT, spline
from cards import _wolf as w

NAME = '倒刺爪'


def draw(c):
    w.night(c, top=0.85, bottom=0.65, seed=301)
    F = Frame((470, 150), rot=-28, scale=1.0)
    w.fang(c, F, length=640, width=190, curve=0.55, tone=0.35)
    # 内弯（+x 一侧）上的倒刺：小三角，尖朝爪根
    for k, t in enumerate((0.25, 0.4, 0.55, 0.7)):
        L = 640
        bx = 190 * 0.48 * (1 - t) + 0.55 * L * 0.45 * t ** 1.4
        by = L * t
        base = [F.p(bx - 6, by - 26), F.p(bx - 6, by + 26)]
        tip = F.p(bx + 60 - 10 * k, by - 50)
        pts = [base[0], tip, base[1]]
        m = c.mask(pts)
        c.clear(m)
        c.engrave(m, 0.4, [c.ph_lin(F.angle(70))], spacing=5.5, wmax=2)
        c.stroke(pts, 3.2, taper=0)
        w.drop(c, tip[0] + 4, tip[1] + 26, 9)
    w.splatter(c, 980, 760, 24, n=12, seed=301)
