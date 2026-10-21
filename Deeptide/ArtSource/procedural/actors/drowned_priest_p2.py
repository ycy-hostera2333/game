"""BOSS 溺亡司祭 · 褪去人形（Boss 档）：法衣撕裂，下半身变成触须；主教冠下张开鳃和多只眼（深渊遮罩）（策划/10 §5.3）。

上半身还是司祭：主教冠、浮肿的侧脸、披着撕成条的法衣；腰以下是一大团触须，压在祭坛上、往四面摊开，尖端卷起。
近侧的手臂往前伸、手指拉长成爪；远侧的手把权杖高高举起。脸和脖子上的鳃、眼走深渊遮罩：清醒时也是青的。
"""
import math

import numpy as np

from etch import STRUCT, Frame, ellipse_pts, smoothstep, spline
from actors._priest import FEET, OUT_W, TOP, altar, mitre, staff
from cards._common import tentacle

NAME = '溺亡司祭 · 褪去人形'
SIZE = (660, 940)


def draw(c):
    rng = np.random.default_rng(32)
    side = c.grad(300, 0, 1050, 0)
    shadow = c.ellipse(660, FEET + 2, 600, 32)
    c.add(shadow, 'fill')
    c.engrave(shadow, 0.9, [c.ph_lin(0, wobble=0.6), c.ph_lin(60)], spacing=7, swell=1.2)
    altar(c, rng)

    # ------------------------------------------------------------ 远侧的手：把权杖举过头顶（先画，在身子后面）
    far = [(800, 640), (900, 560), (980, 420), (1010, 330), (1050, 340), (1030, 450), (950, 620), (850, 720)]
    fm = c.mask(spline(far, closed=True))
    c.add(fm, 'fill')
    c.engrave(fm, 0.9, [c.ph_lin(60), c.ph_lin(-20)], spacing=8, swell=1.2)
    c.outline(far, w=STRUCT + 1, light=0.4)
    staff(c, 1040, 90, 760, 330)

    # ------------------------------------------------------------ 下半身：一团触须，从腰往下、往两边摊开
    root = (680, 1120)
    arms = [((680, 1120), (520, 1400), (420, 1620), (260, 1700), (170, 1640), (190, 1580), 92),
            ((700, 1140), (880, 1400), (1000, 1600), (1150, 1690), (1220, 1620), (1190, 1570), 92),
            ((640, 1150), (560, 1450), (560, 1680), (470, 1780), (380, 1760), (400, 1720), 84),
            ((720, 1150), (790, 1450), (820, 1680), (930, 1790), (1010, 1760), (990, 1720), 84),
            ((660, 1120), (380, 1250), (200, 1380), (110, 1300), (150, 1230), (200, 1260), 64),
            ((700, 1120), (980, 1230), (1170, 1320), (1250, 1240), (1210, 1180), (1170, 1210), 64)]
    # 腰下先盘一团短而粗的触须，把根部填实，长的那几条从这团里伸出去
    coil = [((600, 1120), (520, 1260), (560, 1380), (660, 1360), 120), ((760, 1120), (840, 1260), (800, 1390), (700, 1380), 120),
            ((680, 1140), (640, 1300), (700, 1440), (780, 1420), 110)]
    for a in coil:
        ctrl, w = list(a[:-1]), a[-1]
        c.add(tentacle(c, ctrl, w, layer='ink', tone=1.0, taper=0.7, outline=6.0), 'fill')
    for a in reversed(arms):
        ctrl, w = list(a[:-1]), a[-1] * 1.4
        m = tentacle(c, ctrl, w, layer='ink', tone=0.95, taper=0.9, outline=6.5)
        c.add(m, 'fill')

    # ------------------------------------------------------------ 上半身：撕成条的法衣披在肩上
    torso = [(560, 560), (760, 560), (850, 640), (880, 860), (840, 1100), (700, 1170), (560, 1150), (480, 1000), (470, 760),
             (490, 640)]
    tm = c.mask(spline(torso, closed=True))
    c.add(tm, 'fill')
    lam = c.relief(tm, radius=120, blur=6)
    c.engrave(tm, np.clip(0.3 + 0.45 * side + 0.25 * (1 - lam), 0, 1), [c.ph_lin(86, wobble=1.5, seed=1), c.ph_lin(25, seed=2),
                                                                     c.ph_lin(-35, seed=3)], spacing=9, swell=1.2)
    c.outline(torso, w=OUT_W, light=0.45)
    for k in range(9):                                                         # 腰上垂下来的布条，盖在触须根上
        x = 500 + k * 42 + rng.uniform(-8, 8)
        ln = rng.uniform(120, 260)
        rag = [(x - 18, 1080), (x + 18, 1080), (x + 10 + rng.uniform(-10, 10), 1080 + ln), (x - 4, 1080 + ln + 20)]
        rm = c.mask(spline(rag, closed=True))
        c.add(rm, 'fill')
        c.engrave(rm, 0.6 + 0.3 * side, [c.ph_lin(88)], spacing=6.5, wmax=2.4)
        c.outline(rag, w=STRUCT - 1, light=0.4)
    # 胸口撕开的口子：里面是鳞
    rip = [(600, 700), (680, 690), (720, 800), (690, 940), (620, 960), (580, 840)]
    ripm = c.mask(spline(rip, closed=True))
    c.clear(ripm, layers=('ink',))
    for row in range(6):
        for k in range(4):
            x, y = 600 + k * 30 + (row % 2) * 15, 720 + row * 40
            c.add(c.stroke(ellipse_pts(x, y, 16, 12, a0=10, a1=170), 2.6, add=False, taper=0.2) * ripm)
    pts = spline(rip, closed=True)
    c.stroke(pts + rng.normal(0, 3, pts.shape), 4, closed=True, taper=0)

    # ------------------------------------------------------------ 近侧的手臂：往前伸，手指拉长成爪
    arm = [(560, 620), (470, 700), (360, 820), (300, 860), (320, 910), (400, 880), (520, 780), (580, 700)]
    am = c.mask(spline(arm, closed=True))
    c.add(am, 'fill')
    lam = c.relief(am, radius=40, blur=3)
    c.engrave(am, np.clip(0.35 + 0.55 * (1 - lam), 0, 1), [c.ph_axis((560, 620), (300, 880), bow=-14, half=50), c.ph_lin(20)],
              spacing=8, swell=1.2)
    c.outline(arm, w=OUT_W - 1, light=0.45)
    for k, (dx, dy) in enumerate(((-150, -40), (-170, 20), (-150, 80), (-110, 120))):
        claw = spline([(310, 880), (310 + dx * 0.5, 880 + dy * 0.4 - 10), (310 + dx, 880 + dy)])
        cm = c.stroke(claw, 16, add=False, taper=(0.0, 0.95))
        c.add(cm, 'fill')
        c.add(cm)
        c.erase(c.stroke(claw[: len(claw) // 2] + np.array([0, -3]), 2.0, add=False, taper=0.4))

    # ------------------------------------------------------------ 头：同一张脸，嘴咧到耳根；主教冠
    head = [(690, 400), (640, 352), (570, 362), (548, 398), (540, 426), (510, 470, 'c'), (526, 482), (514, 500), (524, 520),
            (500, 548), (520, 590), (580, 610), (640, 610), (700, 560), (716, 470)]
    hm = c.mask(spline(head, closed=True))
    c.add(hm, 'fill')
    lam = c.relief(hm, radius=50, blur=3)
    c.engrave(hm, np.clip(0.2 + 0.4 * c.grad(500, 0, 716, 0) + 0.3 * (1 - lam), 0, 1), [c.ph_lin(70), c.ph_lin(-20)], spacing=7.5)
    c.outline(head, w=OUT_W - 2, light=0.45)
    mouth = [(516, 506), (600, 520), (640, 540), (590, 560), (512, 546)]
    mm = c.mask(spline(mouth, closed=True))
    c.fill(mm)
    for k in range(6):                                                         # 一排尖牙
        x = 524 + k * 18
        c.erase(c.stroke([(x, 512 + k * 2), (x + 6, 530 + k * 2)], 5, add=False, taper=(0.0, 0.9)))
        c.erase(c.stroke([(x + 4, 552 - k), (x + 9, 536 - k)], 4, add=False, taper=(0.0, 0.9)))
    c.stroke(spline([(540, 470), (552, 484), (546, 494)]), 3.4, taper=0.3)
    mitre(c, Frame((610, 360), rot=-4), c.grad(500, 0, 720, 0))

    # ------------------------------------------------------------ 深渊遮罩：多只眼 + 张开的鳃
    for (x, y, w) in ((566, 430, 18), (600, 410, 13), (620, 460, 15), (590, 480, 10), (650, 430, 11), (560, 400, 9)):
        up = spline([(x - w, y, 'c'), (x, y - w * 0.65), (x + w, y, 'c')])
        lo = spline([(x - w, y, 'c'), (x, y + w * 0.55), (x + w, y, 'c')])
        em = c.mask(np.vstack([up, lo[::-1]]))
        c.clear(em, layers=('ink',))
        c.add(em * 0.35, 'abyss')
        c.add(c.ellipse(x, y, w * 0.38, w * 0.38), 'abyss')
        c.stroke(up, 3.4, taper=0.1)
        c.stroke(lo, 2.4, taper=0.1)
    for k in range(5):
        y = 560 + k * 24
        gill = spline([(600, y - 10), (640, y + 4), (690, y - 6)])
        c.add(c.stroke(gill, 10, add=False, taper=0.2), 'abyss')
        c.stroke(gill + np.array([0, 7]), 3, taper=0.2)
