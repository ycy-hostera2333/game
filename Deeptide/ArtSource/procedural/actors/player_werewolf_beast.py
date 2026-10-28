"""荒原猎户的兽形（策划/14 §2.1）：四脚着地、面朝右的狼，肩胛高耸、头往前探，但有鳞、有鳃裂，不是普通的狼。
后半身长满鳞，前半身是毛；颈侧三道鳃裂和眼睛是深渊色。比人形宽、矮一些，脚底照样在底边往上 4%。
癫狂层：背脊上竖起一排半透明的鳍棘（兽形本身就是癫狂，所以这一层在战斗里几乎一直亮着）。
"""
import numpy as np

from etch import Frame, OUTLINE, spline
from cards import _wolf as w

NAME = '兽形'
SIZE = (720, 520)

FEET = 998
BODY = [(300, 600), (340, 520), (450, 490), (600, 515), (760, 450), (870, 385), (960, 395), (1010, 470), (1030, 560), (1015, 680),
        (1045, 800), (1055, 930), (1095, 992, 'c'), (1005, 998), (990, 930), (962, 820), (935, 765), (918, 840), (930, 940),
        (955, 994, 'c'), (878, 998), (866, 930), (856, 800), (820, 705), (700, 690), (560, 700), (480, 720), (470, 800), (500, 920),
        (528, 994, 'c'), (450, 998), (430, 920), (405, 830), (385, 880), (385, 960), (405, 994, 'c'), (330, 998), (318, 900),
        (300, 780), (288, 680)]
TAIL = [(330, 560), (250, 620), (205, 720), (190, 820), (196, 880)]
HEAD = Frame((1120, 500), rot=16, scale=0.78)


def draw(c):
    body = c.mask(spline(BODY, closed=True))
    tail = c.stroke(spline(TAIL), 86, add=False, taper=(0.0, 0.8))
    shadow = c.ellipse(690, FEET + 2, 520, 22)
    m = np.clip(body + tail, 0, 1)
    c.add(np.clip(m + shadow, 0, 1), 'fill')
    c.engrave(shadow, 0.9, [c.ph_lin(0, wobble=0.6), c.ph_lin(60)], spacing=7, swell=1.2)

    # ------------------------------------------------------------ 身体：浮雕排线，前毛后鳞
    lam = c.relief(m, radius=90, blur=6)
    tone = np.clip(1.02 - 0.42 * lam + 0.12 * c.grad(0, 500, 0, FEET) + 0.04 * c.noise(30, 1), 0, 1)
    c.engrave(m, tone, [c.ph_lin(80, wobble=1.6, seed=2), c.ph_lin(20, seed=3), c.ph_lin(-40, seed=4)], spacing=7.5, swell=1.2)
    back = m * np.clip(1 - c.grad(560, 0, 820, 0), 0, 1) * (c.inside(m) > 4)
    w.scales(c, back, size=26, angle=180, tone=0.45, w=2.4, seed=5)
    w.fur(c, m * c.grad(640, 0, 960, 0), 105, length=34, density=0.004, seed=6)
    w.fringe(c, spline(BODY[3:8]), -10, length=30, step=8, seed=7)             # 肩背上竖起的毛
    w.fringe(c, spline(BODY[22:27]), 20, length=26, step=8, seed=8)            # 肚子下的毛
    w.fringe(c, spline(BODY[7:11]), 10, length=34, step=7, seed=9)             # 胸前的鬃
    c.outline(BODY, w=OUTLINE + 1, light=0.45)
    c.line(TAIL, w=OUTLINE - 2, taper=(0.0, 0.8))
    for x0 in (1060, 940, 510, 390):                                           # 爪
        for k in (-1, 0, 1):
            c.line([(x0 + 12 * k, FEET - 10), (x0 + 12 * k + 20, FEET + 2)], w=5, taper=(0.0, 0.8))
    for pts in ([(990, 760), (1010, 820)], [(880, 760), (890, 830)], [(470, 760), (480, 830)], [(360, 800), (390, 860)]):
        c.line(pts, w=3.4, taper=0.3)                                          # 腿上的关节

    # ------------------------------------------------------------ 头：龇着牙往前探
    hm = w.wolf_head(c, HEAD, jaw=12, seed=10)
    c.add(hm, 'fill')

    # ============================================================ 癫狂层：背脊上的鳍棘
    for k in range(9):
        t = k / 8
        x = 400 + 470 * t
        y = 500 - 100 * t ** 1.5 + 6
        h = 70 + 40 * np.sin(t * np.pi)
        base0, base1, tip = (x - 22, y + 12), (x + 22, y + 8), (x - 26, y - h)
        fin = c.mask([base0, tip, base1])
        c.engrave(fin, 0.45, [c.ph_lin(-70, seed=11 + k)], layer='mad', spacing=6, wmax=2.2, grain=0)
        c.stroke([base0, tip, base1], 3, layer='mad', taper=0)
