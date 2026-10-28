"""狼人的图标（策划/14 §2.7、09 M6.6）：流血状态、狼牙吊坠、狼毛斗篷、血碗、特性「兽血」。

和 icons.py 同一套规矩：不用排线，只用实墨块 + 留白；256 工作画布的坐标按 c.w / 256 缩放。
状态图标导出 64，遗物导出 128。
"""
import math

import numpy as np

from etch import ellipse_pts, poly, spline


def _drop(c, x, y, r):
    """尖朝上的一滴（256 坐标）。返回遮罩。"""
    s = c.w / 256
    pts = [(x, y - 2.3 * r, 'c'), (x + 0.45 * r, y - r), (x + r, y + 0.15 * r), (x + 0.7 * r, y + 0.85 * r), (x, y + 1.02 * r),
           (x - 0.7 * r, y + 0.85 * r), (x - r, y + 0.15 * r), (x - 0.45 * r, y - r)]
    m = c.mask(spline([(px * s, py * s) + tuple(k) for px, py, *k in pts], closed=True))
    c.add(m)
    return m


def bleed(c):
    """流血：一大滴血 + 左上的高光，右下再坠一小滴。"""
    s = c.w / 256
    _drop(c, 112, 140, 70)
    c.erase(c.ellipse(86 * s, 130 * s, 12 * s, 24 * s, 25))
    _drop(c, 200, 206, 24)


def fang_pendant(c):
    """狼牙吊坠：皮绳挂成 V 字，下面一颗弯牙，牙根缠着一圈绳。"""
    s = c.w / 256
    c.stroke(spline([(40 * s, 20 * s), (80 * s, 70 * s), (118 * s, 100 * s)]), 8 * s, taper=0)
    c.stroke(spline([(216 * s, 20 * s), (176 * s, 70 * s), (138 * s, 100 * s)]), 8 * s, taper=0)
    tooth = [(104, 104), (152, 104), (156, 150), (144, 196), (118, 238, 'c'), (124, 196), (112, 150)]
    c.add(c.mask(spline([(x * s, y * s) + tuple(k) for x, y, *k in tooth], closed=True)))
    c.erase(c.stroke(spline([(140 * s, 124 * s), (140 * s, 170 * s), (128 * s, 206 * s)]), 6 * s, add=False, taper=0.4))
    for k in range(3):                                                       # 缠绳：留白的斜纹
        y = (110 + 10 * k) * s
        c.erase(c.stroke([(106 * s, y + 6 * s), (154 * s, y - 2 * s)], 4 * s, add=False, taper=0))


def pelt_cloak(c):
    """狼毛斗篷：兜帽是一张狼头皮（两只耳朵），下摆一圈毛尖。"""
    s = c.w / 256
    body = [(128, 40), (176, 56), (196, 110), (214, 200), (196, 226), (60, 226), (42, 200), (60, 110), (80, 56)]
    c.add(c.mask(spline([(x * s, y * s) for x, y in body], closed=True)))
    for ex in (92, 164):                                                     # 耳朵
        sd = -1 if ex < 128 else 1
        c.add(c.mask(poly([(ex * s - 18 * s, 66 * s), ((ex + sd * 6) * s, 18 * s), (ex * s + 18 * s, 66 * s)])))
    rng = np.random.default_rng(3)
    for x in np.linspace(50, 206, 14):                                       # 下摆的毛尖
        ln = rng.uniform(14, 26)
        c.add(c.mask(poly([((x - 7) * s, 220 * s), ((x + rng.uniform(-4, 4)) * s, (222 + ln) * s), ((x + 7) * s, 220 * s)])))
    c.erase(c.mask(spline([(x * s, y * s) for x, y in [(128, 84), (156, 100), (150, 140), (128, 156), (106, 140), (100, 100)]],
                          closed=True)))                                     # 兜帽的开口：留白
    for x in (104, 152):                                                     # 兜帽上的两只眼（皮上挖的洞）
        c.add(c.ellipse(x * s, 70 * s, 8 * s, 4 * s, -15 if x < 128 else 15))
    c.erase(c.stroke(spline([(128 * s, 160 * s), (124 * s, 200 * s), (128 * s, 222 * s)]), 4 * s, add=False, taper=0.3))


def blood_bowl(c):
    """血碗：一只浅木碗，碗口一圈留白，碗里的血是实墨；上面一滴正落下去，溅起一圈小点。"""
    s = c.w / 256
    bowl = [(36, 140), (220, 140), (204, 196), (160, 224), (96, 224), (52, 196)]
    c.add(c.mask(spline([(x * s, y * s) for x, y in bowl], closed=True)))
    c.add(c.mask(poly([(100 * s, 220 * s), (156 * s, 220 * s), (168 * s, 240 * s), (88 * s, 240 * s)])))   # 圈足
    c.erase(c.ellipse(128 * s, 140 * s, 92 * s, 22 * s))
    c.add(c.ellipse(128 * s, 142 * s, 80 * s, 15 * s))
    c.stroke(ellipse_pts(128 * s, 140 * s, 92 * s, 22 * s), 6 * s, closed=True, taper=0)
    c.erase(c.stroke(spline([(60 * s, 166 * s), (84 * s, 196 * s), (120 * s, 208 * s)]), 5 * s, add=False, taper=0.4))
    _drop(c, 128, 70, 22)
    for k in range(6):
        a = math.radians(200 + 28 * k)
        c.add(c.ellipse((128 + 40 * math.cos(a)) * s, (124 + 14 * math.sin(a)) * s, 5 * s, 5 * s))


def beast_blood(c):
    """兽血（特性）：一滴血，里面留白出一只竖瞳的兽眼。"""
    s = c.w / 256
    _drop(c, 128, 150, 86)
    eye = [(70, 160, 'c'), (128, 128), (186, 160, 'c'), (128, 190)]
    c.erase(c.mask(spline([(x * s, y * s) + tuple(k) for x, y, *k in eye], closed=True)))
    c.add(c.ellipse(128 * s, 160 * s, 8 * s, 26 * s))


I = 128
S = 64
SPRITES = {
    'status_bleed': (S, S, bleed),
    'relic_wolf_fang_pendant': (I, I, fang_pendant),
    'relic_wolf_pelt_cloak': (I, I, pelt_cloak),
    'relic_blood_bowl': (I, I, blood_bowl),
    'relic_beast_blood': (I, I, beast_blood),
}
