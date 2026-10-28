"""LC07 狂暴横扫：一爪横着扫过齐腰的荒草，四道弧形的爪风把草齐齐削断，断茎飞起来（对所有敌人两次）。"""
import math

import numpy as np

from etch import ellipse_pts
from cards import _wolf as w

NAME = '狂暴横扫'


def draw(c):
    w.night(c, top=0.7, bottom=0.4, horizon=760, seed=101)
    w.ground(c, 760, tone=0.55, seed=101)
    cut_y = 520
    rng = np.random.default_rng(101)
    for _ in range(90):                                                     # 被削断的草：下半截还立着
        x = rng.uniform(-20, 1270)
        base = 800 + rng.uniform(-10, 40)
        top = cut_y + rng.uniform(-20, 30) + 0.08 * (x - 620)
        c.line([(x, base), (x + rng.normal(0, 6), (base + top) / 2), (x + rng.normal(0, 8), top)], w=rng.uniform(3, 4.5), taper=(0.0, 0.1))
    for _ in range(40):                                                     # 飞起来的上半截
        x = rng.uniform(100, 1150)
        y = rng.uniform(150, 450)
        a = rng.uniform(0, 2 * math.pi)
        ln = rng.uniform(40, 120)
        c.line([(x, y), (x + ln * 0.5 * math.cos(a) + 6, y + ln * 0.5 * math.sin(a)), (x + ln * math.cos(a), y + ln * math.sin(a))],
               w=4.5, taper=(0.1, 0.6))
    # 爪风：四道同心弧，从左到右
    for k in range(4):
        r = 900 + 40 * k
        pts = ellipse_pts(620, cut_y + r - 60 + 22 * k, r, r * 0.9, a0=235, a1=305, n=200)
        cov = c.stroke(pts, 44 - 7 * k, add=False, taper=(0.9, 0.15))
        c.erase(np.clip(c.shadow_of(cov, 0, 0, blur=10) * 3, 0, 1))
        c.add(cov * 0.95)
