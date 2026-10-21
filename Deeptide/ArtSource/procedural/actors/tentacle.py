"""N07 海底触须（小档，Boss 召唤物）：从地面墨渍里伸出的单根触须，吸盘用圆形排线（策划/10 §5.3）。

一根粗触须从一滩黑水里升起来，先往右弯、再向左探过来（敌人朝左），尖端卷成一个钩；
内侧一排吸盘，外侧有几道横褶。根部的水滩一圈涟漪。
"""
import math

import numpy as np

from etch import STRUCT, ellipse_pts, smoothstep, spline

NAME = '海底触须'
SIZE = (260, 420)

OUT_W = 10.0
FEET = 806


def draw(c):
    # 水滩
    pool = c.ellipse(260, FEET, 230, 30)
    c.add(pool, 'fill')
    c.engrave(pool, 0.95, [c.ph_lin(0, wobble=0.4), c.ph_lin(60)], spacing=6, swell=1.4)
    for k in range(3):
        c.erase(c.stroke(ellipse_pts(260, FEET, 120 + k * 40, 12 + k * 6, a0=190, a1=350), 2.4, add=False, taper=0.3))

    # 触须：中心线，从根部（粗）收到尖（细）
    ctrl = [(270, FEET), (300, 680), (330, 540), (300, 380), (220, 250), (140, 170), (120, 110), (160, 80), (190, 110), (170, 150)]
    center = spline(ctrl)
    n = len(center)
    u = np.linspace(0, 1, n)
    half = 64 * (1 - 0.9 * u ** 0.9) + 3
    t = np.gradient(center, axis=0)
    t /= np.hypot(t[:, 0], t[:, 1])[:, None]
    nn = np.stack([-t[:, 1], t[:, 0]], 1)
    body = c.stroke(center, 128, add=False, taper=0, profile=lambda uu: (64 * (1 - 0.9 * uu ** 0.9) + 3) / 64)
    c.add(body, 'fill')
    lam = c.relief(body, radius=40, blur=2)
    side = c.grad(150, 0, 380, 0)
    c.engrave(body, np.clip(0.35 + 0.4 * side + 0.3 * (1 - lam), 0, 1),
              [c.ph_contour(body, blur=2) * 0 + c.ph_lin(-20, seed=1), c.ph_lin(60, seed=2), c.ph_lin(10, seed=3)], spacing=7.5,
              swell=1.2)
    # 外侧（右）的横褶
    for i in range(20, n - 30, 18):
        p = center[i]
        w = half[i]
        c.stroke(spline([tuple(p + nn[i] * w * 0.95), tuple(p + nn[i] * w * 0.3 - t[i] * 6), tuple(p - nn[i] * w * 0.1)]), 2.6,
                 taper=0.4)
    # 两侧轮廓
    c.stroke(center + nn * half[:, None], OUT_W, taper=(0.0, 0.2))
    c.stroke(center - nn * half[:, None], OUT_W * 0.75, taper=(0.0, 0.2))
    # 吸盘：内侧（左）一排，越往尖越小
    step = 22
    for i in range(10, n - 20, step):
        p = center[i] - nn[i] * half[i] * 0.55
        r = max(half[i] * 0.42, 3)
        sucker = c.ellipse(p[0], p[1], r, r * 0.8, rot=math.degrees(math.atan2(t[i][1], t[i][0])))
        c.clear(sucker, layers=('ink',))
        c.engrave(sucker, 0.55, [c.ph_ring(p[0], p[1])], spacing=4.5, wmax=1.8)
        c.stroke(ellipse_pts(p[0], p[1], r, r * 0.8, rot=math.degrees(math.atan2(t[i][1], t[i][0]))), 2.8, closed=True)
        c.add(c.ellipse(p[0], p[1], r * 0.3, r * 0.3))
        step = max(int(half[i] * 0.7), 10)
