"""C04 镇定：闭眼的人，手按在怀表上（策划/10 §4.6）。

侧脸朝右，微微低头，闭着眼像在听表走的声音；右下角一只手托着打开的怀表，拇指压在表壳上。
背景左上亮、右边暗，脸的前轮廓衬在暗底上，后脑的头发衬在亮底上。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, smoothstep, spline

NAME = '镇定'

WATCH = (905, 640)
WR = 118


def draw(c):
    # ------------------------------------------------------------ 背景
    bg = np.ones((c.h, c.w), np.float32)
    c.engrave(bg, 0.1 + 0.62 * c.grad(250, 120, 1150, 600) ** 1.2 + 0.05 * c.noise(120, 1),
              [c.ph_lin(-50, wobble=2, seed=2), c.ph_lin(40, seed=3), c.ph_lin(-5, seed=4)], spacing=10)

    head(c, Frame((420, 330), rot=14, scale=1.05))
    watch_and_hand(c)


def head(c, F):
    """侧脸（朝右），原点约在耳朵处。"""
    profile = [(-40, -262), (60, -246), (126, -200), (156, -132), (164, -80), (158, -58), (176, -20), (206, 14), (198, 26),
               (176, 32), (170, 48), (182, 66), (170, 78), (178, 92), (164, 106), (168, 128), (156, 152), (120, 166),
               (110, 200), (106, 320), (-96, 330), (-96, 150), (-130, 80), (-172, -40), (-160, -170), (-110, -236)]
    face_m = c.mask(spline(F(profile), closed=True))
    side = c.grad(*F.p(180, 0), *F.p(-120, 0))                           # 0 = 脸前（迎光）
    lam = c.relief(face_m, radius=70, blur=5)
    c.engrave(face_m, np.clip(0.02 + 0.28 * side + 0.3 * (1 - lam) ** 1.5, 0, 1),
              [c.ph_axis(F.p(0, -250), F.p(0, 300), bow=-30, half=160, seed=5), c.ph_lin(F.angle(70), seed=6),
               c.ph_lin(F.angle(-20), seed=7)], spacing=8.5, wmax=2.6)
    # 头发：从前额发际线盖到后脑，顺着头形的弧线
    hair = [(-40, -286), (30, -280), (86, -258), (132, -214), (138, -178), (112, -176, 'c'), (96, -150), (64, -138), (40, -100),
            (30, -62), (14, -66, 'c'), (-10, -40), (-30, 10), (-50, 60), (-80, 118), (-104, 132), (-140, 90), (-186, -30),
            (-182, -170), (-130, -250)]
    hm = c.mask(spline(F(hair), closed=True))
    flow = c.ph_contour(hm, blur=6) + 3 * c.noise(30, 8)
    c.engrave(hm, np.clip(0.6 + 0.3 * c.grad(*F.p(100, -200), *F.p(-150, 50)) + 0.06 * c.noise(20, 9), 0, 1),
              [flow, c.ph_lin(F.angle(60), seed=10), c.ph_lin(F.angle(-30), seed=11)], spacing=7.5, swell=1.2)
    rng = np.random.default_rng(5)
    for k in range(14):                                                     # 几绺发丝，从发旋往前、往后梳
        x0, y0 = -60 + rng.uniform(-30, 30), -250 + rng.uniform(-20, 20)
        x1, y1 = x0 + rng.uniform(80, 170) * (1 if k % 2 else -0.8), y0 + rng.uniform(60, 200)
        c.line(F.cp([(x0, y0), ((x0 + x1) / 2 + rng.uniform(-20, 20), (y0 + y1) / 2 - 30), (x1, y1)]), w=2.2, taper=0.4)
    c.outline(F.cp(hair), w=STRUCT, light=0.4)
    c.stroke(F(spline(profile[:21])), OUTLINE, taper=(0.05, 0.3), light=0.4)
    c.stroke(F(spline(profile[20:] + [profile[0]])), OUTLINE * 0.9, taper=(0.3, 0.05), light=0.4)
    # 闭着的眼：上眼睑一道下弯的弧 + 睫毛；眉毛
    c.line(F.cp([(96, -58), (116, -48), (140, -50)]), w=4.6, taper=0.25)
    for k in range(5):
        x = 104 + k * 8
        y = -51 + 0.04 * (x - 118) ** 2 * 0.3
        c.stroke(F([(x, y), (x + 4, y + 10)]), 2.2, taper=(0.0, 0.8))
    c.line(F.cp([(90, -92), (118, -98), (148, -88)]), w=6, taper=0.35)
    c.line(F.cp([(104, -36), (126, -30), (146, -34)]), w=2.0, taper=0.4)     # 下眼睑
    # 鼻翼、嘴角、下巴的结构线
    c.line(F.cp([(170, 14), (160, 22), (164, 30)]), w=3.2, taper=0.3)
    c.line(F.cp([(150, 78), (164, 78)]), w=3.0, taper=0.3)
    c.line(F.cp([(120, 120), (140, 136), (150, 150)]), w=2.4, taper=0.4)
    c.line(F.cp([(76, -10), (100, 40), (120, 70)]), w=2.2, taper=0.5)        # 颧骨下的一道
    # 耳朵
    ear = [(-30, -44), (-6, -56), (10, -40), (8, -10), (12, 20), (0, 44), (-16, 50), (-30, 30), (-36, -10)]
    em = c.mask(spline(F(ear), closed=True))
    lam = c.relief(em, radius=10, blur=1)
    c.engrave(em, np.clip(0.55 - 0.45 * lam, 0, 1), [c.ph_lin(F.angle(80)), c.ph_lin(F.angle(10))], spacing=6, wmax=2.2)
    c.outline(F.cp(ear), w=STRUCT, light=0.4)
    c.line(F.cp([(-8, -40), (0, -24), (-4, 0), (-12, 14)]), w=2.6, taper=0.3)       # 耳轮
    c.line(F.cp([(-20, -6), (-12, 6), (-20, 22)]), w=2.2, taper=0.3)               # 耳屏
    # 领子
    collar = [(-96, 220), (20, 200), (110, 196), (150, 260), (120, 330), (-96, 330)]
    cm = c.mask(spline(F(collar), closed=True))
    c.engrave(cm, 0.12, [c.ph_lin(F.angle(80))], spacing=8, wmax=2)
    c.outline(F.cp(collar), w=STRUCT, light=0.4)
    coat = [(-120, 280), (-10, 270), (120, 300), (240, 420), (260, 600), (-120, 600)]
    m = c.mask(spline(F(coat), closed=True))
    c.engrave(m, 0.88, [c.ph_lin(F.angle(85), seed=12), c.ph_lin(F.angle(20), seed=13), c.ph_lin(F.angle(-40), seed=14)],
              spacing=8.5, swell=1)
    c.outline(F.cp(coat), w=OUTLINE, light=0.4)


def watch_and_hand(c):
    wx, wy = WATCH
    # 手掌：从右下伸进来托住表
    palm = [(wx - 40, wy + 70), (wx + 120, wy + 40), (wx + 260, wy + 60), (wx + 360, wy + 200), (wx - 60, wy + 260)]
    m = c.mask(spline(palm, closed=True))
    lam = c.relief(m, radius=60, blur=4)
    c.engrave(m, np.clip(0.55 - 0.45 * lam + 0.2 * c.grad(wx, wy, wx + 300, wy + 200), 0.05, 1),
              [c.ph_lin(-20, seed=15), c.ph_lin(60, seed=16)], spacing=8.5)
    c.outline(palm, w=OUTLINE * 0.9, light=0.4)
    # 表链：从表冠绕到左上，消失在衣襟里
    chain = spline([(wx, wy - WR - 30), (wx - 60, wy - WR - 90), (wx - 180, wy - 150), (wx - 260, wy - 40), (wx - 330, wy + 40)])
    for i, p in enumerate(chain[::9]):
        c.stroke(ellipse_pts(p[0], p[1], 7, 4.5, rot=(i % 2) * 90 + 30), 2.4, closed=True)
    # 表壳
    case = c.ellipse(wx, wy, WR + 16, WR + 16)
    lam = c.relief(case, radius=20, blur=1.5)
    c.engrave(case, np.clip(0.75 - 0.8 * lam, 0, 1), [c.ph_ring(wx, wy), c.ph_ray(wx, wy, r0=120)], spacing=6, wmax=2.4)
    c.stroke(ellipse_pts(wx, wy, WR + 16, WR + 16), STRUCT + 1, closed=True)
    # 表冠和提环
    c.fill(c.rect(wx - 14, wy - WR - 34, wx + 14, wy - WR - 12), value=0.7)
    c.stroke(poly([(wx - 14, wy - WR - 34), (wx + 14, wy - WR - 34), (wx + 14, wy - WR - 12), (wx - 14, wy - WR - 12),
                   (wx - 14, wy - WR - 34)]), 3, taper=0)
    c.stroke(ellipse_pts(wx, wy - WR - 44, 20, 14), 4, closed=True)
    # 表盘：留白，刻度，罗马数字用短竖线代替，两根指针（快到十二点）
    dial = c.ellipse(wx, wy, WR, WR)
    c.clear(dial, layers=('ink',))
    c.stroke(ellipse_pts(wx, wy, WR, WR), 3, closed=True)
    c.stroke(ellipse_pts(wx, wy, WR - 26, WR - 26), 1.6, closed=True)
    for k in range(60):
        a = math.radians(k * 6 - 90)
        r0 = WR - (16 if k % 5 == 0 else 8)
        c.stroke(poly([(wx + r0 * math.cos(a), wy + r0 * math.sin(a)), (wx + (WR - 3) * math.cos(a), wy + (WR - 3) * math.sin(a))]),
                 3.4 if k % 5 == 0 else 1.6, taper=0)
    for k in range(12):                                                   # 小时数字：两三道短竖
        a = math.radians(k * 30 - 90)
        cx, cy = wx + (WR - 40) * math.cos(a), wy + (WR - 40) * math.sin(a)
        n = [3, 1, 2, 3, 2, 1, 2, 3, 1, 2, 3, 2][k]
        for j in range(n):
            ox = (j - (n - 1) / 2) * 5
            c.stroke(poly([(cx + ox, cy - 7), (cx + ox, cy + 7)]), 2.0, taper=0)
    for ang, ln, w in ((-96, WR - 34, 5), (-72, WR - 56, 7)):
        a = math.radians(ang)
        c.stroke(poly([(wx - 14 * math.cos(a), wy - 14 * math.sin(a)), (wx + ln * math.cos(a), wy + ln * math.sin(a))]), w,
                 taper=(0.0, 0.7))
    c.dots([(wx, wy)], 7)
    c.engrave(dial * c.half_plane((wx + 200, wy - 200), (wx - 200, wy + 200)) * (1 - c.ellipse(wx, wy, WR - 20, WR - 20)),
              0.25, [c.ph_lin(45, wobble=0.3)], spacing=7, wmax=1.8)
    c.erase(c.stroke(spline([(wx - 80, wy - 50), (wx - 60, wy - 80), (wx - 30, wy - 96)]), 5, add=False, taper=0.5))
    # 拇指：从左下压在表壳边上
    thumb = [(wx - 150, wy + 150), (wx - 130, wy + 90), (wx - 96, wy + 52), (wx - 70, wy + 44), (wx - 60, wy + 66),
             (wx - 84, wy + 104), (wx - 100, wy + 160)]
    m = c.mask(spline(thumb, closed=True))
    lam = c.relief(m, radius=22, blur=2)
    c.engrave(m, np.clip(0.5 - 0.5 * lam, 0.02, 1), [c.ph_lin(-40), c.ph_lin(50)], spacing=7)
    c.outline(thumb, w=STRUCT + 1, light=0.4)
    c.line([(wx - 92, wy + 60), (wx - 76, wy + 52), (wx - 70, wy + 62)], w=2.2, taper=0.3)
    # 四根手指从右边弯上来扣住表壳
    for k, (dy, ln) in enumerate(((-30, 60), (10, 70), (50, 66), (86, 54))):
        y = wy + dy
        x = wx + math.sqrt(max((WR + 16) ** 2 - dy ** 2, 0)) - 18
        f = [(x - 8, y - 15), (x + ln * 0.6, y - 20), (x + ln, y - 8), (x + ln, y + 12), (x + ln * 0.6, y + 18), (x - 6, y + 14)]
        m = c.mask(spline(f, closed=True))
        lam = c.relief(m, radius=12, blur=1)
        c.engrave(m, np.clip(0.55 - 0.5 * lam + 0.1 * k, 0.02, 1), [c.ph_lin(90), c.ph_lin(20)], spacing=6.5, wmax=2.4)
        c.outline(f, w=STRUCT, light=0.4)
