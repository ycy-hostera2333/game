"""U11 恐惧之源：黑暗中向外扩散的同心涟漪，中心一只眼（策划/10 §4.6）。

能力牌圆窗。整片是黑水一样的暗面，从中心一圈圈涟漪向外推开（涟漪的波峰亮、波谷暗，越外越宽越淡）；
中心浮着一只睁开的眼，眼周一圈留白的光。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, ellipse_pts, smoothstep, spline

NAME = '恐惧之源'

CX, CY = 624, 432


def draw(c):
    r = np.hypot(c.xx - CX, (c.yy - CY) / 0.82)
    # 涟漪：波长从中心往外变长，波峰亮
    ph = 2 * math.pi * np.sqrt(np.maximum(r - 100, 0) / 12.0) + 0.6 * c.noise(60, 1)
    crest = 0.5 + 0.5 * np.cos(ph)
    fade = smoothstep(90, 420, r)
    tone = np.clip(0.9 - 0.8 * crest ** 1.5 * (1 - 0.35 * fade) + 0.04 * c.noise(30, 2), 0, 1)
    c.engrave(np.ones((c.h, c.w), np.float32), tone,
              [r + 1.5 * c.noise(40, 3), c.ph_ray(CX, CY, r0=260), c.ph_lin(30, seed=4)], spacing=7.5, swell=1.3)
    # 每道波峰描一条细亮线
    k = np.round(ph / (2 * math.pi))
    peak = np.abs(ph - 2 * math.pi * k) * np.sqrt(np.maximum(r - 100, 1) * 12.0) / math.pi   # 约等于离波峰的像素距离
    c.erase(np.clip(2.0 - peak, 0, 1) * smoothstep(80, 120, r) * (1 - 0.7 * fade))

    # 中心的光：一圈留白 + 放射短线
    c.erase(1 - smoothstep(140, 200, r))
    for a in np.arange(0, 360, 6):
        ang = math.radians(a)
        r0, r1 = 165, 200 + 22 * (a % 12 == 0)
        c.stroke([(CX + r0 * math.cos(ang), CY + r0 * 0.82 * math.sin(ang)),
                  (CX + r1 * math.cos(ang), CY + r1 * 0.82 * math.sin(ang))], 2.2, taper=(0.1, 0.9))

    # 眼
    W, H = 130, 64
    upper = spline([(CX - W, CY, 'c'), (CX - W * 0.4, CY - H), (CX + W * 0.4, CY - H), (CX + W, CY, 'c')])
    lower = spline([(CX + W, CY, 'c'), (CX + W * 0.4, CY + H * 0.8), (CX - W * 0.4, CY + H * 0.8), (CX - W, CY, 'c')])
    eye = c.mask(np.vstack([upper, lower]))
    ir = 50
    rr = np.hypot(c.xx - CX, c.yy - CY)
    iris = c.ellipse(CX, CY, ir, ir) * eye
    c.paint(iris, np.clip(c.lines(np.arctan2(c.yy - CY, c.xx - CX) * 40, 6, 2.2) * smoothstep(20, 26, rr), 0, 1))
    c.fill(c.ellipse(CX, CY, 21, 21) * eye)
    c.erase(c.ellipse(CX - 13, CY - 13, 5, 5))
    c.add(c.stroke(ellipse_pts(CX, CY, ir, ir), 3.0, closed=True, add=False) * eye)
    c.stroke(upper, OUTLINE, taper=0.15)
    c.stroke(lower, STRUCT, taper=0.15)
    for t in np.linspace(0.12, 0.88, 9):                                    # 睫毛
        i = int(t * (len(upper) - 1))
        p = upper[i]
        ang = math.atan2(p[1] - (CY + 56), p[0] - CX)
        c.stroke([tuple(p), (p[0] + 30 * math.cos(ang), p[1] + 30 * math.sin(ang))], 3.6, taper=(0.0, 0.8))
