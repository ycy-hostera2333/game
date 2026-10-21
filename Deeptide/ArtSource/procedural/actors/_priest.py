"""溺亡司祭两个阶段共用的部分：半淹的祭坛、主教冠、头、潮汐权杖。工作画布 1320 × 1880，脚底 1805，面朝左。"""
import math

import numpy as np

from etch import STRUCT, Frame, ellipse_pts, smoothstep, spline

FEET = 1805
TOP = 1690                       # 祭坛顶面，司祭站在上面
OUT_W = 12.0


def altar(c, rng):
    """半淹的祭坛：两级石阶，水漫到半腰，石面上挂着海藻。返回遮罩。"""
    base = c.mask(np.array([(150, 1740), (1170, 1740), (1190, FEET), (130, FEET)]))
    top = c.mask(np.array([(250, TOP), (1070, TOP), (1090, 1742), (230, 1742)]))
    m = np.clip(base + top, 0, 1)
    c.add(m, 'fill')
    side = c.grad(150, 0, 1190, 0)
    for part, y0, y1 in ((base, 1740, FEET), (top, TOP, 1742)):
        c.engrave(part, np.clip(0.45 + 0.4 * side, 0, 1), [c.ph_lin(0, wobble=1.2, seed=21), c.ph_lin(70, seed=22)], spacing=8)
    for pts in ([(150, 1740), (1170, 1740)], [(250, TOP), (1070, TOP)], [(130, FEET), (1190, FEET)]):
        c.stroke(np.array(pts), OUT_W - 2, taper=0)
    for x in (420, 700, 960):                                                   # 石缝
        c.stroke([(x, TOP + 4), (x + 6, 1740)], 3, taper=0)
    for x in (300, 560, 830, 1080):
        c.stroke([(x, 1744), (x - 4, FEET - 4)], 3, taper=0)
    c.stroke([(150, 1740), (130, FEET)], OUT_W - 2, taper=0)
    c.stroke([(1170, 1740), (1190, FEET)], OUT_W - 2, taper=0)
    # 漫上来的水：石阶下半截压着一片波纹
    water = m * smoothstep(1750, 1765, c.yy + 6 * np.sin(c.xx / 40))
    c.paint(water, np.clip(0.75 * c.lines(c.yy + 5 * np.sin(c.xx / 30), 7, 2.6) + 0.15, 0, 1))
    c.erase(c.stroke(spline([(130, 1758), (400, 1752), (700, 1762), (1000, 1752), (1190, 1758)]), 3, add=False, taper=0))
    c.stroke(spline([(130, 1762), (400, 1756), (700, 1766), (1000, 1756), (1190, 1762)]), 3.4, taper=0)
    for x in (200, 520, 880, 1120):                                              # 挂在石面上的海藻
        weed = c.stroke(spline([(x, TOP + 2), (x + 10, TOP + 40), (x - 6, TOP + 80), (x + 6, TOP + 110)]), 16, add=False, taper=(0.2, 0.8))
        c.add(weed)
    return m


def mitre(c, F, tone_side):
    """主教冠：两片尖顶，中间开口；正面一枚潮纹。F 的原点在冠底中心。"""
    shape = [(-90, 0), (-96, -120), (-70, -260), (-10, -360, 'c'), (10, -300), (30, -360, 'c'), (84, -250), (100, -120), (94, 0)]
    m = c.mask(spline(F(shape), closed=True))
    c.add(m, 'fill')
    lam = c.relief(m, radius=50, blur=3)
    c.engrave(m, np.clip(0.15 + 0.5 * tone_side + 0.25 * (1 - lam), 0, 1), [c.ph_lin(F.angle(84)), c.ph_lin(F.angle(20))], spacing=8)
    c.outline(F.cp(shape), w=OUT_W - 1, light=0.45)
    c.stroke(spline(F([(-90, -40), (0, -30), (94, -40)])), 14, taper=0)     # 冠底的箍
    c.erase(c.stroke(spline(F([(-86, -46), (0, -36), (90, -46)])), 2.4, add=False, taper=0.2))
    c.stroke(spline(F([(0, -40), (6, -180), (10, -300)])), 4, taper=0.2)    # 中缝
    ex, ey = F.p(-20, -150)
    c.stroke(ellipse_pts(ex, ey, 34, 34), 4, closed=True)
    for k, dy in enumerate((-12, 0, 12)):
        c.stroke(spline([(ex - 24 + j * 8, ey + dy + 5 * math.sin(j * 1.3 + k)) for j in range(7)]), 2.6, taper=0.2)
    return m


def staff(c, x, y_top, y_bot, hand_y):
    """潮汐权杖：竖直的杖身，顶上卷成一个浪头，浪头往下滴水。"""
    shaft = c.mask(np.array([(x - 11, y_top + 120), (x + 11, y_top + 120), (x + 11, y_bot), (x - 11, y_bot)]))
    c.add(shaft, 'fill')
    c.engrave(shaft, 0.4 + 0.5 * c.grad(x - 11, 0, x + 11, 0), [c.ph_lin(90, wobble=0.4)], spacing=6)
    c.stroke([(x - 11, y_top + 120), (x - 11, y_bot)], 4, taper=0)
    c.stroke([(x + 11, y_top + 120), (x + 11, y_bot)], 6, taper=0)
    for yy in np.arange(y_top + 200, y_bot, 160):                               # 杖身的箍
        c.stroke([(x - 14, yy), (x + 14, yy)], 6, taper=0)
    # 浪头：一个向左卷的螺旋，带一排浪花爪
    t = np.linspace(0, 1.75 * math.pi, 120)
    r = 86 * (1 - t / (2.2 * math.pi))
    cx, cy = x - 40, y_top + 70
    curl = np.stack([cx - r * np.cos(t + 0.3), cy - r * np.sin(t + 0.3)], 1)
    curl = np.vstack([[[x, y_top + 140]], [[x + 10, y_top + 60]], curl])
    band = c.stroke(spline(curl), 42, add=False, taper=(0.0, 0.7))
    c.add(band, 'fill')
    c.paint(band, np.clip(c.lines(np.hypot(c.xx - cx, c.yy - cy), 7, 2.4) + 0.1, 0, 1))
    c.stroke(spline(curl), 4, taper=(0.0, 0.7))
    for k in range(5):                                                         # 浪花爪
        a = math.radians(-160 + k * 30)
        p = (cx + 100 * math.cos(a), cy + 100 * math.sin(a))
        q = (cx + 140 * math.cos(a - 0.2), cy + 140 * math.sin(a - 0.2))
        claw = c.stroke(spline([p, ((p[0] + q[0]) / 2, (p[1] + q[1]) / 2 - 6), q]), 14, add=False, taper=(0.0, 0.9))
        c.add(claw, 'fill')
        c.add(claw)
    for k in range(4):                                                         # 往下滴的水
        dx = cx - 60 + k * 30
        c.stroke([(dx, y_top + 160), (dx, y_top + 200 + k * 20)], 3, taper=(0.0, 0.5))
        c.add(c.ellipse(dx, y_top + 210 + k * 20, 5, 7))
        c.add(c.ellipse(dx, y_top + 210 + k * 20, 6, 8), 'fill')
    # 握杖的手
    hand = [(x - 34, hand_y - 30), (x + 30, hand_y - 34), (x + 40, hand_y + 30), (x - 30, hand_y + 36)]
    hm = c.mask(spline(hand, closed=True))
    c.add(hm, 'fill')
    lam = c.relief(hm, radius=16, blur=1)
    c.engrave(hm, np.clip(0.45 - 0.5 * lam, 0, 1), [c.ph_lin(80)], spacing=6, wmax=2.2)
    c.outline(hand, w=STRUCT, light=0.4)
    for k in range(3):
        c.stroke([(x - 30, hand_y - 14 + k * 16), (x - 14, hand_y - 10 + k * 16)], 2.4, taper=0.3)
