"""倒影的共用画法（Boss 档，策划/15 §2.3、09 M7.7）。

一面落地的椭圆穿衣镜，木框雕着珠串，顶上一枚扇贝形的雕花，两条带爪的腿。镜子里站着的是你，但照出来的是你的背影
（调查员的图左右翻过来放进镜面）：浑身湿透，帽檐、衣摆、袖口往下滴水，脚下一滩水；镜框底下也在往外渗水。
信里说「见了镜子绕着走」。癫狂层：一只手从镜面里头按在玻璃上。
龟裂（第二阶段）：从胸口一点往外炸开的裂纹，裂缝发青（深渊遮罩），几块碎片错开，有两块已经掉了，露出后面的深渊。
"""
import math

import numpy as np

from etch import STRUCT, Canvas, ellipse_pts, smoothstep, spline
import actors.player as player

SIZE = (520, 940)
OUT_W = 12.0
FEET = 1805

CX, CY, RX, RY = 520, 860, 400, 700        # 镜面
FRAME = 64                                  # 框宽


def draw(c, cracked=False):
    rng = np.random.default_rng(23)
    side = c.grad(100, 100, 1000, 1800)
    glass = c.ellipse(CX, CY, RX, RY)
    outer = c.ellipse(CX, CY, RX + FRAME, RY + FRAME)
    ring = outer * (1 - glass)
    legs = [[(330, 1480), (380, 1500), (300, 1790), (230, 1800)], [(660, 1500), (710, 1480), (810, 1800), (740, 1790)]]
    crest = [(380, 190), (440, 110), (520, 70), (600, 110), (660, 190), (520, 170)]
    shadow = c.ellipse(CX, FEET + 2, 420, 26)
    puddle = c.ellipse(CX + 20, FEET - 6, 300, 30)
    leg_m = np.clip(sum(c.mask(spline(l, closed=True)) for l in legs), 0, 1)
    crest_m = c.mask(spline(crest, closed=True))
    c.add(np.clip(outer + leg_m + crest_m + shadow + puddle, 0, 1), 'fill')

    # ------------------------------------------------------------ 地上：影子 + 镜框底下渗出来的一滩水
    c.engrave(shadow, 0.85, [c.ph_lin(0, wobble=0.6), c.ph_lin(60)], spacing=7, swell=1.2)
    c.clear(puddle)
    c.engrave(puddle, 0.5, [c.ph_lin(0, wobble=0.4)], spacing=6, wmax=2.2)
    for r in (80, 150, 230):
        c.stroke(ellipse_pts(CX + 20, FEET - 6, r, r * 0.11), 2.4, closed=True)

    # ------------------------------------------------------------ 腿：两条带爪的木腿
    for l in legs:
        m = c.mask(spline(l, closed=True))
        lam = c.relief(m, radius=20, blur=1.5)
        c.engrave(m, np.clip(0.45 + 0.35 * side - 0.3 * lam, 0, 1), [c.ph_axis(l[0], l[3], half=30), c.ph_lin(30)], spacing=7,
                  wmax=2.6)
        c.outline(l, w=STRUCT + 1, light=0.4)
        fx, fy = l[3]
        for k in range(3):                                                  # 爪
            a = math.radians(160 + 25 * k) if fx < CX else math.radians(-5 - 25 * k)
            c.stroke([(fx + 20, fy - 6), (fx + 20 + 40 * math.cos(a), fy - 6 - 10 * math.sin(a) + 10)], 7, taper=(0.0, 0.8))

    # ------------------------------------------------------------ 镜面：横排线的暗玻璃 + 一道道往下淌的水痕
    gtone = np.clip(0.42 + 0.25 * side, 0, 1)
    c.engrave(glass, gtone, [c.ph_lin(2, wobble=0.5, seed=1), c.ph_lin(-62, seed=2)], spacing=8, swell=1.0)
    for _ in range(26):
        x = rng.uniform(CX - RX + 40, CX + RX - 40)
        y0 = rng.uniform(CY - RY + 80, CY + 200)
        L = rng.uniform(160, 520)
        c.erase(c.stroke(spline([(x, y0), (x + rng.uniform(-8, 8), y0 + L / 2), (x + rng.uniform(-14, 14), y0 + L)]),
                         rng.uniform(3, 6), add=False, taper=(0.2, 0.6)) * glass)

    # ------------------------------------------------------------ 镜子里：调查员的背影，左右翻过来，浑身湿透
    pc = Canvas(player.SIZE[0] * 2, player.SIZE[1] * 2, seed=7)
    player.draw(pc)
    pc.layers['ink'] *= np.clip(pc.layers['fill'] * 1.5, 0, 1)
    dy = CY + RY - 70 - player.FEET
    dx = CX - (pc.w - 420)
    reflect = {}
    for k, L in pc.layers.items():
        a = np.zeros((c.h, c.w), np.float32)
        src = L[:, ::-1]
        y0, x0 = max(dy, 0), max(dx, 0)
        h = min(c.h - y0, src.shape[0] - (y0 - dy))
        w = min(c.w - x0, src.shape[1] - (x0 - dx))
        a[y0:y0 + h, x0:x0 + w] = src[y0 - dy:y0 - dy + h, x0 - dx:x0 - dx + w]
        reflect[k] = a * glass
    body = np.clip(reflect['fill'] * 1.5, 0, 1)
    c.clear(body)
    c.add(reflect['ink'])
    c.add(reflect['abyss'], 'abyss')
    c.add(reflect['mad'], 'mad')
    # 滴水：从帽檐、袖口、衣摆的下沿往下滴
    fill = reflect['fill'] > 0.5
    for x in range(CX - 300, CX + 300, 26):
        col = np.nonzero(fill[:, x])[0]
        if len(col) == 0:
            continue
        # 每一段身子的下沿（填充从有到无的地方）都滴一点
        edges = col[np.nonzero(np.diff(col) > 4)[0]].tolist() + [col[-1]]
        for y in edges:
            if y > CY + RY - 90 or rng.random() < 0.35:
                continue
            L = rng.uniform(30, 110)
            c.stroke([(x, y + 2), (x + rng.uniform(-3, 3), y + L)], 3.2, taper=(0.0, 0.5))
            c.add(c.ellipse(x, y + L + 8, 5, 8))
    # 镜子里那盏提灯：火是青的（灯在镜子里也泡过水）
    lx, ly = dx + pc.w - 630, dy + 1060 - 100
    c.fill(c.ellipse(lx, ly, 20, 32) * glass, 'abyss', 0.9)
    pool = c.ellipse(CX, CY + RY - 66, 240, 22) * glass                     # 镜子里脚下的一滩水
    c.engrave(pool * (1 - body), 0.6, [c.ph_lin(0, wobble=0.3)], spacing=5, wmax=2.0)
    for r in (90, 170):
        c.stroke(ellipse_pts(CX, CY + RY - 66, r, r * 0.09), 2.2, closed=True)

    # 癫狂层：一只手从镜面里头按在玻璃上（掌心、五指摊开），在背影的右上
    hx, hy = CX + 190, CY - 260
    palm = ellipse_pts(hx, hy, 52, 62)
    c.stroke(palm, 5.0, layer='mad', closed=True)
    for k, (a, L) in enumerate(((-150, 90), (-118, 120), (-92, 130), (-66, 118), (-30, 80))):
        r = math.radians(a)
        x0, y0 = hx + 46 * math.cos(r), hy + 56 * math.sin(r)
        c.stroke([(x0, y0), (x0 + L * math.cos(r), y0 + L * math.sin(r))], 9.0, layer='mad', taper=(0.0, 0.25))

    # ------------------------------------------------------------ 龟裂：从胸口一点炸开
    if cracked:
        _crack(c, glass, rng)

    # ------------------------------------------------------------ 框：雕花木框，一圈珠串；顶上扇贝
    c.clear(ring)
    lam = c.relief(outer, radius=50, blur=2)
    c.engrave(ring, np.clip(0.4 + 0.35 * side + 0.25 * (1 - lam), 0, 1), [c.ph_ring(CX, CY, 1.0, RX / RY), c.ph_lin(40, seed=3)],
              spacing=7, swell=1.2)
    for k in range(64):
        a = 2 * math.pi * k / 64
        x, y = CX + (RX + FRAME / 2) * math.cos(a), CY + (RY + FRAME / 2) * math.sin(a)
        c.clear(c.ellipse(x, y, 11, 11))
        c.stroke(ellipse_pts(x, y, 11, 11), 2.6, closed=True)
        c.add(c.ellipse(x + 3, y + 3, 5, 5) * 0.8)
    c.stroke(ellipse_pts(CX, CY, RX + FRAME, RY + FRAME), OUT_W, closed=True, light=0.45)
    c.stroke(ellipse_pts(CX, CY, RX, RY), STRUCT, closed=True)
    c.clear(crest_m)
    c.engrave(crest_m, 0.45, [c.ph_ray(520, 180, r0=20)], spacing=7, wmax=2.4)
    for x, y in crest[:5]:
        c.stroke([(520, 175), (x, y)], 3.4, taper=(0.0, 0.5))
    c.outline(crest, w=STRUCT + 1, light=0.4)


def _crack(c, glass, rng):
    ix, iy = CX + 30, CY + 40
    rays = []
    for k in range(12):
        a = 2 * math.pi * k / 12 + rng.uniform(-0.2, 0.2)
        pts = [(ix, iy)]
        r = 0
        while True:
            r += rng.uniform(50, 90)
            aa = a + rng.uniform(-0.18, 0.18)
            x, y = ix + r * math.cos(aa), iy + r * 1.4 * math.sin(aa)
            if ((x - CX) / RX) ** 2 + ((y - CY) / RY) ** 2 > 1:
                break
            pts.append((x, y))
        rays.append((a, pts))
    # 错开的碎片：相邻两条裂纹之间的扇形，整块挪几个像素
    for k in (1, 4, 8):
        a0, a1 = rays[k][0], rays[(k + 1) % 12][0]
        wedge = c.mask(np.array([(ix, iy), (ix + 2000 * math.cos(a0), iy + 2800 * math.sin(a0)),
                                 (ix + 2000 * math.cos((a0 + a1) / 2), iy + 2800 * math.sin((a0 + a1) / 2)),
                                 (ix + 2000 * math.cos(a1), iy + 2800 * math.sin(a1))])) * glass
        wedge *= smoothstep(60, 120, np.hypot(c.xx - ix, c.yy - iy))
        dx, dy = int(rng.integers(-14, 14)), int(rng.integers(6, 16))
        for name in ('ink', 'abyss', 'mad'):
            L = c.layers[name]
            c.layers[name] = L * (1 - wedge) + np.roll(L, (dy, dx), axis=(0, 1)) * wedge
    # 裂纹：外面一道青光（深渊遮罩），里面一道墨
    for a, pts in rays:
        line = np.array(pts, float)                                         # 裂纹是直的折线，不能平滑
        c.stroke(line, 12.0, layer='abyss', taper=(0.0, 0.6), step=1.0)
        c.stroke(line, 4.0, taper=(0.0, 0.6), step=1.0)
    for r, a0, a1 in ((120, 20, 160), (120, 200, 330), (260, 60, 120), (260, 230, 300), (380, 150, 200)):
        arc = ellipse_pts(ix, iy, r, r * 1.4, a0=a0, a1=a1, n=6)                # 一段一段的直线，像玻璃的环形裂
        c.stroke(arc, 8.0, layer='abyss', taper=0.2)
        c.stroke(arc, 3.0, taper=0.2)
    # 掉了的两块：露出后面的深渊
    for tri in ([(ix - 20, iy - 30), (ix - 90, iy - 80), (ix - 36, iy - 110)], [(ix + 40, iy + 20), (ix + 110, iy + 50), (ix + 70, iy + 110)]):
        m = c.mask(np.array(tri)) * glass
        c.clear(m)
        c.fill(m, 'abyss', 0.9)
        c.stroke(np.array(tri + tri[:1]), 4.0, taper=0)
    c.add(c.ellipse(ix, iy, 16, 16))
