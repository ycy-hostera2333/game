"""U10 狂笑：捂嘴笑的人。癫狂层：指缝里露出更多的嘴（策划/10 §4.6）。

侧脸朝右，眼睛笑得眯成两道弯，眼角挤出皱纹，肩膀在抖（几道抖动线）；一只手捂着嘴，手指张开一点。
手是亮的（迎光），癫狂层的嘴就从指缝里、手背上咧出来：一排排牙。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, smoothstep, spline

NAME = '狂笑'


def draw(c):
    rng = np.random.default_rng(10)
    # ------------------------------------------------------------ 背景
    c.engrave(np.ones((c.h, c.w), np.float32), 0.25 + 0.55 * c.grad(1100, 300, 300, 400) ** 1.2 + 0.05 * c.noise(100, 1),
              [c.ph_lin(-60, wobble=2, seed=2), c.ph_lin(30, seed=3), c.ph_lin(-15, seed=4)], spacing=10, swell=1.1)

    # ------------------------------------------------------------ 头：侧脸朝右，笑得往后仰
    F = Frame((410, 400), rot=-12, scale=1.08)
    prof = [(-40, -262), (60, -246), (126, -200), (156, -132), (164, -80), (158, -58), (176, -20), (206, 14), (198, 26),
            (176, 32), (172, 50), (186, 70), (172, 84), (178, 98), (164, 112), (168, 130), (156, 152), (120, 166), (110, 200),
            (106, 340), (-110, 350), (-100, 150), (-130, 80), (-172, -40), (-160, -170), (-110, -236)]
    m = c.mask(spline(F(prof), closed=True))
    lam = c.relief(m, radius=70, blur=5)
    side = c.grad(*F.p(190, 0), *F.p(-160, 0))
    c.engrave(m, np.clip(0.0 + 0.3 * side + 0.32 * (1 - lam) ** 1.5, 0, 1),
              [c.ph_axis(F.p(0, -260), F.p(0, 340), bow=-30, half=170, seed=5), c.ph_lin(F.angle(65), seed=6),
               c.ph_lin(F.angle(-25), seed=7)], spacing=8.5, swell=1.0)
    c.outline(F.cp(prof), w=OUTLINE, light=0.45)
    hair = [(-40, -286), (30, -280), (86, -258), (132, -214), (138, -178), (112, -176, 'c'), (96, -150), (64, -138), (40, -100),
            (30, -62), (14, -66, 'c'), (-10, -40), (-30, 10), (-50, 60), (-80, 118), (-104, 132), (-140, 90), (-186, -30),
            (-182, -170), (-130, -250)]
    hm = c.mask(spline(F(hair), closed=True))
    c.engrave(hm, 0.85, [c.ph_contour(hm, blur=5) + 3 * c.noise(25, 8), c.ph_lin(50), c.ph_lin(-40)], spacing=7.5, swell=1.3)
    c.outline(F.cp(hair), w=STRUCT, light=0.4)
    # 笑眯的眼：一道向上拱的弧 + 鱼尾纹；眉毛挑高
    c.line(F.cp([(96, -52), (118, -66), (142, -54)]), w=7, taper=0.25)
    c.line(F.cp([(104, -42), (122, -38), (138, -44)]), w=2.6, taper=0.3)
    for k in range(3):
        c.line(F.cp([(92, -56 + k * 10), (72, -62 + k * 16)]), w=2.2, taper=0.4)
    c.line(F.cp([(88, -100), (118, -116), (150, -102)]), w=7, taper=0.3)
    c.line(F.cp([(170, 14), (160, 22), (164, 30)]), w=3.2, taper=0.3)          # 鼻翼
    c.line(F.cp([(150, 20), (132, 56), (128, 90)]), w=3.4, taper=0.4)          # 笑出来的法令纹
    c.line(F.cp([(110, -20), (130, 0), (138, 24)]), w=2.2, taper=0.5)          # 鼓起的颧骨
    for k in range(3):                                                          # 额头的抬头纹
        c.line(F.cp([(50, -190 + k * 18), (90, -200 + k * 18), (126, -192 + k * 18)]), w=2.0, taper=0.4)
    # 耳朵
    ear = [(-30, -44), (-6, -56), (10, -40), (8, -10), (12, 20), (0, 44), (-16, 50), (-30, 30), (-36, -10)]
    em = c.mask(spline(F(ear), closed=True))
    lam = c.relief(em, radius=10, blur=1)
    c.engrave(em, np.clip(0.8 - 0.45 * lam, 0, 1), [c.ph_lin(F.angle(80)), c.ph_lin(F.angle(10))], spacing=6, wmax=2.2)
    c.outline(F.cp(ear), w=STRUCT, light=0.4)
    c.line(F.cp([(-8, -40), (0, -24), (-4, 0), (-12, 14)]), w=2.6, taper=0.3)
    # 领子 + 风衣
    coat = [(-140, 280), (-10, 270), (120, 300), (260, 420), (300, 640), (-160, 640)]
    m = c.mask(spline(F(coat), closed=True))
    c.engrave(m, 0.9, [c.ph_lin(F.angle(85), seed=12), c.ph_lin(F.angle(20), seed=13), c.ph_lin(F.angle(-40), seed=14)],
              spacing=8.5, swell=1)
    c.outline(F.cp(coat), w=OUTLINE, light=0.4)
    # 肩膀在抖：后背的几道平行抖动线
    for k in range(4):
        c.stroke(F([(-190 - k * 20, 300 + k * 26), (-210 - k * 20, 380 + k * 26)]), 3.4, taper=0.4)
        c.stroke(F([(-200 - k * 20, -180 + k * 30), (-230 - k * 20, -120 + k * 30)]), 3.0, taper=0.4)

    # ------------------------------------------------------------ 捂嘴的手：从下方伸上来，手背朝外，手指张开
    H = Frame(F.p(170, 150), rot=F.angle(-82))                                        # +x 指向指尖（向上偏右）
    cuff = [(-330, -70), (-140, -66), (-140, 66), (-330, 74)]
    cm = c.mask(H(cuff))
    c.engrave(cm, 0.92, [c.ph_lin(H.angle(90)), c.ph_lin(H.angle(20))], spacing=8, swell=1)
    c.stroke(H(cuff + cuff[:1]), STRUCT, taper=0)
    palm = [(-150, -56), (-40, -66), (20, -60), (30, 56), (-40, 64), (-150, 54)]
    m = c.mask(spline(H(palm), closed=True))
    lam = c.relief(m, radius=40, blur=3)
    c.engrave(m, np.clip(0.3 - 0.35 * lam + 0.2 * c.grad(*H.p(0, -70), *H.p(0, 70)), 0, 1), [c.ph_lin(H.angle(0)), c.ph_lin(H.angle(70))],
              spacing=8, wmax=2.4)
    c.outline(H.cp(palm), w=STRUCT + 1, light=0.4)
    for k, (oy, ln, spread) in enumerate(((-48, 80, -16), (-16, 96, -5), (16, 94, 6), (46, 78, 17))):
        G = Frame(H.p(14, oy), rot=H.angle(spread))
        hw = 15
        pts = [(-6, -hw), (ln * 0.5, -hw - 1), (ln, -4), (ln, 6), (ln * 0.5, hw + 1), (-6, hw)]
        fm = c.mask(spline(G(pts), closed=True))
        lam = c.relief(fm, radius=hw, blur=1)
        c.engrave(fm, np.clip(0.3 - 0.35 * lam, 0, 1), [c.ph_lin(G.angle(90))], spacing=6.5, wmax=2.2)
        c.outline(G.cp(pts), w=STRUCT, light=0.4)
        for j in (0.4, 0.72):
            c.line(G.cp([(ln * j, -hw * 0.7), (ln * j + 4, 0), (ln * j, hw * 0.7)]), w=2.0, taper=0.3)


    # ============================================================ 癫狂层：指缝里、手背上咧开的嘴
    for (lx, ly, w, rot) in ((54, -32, 26, 0), (60, 0, 30, 0), (56, 32, 26, 0), (-80, -20, 56, 90), (-150, 30, 44, 90),
                             (-40, 40, 36, 90)):
        x, y = H.p(lx, ly)
        mouth(c, x, y, w, H.angle(rot), rng)


def mouth(c, x, y, w, rot, rng):
    """一张咧开的嘴：上下唇一个弯月形，里面两排牙。"""
    G = Frame((x, y), rot=rot)
    h = w * 0.45
    upper = spline(G([(-w, 0), (0, -h * 0.5), (w, 0)]))
    lower = spline(G([(w, 0), (0, h), (-w, 0)]))
    m = c.mask(np.vstack([upper, lower]))
    c.fill(m, layer='mad', value=0.15)
    n = 6
    for k in range(n):
        u = -w * 0.85 + (k + 0.5) * 1.7 * w / n
        top = -h * 0.5 * (1 - (u / w) ** 2)
        bot = h * (1 - (u / w) ** 2)
        c.stroke(G([(u - w / n * 0.4, top + 1), (u, top + h * 0.45), (u + w / n * 0.4, top + 1)]), 2.0, layer='mad', taper=0)
        c.stroke(G([(u - w / n * 0.4, bot - 1), (u, bot - h * 0.45), (u + w / n * 0.4, bot - 1)]), 2.0, layer='mad', taper=0)
    c.stroke(upper, 3.6, layer='mad', taper=0.1)
    c.stroke(lower, 3.6, layer='mad', taper=0.1)
