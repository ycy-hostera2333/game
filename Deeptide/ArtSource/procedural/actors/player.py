"""调查员（主角，中档）：长风衣 + 宽檐帽，左手提灯；3/4 背身面朝右，帽檐阴影遮住上半张脸（策划/10 §5.3）。
癫狂层：影子里长出触须；提灯火焰变青。

背对观者、略朝右；近侧（左手）垂着一盏防风提灯，灯在身体右前方。风衣下摆到小腿，后背有一道开衩和腰带。
脸只露出右边一条脸颊和下颌，其余都在帽檐的影子里。
"""
import math

import numpy as np

from etch import STRUCT, Frame, ellipse_pts, smoothstep, spline
from cards.C02 import lantern
from cards._common import tentacle

NAME = '调查员'
SIZE = (400, 680)

OUT_W = 11.0
FEET = 1306

LANTERN = [(-92, 0), (-100, -30), (-80, -60), (80, -60), (100, -30), (92, 0)]
GLOBE = [(-56, -60), (-82, -130), (-74, -200), (-40, -238), (40, -238), (74, -200), (82, -130), (56, -60)]
CAP = [(-56, -236), (-48, -262), (-20, -280), (20, -280), (48, -262), (56, -236)]


def draw(c):
    # ------------------------------------------------------------ 形状
    coat = [(300, 330), (500, 320), (560, 400), (580, 560), (600, 800), (640, 1080), (660, 1160), (560, 1180), (420, 1170),
            (300, 1182), (180, 1166), (200, 1000), (220, 760), (232, 540), (250, 400)]
    legs = [[(270, 1160), (340, 1160), (334, 1290), (262, 1294)], [(430, 1160), (500, 1160), (510, 1292), (438, 1292)]]
    boots = [[(250, 1250), (346, 1248), (350, 1306), (236, 1308)], [(424, 1250), (518, 1250), (540, 1306), (430, 1308)]]
    head = (420, 250, 70, 84)
    brim = (410, 180, 196, 44)
    crown = [(320, 186), (330, 92), (400, 70), (480, 80), (504, 182)]
    arm = [(520, 380), (590, 470), (622, 640), (620, 760), (580, 770), (566, 650), (540, 500)]
    shadow = c.ellipse(420, FEET + 2, 300, 24)
    L = Frame((630, 1060), scale=0.66)                                       # 提灯：灯底的位置，灯身往上

    coat_m = c.mask(spline(coat, closed=True))
    arm_m = c.mask(spline(arm, closed=True))
    leg_m = np.clip(sum(c.mask(np.array(l)) for l in legs), 0, 1)
    boot_m = np.clip(sum(c.mask(spline(b, closed=True)) for b in boots), 0, 1)
    head_m = c.ellipse(*head)
    brim_m = c.ellipse(*brim, rot=-4)
    crown_m = c.mask(spline(crown, closed=True))
    lamp_m = np.clip(c.mask(spline(L(LANTERN), closed=True)) + c.mask(spline(L(GLOBE), closed=True))
                     + c.mask(spline(L(CAP), closed=True)), 0, 1)
    bail = spline([(600, 790), L.p(-60, -300), L.p(0, -360), L.p(60, -300), L.p(60, -236)])
    bail_m = c.stroke(bail, 6, add=False, taper=0)
    fill = np.clip(coat_m + arm_m + leg_m + boot_m + head_m + brim_m + crown_m + lamp_m + bail_m + shadow, 0, 1)
    c.add(fill, 'fill')

    side = c.grad(230, 0, 640, 0)

    c.engrave(shadow, 0.9, [c.ph_lin(0, wobble=0.6), c.ph_lin(60)], spacing=7, swell=1.2)
    # ------------------------------------------------------------ 腿和靴子
    for l, b in zip(legs, boots):
        m = c.mask(np.array(l))
        c.engrave(m, 0.88, [c.ph_lin(84, seed=1), c.ph_lin(10, seed=2)], spacing=7.5, swell=1.2)
        c.stroke(np.array(l + l[:1]), STRUCT, taper=0)
        bm = c.mask(spline(b, closed=True))
        lam = c.relief(bm, radius=20, blur=1.5)
        c.engrave(bm, np.clip(0.95 - 0.5 * lam, 0.5, 1), [c.ph_lin(0, seed=3), c.ph_lin(70, seed=4)], spacing=7)
        c.outline(b, w=STRUCT + 1, light=0.4)
        c.erase(c.stroke(spline([(b[0][0] + 12, b[0][1] + 10), (b[1][0] - 14, b[1][1] + 8)]), 2.4, add=False, taper=0.3))

    # ------------------------------------------------------------ 风衣：后背，左亮右暗，后开衩、腰带、褶
    lam = c.relief(coat_m, radius=130, blur=6)
    fold = np.sin((c.xx - 420) / 40 + 0.5 * np.sin(c.yy / 180))
    tone = np.clip(0.45 + 0.4 * side + 0.22 * (1 - lam) - 0.1 * fold * smoothstep(700, 1100, c.yy), 0, 1)
    c.engrave(coat_m, tone, [c.ph_lin(86, wobble=1.5, seed=5), c.ph_lin(22, seed=6), c.ph_lin(-38, seed=7)], spacing=8.5,
              swell=1.2)
    c.line([(420, 820), (424, 1000), (430, 1176)], w=STRUCT, taper=(0.3, 0.0))     # 后开衩
    c.stroke(spline([(240, 720), (420, 740), (590, 724)]), 12, taper=0)            # 腰带
    c.erase(c.stroke(spline([(250, 714), (420, 734), (580, 718)]), 2.4, add=False, taper=0.2))
    for pts in ([(300, 760), (280, 960), (250, 1160)], [(520, 760), (560, 960), (600, 1150)], [(360, 400), (350, 560), (340, 700)]):
        c.line(pts, w=3.2, taper=0.4)
    c.line([(300, 340), (330, 420), (420, 450), (520, 420), (548, 360)], w=STRUCT, taper=0.2)   # 披肩的下沿
    c.outline(coat, w=OUT_W, light=0.45)

    # ------------------------------------------------------------ 近侧手臂：垂着，手里提灯
    lam = c.relief(arm_m, radius=30, blur=2)
    c.engrave(arm_m, np.clip(0.98 - 0.45 * lam, 0.5, 1), [c.ph_axis((520, 380), (600, 770), bow=-10, half=40), c.ph_lin(20)],
              spacing=8, swell=1.1)
    c.outline(arm, w=STRUCT + 1, light=0.4)
    c.line([(566, 720), (594, 730), (620, 724)], w=STRUCT, taper=0.1)             # 袖口
    c.stroke(bail, 5, taper=0)                                                    # 提环
    lantern(c, L)
    fist = c.ellipse(600, 784, 28, 24)
    c.clear(fist, layers=('ink',))
    c.engrave(fist, 0.4, [c.ph_lin(30)], spacing=6, wmax=2.2)
    c.stroke(ellipse_pts(600, 784, 28, 24), 4, closed=True)

    # ------------------------------------------------------------ 头：后脑 + 竖起的领子 + 宽檐帽；右边露出一条脸颊
    lam = c.relief(head_m, radius=40, blur=2)
    c.engrave(head_m, np.clip(0.95 - 0.3 * lam, 0.65, 1), [c.ph_lin(80, wobble=2.5, seed=12), c.ph_lin(20, seed=13)], spacing=7,
              swell=1.3)
    cheek = [(470, 220), (492, 250), (494, 300), (470, 330), (450, 300)]
    ch = c.mask(spline(cheek, closed=True))
    c.clear(ch, layers=('ink',))
    c.engrave(ch, 0.25 + 0.5 * c.grad(0, 210, 0, 260), [c.ph_lin(80)], spacing=6, wmax=2.2)
    c.outline(cheek, w=3.4, light=0.4)
    collar = [(330, 260), (380, 300), (420, 340), (470, 320), (520, 270), (540, 330), (500, 380), (420, 400), (330, 380), (300, 330)]
    cm = c.mask(spline(collar, closed=True))
    c.engrave(cm, 0.8, [c.ph_lin(70, seed=8), c.ph_lin(-20, seed=9)], spacing=7.5)
    c.outline(collar, w=STRUCT + 1, light=0.4)
    # 帽子
    lam = c.relief(crown_m, radius=30, blur=2)
    c.engrave(crown_m, np.clip(0.98 - 0.45 * lam, 0.5, 1), [c.ph_lin(86, seed=10), c.ph_lin(14, seed=11)], spacing=7.5, swell=1.2)
    c.outline(crown, w=STRUCT + 1, light=0.4)
    c.stroke(spline([(324, 160), (410, 168), (500, 160)]), 12, taper=0.05)      # 帽带
    c.engrave(brim_m, np.clip(0.75 + 0.2 * c.grad(214, 0, 606, 0), 0, 1), [c.ph_ring(410, 180, sy=0.22), c.ph_lin(0)], spacing=6.5)
    c.stroke(ellipse_pts(*brim, rot=-4), OUT_W - 2, closed=True, light=0.3)
    c.stroke(ellipse_pts(410, 186, 120, 24, rot=-4, a0=0, a1=180), 3.0)          # 帽檐和帽身的交线

    # ============================================================ 癫狂层：影子里长出触须；提灯火焰变青
    for x0, h, bend, w in ((190, 420, -1, 62), (110, 280, 1, 46), (670, 380, 1, 56), (740, 240, -1, 40), (300, 180, 1, 34)):
        ctrl = [(x0, FEET + 6)]
        for j in range(1, 6):
            t = j / 5
            ctrl.append((x0 + bend * 60 * math.sin(t * 2.4) + (x0 - 420) * 0.15 * t, FEET + 6 - h * t))
        tentacle(c, ctrl, w, layer='mad', tone=0.6, taper=0.92, outline=4.5)
    flame = [L.p(0, -200), L.p(18, -160), L.p(20, -126), L.p(10, -104), L.p(-10, -104), L.p(-20, -126), L.p(-18, -160)]
    c.fill(c.mask(spline(flame, closed=True)), layer='mad', value=1.0)
    for a in np.arange(0, 360, 30):                                           # 青光往外放
        q = math.radians(a)
        p0 = L.p(30 * math.cos(q), -150 + 40 * math.sin(q))
        p1 = L.p(60 * math.cos(q), -150 + 80 * math.sin(q))
        c.stroke([p0, p1], 2.6, layer='mad', taper=(0.1, 0.9))
