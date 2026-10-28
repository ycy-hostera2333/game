"""LU02 啃噬：斜在地上的一根长骨，上沿被啃掉一口一口，满是牙印，碎渣散在旁边（代价 1，多段攻击）。"""
import math

import numpy as np

from etch import Frame, OUTLINE, STRUCT, spline
from cards import _wolf as w

NAME = '啃噬'
BITES = (-150, -30, 110, 230)


def draw(c):
    w.night(c, top=0.9, bottom=0.75, seed=171)
    B = Frame((620, 470), rot=-22)
    bone = [(-470, -80), (-430, -130), (-360, -120), (-320, -62), (-200, -58), (0, -56), (200, -58), (320, -62), (360, -124), (430, -130), (480, -76), (470, 0),
            (480, 76), (430, 130), (360, 120), (320, 62), (200, 58), (0, 56), (-200, 58), (-320, 62), (-360, 122), (-430, 132), (-480, 80), (-460, 0)]
    m = c.mask(spline(B(bone), closed=True))
    bites = np.zeros_like(m)                                               # 啃掉的几口：上沿的半圆缺口
    for bx in BITES:
        bites = np.maximum(bites, c.ellipse(*B.p(bx, -62), 44, 32, -22))
    c.clear(m)
    lam = c.relief(m * (1 - bites), radius=50, blur=3)
    c.engrave(m * (1 - bites), np.clip(0.55 - 0.6 * lam, 0, 1),
              [c.ph_axis(B.p(-480, 0), B.p(480, 0), bow=-10, half=60, seed=172), c.ph_lin(B.angle(60), seed=173)], spacing=7.5)
    c.outline(B.cp(bone), w=OUTLINE)
    c.erase(bites)
    w.night(c, top=0.9, bottom=0.75, seed=171, mask=bites * (1 - c.ellipse(*B.p(0, 0), 2, 2)))   # 缺口里透出背景
    for bx in BITES:                                                       # 缺口的齿痕：一圈小锯齿
        for k in range(9):
            a = math.radians(20 + 140 * k / 8)
            p = B.p(bx + 44 * math.cos(a), -62 + 32 * math.sin(a))
            q = B.p(bx + 32 * math.cos(a), -62 + 20 * math.sin(a))
            c.line([p, q], w=3.2, taper=(0.0, 0.8))
        c.line(B.cp([(bx - 44, -56), (bx, -28), (bx + 44, -56)]), w=STRUCT, taper=0.2)
    rng = np.random.default_rng(171)
    for _ in range(26):                                                   # 碎渣
        x, y = B.p(rng.uniform(-300, 300), rng.uniform(80, 230) * rng.choice([-1, 1]))
        r = rng.uniform(5, 13)
        a = rng.uniform(0, 180)
        pts = [(x + r * math.cos(math.radians(a + 120 * j)) * rng.uniform(0.6, 1.4), y + r * math.sin(math.radians(a + 120 * j)))
               for j in range(3)]
        c.clear(c.mask(pts), layers=('ink',))
        c.stroke(pts + [pts[0]], 2.2, taper=0)
