"""鳞卵（鳞母的卵，小档）：一枚立着的半透明卵囊，坐在一滩黏液里；膜上几道血管，里面蜷着一只鳞民的胚胎，
一只眼已经亮了（深渊遮罩）。癫狂层：膜上裂开的纹，快孵了（策划/15 §2.3、09 M7.7）。

卵囊左上受光：膜的轮廓粗细随光变，左上一块留白的高光；里面的胚胎是一团深色，弯成逗号，背上一排小刺。
"""
import math

import numpy as np

from etch import STRUCT, ellipse_pts, smoothstep, spline

NAME = '鳞卵'
SIZE = (220, 240)

FEET = 461


def draw(c):
    cx, cy, rx, ry = 220, 270, 132, 168
    puddle = [(40, 452), (120, 426), (220, 420), (330, 428), (404, 452), (350, 470), (220, 474), (90, 470)]
    pm = c.mask(spline(puddle, closed=True))
    egg = c.ellipse(cx, cy, rx, ry)
    c.add(np.clip(egg + pm, 0, 1), 'fill')
    side = c.grad(cx - rx, cy - ry, cx + rx, cy + ry)

    # 黏液：一滩扁平的水，横排线，边上鼓起几个泡
    c.engrave(pm, 0.55, [c.ph_lin(0, wobble=0.5), c.ph_lin(12)], spacing=6, wmax=2.2)
    c.stroke(spline(puddle, closed=True), 3.2, closed=True, light=0.3)
    for x, y, r in ((70, 446, 10), (360, 444, 8), (300, 460, 6)):
        c.clear(c.ellipse(x, y, r, r * 0.8))
        c.stroke(ellipse_pts(x, y, r, r * 0.8), 2.4, closed=True)

    # 膜：淡淡的排线，越往右下越暗；左上一块高光留白
    lam = c.relief(egg, radius=90, blur=4)
    tone = np.clip(0.12 + 0.35 * side + 0.2 * (1 - lam), 0, 1)
    c.engrave(egg, tone, [c.ph_contour(egg, blur=4), c.ph_lin(-30, seed=2)], spacing=7, wmax=2.2)

    # 胚胎：一团弯成逗号的深色，头在上、尾巴卷到下面；背上一排小刺
    body = [(170, 200), (240, 170), (300, 210), (310, 290), (270, 350), (210, 370), (170, 340), (200, 300), (250, 290),
            (250, 240), (210, 230)]
    bm = c.mask(spline(body, closed=True)) * egg
    c.paint(bm, np.clip(c.lines(c.ph_lin(40, seed=3), 6, 2.6) + smoothstep(0.5, 0.9, 0.7 + 0.2 * side), 0, 1))
    c.stroke(spline(body, closed=True), 3.0, closed=True, light=0.3)
    for k in range(5):
        a = math.radians(-60 + 26 * k)
        x0, y0 = 255 + 62 * math.cos(a), 265 + 92 * math.sin(a)
        c.stroke([(x0, y0), (x0 + 16 * math.cos(a), y0 + 16 * math.sin(a))], 4.0, taper=(0.0, 0.8))
    # 亮了的眼：一圈深渊青，中间竖瞳
    ex, ey = 205, 214
    eye = c.ellipse(ex, ey, 20, 18)
    c.clear(eye)
    c.fill(eye, 'abyss', 0.85)
    c.fill(c.ellipse(ex - 2, ey, 4, 12), value=1.0)

    # 膜上的血管：从底下往上分叉的细线
    rng = np.random.default_rng(7)
    for x0, a0 in ((150, -100), (230, -85), (300, -70)):
        pts = [(x0, 420)]
        a = math.radians(a0)
        for k in range(6):
            a += rng.uniform(-0.3, 0.3)
            x, y = pts[-1]
            pts.append((x + 34 * math.cos(a), y + 34 * math.sin(a)))
        c.stroke(spline(pts), 2.6, taper=(0.0, 0.9))

    # 高光：左上留白
    c.erase(c.ellipse(cx - 62, cy - 90, 30, 52, -25))
    c.erase(c.ellipse(cx - 30, cy - 140, 10, 14))
    c.stroke(ellipse_pts(cx, cy, rx, ry), 6.5, closed=True, light=0.5)

    # 癫狂层：膜上裂开的纹（快孵了），从顶上往下劈
    for pts in ([(220, 104), (232, 150), (214, 190), (238, 236)], [(232, 150), (276, 170), (300, 160)],
                [(214, 190), (170, 214), (150, 206)]):
        c.stroke(spline(pts), 4.0, layer='mad', taper=(0.0, 0.7))
