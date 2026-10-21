"""BOSS 溺亡司祭 · 一阶段（Boss 档）：高主教冠、湿透的长法衣、潮汐权杖；站在半淹的祭坛上（策划/10 §5.3）。
癫狂层：主教冠阴影下的脸上睁开几只眼，脖子上的鳃一张一合（二阶段会整个显露出来）。

面朝左，挺直站在祭坛上，近侧的手握着权杖立在身前；法衣下半截湿透发黑，滴着水，挂着海藻；
胸前是绣着潮纹的祭披和一条垂到下摆的领带。
"""
import math

import numpy as np

from etch import STRUCT, Frame, ellipse_pts, smoothstep, spline
from actors._priest import FEET, OUT_W, TOP, altar, mitre, staff

NAME = '溺亡司祭 · 一阶段'
SIZE = (660, 940)


def draw(c):
    rng = np.random.default_rng(31)
    side = c.grad(300, 0, 1050, 0)
    shadow = c.ellipse(660, FEET + 2, 560, 30)
    c.add(shadow, 'fill')
    c.engrave(shadow, 0.9, [c.ph_lin(0, wobble=0.6), c.ph_lin(60)], spacing=7, swell=1.2)
    altar(c, rng)

    # ------------------------------------------------------------ 法衣：从肩膀一路宽到下摆
    robe = [(560, 560), (760, 560), (860, 640), (900, 900), (950, 1200), (1010, 1500), (1040, TOP + 4), (840, TOP - 6),
            (660, TOP + 4), (480, TOP - 6), (320, TOP + 4), (360, 1450), (420, 1150), (460, 880), (480, 650)]
    rm = c.mask(spline(robe, closed=True))
    c.add(rm, 'fill')
    lam = c.relief(rm, radius=180, blur=8)
    fold = np.sin((c.xx - 0.1 * (c.yy - 560)) / 40 + 0.5 * np.sin(c.yy / 220))
    wet = smoothstep(1250, 1500, c.yy + 40 * c.noise(50, 1))
    tone = np.clip(0.25 + 0.45 * side + 0.22 * (1 - lam) - 0.12 * fold + 0.35 * wet, 0, 1)
    c.engrave(rm, tone, [c.ph_lin(87, wobble=1.5, seed=1), c.ph_lin(25, seed=2), c.ph_lin(-35, seed=3)], spacing=9, swell=1.2)
    for k, x0 in enumerate((520, 600, 690, 780, 860)):                         # 褶谷
        x1 = x0 + (x0 - 680) * 0.7
        c.line([(x0, 900 + k * 20), (x0 + (x1 - x0) * 0.5 + 8, 1300), (x1, TOP - 10)], w=3.6, taper=(0.5, 0.1))
    # 湿透的下摆：一道道往下淌的水痕 + 滴落
    for x in np.linspace(340, 1020, 22):
        y0 = 1420 + rng.uniform(-60, 40)
        c.erase(c.stroke([(x, y0), (x + rng.uniform(-4, 4), TOP - 20)], 2.4, add=False, taper=(0.8, 0.1)))
    for x in np.linspace(350, 1010, 12):
        c.stroke([(x, TOP - 4), (x, TOP + rng.uniform(16, 40))], 4, taper=(0.0, 0.5))
    c.outline(robe, w=OUT_W, light=0.45)

    # 祭披：胸前一块长方的绣片，潮纹；领带垂到下摆
    chas = [(560, 600), (720, 600), (760, 900), (700, 1180), (600, 1180), (520, 900)]
    cm = c.mask(spline(chas, closed=True))
    c.clear(cm, layers=('ink',))
    c.engrave(cm, np.clip(0.2 + 0.4 * side, 0, 1), [c.ph_lin(88, wobble=0.6)], spacing=7, wmax=2.4)
    c.outline(chas, w=STRUCT + 1, light=0.4)
    for k in range(5):                                                         # 绣的潮纹
        y = 680 + k * 90
        c.stroke(spline([(560 + j * 20, y + 10 * math.sin(j * 1.1 + k)) for j in range(10)]), 4, taper=0.15)
    c.stroke(ellipse_pts(640, 640, 30, 30), 4, closed=True)
    c.stroke([(640, 610), (640, 670)], 4, taper=0)
    c.stroke([(612, 636), (668, 636)], 4, taper=0)

    # ------------------------------------------------------------ 远侧的手臂：垂在身后
    far = [(780, 600), (880, 700), (920, 940), (900, 1080), (850, 1080), (840, 920), (800, 720)]
    fm = c.mask(spline(far, closed=True))
    c.add(fm, 'fill')
    c.engrave(fm, 0.92, [c.ph_lin(80), c.ph_lin(10)], spacing=8, swell=1.2)
    c.outline(far, w=STRUCT + 1, light=0.4)

    # ------------------------------------------------------------ 头：浮肿发白的侧脸，眼睛是两个灰白的点；主教冠
    head = [(690, 400), (640, 352), (570, 362), (548, 398), (540, 426), (510, 470, 'c'), (526, 482), (518, 500), (528, 512),
            (518, 528), (530, 562), (580, 592), (640, 600), (700, 560), (716, 470)]
    hm = c.mask(spline(head, closed=True))
    c.add(hm, 'fill')
    lam = c.relief(hm, radius=50, blur=3)
    c.engrave(hm, np.clip(0.12 + 0.4 * c.grad(500, 0, 716, 0) + 0.3 * (1 - lam), 0, 1), [c.ph_lin(70), c.ph_lin(-20)], spacing=7.5)
    c.outline(head, w=OUT_W - 2, light=0.45)
    c.stroke(spline([(540, 470), (552, 484), (546, 494)]), 3.4, taper=0.3)      # 鼻翼
    c.stroke(spline([(522, 512), (546, 514), (562, 508)]), 4, taper=0.3)        # 嘴
    c.erase(c.ellipse(566, 430, 13, 8))                                        # 灰白的眼
    c.stroke(ellipse_pts(566, 430, 14, 9), 3, closed=True)
    c.stroke(spline([(548, 404), (570, 398), (594, 406)]), 5.5, taper=0.3)      # 眉
    for k in range(3):                                                         # 浮肿的脸上几道褶
        c.stroke(spline([(590, 470 + k * 26), (620, 482 + k * 26)]), 2.2, taper=0.4)
    c.stroke(spline([(600, 450), (620, 520), (610, 570)]), 2.4, taper=0.4)      # 腮帮
    mitre(c, Frame((610, 360), rot=-4), c.grad(500, 0, 720, 0))
    for sd, x in ((-1, 650), (1, 690)):                                       # 冠后垂下的两条飘带
        tail = c.stroke(spline([(x, 360), (x + 20, 480), (x + 10, 600)]), 30, add=False, taper=(0.0, 0.2))
        c.add(tail, 'fill')
        c.engrave(tail, 0.6, [c.ph_lin(85)], spacing=6)

    # ------------------------------------------------------------ 近侧的手臂：伸向前握着权杖
    arm = [(560, 600), (480, 680), (390, 840), (370, 900), (420, 940), (470, 860), (560, 720)]
    am = c.mask(spline(arm, closed=True))
    c.add(am, 'fill')
    lam = c.relief(am, radius=40, blur=3)
    c.engrave(am, np.clip(0.3 + 0.55 * (1 - lam), 0, 1), [c.ph_axis((560, 600), (390, 900), bow=-16, half=60), c.ph_lin(20)],
              spacing=8, swell=1.2)
    c.outline(arm, w=OUT_W - 1, light=0.45)
    staff(c, 360, 230, TOP, 920)

    # ============================================================ 癫狂层：脸上的几只眼，脖子上的鳃
    for (x, y, w) in ((590, 410, 16), (620, 450, 13), (580, 500, 11), (650, 420, 10)):
        c.stroke(spline([(x - w, y, 'c'), (x, y - w * 0.6), (x + w, y, 'c')]), 4, layer='mad', taper=0.1)
        c.stroke(spline([(x - w, y, 'c'), (x, y + w * 0.5), (x + w, y, 'c')]), 3, layer='mad', taper=0.1)
        c.add(c.ellipse(x, y, w * 0.32, w * 0.32), 'mad')
    for k in range(4):
        y = 540 + k * 18
        c.stroke(spline([(600, y - 8), (630, y), (664, y - 6)]), 6, layer='mad', taper=0.2)
