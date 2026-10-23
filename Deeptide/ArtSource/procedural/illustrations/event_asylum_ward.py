"""事件 5 疯人院病房：值班的护士冲你点头，好像早就在等你（Data/events.csv）。

一点透视的长病房：左墙一排拱形高窗，月光在地上投下一格格亮斑；靠左墙一排铁床，白床单；右墙一排带铁栅小窗的门；
天花板上吊着几盏灯，只有一盏亮着。走廊尽头的门开着，门里亮，护士站在门口，头偏向一边（点头）。
癫狂层：床上坐起来的人影、门上铁栅窗里的眼睛。
"""
import math

import numpy as np

from etch import Frame, ellipse_pts, poly, smoothstep, spline
from cards._common import eye, wraith
from illustrations._kit import GAP, OUT, STRUCT, hatch, ray_r0

NAME = '疯人院病房'
SIZE = (1920, 1280)
VP = (1000, 560)                 # 灭点：走廊尽头的门
DOOR = (930, 450, 1070, 680)     # 尽头那扇门（x0, y0, x1, y1）
H_FLOOR = 1280                   # 左墙墙脚线在画面边缘的 y（= 画面底边）


def persp(x, y, t):
    """从画面边缘的点 (x, y) 往灭点走 t（0 = 原处，1 = 灭点）。"""
    return (x + (VP[0] - x) * t, y + (VP[1] - y) * t)


def draw(c):
    rng = np.random.default_rng(5)
    W, H = c.w, c.h
    dx0, dy0, dx1, dy1 = DOOR
    # 走廊的四个面：天花板、地板、左墙、右墙，交线从画面四角连到尽头的门
    ceil = c.mask(poly([(0, 0), (W, 0), (dx1, dy0), (dx0, dy0)]))
    floor = c.mask(poly([(0, H), (W, H), (dx1, dy1), (dx0, dy1)]))
    left = c.mask(poly([(0, 0), (dx0, dy0), (dx0, dy1), (0, H)]))
    right = c.mask(poly([(W, 0), (dx1, dy0), (dx1, dy1), (W, H)]))
    depth = 1 - c.radial(VP[0], VP[1], 0, 1100)                            # 越往里越暗

    hatch(c, ceil, np.clip(0.62 + 0.2 * depth, 0, 1), [c.ph_ray(*VP, r0=ray_r0(700)), c.ph_lin(5, seed=1)], spacing=9)
    hatch(c, left, np.clip(0.4 + 0.35 * depth + 0.06 * c.noise(60, 2), 0, 1), [c.ph_lin(90, seed=3), c.ph_ray(*VP, r0=ray_r0(700))], spacing=9)
    hatch(c, right, np.clip(0.55 + 0.3 * depth + 0.06 * c.noise(60, 4), 0, 1), [c.ph_lin(90, seed=5), c.ph_ray(*VP, r0=ray_r0(700))], spacing=9)
    # 地板：黑白相间的方砖，按透视排（砖缝是到灭点的放射线 + 对数间距的横缝）
    d0 = 40.0
    rows = np.log(np.maximum(c.yy - dy1 + d0, 1) / d0)
    cols = np.arctan2(c.xx - VP[0], c.yy - VP[1] + 1e-3)
    checker = ((np.floor(rows * 4.2) + np.floor(cols * 9)) % 2).astype(np.float32)
    hatch(c, floor, np.clip(0.2 + 0.5 * checker + 0.25 * depth, 0, 1), [c.ph_lin(-30, seed=6), c.ph_lin(40, seed=7)], spacing=8.5)
    for k in range(-8, 9):                                                  # 砖缝
        a = k / 9
        c.add(c.stroke([(VP[0] + math.tan(a) * (dy1 - VP[1]), dy1), (VP[0] + math.tan(a) * (H - VP[1]), H)], 2.2, add=False, taper=0) * floor)
    for j in range(1, 12):
        y = dy1 - d0 + d0 * math.exp(j / 4.2)
        if y > H:
            break
        c.add(c.stroke([(0, y), (W, y)], 2.0, add=False, taper=0) * floor)
    # 四条墙角线
    for p in ((0, 0), (W, 0), (0, H), (W, H)):
        q = (dx0 if p[0] == 0 else dx1, dy0 if p[1] == 0 else dy1)
        c.stroke([p, q], STRUCT, taper=0)

    # ------------------------------------------------------------ 左墙的拱窗和地上的月光
    for k, t in enumerate((0.04, 0.36, 0.58, 0.72)):
        t2 = t + 0.13 * (1 - t)
        a0, a1 = persp(0, 120, t), persp(0, 120, t2)
        b0, b1 = persp(0, 640, t), persp(0, 640, t2)
        top = (a0[1] + a1[1]) / 2 - (b0[1] - a0[1]) * 0.18
        win = [b0, a0, ((a0[0] + a1[0]) / 2, top), a1, b1]
        wm = c.mask(poly(win))
        c.clear(wm)
        hatch(c, wm, 0.15, [c.ph_lin(80, seed=10 + k)], spacing=9, wmax=2.0)
        c.stroke(poly(win + [win[0]]), STRUCT * (1 - t * 0.6), taper=0)
        c.stroke([((a0[0] + a1[0]) / 2, top), ((b0[0] + b1[0]) / 2, (b0[1] + b1[1]) / 2)], 3 * (1 - t * 0.6), taper=0)   # 窗棂
        # 月光落在地上：窗的形状往右下投成一块平行四边形，把地板的墨擦淡
        f0, f1 = persp(260, H, t), persp(260, H, t2)
        patch = c.mask(poly([f0, (f0[0] + 300 * (1 - t), f0[1]), (f1[0] + 300 * (1 - t2), f1[1]), f1])) * floor
        c.erase(patch * 0.6)

    # ------------------------------------------------------------ 右墙的门：铁栅小窗
    doors = []
    for k, t in enumerate((0.05, 0.4, 0.62, 0.76)):
        t2 = t + 0.09 * (1 - t)
        a0, a1 = persp(W, 260, t), persp(W, 260, t2)
        b0, b1 = persp(W, H - 120, t), persp(W, H - 120, t2)
        door = [a0, a1, b1, b0]
        dm = c.mask(poly(door))
        hatch(c, dm, 0.85, [c.ph_lin(90, seed=20 + k), c.ph_lin(30, seed=24 + k)], spacing=7.5)
        c.stroke(poly(door + [door[0]]), STRUCT * (1 - t * 0.6), taper=0)
        # 铁栅小窗：门的上部
        g0 = (a0[0] + (a1[0] - a0[0]) * 0.25, a0[1] + (b0[1] - a0[1]) * 0.15)
        g1 = (a0[0] + (a1[0] - a0[0]) * 0.75, a1[1] + (b1[1] - a1[1]) * 0.15)
        gh = (b0[1] - a0[1]) * 0.16
        grille = [g0, g1, (g1[0], g1[1] + gh), (g0[0], g0[1] + gh)]
        gm = c.mask(poly(grille))
        c.fill(gm, value=1.0)
        for j in range(1, 4):
            x = g0[0] + (g1[0] - g0[0]) * j / 4
            c.erase(c.stroke([(x, g0[1] + (g1[1] - g0[1]) * j / 4 + 3), (x, g0[1] + (g1[1] - g0[1]) * j / 4 + gh - 3)],
                             max(1.5, 4 * (1 - t)), add=False, taper=0))
        doors.append(((g0[0] + g1[0]) / 2, (g0[1] + g1[1]) / 2 + gh / 2, (g1[0] - g0[0]) * 0.3))

    # ------------------------------------------------------------ 铁床：靠左墙，越远越小
    beds = []
    for k, t in enumerate((0.1, 0.42, 0.6, 0.72)):
        beds.append(bed(c, t))

    # ------------------------------------------------------------ 吊灯：一盏亮着
    for k, t in enumerate((0.25, 0.5, 0.68)):
        x, y = persp(960, 0, t)
        y_lamp = y + 140 * (1 - t)
        c.stroke([(x, y), (x, y_lamp)], 2.4 * (1 - t) + 1, taper=0)
        shade = [(x - 50 * (1 - t), y_lamp + 30 * (1 - t)), (x, y_lamp), (x + 50 * (1 - t), y_lamp + 30 * (1 - t))]
        c.fill(c.mask(poly(shade)), value=0.95)
        if k == 1:
            glow = 1 - c.radial(x, y_lamp + 40, 20, 260, sy=0.8)
            c.erase(glow * 0.6)
            c.fill(c.mask(poly(shade)), value=0.95)
            c.clear(c.ellipse(x, y_lamp + 30 * (1 - t), 14, 8))

    # ------------------------------------------------------------ 尽头的门：门里亮，护士站在门口
    c.clear(c.rect(dx0 + 8, dy0 + 8, dx1 - 8, dy1))
    hatch(c, c.rect(dx0 + 8, dy0 + 8, dx1 - 8, dy1), 0.08, [c.ph_lin(90, seed=30)], spacing=10, wmax=1.8)
    c.stroke(poly([(dx0, dy1), (dx0, dy0), (dx1, dy0), (dx1, dy1)]), STRUCT, taper=0)
    nurse(c, 1000, dy1 - 2, 190)

    # ============================================================ 癫狂层：床上坐起来的人影，铁栅窗里的眼睛
    for x, y, s in beds:
        wraith(c, x, y + 10 * s, 260 * s, lean=-8, layer='mad', tone=0.85)
    for x, y, w in doors:
        eye(c, x, y, max(w, 8), layer='mad', look=-0.5, width=max(2.0, w * 0.12))


def bed(c, t):
    """铁床：床头靠左墙，床身横着伸进走廊（和画面平行，只随深度缩小）。t 是床头那一侧离画面的深度（0 近、1 灭点）。
    返回 (床中间的 x, 床面的 y, 缩放)，癫狂层的人影坐在上面。"""
    s = 1 - t
    t2 = t + 0.05 * s                                                       # 床宽：往里再走一点
    head, head2 = persp(0, H_FLOOR, t), persp(0, H_FLOOR, t2)               # 墙脚线上床头的两个角
    L, L2 = 640 * s, 640 * (1 - t2)
    hgt, hgt2 = 150 * s, 150 * (1 - t2)
    A, B = (head[0], head[1] - hgt), (head[0] + L, head[1] - hgt)            # 床面靠近我们的那条边
    D, C = (head2[0], head2[1] - hgt2), (head2[0] + L2, head2[1] - hgt2)     # 远的那条边
    top = [D, C, B, A]
    front = [A, B, (B[0], B[1] + hgt * 0.28), (A[0], A[1] + hgt * 0.28)]
    for pts, tone in ((front, 0.7), (top, 0.12)):                          # 床单：顶面白，垂下来的侧面暗
        m = c.mask(poly(pts))
        c.clear(m)
        hatch(c, m, tone, [c.ph_lin(-4, seed=40), c.ph_lin(80, seed=41)], spacing=8, wmax=2.0)
        c.stroke(poly(pts + [pts[0]]), max(1.6, 3.4 * s), taper=0)
    for k in range(3):                                                     # 床单的褶：从床面垂到侧面
        x = A[0] + L * (0.3 + 0.2 * k)
        c.line([(x, A[1] - 6 * s), (x + 10 * s, A[1] + hgt * 0.15), (x + 4 * s, A[1] + hgt * 0.28)], w=max(1.4, 2.4 * s), taper=0.3)
    pw, ph = 110 * s, 34 * s                                                # 枕头：靠床头
    px, py = A[0] + pw * 0.75, (A[1] + D[1]) / 2 - ph * 0.3
    c.clear(c.ellipse(px, py, pw / 2, ph / 2))
    c.stroke(ellipse_pts(px, py, pw / 2, ph / 2), max(1.6, 2.6 * s), closed=True)
    w = max(2.0, 7 * s)                                                     # 铁管：床头的拱、床尾的栏杆、四条腿
    c.stroke(spline([(A[0] + 6 * s, head[1]), (A[0] + 4 * s, A[1] - hgt * 1.1), (A[0] + 40 * s, A[1] - hgt * 1.45),
                     (A[0] + 80 * s, A[1] - hgt * 1.1), (A[0] + 84 * s, A[1])]), w, taper=0)
    c.stroke(spline([(B[0] - 4 * s, head[1]), (B[0] - 4 * s, B[1] - hgt * 0.6), (B[0] - 40 * s, B[1] - hgt * 0.75),
                     (B[0] - 70 * s, B[1] - hgt * 0.6), (B[0] - 72 * s, B[1])]), w * 0.9, taper=0)
    for x in (A[0] + 6 * s, B[0] - 4 * s):
        c.stroke([(x, A[1] + hgt * 0.28), (x, head[1])], w * 0.8, taper=0)
    c.fill(c.shadow_of(c.ellipse(A[0] + L / 2, head[1] + 6 * s, L * 0.55, 18 * s), 0, 0, blur=4), value=0.85)
    return (A[0] + L * 0.45, A[1], s)


def nurse(c, x, y, h):
    """护士：长裙 + 围裙 + 白帽，逆光是一个剪影，围裙和帽子是亮的；头偏向一边（点头）。"""
    F = Frame((x, y), scale=h / 360)
    dress = [(-60, 0), (-46, -120), (-36, -220), (-46, -250), (-28, -272), (28, -272), (46, -250), (36, -220), (46, -120), (60, 0)]
    c.fill(c.mask(spline(F(dress), closed=True)), value=0.95)
    apron = [(-26, -10), (-22, -120), (-18, -200), (18, -200), (22, -120), (26, -10)]
    am = c.mask(spline(F(apron), closed=True))
    c.clear(am)
    hatch(c, am, 0.2, [c.ph_lin(90, seed=50)], spacing=8, wmax=1.8)
    c.outline(F.cp(apron), w=2.4, light=0.2)
    hx, hy = F.p(10, -296)
    c.fill(c.ellipse(hx, hy, 18 * F.scale, 22 * F.scale, rot=16), value=0.95)   # 头偏向右
    cap = [(-6, -322), (8, -330), (34, -318), (30, -306), (-2, -308)]
    c.clear(c.mask(spline(F(cap), closed=True)))
    c.outline(F.cp(cap), w=2.0, light=0.2)
    c.fill(c.shadow_of(c.ellipse(x, y + 4, 90 * F.scale, 12 * F.scale), 0, 0, blur=3), value=0.9)
