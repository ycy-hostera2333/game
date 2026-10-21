"""战斗背景 · 码头街道（普通战斗，策划/10 §12.1 P0）。癫狂层：港里升起的巨大触须、仓库黑窗里睁开的眼、雾里的一只大眼。

夜雾里的灰潮镇码头：左边是一排往远处收的仓库和窄楼（在调查员身后），右边是港口（在敌人身后）：
石砌码头沿、黑水、几艘泊着的船只剩桅杆和缆绳，远处海面上一座灯塔。前景是透视的石板路，两根煤气灯柱。
全图只用淡排线（游戏里着淡墨，墨覆盖率 ≤ 40%），中间留给角色的那一带最空。
"""
import math

import numpy as np

from etch import ellipse_pts, poly, smoothstep, spline

NAME = '码头街道'
MAX_MAD = 0.6            # 背景的癫狂层也要压着，不能盖过角色
SIZE = (2560, 1440)

HORIZON = 853            # 角色脚底的地平线（1080p 的 y = 640）
VP = (1500, 690)          # 灭点


def draw(c):
    rng = np.random.default_rng(5)
    W, H = c.w, c.h
    vx, vy = VP

    # ------------------------------------------------------------ 天：雾，上浓下淡；远处的灯塔
    sky = c.rect(0, 0, W, 700)
    fog = 0.35 + 0.25 * c.grad(0, 700, 0, 0) + 0.12 * c.noise(260, 1)
    c.engrave(sky, np.clip(fog, 0, 1), [c.ph_wave(-2, 10, 900, wobble=6, seed=2), c.ph_lin(4, wobble=4, seed=3)], spacing=14, wmax=2.2,
              bands=[(0.1, 0.7), (0.55, 0.95)], solid=None)
    lx = 2280
    tower = c.mask(poly([(lx - 22, 700), (lx - 14, 560), (lx + 14, 560), (lx + 22, 700)]))
    c.paint(tower, 0.55)
    c.paint(c.mask(poly([(lx - 20, 560), (lx, 530), (lx + 20, 560)])), 0.6)
    for a in (-14, -6, 2, 10):                                              # 灯塔的光柱，刻在雾里
        q = math.radians(180 + a)
        c.erase(c.stroke([(lx - 4, 548), (lx + 900 * math.cos(q), 548 + 900 * math.sin(q))], 3, add=False, taper=(0.0, 1.0)))

    # ------------------------------------------------------------ 港口：右半边的黑水，船和桅杆
    water = c.mask(poly([(1240, 700), (W, 700), (W, HORIZON - 40), (1240, HORIZON - 40)]))
    c.engrave(water, 0.62 + 0.1 * c.noise(80, 4), [c.ph_wave(0, 3, 220, wobble=2, seed=5), c.ph_lin(-6, seed=6)], spacing=11, wmax=2.2)
    for k in range(40):                                                     # 水面的反光
        x, y = rng.uniform(1260, W), rng.uniform(710, HORIZON - 50)
        c.erase(c.stroke([(x, y), (x + rng.uniform(20, 70), y)], 2, add=False, taper=0.5))
    for x0, h, lean in ((1420, 420, -2), (1780, 520, 1), (2080, 380, -1), (2420, 460, 2)):   # 船：船身 + 桅杆 + 缆绳
        hull = c.mask(poly([(x0 - 120, 690), (x0 + 130, 686), (x0 + 100, 730), (x0 - 96, 732)]))
        c.paint(hull, 0.75)
        top = (x0 + lean * 20, 690 - h)
        c.stroke([(x0, 690), top], 6, taper=(0.0, 0.3))
        c.stroke([(x0 - 70, 690 - h * 0.62), (x0 + 70, 690 - h * 0.6)], 4, taper=0.1)    # 帆桁
        for sd in (-1, 1):
            c.stroke([top, (x0 + sd * 150, 694)], 1.6, taper=0)
        c.stroke(spline([(x0 + 130, 700), (x0 + 220, 740), (x0 + 300, 770)]), 2.0, taper=0)   # 系缆
    # 码头沿：一条石砌的边，往灭点收
    quay = c.mask(poly([(1180, HORIZON - 40), (W, HORIZON - 40), (W, HORIZON + 10), (1150, HORIZON + 10)]))
    c.engrave(quay, 0.45, [c.ph_lin(0, wobble=1.5, seed=7), c.ph_lin(80, seed=8)], spacing=11)
    c.stroke([(1150, HORIZON - 40), (W, HORIZON - 40)], 4, taper=0)
    for x in range(1200, W, 110):
        c.stroke([(x, HORIZON - 40), (x - 4, HORIZON + 10)], 2.5, taper=0)
    for x in (1350, 1900, 2350):                                            # 系缆桩
        c.paint(c.mask(spline([(x - 18, HORIZON - 40), (x - 22, HORIZON - 90), (x, HORIZON - 104), (x + 22, HORIZON - 90),
                               (x + 18, HORIZON - 40)], closed=True)), 0.7)

    # ------------------------------------------------------------ 左边的街：仓库立面往灭点收
    facades = [(0, 140, 330), (330, 260, 420), (600, 330, 540), (820, 420, 640), (980, 500, 720), (1100, 560, 790)]
    for i, (x0, y_top, x1) in enumerate(facades):
        depth = i / (len(facades) - 1)
        y_bot = HORIZON - 10 - 120 * depth
        roof = [(x0, y_top + 40), ((x0 + x1) / 2, y_top - 40 * (1 - depth)), (x1, y_top + 40)]
        face = c.mask(poly([(x0, y_top + 40), roof[1], (x1, y_top + 40), (x1, y_bot), (x0, y_bot)]))
        tone = 0.5 - 0.25 * depth + 0.05 * c.noise(60, 9 + i)
        c.engrave(face, np.clip(tone, 0, 1), [c.ph_lin(88, wobble=1.5, seed=10 + i), c.ph_lin(10, seed=20 + i)], spacing=11, wmax=2.2)
        c.stroke(poly(roof), 4 - 2 * depth, taper=0)
        c.stroke([(x1, y_top + 40), (x1, y_bot)], 3 - depth, taper=0)
        # 窗：一格格的，大部分黑，几扇亮着
        cols = max(2, int((x1 - x0) / 90))
        rows = max(2, int((y_bot - y_top - 80) / 120))
        for r in range(rows):
            for k in range(cols):
                wx = x0 + (k + 0.5) * (x1 - x0) / cols
                wy = y_top + 90 + r * (y_bot - y_top - 120) / rows
                ww, wh = (x1 - x0) / cols * 0.4, 50 * (1 - 0.5 * depth)
                win = c.rect(wx - ww / 2, wy, wx + ww / 2, wy + wh)
                lit = rng.random() < 0.18
                c.paint(win, 0.0 if lit else 0.85)
                c.stroke(poly([(wx - ww / 2, wy), (wx + ww / 2, wy), (wx + ww / 2, wy + wh), (wx - ww / 2, wy + wh), (wx - ww / 2, wy)]),
                         2, taper=0)
    # 仓库的吊臂和挂着的货箱（让「码头」更明确）
    c.stroke([(560, 300), (720, 300)], 8, taper=0)
    c.stroke([(700, 300), (700, 420)], 2, taper=0)
    c.paint(c.rect(670, 420, 730, 480), 0.7)

    # ------------------------------------------------------------ 地面：透视的石板路，靠近手牌的地方渐渐淡出
    ground = c.mask(poly([(0, HORIZON - 10), (1150, HORIZON + 10), (W, HORIZON + 10), (W, H), (0, H)]))
    gtone = 0.28 + 0.1 * c.noise(90, 30)
    c.engrave(ground, np.clip(gtone * (1 - 0.6 * smoothstep(1000, 1440, c.yy)), 0, 1), [c.ph_lin(2, wobble=1.5, seed=31)], spacing=12,
              wmax=2.0)
    rows = [HORIZON + 10 + 600 * (k / 10) ** 1.7 for k in range(11)]
    for k in range(1, len(rows)):                                           # 石板：横缝随透视变疏，竖缝错开（一行一行地砌）
        y0, y1 = rows[k - 1], rows[k]
        fade = 1 - 0.55 * smoothstep(1050, 1440, y1)
        c.stroke(spline([(0, y1 + rng.uniform(-2, 2)), (W / 2, y1 + rng.uniform(-3, 3)), (W, y1 + rng.uniform(-2, 2))]),
                 (1.2 + 1.4 * k / 10) * fade, taper=0)
        step = 60 + 22 * k
        off = (k % 2) * step / 2 + rng.uniform(-10, 10)
        for x in np.arange(-step + off, W + step, step):
            xs = vx + (x - vx) * (0.3 + 0.7 * (y0 - HORIZON) / 600)
            xe = vx + (x - vx) * (0.3 + 0.7 * (y1 - HORIZON) / 600)
            j = rng.uniform(-6, 6)
            c.stroke([(xs + j, y0 + 2), (xe + j, y1 - 2)], (1.0 + 1.2 * k / 10) * fade, taper=0)

    # ------------------------------------------------------------ 煤气灯柱：两根
    for gx, top in ((1040, 360), (2460, 300)):
        c.stroke([(gx, top + 60), (gx, HORIZON + 30)], 9, taper=0)
        c.stroke(poly([(gx - 34, top), (gx + 34, top), (gx + 24, top + 60), (gx - 24, top + 60), (gx - 34, top)]), 4, taper=0)
        c.paint(c.mask(poly([(gx - 44, top), (gx, top - 30), (gx + 44, top)])), 0.7)
        c.erase(1 - smoothstep(30, 160, np.hypot(c.xx - gx, c.yy - top - 30)))
        c.stroke(poly([(gx - 34, top), (gx + 34, top), (gx + 24, top + 60), (gx - 24, top + 60), (gx - 34, top)]), 4, taper=0)

    # ============================================================ 癫狂层
    # 港里升起的几条巨大触须，卷进雾里
    from cards._common import tentacle
    for ctrl, w in (([(1620, 760), (1560, 520), (1660, 300), (1820, 180), (1880, 240), (1840, 300)], 130),
                    ([(2200, 760), (2280, 560), (2180, 380), (2240, 200), (2340, 160), (2380, 230)], 110),
                    ([(1350, 770), (1320, 640), (1380, 540), (1460, 500)], 70)):
        tentacle(c, ctrl, w, layer='mad', tone=0.45, taper=0.9, outline=5.0)
    # 仓库的黑窗里睁开的眼
    from cards._common import eye
    for x, y, s in ((165, 330, 26), (390, 470, 24), (700, 560, 20), (880, 640, 16), (250, 640, 22)):
        eye(c, x, y, s, layer='mad', look=0.4, width=3.4)
    # 雾里的一只大眼（很淡）
    ex, ey = 1100, 220
    up = spline([(ex - 300, ey, 'c'), (ex, ey - 110), (ex + 300, ey, 'c')])
    lo = spline([(ex - 300, ey, 'c'), (ex, ey + 90), (ex + 300, ey, 'c')])
    c.stroke(up, 6, layer='mad', taper=0.1)
    c.stroke(lo, 4, layer='mad', taper=0.1)
    c.add(c.stroke(ellipse_pts(ex, ey - 6, 70, 70), 4, closed=True, add=False), 'mad')
    c.add(c.ellipse(ex, ey - 6, 22, 34) * 0.8, 'mad')
