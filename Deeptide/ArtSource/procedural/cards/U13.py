"""U13 凝视深渊：俯视一口井。癫狂层：井底有眼睛回望（策划/10 §4.6）。

从井口往下看：最外一圈是石砌的井沿，里面一圈圈石砖收进黑暗，井底一小片水面映着天光（亮的）。
癫狂层：水面上睁开一只大眼，往上看着你；井壁的石缝里也睁开几只小的。
井底的水留成亮的，癫狂面里那只眼才看得清（10 §11.2：癫狂层压在暗处会被吃掉）。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, ellipse_pts, smoothstep, spline

NAME = '凝视深渊'

CX, CY = 640, 450
BOTTOM = (650, 470, 92)           # 井底水面：中心 x, y, 半径


def ring(t):
    """井壁上的第 t 圈（0 = 井口，1 = 井底）：中心、横半径、竖半径。透视：越深越小、越往下偏一点。"""
    bx, by, br = BOTTOM
    k = (1 - t) ** 1.8
    rx = br + (430 - br) * k
    return CX + (bx - CX) * t, CY + (by - CY) * t, rx, rx * 0.78


def draw(c):
    rng = np.random.default_rng(13)
    bx, by, br = BOTTOM

    # ------------------------------------------------------------ 井外：井口外面的地面（四角）
    out = 1 - c.ellipse(*ring(0)[:2], ring(0)[2] + 60, ring(0)[3] + 60)
    c.engrave(out, 0.55 + 0.05 * c.noise(40, 1), [c.ph_lin(30, seed=2), c.ph_lin(-40, seed=3)], spacing=9)
    for _ in range(30):                                                    # 草 / 碎石
        x, y = rng.uniform(0, c.w), rng.uniform(0, c.h)
        if out[int(y), int(x)] > 0.5:
            c.stroke([(x, y), (x + rng.uniform(-8, 8), y - rng.uniform(14, 30))], 2.2, taper=(0, 0.8))

    # ------------------------------------------------------------ 井沿：一圈大石块，顶面亮
    x0, y0, rx0, ry0 = ring(0)
    lip = c.ellipse(x0, y0, rx0 + 60, ry0 + 60) * (1 - c.ellipse(x0, y0, rx0, ry0))
    ang = np.arctan2((c.yy - y0) / 0.78, c.xx - x0)
    c.engrave(lip, 0.12 + 0.25 * smoothstep(-0.5, 1, np.sin(ang + 0.8)), [c.ph_ring(x0, y0, sy=0.78), c.ph_lin(60)], spacing=8)
    for a in np.arange(0, 360, 24) + rng.uniform(-4, 4):                   # 石块的缝
        t = math.radians(a)
        c.stroke([(x0 + rx0 * math.cos(t), y0 + ry0 * math.sin(t)), (x0 + (rx0 + 60) * math.cos(t), y0 + (ry0 + 60) * math.sin(t))],
                 3.2, taper=0)
    c.stroke(ellipse_pts(x0, y0, rx0 + 60, ry0 + 60), OUTLINE, closed=True, light=0.3)

    # ------------------------------------------------------------ 井壁：一圈圈石砖，越深越黑；井底亮
    shaft = c.ellipse(x0, y0, rx0, ry0)
    # 把每个像素映射到深度 t：在若干圈之间插值
    ts = np.linspace(0, 1, 60)
    depth = np.zeros((c.h, c.w), np.float32)
    for t in ts:
        cx, cy, rx, ry = ring(t)
        depth = np.maximum(depth, c.ellipse(cx, cy, rx, ry) * t)
    lit = smoothstep(-0.6, 1, np.sin(ang + 2.4))                          # 光从左上照进来：右下的井壁亮
    tone = np.clip(0.5 + 0.5 * smoothstep(0.05, 0.8, depth) - 0.25 * lit * (1 - depth), 0, 1)
    c.engrave(shaft, tone, [c.ph_ray(bx, by, r0=200), depth * 900 + 2 * c.noise(30, 4), c.ph_lin(30, seed=5)],
              spacing=8, swell=1.2)
    # 砖缝：一圈圈椭圆 + 错开的竖缝
    for k, t in enumerate(np.linspace(0.05, 0.75, 12) ** 1.1):
        cx, cy, rx, ry = ring(t)
        w = 3.6 * (1 - t) + 1.0
        c.stroke(ellipse_pts(cx, cy, rx, ry), w, closed=True, taper=0)
        cx2, cy2, rx2, ry2 = ring(t + 0.06)
        for a in np.arange(0, 360, 20 + 6 * t) + (k % 2) * 10:
            q = math.radians(a)
            c.stroke([(cx + rx * math.cos(q), cy + ry * math.sin(q)), (cx2 + rx2 * math.cos(q), cy2 + ry2 * math.sin(q))],
                     w * 0.7, taper=0)

    # ------------------------------------------------------------ 井底的水：亮，一道倒映的天光
    water = c.ellipse(bx, by, br, br * 0.78)
    c.clear(water)
    c.engrave(water, 0.12 * c.grad(bx - br, 0, bx + br, 0), [c.ph_lin(0, wobble=0.5)], spacing=7, wmax=1.8)
    c.stroke(ellipse_pts(bx, by, br, br * 0.78), 3.0, closed=True)
    c.stroke([(bx - 50, by - 10), (bx + 40, by - 18)], 2.0, taper=0.5)
    c.stroke([(bx - 30, by + 12), (bx + 55, by + 6)], 1.6, taper=0.5)

    # ============================================================ 癫狂层：井底的眼回望
    mad(c, rng)


def mad(c, rng):
    bx, by, br = BOTTOM
    W, H = br * 0.85, br * 0.42
    upper = spline([(bx - W, by, 'c'), (bx - W * 0.4, by - H), (bx + W * 0.4, by - H), (bx + W, by, 'c')])
    lower = spline([(bx + W, by, 'c'), (bx + W * 0.4, by + H * 0.85), (bx - W * 0.4, by + H * 0.85), (bx - W, by, 'c')])
    eye = c.mask(np.vstack([upper, lower]))
    ir = H * 0.9
    r = np.hypot(c.xx - bx, c.yy - by)
    c.add(c.ellipse(bx, by, ir, ir) * eye * c.lines(np.arctan2(c.yy - by, c.xx - bx) * 18, 4.5, 1.8) * smoothstep(9, 13, r), 'mad')
    c.add(c.ellipse(bx, by, ir * 0.38, ir * 0.85) * eye, 'mad')                  # 竖瞳
    c.add(c.stroke(ellipse_pts(bx, by, ir, ir), 2.4, closed=True, add=False) * eye, 'mad')
    c.stroke(upper, 5, layer='mad', taper=0.1)
    c.stroke(lower, 3, layer='mad', taper=0.1)
    # 井口的石沿上（亮处）也睁开几只小眼，围着井口看下去
    x0, y0, rx0, ry0 = ring(0)
    for a in (200, 250, 300, 340, 30, 140):
        q = math.radians(a + rng.uniform(-6, 6))
        x, y = x0 + (rx0 + 30) * math.cos(q), y0 + (ry0 + 30) * math.sin(q)
        w, h = 22, 10
        up = spline([(x - w, y, 'c'), (x, y - h), (x + w, y, 'c')])
        lo = spline([(x + w, y, 'c'), (x, y + h * 0.8), (x - w, y, 'c')])
        c.stroke(up, 3.2, layer='mad', taper=0.1)
        c.stroke(lo, 2.2, layer='mad', taper=0.1)
        c.add(c.ellipse(x, y, 5.5, 6), 'mad')
