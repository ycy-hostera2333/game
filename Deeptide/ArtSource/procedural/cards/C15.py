"""C15 查阅资料：堆满书的书桌和一只放大镜。癫狂层：书里的插图爬出了书页（策划/10 §4.6）。

书桌从前上方看：左边一摞书，中间一本摊开的大书，书页上是一幅海洋生物的图版（触须、吸盘）和几行字；
放大镜压在右页上，镜片里的字被放大。癫狂层：图版里那只东西的触须伸出了页边，爬到桌面上。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, resample, scrawl, smoothstep, spline

NAME = '查阅资料'

# 摊开的书（透视四边形）：左页、右页
BOOK_L = [(330, 330), (628, 300), (628, 700), (290, 740)]
BOOK_R = [(628, 300), (930, 318), (980, 736), (628, 700)]


def quad(pt, q):
    """四边形 q 里的双线性坐标 (u, v) → 画布。"""
    u, v = pt
    a, b, c_, d = (np.array(p, float) for p in q)
    top = a + (b - a) * u
    bot = d + (c_ - d) * u
    return tuple(top + (bot - top) * v)


def draw(c):
    rng = np.random.default_rng(5)

    # ------------------------------------------------------------ 桌面 + 远处的墙
    wall = c.rect(0, 0, c.w, 150)
    c.engrave(wall, 0.75, [c.ph_lin(88, seed=1), c.ph_lin(-20, seed=2)], spacing=9)
    desk = 1 - wall
    c.engrave(desk, 0.12 + 0.4 * c.radial(640, 520, 250, 820) ** 1.5 + 0.05 * c.noise(70, 3),
              [c.ph_wave(-4, 5, 400, wobble=2, seed=4), c.ph_lin(60, seed=5)], spacing=10)
    c.stroke(poly([(0, 150), (c.w, 150)]), OUTLINE, taper=0)

    # ------------------------------------------------------------ 左边一摞书
    y = 650
    for k in range(6):
        h = rng.uniform(40, 62)
        w = rng.uniform(250, 300)
        x = 60 + rng.uniform(-14, 14)
        skew = rng.uniform(-8, 8)
        spine = [(x, y - h), (x + w, y - h + skew), (x + w, y + skew), (x, y)]
        m = c.mask(poly(spine))
        c.engrave(m, 0.55 + 0.3 * rng.random(), [c.ph_lin(skew / w * 57, seed=10 + k), c.ph_lin(80, seed=20 + k)], spacing=7.5)
        c.stroke(poly(spine + spine[:1]), STRUCT, taper=0)
        for bx in (x + 30, x + w - 40):                                    # 书脊上的两道烫金线
            c.stroke(poly([(bx, y - h + 6), (bx, y - 6)]), 2.4, taper=0)
            c.erase(c.stroke(poly([(bx + 6, y - h + 6), (bx + 6, y - 6)]), 2.0, add=False, taper=0))
        y -= h
    # 最上面一本是斜放的
    top = [(80, y + 10), (360, y - 40), (380, y), (100, y + 50)]
    c.engrave(c.mask(poly(top)), 0.7, [c.ph_lin(-10), c.ph_lin(70)], spacing=7.5)
    c.stroke(poly(top + top[:1]), STRUCT, taper=0)

    # ------------------------------------------------------------ 摊开的书
    shadow = c.mask(poly([(300, 750), (990, 748), (1030, 790), (330, 800)]))
    c.engrave(shadow, 0.8, [c.ph_lin(0), c.ph_lin(50)], spacing=8)
    for q, sd in ((BOOK_L, -1), (BOOK_R, 1)):
        m = c.mask(poly(q))
        gutter = c.grad(*quad((0.0 if sd > 0 else 1.0, 0.5), q), *quad((0.5, 0.5), q)) if sd > 0 else \
            c.grad(*quad((1.0, 0.5), q), *quad((0.5, 0.5), q))
        c.engrave(m, 0.4 * (1 - gutter) ** 3, [c.ph_lin(90 if sd < 0 else 92, wobble=0.5)], spacing=7, wmax=2.2)
        c.stroke(poly(q + q[:1]), STRUCT, taper=0)
    # 页边下面的书页厚度
    for q in (BOOK_L, BOOK_R):
        for k in range(1, 4):
            c.stroke(poly([(q[3][0] + 0, q[3][1] + k * 6), (q[2][0], q[2][1] + k * 6)]), 1.6, taper=0)
    c.stroke(poly([(628, 300), (628, 700)]), 4, taper=0)

    # 左页：图版——一只章鱼样的东西，下面一行图注
    plate_box = [quad((0.12, 0.08), BOOK_L), quad((0.88, 0.08), BOOK_L), quad((0.88, 0.7), BOOK_L), quad((0.12, 0.7), BOOK_L)]
    c.stroke(poly(plate_box + plate_box[:1]), 2.6, taper=0)
    cx, cy = quad((0.5, 0.32), BOOK_L)
    creature(c, cx, cy + 10, 0.85, 'ink', rng)
    for v in (0.78, 0.85, 0.92):
        a, b = quad((0.14, v), BOOK_L), quad((0.86, v), BOOK_L)
        scrawl(c, a[0], b[0], a[1], 8, seed=int(v * 100), width=1.8, slope=(b[1] - a[1]) / (b[0] - a[0]), gap=(8, 14))
    # 右页：几段字
    for i, v in enumerate(np.linspace(0.08, 0.92, 15)):
        a, b = quad((0.1, v), BOOK_R), quad((0.9 - 0.25 * (i % 5 == 4), v), BOOK_R)
        scrawl(c, a[0], b[0], a[1], 8, seed=40 + i, width=1.8, slope=(b[1] - a[1]) / (b[0] - a[0]), gap=(8, 14))

    # ------------------------------------------------------------ 放大镜：压在右页上，镜片里的字放大
    lx, ly, lr = 820, 470, 110
    lens = c.ellipse(lx, ly, lr, lr * 0.9)
    c.clear(lens, layers=('ink',))
    for i, dy in enumerate((-60, -20, 20, 60)):
        scrawl(c, lx - 100, lx + 100, ly + dy + 10, 18, seed=70 + i, width=3.6, gap=(16, 26))
    c.erase(c.stroke(spline([(lx - 70, ly - 50), (lx - 40, ly - 76), (lx, ly - 86)]), 7, add=False, taper=0.5))
    ring = c.ellipse(lx, ly, lr + 14, lr * 0.9 + 14) - lens
    c.engrave(np.clip(ring, 0, 1), 0.55, [c.ph_ring(lx, ly, sy=0.9)], spacing=5, wmax=2.2)
    c.stroke(ellipse_pts(lx, ly, lr + 14, lr * 0.9 + 14), STRUCT, closed=True)
    c.stroke(ellipse_pts(lx, ly, lr, lr * 0.9), 3, closed=True)
    H = Frame((lx + 100, ly + 70), rot=38)
    handle = [(0, -16), (40, -18), (200, -22), (230, -10), (230, 12), (200, 22), (40, 18), (0, 16)]
    m = c.mask(spline(H(handle), closed=True))
    lam = c.relief(m, radius=14, blur=1)
    c.engrave(m, np.clip(0.85 - 0.6 * lam, 0, 1), [c.ph_lin(H.angle(0)), c.ph_lin(H.angle(90))], spacing=6.5)
    c.outline(H.cp(handle), w=STRUCT, light=0.4)
    for x in (40, 52):
        c.stroke(H([(x, -18), (x, 18)]), 3, taper=0)

    # ============================================================ 癫狂层：插图爬出了书页
    mad(c, cx, cy, rng)


def creature(c, cx, cy, s, layer, rng, arms=None):
    """图版里的章鱼样生物：头 + 八条触须。"""
    head = [(cx - 60 * s, cy - 10 * s), (cx - 50 * s, cy - 90 * s), (cx, cy - 120 * s), (cx + 50 * s, cy - 90 * s),
            (cx + 60 * s, cy - 10 * s), (cx, cy + 20 * s)]
    m = c.mask(spline(head, closed=True))
    lam = c.relief(m, radius=30 * s, blur=1)
    c.engrave(m, np.clip(0.7 - 0.6 * lam, 0, 1), [c.ph_ring(cx, cy - 50 * s), c.ph_lin(30)], layer=layer, spacing=5.5, wmax=1.8,
              grain=0)
    c.outline(head, w=2.6, layer=layer, light=0.4)
    for sd in (-1, 1):
        c.add(c.ellipse(cx + sd * 22 * s, cy - 40 * s, 7 * s, 9 * s), layer)
    for k in range(8):
        a = math.radians(200 + k * 20)
        pts = [(cx + 40 * s * math.cos(a) * 0.5, cy + 10 * s)]
        for j in range(1, 6):
            rr = j * 32 * s
            pts.append((cx + rr * math.cos(a + 0.4 * math.sin(j + k)), cy + 10 * s - rr * math.sin(a + 0.4 * math.sin(j + k)) * 0.8))
        c.stroke(spline(pts), 7 * s, layer=layer, taper=(0.0, 0.9), w_end=1.0)


def mad(c, cx, cy, rng):
    # 几条触须从图版里伸出来，越过页边，爬到桌面和放大镜上
    targets = [(160, 830), (40, 470), (450, 860), (760, 840), (1120, 640), (600, 120), (1150, 260)]
    for k, (tx, ty) in enumerate(targets):
        start = np.array([cx, cy + 20])
        end = np.array([tx, ty])
        d = end - start
        L = np.hypot(*d)
        n = np.array([-d[1], d[0]]) / L
        pts = [tuple(start)]
        for j in range(1, 6):
            t = j / 5
            pts.append(tuple(start + d * t + n * 60 * math.sin(t * math.pi * 1.5 + k)))
        center = spline(pts)
        u = np.linspace(0, 1, len(center))
        prof = lambda uu: 1.0 - 0.85 * uu
        m = c.stroke(center, 48, add=False, taper=(0.0, 0.0), profile=prof)
        t = np.gradient(center, axis=0)
        t /= np.hypot(t[:, 0], t[:, 1])[:, None]
        nn = np.stack([-t[:, 1], t[:, 0]], 1)
        half = 24 * (1 - 0.85 * u)
        c.engrave(m, 0.5, [c.ph_lin(math.degrees(math.atan2(d[1], d[0])) + 90)], layer='mad', spacing=7, wmax=2.2, grain=0)
        for sd in (-1, 1):
            c.stroke(center + nn * half[:, None] * sd, 4.2, layer='mad', taper=(0.0, 0.2))
        # 吸盘：内侧一排小圈
        for i in range(8, len(center) - 10, 12):
            p = center[i] - nn[i] * half[i] * 0.45
            r = max(half[i] * 0.4, 2)
            c.stroke(ellipse_pts(p[0], p[1], r, r), 2.0, layer='mad', closed=True)
