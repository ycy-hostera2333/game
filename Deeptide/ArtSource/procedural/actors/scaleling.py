"""N03 鳞民幼体（小档，兽形）：蛙鱼混种、半蹲、过大的眼；鳃裂、蹼爪，皮肤用鳞状排线。眼睛发青光（深渊遮罩）（策划/10 §5.3）。

面朝左蹲着：大脑袋占身子的一半，一只鼓出来的圆眼，宽嘴角下垂；背上一排鳍刺；
前爪撑地、带蹼，后腿像蛙一样折着；身上是一片片鳞（半月形排线）。
"""
import math

import numpy as np

from etch import STRUCT, ellipse_pts, smoothstep, spline

NAME = '鳞民幼体'
SIZE = (380, 232)

OUT_W = 9.0
FEET = 446


def draw(c):
    rng = np.random.default_rng(4)
    body = [(150, 230), (220, 150), (330, 112), (460, 120), (580, 170), (660, 260), (680, 350), (640, 420), (520, 440),
            (380, 436), (250, 420), (170, 370), (120, 300)]
    hind = [(500, 300), (590, 270), (680, 320), (700, 400), (660, 446), (540, 446), (560, 410), (500, 380)]
    fore = [(220, 330), (260, 360), (240, 420), (250, 446), (170, 446), (180, 410), (200, 360)]
    fins = [(260, 150), (300, 70), (330, 120), (370, 64), (400, 116), (440, 70), (470, 124), (520, 92), (540, 150)]
    shadow = c.ellipse(420, FEET + 2, 300, 14)
    body_m = c.mask(spline(body, closed=True))
    hind_m = c.mask(spline(hind, closed=True))
    fore_m = c.mask(spline(fore, closed=True))
    fin_m = c.mask(np.array(fins)) * (1 - body_m * 0)
    fill = np.clip(body_m + hind_m + fore_m + fin_m + shadow, 0, 1)
    c.add(fill, 'fill')
    side = c.grad(150, 120, 650, 440)

    c.engrave(shadow, 0.9, [c.ph_lin(0), c.ph_lin(60)], spacing=6, swell=1.2)
    # 背鳍：几根刺，刺之间是有排线的膜
    c.engrave(fin_m, 0.55, [c.ph_lin(90, wobble=0.3)], spacing=6, wmax=2)
    for x0, y0, x1, y1 in ((280, 150, 300, 70), (350, 124, 370, 64), (420, 122, 440, 70), (500, 140, 520, 92)):
        c.stroke([(x0, y0), (x1, y1)], 6, taper=(0.0, 0.8))
    c.stroke(np.array(fins), 3.4, taper=0)
    # 身子：鳞（半月形排线一排排错开），左上亮
    lam = c.relief(body_m, radius=90, blur=4)
    tone = np.clip(0.2 + 0.5 * side + 0.3 * (1 - lam), 0, 1)
    c.engrave(body_m, tone, [c.ph_lin(-20, seed=1), c.ph_lin(50, seed=2)], spacing=8, swell=1.1, bands=[(0.35, 0.65), (0.6, 0.9)])
    for row in range(7):
        y = 150 + row * 42
        for k in range(14):
            x = 230 + k * 34 + (row % 2) * 17
            if body_m[int(min(y + 10, c.h - 1)), int(min(x, c.w - 1))] < 0.5 or x < 330 - row * 10:
                continue
            c.stroke(ellipse_pts(x, y, 18, 14, a0=10, a1=170), 2.2 + 1.2 * float(side[int(y), int(x)]), taper=0.2)
    # 头：宽嘴、嘴角下垂，鳃裂三道
    c.stroke(spline([(130, 300), (180, 318), (240, 322), (270, 340)]), 5.5, taper=(0.0, 0.3))
    c.stroke(spline([(140, 306), (176, 330), (220, 336)]), 2.2, taper=0.4)
    for k in range(3):
        x = 300 + k * 22
        c.stroke(spline([(x, 230), (x + 6, 270), (x, 310)]), 4.0, taper=0.3)
    c.outline(body, w=OUT_W, light=0.45)
    # 后腿：蛙一样折着，大腿鼓在身子外面
    lam = c.relief(hind_m, radius=40, blur=2)
    c.engrave(hind_m, np.clip(0.35 + 0.35 * side + 0.25 * (1 - lam), 0, 1), [c.ph_contour(hind_m, blur=3), c.ph_lin(30)], spacing=7,
              swell=1.2)
    c.outline(hind, w=STRUCT + 1, light=0.45)
    for k in range(3):                                                       # 后脚的蹼趾
        c.stroke(spline([(560 + k * 30, 440), (540 + k * 30, 448), (520 + k * 30, 452)]), 4.0, taper=(0.0, 0.7))
    # 前爪：撑地，三根带蹼的趾
    lam = c.relief(fore_m, radius=16, blur=1)
    c.engrave(fore_m, np.clip(0.45 + 0.3 * side - 0.3 * lam, 0, 1), [c.ph_lin(80), c.ph_lin(10)], spacing=6.5, wmax=2.4)
    c.outline(fore, w=STRUCT, light=0.45)
    for k in range(3):
        x = 176 + k * 26
        c.stroke(spline([(x + 20, 430), (x + 4, 446), (x - 8, 450)]), 4.0, taper=(0.0, 0.7))
    # 眼：鼓出来的大圆眼，眼眶一圈褶；虹膜发青光（深渊遮罩）
    ex, ey, er = 210, 220, 44
    lid = c.ellipse(ex, ey, er + 14, er + 12)
    c.engrave(lid, 0.6, [c.ph_ring(ex, ey)], spacing=6, wmax=2.2)
    c.stroke(ellipse_pts(ex, ey, er + 14, er + 12), 4.5, closed=True, light=0.4)
    eye = c.ellipse(ex, ey, er, er)
    c.clear(eye)
    c.fill(eye * c.lines(np.arctan2(c.yy - ey, c.xx - ex) * 20, 5, 2.2) * (np.hypot(c.xx - ex, c.yy - ey) > 14), 'abyss')
    c.fill(eye * (1 - smoothstep(er - 6, er, np.hypot(c.xx - ex, c.yy - ey))) * 0.35, 'abyss')
    c.fill(c.ellipse(ex - 4, ey, 8, 24), value=1.0)                            # 竖瞳
    c.erase(c.ellipse(ex - 16, ey - 16, 7, 7))
    c.erase(c.ellipse(ex - 16, ey - 16, 7, 7), 'abyss')
    c.stroke(ellipse_pts(ex, ey, er, er), 3.4, closed=True)
