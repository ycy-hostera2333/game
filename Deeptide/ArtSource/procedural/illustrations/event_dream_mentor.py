"""事件 8 梦中的导师：你梦见了带你入行的老调查员。他的眼睛是两个空洞，里面有海水在流（Data/events.csv）。

一张梦里的胸像：背景是往中心旋进去的螺旋排线（梦），中间是老调查员的头和肩——宽檐帽、长胡子、竖起的大衣领。
眼窝是两个黑洞，海水从里面流出来，顺着脸颊流进胡子，再往下淌出画面（深渊遮罩，着深渊青）。
癫狂层：流下来的水变成两条触须，螺旋里游着几条小鱼。
"""
import math

import numpy as np

from etch import Frame, ellipse_pts, poly, resample, smoothstep, spline
from cards._common import tentacle
from illustrations._kit import GAP, OUT, STRUCT, hatch, ray_r0

NAME = '梦中的导师'
SIZE = (1920, 1280)
CX, CY = 960, 560            # 脸的中心


def draw(c):
    rng = np.random.default_rng(8)
    W, H = c.w, c.h

    # ------------------------------------------------------------ 梦：往脸后面旋进去的螺旋，四角最暗
    r = np.hypot(c.xx - CX, (c.yy - CY) * 1.15)
    a = np.arctan2(c.yy - CY, c.xx - CX)
    # 绕一圈相位跳 2 × 63 = 126 = 14 条线距，接缝处的线正好对上
    spiral = r + 63 * a / math.pi + 6 * c.noise(80, 1)
    hatch(c, c.rect(0, 0, W, H), np.clip(0.25 + 0.6 * smoothstep(250, 1100, r) + 0.06 * c.noise(120, 2), 0, 1),
          [spiral, c.ph_ray(CX, CY, r0=ray_r0(600))], spacing=9, wmax=2.6, swell=1.2)

    # ------------------------------------------------------------ 肩和大衣：从画面底边升上来，领子竖着
    coat = [(300, H + 20), (360, 1020), (520, 900), (700, 840), (960, 820), (1220, 840), (1400, 900), (1560, 1020), (1620, H + 20)]
    m = c.mask(spline(coat, closed=True))
    lam = c.relief(m, radius=120, blur=6)
    hatch(c, m, np.clip(0.95 - 0.4 * lam, 0.5, 1), [c.ph_lin(80, seed=3), c.ph_lin(20, seed=4), c.ph_lin(-40, seed=5)], swell=1.3)
    c.outline(coat, w=OUT + 1, light=0.4)
    for sd in (-1, 1):                                                     # 竖起的领子：两片尖角
        lap = [(CX + sd * 70, 880), (CX + sd * 200, 690, 'c'), (CX + sd * 300, 790), (CX + sd * 250, 960), (CX + sd * 120, 1010, 'c')]
        lm = c.mask(spline(lap, closed=True))
        lam2 = c.relief(lm, radius=40)
        hatch(c, lm, np.clip(0.8 - 0.4 * lam2, 0.3, 1), [c.ph_lin(60 * sd, seed=6), c.ph_lin(-30 * sd, seed=7)], spacing=7.5)
        c.outline(lap, w=STRUCT + 1, light=0.4)

    # ------------------------------------------------------------ 胡子：长，一缕缕的波浪线
    beard = [(CX - 150, 640), (CX - 170, 760), (CX - 120, 900), (CX - 40, 1010), (CX + 30, 1020), (CX + 110, 920), (CX + 165, 770),
             (CX + 150, 640), (CX + 60, 690), (CX - 60, 690)]
    bm = c.mask(spline(beard, closed=True))
    c.clear(bm)
    hatch(c, bm, np.clip(0.35 + 0.35 * c.grad(CX - 170, 0, CX + 170, 0) + 0.1 * c.noise(30, 8), 0, 1),
          [c.ph_wave(85, 6, 90, wobble=2, seed=9), c.ph_lin(60, seed=10)], spacing=7.5)
    for k in range(12):                                                    # 几缕深一点的须
        x = CX - 140 + k * 25
        c.line([(x, 700), (x + rng.uniform(-20, 20), 820), (x + rng.uniform(-30, 30), 940 - abs(k - 6) * 18)], w=2.6, taper=(0.1, 0.6))
    c.outline(beard[:8], w=STRUCT, light=0.3)

    # ------------------------------------------------------------ 脸：瘦，高颧骨，左上来光
    face = [(CX - 150, 640), (CX - 160, 520), (CX - 130, 420), (CX - 60, 370), (CX + 60, 370), (CX + 130, 420), (CX + 160, 520),
            (CX + 150, 640), (CX + 60, 690), (CX - 60, 690)]
    fm = c.mask(spline(face, closed=True))
    c.clear(fm)
    lam = c.relief(fm, radius=70, blur=4)
    hatch(c, fm, np.clip(0.75 - 0.75 * lam + 0.05 * c.noise(30, 11), 0, 1),
          [c.ph_axis((CX - 160, 520), (CX + 160, 520), bow=-30, half=160, seed=11), c.ph_lin(-20, seed=12)], spacing=7.5)
    c.outline(face[:8], w=STRUCT + 0.5, light=0.4)
    # 眉骨、颧骨、鼻子、嘴
    for sd in (-1, 1):
        c.line([(CX + sd * 30, 470), (CX + sd * 80, 455), (CX + sd * 130, 470)], w=6, taper=0.2)
        c.line([(CX + sd * 120, 560), (CX + sd * 90, 600), (CX + sd * 60, 610)], w=2.6, taper=0.4)
    c.line([(CX - 6, 490), (CX - 16, 590), (CX - 4, 606), (CX + 20, 600)], w=4, taper=0.2)
    c.line([(CX - 50, 660), (CX, 668), (CX + 50, 660)], w=4, taper=0.2)
    # 眼窝：两个黑洞
    for sd in (-1, 1):
        # 眼窝：先一圈往里压暗的凹陷，再是全黑的洞
        socket = c.ellipse(CX + sd * 72, 512, 66, 50, rot=sd * 8)
        hatch(c, socket, np.clip(0.9 - 0.7 * c.radial(CX + sd * 72, 512, 20, 66, sy=50 / 66), 0, 1),
              [c.ph_ring(CX + sd * 72, 512, 1.0, 0.76), c.ph_lin(40, seed=18)], spacing=7.5)
        c.fill(c.ellipse(CX + sd * 72, 512, 44, 32, rot=sd * 8), value=1.0)

    # ------------------------------------------------------------ 帽：宽檐，帽檐压在眉骨上
    brim = c.ellipse(CX, 392, 330, 52)
    c.fill(brim, value=0.97)
    crown = [(CX - 170, 392), (CX - 150, 220), (CX - 60, 180), (CX + 60, 180), (CX + 150, 220), (CX + 170, 392)]
    cm = c.mask(spline(crown, closed=True))
    lam = c.relief(cm, radius=60, blur=4)
    hatch(c, cm, np.clip(0.95 - 0.4 * lam, 0.5, 1), [c.ph_lin(88, seed=13), c.ph_lin(30, seed=14)], swell=1.2)
    c.outline(crown, w=OUT, light=0.4)
    c.paint(c.mask(poly([(CX - 166, 330), (CX + 166, 330), (CX + 168, 370), (CX - 168, 370)])), 1.0)   # 帽带
    c.erase(c.stroke(spline([(CX - 300, 380), (CX - 100, 360), (CX + 100, 360)]), 4, add=False, taper=0.3) * brim)   # 帽檐上的高光

    # ------------------------------------------------------------ 海水：从眼窝流出来，顺着脸颊、胡子淌出画面（深渊遮罩）
    streams = []
    for sd in (-1, 1):
        x0 = CX + sd * 72
        pts = [(x0, 530), (x0 + sd * 10, 620), (x0 - sd * 6, 720), (x0 + sd * 20, 840), (x0 + sd * 10, 980), (x0 + sd * 40, 1120),
               (x0 + sd * 30, H + 20)]
        streams.append(pts)
        path = spline(pts)
        for w, k in ((34, 0), (20, 1)):
            wave = c.stroke(path, w, add=False, taper=(0.0, 0.0), profile=lambda u: 0.6 + 0.8 * u)
            c.add(wave * c.lines(c.ph_along(pts[0], pts[-1], seed=15 + k) + 4 * np.sin(c.yy / 26), 7, 2.6), 'abyss')
        c.add(c.stroke(path, 6, add=False, taper=(0.0, 0.0)), 'abyss')
        c.clear(c.stroke(path, 30, add=False, taper=(0.0, 0.0), profile=lambda u: 0.6 + 0.8 * u), layers=('ink',))
        c.stroke(offset_pts(pts, -16), 2.4, taper=0.1)
        c.stroke(offset_pts(pts, 16), 2.4, taper=0.1)
    # 胸口积起来的一小汪水
    pool = c.ellipse(CX, H - 40, 320, 50)
    c.add(pool * c.lines(c.ph_lin(0, wobble=0.8, seed=17), 7, 2.4), 'abyss')

    # ============================================================ 癫狂层：水流变成触须，螺旋里的小鱼
    for pts in streams:
        tentacle(c, pts, 46, layer='mad', tone=0.5, taper=0.5, outline=4.0)
    for k in range(9):
        t = k / 9 * 2 * math.pi
        rr = 520 + 120 * (k % 3)
        fish(c, CX + rr * math.cos(t) * 1.05, CY + rr * math.sin(t) * 0.8, 40, math.degrees(t) + 90)


def fish(c, x, y, s, ang):
    """一条小鱼：梭形的身子 + 叉尾 + 一个眼点，只在癫狂层。"""
    F = Frame((x, y), rot=ang, scale=s / 60)
    body = [(-60, 0), (-20, -18), (30, -14), (60, 0), (30, 14), (-20, 18)]
    c.stroke(spline(F(body), closed=True), 3, layer='mad', closed=True)
    c.stroke(poly(F([(-60, 0), (-86, -20), (-80, 0), (-86, 20), (-60, 0)])), 3, layer='mad', taper=0)
    c.add(c.ellipse(*F.p(36, -2), 4, 4), 'mad')


def offset_pts(pts, d):
    from etch import offset
    return offset(spline(pts), d)
