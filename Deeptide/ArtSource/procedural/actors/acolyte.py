"""N01 教团侍僧（中档）：尖兜帽、下垂的长袖、弯匕首；胸前缝着潮纹徽章，袍角湿透。癫狂层：兜帽下露出鳃裂（策划/10 §5.3）。

面朝左，微微前倾；近处的手臂伸向前方，从宽大的袖口里握着一把弯匕首，刃尖朝下。
脸藏在兜帽的阴影里，只看得到下巴和一点鼻尖；癫狂层的鳃裂和两点眼光就落在这片暗里（舞台上癫狂层是发光青，压在暗处反而最显眼）。
"""
import math

import numpy as np

from etch import STRUCT, ellipse_pts, smoothstep, spline

NAME = '教团侍僧'
SIZE = (400, 680)

OUT_W = 11.0           # 敌人外轮廓 10–12（10 §3）
FEET = 1306


def draw(c):
    rng = np.random.default_rng(1)

    # ------------------------------------------------------------ 形状
    hood = [(392, 52, 'c'), (450, 160), (520, 290), (548, 380), (500, 430), (300, 440), (262, 380), (282, 300), (330, 170)]
    robe = [(300, 400), (520, 400), (580, 520), (600, 760), (640, 1000), (690, 1290), (600, 1306), (480, 1296), (360, 1308),
            (230, 1294), (150, 1300), (190, 1060), (230, 800), (250, 560)]
    far_sleeve = [(520, 470), (600, 560), (640, 760), (650, 960), (590, 990), (560, 800), (520, 620)]
    near_sleeve = [(330, 460), (250, 560), (170, 700), (100, 860), (90, 980), (150, 1000), (210, 930), (230, 860), (300, 720),
                   (380, 560)]
    hood_m = c.mask(spline(hood, closed=True))
    robe_m = c.mask(spline(robe, closed=True))
    far_m = c.mask(spline(far_sleeve, closed=True))
    near_m = c.mask(spline(near_sleeve, closed=True))
    hand = [(118, 840), (82, 862), (66, 900), (90, 920), (130, 900), (150, 862)]
    hand_m = c.mask(spline(hand, closed=True))
    blade = [(84, 880, 'c'), (60, 950), (40, 1030), (14, 1100, 'c'), (52, 1040), (84, 960), (104, 896, 'c')]
    blade_m = c.mask(spline(blade, closed=True, step=1))
    shadow_m = c.ellipse(420, FEET + 4, 300, 22)
    fill = np.clip(hood_m + robe_m + far_m + near_m + hand_m + blade_m + shadow_m, 0, 1)
    c.add(fill, 'fill')

    side = c.grad(250, 0, 650, 0)                                          # 0 = 迎光（左），1 = 背光（右）

    # 地上的影子
    c.engrave(shadow_m, 0.9, [c.ph_lin(0, wobble=0.6), c.ph_lin(60)], spacing=7, swell=1.2)

    # ------------------------------------------------------------ 远侧的袖子（在身体后面）
    c.engrave(far_m, 0.92, [c.ph_lin(82, seed=1), c.ph_lin(20, seed=2), c.ph_lin(-40, seed=3)], spacing=8, swell=1.2)
    c.outline(far_sleeve, w=STRUCT + 1, light=0.4)

    # ------------------------------------------------------------ 长袍：竖褶，左亮右暗，下摆湿透（更暗、往下滴）
    folds = c.xx + 26 * np.sin(c.yy / 160) * 0
    lam = c.relief(robe_m, radius=120, blur=6)
    fold = np.sin((c.xx - 0.12 * (c.yy - 400)) / 34 + 0.4 * np.sin(c.yy / 200))
    wet = smoothstep(1080, 1240, c.yy + 30 * c.noise(40, 4))
    tone = np.clip(0.3 + 0.45 * side + 0.25 * (1 - lam) - 0.12 * fold + 0.35 * wet, 0, 1)
    c.engrave(robe_m, tone, [c.ph_lin(86, wobble=1.5, seed=5), c.ph_lin(25, seed=6), c.ph_lin(-35, seed=7)], spacing=8.5, swell=1.2)
    for k, x0 in enumerate((300, 360, 430, 500, 560)):                       # 褶谷
        x1 = x0 + (x0 - 420) * 0.6
        c.line([(x0, 600 + k * 20), (x0 + (x1 - x0) * 0.5 + 6, 950), (x1, 1280)], w=3.2, taper=(0.5, 0.1))
    # 湿透的下摆：参差的边 + 水滴
    hem = [(150, 1300), (230, 1294), (360, 1308), (480, 1296), (600, 1306), (690, 1290)]
    for x in np.linspace(180, 660, 14):                                      # 水顺着下摆往下滴，在地上积一小摊
        y = 1270 + rng.uniform(-10, 6)
        ln = rng.uniform(12, 30)
        c.erase(c.stroke([(x + 3, y - 30), (x + 3, y + ln - 8)], 2.0, add=False, taper=(0.6, 0.2)))
        c.stroke([(x, y), (x + rng.uniform(-2, 2), y + ln)], 3.6, taper=(0.0, 0.5))
    c.engrave(c.ellipse(430, FEET, 250, 14) * (1 - robe_m), 0.7, [c.ph_lin(0, wobble=0.4)], spacing=5, wmax=2)
    # 腰绳
    c.stroke(spline([(255, 700), (400, 716), (585, 700)]), 9, taper=0)
    c.erase(c.stroke(spline([(262, 694), (400, 708), (575, 694)]), 2.4, add=False, taper=0.2))
    c.stroke(spline([(420, 716), (430, 800), (414, 900)]), 7, taper=(0.0, 0.3))
    c.stroke(spline([(440, 714), (458, 790), (452, 860)]), 6, taper=(0.0, 0.3))
    c.outline(robe, w=OUT_W, light=0.45)

    # ------------------------------------------------------------ 胸前的潮纹徽章：一个圆，里面三道浪
    ex, ey = 452, 560
    em = c.ellipse(ex, ey, 46, 46)
    c.clear(em, layers=('ink',))
    c.engrave(em, 0.15 + 0.3 * c.grad(ex - 46, 0, ex + 46, 0), [c.ph_lin(45, wobble=0)], spacing=6, wmax=2)
    c.stroke(ellipse_pts(ex, ey, 46, 46), 4.5, closed=True)
    for k, dy in enumerate((-16, 0, 16)):
        wave = [(ex - 32 + j * 8, ey + dy + 6 * math.sin(j * 1.2 + k)) for j in range(9)]
        c.stroke(spline(wave), 3.0, taper=0.2)
    for a in range(0, 360, 30):                                               # 缝线
        q = math.radians(a)
        c.stroke([(ex + 52 * math.cos(q), ey + 52 * math.sin(q)), (ex + 58 * math.cos(q + 0.12), ey + 58 * math.sin(q + 0.12))],
                 2.2, taper=0)

    # ------------------------------------------------------------ 近侧的袖子：宽大下垂，袖口黑
    lam = c.relief(near_m, radius=60, blur=4)
    c.engrave(near_m, np.clip(0.2 + 0.55 * (1 - lam) + 0.2 * c.grad(200, 600, 300, 900), 0, 1),
              [c.ph_axis((340, 470), (130, 960), bow=-24, half=90, seed=8), c.ph_lin(80, seed=9), c.ph_lin(20, seed=10)],
              spacing=8, swell=1.1)
    for pts in ([(300, 520), (250, 640), (200, 760)], [(330, 600), (280, 720), (240, 850)]):
        c.line(pts, w=3.0, taper=0.4)
    opening = c.ellipse(140, 925, 64, 52, rot=-30)
    c.engrave(opening * near_m, 0.97, [c.ph_lin(-30), c.ph_lin(60)], spacing=6.5, swell=1.4)
    c.outline(near_sleeve, w=OUT_W, light=0.45)

    # 手 + 弯匕首
    c.engrave(blade_m, 0.12 + 0.4 * c.grad(60, 0, 100, 0), [c.ph_along((84, 880), (14, 1100), wobble=0.2)], spacing=5.5, wmax=1.8)
    c.stroke(spline(blade, closed=True, step=1), 4.0, closed=True, taper=0)
    c.stroke(spline([(92, 900), (66, 980), (36, 1060)]), 1.8, taper=0.3)    # 刃脊
    lam = c.relief(hand_m, radius=14, blur=1)
    c.engrave(hand_m, np.clip(0.45 - 0.5 * lam, 0, 1), [c.ph_lin(30), c.ph_lin(-50)], spacing=6, wmax=2.2)
    c.outline(hand, w=STRUCT, light=0.45)
    for k in range(3):
        c.line([(80 + k * 12, 900 - k * 4), (92 + k * 12, 914 - k * 4)], w=2.4, taper=0.3)
    c.fill(c.mask(spline([(96, 872), (130, 856), (138, 874), (104, 892)], closed=True)), value=0.95)   # 刀柄露在拳上

    # ------------------------------------------------------------ 兜帽：尖顶向前弯，脸在阴影里
    lam = c.relief(hood_m, radius=80, blur=5)
    c.engrave(hood_m, np.clip(0.25 + 0.5 * side + 0.3 * (1 - lam), 0, 1),
              [c.ph_axis((392, 52), (420, 440), bow=-40, half=150, seed=11), c.ph_lin(70, seed=12), c.ph_lin(10, seed=13)],
              spacing=8.5, swell=1.2)
    face_hole = [(330, 240), (280, 300), (262, 380), (300, 430), (370, 420), (380, 330)]
    fh = c.mask(spline(face_hole, closed=True))
    c.engrave(fh, 0.94, [c.ph_lin(60, seed=14), c.ph_lin(-30, seed=15)], spacing=6.5, swell=1.5)
    # 下巴和鼻尖：一点点被光扫到的边
    chin = [(264, 386), (276, 418), (312, 432), (340, 420), (314, 404), (288, 380)]
    cm = c.mask(spline(chin, closed=True))
    c.clear(cm, layers=('ink',))
    c.engrave(cm, 0.35, [c.ph_lin(70)], spacing=6, wmax=2)
    c.outline(chin, w=3.4, light=0.3)
    c.stroke(spline([(274, 334), (262, 352), (276, 360)]), 3.4, taper=0.2)   # 鼻尖
    c.erase(c.stroke(spline([(276, 330), (264, 350)]), 2.0, add=False, taper=0.3))
    c.line([(330, 240), (290, 290), (266, 370)], w=STRUCT + 1, taper=0.2)      # 兜帽前沿的厚边
    c.outline(hood, w=OUT_W, light=0.45)
    for pts in ([(392, 60), (440, 200), (470, 330)], [(400, 120), (380, 260)]):
        c.line(pts, w=3.0, taper=0.4)

    # ============================================================ 癫狂层：兜帽下的鳃裂 + 两点眼光
    for k in range(3):
        y = 372 + k * 16
        c.stroke(spline([(314, y - 12), (334, y), (356, y - 7)]), 7.0, layer='mad', taper=0.2)
    for x, y in ((300, 316), (326, 312)):
        c.add(c.ellipse(x, y, 9, 7), 'mad')
