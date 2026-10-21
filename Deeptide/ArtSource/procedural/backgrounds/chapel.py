"""战斗背景 · 沉没礼拜堂（Boss 战，策划/10 §12.1 P0）。癫狂层：柱子上爬满触须，穹顶上睁开一圈眼，水里的倒影是一只大眼。

半淹在海水里的哥特礼拜堂内景：两排尖拱柱廊往深处收，尽头是一扇碎了一半的玫瑰窗，光从窗里斜射进来；
地面是水，漫到柱脚，水面上漂着长椅的碎木；拱顶挂着海藻和渔网。中间留给角色，只有水面和远处的窗。
"""
import math

import numpy as np

from etch import ellipse_pts, poly, smoothstep, spline

NAME = '沉没礼拜堂'
SIZE = (2560, 1440)
MAX_MAD = 0.6

HORIZON = 853
VX, VY = 1290, 560                 # 灭点：玫瑰窗中心下方


def arch(cx, base, half, rise):
    """尖拱的一条曲线：两段弧在顶上交成尖。"""
    left = [(cx - half, base), (cx - half * 0.92, base - rise * 0.55), (cx - half * 0.55, base - rise * 0.9), (cx, base - rise, 'c')]
    right = [(cx, base - rise, 'c'), (cx + half * 0.55, base - rise * 0.9), (cx + half * 0.92, base - rise * 0.55), (cx + half, base)]
    return np.vstack([spline(left), spline(right)])


def draw(c):
    rng = np.random.default_rng(9)
    W, H = c.w, c.h

    # ------------------------------------------------------------ 远处的墙和玫瑰窗：窗是亮的，光从窗里斜射进来
    wall = np.ones((H, W), np.float32)
    c.engrave(wall, 0.32 + 0.08 * c.noise(200, 1) + 0.2 * smoothstep(300, 1100, np.abs(c.xx - VX)), [c.ph_lin(1, wobble=2, seed=1),
              c.ph_lin(60, wobble=2, seed=2)], spacing=13, wmax=2.2, bands=[(0.1, 0.7), (0.55, 0.95)], solid=None)
    for y in np.arange(120, HORIZON - 40, 46):                              # 远墙的石砌：一层层错缝
        c.stroke([(0, y), (W, y)], 1.4, taper=0)
    rx, ry, rr = VX, 340, 190
    rose = c.ellipse(rx, ry, rr, rr)
    c.erase(rose)
    c.stroke(ellipse_pts(rx, ry, rr, rr), 7, closed=True, taper=0)
    c.stroke(ellipse_pts(rx, ry, rr * 0.35, rr * 0.35), 5, closed=True, taper=0)
    for k in range(12):                                                    # 窗棂：放射的花瓣
        a = 2 * math.pi * k / 12
        c.stroke([(rx + rr * 0.35 * math.cos(a), ry + rr * 0.35 * math.sin(a)), (rx + rr * math.cos(a), ry + rr * math.sin(a))], 4,
                 taper=0)
        c.stroke(ellipse_pts(rx + rr * 0.67 * math.cos(a + math.pi / 12), ry + rr * 0.67 * math.sin(a + math.pi / 12), 34, 34), 3,
                 closed=True, taper=0)
    broken = c.mask(poly([(rx + 40, ry - rr - 10), (rx + rr + 10, ry - 60), (rx + rr * 0.6, ry + 20), (rx + 90, ry - 30), (rx + 60, ry - 110)]))
    c.erase(broken * c.ellipse(rx, ry, rr + 8, rr + 8))                   # 右上碎掉的一块
    for k in range(5):                                                     # 光柱，往左下斜
        a = math.radians(118 + k * 7)
        p0 = (rx + 60 * math.cos(a), ry + 60 * math.sin(a))
        p1 = (rx + 1300 * math.cos(a), ry + 1300 * math.sin(a))
        c.erase(c.stroke([p0, p1], 60 - k * 6, add=False, taper=(0.0, 0.2)) * 0.6)

    # ------------------------------------------------------------ 柱廊：两边各五根，往灭点收，柱间是尖拱
    for sd in (-1, 1):
        prev = None
        for i in range(6):
            t = (5 - i) / 5                                                 # 1 = 最近
            x = VX + sd * (260 + 1150 * t ** 1.4)
            w = 22 + 120 * t ** 1.4
            top = VY - 260 - 420 * t ** 1.2
            bot = HORIZON - 40 + 600 * t ** 2.2
            col = c.rect(x - w / 2, top, x + w / 2, min(bot, H))
            c.clear(col)
            across = (c.xx - (x - w / 2)) / max(w, 1)                       # 柱子是圆的：左亮右暗
            c.engrave(col, np.clip(0.45 + 0.45 * across + 0.06 * c.noise(60, 10 + i), 0, 1),
                      [c.ph_lin(90, wobble=1.0, seed=20 + i), c.ph_lin(30, seed=30 + i)], spacing=9, wmax=2.4)
            c.stroke([(x - w / 2, top), (x - w / 2, min(bot, H))], 2 + 3 * t, taper=0)
            c.stroke([(x + w / 2, top), (x + w / 2, min(bot, H))], 2 + 3 * t, taper=0)
            for k in range(3):                                              # 柱子上的束柱竖线
                xx = x - w / 2 + w * (k + 1) / 4
                c.stroke([(xx, top + 20), (xx, min(bot, H))], 1.2 + 1.2 * t, taper=0)
            c.stroke([(x - w * 0.7, top), (x + w * 0.7, top)], 4 + 6 * t, taper=0)   # 柱头
            if prev is not None:                                            # 和上一根之间的尖拱
                px, ptop, pw = prev
                cx = (x + px) / 2
                half = abs(x - px) / 2
                base = (top + ptop) / 2
                c.stroke(arch(cx, base, half, half * 1.3), 3 + 4 * t, taper=0)
                c.stroke(arch(cx, base - 20 * t, half * 0.86, half * 1.15), 1.6 + 2 * t, taper=0)
            prev = (x, top, w)
    # 拱顶的肋：从两边的柱头往中间收
    for k in range(5):
        y = 60 + k * 70
        c.stroke(spline([(0, y + 200), (VX, y - 40), (W, y + 200)]), 2.4, taper=0)

    # ------------------------------------------------------------ 水：漫到柱脚，横波纹，越近越宽；中间是窗的倒影（亮）
    water = c.rect(0, HORIZON - 40, W, H)
    yd = np.clip((c.yy - HORIZON + 40) / (H - HORIZON + 40), 0, 1)
    phase = (H - HORIZON) * (yd + 0.8 * yd * yd) / 1.8 + 5 * np.sin(c.xx / (60 + 120 * yd) + 2 * c.noise(120, 40))
    reflect = 1 - smoothstep(80, 320, np.abs(c.xx - VX) + 0 * c.yy)
    tone = np.clip(0.55 - 0.35 * reflect * (1 - 0.5 * yd) + 0.08 * c.noise(80, 41), 0, 1) * (1 - 0.45 * smoothstep(1050, 1440, c.yy))
    c.engrave(water, tone, [phase, c.ph_lin(-3, wobble=3, seed=42)], spacing=12, wmax=2.3, bands=[(0.05, 0.6), (0.5, 0.95)])
    c.stroke([(0, HORIZON - 40), (W, HORIZON - 40)], 3, taper=0)
    for k in range(60):                                                     # 倒影里的碎光
        y = rng.uniform(HORIZON, H - 100)
        x = VX + rng.normal(0, 120 + (y - HORIZON) * 0.4)
        c.erase(c.stroke([(x, y), (x + rng.uniform(30, 90), y)], 2.5, add=False, taper=0.5))
    # 漂着的长椅碎木
    for x, y, ln, a in ((420, 980, 260, 6), (2050, 1040, 300, -5), (2350, 920, 180, 10), (240, 1180, 320, -4)):
        q = math.radians(a)
        d = np.array([math.cos(q), math.sin(q)])
        n = np.array([-d[1], d[0]])
        p = np.array([x, y])
        plank = c.mask(np.array([p - n * 14, p + d * ln - n * 14, p + d * ln + n * 14, p + n * 14]))
        c.paint(plank, 0.55)
        c.stroke(np.array([p - n * 14, p + d * ln - n * 14, p + d * ln + n * 14, p + n * 14, p - n * 14]), 3, taper=0)
    # 拱顶挂下来的海藻
    for x0 in (300, 640, 1900, 2240, 2480):
        for k in range(3):
            xx = x0 + k * 26
            ln = rng.uniform(160, 320)
            c.stroke(spline([(xx, 0), (xx + 12, ln * 0.4), (xx - 10, ln * 0.7), (xx + 6, ln)]), 7, taper=(0.1, 0.8))

    # ============================================================ 癫狂层
    from cards._common import tentacle, eye
    for ctrl, w in (([(200, 1300), (260, 1000), (180, 700), (280, 420), (200, 200)], 110),
                    ([(2380, 1300), (2300, 980), (2400, 680), (2300, 400), (2380, 220)], 110),
                    ([(760, 900), (720, 700), (800, 520), (760, 380)], 60),
                    ([(1820, 900), (1860, 700), (1780, 520), (1820, 380)], 60)):
        tentacle(c, ctrl, w, layer='mad', tone=0.45, taper=0.9, outline=5.0)
    for k in range(9):                                                     # 拱顶上一圈眼
        a = math.pi * (0.12 + 0.76 * k / 8)
        x, y = VX - 900 * math.cos(a), 330 - 260 * math.sin(a)
        eye(c, x, y, 34, layer='mad', look=0.0, width=3.6)
    # 水里窗的倒影变成一只大眼
    ex, ey = VX, 1080
    up = spline([(ex - 360, ey, 'c'), (ex, ey - 120), (ex + 360, ey, 'c')])
    lo = spline([(ex - 360, ey, 'c'), (ex, ey + 100), (ex + 360, ey, 'c')])
    c.stroke(up, 7, layer='mad', taper=0.1)
    c.stroke(lo, 5, layer='mad', taper=0.1)
    c.add(c.stroke(ellipse_pts(ex, ey - 6, 90, 70), 5, closed=True, add=False), 'mad')
    c.add(c.ellipse(ex, ey - 6, 22, 60) * 0.8, 'mad')
