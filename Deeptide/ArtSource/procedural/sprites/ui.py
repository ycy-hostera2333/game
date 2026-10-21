"""战斗 UI 套件（策划/10 §6、§12.1 P0）：九宫格的铜版边框、顶栏的饰带、能量圈、牌堆图标、理智柱、生命心形。

全部是单色精灵，游戏里由 Image 着色。九宫格的边距写在 C# 那边（Ui 套件按 ID 取），这里只管画：
- ui_plaque：按钮、牌堆的牌匾框（双线 + 四角卷草），边距 28
- ui_scroll：提示框的框（单线 + 两端像卷轴一样卷起），边距 30
- ui_bar / ui_bar_block：生命条的框（细 / 有格挡时加粗），边距 12
- ui_rule：顶栏下沿的饰带（横向平铺）
- ui_orb：能量圈的外环（刻度 + 放射纹）
- ui_gauge：理智条后面的柱子（柱头 + 柱身 + 柱础），竖向九宫格，边距上 70 下 60
- icon_heart / icon_draw / icon_discard / icon_exhaust：生命、抽牌堆、弃牌堆、消耗堆
"""
import math

import numpy as np

from etch import ellipse_pts, poly, spline
from sprites.frames import corner_flourish, rrect


def _k(c, w):
    return c.w / w


def plaque(c):
    W = 160
    k = _k(c, W)
    c.stroke(rrect(3 * k, 3 * k, c.w - 3 * k, c.h - 3 * k, 14 * k), 4.5 * k, closed=True, taper=0)
    c.stroke(rrect(11 * k, 11 * k, c.w - 11 * k, c.h - 11 * k, 8 * k), 2.0 * k, closed=True, taper=0)
    for x, y, sx, sy in ((14, 14, 1, 1), (W - 14, 14, -1, 1), (14, W - 14, 1, -1), (W - 14, W - 14, -1, -1)):
        _flourish(c, x, y, sx, sy, 22, W)


def _flourish(c, x, y, sx, sy, size, W):
    k = _k(c, W)
    s = size * k
    X = lambda u, v: (x * k + sx * u * s, y * k + sy * v * s)
    c.stroke(spline([X(0.1, 0.9), X(0.1, 0.25), X(0.3, 0.1), X(0.9, 0.1)]), 2.0 * k, taper=(0.4, 0.4))
    c.add(c.ellipse(*X(0.36, 0.36), 2.4 * k, 2.4 * k))


def scroll(c):
    W = 200
    k = _k(c, W)
    c.stroke(rrect(4 * k, 10 * k, c.w - 4 * k, c.h - 10 * k, 6 * k), 3.0 * k, closed=True, taper=0)
    for y in (10, W - 10):                                                     # 上下两头卷起的纸边
        c.stroke(spline([(14 * k, y * k), (60 * k, (y + (6 if y < 100 else -6)) * k), (140 * k, (y + (6 if y < 100 else -6)) * k),
                         (186 * k, y * k)]), 2.0 * k, taper=0.2)
    for sx in (0, 1):
        for sy in (0, 1):
            x = (10 if sx == 0 else W - 10) * k
            y = (10 if sy == 0 else W - 10) * k
            c.stroke(ellipse_pts(x, y, 7 * k, 7 * k, a0=0, a1=300), 2.4 * k, taper=(0.0, 0.6))


def bar(c, heavy=False):
    W = 64
    k = _k(c, W)
    w = (5.0 if heavy else 2.8) * k
    c.stroke(rrect(2 * k, 2 * k, c.w - 2 * k, c.h - 2 * k, 5 * k), w, closed=True, taper=0)
    for x in (6, W - 6):                                                       # 两端的小铆钉
        c.add(c.ellipse(x * k, c.h / 2, 2.6 * k, 2.6 * k))


def rule(c):
    """顶栏下沿：一道粗线、一串珠点、一道细线；横向平铺，128 一个循环。"""
    k = _k(c, 128)
    c.stroke([(0, 4 * k), (c.w, 4 * k)], 3.4 * k, taper=0)
    for x in range(8, 128, 16):
        c.add(c.ellipse(x * k, 12 * k, 2.6 * k, 2.6 * k))
        c.stroke([((x + 6) * k, 12 * k), ((x + 10) * k, 12 * k)], 1.4 * k, taper=0)
    c.stroke([(0, 19 * k), (c.w, 19 * k)], 1.4 * k, taper=0)


def orb(c):
    """能量圈外环：一圈粗环、一圈刻度、内圈细线；外环上 12 道放射的短线，像怀表的表圈。"""
    k = _k(c, 256)
    cx = cy = 128 * k
    c.stroke(ellipse_pts(cx, cy, 120 * k, 120 * k), 9 * k, closed=True, taper=0)
    c.stroke(ellipse_pts(cx, cy, 100 * k, 100 * k), 3 * k, closed=True, taper=0)
    for j in range(60):
        q = 2 * math.pi * j / 60
        r0 = (102 if j % 5 else 104) * k
        r1 = (110 if j % 5 else 116) * k
        c.stroke([(cx + r0 * math.cos(q), cy + r0 * math.sin(q)), (cx + r1 * math.cos(q), cy + r1 * math.sin(q))],
                 (3.2 if j % 5 == 0 else 1.6) * k, taper=0)
    for j in range(4):                                                         # 上下左右四个小凸起（像表冠）
        q = math.pi / 2 * j
        c.add(c.ellipse(cx + 124 * k * math.cos(q), cy + 124 * k * math.sin(q), 7 * k, 7 * k))


def gauge(c):
    """理智柱：柱头（一盏小提灯的灯罩）、细长的柱身（左右两道线 + 一道中线槽）、柱础（三级台阶）。"""
    W, H = 96, 420
    k = _k(c, W)
    # 柱身
    for x in (22, W - 22):
        c.stroke([(x * k, 70 * k), (x * k, (H - 60) * k)], 3.4 * k, taper=0)
    for x in (14, W - 14):
        c.stroke([(x * k, 74 * k), (x * k, (H - 64) * k)], 1.6 * k, taper=0)
    # 柱头：灯罩形
    cap = [(10, 70), (86, 70), (78, 54), (64, 44), (58, 22), (48, 8, 'c'), (38, 22), (32, 44), (18, 54)]
    c.stroke(spline([(x * k, y * k) + tuple(r) for x, y, *r in cap], closed=True), 3.4 * k, closed=True, taper=0)
    c.add(c.mask(poly([(10 * k, 62 * k), (86 * k, 62 * k), (86 * k, 72 * k), (10 * k, 72 * k)])))
    c.add(c.ellipse(48 * k, 30 * k, 4 * k, 7 * k))
    # 柱础
    for j, (x0, x1) in enumerate(((10, 86), (4, 92), (0, 96))):
        y = H - 60 + j * 18
        c.stroke(poly([(x0 * k, y * k), (x1 * k, y * k), (x1 * k, (y + 14) * k), (x0 * k, (y + 14) * k), (x0 * k, y * k)]), 2.6 * k,
                 taper=0)


def heart(c):
    k = _k(c, 128)
    pts = [(64, 116, 'c'), (20, 72), (12, 44), (24, 22), (46, 18), (64, 36, 'c'), (82, 18), (104, 22), (116, 44), (108, 72)]
    m = c.mask(spline([(x * k, y * k) + tuple(r) for x, y, *r in pts], closed=True))
    c.add(m)
    c.erase(c.ellipse(36 * k, 40 * k, 9 * k, 6 * k, rot=-35))


def pile_draw(c):
    """抽牌堆：一叠背面朝上的牌，最上面那张有个卷草纹。"""
    k = _k(c, 128)
    for j in range(3):
        o = j * 6
        card = [(26 + o, 22 + o * 0.5), (92 + o, 18 + o * 0.5), (100 + o, 106 + o * 0.5), (34 + o, 110 + o * 0.5)]
        m = c.mask(np.array(card[::-1]) * k)
        c.erase(m)
        c.stroke(np.array(card + card[:1]) * k, 4 * k, taper=0)
    c.add(c.ellipse(76 * k, 74 * k, 14 * k, 14 * k))
    c.erase(c.ellipse(76 * k, 74 * k, 7 * k, 7 * k))


def pile_discard(c):
    """弃牌堆：几张散开、斜着的牌。"""
    k = _k(c, 128)
    for j, (cx, cy, a) in enumerate(((50, 70, -18), (74, 66, 8), (64, 78, 32))):
        q = math.radians(a)
        R = np.array([[math.cos(q), -math.sin(q)], [math.sin(q), math.cos(q)]])
        card = np.array([(-26, -38), (26, -38), (26, 38), (-26, 38)]) @ R.T + [cx, cy]
        c.erase(c.mask(card * k))
        c.stroke(np.vstack([card, card[:1]]) * k, 4 * k, taper=0)
    c.stroke([(54 * k, 70 * k), (80 * k, 84 * k)], 3 * k, taper=0.2)


def pile_exhaust(c):
    """消耗堆：一张牌烧成灰，上面一缕烟。"""
    k = _k(c, 128)
    card = [(34, 50), (94, 50), (94, 116), (34, 116)]
    c.stroke(np.array(card + card[:1]) * k, 4 * k, taper=0)
    burn = [(34, 50), (50, 66), (62, 54), (76, 70), (94, 52), (94, 50)]
    c.erase(c.mask(np.array([(30, 30), (98, 30)] + burn[::-1]) * k))
    c.stroke(spline([(x * k, y * k) for x, y in burn]), 4 * k, taper=0)
    c.stroke(spline([(64 * k, 52 * k), (54 * k, 36 * k), (70 * k, 22 * k), (58 * k, 6 * k)]), 5 * k, taper=(0.1, 0.9))


SPRITES = {
    'ui_plaque': (160, 160, plaque),
    'ui_scroll': (200, 200, scroll),
    'ui_bar': (64, 32, lambda c: bar(c)),
    'ui_bar_block': (64, 32, lambda c: bar(c, heavy=True)),
    'ui_rule': (128, 24, rule),
    'ui_orb': (256, 256, orb),
    'ui_gauge': (96, 420, gauge),
    'icon_heart': (128, 128, heart),
    'icon_draw': (128, 128, pile_draw),
    'icon_discard': (128, 128, pile_discard),
    'icon_exhaust': (128, 128, pile_exhaust),
}
