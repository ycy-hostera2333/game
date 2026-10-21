"""C07 撕裂：被撕开的布帛。癫狂层：裂口里伸出第二只手（策划/10 §4.6）。

一幅垂挂的亚麻布占满画面，竖褶；一道斜的裂口从左上撕到右下，裂口边缘翻卷、拉出线头，后面是黑的。
癫狂层的手从裂口中段伸出来，手指张开压在布面上——布是浅色的，癫狂面里这只手才看得清。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, resample, smoothstep, spline

NAME = '撕裂'

TEAR_A, TEAR_B = (400, 90), (850, 790)


def tear_geometry():
    """裂口的左右两条边（锯齿），中间张开最宽。"""
    rng = np.random.default_rng(17)
    n = 46
    t = np.linspace(0, 1, n)
    ax, ay = TEAR_A
    bx, by = TEAR_B
    cx = ax + (bx - ax) * t + 40 * np.sin(t * math.pi * 1.3)
    cy = ay + (by - ay) * t
    dx, dy = (bx - ax), (by - ay)
    L = math.hypot(dx, dy)
    nx, ny = -dy / L, dx / L                                              # 指向左下
    width = 82 * np.sin(np.pi * t) ** 0.8
    jag_l = rng.uniform(-14, 14, n) * (0.3 + np.sin(np.pi * t))
    jag_r = rng.uniform(-14, 14, n) * (0.3 + np.sin(np.pi * t))
    left = np.stack([cx - nx * (width + jag_l), cy - ny * (width + jag_l)], 1)
    right = np.stack([cx + nx * (width + jag_r), cy + ny * (width + jag_r)], 1)
    return left, right, (nx, ny), np.stack([cx, cy], 1)


def draw(c):
    left, right, (nx, ny), mid = tear_geometry()
    hole = c.mask(np.vstack([left, right[::-1]]))

    # ------------------------------------------------------------ 布：竖褶，褶子在裂口两边被拉开
    side = np.sign((c.xx - mid[:, 0].mean()) * nx + (c.yy - mid[:, 1].mean()) * ny)
    # 到裂口中线的距离
    ax, ay = TEAR_A
    bx, by = TEAR_B
    dist = np.abs((c.xx - ax) * nx + (c.yy - ay) * ny)
    pull = 70 * np.exp(-dist / 170) * side
    xw = c.xx - pull
    phase = xw / 30 + 0.9 * np.sin(c.yy / 260 + 0.4)
    fold = np.sin(phase) * 0.7 + 0.3 * np.sin(2.1 * phase + 1.3)                # 1 = 褶脊（亮），-1 = 褶谷
    tone = 0.2 - 0.2 * fold + 0.16 * c.grad(200, 0, 1200, 864) + 0.04 * c.noise(60, 2)
    tone = tone + 0.3 * np.exp(-dist / 60)                                  # 裂口边上布被扯皱
    cloth = np.ones((c.h, c.w), np.float32)
    c.engrave(cloth, np.clip(tone, 0, 1), [xw + 2 * c.noise(40, 3), c.yy + 10 * np.sin(phase), c.ph_lin(35, seed=5)],
              spacing=10.5, swell=0.9, bands=[(0.05, 0.42), (0.38, 0.7), (0.66, 0.95)])
    # 褶谷的墨线：沿 fold 的谷底
    for k in range(-4, 16):
        pts = []
        for y in np.linspace(0, c.h, 60):
            ph0 = (2 * k + 1.5) * math.pi - 0.9 * math.sin(y / 260 + 0.4)
            xx = ph0 * 30
            d = abs((xx - ax) * nx + (y - ay) * ny)
            sd = 1 if (xx - mid[:, 0].mean()) * nx + (y - mid[:, 1].mean()) * ny > 0 else -1
            pts.append((xx + 70 * math.exp(-d / 170) * sd, y))
        pts = np.array(pts)
        if -50 < pts[:, 0].mean() < c.w + 50:
            c.stroke(pts, 3.0, taper=0.25)

    # ------------------------------------------------------------ 裂口：后面是黑的
    c.fill(hole, value=1.0)
    c.engrave(hole, 0.95, [c.ph_lin(-40), c.ph_lin(40)], spacing=6, swell=1.4)
    # 翻卷的布边：沿两条边，一串小三角翻过来露出布的反面（浅）
    rng = np.random.default_rng(23)
    for edge, sd in ((left, 1), (right, -1)):
        i = 3
        while i < len(edge) - 5:
            span = int(rng.integers(2, 5))
            p0, p1 = edge[i], edge[min(i + span, len(edge) - 1)]
            if rng.random() < 0.6:
                out = np.array([nx, ny]) * sd                              # 翻向布面一侧
                along = (p1 - p0)
                size = rng.uniform(18, 46) * (0.5 + math.sin(math.pi * i / len(edge)))
                q1 = p0 + out * size * rng.uniform(0.6, 1.0) + along * rng.uniform(0.1, 0.4)
                q2 = p1 + out * size * rng.uniform(0.3, 0.7) - along * rng.uniform(0.0, 0.3)
                flap_pts = [tuple(p0), tuple(q1), tuple(q2), tuple(p1)]
                flap = c.mask(poly(flap_pts))
                c.engrave(flap, 0.1 + 0.25 * rng.random(), [c.ph_along(tuple(p0), tuple(p1), wobble=0.3)], spacing=6.5, wmax=2)
                c.stroke(poly(flap_pts), 2.8, taper=0)
                for _ in range(3):                                         # 撕开的纤维
                    a = q1 + (q2 - q1) * rng.random()
                    c.stroke(poly([tuple(a), tuple(a + out * rng.uniform(5, 14) + rng.normal(0, 3, 2))]), 1.4, taper=(0, 0.8))
            i += span
        c.stroke(resample(edge, 3), OUTLINE * 0.8, taper=(0.15, 0.15), light=0.3)
    # 线头：横跨裂口的细丝，有的断了
    for i in range(3, len(left) - 3, 2):
        if rng.random() < 0.45:
            continue
        p0, p1 = left[i], right[i + rng.integers(-1, 2)]
        if rng.random() < 0.5:
            mid_p = p0 + (p1 - p0) * rng.uniform(0.3, 0.6) + np.array([0, rng.uniform(8, 20)])
            c.stroke(spline([tuple(p0), tuple(mid_p), tuple(p1)]), 1.6, taper=0.1)
        else:
            k = rng.uniform(0.25, 0.45)
            for a, b in ((p0, p0 + (p1 - p0) * k), (p1, p1 + (p0 - p1) * k)):
                c.stroke(spline([tuple(a), tuple(0.5 * (a + b) + np.array([0, 8])), tuple(b)]), 1.6, taper=(0.0, 0.8))
    # ============================================================ 癫狂层：裂口里伸出的第二只手
    k = 22
    ox, oy = mid[k] + np.array([nx, ny]) * 30
    hand(c, Frame((ox, oy), rot=math.degrees(math.atan2(ny, nx)) + 8, scale=1.35), hole)


def hand(c, F, hole):
    """从裂口里伸出来的手：手腕在裂口里，手指张开压在布面上。局部坐标：+x 指向指尖。"""
    arm = [(-260, -40), (-40, -46), (10, -52), (10, 52), (-40, 46), (-260, 40)]
    palm = [(-20, -58), (40, -66), (96, -60), (112, 0), (98, 58), (40, 66), (-20, 56)]
    fingers = [(-46, 34, 120, -24), (-16, 37, 148, -8), (14, 36, 152, 7), (42, 31, 126, 22)]   # (y, 宽, 长, 张开角)
    m = c.mask(spline(F(arm), closed=True)) * hole                      # 手腕只在裂口里露出来
    c.engrave(m, 0.4, [c.ph_lin(F.angle(0), seed=30)], layer='mad', spacing=9, grain=0)
    c.add(c.stroke(F(spline(arm[:3])), STRUCT, add=False, taper=0) * hole, 'mad')
    c.add(c.stroke(F(spline(arm[3:])), STRUCT, add=False, taper=0) * hole, 'mad')
    m = c.mask(spline(F(palm), closed=True))
    lam = c.relief(m, radius=40, blur=3)
    c.engrave(m, np.clip(0.75 - 0.7 * lam, 0, 1), [c.ph_axis(F.p(0, 0), F.p(110, 0), bow=-10, half=60, seed=32), c.ph_lin(F.angle(50))],
              layer='mad', spacing=7.5, grain=0)
    c.outline(F.cp(palm), w=STRUCT + 1, layer='mad', light=0.4)
    for i, (fy, fw, ln, spread) in enumerate(fingers):
        G = Frame(F.p(96, fy), rot=F.angle(spread))
        hw = fw / 2
        # 三节，关节处弯一点，像爪
        pts = [(-6, -hw), (ln * 0.4, -hw - 2), (ln * 0.72, -hw + 2), (ln * 0.94, -hw * 0.4 + 6), (ln + 14, 8, 'c'),
               (ln * 0.92, hw * 0.6 + 6), (ln * 0.7, hw + 2), (ln * 0.4, hw + 1), (-6, hw)]
        m = c.mask(spline(G(pts), closed=True))
        lam = c.relief(m, radius=hw, blur=1)
        c.engrave(m, np.clip(0.7 - 0.7 * lam, 0, 1), [c.ph_axis(G.p(0, 0), G.p(ln, 0), bow=-6, half=hw, seed=33 + i)],
                  layer='mad', spacing=6.5, wmax=2.4, grain=0)
        c.outline(G.cp(pts), w=STRUCT, layer='mad', light=0.4)
        for j in (0.4, 0.72):
            c.line(G.cp([(ln * j - 4, -hw * 0.8), (ln * j + 3, 0), (ln * j - 4, hw * 0.8)]), w=2.2, layer='mad', taper=0.3)
        c.line(G.cp([(ln * 0.86, -hw * 0.5), (ln + 12, 8), (ln * 0.86, hw * 0.4 + 4)]), w=3.4, layer='mad', taper=0.2)   # 指甲
    thumb = [(10, -50), (60, -96), (110, -128), (130, -126), (120, -104), (70, -60)]
    m = c.mask(spline(F(thumb), closed=True))
    c.engrave(m, 0.5, [c.ph_lin(F.angle(-40))], layer='mad', spacing=6.5, grain=0)
    c.outline(F.cp(thumb), w=STRUCT, layer='mad', light=0.4)
