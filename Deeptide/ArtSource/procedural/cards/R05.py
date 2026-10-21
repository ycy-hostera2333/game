"""R05 明理之刃：一柄刻满星图铭文的长剑（策划/10 §4.6）。

长剑横在星空前，剑尖朝右上；宽剑身上刻着一条条星座（星点 + 连线）和一行铭文，
星空里同样的星座被连了出来，像剑把天上的东西抄了下来。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, smoothstep, spline

NAME = '明理之刃'

ROT = -9


def draw(c):
    rng = np.random.default_rng(55)
    G = Frame((300, 520), rot=ROT)                    # 剑的坐标：原点在护手中心，+x 指向剑尖

    # ------------------------------------------------------------ 星空：深色，满天的星，几个星座连了线
    sky = np.ones((c.h, c.w), np.float32)
    c.engrave(sky, 0.78 + 0.1 * c.grad(624, 0, 624, 864) + 0.05 * c.noise(100, 1),
              [c.ph_lin(-2, wobble=1.5, seed=2), c.ph_lin(64, seed=3), c.ph_lin(-50, seed=4)], spacing=9, swell=1.0, solid=None)
    stars = [(rng.uniform(0, c.w), rng.uniform(0, c.h), rng.uniform(1.5, 5.5) ** 1.1) for _ in range(140)]
    for x, y, r in stars:
        c.erase(c.ellipse(x, y, r + 2, r + 2))
        if r > 5:                                                         # 亮星：十字光芒
            for a in (0, 90):
                ang = math.radians(a + 15)
                c.erase(c.stroke(poly([(x - 4 * r * math.cos(ang), y - 4 * r * math.sin(ang)),
                                       (x + 4 * r * math.cos(ang), y + 4 * r * math.sin(ang))]), 2.0, add=False, taper=0.5))
    for cons in ([(140, 140), (230, 110), (320, 160), (380, 120), (440, 190)], [(820, 90), (880, 170), (960, 150), (1030, 210),
                 (980, 270)], [(700, 760), (780, 700), (860, 740), (930, 680)]):
        for (x0, y0), (x1, y1) in zip(cons[:-1], cons[1:]):
            c.erase(c.stroke(poly([(x0, y0), (x1, y1)]), 2.0, add=False, taper=0))
        for x, y in cons:
            c.erase(c.ellipse(x, y, 7, 7))
            c.stroke(ellipse_pts(x, y, 11, 11), 1.6, closed=True)

    # ------------------------------------------------------------ 剑身：宽，中间一道血槽；刻着星座
    L, half = 860, 46
    blade = [(30, -half, 'c'), (L - 190, -half + 4, 'c'), (L - 80, -half * 0.55), (L, 0, 'c'), (L - 80, half * 0.55),
             (L - 190, half - 4, 'c'), (30, half, 'c')]
    bm = c.mask(spline(G(blade), closed=True))
    c.clear(np.clip(c.shadow_of(bm, 0, 0, blur=6) * 2, 0, 1))              # 剑周围一圈光
    v = c.ph_along(G.p(0, 0), G.p(1, 0), wobble=0) / half                   # -1 上刃 … 1 下刃
    tone = np.where(v < 0, 0.06 + 0.2 * smoothstep(-0.8, -1.0, v), 0.22 + 0.4 * smoothstep(0.1, 0.7, v))
    c.engrave(bm, tone, [c.ph_lin(ROT, wobble=0.4, seed=5), c.ph_lin(ROT + 6, wobble=0.4, seed=6)], spacing=7, wmax=2.4)
    fuller = [(60, -9, 'c'), (L - 260, -6, 'c'), (L - 230, 0, 'c'), (L - 260, 6, 'c'), (60, 9, 'c')]
    c.engrave(c.mask(spline(G(fuller), closed=True)), 0.55, [c.ph_lin(ROT, wobble=0.3)], spacing=4.5, wmax=1.8)
    c.stroke(G(spline(fuller, closed=True)), 2.2, closed=True, taper=0)
    c.stroke(G([(40, 0), (L - 4, 0)]), 1.6, taper=(0.0, 0.3))
    c.outline(G.cp(blade), w=STRUCT + 1.5, light=0.45)
    # 星座刻纹：上下两半各一串
    for row, yy in ((0, -29), (1, 29)):
        x = 80
        while x < L - 240:
            n = rng.integers(3, 6)
            pts = [(x + i * rng.uniform(24, 36), yy + rng.uniform(-9, 9)) for i in range(n)]
            pts = [p for p in pts if p[0] < L - 220]
            if len(pts) < 2:
                break
            for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
                c.stroke(G([(x0, y0), (x1, y1)]), 2.4, taper=0)
            for px, py in pts:
                r = rng.uniform(3.5, 5.5)
                c.erase(c.ellipse(*G.p(px, py), r + 3, r + 3))
                c.dots([G.p(px, py)], r)
            x = pts[-1][0] + rng.uniform(44, 64)
            sx = x - 26                                                     # 两个星座之间一个小符号（圆 + 一道）
            c.stroke(ellipse_pts(*G.p(sx, yy), 7, 7), 2.2, closed=True)
            c.stroke(G([(sx, yy - 12), (sx, yy + 12)]), 2.2, taper=0)
    # 剑尖一颗星
    tx, ty = G.p(L - 120, -12)
    for a in (0, 45, 90, 135):
        ang = math.radians(a + ROT)
        ln = 40 if a % 90 == 0 else 20
        c.erase(c.stroke(poly([(tx - ln * math.cos(ang), ty - ln * math.sin(ang)), (tx + ln * math.cos(ang), ty + ln * math.sin(ang))]),
                         4 if a % 90 == 0 else 2.6, add=False, taper=0.5))

    # ------------------------------------------------------------ 护手：一道横档，两端是新月
    guard = [(-14, -150), (14, -150), (16, -40), (14, 40), (14, 150), (-14, 150), (-16, 40), (-16, -40)]
    gm = c.mask(spline(G(guard), closed=True))
    lam = c.relief(gm, radius=12, blur=1)
    c.engrave(gm, np.clip(0.65 - 0.7 * lam, 0, 1), [c.ph_lin(ROT + 90), c.ph_lin(ROT + 20)], spacing=6, wmax=2.2)
    c.outline(G.cp(guard), w=STRUCT, light=0.45)
    for sd in (-1, 1):
        cx, cy = G.p(0, sd * 162)
        moon = c.ellipse(cx, cy, 22, 22) * (1 - c.ellipse(*G.p(8, sd * 166), 18, 18))
        c.fill(moon, value=0.85)
    c.fill(c.ellipse(*G.p(0, 0), 20, 20), value=0.0)
    c.stroke(ellipse_pts(*G.p(0, 0), 20, 20), 3.4, closed=True)
    c.dots([G.p(0, 0)], 6)
    # 握柄：缠皮，斜纹
    grip = [(-20, -24), (-210, -22), (-210, 22), (-20, 24)]
    gm = c.mask(G(grip))
    c.engrave(gm, 0.55 + 0.3 * c.grad(*G.p(0, -24), *G.p(0, 24)), [c.ph_lin(ROT + 60), c.ph_lin(ROT - 60)], spacing=8)
    c.stroke(G(grip + grip[:1]), STRUCT, taper=0)
    # 剑首：圆饼
    pm = c.ellipse(*G.p(-236, 0), 30, 34, rot=ROT)
    lam = c.relief(pm, radius=16, blur=1)
    c.engrave(pm, np.clip(0.7 - 0.8 * lam, 0, 1), [c.ph_ring(*G.p(-236, 0))], spacing=5.5, wmax=2)
    c.stroke(ellipse_pts(*G.p(-236, 0), 30, 34, rot=ROT), STRUCT, closed=True)
