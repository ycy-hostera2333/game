"""U08 触须鞭笞：甩出的锁链。癫狂层：锁链变成带吸盘的触须（策划/10 §4.6）。

左下角一只手攥着锁链的一头，锁链甩成一道 S 形从左下抽到右上，链梢砸在一块石头上崩出碎屑；
背景是浅的雾。癫狂层沿同一条线画一条更粗的触须，轮廓和吸盘落在锁链外面的浅底上。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, resample, smoothstep, spline
from cards._common import tentacle, burst

NAME = '触须鞭笞'

PATH = [(250, 760), (380, 600), (420, 450), (560, 330), (760, 330), (900, 250), (1000, 120)]


def draw(c):
    rng = np.random.default_rng(8)
    # ------------------------------------------------------------ 背景：浅雾
    c.engrave(np.ones((c.h, c.w), np.float32), 0.2 + 0.42 * c.grad(200, 800, 1100, 100) + 0.06 * c.noise(90, 1),
              [c.ph_wave(-8, 6, 360, wobble=3, seed=2), c.ph_lin(60, seed=3)], spacing=10.5, bands=[(0.04, 0.5), (0.45, 0.85)])
    # 速度线：沿锁链外侧的几道弧
    path = resample(spline(PATH), 3)
    t = np.gradient(path, axis=0)
    t /= np.hypot(t[:, 0], t[:, 1])[:, None]
    nn = np.stack([-t[:, 1], t[:, 0]], 1)
    for off in (-70, -100, 80, 110):
        seg = path[len(path) // 3:] + nn[len(path) // 3:] * off
        c.stroke(seg, 2.2, taper=(0.8, 0.1))
    # 链梢砸中的地方
    ex, ey = PATH[-1]
    burst(c, ex, ey, 30, 120, n=20, width=3, seed=1)
    for _ in range(10):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(60, 140)
        s = rng.uniform(5, 12)
        c.fill(c.ellipse(ex + d * math.cos(a), ey + d * math.sin(a), s, s * 0.7, rot=math.degrees(a)), value=0.9)

    # ------------------------------------------------------------ 锁链：一节节椭圆环，交替正面 / 侧面
    d = np.hypot(*np.diff(path, axis=0).T)
    s = np.concatenate([[0], np.cumsum(d)])
    pos, k = 0.0, 0
    while pos < s[-1]:
        i = min(int(np.searchsorted(s, pos)), len(path) - 1)
        p, tt = path[i], t[i]
        a = math.degrees(math.atan2(tt[1], tt[0]))
        u = pos / s[-1]
        w = 44 * (1 - 0.35 * u)
        if k % 2 == 0:
            ring = c.ellipse(p[0], p[1], w * 1.25, w * 0.72, rot=a) - c.ellipse(p[0], p[1], w * 0.8, w * 0.32, rot=a)
            c.clear(np.clip(ring, 0, 1))
            lam = c.relief(np.clip(ring, 0, 1), radius=6, blur=0.8)
            c.engrave(np.clip(ring, 0, 1), np.clip(0.85 - 0.7 * lam, 0, 1), [c.ph_lin(a + 90, wobble=0), c.ph_lin(a + 30)], spacing=5.5, wmax=2.2)
            c.stroke(ellipse_pts(p[0], p[1], w * 1.25, w * 0.72, rot=a), 5.5, closed=True, light=0.5)
            c.stroke(ellipse_pts(p[0], p[1], w * 0.8, w * 0.32, rot=a), 3.5, closed=True)
        else:
            c.stroke([tuple(p - tt * w * 1.1), tuple(p + tt * w * 1.1)], 17 * (1 - 0.3 * u), taper=0)
            c.erase(c.stroke([tuple(p - tt * w * 0.8 - nn[i] * 2), tuple(p + tt * w * 0.8 - nn[i] * 2)], 2.4, add=False, taper=0.4))
        pos += w * 1.55
        k += 1

    # ------------------------------------------------------------ 手 + 袖子
    H = Frame((210, 790), rot=-50)
    sleeve = [(-420, -90), (-80, -76), (-60, 0), (-80, 80), (-420, 110)]
    m = c.mask(spline(H(sleeve), closed=True))
    c.engrave(m, 0.95, [c.ph_lin(H.angle(85)), c.ph_lin(H.angle(20)), c.ph_lin(H.angle(-40))], spacing=8, swell=1)
    c.outline(H.cp(sleeve), w=OUTLINE, light=0.4)
    fist = [(-70, -54), (0, -50), (40, -30), (44, 30), (10, 54), (-70, 50)]
    m = c.mask(spline(H(fist), closed=True))
    lam = c.relief(m, radius=22, blur=1.5)
    c.engrave(m, np.clip(0.5 - 0.5 * lam, 0, 1), [c.ph_lin(H.angle(90)), c.ph_lin(H.angle(20))], spacing=7)
    c.outline(H.cp(fist), w=STRUCT + 1, light=0.4)
    for y in (-30, -10, 10, 30):
        c.line(H.cp([(22, y - 8), (38, y), (28, y + 8)]), w=2.2, taper=0.3)

    # ============================================================ 癫狂层：锁链变成带吸盘的触须
    tentacle(c, PATH, 92, layer='mad', tone=0.35, taper=0.85, outline=4.6)
