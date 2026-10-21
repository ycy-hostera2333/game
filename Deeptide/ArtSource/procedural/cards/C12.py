"""C12 影中之物：煤气灯下被拉长的人影。癫狂层：影子里伸出触须和眼（策划/10 §4.6）。

夜里的石板街，左边一根煤气灯柱；一个小小的人站在灯下（画面左中），他的影子被灯拉得很长，斜斜铺过整条街到右下角。
石板路是浅的，这样癫狂层里从影子边上伸出的触须和睁开的眼在癫狂面里看得清。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, resample, smoothstep, spline

NAME = '影中之物'

LAMP = (240, 250)          # 灯头
FEET = (420, 640)          # 人站的位置


def shadow_shape():
    """被拉长的影子：从脚下向右下铺开的人形（两腿、风衣、垂着的手臂、头和帽子），越远越宽。"""
    fx, fy = FEET
    S = Frame((fx, fy), rot=10)                     # 局部：x 沿影子向右下，y 横向
    pts = [(0, -8), (170, -14), (172, -44, 'c'), (330, -40), (430, -50), (468, -62), (490, -48), (500, -20), (515, -18),
           (520, -30), (545, -32), (568, -28), (575, -64), (590, -64, 'c'), (592, -36), (640, -34), (646, 0), (640, 34),
           (592, 36), (590, 64, 'c'), (575, 64), (568, 28), (545, 32), (520, 30), (515, 18), (500, 20), (490, 48), (468, 62),
           (430, 50), (330, 40), (172, 44, 'c'), (170, 14), (0, 8)]
    return S, pts


def draw(c):
    rng = np.random.default_rng(9)
    lx, ly = LAMP
    fx, fy = FEET

    # ------------------------------------------------------------ 夜空 + 远处的房子剪影
    sky = c.rect(0, 0, c.w, 330)
    c.engrave(sky, 0.55 + 0.05 * c.noise(100, 1), [c.ph_lin(2, seed=2), c.ph_lin(70, seed=3)], spacing=9)
    houses = [(-10, 330), (-10, 200), (120, 200), (120, 150), (200, 150), (200, 210), (520, 210), (540, 120), (600, 80),
              (660, 120), (680, 210), (900, 210), (900, 160), (1000, 160), (1000, 230), (1260, 230), (1260, 330)]
    hm = c.mask(poly(houses))
    c.engrave(hm, 0.86, [c.ph_lin(90, seed=20), c.ph_lin(10, seed=21), c.ph_lin(-45, seed=22)], spacing=8, swell=1.2, solid=None)
    c.stroke(poly(houses), 3, taper=0)
    for wx, wy in ((560, 240), (760, 260), (1060, 270), (300, 250)):          # 几扇亮着的窗
        c.erase(c.rect(wx, wy, wx + 26, wy + 36))
        c.stroke(poly([(wx + 13, wy), (wx + 13, wy + 36)]), 3, taper=0)

    # ------------------------------------------------------------ 石板街：灯下亮，远处暗，透视的石缝
    street = c.mask(poly([(-10, 330), (1260, 330), (1260, 870), (-10, 870)]))
    light = smoothstep(0, 1, c.radial(fx - 60, fy, 120, 760, sx=1.5))
    c.engrave(street, 0.06 + 0.55 * light, [c.ph_lin(-4, wobble=1.5, seed=4), c.ph_lin(62, seed=5)], spacing=10.5,
              bands=[(0.04, 0.45), (0.4, 0.8)])
    # 石缝：横缝随透视变密，竖缝错开
    rows = [330 + 540 * (k / 9) ** 1.6 for k in range(1, 10)]
    for k, y in enumerate(rows):
        c.stroke(poly([(-10, y), (1260, y + 4)]), 1.6 + 1.6 * k / 9, taper=0)
        prev = rows[k - 1] if k else 330
        step = 60 + 18 * k
        off = (k % 2) * step / 2
        for x in np.arange(-10 + off, 1260, step):
            c.stroke(poly([(x, prev + 3), (x + (x - 624) * 0.04, y - 2)]), 1.2 + 1.2 * k / 9, taper=0)

    # ------------------------------------------------------------ 影子
    S, pts = shadow_shape()
    sh = c.mask(spline(S(pts), closed=True))
    c.engrave(sh, 0.9, [c.ph_lin(17, wobble=1.0, seed=6), c.ph_lin(-50, seed=7)], spacing=8, swell=1.3, solid=None)
    c.outline(S.cp(pts), w=3.5, light=0)
    c.erase(c.stroke(S([(20, 0), (165, 0)]), 3.0, add=False, taper=(0.6, 0.0)))   # 两条腿之间的缝

    # ------------------------------------------------------------ 煤气灯
    gy = 600
    c.fill(c.rect(lx - 12, ly + 50, lx + 12, gy), value=0.92)
    c.stroke(poly([(lx - 12, ly + 50), (lx - 12, gy)]), 3, taper=0)
    c.stroke(poly([(lx + 12, ly + 50), (lx + 12, gy)]), 5, taper=0)
    base = [(lx - 40, gy), (lx - 26, gy - 60), (lx + 26, gy - 60), (lx + 40, gy)]
    c.fill(c.mask(poly(base)), value=0.9)
    c.stroke(poly(base), STRUCT, taper=0)
    c.stroke(poly([(lx - 30, ly + 70), (lx + 30, ly + 70)]), 6, taper=0)       # 横档
    head = [(lx - 50, ly - 30), (lx + 50, ly - 30), (lx + 30, ly + 50), (lx - 30, ly + 50)]
    hm = c.mask(poly(head))
    c.clear(hm)
    c.stroke(poly(head + head[:1]), STRUCT, taper=0)
    c.stroke(poly([(lx, ly - 30), (lx, ly + 50)]), 2.4, taper=0)
    cap = [(lx - 62, ly - 30), (lx, ly - 70), (lx + 62, ly - 30)]
    c.fill(c.mask(poly(cap)), value=0.95)
    c.stroke(poly(cap + cap[:1]), STRUCT, taper=0)
    for a in np.arange(0, 360, 12):                                          # 光芒（只在天上画）
        ang = math.radians(a)
        p0 = (lx + 80 * math.cos(ang), ly + 10 + 80 * math.sin(ang))
        p1 = (lx + 170 * math.cos(ang), ly + 10 + 170 * math.sin(ang))
        c.erase(c.stroke(poly([p0, p1]), 3.0, add=False, taper=(0.2, 0.9)) * (1 - street))
    c.erase(np.clip(1 - c.radial(lx, ly + 10, 50, 110), 0, 1) * (1 - hm) * (1 - street))

    # ------------------------------------------------------------ 人：小小的背影，戴帽子，站在影子的根上
    P = Frame((fx, fy), scale=0.62)
    body = [(-34, 0), (-40, -120), (-50, -200), (-30, -240), (30, -240), (50, -200), (40, -120), (34, 0)]
    c.fill(c.mask(spline(P(body), closed=True)), value=0.95)
    c.fill(c.ellipse(*P.p(0, -272), 24 * 0.62, 28 * 0.62), value=0.95)
    c.fill(c.ellipse(*P.p(0, -292), 52 * 0.62, 10 * 0.62), value=1.0)
    c.fill(c.mask(P([(-24, -292), (-20, -340), (20, -340), (24, -292)])), value=1.0)
    c.outline(P.cp(body), w=STRUCT, light=0.3)

    # ============================================================ 癫狂层：影子里伸出触须和眼
    mad(c, S, pts, sh, rng)


def mad(c, S, pts, sh, rng):
    outline = resample(spline(S(pts), closed=True), 3)
    n = len(outline)
    t = np.gradient(outline, axis=0)
    t /= np.hypot(t[:, 0], t[:, 1])[:, None]
    nrm = np.stack([t[:, 1], -t[:, 0]], 1)                                  # 指向影子外面
    # 触须：从影子边上伸到亮的石板上，卷起来
    for k, i in enumerate(np.linspace(int(n * 0.16), int(n * 0.84), 8).astype(int)):
        if 0.4 < i / n < 0.6 or i / n < 0.15 or i / n > 0.85:              # 头部那端留给眼睛，脚下那端不长
            continue
        p = outline[i]
        d = nrm[i]
        q = p + d * 20
        if sh[int(np.clip(q[1], 0, c.h - 1)), int(np.clip(q[0], 0, c.w - 1))] > 0.5:
            d = -d
        side = np.array([-d[1], d[0]])
        L = rng.uniform(110, 190)
        curl = rng.choice([-1, 1])
        ctrl = [tuple(p - d * 10)]
        for j in range(1, 6):
            tt = j / 5
            ctrl.append(tuple(p + d * L * tt + side * curl * L * 0.35 * math.sin(tt * math.pi * 1.2)))
        # 末端卷一个小圈
        endp = np.array(ctrl[-1])
        for j in range(1, 6):
            a = j / 5 * math.pi * 1.4
            ctrl.append(tuple(endp + (side * curl * math.cos(a) + d * math.sin(a)) * 26 * (1 - j / 7)))
        center = spline(ctrl)
        u = np.linspace(0, 1, len(center))
        m = c.stroke(center, 30, layer='mad', add=False, profile=lambda uu: 1 - 0.9 * uu, taper=0)
        tt = np.gradient(center, axis=0)
        tt /= np.hypot(tt[:, 0], tt[:, 1])[:, None]
        nn = np.stack([-tt[:, 1], tt[:, 0]], 1)
        half = 15 * (1 - 0.9 * u)
        c.engrave(m * (1 - sh), 0.45, [c.ph_lin(rng.uniform(0, 180))], layer='mad', spacing=6.5, wmax=2, grain=0)
        for sd in (-1, 1):
            c.stroke(center + nn * half[:, None] * sd, 3.0, layer='mad', taper=(0.0, 0.3))
        for q in range(6, len(center) - 12, 14):
            pp = center[q] - nn[q] * half[q] * 0.4
            r = max(half[q] * 0.38, 2)
            c.stroke(ellipse_pts(pp[0], pp[1], r, r), 1.8, layer='mad', closed=True)
    # 眼：影子的头上、肩上沿着边睁开几只
    for (lx, ly, sz) in ((690, -60, 26), (700, 70, 30), (560, -110, 24), (540, 112, 26), (380, -82, 20)):
        x, y = S.p(lx, ly)
        eye(c, x, y, sz, rng)


def eye(c, x, y, w, rng):
    h = w * 0.55
    upper = spline([(x - w, y, 'c'), (x - w * 0.4, y - h), (x + w * 0.4, y - h), (x + w, y, 'c')])
    lower = spline([(x + w, y, 'c'), (x + w * 0.4, y + h * 0.8), (x - w * 0.4, y + h * 0.8), (x - w, y, 'c')])
    m = c.mask(np.vstack([upper, lower]))
    ir = h * 0.8
    lx = x + rng.uniform(-0.3, 0.3) * w * 0.4
    c.add(c.ellipse(lx, y, ir, ir) * m * c.lines(np.arctan2(c.yy - y, c.xx - lx) * 14, 4.5, 1.6), 'mad')
    c.add(c.ellipse(lx, y, ir * 0.42, ir * 0.42) * m, 'mad')
    c.add(c.stroke(ellipse_pts(lx, y, ir, ir), 2.0, closed=True, add=False) * m, 'mad')
    c.stroke(upper, 4.2, layer='mad', taper=0.1)
    c.stroke(lower, 2.6, layer='mad', taper=0.1)
