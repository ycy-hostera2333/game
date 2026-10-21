"""崩溃的裂纹贴图（策划/10 §7.2、§9）：全屏 16:9，2048 × 1152，单色精灵（工作画布 = 导出尺寸）。

从画面中心偏上的一点向外炸开 10 条主裂纹，主裂纹粗、往外收尖、带分叉；中心一圈碎片的轮廓；外围一些细碎的发丝裂纹。
盖在全屏上 0.6 秒淡出，所以只要轮廓有力，不需要细节。
"""
import math

import numpy as np

from etch import poly, spline

SCALE = 1


def crack(c):
    rng = np.random.default_rng(7)
    W, H = c.w, c.h
    ox, oy = W * 0.48, H * 0.42

    def walk(p, ang, width, depth):
        pts = [p]
        widths = [width]
        while width > 0.9 and 0 <= p[0] < W and 0 <= p[1] < H:
            step = rng.uniform(30, 70)
            ang += rng.uniform(-0.45, 0.45)
            p = (p[0] + step * math.cos(ang), p[1] + step * math.sin(ang))
            width *= 0.92
            pts.append(p)
            widths.append(width)
            if depth < 2 and rng.random() < 0.16:
                walk(p, ang + rng.choice([-1, 1]) * rng.uniform(0.5, 1.0), width * 0.6, depth + 1)
        if len(pts) > 1:
            arr = np.array(pts)
            c.stroke(arr, widths[0], taper=(0.0, 0.9), step=3)

    for i in range(10):
        a = i * 2 * math.pi / 10 + rng.uniform(-0.2, 0.2)
        walk((ox, oy), a, rng.uniform(9, 14), 0)
    # 中心：一圈碎片的轮廓（不规则多边形），像玻璃被砸的点
    ring = []
    for j in range(11):
        a = j * 2 * math.pi / 11 + rng.uniform(-0.15, 0.15)
        r = rng.uniform(50, 90)
        ring.append((ox + r * math.cos(a), oy + r * math.sin(a)))
    c.stroke(np.array(ring + ring[:1]), 6, taper=0)
    c.add(c.ellipse(ox, oy, 16, 14))
    for j in range(11):
        c.stroke(np.array([(ox, oy), ring[j]]), 4, taper=(0.0, 0.5))
    # 外围的发丝裂纹
    for _ in range(40):
        x, y = rng.uniform(0, W), rng.uniform(0, H)
        a = math.atan2(y - oy, x - ox) + rng.uniform(-0.6, 0.6)
        pts = [(x, y)]
        for j in range(4):
            a += rng.uniform(-0.6, 0.6)
            pts.append((pts[-1][0] + 26 * math.cos(a), pts[-1][1] + 26 * math.sin(a)))
        c.stroke(np.array(pts), 1.8, taper=(0.2, 0.8))


SPRITES = {
    'vfx_crack': (2048, 1152, crack),
}
