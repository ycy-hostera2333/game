"""事件 6 教团集会：火堆边围着一圈戴兜帽的人，低声念着你听不懂的祷文（Data/events.csv）。

夜里的海滩：火堆在画面中央偏下，火光把周围一圈沙地照亮（墨擦淡），外面是黑的。火堆后面一圈兜帽人被火照着，
前景两三个背对我们的兜帽人是逆光的黑剪影。一柱烟从火里升到画面顶上。远处是黑海和一线地平线。
癫狂层：烟柱里显出一个巨大的兜帽轮廓，兜帽里是一圈眼睛，烟里伸出触须。
"""
import math

import numpy as np

from etch import Frame, ellipse_pts, poly, resample, smoothstep, spline
from cards._common import burst, eye, tentacle
from illustrations._kit import GAP, OUT, STRUCT, flame, hatch, hooded, night_sky, sea

NAME = '教团集会'
SIZE = (1920, 1280)
HORIZON = 520
FIRE = (960, 880)


def draw(c):
    rng = np.random.default_rng(6)
    W, H = c.w, c.h
    fx, fy = FIRE

    night_sky(c, HORIZON, top=0.92, bottom=0.7, seed=7)
    sea(c, HORIZON, 640, tone=0.88, seed=8, glints=25, rng=rng)
    c.stroke([(0, HORIZON), (W, HORIZON)], 3, taper=0)
    # 沙滩：暗，越靠近火越亮
    sand = c.rect(0, 640, W, H)
    lit = 1 - c.radial(fx, fy, 60, 760, sx=1.0, sy=0.55)
    hatch(c, sand, np.clip(0.92 - 0.75 * lit + 0.06 * c.noise(60, 9), 0, 1), [c.ph_lin(4, wobble=2, seed=10), c.ph_lin(-40, seed=11),
                                                                               c.ph_lin(60, seed=12)], swell=1.2)
    c.stroke(spline([(0, 646), (500, 636), (1100, 648), (1920, 640)]), 3, taper=0)

    # ------------------------------------------------------------ 烟柱：从火里升起，往右上飘散
    smoke_c = [(fx, fy - 120), (fx - 40, 620), (fx + 30, 440), (fx - 20, 280), (fx + 80, 120), (fx + 200, -20)]
    center = spline(smoke_c)
    sm = c.stroke(center, 120, add=False, taper=0, profile=lambda u: 0.6 + 1.6 * u)
    hatch(c, sm, np.clip(0.4 + 0.25 * c.noise(50, 13), 0, 1), [c.ph_contour(sm, blur=10) + 6 * c.noise(40, 14), c.ph_lin(70, seed=15)],
          spacing=9, wmax=2.4, solid=None)
    for k in range(4):                                                     # 烟的卷边
        sd = -1 if k % 2 else 1
        p = center[int(len(center) * (0.25 + k * 0.18))]
        c.line([(p[0] + sd * 40, p[1] + 30), (p[0] + sd * 90, p[1] - 10), (p[0] + sd * 70, p[1] - 60)], w=3, taper=0.3)

    # ------------------------------------------------------------ 火：先擦出一圈光，再画柴堆和火苗
    c.erase((1 - c.radial(fx, fy - 80, 30, 300, sy=1.2)) * 0.9)
    burst(c, fx, fy - 90, 140, 320, n=40, width=2.4, erase=True, sy=0.9, seed=3)
    for a in (-30, -10, 15, 35, 160, 190, 200):                           # 柴：交叉堆着的木头
        ang = math.radians(a)
        p0 = (fx - 110 * math.cos(ang), fy + 10 - 30 * math.sin(ang))
        p1 = (fx + 110 * math.cos(ang), fy + 10 + 30 * math.sin(ang))
        log = c.stroke([p0, p1], 26, add=False, taper=0)
        hatch(c, log, 0.85, [c.ph_along(p0, p1, seed=16), c.ph_lin(a + 80, seed=17)], spacing=7.5)
        c.stroke([p0, p1], 3, taper=0)
    flame(c, fx, fy, 200, 300, seed=4)
    for _ in range(40):                                                    # 火星
        a = rng.uniform(-math.pi, 0)
        r = rng.uniform(160, 460)
        x, y = fx + r * math.cos(a) * 0.7 + 60, fy - 200 + r * math.sin(a)
        c.erase(c.ellipse(x, y, 3, 3))

    # ------------------------------------------------------------ 火堆后面的一圈兜帽人：被火照着（亮面朝我们）
    ring = [(560, 840, 200), (680, 800, 190), (820, 780, 180), (1100, 780, 180), (1240, 800, 190), (1370, 840, 200)]
    for x, y, h in ring:
        hooded(c, x, y, h, tone=0.5, lean=(x - fx) * -0.01)
    # 兜帽人的影子朝我们拖过来
    for x, y, h in ring:
        sh = c.mask(poly([(x - 30, y), (x + 30, y), (x + (x - fx) * 0.6 + 40, y + 260), (x + (x - fx) * 0.6 - 40, y + 260)]))
        c.fill(c.shadow_of(sh, 0, 0, blur=6) * (c.yy > y), value=0.9)

    # ------------------------------------------------------------ 前景：背对我们的逆光剪影
    for x, y, h, lean in ((300, 1340, 640, 4), (1600, 1360, 680, -5), (1180, 1420, 520, -2)):
        hooded(c, x, y, h, tone=1.0, lean=lean)
        # 火光在兜帽边上描出一道亮边（朝火的一侧）
        sd = 1 if x < fx else -1
        F = Frame((x, y), rot=lean, scale=h / 380)
        rim = c.stroke(spline(F.cp([(sd * 40, -250), (sd * 28, -300), (sd * 14, -350), (sd * -4, -374)])), 5, add=False, taper=0.3)
        c.erase(rim)

    # ============================================================ 癫狂层：烟里的大兜帽，一圈眼睛，伸出的触须
    hood = [(fx - 340, 680), (fx - 300, 380), (fx - 160, 150), (fx + 40, 40, 'c'), (fx + 220, 160), (fx + 360, 400), (fx + 380, 680)]
    c.stroke(spline(hood), 8, layer='mad', taper=0.15)
    c.add(c.stroke(ellipse_pts(fx + 20, 420, 160, 210), 5, closed=True, add=False), 'mad')
    for k in range(7):
        a = math.radians(200 + k * 20)
        eye(c, fx + 20 + 100 * math.cos(a), 420 + 140 * math.sin(a) + 60, 26, layer='mad', look=0.0, width=3.6)
    eye(c, fx + 20, 400, 46, layer='mad', look=0.0, width=5.0, slit=True)
    for ctrl, w in (([(fx - 330, 660), (fx - 460, 560), (fx - 520, 400), (fx - 470, 300)], 50),
                    ([(fx + 370, 660), (fx + 500, 600), (fx + 600, 460), (fx + 560, 340)], 54)):
        tentacle(c, ctrl, w, layer='mad', tone=0.5, taper=0.9, outline=3.5)
