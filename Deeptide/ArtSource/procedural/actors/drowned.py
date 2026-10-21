"""N02 溺尸（中档）：肿胀、前倾、湿发盖脸；水手装束，身上挂藤壶和海藻。癫狂层：身体轮廓里涨满水纹（策划/10 §5.3）。

面朝左，驼着背往前探，头垂在胸前，一绺绺湿头发盖住整张脸；条纹水手衫绷在肿胀的身上，裂开几道口子；
近侧的手臂垂到膝盖，手指肿得像香肠；肩上一簇藤壶，手臂和腰上挂着海藻。
"""
import math

import numpy as np

from etch import STRUCT, ellipse_pts, smoothstep, spline

NAME = '溺尸'
SIZE = (420, 680)

OUT_W = 11.0
FEET = 1306


def draw(c):
    rng = np.random.default_rng(3)
    torso = [(300, 470), (400, 400), (560, 410), (660, 490), (700, 650), (690, 820), (650, 950), (520, 990), (390, 980),
             (310, 910), (280, 760), (276, 590)]
    legs = [[(340, 940), (460, 950), (450, 1180), (440, 1270), (330, 1270), (340, 1100)],
            [(480, 950), (620, 930), (630, 1100), (640, 1270), (520, 1272), (500, 1120)]]
    feet = [(380, 1282, 70, 30), (580, 1284, 72, 30)]
    head = (290, 470, 96, 104, -24)
    arm = [(380, 470), (440, 520), (400, 700), (330, 860), (300, 960), (220, 980), (220, 900), (270, 760), (300, 600)]
    far_arm = [(600, 470), (680, 560), (700, 760), (680, 930), (630, 940), (640, 760), (610, 600)]
    shadow = c.ellipse(470, FEET + 2, 300, 24)

    torso_m = c.mask(spline(torso, closed=True))
    leg_ms = [c.mask(spline(l, closed=True)) for l in legs]
    feet_m = np.clip(sum(c.ellipse(*f) for f in feet), 0, 1)
    head_m = c.ellipse(*head)
    arm_m = c.mask(spline(arm, closed=True))
    far_m = c.mask(spline(far_arm, closed=True))
    hand = [(200, 930), (250, 924), (262, 980), (250, 1040), (220, 1052), (196, 1010), (186, 966)]
    hand_m = c.mask(spline(hand, closed=True))
    fill = np.clip(torso_m + sum(leg_ms) + feet_m + head_m + arm_m + far_m + hand_m + shadow, 0, 1)

    side = c.grad(260, 0, 700, 0)
    c.engrave(shadow, 0.9, [c.ph_lin(0, wobble=0.6), c.ph_lin(60)], spacing=7, swell=1.2)
    # 腿：宽腿水手裤，湿透贴腿；光脚肿着
    for m, l in zip(leg_ms, legs):
        c.engrave(m, np.clip(0.7 + 0.25 * side, 0, 1), [c.ph_lin(86, wobble=1.5, seed=1), c.ph_lin(20, seed=2)], spacing=8, swell=1.2)
        c.outline(l, w=STRUCT + 1, light=0.4)
    for f in feet:
        fm = c.ellipse(*f)
        lam = c.relief(fm, radius=20, blur=1)
        c.engrave(fm, np.clip(0.55 - 0.5 * lam, 0, 1), [c.ph_lin(10), c.ph_lin(70)], spacing=6.5, wmax=2.2)
        c.stroke(ellipse_pts(*f), STRUCT, closed=True, light=0.4)
        for k in range(4):                                                     # 脚趾
            x = f[0] - f[2] + 14 + k * 14
            c.stroke([(x, f[1] - 10), (x - 2, f[1] + 8)], 2.2, taper=0.3)
    # 远侧手臂
    c.engrave(far_m, 0.9, [c.ph_lin(80, seed=3), c.ph_lin(10, seed=4)], spacing=8, swell=1.2)
    c.outline(far_arm, w=STRUCT + 1, light=0.4)

    # ------------------------------------------------------------ 身子：条纹水手衫绷在肿胀的身上
    lam = c.relief(torso_m, radius=150, blur=6)
    tone = np.clip(0.18 + 0.45 * side + 0.3 * (1 - lam), 0, 1)
    c.engrave(torso_m, tone, [c.ph_contour(torso_m, blur=8) + 2 * c.noise(40, 5), c.ph_lin(70, seed=6), c.ph_lin(-30, seed=7)],
              spacing=8.5, swell=1.1)
    # 条纹：横向、随身体鼓起弯曲
    for k in range(9):
        y = 470 + k * 56
        pts = [(260, y + 30), (400, y - 6), (560, y), (720, y + 36)]
        stripe = c.stroke(spline(pts), 18, add=False, taper=0)
        c.add(stripe * torso_m * (0.55 + 0.45 * side))
    # 衣服裂开的口子，露出发白的肉
    for (x, y, w, h, r) in ((520, 640, 46, 22, -10), (420, 820, 40, 18, 8), (600, 860, 30, 14, -20)):
        rip = c.ellipse(x, y, w, h, rot=r)
        c.clear(rip, layers=('ink',))
        c.engrave(rip, 0.2, [c.ph_lin(r + 90)], spacing=6, wmax=2)
        pts = ellipse_pts(x, y, w, h, rot=r)
        c.stroke(pts + rng.normal(0, 2.5, pts.shape), 3.2, closed=True, taper=0)
    c.outline(torso, w=OUT_W, light=0.45)
    # 肩上一簇藤壶
    for (x, y, s) in ((560, 430, 22), (600, 462, 18), (528, 452, 16), (630, 500, 14), (575, 490, 12)):
        cone = [(x - s, y + s * 0.5), (x - s * 0.5, y - s * 0.7), (x + s * 0.5, y - s * 0.7), (x + s, y + s * 0.5)]
        cm = c.mask(spline(cone, closed=True))
        c.clear(cm, layers=('ink',))
        c.engrave(cm * c.half_plane((x + s, y - s), (x - s, y + s)), 0.6, [c.ph_lin(80)], spacing=4.5, wmax=1.8)
        c.outline(cone, w=3.2, light=0.4)
        c.stroke(ellipse_pts(x, y - s * 0.7, s * 0.5, s * 0.22), 2.4, closed=True)

    # ------------------------------------------------------------ 近侧手臂：短袖口以下是肿白的胳膊，垂到膝盖
    lam = c.relief(arm_m, radius=40, blur=3)
    c.engrave(arm_m, np.clip(0.1 + 0.4 * (1 - lam) + 0.25 * smoothstep(560, 620, c.yy) * 0, 0, 1),
              [c.ph_axis((400, 480), (240, 960), bow=-18, half=60, seed=8), c.ph_lin(20, seed=9)], spacing=7.5, wmax=2.4)
    sleeve = c.mask(spline([(380, 470), (440, 520), (420, 620), (330, 640), (290, 560), (300, 500)], closed=True)) * arm_m
    c.engrave(sleeve, 0.85, [c.ph_lin(84), c.ph_lin(20)], spacing=7.5)
    for k in range(3):
        c.stroke(spline([(300, 540 + k * 30), (360, 528 + k * 30), (425, 548 + k * 30)]), 9, taper=0)
    c.outline(arm, w=OUT_W - 1, light=0.45)
    lam = c.relief(hand_m, radius=18, blur=1)
    c.engrave(hand_m, np.clip(0.4 - 0.5 * lam, 0, 1), [c.ph_lin(80)], spacing=6, wmax=2.2)
    c.outline(hand, w=STRUCT + 1, light=0.4)
    for k in range(4):                                                         # 肿成香肠的手指
        x = 200 + k * 16
        c.stroke(spline([(x - 8, 1000), (x - 10, 1040), (x - 6, 1072), (x + 2, 1078), (x + 9, 1066), (x + 8, 1000)]), 2.6, taper=0)

    # ------------------------------------------------------------ 头：垂在胸前，湿头发一绺一绺盖住脸
    lam = c.relief(head_m, radius=50, blur=3)
    c.engrave(head_m, np.clip(0.75 - 0.3 * lam, 0, 1), [c.ph_lin(84, seed=10), c.ph_lin(30, seed=11)], spacing=7.5)
    c.stroke(ellipse_pts(*head[:4], rot=head[4]), OUT_W - 1, closed=True, light=0.45)
    for k in range(13):                                                        # 湿发：从头顶垂下来，发梢黏成一绺绺
        a = math.radians(-200 + k * 13)
        x0 = 290 + 92 * math.cos(a) * 0.9
        y0 = 470 + 100 * math.sin(a) * 0.9
        ln = rng.uniform(120, 220)
        pts = [(x0, y0), (x0 - 10 + rng.uniform(-8, 8), y0 + ln * 0.5), (x0 - 18 + rng.uniform(-12, 4), y0 + ln)]
        strand = c.stroke(spline(pts), rng.uniform(16, 26), add=False, taper=(0.0, 0.85))
        c.add(strand, 'fill')
        c.add(strand * 0.95)
        c.erase(c.stroke(spline([(pts[0][0] + 3, pts[0][1] + 10), (pts[1][0] + 3, pts[1][1])]), 2.0, add=False, taper=0.4))
    # 海藻：从手臂和腰上垂下来的带子
    for pts in ([(340, 700), (320, 790), (346, 880), (320, 960)], [(300, 900), (276, 990), (300, 1080), (280, 1140)],
                [(640, 940), (660, 1030), (636, 1110)]):
        weed = c.stroke(spline(pts), 22, add=False, taper=(0.1, 0.8))
        c.add(weed, 'fill')
        c.paint(weed, 0.7 * c.lines(c.ph_lin(80), 6, 2.4) + 0.3 * weed)
        c.stroke(spline(pts), 3.0, taper=(0.1, 0.8))
    c.add(fill, 'fill')

    # ============================================================ 癫狂层：身体轮廓里涨满水纹
    body = np.clip(torso_m + arm_m + sum(leg_ms), 0, 1)
    level = smoothstep(560, 640, c.yy)                                          # 水位到胸口
    waves = c.lines(c.yy + 7 * np.sin(c.xx / 26 + c.yy / 90), 22, 3.4)
    c.add(waves * body * level, 'mad')
    c.add(c.stroke(spline([(280, 600), (420, 584), (560, 596), (700, 610)]), 5, add=False, taper=0.1) * body, 'mad')
    for _ in range(14):                                                          # 水里的气泡
        x, y = rng.uniform(320, 660), rng.uniform(660, 1200)
        r = rng.uniform(5, 11)
        c.add(c.stroke(ellipse_pts(x, y, r, r), 2.4, closed=True, add=False) * body, 'mad')
