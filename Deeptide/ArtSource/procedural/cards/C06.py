"""C06 梦魇之刃：桌上一把普通的切肉刀。癫狂层：刀影长出一排牙齿（策划/10 §4.6）。"""
import math

import numpy as np

from etch import (OUTLINE, STRUCT, ellipse_pts, offset, poly, resample, smoothstep, spline, transform)

NAME = '梦魇之刃'

ROT = -13          # 刀尖朝右上
CX, CY = 480, 505  # 刀枕（刀柄和刀身的交界）
S = 0.95
SHADOW = (34, 54)  # 烛光在左上，影子落向右下


def K(pts):
    """刀的局部坐标（x 沿刀身，0 = 刀枕；y 向下 = 刃口一侧）→ 画布。"""
    return transform(np.asarray(pts, float) * S, dx=CX, dy=CY, rot=ROT)


def KP(pts):
    """控制点（可带 'c' 尖角标记）→ 画布，保留标记。"""
    xy = K([p[:2] for p in pts])
    return [tuple(q) + tuple(p[2:]) for q, p in zip(xy, pts)]


def draw(c):
    # ------------------------------------------------------------ 刀的形状（局部坐标）
    blade_pts = [(14, -34, 'c'), (260, -36), (470, -31), (575, -22), (648, -10, 'c'),
                 (600, 14), (520, 36), (380, 52), (200, 58), (60, 60), (14, 60, 'c')]
    handle_pts = [(-8, -30, 'c'), (-120, -31), (-250, -30), (-318, -34), (-338, -18), (-340, 6),
                  (-330, 30), (-300, 36), (-200, 31), (-90, 30), (-8, 30, 'c')]
    bolster_pts = [(-10, -36, 'c'), (18, -38, 'c'), (18, 62, 'c'), (-10, 34, 'c')]

    blade = c.mask(K(spline(blade_pts, closed=True)))
    handle = c.mask(K(spline(handle_pts, closed=True)))
    bolster = c.mask(K(poly(bolster_pts)))
    knife = np.clip(blade + handle + bolster, 0, 1)

    # ------------------------------------------------------------ 桌面：木板 + 烛光圈
    table_top = 150
    far = c.rect(0, 0, c.w, table_top + 2)
    table = 1 - far
    light = c.radial(600, 480, 170, 720, sx=1.3, sy=1.0)           # 0 = 光圈中心
    tone = 0.05 + 0.62 * smoothstep(0, 1, light) ** 1.3
    # 木纹：大致水平的波浪线，绕开几个树节
    grain = c.ph_wave(-3, 7, 340, wobble=2.0, seed=3)
    for kx, ky, ks in ((230, 300, 26), (930, 640, 30), (1080, 250, 20), (420, 760, 22)):
        r2 = ((c.xx - kx) ** 2 / 2.6 + (c.yy - ky) ** 2) / (ks * ks)
        grain = grain + ks * 0.9 * np.exp(-r2) * np.sign(c.yy - ky)
    seams = [table_top, 300, 470, 655, 864]
    plank_cross = c.ph_lin(64, wobble=1.0, seed=4)
    c.engrave(table, tone, [grain, plank_cross, c.ph_lin(-30, seed=5)], spacing=11,
              bands=[(0.03, 0.5), (0.42, 0.75), (0.7, 0.95)])
    # 木板缝：一条墨线，下侧一窄条阴影
    for y in seams[1:-1]:
        pts = [(-10, y + 3), (300, y - 2), (700, y + 4), (1000, y), (1260, y - 3)]
        c.line(pts, w=4.5, taper=0.0)
        c.stroke(offset(spline(pts), -6), 2.2, taper=0.0)
    # 远处桌沿和桌外的黑暗
    c.engrave(far, 0.5 + 0.2 * c.grad(624, 150, 624, 0), [c.ph_lin(80, seed=6), c.ph_lin(-10, seed=7)])
    c.line([(-10, table_top + 4), (400, table_top), (900, table_top + 3), (1260, table_top)], w=OUTLINE, taper=0.0)
    c.stroke(offset(spline([(-10, table_top + 4), (400, table_top), (900, table_top + 3), (1260, table_top)]), 10), 3, taper=0.0)

    # ------------------------------------------------------------ 刀影
    shadow = np.clip(c.shadow_of(knife, *SHADOW, blur=2.5) - knife, 0, 1) * table
    c.engrave(shadow, 0.86, [c.ph_lin(ROT, wobble=1.6, seed=9), c.ph_lin(ROT + 55, wobble=1.6, seed=10)],
              solid=None, wmax=3.4, swell=1.0)

    # ------------------------------------------------------------ 刀柄：深色硬木，三颗铆钉
    hp = K([(-340, 0), (0, 0)])
    lam = c.relief(handle, radius=22)
    htone = 0.9 - 0.55 * lam
    c.engrave(handle, htone, [c.ph_along(hp[0], hp[1], wobble=1.5, seed=13), c.ph_axis(hp[0], hp[1], bow=10, half=24, seed=14)],
              spacing=8, swell=1.1, solid=0.97)
    # 木纹高光：几条细白线
    for v in (-14, -9):
        c.erase(c.stroke(K(spline([(-300, v), (-200, v - 1), (-100, v + 1), (-20, v)])), 2.0, add=False, taper=0.3))
    for rx in (-262, -170, -78):
        cx, cy = K([(rx, 0)])[0]
        r = 9.5 * S
        rivet = c.ellipse(cx, cy, r, r)
        c.fill(rivet, value=0.0)
        c.engrave(rivet * c.half_plane((cx + 10, cy - 10), (cx - 10, cy + 10)), 0.5,
                  [c.ph_lin(-45, wobble=0)], spacing=5, wmax=2)
        c.stroke(ellipse_pts(cx, cy, r, r), 3.2, closed=True, light=0.5)

    # ------------------------------------------------------------ 刀枕：钢，竖排线
    bp = K([(4, -36), (4, 60)])
    c.engrave(bolster, 0.35 + 0.3 * c.grad(*bp[0], *bp[1]), [c.ph_along(bp[0], bp[1], wobble=0.3)], spacing=7, wmax=2.4)

    # ------------------------------------------------------------ 刀身：钢的反光带
    # v = 0 刀背，1 刃口；刃口一侧有一条开刃线
    spine = K(spline([(14, -34), (260, -36), (470, -31), (575, -22), (648, -10)]))
    p0, p1 = K([(0, 0), (600, 0)])
    ux, uy = (p1 - p0) / np.hypot(*(p1 - p0))
    across = -(c.xx - p0[0]) * uy + (c.yy - p0[1]) * ux        # 垂直刀身，向刃口为正
    along = (c.xx - p0[0]) * ux + (c.yy - p0[1]) * uy
    loc = across / S
    v = np.clip((loc + 34) / 94, 0, 1)
    tip = smoothstep(380, 640, along / S)
    band = 0.62 * np.exp(-((v - 0.46) / 0.17) ** 2)             # 中间一条暗的反光带
    btone = 0.08 + band * (1 - 0.6 * tip) + 0.12 * smoothstep(0.0, 0.08, 0.08 - v)
    bevel_line = [(30, 40), (200, 40), (380, 35), (500, 24), (590, 6), (636, -6)]
    bevel = c.mask(K(spline([(14, 60, 'c')] + bevel_line[::-1][:-1] + [(30, 40), (14, 40, 'c')], closed=True)))
    btone = np.where(bevel > 0.5, 0.05 + 0.12 * smoothstep(0.85, 1.0, v), btone)
    c.engrave(blade, btone, [c.ph_along(p0, p1, wobble=0.5, seed=16), c.ph_lin(ROT + 8, wobble=0.5, seed=17)],
              spacing=8, wmax=2.6)
    c.line(K(bevel_line), w=2.6, taper=0.25)
    # 刀背一道亮边、刀尖一点高光
    c.erase(c.stroke(offset(spine, 7), 3.0, add=False, taper=0.3))
    gx, gy = K([(560, -8)])[0]
    c.erase(c.stroke(spline([(gx - 40, gy + 6), (gx, gy), (gx + 34, gy - 6)]), 4, add=False, taper=0.5))

    # ------------------------------------------------------------ 轮廓
    c.outline(KP(blade_pts), w=OUTLINE * 0.85, light=0.5)
    c.outline(KP(handle_pts), w=OUTLINE, light=0.5)
    c.stroke(K(poly(bolster_pts + bolster_pts[:1])), STRUCT, taper=0.0)

    # 桌上几点碎屑，给画面一点生活气
    for x, y, r in ((300, 690, 5), (322, 700, 3), (860, 300, 4), (1010, 610, 3.5), (980, 625, 2.5), (180, 560, 3)):

        c.add(c.ellipse(x, y, r * 1.4, r, rot=20))

    # ============================================================ 癫狂层：刀影长出一排牙齿
    mad(c, knife, shadow)


def mad(c, knife, shadow):
    # 影子的下沿 = 刃口线平移
    edge_local = [(20, 60), (60, 60), (200, 58), (380, 52), (520, 36), (600, 14), (648, -10)]
    handle_low = [(-330, 30), (-300, 36), (-200, 31), (-90, 30), (-8, 30)]
    edge = resample(K(spline(handle_low + edge_local)) + np.array(SHADOW), 2)
    s = np.concatenate([[0], np.cumsum(np.hypot(*np.diff(edge, axis=0).T))])
    total = s[-1]
    rng = np.random.default_rng(6)

    # 影子在癫狂面里撑开成一张嘴：先把影子下沿往外涨一圈「牙龈」
    t = np.gradient(edge, axis=0)
    t /= np.hypot(t[:, 0], t[:, 1])[:, None]
    nrm = np.stack([-t[:, 1], t[:, 0]], 1)                     # 指向刀外（右下）
    gum_w = 16 + 8 * np.sin(s / 37)
    gum = np.vstack([edge - nrm * 6, (edge + nrm * gum_w[:, None])[::-1]])
    c.fill(c.mask(gum) * (1 - knife), layer='mad')

    teeth = []
    pos = 30.0
    while pos < total - 20:
        u = pos / total
        size = 30 + 52 * math.sin(math.pi * min(max((u - 0.05) / 0.9, 0), 1)) ** 0.7
        size *= rng.uniform(0.75, 1.15)
        teeth.append((pos, size))
        pos += size * rng.uniform(0.62, 0.8)

    for pos, size in teeth:
        i = int(np.searchsorted(s, pos))
        i = min(max(i, 1), len(edge) - 2)
        p = edge[i] + nrm[i] * (gum_w[i] - 4)
        n, tg = nrm[i], t[i]
        bend = rng.uniform(-0.35, 0.35)                          # 牙尖稍微歪一点，不要像锯齿
        half = size * 0.36
        tipp = p + n * size * 1.15 + tg * size * bend
        mid = p + n * size * 0.55
        left = p - tg * half
        right = p + tg * half
        tooth = [tuple(left), tuple(mid - tg * half * 0.72 + tg * size * bend * 0.3),
                 (tipp[0], tipp[1], 'c'), tuple(mid + tg * half * 0.72 + tg * size * bend * 0.3), tuple(right)]
        tooth_pts = spline(tooth, closed=True)
        m = c.mask(tooth_pts)
        # 牙齿背光的一侧（右下）排线
        lam = c.relief(m, radius=size * 0.25, blur=1.0)
        c.engrave(m, np.clip(0.75 - lam, 0, 1) * 0.9, [c.ph_along(tuple(p), tuple(tipp), wobble=0.3), c.ph_lin(30)],
                  layer='mad', spacing=7, wmax=2.2, grain=0)
        c.stroke(tooth_pts, 3.6, layer='mad', closed=True, light=0.5)

    # 几道口水，从牙尖垂下来
    for pos in (0.35, 0.58, 0.8):
        i = int(np.searchsorted(s, pos * total))
        p = edge[i] + nrm[i] * 60
        drip = [(p[0], p[1]), (p[0] + 2, p[1] + 26), (p[0] - 1, p[1] + 48)]
        c.line(drip, w=3.4, layer='mad', taper=(0.0, 0.0))
        c.add(c.ellipse(p[0] - 1, p[1] + 52, 5, 6.5), 'mad')
