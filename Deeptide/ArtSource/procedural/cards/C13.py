"""C13 惊叫：张大嘴尖叫的人。癫狂层：声波纹里浮出许多脸（策划/10 §4.6）。

侧脸朝右，下巴掉下来、嘴张到最大，眼睛瞪圆，头发炸开；声波从嘴里一圈圈向右扩散。
声波之间是留白的纸，癫狂层的脸就浮在这些空隙里。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, smoothstep, spline

NAME = '惊叫'

MOUTH = (0, 0)


def draw(c):
    F = Frame((330, 420), rot=-6, scale=1.0)        # 头的坐标：原点在耳朵附近
    mx, my = F.p(200, 92)                            # 嘴

    # ------------------------------------------------------------ 背景：头后面暗，前面亮
    bg = np.ones((c.h, c.w), np.float32)
    c.engrave(bg, 0.12 + 0.75 * c.grad(800, 0, 100, 0) ** 1.4 + 0.05 * c.noise(100, 1),
              [c.ph_lin(80, wobble=2, seed=2), c.ph_lin(20, seed=3), c.ph_lin(-40, seed=4)], spacing=10, swell=1.1)

    # ------------------------------------------------------------ 声波：以嘴为圆心的一束弧，粗细交替，越远越淡
    ang = np.degrees(np.arctan2(c.yy - my, c.xx - mx))
    r = np.hypot(c.xx - mx, c.yy - my)
    fan = smoothstep(-62, -50, ang) * (1 - smoothstep(50, 62, ang)) * smoothstep(40, 80, r)
    c.erase(fan * 0.92)
    wob = 6 * np.sin(np.radians(ang) * 9 + r / 40)
    band = (r + wob) / 70.0
    k = np.floor(band)
    w = np.where(k % 2 == 0, 7.0, 3.0) * (1 - smoothstep(200, 900, r) * 0.6)
    waves = c.lines(r + wob, 35, w * smoothstep(60, 120, r))
    c.add(waves * fan)

    head(c, F)

    # ============================================================ 癫狂层：声波纹里浮出的脸
    rng = np.random.default_rng(12)
    for rr, aa, sz in ((260, -28, 34), (300, 22, 40), (400, -8, 52), (470, -40, 40), (500, 34, 46), (620, 4, 60),
                       (650, -34, 44), (700, 38, 50), (820, -14, 56), (860, 26, 42)):
        x = mx + rr * math.cos(math.radians(aa))
        y = my + rr * math.sin(math.radians(aa))
        ghost_face(c, x, y, sz, rng)


def head(c, F):
    prof = [(-60, -300), (60, -290), (130, -230), (160, -150), (156, -110), (172, -76), (200, -30), (220, 4), (206, 14),
            (198, 30), (206, 52), (214, 70, 'c'), (170, 100), (160, 170), (192, 220, 'c'), (196, 244), (186, 290),
            (146, 318), (106, 318), (90, 350), (90, 460), (-180, 460), (-170, 200), (-200, 40), (-190, -150), (-140, -260)]
    m = c.mask(spline(F(prof), closed=True))
    side = c.grad(*F.p(220, 0), *F.p(-180, 0))
    lam = c.relief(m, radius=70, blur=5)
    c.engrave(m, np.clip(0.05 + 0.4 * side + 0.3 * (1 - lam) ** 1.4, 0, 1),
              [c.ph_axis(F.p(0, -300), F.p(0, 440), bow=-30, half=180, seed=5), c.ph_lin(F.angle(65), seed=6),
               c.ph_lin(F.angle(-25), seed=7)], spacing=8.5, swell=1.0)
    # 嘴：一个大黑洞，上排牙、舌头
    mouth = [(214, 70, 'c'), (170, 100), (160, 170), (192, 220, 'c'), (160, 206), (128, 160), (136, 96)]
    mm = c.mask(spline(F(mouth), closed=True))
    c.fill(mm)
    teeth = [(206, 72), (180, 86), (166, 90), (168, 102), (184, 100), (208, 84)]
    c.clear(c.mask(spline(F(teeth), closed=True)))
    c.outline(F.cp(teeth), w=2.4, light=0)
    for x in (178, 190, 200):
        c.stroke(F([(x, 84 + (200 - x) * 0.12), (x - 2, 98 - (x - 170) * 0.2)]), 1.8, taper=0)
    tongue = [(186, 214), (152, 196), (140, 172), (162, 184), (190, 204)]
    c.engrave(c.mask(spline(F(tongue), closed=True)), 0.55, [c.ph_lin(F.angle(30))], spacing=5, wmax=2)
    # 轮廓：额头—鼻子—上唇，下唇—下巴—脖子
    c.stroke(F(spline(prof[:12])), OUTLINE, taper=(0.3, 0.0), light=0.4)
    c.stroke(F(spline(prof[13:20])), OUTLINE, taper=(0.0, 0.3), light=0.4)
    c.line(F.cp([(200, -30), (186, -22), (180, -6), (196, 0)]), w=3.4, taper=0.3)     # 鼻翼
    c.line(F.cp([(176, 20), (150, 50), (140, 90)]), w=3.0, taper=0.4)                 # 法令纹，拉得很深
    c.line(F.cp([(156, 226), (138, 258), (148, 290)]), w=2.4, taper=0.4)
    # 眼：瞪圆，眉毛挑起
    ex, ey = 128, -96
    eye = [(ex - 26, ey + 2), (ex + 2, ey - 24), (ex + 26, ey - 6), (ex + 24, ey + 10), (ex, ey + 18)]
    c.clear(c.mask(spline(F(eye), closed=True)))
    c.outline(F.cp(eye), w=3.6, light=0.3)
    ix, iy = F.p(ex + 10, ey - 3)
    c.add(c.ellipse(ix, iy, 9, 9))
    c.erase(c.ellipse(ix - 2, iy - 3, 2.5, 2.5))
    c.line(F.cp([(ex - 40, ey - 52), (ex - 6, ey - 74), (ex + 30, ey - 58)]), w=6, taper=0.3)
    for y in (-180, -160, -140):                                                       # 额头的皱纹
        c.line(F.cp([(60, y), (100, y - 10), (136, y + 2)]), w=2.2, taper=0.4)
    # 耳朵：C 形的耳轮、中间黑的耳甲腔、前面一个小耳屏
    ear = [(-30, -86), (0, -92), (18, -66), (14, -30), (6, 0), (4, 24), (-8, 48), (-28, 50), (-38, 32), (-34, 10), (-46, -20),
           (-52, -60)]
    em = c.mask(spline(F(ear), closed=True))
    c.engrave(em, 0.22 + 0.2 * c.grad(*F.p(-50, 0), *F.p(18, 0)), [c.ph_lin(F.angle(80))], spacing=7, wmax=2.2)
    c.outline(F.cp(ear), w=STRUCT, light=0.4)
    c.line(F.cp([(-26, -74), (-2, -74), (6, -46), (-4, -16)]), w=3.0, taper=0.3)    # 耳轮的内沿
    concha = [(-26, -40), (-8, -48), (0, -24), (-8, 2), (-24, 0), (-30, -20)]
    c.fill(c.mask(spline(F(concha), closed=True)), value=0.92)
    c.line(F.cp([(-38, -6), (-30, 4), (-36, 18)]), w=2.6, taper=0.3)                # 耳屏

    # 头发：盖住头顶和后脑的一团，发梢向后上方炸开
    hair = [(70, -282), (110, -250), (60, -236), (10, -200), (-20, -130), (-60, -90), (-110, -40), (-170, 10), (-196, -60),
            (-200, -170), (-150, -260), (-60, -300)]
    hm = c.mask(spline(F(hair), closed=True))
    flow = c.ph_contour(hm, blur=5) + 3 * c.noise(25, 9)
    c.engrave(hm, 0.82 + 0.1 * c.noise(20, 10), [flow, c.ph_lin(F.angle(50), seed=11), c.ph_lin(F.angle(-40), seed=12)],
              spacing=7.5, swell=1.3)
    rng = np.random.default_rng(3)
    for k in range(90):
        t = rng.uniform(0, 1)
        a = math.radians(-20 - 200 * t)                                   # 从前额沿头顶绕到后脑
        root = F.p(-60 + 130 * math.cos(a), -150 + 140 * math.sin(a))
        out = math.radians(F.rot) + a + rng.uniform(-0.5, 0.2)
        ln = rng.uniform(60, 190) * (0.6 + 0.6 * t)
        tip = (root[0] + ln * math.cos(out), root[1] + ln * math.sin(out))
        mid = ((root[0] + tip[0]) / 2 + rng.uniform(-24, 24), (root[1] + tip[1]) / 2 + rng.uniform(-24, 24))
        c.stroke(spline([root, mid, tip]), rng.uniform(2.5, 5.5), taper=(0.0, 0.9))


def ghost_face(c, x, y, s, rng):
    """声波里浮出的脸：一圈脸的轮廓，两个黑眼窝，一张张大的嘴——也在尖叫。"""
    tilt = rng.uniform(-20, 20)
    G = Frame((x, y), rot=tilt, scale=s / 50)
    outline = [(0, -56), (34, -44), (42, -6), (30, 36), (0, 56), (-30, 36), (-42, -6), (-34, -44)]
    m = c.mask(spline(G(outline), closed=True))
    c.engrave(m * c.half_plane(G.p(30, -60), G.p(-30, 60)), 0.3, [c.ph_lin(tilt + 60)], layer='mad', spacing=6, wmax=1.8,
              grain=0)
    c.stroke(spline(G(outline), closed=True), 2.6, layer='mad', closed=True, taper=0)
    k = s / 50
    for sd in (-1, 1):
        c.add(c.ellipse(*G.p(sd * 16, -12), 7 * k, 10 * k, rot=tilt), 'mad')
        c.stroke(spline(G([(sd * 30, -30), (sd * 16, -26), (sd * 4, -32)])), 2.0, layer='mad', taper=0.3)
    c.add(c.ellipse(*G.p(0, 26), 9 * k, 16 * k, rot=tilt), 'mad')
