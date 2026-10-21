"""C03 银质匕首：银匕首特写，刃上映出一张人脸（策划/10 §4.6）。

匕首竖着，刀尖正好顶进尖拱窗的拱顶。刃是菱形截面，中脊从护手直到刀尖；
刃面上映出调查员的脸——中脊恰好是鼻梁，脸被刃面拉长了一点。背景是深色天鹅绒，衬出银的亮。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, smoothstep, spline

NAME = '银质匕首'

CX = 624
TIP_Y, GUARD_Y = 40, 610
BASE_HALF = 112


def blade_half(y):
    """y 处刃的半宽：下段略收，上面一半收成尖拱一样的弧，和拱窗呼应。"""
    t = np.clip((GUARD_Y - y) / (GUARD_Y - TIP_Y), 0, 1)
    return BASE_HALF * (1 - 0.12 * t) * np.clip((1 - t) / 0.46, 0, 1) ** 0.62


def draw(c):
    # ------------------------------------------------------------ 背景：深色天鹅绒，左上一点光
    bg = np.ones((c.h, c.w), np.float32)
    c.engrave(bg, 0.62 + 0.3 * c.grad(200, 100, 1100, 800) + 0.05 * c.noise(100, 1),
              [c.ph_lin(-35, wobble=1.5, seed=2), c.ph_lin(55, wobble=1.5, seed=3), c.ph_lin(5, seed=4)], spacing=9, swell=1.1,
              solid=None)
    # 刀身周围一圈光晕（版画里用放射短线）
    for a in np.arange(0, 360, 9):
        r0, r1 = 150, 230 + 40 * math.sin(a * 3.1)
        ang = math.radians(a)
        p0 = (CX + r0 * math.cos(ang) * 0.8, 220 + r0 * math.sin(ang) * 1.3)
        p1 = (CX + r1 * math.cos(ang) * 0.8, 220 + r1 * math.sin(ang) * 1.3)
        c.erase(c.stroke(poly([p0, p1]), 3.0, add=False, taper=(0.1, 0.9)))

    # ------------------------------------------------------------ 刃
    ys = np.linspace(TIP_Y, GUARD_Y, 200)
    hw = blade_half(ys)
    left = np.stack([CX - hw, ys], 1)
    right = np.stack([CX + hw, ys], 1)
    blade_pts = np.vstack([left[::-1], right])
    blade = c.mask(blade_pts)
    u = (c.xx - CX) / np.maximum(blade_half(np.clip(c.yy, TIP_Y, GUARD_Y)), 1)     # -1 左刃口 … 1 右刃口
    # 银：左半迎光（亮，刃口一条暗线），右半背光（中间一条暗带，靠刃口又一道白反光）
    tone_l = 0.06 + 0.22 * smoothstep(-0.75, -1.0, u)
    tone_r = 0.18 + 0.55 * np.exp(-((u - 0.45) / 0.22) ** 2) + 0.2 * smoothstep(0.85, 1.0, u)
    tone = np.where(u < 0, tone_l, tone_r)
    tone = tone + 0.1 * c.grad(0, TIP_Y, 0, GUARD_Y)
    c.engrave(blade, np.clip(tone, 0, 1), [c.ph_lin(90, wobble=0.5, seed=5), c.ph_lin(84, wobble=0.5, seed=6)], spacing=7.5,
              wmax=2.6)

    face(c)

    # 中脊、刃口、白反光
    c.stroke(poly([(CX, TIP_Y + 6), (CX, GUARD_Y)]), 3.0, taper=(0.3, 0.0))
    c.erase(c.stroke(poly([(CX - 8, TIP_Y + 60), (CX - 8, GUARD_Y - 20)]), 3.0, add=False, taper=0.3))
    gl = [(CX + 0.86 * float(blade_half(np.array([y]))[0]), y) for y in np.linspace(TIP_Y + 120, GUARD_Y - 30, 20)]
    c.erase(c.stroke(np.array(gl), 4.0, add=False, taper=0.3))
    c.stroke(blade_pts, OUTLINE * 0.9, closed=True, light=0.45)
    # 刀尖一颗星形高光
    sx, sy = CX - 20, TIP_Y + 150
    for a in (0, 90, 45, 135):
        ang = math.radians(a)
        ln = 34 if a % 90 == 0 else 18
        c.erase(c.stroke(poly([(sx - ln * math.cos(ang), sy - ln * math.sin(ang)), (sx + ln * math.cos(ang), sy + ln * math.sin(ang))]),
                         4.0 if a % 90 == 0 else 2.6, add=False, taper=0.5))

    # ------------------------------------------------------------ 护手：两端卷起的横档
    G = Frame((CX, GUARD_Y + 22))
    guard = [(-200, -14), (-120, -22), (0, -26), (120, -22), (200, -14), (232, -30), (252, -16), (240, 8), (210, 14),
             (120, 20), (0, 24), (-120, 20), (-210, 14), (-240, 8), (-252, -16), (-232, -30)]
    m = c.mask(spline(G(guard), closed=True))
    lam = c.relief(m, radius=16, blur=1.5)
    c.engrave(m, np.clip(0.7 - 0.7 * lam, 0.05, 1), [c.ph_lin(0, wobble=0.4, seed=7), c.ph_lin(70, seed=8)], spacing=6.5, wmax=2.4)
    c.outline(G.cp(guard), w=STRUCT + 1, light=0.45)
    for sd in (-1, 1):                                                      # 卷头
        c.stroke(ellipse_pts(*G.p(sd * 236, -10), 12, 12), 3, closed=True)
        c.dots([G.p(sd * 236, -10)], 4)
    c.fill(c.ellipse(*G.p(0, 0), 26, 20), value=0.0)                        # 中间一颗宝石
    c.engrave(c.ellipse(*G.p(0, 0), 26, 20), 0.6, [c.ph_lin(-45, wobble=0)], spacing=5, wmax=2)
    c.erase(c.ellipse(*G.p(-8, -6), 6, 5))
    c.stroke(ellipse_pts(*G.p(0, 0), 26, 20), 3.4, closed=True)

    # ------------------------------------------------------------ 握柄：缠银丝，斜纹
    grip = c.rect(CX - 38, GUARD_Y + 46, CX + 38, 820)
    across = (c.xx - CX) / 38
    c.engrave(grip, np.clip(0.35 + 0.45 * across, 0.05, 1), [c.ph_lin(-28, wobble=0.3, seed=9), c.ph_lin(90, seed=10)], spacing=8)
    wrap = c.lines(c.yy + (c.xx - CX) * 0.45, 13, 2.6) * grip
    c.add(wrap)
    c.stroke(poly([(CX - 38, GUARD_Y + 46), (CX - 38, 820)]), STRUCT * 0.8, taper=0)
    c.stroke(poly([(CX + 38, GUARD_Y + 46), (CX + 38, 820)]), STRUCT * 1.3, taper=0)
    # 柄头
    pm = c.ellipse(CX, 850, 64, 44)
    lam = c.relief(pm, radius=30, blur=2)
    c.engrave(pm, np.clip(0.75 - 0.85 * lam, 0.02, 1), [c.ph_ring(CX, 850), c.ph_lin(30)], spacing=7)
    c.stroke(ellipse_pts(CX, 850, 64, 44), STRUCT + 1, closed=True)


def face(c):
    """刃面上映出的脸：中脊就是鼻梁，整张脸纵向拉长 1.15 倍。"""
    F = Frame((CX, 372), scale=0.9)
    sy = 1.15

    def P(pts):
        return [(x, y * sy) + tuple(p[2:]) for p in pts for x, y in [p[:2]]]

    # 帽檐的影子：一条深色的横带
    brim = c.mask(spline(F(P([(-104, -150), (0, -168), (104, -150), (104, -126), (0, -140), (-104, -124)])), closed=True))
    c.engrave(brim, 0.85, [c.ph_lin(2, wobble=0.4), c.ph_lin(60)], spacing=6, wmax=2.4)
    # 脸右半边背光：一片淡排线
    shade = c.mask(spline(F(P([(0, -120), (60, -112), (88, -40), (80, 60), (40, 118), (0, 132)])), closed=True))
    c.engrave(shade, 0.35, [c.ph_lin(-60, wobble=0.4, seed=11)], spacing=6.5, wmax=2.0)
    # 脸的轮廓（淡）
    c.line(F.cp(P([(-74, -118), (-90, -40), (-80, 60), (-40, 118), (0, 136), (40, 118), (80, 60), (90, -40), (74, -118)])), w=2.4,
           taper=0.2)
    # 眉、眼（睁大，盯着刀外面）
    for sd in (-1, 1):
        c.line(F.cp(P([(sd * 14, -78), (sd * 40, -92), (sd * 72, -84)])), w=5.5, taper=0.3)
        ex, ey = sd * 40, -52
        upper = F(spline(P([(ex - 28, ey + 2), (ex - 8, ey - 13), (ex + 12, ey - 12), (ex + 28, ey)])))
        lower = F(spline(P([(ex - 28, ey + 2), (ex - 6, ey + 11), (ex + 14, ey + 9), (ex + 28, ey)])))
        c.stroke(upper, 4.2, taper=0.2)
        c.stroke(lower, 2.2, taper=0.3)
        ix, iy = F.p(ex + sd * -2, ey * sy)
        c.add(c.ellipse(ix, iy, 9, 10.5))
        c.erase(c.ellipse(ix - 3, iy - 4, 2.6, 2.6))
    # 鼻子：鼻梁就是中脊；鼻翼两道
    for sd in (-1, 1):
        c.line(F.cp(P([(sd * 6, 18), (sd * 20, 26), (sd * 16, 36)])), w=3.2, taper=0.3)
    c.line(F.cp(P([(-10, 38), (0, 42), (10, 38)])), w=2.4, taper=0.3)
    # 嘴：抿着
    c.line(F.cp(P([(-34, 74), (-12, 70), (0, 72), (14, 70), (36, 76)])), w=4.0, taper=0.3)
    c.line(F.cp(P([(-18, 88), (0, 92), (18, 88)])), w=2.0, taper=0.4)
