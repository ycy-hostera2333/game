"""U14 惊惧失神：僵立的人，手里的东西正往下掉（策划/10 §4.6）。

一个人正面僵直地站着（剪影），只有一张发白的脸和瞪圆的两只眼；双臂垂着，右手的手指松开，
一盏提灯正翻着往下掉，拖着几道下落线，灯里的火歪向一边。身后一圈圈同心的恐惧波纹。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, smoothstep, spline
from cards.C02 import lantern

NAME = '惊惧失神'

CX = 560


def draw(c):
    # ------------------------------------------------------------ 背景：一圈圈同心的波纹，从人身上往外推
    r = np.hypot((c.xx - CX) / 1.1, c.yy - 330)
    c.engrave(np.ones((c.h, c.w), np.float32), np.clip(0.25 + 0.55 * (0.5 + 0.5 * np.sin(r / 26)) * smoothstep(120, 300, r) + 0.06 * c.noise(60, 1), 0, 1),
              [r + 2 * c.noise(30, 2), c.ph_lin(60, seed=3)], spacing=8.5, swell=1.1)

    # ------------------------------------------------------------ 人：正面、僵直
    body = [(CX - 70, 864), (CX - 90, 600), (CX - 120, 380), (CX - 100, 300), (CX - 40, 270), (CX + 40, 270), (CX + 100, 300),
            (CX + 120, 380), (CX + 90, 600), (CX + 70, 864)]
    m = c.mask(spline(body, closed=True))
    c.clear(np.clip(c.shadow_of(m, 0, 0, blur=6) * 2, 0, 1))
    c.engrave(m, 0.95, [c.ph_lin(85, seed=4), c.ph_lin(15, seed=5), c.ph_lin(-40, seed=6)], spacing=8, swell=1.2)
    c.outline(body, w=OUTLINE, light=0.4)
    c.line([(CX, 290), (CX, 864)], w=3, taper=0)                            # 风衣的门襟
    c.line([(CX - 40, 280), (CX - 10, 380)], w=3, taper=0.2)
    c.line([(CX + 40, 280), (CX + 10, 380)], w=3, taper=0.2)
    for sd in (-1, 1):                                                       # 垂着的手臂
        arm = [(CX + sd * 104, 310), (CX + sd * 140, 400), (CX + sd * 150, 560), (CX + sd * 120, 570), (CX + sd * 110, 420)]
        am = c.mask(spline(arm, closed=True))
        c.engrave(am, 0.9, [c.ph_lin(80), c.ph_lin(10)], spacing=8)
        c.outline(arm if sd > 0 else arm[::-1], w=STRUCT + 1, light=0.4)
    # 右手（画面右边）松开：手掌和张开的手指
    hx, hy = CX + 140, 590
    hand = [(hx - 26, hy - 20), (hx + 20, hy - 20), (hx + 26, hy + 30), (hx - 20, hy + 34)]
    c.engrave(c.mask(spline(hand, closed=True)), 0.3, [c.ph_lin(80)], spacing=6.5, wmax=2.2)
    c.outline(hand, w=STRUCT, light=0.4)
    for k in range(4):
        x0 = hx - 18 + k * 13
        c.stroke(spline([(x0, hy + 30), (x0 + 3 + k * 3, hy + 60), (x0 + 8 + k * 6, hy + 80)]), 7, taper=(0.0, 0.5))
    c.add(c.ellipse(CX - 140, 590, 26, 34))                                  # 左手（垂着、握着）
    # 头：发白的脸，瞪圆的眼
    c.fill(c.ellipse(CX, 200, 74, 90), value=0.95)
    face = c.ellipse(CX, 214, 56, 72)
    c.clear(face)
    c.engrave(face, 0.25 * c.grad(CX - 56, 0, CX + 56, 0) + 0.1, [c.ph_lin(80)], spacing=7, wmax=2)
    c.stroke(ellipse_pts(CX, 214, 56, 72), 3.4, closed=True)
    for sd in (-1, 1):
        c.stroke(ellipse_pts(CX + sd * 24, 196, 13, 13), 3.4, closed=True)
        c.add(c.ellipse(CX + sd * 24, 196, 4.5, 4.5))
        c.stroke([(CX + sd * 10, 172), (CX + sd * 38, 166)], 3.4, taper=0.3)  # 挑起的眉
    c.stroke(ellipse_pts(CX, 254, 9, 13), 3.2, closed=True)                  # 张着的嘴
    c.fill(c.ellipse(CX, 130, 90, 18), value=1.0)                            # 帽檐
    c.fill(c.mask(np.array([(CX - 50, 130), (CX - 44, 70), (CX + 44, 70), (CX + 50, 130)])), value=1.0)

    # ------------------------------------------------------------ 掉下去的提灯：翻着，拖着下落线
    lx, ly = CX + 240, 760
    for k in range(4):
        x = lx - 60 + k * 36
        c.erase(c.stroke([(x, ly - 300), (x + 6, ly - 120)], 5, add=False, taper=(0.9, 0.1)))
        c.stroke([(x + 3, ly - 280), (x + 7, ly - 130)], 2.0, taper=(0.9, 0.1))
    lantern(c, Frame((lx, ly), rot=-32, scale=0.62))
