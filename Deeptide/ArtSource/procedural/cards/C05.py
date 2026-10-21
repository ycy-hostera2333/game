"""C05 圣水泼洒：圣水泼出一道弧形水幕，水幕后的影子在退缩（策划/10 §4.6）。

左下角一只手把小银瓶甩出去，水从瓶口泼成一道从左下扫到右上的弧形水幕（亮的，水幕里是顺着弧走的细线和水珠）；
水幕后面（右边）几个高瘦的黑影向后仰、抬手挡脸，往暗处缩。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, smoothstep, spline
from cards._common import wraith

NAME = '圣水泼洒'

ARC_C = (180, 900)     # 水幕弧的圆心（手的位置附近）


def draw(c):
    rng = np.random.default_rng(5)
    ax, ay = ARC_C
    r = np.hypot(c.xx - ax, c.yy - ay)
    ang = np.degrees(np.arctan2(c.yy - ay, c.xx - ax))

    # ------------------------------------------------------------ 背景：夜里的巷子，右边更暗
    c.engrave(np.ones((c.h, c.w), np.float32), 0.45 + 0.4 * c.grad(300, 600, 1150, 300) + 0.05 * c.noise(100, 1),
              [c.ph_lin(-60, wobble=2, seed=2), c.ph_lin(30, seed=3), c.ph_lin(-10, seed=4)], spacing=10, swell=1.1)

    # ------------------------------------------------------------ 影子：水幕后面往后缩的几个人形
    for x, y, h, lean in ((1100, 700, 380, 26), (780, 790, 540, 14), (960, 760, 470, 20)):
        wraith(c, x, y, h, lean=lean, flip=False)
        # 被圣水打到的地方冒烟：几缕向上的曲线
        for _ in range(5):
            px, py = x + rng.uniform(-h * 0.12, h * 0.06), y - rng.uniform(h * 0.45, h * 0.95)
            c.erase(c.stroke(spline([(px, py), (px - 14, py - 30), (px + 6, py - 60), (px - 8, py - 90)]), 3.0, add=False,
                             taper=(0.2, 0.9)))

    # ------------------------------------------------------------ 水幕：一道弧形的亮带，越往外越碎
    band = smoothstep(500, 540, r) * (1 - smoothstep(700, 760, r)) * smoothstep(-95, -80, ang) * (1 - smoothstep(-12, -2, ang))
    band = np.clip(band + 0.0, 0, 1)
    c.clear(band)
    streak = c.lines(r + 3 * c.noise(20, 5), 11, 1.6 + 1.2 * c.noise(15, 6))
    c.add(streak * band * (0.35 + 0.65 * smoothstep(640, 720, r)))
    for rr in (502, 742):
        c.stroke(ellipse_pts(ax, ay, rr, rr, a0=-92, a1=-6, n=200), 3.0 if rr < 600 else 2.2, taper=(0.3, 0.3))
    # 水珠：沿弧的外沿甩出去
    for _ in range(60):
        a = math.radians(rng.uniform(-92, -6))
        rr = rng.uniform(745, 830)
        x, y = ax + rr * math.cos(a), ay + rr * math.sin(a)
        s = rng.uniform(3, 9) * (1.3 - (rr - 740) / 300)
        c.clear(c.ellipse(x, y, s + 3, s + 3))
        c.stroke(ellipse_pts(x, y, s, s * 1.2, rot=math.degrees(a)), 2.0, closed=True)
        c.add(c.ellipse(x + s * 0.3, y + s * 0.3, s * 0.35, s * 0.35))

    # ------------------------------------------------------------ 手和瓶：左下角
    H = Frame((210, 760), rot=-35)
    flask = [(0, -30), (70, -34), (96, -20), (130, -14), (130, 14), (96, 20), (70, 34), (0, 30), (-10, 0)]
    m = c.mask(spline(H(flask), closed=True))
    lam = c.relief(m, radius=18, blur=1)
    c.engrave(m, np.clip(0.65 - 0.7 * lam, 0, 1), [c.ph_lin(H.angle(0)), c.ph_lin(H.angle(80))], spacing=6, wmax=2.2)
    c.outline(H.cp(flask), w=STRUCT, light=0.4)
    c.stroke(H([(30, -20), (40, 0), (30, 20)]), 2.4, taper=0.2)              # 瓶身上的十字
    c.stroke(H([(20, 0), (50, 0)]), 2.4, taper=0.2)
    sleeve = [(-300, -90), (-60, -70), (-40, 0), (-60, 80), (-300, 110)]
    m = c.mask(spline(H(sleeve), closed=True))
    c.engrave(m, 0.95, [c.ph_lin(H.angle(85)), c.ph_lin(H.angle(20)), c.ph_lin(H.angle(-40))], spacing=8, swell=1)
    c.outline(H.cp(sleeve), w=OUTLINE, light=0.4)
    fist = [(-60, -50), (0, -46), (20, -26), (22, 26), (0, 44), (-60, 46)]
    m = c.mask(spline(H(fist), closed=True))
    lam = c.relief(m, radius=20, blur=1.5)
    c.engrave(m, np.clip(0.55 - 0.55 * lam, 0, 1), [c.ph_lin(H.angle(90)), c.ph_lin(H.angle(20))], spacing=7)
    c.outline(H.cp(fist), w=STRUCT + 1, light=0.4)
    for y in (-26, -6, 14, 32):
        c.line(H.cp([(4, y - 6), (16, y), (8, y + 8)]), w=2.2, taper=0.3)
