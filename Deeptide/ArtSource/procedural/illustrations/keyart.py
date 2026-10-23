"""主视觉（策划/10 §8：「调查员站在栈桥尽头，雾里有一个巨大的轮廓」），3840 × 2160，标题画面的背景。

栈桥从左下角斜着伸进海里，尽头在画面中间偏右；调查员站在尽头，背对我们，手里提着灯，灯光在雾里晕开一团。
他面前的海上，雾里升起一个巨大的东西：圆顶的头、垂下来的一把触须、背后隆起的肩，被雾吃掉了下半截，只剩中灰的排线；
两只眼是细长的一道深渊青（深渊遮罩，任何时候都显示）。左边和左上是大片的雾，给标题和按钮留位置。
癫狂层：它身上睁开更多的眼，海面下伸出几条触须缠向栈桥。
坐标按 1920 × 1080 写，画的时候 × 2；线距、线宽也按 2 倍画，显示时缩小一半，和事件插图的粗细一致。
"""
import math

import numpy as np

from etch import Frame, ellipse_pts, poly, resample, smoothstep, spline
from cards._common import eye, tentacle
from illustrations._kit import investigator

NAME = '主视觉'
SIZE = (3840, 2160)
K = 2.0
HORIZON = 700 * K
GAP = 14.0
HW = 4.4
LINE = 9.0


def P(pts):
    return [(x * K, y * K) + tuple(p[2:]) for p in pts for x, y in [p[:2]]]


def draw(c):
    rng = np.random.default_rng(11)
    W, H = c.w, c.h

    # ------------------------------------------------------------ 雾天：左下最亮，右上压暗
    sky = c.rect(0, 0, W, HORIZON + 10)
    tone = 0.12 + 0.45 * c.grad(0, HORIZON, W * 0.9, 0) + 0.06 * c.noise(400, 1)
    c.engrave(sky, np.clip(tone, 0, 1), [c.ph_wave(-2, 14, 1500, wobble=8, seed=2), c.ph_lin(32, wobble=5, seed=3)], spacing=GAP,
              wmax=HW, bands=[(0.05, 0.6), (0.5, 0.95)], solid=None)

    # ------------------------------------------------------------ 雾里的巨物：背后隆起的肩、圆顶的头、垂下来的触须
    hump = P([(1060, 700), (1120, 520), (1260, 400), (1460, 360), (1660, 400), (1800, 520), (1860, 700)])
    hm = c.mask(spline(hump, closed=True))
    c.engrave(hm, np.clip(0.42 + 0.08 * c.noise(120, 4), 0, 1), [c.ph_contour(hm, blur=12) + 6 * c.noise(80, 5), c.ph_lin(60, seed=6)],
              spacing=GAP, wmax=HW, solid=None)
    c.stroke(spline(hump[1:-1]), LINE * 0.7, taper=0.2)
    head = P([(1300, 480), (1310, 320), (1380, 200), (1470, 160), (1560, 200), (1630, 320), (1640, 480), (1560, 560), (1380, 560)])
    hd = c.mask(spline(head, closed=True))
    lam = c.relief(hd, radius=160, blur=8)
    c.engrave(hd, np.clip(0.62 - 0.35 * lam + 0.06 * c.noise(60, 7), 0, 1),
              [c.ph_contour(hd, blur=10) + 4 * c.noise(60, 8), c.ph_lin(40, seed=9)], spacing=GAP, wmax=HW, solid=None)
    c.outline(head, w=LINE, light=0.4)
    for k in range(8):                                                     # 脸上垂下来的一把触须
        x0 = (1360 + k * 34) * K
        bend = (k - 3.5) * 22
        ctrl = [(x0, 520 * K), (x0 + bend * K * 0.5, 600 * K), (x0 + bend * K, 680 * K), (x0 + bend * K * 1.3 - 20 * K, 760 * K)]
        tentacle(c, ctrl, 46 * K * (1 - abs(k - 3.5) * 0.06), tone=0.6, taper=0.85, outline=5.0, suckers=False)
    for sd in (-1, 1):                                                     # 眼：细长的一道，着深渊青
        ex, ey = (1470 + sd * 86) * K, 400 * K
        slit = c.ellipse(ex, ey, 46 * K, 9 * K, rot=sd * -12)
        c.fill(slit, value=1.0)
        c.add(c.ellipse(ex, ey, 40 * K, 6 * K, rot=sd * -12), 'abyss')
    # 雾吃掉巨物的下半截：横着的几条雾带，把墨擦淡
    for k, (y, a) in enumerate(((640, 0.7), (580, 0.45), (690, 0.85))):
        band = 1 - smoothstep(0, 60 * K, np.abs(c.yy - y * K + 30 * K * c.noise(300, 20 + k)))
        c.erase(band * a * smoothstep(700 * K, 1050 * K, c.xx))

    # ------------------------------------------------------------ 海
    sea = c.rect(0, HORIZON, W, H)
    near = c.grad(0, HORIZON, 0, H)
    d0 = 260.0
    ph = GAP * d0 / 12 * np.log(np.maximum(c.yy - HORIZON + d0, 1) / d0) + 1.2 * np.sin(c.xx / 260 + c.noise(300, 21))
    c.engrave(sea, np.clip(0.4 + 0.4 * near + 0.08 * c.noise(160, 22), 0, 1), [ph, c.ph_lin(-58, seed=23)], spacing=GAP, wmax=HW)
    c.erase((1 - smoothstep(0, 140 * K, c.yy - HORIZON)) * 0.6 * (c.yy > HORIZON))      # 海面上贴着一层雾
    c.stroke([(0, HORIZON), (W, HORIZON)], 5, taper=0)

    # ------------------------------------------------------------ 栈桥：从左下伸到画面中间偏右
    end_l, end_r = (1130 * K, 752 * K), (1240 * K, 764 * K)
    near_l, near_r = (-40 * K, 1010 * K), (640 * K, 1110 * K)
    deck = [near_l, end_l, end_r, near_r]
    dm = c.mask(poly(deck))
    t_along = np.clip(((c.xx - near_l[0]) * (end_l[0] - near_l[0]) + (c.yy - near_l[1]) * (end_l[1] - near_l[1]))
                      / ((end_l[0] - near_l[0]) ** 2 + (end_l[1] - near_l[1]) ** 2), 0, 1)
    c.engrave(dm, np.clip(0.55 + 0.3 * (1 - t_along) + 0.06 * c.noise(40, 24), 0, 1),
              [c.ph_along(near_l, end_l, seed=25), c.ph_lin(70, seed=26)], spacing=GAP, wmax=HW)
    for k in range(1, 40):                                                 # 木板的缝：越远越密
        t = 1 - (1 - k / 40) ** 1.6
        a = (near_l[0] + (end_l[0] - near_l[0]) * t, near_l[1] + (end_l[1] - near_l[1]) * t)
        b = (near_r[0] + (end_r[0] - near_r[0]) * t, near_r[1] + (end_r[1] - near_r[1]) * t)
        c.stroke([a, b], 3.4 * (1 - t) + 1.2, taper=0)
    c.stroke([near_l, end_l], LINE, taper=0)
    c.stroke([near_r, end_r], LINE * 1.2, taper=0)
    for k in range(9):                                                     # 桥桩：从桥面右边插进水里
        t = 1 - (1 - k / 9) ** 1.5
        x = near_r[0] + (end_r[0] - near_r[0]) * t
        y = near_r[1] + (end_r[1] - near_r[1]) * t
        ln = (240 * (1 - t) + 40) * K
        wd = (26 * (1 - t) + 6) * K
        pile = c.rect(x - wd / 2, y, x + wd / 2, y + ln)
        c.engrave(pile, 0.85, [c.ph_lin(90, seed=27 + k)], spacing=GAP * 0.8, wmax=HW)
        c.stroke(poly([(x - wd / 2, y), (x - wd / 2, y + ln)]), 3, taper=0)
        c.erase(c.stroke([(x - wd, y + ln), (x + wd, y + ln)], 3, add=False, taper=0.4))   # 桩脚的水纹
    for k in range(10):                                                    # 左边一排系缆的短桩和一根松垂的缆绳
        t = 1 - (1 - k / 10) ** 1.5
        x = near_l[0] + (end_l[0] - near_l[0]) * t
        y = near_l[1] + (end_l[1] - near_l[1]) * t
        ph_ = (90 * (1 - t) + 18) * K
        c.stroke([(x, y), (x, y - ph_)], (16 * (1 - t) + 4) * K, taper=0)
        if k:
            px, py, pph = prev
            c.stroke(spline([(px, py - pph * 0.9), ((px + x) / 2, (py + y) / 2 - (pph + ph_) * 0.25), (x, y - ph_ * 0.9)]), 2.4, taper=0)
        prev = (x, y, ph_)

    # ------------------------------------------------------------ 调查员：站在尽头，提灯的光在雾里晕开
    fx, fy = 1190 * K, 758 * K
    lamp = (fx + 56, fy - 90)                                              # 提灯（investigator 的 lantern_side=1）
    c.erase((1 - c.radial(*lamp, 10 * K, 170 * K)) * 0.8)
    investigator(c, Frame((fx, fy), scale=0.45 * K), tone=0.98, lantern_side=1)

    # ============================================================ 癫狂层：它身上更多的眼，水下伸出来的触须
    for x, y, s in ((1380, 300, 20), (1560, 300, 20), (1470, 230, 26), (1200, 520, 16), (1720, 520, 16), (1300, 420, 14), (1640, 430, 14)):
        eye(c, x * K, y * K, s * K, layer='mad', look=-0.4, width=5.0, slit=True)
    for ctrl, w in (([(900, 1080), (980, 900), (1060, 820), (1120, 790)], 60), ([(1500, 1080), (1420, 920), (1330, 840), (1270, 800)], 70),
                    ([(300, 1080), (420, 960), (560, 930), (640, 900)], 50)):
        tentacle(c, [(x * K, y * K) for x, y in ctrl], w * K, layer='mad', tone=0.5, taper=0.9, outline=5.0)
