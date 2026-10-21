"""C11 恐怖低语：凑近耳边的嘴，吐出一条墨字飘带（策划/10 §4.6）。

左边是一张从黑暗里凑过来的侧脸（只看得到鼻尖、嘴唇和下巴，上半张脸隐在暗里），嘴唇半张；
右边是一只很大的耳朵。嘴里吐出一条卷曲的纸带，带上写满字，绕着耳廓钻进耳道。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, offset, poly, resample, scrawl, smoothstep, spline

NAME = '恐怖低语'


def draw(c):
    # ------------------------------------------------------------ 背景：左暗右亮
    bg = np.ones((c.h, c.w), np.float32)
    c.engrave(bg, 0.2 + 0.7 * c.grad(1000, 400, 200, 400) ** 1.3 + 0.05 * c.noise(100, 1),
              [c.ph_lin(-60, wobble=2, seed=2), c.ph_lin(30, seed=3), c.ph_lin(-15, seed=4)], spacing=10, swell=1.1)

    whisperer(c, Frame((250, 420)))
    ear(c, Frame((960, 430), scale=1.15))
    ribbon(c)


def whisperer(c, F):
    """凑过来的侧脸，朝右：鼻子、嘴唇、下巴；眼睛以上没进黑暗。"""
    prof = [(-260, -420), (40, -420), (60, -300), (70, -220), (110, -150), (150, -96), (140, -80), (104, -72), (100, -50),
            (126, -34), (124, -20), (98, -12), (108, 6), (96, 22), (60, 34), (66, 70), (40, 110), (-20, 130), (-60, 220),
            (-260, 260)]
    m = c.mask(spline(F(prof), closed=True))
    dark = c.grad(*F.p(0, 40), *F.p(0, -260))                               # 越往上越黑
    lam = c.relief(m, radius=70, blur=5)
    c.engrave(m, np.clip(0.25 + 0.75 * dark ** 1.2 + 0.25 * (1 - lam), 0, 1),
              [c.ph_axis(F.p(0, -400), F.p(0, 260), bow=-30, half=200, seed=5), c.ph_lin(60, seed=6), c.ph_lin(-30, seed=7)],
              spacing=8.5, swell=1.1)
    c.stroke(F(spline(prof[3:18])), OUTLINE, taper=(0.4, 0.1), light=0.4)
    c.line(F.cp([(104, -72), (80, -66), (72, -80)]), w=3.4, taper=0.3)      # 鼻翼
    c.line(F.cp([(124, -24), (96, -18), (82, -22)]), w=4.6, taper=0.2)      # 唇缝
    c.line(F.cp([(96, 22), (70, 30), (54, 22)]), w=2.4, taper=0.4)          # 下唇下的阴影
    c.erase(c.stroke(spline(F([(112, -36), (122, -30)])), 3, add=False, taper=0.5))


def ear(c, F):
    outer = [(-40, -170), (40, -200), (110, -160), (130, -60), (100, 20), (60, 70), (50, 130), (20, 180), (-30, 186),
             (-56, 140), (-60, 60), (-80, -40), (-76, -130)]
    m = c.mask(spline(F(outer), closed=True))
    lam = c.relief(m, radius=50, blur=3)
    c.engrave(m, np.clip(0.3 - 0.35 * lam + 0.12 * c.grad(*F.p(-80, 0), *F.p(130, 0)), 0, 1),
              [c.ph_contour(m, blur=4), c.ph_lin(70, seed=8)], spacing=9, wmax=2.4)
    # 耳轮的内沿、对耳轮、耳屏、耳道
    c.line(F.cp([(-46, -140), (20, -170), (88, -140), (104, -60), (78, 10), (40, 60)]), w=4.2, taper=0.2)
    c.line(F.cp([(-20, -100), (30, -110), (60, -70), (54, -10), (20, 40), (0, 90)]), w=3.6, taper=0.25)
    canal = [(-40, -10), (-6, -30), (14, 10), (0, 50), (-34, 44)]
    cm = c.mask(spline(F(canal), closed=True))
    c.engrave(cm, 0.95, [c.ph_lin(30), c.ph_lin(-40)], spacing=6, swell=1.2)
    c.outline(F.cp(canal), w=STRUCT, light=0.3)
    c.line(F.cp([(-66, 40), (-46, 66), (-50, 96)]), w=3.4, taper=0.3)       # 耳屏
    c.outline(F.cp(outer), w=OUTLINE, light=0.4)


def ribbon(c):
    """从嘴里吐出来的纸带：卷两圈，最后钻进耳道。带子正面是浅色、写满字，翻过来的背面是排线。"""
    path = spline([(390, 400), (470, 330), (560, 300), (640, 360), (620, 460), (540, 470), (530, 400), (600, 340),
                   (720, 300), (820, 330), (860, 400), (900, 430), (930, 440)], step=1.5)
    path = resample(path, 2.0)
    n = len(path)
    u = np.linspace(0, 1, n)
    width = 100 * (1 - 0.65 * u) * (0.35 + 0.65 * smoothstep(0, 0.08, u))
    twist = np.cos(u * math.pi * 5.0)                                       # 带子扭转：正负号 = 正面 / 背面
    half = width / 2 * np.abs(twist) ** 0.6 + 2
    t = np.gradient(path, axis=0)
    t /= np.hypot(t[:, 0], t[:, 1])[:, None]
    nn = np.stack([-t[:, 1], t[:, 0]], 1)
    left = path + nn * half[:, None]
    right = path - nn * half[:, None]
    # 一段一段画，后画的盖住先画的（带子自己交叠时近处的在上面）
    seg = 14
    for i in range(0, n - 1, seg):
        j = min(i + seg + 1, n)
        q = np.vstack([left[i:j], right[i:j][::-1]])
        m = c.mask(q)
        front = twist[(i + j) // 2] > 0
        if front:
            c.fill(m, value=0.0)
            # 字：顺着带子写的一行连笔小字（带圈的摆线），每隔一段断开成「词」
            hh = half[(i + j) // 2]
            if hh > 10:
                ss = np.arange(i, j - 1)
                arc = ss * 2.0
                r = hh * 0.32
                k = 2 * math.pi / 11.0
                word = (np.sin(arc / 34.0) > -0.75)
                pts = path[ss] + t[ss] * (-r * 0.8 * np.sin(k * arc))[:, None] + nn[ss] * (r * np.cos(k * arc))[:, None]
                run = []
                for p, wv in zip(pts, word):
                    if wv:
                        run.append(p)
                    elif len(run) > 3:
                        c.stroke(np.array(run), 2.0, taper=0.1, step=1.0)
                        run = []
                if len(run) > 3:
                    c.stroke(np.array(run), 2.0, taper=0.1, step=1.0)
        else:
            c.engrave(m, 0.7, [c.ph_along(tuple(path[i]), tuple(path[j - 1]), wobble=0.3)], spacing=5.5, wmax=2.2)
        c.stroke(left[i:j], 3.0, taper=0)
        c.stroke(right[i:j], 3.0, taper=0)
