"""R04 群体癔症：一群镇民同时尖叫着指向天空（策划/10 §4.6）。

下半画面挤满镇民（三排，近大远小），一个个仰着头、张着嘴，手臂齐齐举起指向左上的天空；
天上什么都看不清，只有一团旋着的乌云。前排几张脸看得到发白的脸和黑洞一样的嘴。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, smoothstep, spline
from cards._common import person

NAME = '群体癔症'


def draw(c):
    rng = np.random.default_rng(4)
    # ------------------------------------------------------------ 天：一团旋着的乌云
    sx, sy = 400, 140
    r = np.hypot(c.xx - sx, (c.yy - sy) * 1.6)
    a = np.arctan2((c.yy - sy) * 1.6, c.xx - sx)
    swirl = a * 80 + r * 0.9
    c.engrave(np.ones((c.h, c.w), np.float32), np.clip(0.75 - 0.4 * smoothstep(100, 700, r) + 0.08 * c.noise(50, 1), 0, 1),
              [swirl + 3 * c.noise(30, 2), c.ph_lin(10, seed=3), c.ph_lin(-50, seed=4)], spacing=9, swell=1.2)

    # ------------------------------------------------------------ 人群：后排小、前排大，手臂指向左上
    rows = [(520, 150, 0.85, 16), (640, 230, 0.9, 11), (800, 330, 0.95, 7)]
    for y0, h, tone, n in rows:
        xs = np.linspace(-40, c.w + 40, n) + rng.uniform(-25, 25, n)
        for x in xs:
            lean = rng.uniform(-6, 6)
            ang = math.degrees(math.atan2(sy - (y0 - h), sx - x)) + rng.uniform(-8, 8)   # 指向乌云
            arms = [(0.95, ang)]
            if rng.random() < 0.5:
                arms.append((0.9, ang + rng.uniform(-25, 25)))
            m = person(c, x, y0 + h * 0.15, h * 1.15, tone=0.0, hat=rng.random() < 0.4, lean=lean, arms=arms)
            c.clear(np.clip(c.shadow_of(m, 0, 0, blur=3) * 2.2, 0, 1))
            c.engrave(m, tone, [c.ph_lin(85, seed=int(abs(x)) % 5), c.ph_lin(15, seed=int(abs(x)) % 5 + 5)], spacing=7, swell=1.3)
            # 前两排：仰起的白脸 + 黑洞一样的嘴
            if h > 200:
                F = Frame((x, y0 + h * 0.15), rot=lean, scale=h * 1.15 / 340)
                fx, fy = F.p(0, -266)
                fr = 17 * h * 1.15 / 340
                face = c.ellipse(fx, fy, fr, fr * 1.15)
                c.clear(face)
                c.engrave(face, 0.25, [c.ph_lin(70)], spacing=6, wmax=2)
                c.stroke(ellipse_pts(fx, fy, fr, fr * 1.15), 2.6, closed=True)
                c.add(c.ellipse(fx + fr * 0.1, fy + fr * 0.45, fr * 0.28, fr * 0.4))
                for sd in (-1, 1):
                    c.add(c.ellipse(fx + sd * fr * 0.38, fy - fr * 0.25, fr * 0.12, fr * 0.14))
    # 尖叫：从人群头顶往上冒的几道波线
    for k in range(9):
        x = rng.uniform(100, 1150)
        y = rng.uniform(380, 480)
        c.erase(c.stroke(spline([(x, y), (x - 10, y - 30), (x + 6, y - 60), (x - 8, y - 90)]), 3.6, add=False, taper=(0.2, 0.9)))
