"""U15 禁书残页：被撕下的一页，边缘焦黑，画着手绘符号（策划/10 §4.6）。

一页从书上撕下来的纸斜躺在深色桌面上：一边是撕裂的毛边，另外几边被烧过，焦黑往里渗、卷起；
纸上一个手绘的圆形符阵（同心圆 + 三角里一只眼 + 一圈符文），下面几行潦草的字。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, scrawl, smoothstep, spline

NAME = '禁书残页'


def draw(c):
    rng = np.random.default_rng(15)
    # ------------------------------------------------------------ 桌面：深色木头
    c.engrave(np.ones((c.h, c.w), np.float32), 0.8 + 0.05 * c.noise(80, 1), [c.ph_wave(-8, 6, 380, wobble=2, seed=2),
                                                                              c.ph_lin(60, seed=3)], spacing=8.5, swell=1.2)
    P = Frame((624, 440), rot=-7)
    # 纸的轮廓：左边是撕下来的锯齿，右边和上下是烧过的波浪边
    pts = []
    for y in np.linspace(-330, 330, 40):                                     # 左：撕边
        pts.append((-380 + rng.uniform(-14, 14) + 10 * math.sin(y / 30), y))
    for x in np.linspace(-360, 360, 30):                                     # 下：烧边
        pts.append((x, 330 + 18 * math.sin(x / 40) + rng.uniform(-6, 6)))
    for y in np.linspace(330, -330, 30):                                     # 右
        pts.append((380 + 22 * math.sin(y / 50) + rng.uniform(-6, 6), y))
    for x in np.linspace(360, -360, 30):                                     # 上
        pts.append((x, -330 + 16 * math.sin(x / 35 + 1) + rng.uniform(-6, 6)))
    poly_pts = P(pts)
    page = c.mask(poly_pts)
    c.fill(np.clip(c.shadow_of(page, 18, 22, blur=6), 0, 1) * (1 - page), value=1.0)   # 投影
    # 纸：旧，浅；靠烧过的边越来越黄、越来越黑
    edge = c.inside(page)
    burnt = 1 - smoothstep(4, 70 + 30 * c.noise(30, 4), edge)
    left_side = smoothstep(-300, -380, c.ph_along(P.p(0, -330), P.p(0, 330), wobble=0))     # 撕边那一侧不烧
    burnt = burnt * (1 - left_side)
    c.engrave(page, np.clip(0.05 + 0.95 * burnt ** 1.3 + 0.05 * c.noise(40, 5), 0, 1),
              [c.ph_lin(-7, wobble=1.2, seed=6), c.ph_lin(60, seed=7), c.ph_lin(-40, seed=8)], spacing=8, swell=1.2)
    c.stroke(np.vstack([poly_pts, poly_pts[:1]]), 3.6, taper=0)
    # 撕边：一窄条毛茸茸的纤维
    for y in np.linspace(-320, 320, 70):
        x0 = -380 + 10 * math.sin(y / 30)
        c.stroke(P([(x0, y), (x0 - rng.uniform(4, 14), y + rng.uniform(-4, 4))]), 1.4, taper=(0.0, 0.8))

    # ------------------------------------------------------------ 符阵
    cx, cy = P.p(-20, -90)
    for rr, w in ((180, 4.5), (160, 2.4), (70, 2.4)):
        c.stroke(ellipse_pts(cx, cy, rr, rr), w, closed=True, wobble=0.1)
    tri = [P.p(-20 + 150 * math.cos(a), -90 + 150 * math.sin(a)) for a in (-math.pi / 2, math.pi / 6, 5 * math.pi / 6, -math.pi / 2)]
    c.stroke(np.array(tri), 3.6, taper=0)
    for k in range(16):                                                      # 一圈符文
        a = 2 * math.pi * k / 16
        x, y = cx + 170 * math.cos(a), cy + 170 * math.sin(a)
        g = k % 4
        if g == 0:
            c.stroke(ellipse_pts(x, y, 6, 6), 2, closed=True)
        elif g == 1:
            c.stroke([(x - 6, y + 5), (x, y - 6), (x + 6, y + 5)], 2.2, taper=0)
        elif g == 2:
            c.stroke([(x, y - 7), (x, y + 7)], 2.2, taper=0)
            c.stroke([(x - 5, y), (x + 5, y)], 2.2, taper=0)
        else:
            c.stroke([(x - 6, y - 6), (x + 6, y + 6)], 2.2, taper=0)
    # 三角里的眼
    W, H = 52, 26
    up = spline([(cx - W, cy + 6, 'c'), (cx, cy + 6 - H), (cx + W, cy + 6, 'c')])
    lo = spline([(cx + W, cy + 6, 'c'), (cx, cy + 6 + H * 0.8), (cx - W, cy + 6, 'c')])
    c.stroke(up, 4.2, taper=0.1)
    c.stroke(lo, 2.6, taper=0.1)
    c.add(c.ellipse(cx, cy + 6, 13, 13))
    # 下面几行字
    for k in range(4):
        a = P.p(-320, 160 + k * 38)
        b = P.p(250 - 60 * (k == 3), 160 + k * 38)
        scrawl(c, a[0], b[0], a[1], 12, seed=20 + k, width=2.4, slope=(b[1] - a[1]) / (b[0] - a[0]))
    # 一个指印般的墨渍
    c.engrave(c.ellipse(*P.p(260, 40), 40, 50, rot=20), 0.6, [c.ph_ring(*P.p(260, 40), sy=1.25)], spacing=6, wmax=2.2)
