"""U09 崩坏边缘：站在码头最边缘的鞋尖，碎石往下掉（策划/10 §4.6）。

侧面看码头的断面：左边是石砌的码头，顶面一条线；右边是悬空，下面很远处是黑水。
一只旧皮靴站在最边上，鞋尖探出边缘；边缘的石头裂开，几块碎石正往下掉。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, smoothstep, spline

NAME = '崩坏边缘'

EDGE_X, TOP_Y = 760, 560


def draw(c):
    rng = np.random.default_rng(21)

    # ------------------------------------------------------------ 悬空的一边：雾，越往下越黑，最底下是水
    void = c.rect(EDGE_X - 40, 0, c.w, c.h)
    tone = 0.22 + 0.74 * c.grad(0, 60, 0, 800) ** 1.1 + 0.06 * c.noise(90, 1)
    c.engrave(np.ones((c.h, c.w), np.float32), tone, [c.ph_lin(-4, wobble=2.5, seed=2), c.ph_lin(60, seed=3), c.ph_lin(-40, seed=4)],
              spacing=10, swell=1.2)
    for y in (790, 815, 836, 852):                                          # 远处的水面，几道亮线
        for x in np.arange(EDGE_X + 40, c.w, 70):
            c.erase(c.stroke(poly([(x + rng.uniform(0, 30), y), (x + rng.uniform(40, 60), y)]), 2.0, add=False, taper=0.5))

    # ------------------------------------------------------------ 码头：顶面 + 石砌立面
    face = c.mask(poly([(-10, TOP_Y), (EDGE_X, TOP_Y), (EDGE_X - 10, 640), (EDGE_X + 6, 760), (EDGE_X - 4, 870), (-10, 870)]))
    c.engrave(face, 0.55 + 0.2 * c.grad(EDGE_X, 0, 0, 0) + 0.05 * c.noise(40, 5), [c.ph_lin(0, wobble=1.5, seed=5),
                                                                                    c.ph_lin(80, seed=6)], spacing=9)
    # 石块：横缝 + 错开的竖缝，每块边上一道亮边
    rows = [TOP_Y + 26, 660, 760, 870]
    for k in range(len(rows) - 1):
        y0, y1 = rows[k], rows[k + 1]
        c.stroke(poly([(-10, y0), (EDGE_X, y0 + 2)]), 4, taper=0)
        xs = np.arange(-60 + 90 * (k % 2), EDGE_X - 40, 180)
        for x in xs:
            c.stroke(poly([(x, y0), (x + 4, y1)]), 3.5, taper=0)
            c.erase(c.stroke(poly([(x + 8, y0 + 6), (x + 10, y1 - 6)]), 2.4, add=False, taper=0.2))
    # 顶面：一窄条，看得到一点石板的厚度
    top = c.rect(-10, TOP_Y - 6, EDGE_X, TOP_Y + 26)
    c.engrave(top, 0.25, [c.ph_lin(0, wobble=0.8)], spacing=7, wmax=2)
    c.stroke(poly([(-10, TOP_Y - 6), (EDGE_X, TOP_Y - 6)]), OUTLINE, taper=0)
    # 码头的外沿：不规则的崩口
    lip = [(EDGE_X - 60, TOP_Y - 6), (EDGE_X, TOP_Y - 6), (EDGE_X + 10, TOP_Y + 14), (EDGE_X - 6, TOP_Y + 30), (EDGE_X + 4, 600),
           (EDGE_X - 12, 640), (EDGE_X + 8, 700), (EDGE_X - 2, 760), (EDGE_X + 6, 870)]
    c.stroke(spline(lip[1:]), OUTLINE, taper=0, light=0.3)
    # 裂缝：从鞋尖下面裂到立面
    for pts in ([(EDGE_X - 70, TOP_Y - 4), (EDGE_X - 52, TOP_Y + 20, 'c'), (EDGE_X - 64, TOP_Y + 50, 'c'), (EDGE_X - 40, 640, 'c'),
                 (EDGE_X - 58, 700)],
                [(EDGE_X - 52, TOP_Y + 20), (EDGE_X - 20, TOP_Y + 40, 'c'), (EDGE_X - 10, TOP_Y + 70)]):
        c.line(pts, w=4.5, taper=(0.1, 0.6))

    # ------------------------------------------------------------ 碎石：从崩口往下掉，带速度线
    for x, y, r in ((EDGE_X + 60, TOP_Y + 80, 34), (EDGE_X + 150, TOP_Y + 170, 22), (EDGE_X + 40, TOP_Y + 220, 26),
                    (EDGE_X + 180, TOP_Y + 270, 15), (EDGE_X + 100, TOP_Y + 300, 18), (EDGE_X + 220, TOP_Y + 100, 12),
                    (EDGE_X + 120, TOP_Y + 30, 13)):
        for dx in (-r * 0.4, r * 0.4):                                    # 往下掉：上方拖出两道留白的速度线
            c.erase(c.stroke(poly([(x + dx, y - r * 0.6), (x + dx, y - r - r * 3.2)]), 3.2, add=False, taper=(0.9, 0.0)))
        n = rng.integers(6, 9)
        ang = np.sort(rng.uniform(0, 2 * math.pi, n))
        pts = [(x + r * rng.uniform(0.75, 1.2) * math.cos(a), y + r * rng.uniform(0.75, 1.2) * math.sin(a), 'c') for a in ang]
        m = c.mask(spline(pts, closed=True, step=1))
        c.clear(np.clip(c.shadow_of(m, 0, 0, blur=2.5) * 2.2, 0, 1))
        lam = c.relief(m, radius=r * 0.6, blur=0.8)
        c.engrave(m, np.clip(0.8 - 0.95 * lam, 0, 1), [c.ph_lin(rng.uniform(0, 180), wobble=0.2)], spacing=5.5, wmax=2.2)
        c.outline(pts, w=4.0, light=0.4)
    for _ in range(26):                                                     # 碎屑
        x, y = EDGE_X + rng.uniform(0, 160), TOP_Y + rng.uniform(10, 300)
        rr = rng.uniform(2, 4.5)
        c.erase(c.ellipse(x, y, rr + 2.5, rr + 2.5))
        c.add(c.ellipse(x, y, rr, rr))

    boot(c, Frame((EDGE_X - 330, TOP_Y - 6), scale=0.96), 'far')
    boot(c, Frame((EDGE_X - 210, TOP_Y - 6)), 'near')


def boot(c, F, which):
    """侧面的旧皮靴（朝右），原点在鞋跟下沿。near 的鞋尖探出码头边缘。"""
    far = which == 'far'
    # 裤腿：从画面上方垂下来，在脚背上堆出一个褶
    leg = [(-40, -190), (-46, -520), (-50, -900), (150, -900), (146, -520), (150, -230), (176, -150), (120, -150), (40, -170)]
    m = c.mask(spline(F(leg), closed=True))
    lam = c.relief(m, radius=60, blur=4)
    c.engrave(m, np.clip((0.98 if far else 0.92) - 0.45 * lam, 0.55, 1),
              [c.ph_lin(85, seed=10), c.ph_lin(15, seed=11), c.ph_lin(-40, seed=12)], spacing=8.5, swell=1.1)
    c.outline(F.cp(leg), w=OUTLINE, light=0.4)
    if not far:
        for pts in ([(10, -190), (60, -236), (120, -220), (150, -190)], [(-10, -380), (30, -450)], [(130, -330), (110, -420)]):
            c.line(F.cp(pts), w=3.4, taper=0.35)
    # 靴子：鞋跟、足弓、前掌、微微上翘的鞋尖、鞋面、鞋带
    shoe = [(-24, 0, 'c'), (-28, -40), (-36, -110), (-30, -170), (110, -170), (140, -128), (200, -96), (270, -76), (320, -60),
            (342, -36), (340, -14), (326, -4, 'c'), (170, -4), (120, -14), (54, -14), (50, 0, 'c')]
    m = c.mask(spline(F(shoe), closed=True))
    lam = c.relief(m, radius=40, blur=3)
    c.engrave(m, np.clip((0.86 if far else 0.62) - 0.6 * lam + 0.2 * c.grad(*F.p(0, -170), *F.p(0, 0)), 0.04, 1),
              [c.ph_axis(F.p(0, -60), F.p(340, -40), bow=-14, half=70, seed=13), c.ph_lin(25, seed=14), c.ph_lin(-50, seed=15)],
              spacing=7.5, swell=1.0)
    c.outline(F.cp(shoe), w=OUTLINE, light=0.4)
    # 鞋底一道黑边（在鞋跟、前掌处贴地）
    sole = [(-24, 0), (50, 0), (54, -14), (120, -14), (170, -4), (326, -4), (340, -14)]
    c.stroke(F(spline(sole)), 9, taper=(0.0, 0.3))
    c.line(F.cp([(-28, -30), (50, -30), (52, -4)]), w=3.0, taper=0.1)        # 鞋跟的分界
    c.line(F.cp([(260, -78), (276, -40), (300, -8)]), w=3.0, taper=0.3)      # 鞋头包头的缝线
    for k in range(5):                                                      # 鞋带
        y = -160 + k * 22
        x = 110 + k * 16
        c.line(F.cp([(x - 10, y + 4), (x + 22, y + 12)]), w=3.4, taper=0)
        c.dots([F.p(x - 10, y + 4)], 3.4)
    for pts in ([(150, -110), (170, -80), (176, -50)], [(196, -94), (210, -66)]):
        c.line(F.cp(pts), w=2.6, taper=0.4)
    c.erase(c.stroke(spline(F([(230, -82), (290, -66), (330, -44)])), 4.5, add=False, taper=0.5))
