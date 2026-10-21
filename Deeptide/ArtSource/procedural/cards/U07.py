"""U07 疯狂本能：瞳孔放大的眼睛特写，虹膜里有漩涡（策划/10 §4.6）。

能力牌是圆窗（只看得到中间 864 × 864 的圆）：一只眼睛占满圆窗，上下眼睑、睫毛、眉弓的皮肤褶；
瞳孔放得很大，虹膜只剩一圈，虹膜的纹路拧成漩涡。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, ellipse_pts, poly, smoothstep, spline

NAME = '疯狂本能'

CX, CY = 624, 450


def draw(c):
    rng = np.random.default_rng(7)
    # ------------------------------------------------------------ 皮肤：眼周，排线顺着眼眶的弧
    skin = np.ones((c.h, c.w), np.float32)
    orbit = np.hypot((c.xx - CX) / 1.5, (c.yy - CY - 10) / 1.0)
    c.engrave(skin, np.clip(0.25 + 0.5 * smoothstep(200, 420, orbit) + 0.05 * c.noise(40, 1) + 0.1 * c.grad(300, 100, 900, 800),
                            0, 1),
              [orbit * 1.0 + 2 * c.noise(30, 2), c.ph_lin(30, seed=3), c.ph_lin(-40, seed=4)], spacing=8.5, swell=1)
    # 眉弓下的褶、眼袋
    for k, (dy, w) in enumerate(((-250, 4.5), (-215, 3.0), (210, 3.0), (240, 2.4))):
        sd = -1 if dy < 0 else 1
        c.stroke(spline([(CX - 330 + abs(dy) * 0.1, CY + dy * 0.7), (CX, CY + dy), (CX + 330 - abs(dy) * 0.1, CY + dy * 0.75)]), w,
                 taper=0.35)

    # ------------------------------------------------------------ 眼：杏仁形的开口
    W, Hu, Hl = 400, 225, 168
    upper = spline([(CX - W, CY + 20, 'c'), (CX - W * 0.5, CY - Hu * 0.8), (CX + W * 0.1, CY - Hu), (CX + W * 0.6, CY - Hu * 0.7),
                    (CX + W, CY + 10, 'c')])
    lower = spline([(CX + W, CY + 10, 'c'), (CX + W * 0.5, CY + Hl * 0.85), (CX - W * 0.1, CY + Hl), (CX - W * 0.6, CY + Hl * 0.7),
                    (CX - W, CY + 20, 'c')])
    eye = c.mask(np.vstack([upper, lower]))
    # 眼白：淡淡的排线，靠眼角更暗；几道血丝
    sclera_tone = 0.08 + 0.4 * smoothstep(230, 400, np.abs(c.xx - CX)) + 0.25 * c.grad(0, CY - Hu, 0, CY - Hu + 60)
    c.engrave(eye, np.clip(sclera_tone, 0, 1), [c.ph_ring(CX, CY, sy=0.7), c.ph_lin(80)], spacing=7.5, wmax=2.2)
    for k in range(9):
        side = -1 if k % 2 else 1
        x0 = CX + side * rng.uniform(300, 370)
        y0 = CY + rng.uniform(-60, 60)
        pts = [(x0, y0)]
        for j in range(4):
            pts.append((pts[-1][0] - side * rng.uniform(18, 30), pts[-1][1] + rng.uniform(-14, 14)))
        c.add(c.stroke(spline(pts), 1.8, add=False, taper=(0.1, 0.8)) * eye)

    # 虹膜：一圈，纹路拧成漩涡
    IR, PR = 205, 128
    iris = c.ellipse(CX + 6, CY - 8, IR, IR) * eye
    r = np.hypot(c.xx - CX - 6, c.yy - CY + 8)
    a = np.arctan2(c.yy - CY + 8, c.xx - CX - 6)
    swirl = a * 70 + r * 1.1                                               # 螺旋：越往外拧得越紧
    c.paint(iris, np.clip(c.lines(swirl, 10, 2.4 + 3.0 * smoothstep(PR, IR, r))
                          + 0.35 * smoothstep(IR - 26, IR, r) + 0.3 * (1 - smoothstep(PR, PR + 26, r)), 0, 1))
    c.add(c.stroke(ellipse_pts(CX + 6, CY - 8, IR, IR), 6, closed=True, add=False) * eye)
    # 瞳孔：放大到几乎吃掉虹膜，纯黑；里面映着一个窗形高光
    pupil = c.ellipse(CX + 6, CY - 8, PR, PR) * eye
    c.fill(pupil)
    c.erase(c.mask(poly([(CX - 66, CY - 74), (CX - 34, CY - 78), (CX - 37, CY - 44), (CX - 68, CY - 42)])))
    c.erase(c.ellipse(CX + 50, CY + 40, 10, 10))
    # 上眼睑的阴影压在眼球上
    lid_shadow = eye * smoothstep(CY - Hu + 70, CY - Hu + 10, c.yy)
    c.engrave(lid_shadow, 0.6 * lid_shadow, [c.ph_lin(4, wobble=0.3)], spacing=6, wmax=2.4)

    # ------------------------------------------------------------ 眼睑的厚度 + 轮廓 + 睫毛
    c.stroke(upper, OUTLINE + 3, taper=(0.1, 0.25))
    c.stroke(lower, OUTLINE - 2, taper=(0.15, 0.15))
    c.stroke(spline([(CX - W * 0.8, CY - Hu * 0.55), (CX - W * 0.3, CY - Hu * 1.15), (CX + W * 0.3, CY - Hu * 1.1),
                     (CX + W * 0.85, CY - Hu * 0.5)]), 4.5, taper=0.3)     # 双眼皮的褶
    up = resample_curve(upper, 46)
    for i in range(2, len(up) - 2):
        p = up[i]
        u = i / len(up)
        ln = 40 + 40 * math.sin(math.pi * u)
        ang = math.radians(-90 - 50 * (0.5 - u) * 2 + rng.uniform(-6, 6))
        tip = (p[0] + ln * math.cos(ang) * 1.1, p[1] + ln * math.sin(ang))
        mid = ((p[0] + tip[0]) / 2 + (u - 0.5) * 10, (p[1] + tip[1]) / 2 - 4)
        c.stroke(spline([tuple(p), mid, tip]), 4.0, taper=(0.0, 0.9))
    lo = resample_curve(lower, 30)
    for i in range(3, len(lo) - 3, 2):
        p = lo[i]
        ln = 18 + rng.uniform(0, 10)
        c.stroke(poly([tuple(p), (p[0] + rng.uniform(-6, 6), p[1] + ln)]), 2.2, taper=(0.0, 0.9))
    # 泪阜（内眼角的小肉）
    c.engrave(c.ellipse(CX - W + 22, CY + 18, 20, 14), 0.5, [c.ph_lin(60)], spacing=5, wmax=2)
    c.stroke(ellipse_pts(CX - W + 22, CY + 18, 20, 14), 2.6, closed=True)


def resample_curve(pts, n):
    d = np.hypot(*np.diff(pts, axis=0).T)
    s = np.concatenate([[0], np.cumsum(d)])
    t = np.linspace(0, s[-1], n)
    return np.stack([np.interp(t, s, pts[:, 0]), np.interp(t, s, pts[:, 1])], 1)
