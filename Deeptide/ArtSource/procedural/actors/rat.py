"""N08 疫鼠（小档，兽形）：癞皮、尾巴打结（策划/10 §5.3）。

面朝左，弓着背，尖嘴往前探；毛一缕缕的、背上几块秃斑露出皮；耳朵缺了一角；
长尾巴从身后绕一圈，中间打了个结。
"""
import math

import numpy as np

from etch import STRUCT, ellipse_pts, smoothstep, spline

NAME = '疫鼠'
SIZE = (400, 232)

OUT_W = 9.0
FEET = 446


def draw(c):
    rng = np.random.default_rng(8)
    body = [(40, 356, 'c'), (100, 316), (170, 272), (240, 246), (330, 200), (440, 190), (540, 230), (600, 300), (606, 370),
            (570, 420), (440, 436), (300, 430), (200, 412), (120, 392), (70, 378)]
    ear = [(214, 252), (208, 196), (240, 176), (276, 192), (280, 236)]
    legs = [[(160, 390), (196, 400), (180, 446), (140, 446)], [(260, 410), (300, 418), (294, 446), (250, 446)],
            [(480, 400), (530, 396), (536, 446), (482, 446)]]
    tail = spline([(596, 372), (660, 396), (700, 380), (712, 344), (690, 326), (672, 346), (690, 374), (740, 400), (790, 384)])
    shadow = c.ellipse(380, FEET + 2, 300, 14)
    body_m = c.mask(spline(body, closed=True))
    ear_m = c.mask(spline(ear, closed=True))
    leg_ms = [c.mask(np.array(l)) for l in legs]
    tail_m = c.stroke(tail, 18, add=False, taper=(0.0, 0.9))
    fill = np.clip(body_m + ear_m + sum(leg_ms) + tail_m + shadow, 0, 1)
    c.add(fill, 'fill')
    side = c.grad(150, 140, 600, 430)

    c.engrave(shadow, 0.9, [c.ph_lin(0), c.ph_lin(60)], spacing=6, swell=1.2)
    # 尾巴：一节节的环纹 + 中间的结
    c.engrave(tail_m, 0.55, [c.ph_contour(tail_m, blur=1) * 0 + c.ph_lin(0), c.ph_lin(90)], spacing=6, wmax=2.2)
    for i in range(10, len(tail) - 10, 14):
        p = tail[i]
        t = tail[i + 1] - tail[i - 1]
        n = np.array([-t[1], t[0]]) / max(np.hypot(*t), 1e-6)
        w = 8 * (1 - i / len(tail) * 0.85)
        c.stroke([tuple(p - n * w), tuple(p + n * w)], 2.2, taper=0)
    c.stroke(tail + np.array([0, 0]), 3, taper=(0.0, 0.85), add=False)
    for sd in (-1, 1):
        t = np.gradient(tail, axis=0)
        t /= np.hypot(t[:, 0], t[:, 1])[:, None]
        nn = np.stack([-t[:, 1], t[:, 0]], 1)
        w = 9 * (1 - np.linspace(0, 0.85, len(tail)))
        c.stroke(tail + nn * w[:, None] * sd, 3.2, taper=(0.0, 0.3))
    for leg, m in zip(legs, leg_ms):
        c.engrave(m, 0.8, [c.ph_lin(80)], spacing=6, wmax=2.2)
        c.stroke(np.array(leg + leg[:1]), STRUCT - 1, taper=0)
        x = leg[2][0]
        for k in range(3):
            c.stroke([(x - 6 - k * 9, 440), (x - 14 - k * 9, 450)], 2.6, taper=(0.0, 0.7))
    # 身子：一缕缕的毛（沿身体走向的短笔触），几块秃斑
    lam = c.relief(body_m, radius=90, blur=4)
    tone = np.clip(0.4 + 0.4 * side + 0.25 * (1 - lam), 0, 1)
    c.engrave(body_m, tone, [c.ph_lin(-10, seed=1), c.ph_lin(60, seed=2), c.ph_lin(-50, seed=3)], spacing=8, swell=1.2)
    for _ in range(140):                                                       # 毛：沿背的弧往后梳
        x, y = rng.uniform(150, 600), rng.uniform(150, 420)
        if body_m[int(y), int(x)] < 0.5:
            continue
        a = math.atan2(y - 560, x - 360) + math.pi / 2 + rng.uniform(-0.3, 0.3)
        ln = rng.uniform(16, 30)
        c.stroke([(x, y), (x + ln * math.cos(a), y + ln * math.sin(a))], 2.6, taper=(0.0, 0.8))
    for (x, y, rx, ry) in ((400, 214, 30, 14), (500, 250, 20, 11), (330, 236, 14, 9)):   # 秃斑：露出的皮，几个疮
        spot = c.ellipse(x, y, rx, ry, rot=10)
        c.clear(spot, layers=('ink',))
        c.engrave(spot, 0.25, [c.ph_lin(60)], spacing=5, wmax=1.8)
        pts = ellipse_pts(x, y, rx, ry, rot=10)
        c.stroke(pts + rng.normal(0, 2.5, pts.shape), 2.4, closed=True, taper=0)
        c.add(c.ellipse(x + rx * 0.3, y, 4, 4))
    c.outline(body, w=OUT_W, light=0.45)
    # 耳朵（缺一角）、眼、鼻、胡须、牙
    c.engrave(ear_m, 0.35, [c.ph_contour(ear_m, blur=2)], spacing=5, wmax=2)
    c.outline(ear, w=STRUCT, light=0.45)
    c.add(c.ellipse(150, 300, 10, 8))
    c.erase(c.ellipse(146, 297, 3, 3))
    c.fill(c.ellipse(46, 354, 10, 9), value=1.0)
    for k in range(4):
        a = math.radians(160 + k * 14)
        c.stroke([(70, 346), (70 + 90 * math.cos(a), 346 + 90 * math.sin(a) * 0.6 - 10)], 1.6, taper=(0.0, 0.9))
    c.stroke([(74, 376), (78, 396)], 4, taper=(0.0, 0.7))
    c.stroke([(86, 378), (90, 396)], 4, taper=(0.0, 0.7))
