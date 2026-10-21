"""R06 心灵之锚：沉入水中的船锚，锚链连向上方的光（策划/10 §4.6）。

能力牌圆窗。水下：上方是水面透下来的光（一束束斜射的光柱），往下越来越黑；
一只铁锚头朝下沉在中下方，锚链从锚环一路往上，消失在光里。几串气泡往上冒。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, smoothstep, spline

NAME = '心灵之锚'

LIGHT = (624, -60)


def draw(c):
    rng = np.random.default_rng(6)
    lx, ly = LIGHT
    # ------------------------------------------------------------ 水：越深越黑，波纹横排线；光柱从上方斜照
    depth = c.grad(0, 0, 0, c.h)
    c.engrave(np.ones((c.h, c.w), np.float32), np.clip(0.2 + 0.75 * depth ** 1.2 + 0.05 * c.noise(60, 1), 0, 1),
              [c.ph_wave(0, 5, 200, wobble=2, seed=2), c.ph_lin(60, seed=3), c.ph_lin(-50, seed=4)], spacing=9, swell=1.2)
    ang = np.degrees(np.arctan2(c.yy - ly, c.xx - lx))
    rays = 0.5 + 0.5 * np.sin(np.radians(ang) * 23 + 2 * c.noise(80, 5))
    fan = smoothstep(55, 70, ang) * (1 - smoothstep(110, 125, ang))
    c.erase(smoothstep(0.55, 0.95, rays) * fan * (1 - smoothstep(0.2, 0.85, depth)) * 0.9)
    c.erase(1 - smoothstep(0, 200, np.hypot(c.xx - lx, (c.yy - ly) * 1.5)))

    # ------------------------------------------------------------ 锚
    A = Frame((624, 610), rot=8)
    shank = [(-16, -270), (16, -270), (16, 120), (-16, 120)]
    stock = [(-130, -230), (130, -230), (130, -206), (-130, -206)]
    crown = [(-200, 30), (-170, 100), (-90, 150), (0, 166), (90, 150), (170, 100), (200, 30), (226, 0, 'c'), (190, 60), (100, 120),
             (0, 136), (-100, 120), (-190, 60), (-226, 0, 'c')]
    for part, closed in ((stock, False), (shank, False)):
        m = c.mask(A(part))
        lam = c.relief(m, radius=12, blur=1)
        c.engrave(m, np.clip(0.75 - 0.5 * lam, 0, 1), [c.ph_lin(A.angle(90)), c.ph_lin(A.angle(20))], spacing=6.5)
        c.stroke(A(part + part[:1]), STRUCT, taper=0)
    m = c.mask(spline(A(crown), closed=True))
    lam = c.relief(m, radius=16, blur=1.5)
    c.engrave(m, np.clip(0.8 - 0.55 * lam, 0, 1), [c.ph_contour(m, blur=2), c.ph_lin(A.angle(60))], spacing=6.5)
    c.outline(A.cp(crown), w=STRUCT + 1, light=0.4)
    for sd in (-1, 1):                                                       # 锚爪
        fluke = [(sd * 190, 50), (sd * 250, -20), (sd * 240, 30), (sd * 214, 80)]
        fm = c.mask(spline(A(fluke), closed=True))
        c.engrave(fm, 0.7, [c.ph_lin(A.angle(45 * sd))], spacing=6)
        c.outline(A.cp(fluke if sd > 0 else fluke[::-1]), w=STRUCT, light=0.4)
    ring = A.p(0, -300)
    c.stroke(ellipse_pts(*ring, 34, 34), 9, closed=True)
    c.erase(c.stroke(ellipse_pts(*ring, 34, 34, a0=200, a1=280), 3, add=False, taper=0.4))

    # ------------------------------------------------------------ 锚链：从锚环往上，一节节，越往上越亮越细
    chain = spline([ring, (600, 200), (630, 60), (624, -40)])
    d = np.hypot(*np.diff(chain, axis=0).T)
    s = np.concatenate([[0], np.cumsum(d)])
    k = 0
    pos = 0.0
    while pos < s[-1]:
        i = int(np.searchsorted(s, pos))
        i = min(i, len(chain) - 2)
        p = chain[i]
        t = chain[i + 1] - chain[i]
        a = math.degrees(math.atan2(t[1], t[0]))
        u = pos / s[-1]
        w = 20 * (1 - 0.4 * u)
        if k % 2 == 0:
            c.stroke(ellipse_pts(p[0], p[1], w * 1.4, w * 0.8, rot=a), 6 * (1 - 0.5 * u), closed=True)
        else:
            c.stroke([tuple(p - t / np.hypot(*t) * w), tuple(p + t / np.hypot(*t) * w)], 8 * (1 - 0.5 * u), taper=0)
        pos += w * 1.8
        k += 1

    # ------------------------------------------------------------ 气泡
    for _ in range(40):
        x = 624 + rng.normal(0, 140)
        y = rng.uniform(80, 700)
        rr = rng.uniform(3, 11)
        c.clear(c.ellipse(x, y, rr + 2, rr + 2))
        c.stroke(ellipse_pts(x, y, rr, rr), 2.0, closed=True)
        c.erase(c.ellipse(x - rr * 0.3, y - rr * 0.3, rr * 0.3, rr * 0.3))
