"""BOSS 鳞母（Boss 档）：最早跟海里结亲的那个镇上女人，早就不是人了（策划/15 §2.3、09 M7.7）。

面朝左，伏在礁石上：上半身还像个佝偻的女人，驼着背，背脊上一排鳍刺；肩上披一张破渔网。湿透的长发像一道帘子
从头顶垂下来盖住脸，帘缝里露出一只鼓出来的圆眼（深渊遮罩）；脖子上三道鳃也是青的。两只长出蹼的手把一枚卵抱在胸前，
卵里亮着一只小眼。腰以下是一条鱼尾，盘在礁石上，尾鳍在身后翘起来。
癫狂层：礁石缝里、影子里一串串的卵囊，从轮廓外长出来。
"""
import math

import numpy as np

from etch import STRUCT, Canvas, ellipse_pts, smoothstep, spline
from cards._wolf import scales

NAME = '鳞母'
SIZE = (600, 760)
CROP = 360    # 按 600 × 940 的坐标画，裁掉顶上 360（工作像素）的空白：意图标记才贴着头顶

OUT_W = 12.0
FEET = 1805


def _band(c, center, w0, w1):
    """沿中心线从 w0 收到 w1 的一条宽带。返回遮罩。"""
    return c.stroke(spline(center), w0, add=False, taper=0, profile=lambda u: 1 - (1 - w1 / w0) * u)


ROCK = [(90, 1805), (140, 1700), (320, 1640), (600, 1610), (880, 1620), (1080, 1680), (1170, 1805)]
DY = 150      # 身子画在自己的画布上，再整体往下挪到礁石上（形状是先按悬空画的）


def draw(c):
    big = Canvas(c.w, c.h + CROP, seed=c.seed)
    _draw_full(big)
    for k in c.layers:
        c.layers[k][:] = big.layers[k][CROP:]


def _draw_full(c):
    rng = np.random.default_rng(15)
    side = c.grad(150, 500, 1150, 1800)
    rock = ROCK
    rock_m = c.mask(spline(rock, closed=True))
    shadow = c.ellipse(640, FEET + 2, 560, 30)
    c.add(np.clip(rock_m + shadow, 0, 1), 'fill')
    # ------------------------------------------------------------ 礁石：等高排线 + 藤壶
    c.engrave(shadow, 0.9, [c.ph_lin(0, wobble=0.6), c.ph_lin(60)], spacing=7, swell=1.2)
    lam = c.relief(rock_m, radius=120, blur=6)
    c.engrave(rock_m, np.clip(0.45 + 0.3 * side + 0.25 * (1 - lam), 0, 1), [c.ph_contour(rock_m, blur=6), c.ph_lin(30, seed=4),
                                                                          c.ph_lin(-40, seed=5)], spacing=9, swell=1.2)
    for _ in range(30):
        x, y = rng.uniform(160, 1120), rng.uniform(1650, 1790)
        if rock_m[int(y), int(x)] < 0.5:
            continue
        r = rng.uniform(8, 16)
        c.clear(c.ellipse(x, y, r, r * 0.8))
        c.stroke(ellipse_pts(x, y, r, r * 0.8), 2.6, closed=True)
        c.add(c.ellipse(x, y, r * 0.35, r * 0.3))
    c.outline(rock, w=OUT_W, light=0.4)


    # 身子：另一张画布画好，往下挪 DY，盖在礁石上
    f = Canvas(c.w, c.h, seed=c.seed + 1)
    _figure(f, rng)
    cover = np.roll(f.layers['fill'], DY, axis=0)
    for k in ('ink', 'abyss'):
        c.layers[k] = c.layers[k] * (1 - np.clip(cover * 1.5, 0, 1)) + np.roll(f.layers[k], DY, axis=0)
    c.layers['mad'] = np.maximum(c.layers['mad'], np.roll(f.layers['mad'], DY, axis=0))
    c.layers['fill'] = np.maximum(c.layers['fill'], cover)

    # ------------------------------------------------------------ 癫狂层：礁石缝里、影子里一串串的卵囊
    for x, y, n in ((110, 1700, 4), (1150, 1640, 5), (980, 1790, 3), (40, 1800, 3), (1180, 1780, 4), (620, 1800, 3)):
        for k in range(n):
            ox, oy = x + rng.uniform(-50, 50), y + rng.uniform(-40, 40)
            r = rng.uniform(16, 30)
            c.stroke(ellipse_pts(ox, oy, r, r * 1.2), 3.4, layer='mad', closed=True)
            c.add(c.ellipse(ox + 4, oy - 2, r * 0.3, r * 0.3), 'mad')


def _figure(c, rng):
    side = c.grad(150, 350, 1150, 1650)
    # ------------------------------------------------------------ 形状
    tail_c = [(800, 1330), (960, 1450), (1060, 1560), (1120, 1470), (1110, 1340), (1070, 1250)]
    fin = [(1070, 1270), (1000, 1120), (1060, 1150), (1110, 1080), (1140, 1160), (1190, 1130), (1150, 1280)]
    torso = [(430, 760), (560, 610), (740, 600), (870, 720), (920, 940), (900, 1180), (830, 1370), (650, 1480), (500, 1440),
             (440, 1300), (420, 1080), (400, 900)]
    head = (330, 850, 128, 150)
    veil = [(250, 720), (330, 690), (430, 720), (470, 860), (480, 1080), (500, 1300), (440, 1360), (380, 1330), (330, 1360),
            (270, 1330), (220, 1360), (200, 1200), (210, 980), (220, 820)]
    egg = (390, 1220, 76, 94)
    arm_near = [(500, 980), (540, 1110), (520, 1260), (450, 1330), (330, 1340), (300, 1300), (420, 1280), (470, 1190), (460, 1060)]

    tail_m = _band(c, tail_c, 170, 60)
    fin_m = c.mask(spline(fin, closed=True))
    torso_m = c.mask(spline(torso, closed=True))
    head_m = c.ellipse(*head, rot=-20)
    veil_m = c.mask(spline(veil, closed=True))
    egg_m = c.ellipse(*egg)
    arm_m = c.mask(spline(arm_near, closed=True))
    spikes = []
    for k in range(8):
        t = k / 7
        # 沿着背脊的外缘（驼背最高处在右上），刺朝外、朝后
        x, y = 560 + 340 * t, 610 + 580 * t ** 1.4
        a = math.radians(-80 + 75 * t)
        L = 110 - 40 * abs(t - 0.35)
        spikes.append([(x - 26, y + 6), (x + L * math.cos(a), y + L * math.sin(a)), (x + 24, y + 36)])
    spike_m = np.clip(sum(c.mask(spline(s, closed=True)) for s in spikes), 0, 1)
    fill = np.clip(tail_m + fin_m + torso_m + head_m + veil_m + egg_m + arm_m + spike_m, 0, 1)
    c.add(fill, 'fill')

    # ------------------------------------------------------------ 鳍刺（先画，在背后）
    for s in spikes:
        sm = c.mask(spline(s, closed=True))
        c.clear(sm)
        c.engrave(sm, 0.6, [c.ph_axis(s[0], s[1], half=30)], spacing=6, wmax=2.4)
        c.stroke(spline(s, closed=True), 4.0, closed=True, light=0.4)

    # ------------------------------------------------------------ 尾：盘在礁石上，尾鳍在身后翘起
    c.clear(tail_m)
    lam = c.relief(tail_m, radius=60, blur=3)
    c.engrave(tail_m, np.clip(0.35 + 0.35 * side + 0.25 * (1 - lam), 0, 1), [c.ph_lin(70, seed=6), c.ph_lin(-20, seed=7)],
              spacing=8, swell=1.1)
    scales(c, tail_m, size=36, angle=-30, tone=0.4, w=2.6, seed=3)
    c.clear(fin_m)
    c.engrave(fin_m, 0.4, [c.ph_ray(1100, 1300, r0=40)], spacing=7, wmax=2.4)
    for p in fin[1:-1]:
        c.stroke([(1100, 1300), p], 3.6, taper=(0.0, 0.6))
    c.outline(fin, w=STRUCT + 1, light=0.4)

    # ------------------------------------------------------------ 身子：驼背，左上受光；下半截长满鳞
    c.clear(torso_m)
    lam = c.relief(torso_m, radius=180, blur=6)
    c.engrave(torso_m, np.clip(0.38 + 0.4 * side + 0.3 * (1 - lam), 0, 1), [c.ph_lin(80, wobble=1.6, seed=1), c.ph_lin(20, seed=2),
                                                                           c.ph_lin(-40, seed=3)], spacing=9, swell=1.3)
    belly = torso_m * smoothstep(1080, 1220, c.yy)
    scales(c, belly, size=34, angle=10, tone=0.45, w=2.6, seed=5)
    c.outline(torso, w=OUT_W, light=0.45)

    # 披肩：一张破渔网搭在肩背上，网格 + 结，下摆垂着几缕网线
    edge = 900 + 0.35 * (c.xx - 500)
    shawl = torso_m * (1 - smoothstep(edge - 20, edge + 20, c.yy))
    net = np.maximum(c.lines(c.ph_lin(40, wobble=0.6, seed=8), 44, 3.6), c.lines(c.ph_lin(-50, wobble=0.6, seed=9), 44, 3.6))
    c.erase(shawl * 0.7)
    c.engrave(shawl, 0.2, [c.ph_lin(85, seed=12)], spacing=9, wmax=2.0)
    c.add(shawl * net)
    for x in np.linspace(470, 900, 9):
        y0 = 900 + 0.35 * (x - 500)
        c.stroke(spline([(x, y0), (x + rng.uniform(-14, 14), y0 + 60), (x + rng.uniform(-20, 20), y0 + rng.uniform(100, 160))]),
                 3.0, taper=(0.0, 0.8))
        c.add(c.ellipse(x, y0 + 4, 7, 7))

    # 鳃（深渊遮罩），在脖子和披肩之间
    for k in range(3):
        x = 470 + 24 * k
        pts = spline([(x, 790), (x + 14, 830), (x + 6, 880)])
        c.stroke(pts, 7.0, layer='abyss', taper=0.3)
        c.stroke(pts, 2.6, taper=0.3)

    # ------------------------------------------------------------ 头和湿发：一道深色的发帘从头顶垂到卵上，帘缝里一只眼
    c.clear(head_m)
    lam = c.relief(head_m, radius=70, blur=3)
    c.engrave(head_m, np.clip(0.35 + 0.35 * side + 0.25 * (1 - lam), 0, 1), [c.ph_contour(head_m, blur=3), c.ph_lin(30, seed=10)],
              spacing=7, swell=1.1)
    gap = c.mask(spline([(282, 760), (322, 770), (330, 900), (300, 940), (270, 900), (262, 800)], closed=True))
    hair = veil_m * (1 - gap)
    c.clear(hair)
    c.engrave(hair, np.clip(0.72 + 0.2 * side, 0, 1), [c.ph_lin(93, wobble=2.2, wscale=40, seed=13), c.ph_lin(80, seed=14)],
              spacing=8, swell=1.4)
    for k in range(18):                                                     # 几绺亮一点的发丝，让帘子有层次
        x0 = 230 + k * 14 + rng.uniform(-5, 5)
        if gap[800, int(min(max(x0, 0), c.w - 1))] > 0.5:
            continue
        pts = [(x0, 720 + rng.uniform(0, 30)), (x0 - 6, 950), (x0 - 18 + rng.uniform(-10, 10), 1150),
               (x0 - 26 + rng.uniform(-14, 14), 1330)]
        c.erase(c.stroke(spline(pts), rng.uniform(1.6, 2.6), add=False, taper=(0.1, 0.8)) * hair)
    for x, L in ((230, 120), (300, 160), (360, 110), (440, 140)):          # 发梢垂过轮廓，滴着水
        c.stroke(spline([(x, 1320), (x - 8, 1320 + L * 0.6), (x - 14, 1320 + L)]), 5.0, taper=(0.0, 0.9))
        c.add(c.ellipse(x - 14, 1330 + L, 6, 8))
    c.stroke(spline(veil, closed=True), STRUCT, closed=True, light=0.4)
    # 帘缝里的眼：鼓出来、圆、竖瞳，虹膜发青光
    ex, ey, er = 296, 846, 40
    eye = c.ellipse(ex, ey, er, er + 6)
    c.clear(eye)
    c.fill(eye * c.lines(np.arctan2(c.yy - ey, c.xx - ex) * 18, 5, 2.2) * (np.hypot(c.xx - ex, c.yy - ey) > 12), 'abyss')
    c.fill(eye * 0.4, 'abyss')
    c.fill(c.ellipse(ex - 4, ey, 7, 24), value=1.0)
    c.erase(c.ellipse(ex - 14, ey - 18, 6, 6))
    c.erase(c.ellipse(ex - 14, ey - 18, 6, 6), 'abyss')
    c.stroke(ellipse_pts(ex, ey, er, er + 6), 4.0, closed=True)

    # ------------------------------------------------------------ 卵：抱在胸前，里面亮着一只小眼
    gx, gy, grx, gry = egg
    c.clear(egg_m)
    c.engrave(egg_m, np.clip(0.15 + 0.45 * c.grad(gx - grx, gy - gry, gx + grx, gy + gry), 0, 1), [c.ph_contour(egg_m, blur=3)],
              spacing=6, wmax=2.2)
    c.fill(c.ellipse(gx + 12, gy - 4, 26, 22), 'abyss', 0.6)
    c.fill(c.ellipse(gx + 12, gy - 4, 6, 16), value=1.0)
    c.erase(c.ellipse(gx - 30, gy - 42, 14, 22, -25))
    c.stroke(ellipse_pts(gx, gy, grx, gry), 5.0, closed=True, light=0.4)

    # 近侧的手臂：从肩上绕下来托住卵，手指带蹼扣在卵的前面
    c.clear(arm_m * (1 - egg_m))
    lam = c.relief(arm_m, radius=40, blur=2)
    c.engrave(arm_m * (1 - egg_m), np.clip(0.4 + 0.35 * side + 0.25 * (1 - lam), 0, 1), [c.ph_contour(arm_m, blur=3),
                                                                                       c.ph_lin(40, seed=11)], spacing=7, swell=1.1)
    c.outline(arm_near, w=STRUCT + 1, light=0.45)
    for k in range(4):
        a = math.radians(150 + 26 * k)
        x0, y0 = gx + (grx - 4) * math.cos(a), gy + (gry - 4) * math.sin(a)
        x1, y1 = x0 + 34 * math.cos(a - 1.3), y0 + 34 * math.sin(a - 1.3)
        c.stroke(spline([(x0 + 30, y0 + 40), (x0, y0), (x1, y1)]), 8.0, taper=(0.0, 0.7))
    web = c.mask(spline([(gx - 70, gy + 30), (gx - 30, gy + 70), (gx + 10, gy + 96), (gx - 60, gy + 90)], closed=True))
    c.engrave(web * (1 - egg_m), 0.45, [c.ph_lin(70)], spacing=5, wmax=1.8)

