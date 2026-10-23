"""事件 1 禁书：一本潮湿的皮面书摊开在礁石上，书页上的字在你看它的时候慢慢移动（Data/events.csv）。

夜里的海边：上半是起雾的夜空和一轮被云遮住一半的月亮，中间一条黑海，前景一块平顶的黑礁石，书摊开在礁石顶上，
书页是全图最亮的地方。右页有几行字是深渊青的（字在动）。癫狂层：字从书页上升起来，排成一条往天上卷的字带，带上睁开几只眼。
"""
import math

import numpy as np

from etch import Frame, ellipse_pts, poly, resample, scrawl, smoothstep, spline
from cards._common import eye
from illustrations._kit import GAP, OUT, STRUCT, hatch, moon, night_sky, rock, sea

NAME = '禁书'
SIZE = (1920, 1280)

SPINE_TOP, SPINE_BOT = (960, 690), (960, 960)


def page(side):
    """一页的轮廓（side = -1 左页，1 右页）：靠书脊的地方鼓起来，外缘微微翘。"""
    s = side
    return [(960, 696), (960 + s * 120, 668), (960 + s * 300, 662), (960 + s * 430, 690, 'c'), (960 + s * 446, 820),
            (960 + s * 452, 946, 'c'), (960 + s * 300, 924), (960 + s * 120, 932), (960, 962)]


def draw(c):
    rng = np.random.default_rng(1)
    W, H = c.w, c.h

    # ------------------------------------------------------------ 天、月、海
    night_sky(c, 560, top=0.8, bottom=0.45, seed=1)
    moon(c, 1460, 230, 70)
    cloud = c.smask([(1240, 250), (1360, 200), (1520, 214), (1700, 196), (1820, 240), (1700, 290), (1500, 300), (1330, 300)])
    hatch(c, cloud, 0.62 + 0.1 * c.noise(40, 4), [c.ph_wave(-4, 6, 260, seed=5), c.ph_lin(20, seed=6)], spacing=8)
    c.line([(1250, 252), (1380, 206), (1520, 218), (1700, 200), (1816, 240)], w=2.6, taper=0.2)
    sea(c, 556, 800, tone=0.72, seed=7, rng=rng)
    c.stroke([(0, 558), (W, 554)], 3, taper=0)
    for x0, w, h in ((140, 260, 70), (1640, 200, 46), (1860, 120, 30)):        # 远处的礁石
        rock(c, [(x0, 560), (x0 + w * 0.25, 560 - h), (x0 + w * 0.6, 560 - h * 0.8), (x0 + w, 560)], tone=0.9, light=0.2, seed=x0)

    # ------------------------------------------------------------ 前景的礁石：平顶，书躺在上面
    slab = [(-20, H + 20), (-20, 840), (240, 800), (520, 772), (820, 762), (1180, 760), (1500, 776), (1760, 806), (1960, 840),
            (1960, H + 20)]
    rock(c, slab, tone=0.82, seed=12, cracks=([(300, 880), (360, 1040), (330, 1220)], [(1500, 860), (1460, 1000), (1540, 1180)],
                                               [(720, 1080), (860, 1180), (840, 1280)]))
    # 礁石的顶面朝天，受月光：比正面亮一档，往前渐渐转暗
    top = c.mask(spline(slab, closed=True)) * (1 - smoothstep(800, 1010, c.yy))
    hatch(c, top, np.clip(0.5 + 0.12 * c.noise(60, 40), 0, 1), [c.ph_lin(4, wobble=1.5, seed=41), c.ph_lin(-40, seed=42)], spacing=7)
    # 礁石顶上的水洼：横向的反光
    for k, (cx, cy, rx) in enumerate(((330, 905, 120), (1640, 930, 110))):
        pool = c.smask([(cx - rx, cy), (cx - rx * 0.4, cy - rx * 0.12), (cx + rx * 0.5, cy - rx * 0.1), (cx + rx, cy + 4),
                        (cx + rx * 0.3, cy + rx * 0.14), (cx - rx * 0.6, cy + rx * 0.12)])
        c.clear(pool)
        hatch(c, pool, 0.55 + 0.25 * c.grad(cx - rx, 0, cx + rx, 0), [c.ph_lin(0, wobble=0.4, seed=k), c.ph_lin(75, seed=k + 5)],
              spacing=6)
        for j in range(3):                                                    # 水面映着的月光：几道短横
            yy = cy - 6 + j * 7
            c.erase(c.stroke([(cx - rx * 0.4 + j * 14, yy), (cx + rx * 0.2 + j * 10, yy)], 2.4, add=False, taper=0.4) * pool)

    # ------------------------------------------------------------ 书：先是书下的影子和封皮，再是书页
    shadow = c.mask(spline([(480, 960), (960, 990), (1440, 960), (1450, 1000), (960, 1040), (470, 1000)], closed=True))
    c.fill(c.shadow_of(shadow, 10, 6, blur=8), value=1.0)
    cover = [(960, 700), (960 - 470, 676, 'c'), (960 - 486, 962, 'c'), (960, 990), (960 + 486, 962, 'c'), (960 + 470, 676, 'c')]
    cm = c.mask(spline(cover, closed=True))
    hatch(c, cm, 0.9, [c.ph_lin(4, seed=13), c.ph_lin(60, seed=14)], spacing=6.5)
    c.outline(cover, w=OUT, light=0.3)
    for side in (-1, 1):
        # 书页侧面的纸层：几道平行的细线
        for k in range(1, 6):
            edge = [(960 + side * (452 + k * 3), 946 + k * 3), (960 + side * 300, 924 + k * 4), (960 + side * 120, 932 + k * 4),
                    (960, 962 + k * 4)]
            c.line(edge, w=1.6, taper=0.1)
        pm = c.mask(spline(page(side), closed=True))
        c.clear(pm)
        # 书页：只在书脊附近和外缘有一点阴影，靠书脊的那一侧往下弯
        gutter = 1 - smoothstep(0, 110, np.abs(c.xx - 960))
        stain = smoothstep(0.35, 0.8, c.noise(70, 15 + side))
        hatch(c, pm, np.clip(0.42 * gutter + 0.16 * stain, 0, 1),
              [c.ph_axis((960, 700), (960 + side * 440, 700), bow=-30, half=260, seed=16), c.ph_lin(80, seed=17)], spacing=6, wmax=2.0)
        c.outline(page(side), w=STRUCT, light=0.3)
        # 一行行的字：沿书页的弧度
        for i, y in enumerate(np.arange(720, 920, 24)):
            x0, x1 = (560, 900) if side < 0 else (1020, 1360)
            words = scrawl(c, x0, x1, y + 8 * math.sin((x0 - 960) / 600), 6, seed=40 + i + (0 if side < 0 else 100), width=1.9,
                           slope=-0.02 * side, gap=(10, 18))
            if side > 0 and i in (2, 3, 5):                                    # 右页有几行在动：着深渊青
                scrawl(c, x0 + 6, x1 - 20, y + 13, 6, seed=300 + i, width=2.0, slope=0.03, layer='abyss')
    c.line([SPINE_TOP, (958, 830), SPINE_BOT], w=STRUCT, taper=0.1)
    # 书签：一条从书脊垂到礁石上的布带
    ribbon = [(978, 960), (1010, 1010), (990, 1070), (1030, 1120)]
    c.stroke(spline(ribbon), 14, taper=(0.0, 0.1))
    c.erase(c.stroke(offset_line(ribbon, -3), 3, add=False, taper=0.3))

    # ============================================================ 癫狂层：字从书页上升起来，卷成一条字带，带上睁开几只眼
    band = [(1120, 760), (1180, 640), (1120, 520), (980, 440), (900, 330), (980, 210), (1160, 150), (1320, 180)]
    path = resample(spline(band), 24)
    for k, p in enumerate(path[:-1]):
        q = path[k + 1]
        ang = math.degrees(math.atan2(q[1] - p[1], q[0] - p[0]))
        wd = 0.4 + 0.6 * k / len(path)                                         # 越往上字越大
        F = Frame(tuple(p), rot=ang, scale=1.0)
        pts = F([(-10, 0), (10, 0)])
        scrawl(c, pts[0][0] - 14, pts[0][0] + 30, pts[0][1], 8 + 9 * wd, seed=500 + k, width=2.6 + 2.2 * wd, layer='mad',
               slope=math.tan(math.radians(ang)) * 0.6)
    for (x, y), s in zip([(1190, 610), (930, 380), (1060, 170), (1300, 190)], (48, 62, 70, 54)):
        eye(c, x, y, s, layer='mad', look=-0.3, width=4.4)
    c.line(band, w=4, layer='mad', taper=0.4)


def offset_line(pts, d):
    from etch import offset
    return offset(spline(pts), d)
