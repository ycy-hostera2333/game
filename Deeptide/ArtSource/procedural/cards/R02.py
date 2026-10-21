"""R02 圣痕：摊开的掌心，十字伤痕放出光芒（策划/10 §4.6）。

能力牌圆窗。一只右手掌心朝外举着，五指张开；掌心中央一道十字形的伤口，伤口里放出光，
光芒用放射的白线在暗背景上刻出来，手的边缘被光吃掉一圈。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, smoothstep, spline
from cards._common import burst

NAME = '圣痕'

PALM = (624, 500)


def draw(c):
    px, py = PALM
    # ------------------------------------------------------------ 背景：暗，从掌心放出白线
    c.engrave(np.ones((c.h, c.w), np.float32), 0.88 + 0.04 * c.noise(80, 1),
              [c.ph_ring(px, py), c.ph_ray(px, py, r0=300), c.ph_lin(30, seed=2)], spacing=8.5, swell=1.2)
    burst(c, px, py, 120, 520, n=64, width=4.5, erase=True, seed=1)
    c.erase(1 - smoothstep(200, 380, np.hypot(c.xx - px, c.yy - py)))

    F = Frame((px, py))
    # 手掌 + 手腕
    palm = [(-120, 140), (-130, 20), (-118, -90), (-60, -120), (40, -122), (110, -100), (130, -20), (122, 90), (80, 170), (70, 380),
            (-90, 380), (-100, 200)]
    fingers = [(-104, -96, -150, 120, 40), (-42, -118, -170, 190, 46), (26, -120, -178, 200, 46), (92, -102, -168, 170, 42)]
    thumb = [(110, 0), (160, -40), (210, -90), (236, -84), (228, -50), (180, 30), (120, 100)]

    def part(pts, tone_k=1.0, frame=F, w=STRUCT + 1):
        m = c.mask(spline(frame(pts), closed=True))
        lam = c.relief(m, radius=26, blur=2)
        r = np.hypot(c.xx - px, c.yy - py)
        c.engrave(m, np.clip((0.12 + 0.55 * (1 - lam) ** 1.5) * smoothstep(60, 320, r) * tone_k, 0, 1),
                  [c.ph_ring(px, py), c.ph_lin(70)], spacing=7.5, wmax=2.4)
        c.outline(frame.cp(pts), w=w, light=0.45)
        return m

    for i, (x, y, ang, ln, w) in enumerate(fingers):
        G = Frame(F.p(x, y), rot=(i - 1.5) * 7)
        hw = w / 2
        pts = [(-hw, 20), (-hw - 2, -ln * 0.5), (-hw + 4, -ln * 0.88), (0, -ln - 4), (hw - 4, -ln * 0.88), (hw + 2, -ln * 0.5), (hw, 20)]
        part(pts, frame=G, w=STRUCT)
        for j in (0.38, 0.7):
            c.line(G.cp([(-hw * 0.6, -ln * j), (0, -ln * j + 4), (hw * 0.6, -ln * j)]), w=2.2, taper=0.3)
    part(thumb)
    part(palm)
    # 掌纹
    for pts in ([(-110, -40), (-30, -60), (60, -50), (120, -70)], [(-100, 40), (-20, 10), (60, 30)], [(90, 120), (40, 40), (20, -40)]):
        c.line(F.cp(pts), w=2.4, taper=0.35)

    # ------------------------------------------------------------ 十字伤口：黑的十字 + 一圈光
    c.erase(1 - smoothstep(30, 110, np.hypot(c.xx - px, c.yy - py)))
    burst(c, px, py, 46, 110, n=24, width=2.0, seed=2)
    for (x0, y0, x1, y1) in ((0, -48, 0, 52), (-34, -12, 34, -12)):
        c.stroke(F([(x0, y0), (x1, y1)]), 12, taper=0.3)
        c.erase(c.stroke(F([(x0 - 3, y0 + 3), (x1 - 3, y1 + 3)]), 3, add=False, taper=0.5))
    # 血：一滴顺手腕往下淌
    c.stroke(F.cp([(0, 52), (4, 120), (-2, 200), (2, 260)]), 6, taper=(0.0, 0.0))
    c.add(c.ellipse(*F.p(2, 268), 8, 10))
