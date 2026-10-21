"""C14 左轮射击：左轮枪口焰，硝烟卷成环（策划/10 §4.6）。

左轮侧面朝右，握在手里，枪口刚喷出一团星形的火；前方一个硝烟圈慢慢卷开。背景是夜，火光把周围照亮一圈。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, smoothstep, spline

NAME = '左轮射击'


def draw(c):
    G = Frame((330, 540), rot=-9, scale=1.08)          # 枪的坐标：原点在转轮中心，+x 指向枪口
    mx, my = G.p(440, -38)                              # 枪口

    # ------------------------------------------------------------ 背景：夜，火光照亮一圈
    bg = np.ones((c.h, c.w), np.float32)
    lit = smoothstep(0, 1, c.radial(mx, my, 80, 520))
    c.engrave(bg, 0.25 + 0.6 * lit + 0.05 * c.noise(100, 1),
              [c.ph_ray(mx, my, r0=300) + 1.5 * c.noise(50, 2), c.ph_ring(mx, my) + 2 * c.noise(60, 3), c.ph_lin(30, seed=4)],
              spacing=10, swell=1.0, solid=None)

    c.erase(1 - smoothstep(90, 200, np.hypot((c.xx - mx - 40) / 1.2, c.yy - my)))     # 火光正中照得发白

    # ------------------------------------------------------------ 硝烟圈：一个卷着的圆环，前方偏上
    ring_c = (mx + 190, my - 130)
    rcx, rcy = ring_c
    R, r = 112, 40
    outer = c.ellipse(rcx, rcy, R + r, (R + r) * 0.55, rot=-14)
    inner = c.ellipse(rcx, rcy, R - r, (R - r) * 0.55, rot=-14)
    torus = np.clip(outer - inner, 0, 1)
    c.clear(torus)
    ang = np.arctan2((c.yy - rcy) / 0.55, c.xx - rcx)
    swirl = ang * 60 + np.hypot(c.xx - rcx, (c.yy - rcy) / 0.55) * 0.6           # 绕着环卷的螺旋线
    tube = np.abs(np.hypot(c.xx - rcx, (c.yy - rcy) / 0.55) - R) / r      # 0 = 环的中线，1 = 环的边
    c.engrave(torus, np.clip(0.05 + 0.4 * tube ** 2 + 0.25 * smoothstep(-0.2, 1, np.sin(ang + 0.8)), 0, 1),
              [swirl, c.ph_lin(-14, seed=6)], spacing=9, wmax=2.6)
    c.stroke(ellipse_pts(rcx, rcy, R + r, (R + r) * 0.55, rot=-14), 3.4, closed=True)
    c.stroke(ellipse_pts(rcx, rcy, R - r, (R - r) * 0.55, rot=-14), 3.0, closed=True)
    hole = inner
    c.engrave(hole, 0.75, [c.ph_lin(30, seed=7), c.ph_lin(-40, seed=8)], spacing=8)
    # 枪口和烟圈之间的一缕烟
    for off in (-14, 0, 16):
        c.stroke(spline([(mx + 60, my + off * 0.4), (mx + 150, my - 30 + off), (rcx - 130, rcy + 40 + off * 0.5)]), 2.2, taper=(0.4, 0.6))

    # ------------------------------------------------------------ 枪口焰：星形，焰心留白
    rng = np.random.default_rng(7)
    n = 13
    pts = []
    for i in range(2 * n):
        a = (i / (2 * n)) * 2 * math.pi
        if i % 2 == 0:
            ln = rng.uniform(70, 150) * (1.5 if abs(math.cos(a)) > 0.8 and math.cos(a) > 0 else 1.0)
        else:
            ln = rng.uniform(26, 40)
        pts.append((mx + 40 + ln * math.cos(a) * 1.25, my + ln * math.sin(a), 'c'))
    flash = c.mask(spline(pts, closed=True, step=1.5))
    c.clear(flash)
    core = c.ellipse(mx + 44, my, 46, 30)
    c.engrave(flash * (1 - core), 0.22 * c.radial(mx + 44, my, 30, 160), [c.ph_ray(mx + 40, my, r0=80)], spacing=7, wmax=2)
    c.outline(pts, w=4.0, light=0)
    for a in np.arange(0, 360, 15):                                        # 火星
        ang = math.radians(a + rng.uniform(-5, 5))
        r0 = rng.uniform(150, 200)
        c.stroke(poly([(mx + 40 + r0 * math.cos(ang) * 1.2, my + r0 * math.sin(ang)),
                       (mx + 40 + (r0 + rng.uniform(20, 50)) * math.cos(ang) * 1.2, my + (r0 + 30) * math.sin(ang))]),
                 2.4, taper=(0.1, 0.8))

    revolver(c, G)


def revolver(c, G):
    def part(pts, tone, phases, light=0.4, w=STRUCT):
        m = c.mask(spline(G(pts), closed=True))
        lam = c.relief(m, radius=14, blur=1.5)
        c.engrave(m, np.clip(tone - 0.5 * lam, 0, 1), phases, spacing=6.5, wmax=2.6, swell=1)
        c.outline(G.cp(pts), w=w, light=light)
        return m

    ang = G.rot
    # 握把（木，网纹）
    grip = [(-92, 16, 'c'), (-38, 36), (-52, 120), (-78, 196), (-150, 210), (-170, 180), (-136, 110), (-112, 30, 'c')]
    m = c.mask(spline(G(grip), closed=True))
    c.engrave(m, 0.62 + 0.2 * c.grad(*G.p(-60, 0), *G.p(-170, 200)), [c.ph_lin(ang + 60), c.ph_lin(ang - 30)], spacing=7.5)
    c.outline(G.cp(grip), w=STRUCT + 1, light=0.4)
    # 手：握住枪把，三根手指绕在握把前面，食指扣着扳机，拇指压在后面
    back = [(-210, 20), (-120, -10), (-70, 30), (-60, 110), (-96, 180), (-200, 200)]
    m = c.mask(spline(G(back), closed=True))
    lam = c.relief(m, radius=50, blur=3)
    c.engrave(m, np.clip(0.5 - 0.4 * lam + 0.3 * c.grad(*G.p(-100, 0), *G.p(-100, 200)), 0.05, 1),
              [c.ph_lin(ang + 70, seed=8), c.ph_lin(ang - 20, seed=9)], spacing=8)
    c.outline(G.cp(back), w=OUTLINE * 0.9, light=0.4)
    for k, y in enumerate((88, 124, 160)):                                 # 中指、无名指、小指
        f = [(-70, y - 18), (-26, y - 22), (-8, y - 6), (-14, y + 14), (-60, y + 18)]
        part(f, 0.45 + 0.08 * k, [c.ph_lin(ang + 90), c.ph_lin(ang)], w=STRUCT)
        c.line(G.cp([(-30, y - 18), (-26, y), (-30, y + 14)]), w=2.2, taper=0.3)
    # 枪身
    frame = [(-80, -60), (60, -64), (64, 36), (-36, 40), (-92, 16)]
    part(frame, 0.75, [c.ph_lin(ang), c.ph_lin(ang + 70)])
    barrel = [(56, -64), (436, -60), (440, -22), (56, -18)]
    m = c.mask(G(barrel))
    v = c.grad(*G.p(0, -60), *G.p(0, -20))
    c.engrave(m, np.clip(0.35 + 0.55 * v - 0.3 * np.exp(-((v - 0.3) / 0.1) ** 2), 0, 1), [c.ph_lin(ang), c.ph_lin(ang + 80)],
              spacing=6.5, wmax=2.4)
    c.stroke(G(barrel + barrel[:1]), STRUCT, taper=0)
    rod = [(60, -18), (300, -16), (300, -2), (60, -2)]
    m = c.mask(G(rod))
    c.engrave(m, 0.6, [c.ph_lin(ang)], spacing=5, wmax=2)
    c.stroke(G(rod + rod[:1]), 3.2, taper=0)
    c.stroke(G([(420, -60), (424, -76), (436, -76), (436, -60)]), 4, taper=0)   # 准星
    # 转轮：槽线 + 一道高光
    cyl = [(-46, -54), (46, -54), (52, 30), (-46, 34)]
    m = c.mask(G(cyl))
    c.engrave(m, 0.5 + 0.35 * c.grad(*G.p(0, -54), *G.p(0, 34)), [c.ph_lin(ang)], spacing=5.5, wmax=2.2)
    for y in (-34, -12, 10):
        c.stroke(G([(-40, y), (44, y)]), 4.5, taper=0.15)
    c.stroke(G(cyl + cyl[:1]), STRUCT, taper=0)
    c.erase(c.stroke(G([(-36, -46), (40, -46)]), 3.5, add=False, taper=0.3))
    # 击锤、扳机护圈、扳机
    hammer = [(-86, -58), (-120, -96), (-104, -104), (-60, -66)]
    part(hammer, 0.8, [c.ph_lin(ang + 45)], w=STRUCT)
    c.stroke(G(spline([(-30, 40), (-34, 80), (0, 96), (34, 70), (40, 36)])), 6, taper=0)
    c.stroke(G(spline([(-6, 40), (-2, 62), (10, 74)])), 5, taper=(0, 0.4))
    # 食指扣在扳机上；拇指从后面压过来
    idx = [(-60, 52), (-20, 50), (10, 62), (12, 80), (-20, 76), (-62, 80)]
    part(idx, 0.4, [c.ph_lin(ang + 90), c.ph_lin(ang)], w=STRUCT)
    thumb = [(-150, -8), (-96, -40), (-44, -46), (-30, -30), (-60, -20), (-120, 12)]
    part(thumb, 0.35, [c.ph_lin(ang + 80), c.ph_lin(ang + 10)], w=STRUCT + 0.5)
    # 袖口
    cuff = [(-330, -30), (-196, -10), (-180, 90), (-200, 220), (-330, 260)]
    m = c.mask(spline(G(cuff), closed=True))
    c.engrave(m, 0.92, [c.ph_lin(ang + 85), c.ph_lin(ang + 20), c.ph_lin(ang - 40)], spacing=8.5, swell=1)
    c.outline(G.cp(cuff), w=OUTLINE, light=0.4)
    c.line(G.cp([(-214, 0), (-200, 100), (-214, 210)]), w=STRUCT, taper=0.1)
