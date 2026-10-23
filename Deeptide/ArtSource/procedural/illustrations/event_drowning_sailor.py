"""事件 2 溺水的水手：浪里有人在挣扎，他的手指上缠着海草，口袋鼓鼓囊囊（Data/events.csv）。

风暴里的海占满画面：上面一条压得很低的乌云天，左边一道立起来的大浪，右中是水手——头和肩露出水面，
一只手高高举起，手指上缠着垂下来的海草，另一只手扒着一块漂着的木板。他身下的水最深（深渊遮罩）。
癫狂层：水下伸上来几只苍白的手抓着他，大浪的浪面上睁开一只眼。
"""
import math

import numpy as np

from etch import Frame, ellipse_pts, poly, resample, smoothstep, spline
from cards._common import eye
from illustrations._kit import GAP, OUT, STRUCT, hatch, night_sky, sea

NAME = '溺水的水手'
SIZE = (1920, 1280)
HORIZON = 330


def draw(c):
    rng = np.random.default_rng(2)
    W, H = c.w, c.h

    # ------------------------------------------------------------ 天：低压的乌云，几道斜雨
    night_sky(c, HORIZON + 4, top=0.86, bottom=0.55, seed=3)
    for k in range(5):                                                       # 云底：几条压暗的云带
        y = 60 + k * 55 + rng.uniform(-10, 10)
        band = c.smask([(-20, y), (500, y - 30), (1100, y - 10), (1700, y - 34), (1960, y), (1960, y + 30), (1100, y + 26),
                        (400, y + 34), (-20, y + 26)])
        hatch(c, band, 0.8, [c.ph_wave(-3, 6, 300, seed=10 + k), c.ph_lin(30, seed=20 + k)], spacing=8)
    for _ in range(70):                                                      # 雨：刻出来的白色斜线
        x, y = rng.uniform(0, W), rng.uniform(0, HORIZON - 20)
        c.erase(c.stroke([(x, y), (x - 40, y + 110)], 1.8, add=False, taper=0.3))

    # ------------------------------------------------------------ 海：远处平，近处起伏大
    sea(c, HORIZON, H, tone=0.68, seed=4, glints=40, rng=rng)
    c.stroke([(0, HORIZON), (W, HORIZON + 6)], 3, taper=0)
    # 近处的大波谷：几道横贯画面的浪脊，脊上一排白沫
    for k, y in enumerate((620, 860, 1110)):
        ridge = spline([(-20, y + 30), (300, y - 20), (700, y + 10), (1100, y - 30), (1500, y + 20), (1960, y - 10)])
        below = c.mask(np.vstack([ridge, [(1960, y + 160), (-20, y + 160)]]))
        hatch(c, below, 0.68 + 0.08 * k, [c.ph_contour(below, blur=6) + 2 * c.noise(40, 30 + k), c.ph_lin(-55, seed=40 + k)],
              spacing=8 + 2 * k, swell=1.2)
        c.stroke(ridge, 4 + k, taper=0)
        for p in resample(ridge, 26)[::2]:
            r = rng.uniform(4, 9) * (1 + 0.4 * k)
            c.clear(c.ellipse(p[0] + rng.uniform(-6, 6), p[1] - r * 0.6, r * 1.4, r))
            c.stroke(ellipse_pts(p[0], p[1] - r * 0.6, r * 1.4, r), 1.8, closed=True)

    # 他身后一块被云缝里的光照亮的水面：把墨擦淡，人物就从亮处跳出来
    c.erase(0.55 * (1 - c.radial(1200, 560, 0, 460, sx=1.7, sy=1.0)) * (c.yy > HORIZON))

    # ------------------------------------------------------------ 左边立起来的大浪
    crest = [(-20, H), (-20, 380), (120, 300), (300, 270), (470, 300), (600, 370), (650, 430), (610, 470), (560, 440),
             (520, 470), (560, 560), (600, 720), (620, 900), (560, 1100), (520, H)]
    wave = c.mask(spline(crest, closed=True))
    lit = c.grad(0, 600, 640, 600)
    hatch(c, wave, np.clip(0.6 + 0.3 * lit + 0.08 * c.noise(50, 5), 0, 1),
          [c.ph_contour(wave, blur=9) + 3 * c.noise(40, 6), c.ph_lin(70, seed=7), c.ph_lin(-30, seed=8)], spacing=8.5, swell=1.2)
    c.stroke(spline(crest[1:-1]), OUT, taper=(0.1, 0.3), light=0.3)
    lip = resample(spline([(300, 270), (470, 300), (600, 370), (650, 430)]), 4)
    for i in range(0, len(lip), 6):                                         # 浪尖的碎沫
        p = lip[i]
        for _ in range(3):
            q = p + rng.normal(0, 16, 2) + np.array([20, -16])
            r = rng.uniform(4, 10)
            c.clear(c.ellipse(q[0], q[1], r, r))
            c.stroke(ellipse_pts(q[0], q[1], r, r), 1.8, closed=True)

    # ------------------------------------------------------------ 水手：身下最深的水（深渊遮罩）
    deep = c.ellipse(1160, 860, 360, 120) * (c.yy > 760)
    c.add(deep * (0.55 + 0.35 * smoothstep(0.0, 0.6, c.noise(30, 9))) * c.lines(c.ph_wave(0, 5, 200, seed=10), GAP, 3.0), 'abyss')
    sailor(c, rng)

    # ============================================================ 癫狂层：水下伸上来的手，浪面上的眼
    for (x, y), ang, s in (((960, 900), -70, 1.0), ((1320, 920), -105, 1.1), ((1100, 1000), -88, 1.3), ((1460, 840), -125, 0.8)):
        mad_hand(c, x, y, ang, s)
    eye(c, 300, 620, 80, layer='mad', look=0.6, width=6.0, lash=True)


def sailor(c, rng):
    """头、肩露出水面；右手（画面右）高举缠着海草，左手扒着木板。"""
    # 木板：斜着漂在他左边
    plank = c.mask(poly([(800, 760), (1060, 700), (1074, 740), (814, 802)]))
    lam = c.relief(plank, radius=10)
    hatch(c, plank, np.clip(0.65 - 0.3 * lam, 0, 1), [c.ph_along((800, 760), (1060, 700), seed=50)], spacing=7.5)
    c.outline([(800, 760), (1060, 700, 'c'), (1074, 740, 'c'), (814, 802, 'c')], w=STRUCT)
    # 肩和湿透的衣服
    body = [(1040, 820), (1060, 740), (1120, 700), (1220, 700), (1280, 730), (1310, 800), (1300, 840)]
    bm = c.mask(spline(body, closed=True))
    hatch(c, bm, 0.85, [c.ph_lin(80, seed=51), c.ph_lin(20, seed=52)], swell=1.2)
    c.outline(body, w=STRUCT + 1, light=0.3)
    # 扒着木板的左手：从肩伸到木板上
    c.line([(1080, 740), (1010, 740), (960, 742)], w=44, taper=(0.0, 0.1))
    c.erase(c.stroke(spline([(1080, 724), (1010, 722), (962, 726)]), 4, add=False, taper=0.3))
    for k in range(4):
        c.line([(962, 730 + k * 9), (928 - k * 2, 726 + k * 12), (920 - k * 2, 742 + k * 10)], w=10, taper=(0.0, 0.4))
    # 头：仰着，侧脸朝左上，湿头发贴在头上
    head = [(1120, 690), (1110, 620), (1140, 580), (1200, 572), (1240, 604), (1250, 660), (1220, 700), (1170, 712)]
    hm = c.mask(spline(head, closed=True))
    lam = c.relief(hm, radius=40, blur=3)
    hatch(c, hm, np.clip(0.7 - 0.6 * lam, 0.05, 1), [c.ph_lin(72, seed=53), c.ph_lin(12, seed=59)], spacing=7.5)
    c.outline(head, w=STRUCT + 0.5, light=0.4)
    hair = c.mask(spline([(1140, 590), (1180, 566), (1240, 590), (1252, 650), (1230, 640), (1200, 600), (1160, 600)], closed=True))
    hatch(c, hair, 0.95, [c.ph_lin(60, seed=54), c.ph_lin(-20, seed=55)], spacing=7)
    c.line([(1124, 640), (1112, 650), (1122, 664)], w=3)                     # 鼻子
    c.line([(1128, 682), (1144, 676)], w=4, taper=0.2)                       # 张着的嘴
    c.fill(c.ellipse(1140, 626, 8, 5), value=1.0)                            # 眼睛（仰着看天）
    # 举起的右手：前臂从水里伸上去，手张开，手指上缠着海草
    arm = spline([(1240, 720), (1290, 600), (1320, 470), (1330, 380)])
    am = c.stroke(arm, 40, add=False, taper=0, profile=lambda u: 1 - 0.25 * u)
    lam = c.relief(am, radius=16, blur=2)
    hatch(c, am, np.clip(0.8 - 0.55 * lam, 0.15, 1), [c.ph_axis((1240, 720), (1330, 380), bow=10, half=20, seed=56)], spacing=7)
    for sd in (-1, 1):
        from etch import offset
        c.stroke(offset(arm, (sd * 20 * np.linspace(1, 0.75, len(arm)))[:, None]), 3.5 + (1.5 if sd > 0 else 0), taper=(0.0, 0.1))
    palm = c.ellipse(1332, 352, 34, 40, rot=8)
    c.clear(palm)
    hatch(c, palm, 0.35 + 0.4 * c.grad(1300, 320, 1360, 390), [c.ph_lin(30, seed=57)], spacing=7)
    c.stroke(ellipse_pts(1332, 352, 34, 40, rot=8), 3.5, closed=True)
    tips = []
    for k, a in enumerate((-150, -118, -92, -66, -30)):                     # 张开的五指
        ang = math.radians(a)
        ln = 70 if k in (1, 2, 3) else 52
        p0 = (1332 + 26 * math.cos(ang), 352 + 30 * math.sin(ang))
        p1 = (p0[0] + ln * math.cos(ang + 0.15), p0[1] + ln * math.sin(ang + 0.15))
        c.line([p0, ((p0[0] + p1[0]) / 2 + 4, (p0[1] + p1[1]) / 2), p1], w=13, taper=(0.0, 0.5))
        tips.append(p1)
    # 缠在手指上的海草：几条从指间垂下来的带子，带锯齿边
    for k, (x, y) in enumerate(((1300, 330), (1340, 300), (1372, 320))):
        pts = [(x, y), (x + 20 + 10 * k, y + 80), (x - 10, y + 170), (x + 30, y + 250), (x + 10, y + 330 + 30 * k)]
        weed = c.stroke(spline(pts), 16, add=False, taper=(0.1, 0.6))
        c.fill(weed, value=0.9)
        for p in resample(spline(pts), 22)[1:-1]:
            c.stroke([tuple(p), (p[0] + 14, p[1] + 6)], 2.4, taper=(0.0, 0.8))
    # 鼓起来的口袋：上衣前襟的一块方形凸起
    pocket = c.smask([(1170, 760), (1240, 752), (1250, 800), (1176, 808)])
    c.clear(pocket)
    hatch(c, pocket, 0.55, [c.ph_lin(85, seed=58)], spacing=7)
    c.outline([(1170, 760), (1240, 752), (1250, 800), (1176, 808)], w=3.5)
    # 身边的水沫
    for _ in range(30):
        a = rng.uniform(0, 2 * math.pi)
        x, y = 1180 + 190 * math.cos(a), 800 + 40 * math.sin(a)
        r = rng.uniform(4, 10)
        c.clear(c.ellipse(x, y, r * 1.4, r))
        c.stroke(ellipse_pts(x, y, r * 1.4, r), 1.8, closed=True)


def mad_hand(c, x, y, ang, s):
    """从水下伸上来的一只手：细长的前臂 + 张开的手指，只在癫狂层。"""
    a = math.radians(ang)
    ln = 240 * s
    wrist = (x + ln * math.cos(a), y + ln * math.sin(a))
    c.stroke(spline([(x, y), (x + ln * 0.5 * math.cos(a) + 12, y + ln * 0.5 * math.sin(a)), wrist]), 16 * s, layer='mad',
             taper=(0.0, 0.1))
    palm = (wrist[0] + 22 * s * math.cos(a), wrist[1] + 22 * s * math.sin(a))
    c.add(c.ellipse(palm[0], palm[1], 22 * s, 18 * s, rot=ang), 'mad')
    for k in range(5):                                                     # 细长的手指，指尖勾着
        fa = a + math.radians(-56 + k * 26)
        f = 64 * s * (0.75 if k in (0, 4) else 1.0)
        base = (palm[0] + 16 * s * math.cos(fa), palm[1] + 16 * s * math.sin(fa))
        mid = (base[0] + f * 0.6 * math.cos(fa), base[1] + f * 0.6 * math.sin(fa))
        tip = (mid[0] + f * 0.4 * math.cos(fa + 0.6), mid[1] + f * 0.4 * math.sin(fa + 0.6))
        c.stroke([base, mid, tip], 8 * s, layer='mad', taper=(0.0, 0.6))
