"""S02 缠网（状态）：缠成一团的渔网和鱼钩（策划/10 §4.5、§4.6）。

一团乱糟糟的渔网：网眼被扯得歪七扭八、互相缠住，几根粗绳从团里伸出来，打着结；
网里挂着几个生锈的鱼钩和一个软木浮标。状态牌整张偏灰（卡面是纸影色），所以这里墨不多、对比柔和。
"""
import math

import numpy as np

from etch import STRUCT, Frame, ellipse_pts, smoothstep, spline

NAME = '缠网'


def draw(c):
    rng = np.random.default_rng(2)
    cx, cy = 624, 440
    c.engrave(np.ones((c.h, c.w), np.float32), 0.18 + 0.05 * c.noise(80, 1), [c.ph_lin(-20, wobble=2, seed=1)], spacing=11)
    # ------------------------------------------------------------ 网团的暗心：越往里越密
    r = np.hypot((c.xx - cx) / 1.5, c.yy - cy)
    c.engrave(np.ones((c.h, c.w), np.float32) * (1 - smoothstep(180, 300, r)), np.clip(0.7 - 0.5 * r / 300, 0, 1),
              [c.ph_lin(30, seed=2), c.ph_lin(-50, seed=3)], spacing=8)
    # ------------------------------------------------------------ 网：几层扭曲的菱形网眼，叠在一起
    for layer in range(3):
        ang = rng.uniform(0, 180)
        warp = 8 + 8 * layer
        a = math.radians(ang)
        u = c.xx * math.cos(a) + c.yy * math.sin(a) + warp * c.noise(90, 10 + layer)
        v = -c.xx * math.sin(a) + c.yy * math.cos(a) + warp * c.noise(90, 20 + layer)
        mesh = np.maximum(c.lines(u + v, 64, 3.0), c.lines(u - v, 64, 3.0))
        knots = c.lines(u + v, 64, 9) * c.lines(u - v, 64, 9)
        blob = 1 - smoothstep(250 + 40 * layer, 420 + 40 * layer, r + 60 * c.noise(70, 30 + layer))
        c.add(np.maximum(mesh, knots) * blob)
    # ------------------------------------------------------------ 粗绳：从团里伸出来，打结
    for k in range(5):
        a = rng.uniform(0, 2 * math.pi)
        pts = [(cx + 60 * math.cos(a), cy + 40 * math.sin(a))]
        for j in range(6):
            a += rng.uniform(-0.6, 0.6)
            pts.append((pts[-1][0] + 90 * math.cos(a) * 1.3, pts[-1][1] + 90 * math.sin(a)))
        rope = spline(pts)
        c.stroke(rope, 14, taper=(0.0, 0.1))
        c.erase(c.stroke(rope, 9, add=False, taper=(0.0, 0.1)))
        tt = np.gradient(rope, axis=0)
        tt /= np.hypot(tt[:, 0], tt[:, 1])[:, None]
        for i in range(0, len(rope), 9):                                     # 绳子的捻纹
            p = rope[i]
            n = np.array([-tt[i][1], tt[i][0]])
            c.stroke([tuple(p - n * 6 - tt[i] * 4), tuple(p + n * 6 + tt[i] * 4)], 2, taper=0)
        kx, ky = rope[len(rope) // 2]
        c.fill(c.ellipse(kx, ky, 16, 14), value=0.0)
        c.stroke(ellipse_pts(kx, ky, 16, 14), 4, closed=True)
        c.stroke([(kx - 10, ky - 8), (kx + 10, ky + 8)], 3, taper=0)
    # ------------------------------------------------------------ 鱼钩
    for x, y, rot, s in ((440, 300, 20, 1.8), (820, 540, -30, 2.0), (520, 640, 160, 1.6), (900, 270, 70, 1.5)):
        F = Frame((x, y), rot=rot, scale=s)
        hook = spline(F([(0, -80), (0, 10), (6, 34), (26, 44), (44, 30), (46, 6)]))
        c.erase(c.stroke(hook, 6 * s + 12, add=False, taper=(0.0, 0.3)))
        c.stroke(hook, 5 * s, taper=(0.0, 0.5))
        c.stroke(F([(46, 6), (36, 20)]), 3.5 * s, taper=(0.0, 0.6))     # 倒刺
        c.erase(c.ellipse(*F.p(0, -88), 12 * s, 12 * s))
        c.stroke(ellipse_pts(*F.p(0, -88), 8 * s, 8 * s), 3.4, closed=True)
        c.stroke(F([(0, -96), (-30, -170)]), 2.0, taper=0)                # 鱼线
    # 软木浮标
    bx, by = 720, 410
    fm = c.ellipse(bx, by, 44, 30, rot=-20)
    c.clear(fm)
    lam = c.relief(fm, radius=16, blur=1)
    c.engrave(fm, np.clip(0.55 - 0.6 * lam, 0, 1), [c.ph_lin(70), c.ph_lin(-20)], spacing=6, wmax=2.2)
    c.stroke(ellipse_pts(bx, by, 44, 30, rot=-20), 4, closed=True)
    c.stroke([(bx - 8, by - 44), (bx + 10, by + 46)], 5, taper=0)
