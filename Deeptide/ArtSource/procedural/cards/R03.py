"""R03 终末仪式：环形法阵和一圈蜡烛，法阵下潮水正在上涌（策划/10 §4.6）。

石板地上画着一个圆形法阵（透视成扁椭圆）：双圈、圈里的符文、中间一个五芒的星；一圈蜡烛立在外圈上。
法阵中心的石板裂开，黑水从裂缝里涌上来，漫过半个法阵，水面一圈圈波纹，几支蜡烛已经歪倒、熄灭冒烟。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, ellipse_pts, smoothstep, spline

NAME = '终末仪式'

CX, CY, RX, RY = 624, 560, 470, 175


def draw(c):
    rng = np.random.default_rng(3)
    # ------------------------------------------------------------ 背景：上面是黑暗，地面是透视的石板
    floor_y = 300
    c.engrave(c.rect(0, 0, c.w, floor_y), 0.9, [c.ph_lin(85, seed=1), c.ph_lin(5, seed=2), c.ph_lin(-40, seed=3)], spacing=9)
    floor = c.rect(0, floor_y, c.w, c.h)
    r = np.hypot((c.xx - CX) / RX, (c.yy - CY) / RY)
    c.engrave(floor, np.clip(0.2 + 0.6 * smoothstep(0.8, 1.9, r) + 0.05 * c.noise(40, 4), 0, 1),
              [c.ph_lin(0, wobble=1, seed=5), c.ph_lin(70, seed=6)], spacing=9)
    for k in range(1, 8):                                               # 石板缝：透视横缝 + 汇向远处的竖缝
        y = floor_y + 564 * (k / 7) ** 1.5
        c.stroke([(0, y), (c.w, y)], 1.4 + 2 * k / 7, taper=0)
    for x in np.arange(-600, 1900, 170):
        c.stroke([(CX + (x - CX) * 0.35, floor_y), (x, c.h)], 2.0, taper=0)

    # ------------------------------------------------------------ 法阵：双圈、符文、五芒星
    for s, w in ((1.0, 6), (0.88, 3.5), (0.45, 3)):
        c.stroke(ellipse_pts(CX, CY, RX * s, RY * s), w, closed=True)
    for k in range(20):                                                 # 双圈之间的符文
        a = 2 * math.pi * k / 20
        x, y = CX + RX * 0.94 * math.cos(a), CY + RY * 0.94 * math.sin(a)
        g = rng.integers(0, 3)
        if g == 0:
            c.stroke(ellipse_pts(x, y, 9, 5), 2.2, closed=True)
            c.stroke([(x, y - 9), (x, y + 9)], 2.2, taper=0)
        elif g == 1:
            c.stroke([(x - 10, y + 5), (x, y - 6), (x + 10, y + 5)], 2.4, taper=0)
        else:
            c.stroke([(x - 8, y - 4), (x + 8, y + 4)], 2.4, taper=0)
            c.stroke([(x - 8, y + 4), (x + 8, y - 4)], 2.4, taper=0)
    star = [(CX + RX * 0.86 * math.cos(-math.pi / 2 + k * 4 * math.pi / 5), CY + RY * 0.86 * math.sin(-math.pi / 2 + k * 4 * math.pi / 5))
            for k in range(6)]
    c.stroke(np.array(star), 3.6, taper=0)

    # ------------------------------------------------------------ 涌上来的黑水：法阵中心偏右，漫过半个圈
    wx, wy = CX + 60, CY + 10
    water = np.clip(1 - np.hypot((c.xx - wx) / 330, (c.yy - wy) / 125) + 0.12 * c.noise(30, 7), 0, 1)
    wm = smoothstep(0.0, 0.08, water)
    rw = np.hypot((c.xx - wx) / 330, (c.yy - wy) / 125)
    c.engrave(wm, np.clip(0.75 + 0.2 * (1 - rw), 0, 1), [rw * 330 + 2 * c.noise(20, 8), c.ph_lin(0, wobble=0.6)], spacing=8, swell=1.3)
    for k in range(1, 6):                                               # 一圈圈波纹，亮线
        c.erase(c.stroke(ellipse_pts(wx, wy, 330 * k / 6, 125 * k / 6), 2.6, add=False, closed=True) * wm)
    c.stroke(spline([(wx - 330, wy), (wx - 200, wy - 128), (wx + 200, wy - 128), (wx + 330, wy), (wx + 200, wy + 126),
                     (wx - 200, wy + 126)], closed=True), 3.0, closed=True, taper=0)
    # 中心的裂缝，水从这里往上冒
    for a in (0.3, 1.9, 3.4, 4.8):
        pts = [(wx, wy)]
        for j in range(5):
            a += rng.uniform(-0.4, 0.4)
            pts.append((pts[-1][0] + 40 * math.cos(a), pts[-1][1] + 16 * math.sin(a)))
        c.stroke(pts, 5, taper=(0.0, 0.8))
    for k in range(9):                                                  # 往上翻的水花
        a = rng.uniform(math.pi, 2 * math.pi)
        x0 = wx + rng.uniform(-60, 60)
        c.stroke(spline([(x0, wy), (x0 + 30 * math.cos(a), wy - rng.uniform(30, 90)), (x0 + 60 * math.cos(a), wy - rng.uniform(10, 40))]),
                 3, taper=(0.0, 0.9))

    # ------------------------------------------------------------ 蜡烛：立在外圈上，靠近水的几支歪倒熄灭
    for k in range(12):
        a = 2 * math.pi * k / 12 + 0.13
        x, y = CX + RX * 1.04 * math.cos(a), CY + RY * 1.04 * math.sin(a)
        scale = 0.7 + 0.5 * (y - (CY - RY)) / (2 * RY)                  # 近大远小
        h = 90 * scale
        w = 16 * scale
        drowned = np.hypot((x - wx) / 330, (y - wy) / 125) < 1.15
        tilt = rng.uniform(25, 60) * (1 if x > wx else -1) if drowned else 0
        t = math.radians(tilt)
        top = (x + h * math.sin(t), y - h * math.cos(t))
        body = c.stroke([(x, y), top], w * 2, add=False, taper=0)
        c.clear(body)
        c.engrave(body, 0.25 + 0.3 * (x > CX), [c.ph_lin(90 + tilt, wobble=0)], spacing=5.5, wmax=1.8)
        c.add(c.stroke([(x, y), top], w * 2 + 5, add=False, taper=0) - body * 1.0)
        if drowned:                                                     # 熄灭：一缕烟
            c.stroke(spline([top, (top[0] - 6, top[1] - 30), (top[0] + 8, top[1] - 60), (top[0] - 4, top[1] - 90)]), 2.0,
                     taper=(0.2, 0.9))
        else:
            fy = top[1] - 26 * scale
            flame = [(top[0], fy - 26 * scale), (top[0] + 9 * scale, fy), (top[0], fy + 14 * scale), (top[0] - 9 * scale, fy)]
            c.clear(c.mask(spline(flame, closed=True)))
            c.outline(flame, w=2.4, light=0)
            c.stroke([(top[0], top[1]), (top[0], top[1] - 8 * scale)], 2.4, taper=0)
            c.erase(1 - smoothstep(20 * scale, 70 * scale, np.hypot(c.xx - top[0], c.yy - fy)) * 1.0)
            c.clear(c.mask(spline(flame, closed=True)))
            c.outline(flame, w=2.4, light=0)
