"""U02 坚定信念：站在礁石上迎着巨浪的背影（策划/10 §4.6）。

一块黑礁石从左下角伸进画面，调查员站在礁石尖上，背对我们，风衣向后飞；
右边一道巨浪立起来，浪头卷过半个天空，浪尖碎成一排爪子样的浪花，正对着他。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, resample, smoothstep, spline

NAME = '坚定信念'


def draw(c):
    rng = np.random.default_rng(8)

    # ------------------------------------------------------------ 天：暴风雨前的云，横排线
    c.engrave(np.ones((c.h, c.w), np.float32), 0.5 + 0.2 * c.noise(140, 1) + 0.15 * c.grad(0, 0, 0, 500),
              [c.ph_wave(-6, 8, 360, wobble=3, seed=2), c.ph_lin(50, seed=3)], spacing=10, bands=[(0.04, 0.5), (0.45, 0.85)])

    # ------------------------------------------------------------ 巨浪：浪身是一面向左倾的水墙，顶上卷成一个管
    # 浪的外轮廓（从右下沿浪背上去，卷到左边的浪唇，再回到浪腹）
    crest = [(1260, 864), (1260, 120), (1150, 60), (1010, 40), (870, 70), (760, 130), (690, 210), (670, 290), (700, 340),
             (750, 330), (770, 280), (760, 240), (800, 210), (850, 220), (880, 280), (880, 380), (850, 500), (800, 620),
             (760, 720), (740, 864)]
    wave = c.mask(spline(crest, closed=True))
    # 水流方向：沿着浪面向上、在顶部向左卷——用以 (920, 280) 为圆心的同心弧近似
    cx, cy = 920, 300
    r = np.hypot(c.xx - cx, c.yy - cy)
    face_lit = c.grad(1260, 300, 700, 300)
    tone = np.clip(0.75 - 0.35 * face_lit + 0.1 * c.noise(40, 4) + 0.15 * smoothstep(200, 420, r), 0, 1)
    flow = c.ph_contour(wave, blur=8) + 4 * c.noise(40, 5)                 # 水纹顺着浪的轮廓走
    c.engrave(wave, tone, [flow, c.ph_lin(70, seed=6), c.ph_lin(-20, seed=7)], spacing=8.5, swell=1.2)
    # 浪腹的卷管：暗
    tube = c.mask(spline([(770, 280), (760, 240), (800, 210), (850, 220), (880, 280), (880, 380), (840, 360), (800, 320)],
                         closed=True))
    c.engrave(tube, 0.92, [c.ph_ring(830, 290), c.ph_lin(30)], spacing=7, swell=1.4)
    c.stroke(spline(crest[1:-1]), OUTLINE, taper=(0.2, 0.2), light=0.3)
    # 浪面上的水纹：顺着浪往上走的长弧线
    for k in range(7):                                                     # 浪面上几道更深的水纹
        off = 40 + k * 46
        line = c.stroke(resample(spline([(1260 - k * 8, 860 - k * 20), (1180 - k * 20, 400), (1100 - k * 30, 160 + k * 26),
                                          (960 - k * 10, 120 + k * 40), (900, 200 + k * 24)]), 3), 3.0, add=False, taper=(0.3, 0.6))
        c.add(line * wave)
    # 浪唇碎成一排爪子
    lip = resample(spline([(1150, 60), (1010, 40), (870, 70), (760, 130), (690, 210), (670, 290), (700, 340)]), 4)
    t = np.gradient(lip, axis=0)
    t /= np.hypot(t[:, 0], t[:, 1])[:, None]
    nrm = np.stack([t[:, 1], -t[:, 0]], 1)                                # 指向浪外
    for i in range(6, len(lip) - 4, 9):
        p = lip[i]
        n = nrm[i]
        ln = rng.uniform(40, 90)
        side = np.array([-n[1], n[0]])
        claw = [tuple(p - side * 12), tuple(p + n * ln * 0.6 - side * 6), tuple(p + n * ln + side * ln * 0.3),
                tuple(p + n * ln * 0.4 + side * 8), tuple(p + side * 12)]
        m = c.mask(spline(claw, closed=True))
        c.clear(m)
        c.engrave(m, 0.18, [c.ph_lin(rng.uniform(0, 180), wobble=0.3)], spacing=6, wmax=2)
        c.stroke(spline(claw, closed=True), 3, closed=True, taper=0)
        for _ in range(3):                                                # 水沫
            q = p + n * rng.uniform(ln, ln * 1.6) + side * rng.uniform(-30, 30)
            rr = rng.uniform(3, 7)
            c.erase(c.ellipse(q[0], q[1], rr + 2, rr + 2))
            c.stroke(ellipse_pts(q[0], q[1], rr, rr), 2.0, closed=True)

    # ------------------------------------------------------------ 前景的海：起伏的浪谷，深色
    sea = c.mask(poly([(-10, 640), (300, 620), (760, 660), (760, 870), (-10, 870)]))
    c.engrave(sea, 0.75, [c.ph_wave(-3, 10, 160, wobble=3, seed=9), c.ph_lin(40, seed=10)], spacing=8, swell=1.2)
    for y in (680, 730, 790, 840):
        for x in np.arange(-10, 760, 90):
            xo = x + rng.uniform(0, 40)
            c.erase(c.stroke(spline([(xo, y), (xo + 30, y - 8), (xo + 60, y)]), 2.6, add=False, taper=0.5))

    # ------------------------------------------------------------ 礁石：从左下伸到画面中下，顶上是一个尖
    rock = [(-10, 864), (-10, 600), (80, 560), (200, 500), (300, 470), (370, 440), (420, 452), (470, 520), (560, 600),
            (640, 700), (700, 864)]
    rm = c.mask(spline(rock, closed=True))
    lam = c.relief(rm, radius=80, blur=5)
    c.engrave(rm, np.clip(1.0 - 0.45 * lam, 0.55, 1), [c.ph_lin(40, seed=11), c.ph_lin(-30, seed=12), c.ph_lin(85, seed=13)],
              spacing=8, swell=1.2)
    for pts in ([(200, 500), (230, 600), (210, 720)], [(370, 444), (380, 560), (440, 680)], [(80, 560), (130, 680)]):
        c.line(pts, w=4.5, taper=0.3)
    c.erase(c.stroke(spline([(90, 556), (200, 498), (300, 468), (366, 440)]), 4, add=False, taper=0.3))   # 礁石顶上的亮边
    c.outline(rock, w=OUTLINE, light=0.4)

    figure(c, Frame((382, 446), scale=0.95))


def figure(c, F):
    """背影：宽檐帽、风衣向左后方飞。原点在脚底。"""
    coat = [(-26, 0), (-30, -80), (-40, -150), (-34, -200), (-20, -230), (20, -230), (36, -200), (40, -150), (34, -90),
            (28, -40), (24, 0)]
    tail = [(-30, -150), (-90, -130), (-170, -100), (-230, -90), (-180, -70), (-210, -40), (-140, -46), (-80, -40), (-36, -60)]
    m = c.mask(spline(F(tail), closed=True))
    c.engrave(m, 0.88, [c.ph_lin(-15, seed=20), c.ph_lin(60, seed=21)], spacing=7.5, swell=1)
    c.outline(F.cp(tail), w=STRUCT, light=0.4)
    m = c.mask(spline(F(coat), closed=True))
    lam = c.relief(m, radius=20, blur=2)
    c.engrave(m, np.clip(0.98 - 0.4 * lam, 0.6, 1), [c.ph_lin(85, seed=22), c.ph_lin(20, seed=23)], spacing=7, swell=1)
    c.outline(F.cp(coat), w=STRUCT + 1, light=0.4)
    c.fill(c.ellipse(*F.p(0, -250), 20, 24), value=0.95)
    c.fill(c.ellipse(*F.p(0, -270), 46, 9), value=1.0)
    c.fill(c.mask(F([(-22, -270), (-18, -306), (18, -306), (22, -270)])), value=1.0)
    for sd in (-1, 1):                                                     # 手臂垂在两边
        c.line(F.cp([(sd * 32, -200), (sd * 44, -140), (sd * 40, -100)]), w=14, taper=(0.0, 0.3))
