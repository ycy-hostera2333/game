"""C10 扭曲躯壳：蜷身用后背挡下攻击的人。癫狂层：背上隆起鳞甲（策划/10 §4.6）。

侧面看一个人跪在地上蜷成一团，脸朝左埋向地面，两条手臂交叉护住后脑，靴底从身子后面露出来；
帽子掉在左边地上。弓起的后背朝向右上，一记重击正砸在背上。
背的左上半迎光、排线很淡——癫狂层的鳞甲就长在这片亮面上，背脊上还顶出一排骨刺，伸到亮的背景里。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, resample, smoothstep, spline
from cards._common import burst

NAME = '扭曲躯壳'

GROUND = 716
# 背脊：从后颈到尾骨（癫狂层沿它长鳞和骨刺）
BACK = [(400, 470), (450, 400), (540, 320), (650, 282), (760, 288), (860, 346), (928, 444), (958, 560), (960, 600)]


def draw(c):
    rng = np.random.default_rng(10)
    # ------------------------------------------------------------ 背景 + 地面
    c.engrave(np.ones((c.h, c.w), np.float32), 0.18 + 0.4 * c.grad(300, 100, 1000, 800) + 0.05 * c.noise(100, 1),
              [c.ph_lin(-40, wobble=2, seed=2), c.ph_lin(50, seed=3)], spacing=10.5, bands=[(0.04, 0.5), (0.45, 0.85)])
    ground = c.rect(0, GROUND, c.w, c.h)
    c.engrave(ground, 0.62, [c.ph_lin(0, wobble=1.5, seed=4), c.ph_lin(60, seed=5)], spacing=9)
    c.stroke([(0, GROUND), (c.w, GROUND + 2)], STRUCT, taper=0)
    c.engrave(c.ellipse(640, GROUND + 14, 420, 26), 0.95, [c.ph_lin(0), c.ph_lin(70)], spacing=7, swell=1.3)

    # ------------------------------------------------------------ 冲击：右上方砸下来的一击，在背上炸开
    hx, hy = 790, 300
    c.erase(1 - smoothstep(30, 150, np.hypot(c.xx - hx, c.yy - hy)))
    burst(c, hx, hy, 50, 230, n=36, width=4.0, seed=3)
    for k in range(3):                                                       # 砸下来的东西：几道粗速度线
        c.stroke([(hx + 150 + k * 44, hy - 280 + k * 14), (hx + 40 + k * 22, hy - 60)], 7 - k * 1.5, taper=(0.9, 0.0))

    # ------------------------------------------------------------ 掉在地上的帽子
    c.engrave(c.ellipse(170, GROUND - 14, 96, 20), 0.8, [c.ph_lin(0), c.ph_lin(70)], spacing=7)
    crown = [(110, GROUND - 18), (118, GROUND - 82), (170, GROUND - 96), (224, GROUND - 82), (230, GROUND - 18)]
    m = c.mask(spline(crown, closed=True))
    lam = c.relief(m, radius=24, blur=2)
    c.engrave(m, np.clip(0.95 - 0.5 * lam, 0.4, 1), [c.ph_lin(85), c.ph_lin(15)], spacing=7.5)
    c.outline(crown, w=STRUCT, light=0.4)
    c.stroke(ellipse_pts(170, GROUND - 14, 96, 20), STRUCT, closed=True)
    c.line([(114, GROUND - 40), (170, GROUND - 46), (228, GROUND - 40)], w=7, taper=0.1)       # 帽带

    # ------------------------------------------------------------ 靴底：从身子后面露出来，鞋跟朝右
    for dx, dy in ((0, 0), (26, -50)):
        sole = [(900 + dx, GROUND - 80 + dy), (990 + dx, GROUND - 74 + dy), (1000 + dx, GROUND - 6 + dy), (912 + dx, GROUND + dy)]
        m = c.mask(spline(sole, closed=True))
        c.engrave(m, 0.88, [c.ph_lin(5), c.ph_lin(80)], spacing=6.5, swell=1.2)
        c.outline(sole, w=STRUCT, light=0.4)
        c.erase(c.stroke([(948 + dx, GROUND - 72 + dy), (956 + dx, GROUND - 8 + dy)], 3, add=False, taper=0.2))   # 鞋跟的分界
        for k in range(4):                                                    # 鞋底纹
            y = GROUND - 66 + dy + k * 15
            c.erase(c.stroke([(912 + dx, y), (942 + dx, y + 2)], 2.2, add=False, taper=0.3))

    # ------------------------------------------------------------ 身子：风衣裹着弓起的背
    body = BACK[1:-1] + [(960, 640), (990, GROUND), (700, GROUND + 2), (430, GROUND), (380, 650), (370, 560)]
    m = c.mask(spline(body, closed=True))
    lam = c.relief(m, radius=170, blur=8)
    c.engrave(m, np.clip(0.06 + 0.62 * c.grad(560, 300, 930, 700) + 0.4 * (1 - lam) ** 1.5, 0.04, 1),
              [c.ph_axis((400, GROUND), (980, GROUND), bow=-260, half=300, seed=6), c.ph_lin(60, seed=7),
               c.ph_lin(-30, seed=8)], spacing=9, swell=1.1)
    for pts in ([(540, 330), (580, 460), (560, 600)], [(680, 290), (700, 420), (680, 560)], [(820, 330), (790, 470)],
                [(600, GROUND - 4), (640, 640), (700, 600)]):
        c.line(pts, w=3.2, taper=0.4)                                        # 衣服绷紧的褶
    c.line([(880, 420), (850, 560), (860, GROUND - 6)], w=4, taper=0.2)     # 风衣后开衩
    c.outline(body, w=OUTLINE + 1, light=0.4)
    c.line([(420, 440), (470, 470), (500, 520)], w=STRUCT, taper=0.2)        # 立起的衣领

    # ------------------------------------------------------------ 头：脸朝下埋着，只看得到后脑、耳朵
    hx0, hy0 = 322, 652
    head = c.ellipse(hx0, hy0, 70, 60, rot=-10)
    lam = c.relief(head, radius=40, blur=2)
    c.engrave(head, np.clip(0.9 - 0.4 * lam, 0.5, 1), [c.ph_contour(head, blur=3), c.ph_lin(40)], spacing=7, swell=1.2)
    c.stroke(ellipse_pts(hx0, hy0, 70, 60, rot=-10), STRUCT + 1, closed=True, light=0.4)
    ear = [(336, 650), (360, 642), (370, 668), (354, 690), (334, 680)]
    c.engrave(c.mask(spline(ear, closed=True)), 0.35, [c.ph_lin(70)], spacing=6, wmax=2)
    c.outline(ear, w=3, light=0.4)

    # ------------------------------------------------------------ 手臂：上臂从肩膀垂到前面，前臂翻上来护住后脑
    upper = spline([(480, 470), (360, 520), (236, 570)])
    fore = spline([(236, 572), (310, 590), (392, 612)])
    for arm, w in ((upper, 86), (fore, 72)):
        am = c.stroke(arm, w, add=False, taper=0)
        lam = c.relief(am, radius=30, blur=2)
        c.engrave(am, np.clip(0.95 - 0.55 * lam, 0.35, 1), [c.ph_axis(tuple(arm[0]), tuple(arm[-1]), bow=-10, half=w / 2),
                                                           c.ph_lin(30)], spacing=8, swell=1)
        t = arm[-1] - arm[0]
        n = np.array([-t[1], t[0]]) / np.hypot(*t)
        for sd in (-1, 1):
            c.stroke(arm + n * w / 2 * sd, STRUCT + (1.5 if sd > 0 else 0), taper=(0.0, 0.0))
    c.engrave(c.ellipse(236, 572, 44, 40), 0.95, [c.ph_lin(30), c.ph_lin(-40)], spacing=7.5)       # 手肘
    c.stroke(ellipse_pts(236, 572, 44, 40, a0=90, a1=270), STRUCT + 1)
    # 手：张开盖在后脑上，四指朝下
    H = Frame((392, 614), rot=60)
    palm = [(-20, -34), (40, -38), (60, 0), (40, 38), (-20, 34)]
    m = c.mask(spline(H(palm), closed=True))
    lam = c.relief(m, radius=20, blur=1.5)
    c.engrave(m, np.clip(0.4 - 0.45 * lam, 0, 1), [c.ph_lin(H.angle(90))], spacing=6.5, wmax=2.2)
    c.outline(H.cp(palm), w=STRUCT, light=0.4)
    for k, oy in enumerate((-26, -9, 8, 25)):
        ln = (60, 70, 68, 56)[k]
        G = Frame(H.p(54, oy), rot=H.angle(-6 + k * 5))
        f = [(-4, -9), (ln * 0.6, -9), (ln, -2), (ln, 5), (ln * 0.6, 9), (-4, 9)]
        fm = c.mask(spline(G(f), closed=True))
        c.engrave(fm, 0.3, [c.ph_lin(G.angle(90))], spacing=6, wmax=2)
        c.outline(G.cp(f), w=3.2, light=0.4)
        c.line(G.cp([(ln * 0.5, -7), (ln * 0.5 + 3, 0), (ln * 0.5, 7)]), w=1.8, taper=0.3)

    # ============================================================ 癫狂层：背上隆起鳞甲，背脊顶出骨刺
    mad(c, rng)


def mad(c, rng):
    back = resample(spline(BACK[1:-1]), 3)
    t = np.gradient(back, axis=0)
    t /= np.hypot(t[:, 0], t[:, 1])[:, None]
    out = np.stack([t[:, 1], -t[:, 0]], 1)                                  # 指向背外
    # 鳞：一排排沿背的弧往里收的半月形，只铺在背的上半
    for row in range(5):
        inset = 26 + row * 46
        for i in range(10 + row * 3, len(back) - 10 - row * 3, 16):
            p = back[i] - out[i] * inset + t[i] * (8 if row % 2 else 0)
            n = out[i]
            s = 26 - row * 2
            arc = [tuple(p - t[i] * s), tuple(p + n * s * 0.75), tuple(p + t[i] * s)]
            c.stroke(spline(arc), 3.2, layer='mad', taper=(0.1, 0.1))
            c.stroke(spline([tuple(p - t[i] * s * 0.5 + n * s * 0.2), tuple(p + n * s * 0.5), tuple(p + t[i] * s * 0.5 + n * s * 0.2)]),
                     1.6, layer='mad', taper=0.2)
    # 骨刺：背脊上一排，往背外伸
    for i in range(20, len(back) - 20, 22):
        p = back[i]
        n = out[i]
        ln = 50 + 40 * math.sin(math.pi * i / len(back))
        spike = [tuple(p - t[i] * 16), tuple(p + n * ln * 0.55 - t[i] * 4), (*(p + n * ln + t[i] * 10), 'c'), tuple(p + t[i] * 16)]
        m = c.mask(spline(spike, closed=True))
        c.engrave(m, 0.4, [c.ph_lin(math.degrees(math.atan2(n[1], n[0])))], layer='mad', spacing=6, wmax=2, grain=0)
        c.stroke(spline(spike, closed=True), 3.4, layer='mad', closed=True, taper=0)
