"""B01 打击：手杖劈下的瞬间，近景是手和衣袖（策划/10 §4.6）。

调查员面朝右：衣袖从左上伸进画面，右手握着手杖，杖头（尖端）从竖直劈向右下，身后留下一束弧形速度线。
手从指面看：四根手指横着裹住杖身，拇指斜压在食指上。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, smoothstep, spline

NAME = '打击'

GRIP = (560, 520)       # 握点（杖身轴线穿过拳心）
CANE = -30              # 杖身此刻的角度：杖头指向右上
SWEEP = -112            # 速度线从这个角度开始（刚才举过头顶）
HALF = 27               # 杖身半宽


def draw(c):
    gx, gy = GRIP
    G = Frame(GRIP, rot=CANE)               # 杖的坐标：x 沿杖身指向杖头，y 指向右下（指尖一侧）

    # ------------------------------------------------------------ 背景：夜雾，上亮下暗
    bg = np.ones((c.h, c.w), np.float32)
    fog = 0.3 + 0.55 * c.grad(500, 100, 900, 860) ** 1.2 + 0.08 * c.noise(140, 1)
    c.engrave(bg, fog, [c.ph_lin(-12, wobble=2.5, seed=2), c.ph_lin(60, wobble=2, seed=3), c.ph_lin(-70, wobble=2, seed=4)],
              spacing=10, bands=[(0.04, 0.42), (0.38, 0.7), (0.66, 0.95)])

    # ------------------------------------------------------------ 速度线：杖扫过的扇形里雾被劈开，留下一束同心弧
    ang = np.degrees(np.arctan2(c.yy - gy, c.xx - gx))
    r = np.hypot(c.xx - gx, c.yy - gy)
    fan = smoothstep(SWEEP, SWEEP + 30, ang) * (1 - smoothstep(CANE - 1.5, CANE + 0.5, ang)) * smoothstep(150, 260, r)
    c.erase(fan)
    k = np.clip((ang - SWEEP) / (CANE - SWEEP), 0, 1)               # 0 = 起点，1 = 杖身此刻的位置
    width = 4.2 * k ** 1.6 * smoothstep(200, 420, r)
    jitter = 4 * c.noise(30, 5) * (1 - k)
    arcs = c.lines(r + jitter, 13, width * (0.75 + 0.5 * (0.5 + 0.5 * np.sin(r / 41.0))))
    c.add(arcs * fan)
    # 杖头划过的那一道最重
    c.stroke(ellipse_pts(gx, gy, 820, 820, a0=CANE - 50, a1=CANE - 1.0, n=200), 7, taper=(0.95, 0.0))

    # ------------------------------------------------------------ 杖身：乌木，一道长高光
    a, b = G.p(-200, 0), G.p(900, 0)
    shaft_pts = [(-200, -HALF), (900, -HALF + 3), (900, HALF - 3), (-200, HALF)]
    shaft = c.mask(G(shaft_pts))
    v = c.ph_along(a, b, wobble=0) / HALF                          # -1 … 1，正 = 右下（背光）
    stone = np.clip(0.62 + 0.36 * v - 0.55 * np.exp(-((v + 0.42) / 0.2) ** 2), 0, 1)
    c.engrave(shaft, stone, [c.ph_along(a, b, wobble=0.5, seed=5), c.ph_axis(a, b, bow=8, half=HALF, seed=6)],
              spacing=8, swell=1.0)
    c.stroke(G(poly([shaft_pts[0], shaft_pts[1]])), 4.0, taper=0)
    c.stroke(G(poly([shaft_pts[3], shaft_pts[2]])), 7.0, taper=0)

    # 银杖头：小指下面露出来的一截，带颈的球
    K = Frame(G.p(-205, 0), rot=CANE + 180)
    knob_pts = [(-20, -24), (6, -27), (26, -36), (56, -42), (88, -30), (100, 0), (88, 30), (56, 42), (26, 36), (6, 27), (-20, 24)]
    knob = c.mask(spline(K(knob_pts), closed=True))
    lam = c.relief(knob, radius=30)
    c.engrave(knob, np.clip(0.8 - 0.95 * lam, 0, 1), [c.ph_ring(*K.p(56, 0)), c.ph_along(K.p(0, 0), K.p(90, 0))],
              spacing=7, wmax=2.6)
    c.outline(K.cp(knob_pts), w=STRUCT + 1.5)
    for x in (2, 12):
        c.stroke(K([(x, -27), (x + 2, 0), (x, 27)]), 3, taper=0.2)
    c.erase(c.stroke(spline(K([(40, -26), (58, -32), (76, -24)])), 6, add=False, taper=0.5))

    # ------------------------------------------------------------ 衣袖：厚呢风衣，从左上伸进来
    S = Frame(G.p(17, -140), rot=CANE + 86)                         # 前臂的坐标：x 从袖子指向手腕
    sleeve_pts = [(-760, -250), (-330, -180), (-150, -150, 'c'), (-126, -60), (-118, 40), (-138, 140, 'c'),
                  (-330, 190), (-760, 300)]
    sleeve = c.mask(spline(S(sleeve_pts), closed=True))
    lam = c.relief(sleeve, radius=130, blur=6)
    stone = np.clip(1.0 - 0.75 * lam, 0.42, 1)
    sa, sb = S.p(-700, 0), S.p(-130, 0)
    c.engrave(sleeve, stone, [c.ph_axis(sa, sb, bow=-45, half=200, wobble=1.5, seed=7),
                              c.ph_along(sa, sb, wobble=1.5, seed=8), c.ph_lin(64, seed=9)], spacing=9, swell=1.0)
    for pts, w in (([(-470, -200), (-420, -110), (-410, -10), (-430, 80)], 5.5),
                   ([(-300, -165), (-275, -60), (-285, 40)], 4.5),
                   ([(-600, 270), (-520, 160), (-430, 110), (-330, 128)], 5.5),
                   ([(-250, 165), (-215, 118), (-176, 112)], 3.5)):
        c.line(S.cp(pts), w=w, taper=0.35)
    c.outline(S.cp(sleeve_pts), w=OUTLINE + 1)
    c.line(S.cp([(-162, -140), (-150, -50), (-146, 40), (-160, 128)]), w=STRUCT, taper=0.1)   # 袖口翻边

    cuff_pts = [(-142, -122), (-82, -112), (-74, -30), (-74, 50), (-84, 112), (-140, 124)]
    cuff = c.mask(spline(S(cuff_pts), closed=True))
    c.engrave(cuff, 0.1 + 0.55 * c.grad(*S.p(0, -110), *S.p(0, 120)),
              [c.ph_axis(S.p(-150, 0), S.p(-70, 0), bow=-14, half=110, seed=10)], spacing=8)
    c.outline(S.cp(cuff_pts), w=STRUCT)
    c.dots([S.p(-106, -16)], 7)                                    # 袖扣

    # ------------------------------------------------------------ 手：手腕和手掌（在手指后面）
    palm_pts = [(-92, -70), (-104, -150), (-76, -232), (0, -268), (82, -254), (128, -196), (132, -110), (104, -60)]
    palm = c.mask(spline(G(palm_pts), closed=True))
    hand_back = palm
    lam = c.relief(hand_back, radius=60, blur=4)
    c.engrave(hand_back, np.clip(0.62 - 0.6 * lam + 0.25 * c.grad(*G.p(0, -280), *G.p(0, -60)), 0.06, 1),
              [c.ph_axis(G.p(10, -300), G.p(10, -60), bow=16, half=110, seed=11), c.ph_along(G.p(10, -300), G.p(10, -60), seed=12)],
              spacing=8.5)
    c.outline(G.cp(palm_pts), w=OUTLINE * 0.9)
    c.line(G.cp([(-70, -150), (-40, -200), (20, -222)]), w=2.6, taper=0.4)   # 掌根的肌肉线

    # ------------------------------------------------------------ 四根手指：横着裹住杖身，从杖尾往杖头：小指 → 食指
    fingers = [(-56, 30, 128), (-22, 36, 150), (14, 37, 156), (50, 36, 150)]   # (中心 x, 宽, 长)
    for fx, fw, fl in fingers:
        y0, y1 = -88, -88 + fl
        hw = fw / 2
        pts = [(fx - hw, y0), (fx - hw - 2, y0 + fl * 0.5), (fx - hw + 3, y1 - 14), (fx - 4, y1 + 1), (fx + hw - 4, y1 - 12),
               (fx + hw + 1, y0 + fl * 0.5), (fx + hw, y0)]
        m = c.mask(spline(G(pts), closed=True))
        lam = c.relief(m, radius=hw * 0.9, blur=1.5)
        joint = c.ph_along(G.p(0, y0), G.p(1, y0), wobble=0)            # 沿手指的位置
        tone = np.clip(0.55 - 0.6 * lam + 0.18 * smoothstep(y0 + 70, y1, joint), 0.04, 1)
        c.engrave(m, tone, [c.ph_axis(G.p(fx, y0), G.p(fx, y1), bow=-7, half=hw, seed=13),
                            c.ph_along(G.p(fx, y0), G.p(fx, y1), seed=14)], spacing=7.5, wmax=2.6, swell=0.8)
        c.outline(G.cp(pts), w=STRUCT + 0.5, light=0.5)
        # 指节横纹：中节和末节的褶
        for jy, ln in ((y0 + fl * 0.42, 0.7), (y0 + fl * 0.78, 0.55)):
            c.line(G.cp([(fx - hw * ln, jy - 2), (fx, jy + 3), (fx + hw * ln, jy - 1)]), w=2.2, taper=0.35)
        # 指甲：指尖露出一点
        c.line(G.cp([(fx - hw * 0.45, y1 - 16), (fx, y1 - 22), (fx + hw * 0.45, y1 - 17)]), w=2.0, taper=0.3)
    # 指根一排关节的阴影（压在手掌上）
    c.line(G.cp([(-84, -86), (-40, -92), (10, -94), (60, -92), (78, -88)]), w=5, taper=0.15)

    # ------------------------------------------------------------ 拇指：从掌根斜压到食指上
    thumb_pts = [(70, -160), (112, -150), (128, -88), (118, -20), (98, 34), (74, 46), (60, 26), (70, -40), (64, -110)]
    thumb = c.mask(spline(G(thumb_pts), closed=True))
    lam = c.relief(thumb, radius=22, blur=2)
    c.engrave(thumb, np.clip(0.6 - 0.65 * lam, 0.04, 1),
              [c.ph_axis(G.p(95, -160), G.p(86, 40), bow=-8, half=26, seed=16), c.ph_along(G.p(95, -160), G.p(86, 40), seed=17)],
              spacing=7.5, swell=0.8)
    c.outline(G.cp(thumb_pts), w=STRUCT + 1, light=0.5)
    c.line(G.cp([(76, 4), (88, 14), (104, 12)]), w=2.2, taper=0.3)     # 拇指甲
    c.line(G.cp([(80, -60), (96, -50), (114, -54)]), w=2.2, taper=0.3)  # 拇指关节
