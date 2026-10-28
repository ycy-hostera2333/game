"""LU11 血之狂宴：雾里的滩涂上三个蹲着的黑影，头顶下着一阵血雨，地上全是溅开的血点（全体伤害，每个流血的敌人抽 1）。"""
import math

import numpy as np

from etch import spline
from cards import _wolf as w

NAME = '血之狂宴'


def hunched(c, x, y, s, seed=0):
    """蹲着的人形黑影，背对着光、低着头。"""
    body = [(-90, 0), (-110, -90), (-80, -170), (-10, -210), (50, -200), (70, -160), (90, -150), (110, -110), (80, -100),
            (90, -40), (100, 0)]
    m = c.mask(spline([(x + px * s, y + py * s) for px, py in body], closed=True))
    c.engrave(m, 0.95, [c.ph_lin(80, seed=seed), c.ph_lin(20, seed=seed + 1)], spacing=7, swell=1.4)
    c.erase(c.stroke(spline([(x - 104 * s, y - 80 * s), (x - 76 * s, y - 166 * s), (x - 10 * s, y - 206 * s)]), 4, add=False, taper=0.3))


def draw(c):
    w.night(c, top=0.65, bottom=0.35, horizon=560, seed=271)
    w.ground(c, 560, tone=0.35, seed=271)
    for x, y, s, k in ((300, 700, 1.1, 1), (640, 640, 0.9, 2), (960, 720, 1.2, 3)):
        hunched(c, x, y, s, seed=271 + k)
        w.splatter(c, x + 30, y + 30, 26 * s, n=16, seed=271 + k)
    rng = np.random.default_rng(271)
    for _ in range(110):                                                     # 血雨：细长的斜滴
        x, y = rng.uniform(0, 1248), rng.uniform(0, 820)
        ln = rng.uniform(26, 60)
        c.line([(x, y), (x - ln * 0.25, y + ln)], w=rng.uniform(5, 9), taper=(0.7, 0.0))
