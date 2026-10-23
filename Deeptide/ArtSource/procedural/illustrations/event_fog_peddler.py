"""事件 7 雾中商贩：雾里传来铃声，一个看不清脸的商贩推着车停在你面前（Data/events.csv）。商店界面也用这张（只截左半）。

起雾的街：两边的房子只剩很淡的轮廓，远处一盏街灯。左半是商贩——高高的斗篷一直拖到地上，兜帽里是一片黑，
只露出一双苍白的手：一只举着小铃（铃声画成几道弧），一只搭在推车的把手上。右半是两轮推车：车厢、布篷、
架子上的瓶罐，篷下挂着提灯和用绳子吊着的药瓶。商店界面截左半（x < 960），所以商贩整个落在左半。
癫狂层：兜帽里睁开的两只眼、瓶子里浮着的眼珠、布篷的边垂下来变成触须。
"""
import math

import numpy as np

from etch import Frame, ellipse_pts, poly, resample, smoothstep, spline
from cards._common import eye, tentacle
from illustrations._kit import GAP, OUT, STRUCT, hatch

NAME = '雾中商贩'
SIZE = (1920, 1280)
GROUND = 1150
VP = (1180, 620)


def draw(c):
    rng = np.random.default_rng(7)
    W, H = c.w, c.h

    # ------------------------------------------------------------ 雾：整张很淡的横排线，越远越白
    fog = c.rect(0, 0, W, H)
    hatch(c, fog, np.clip(0.22 + 0.12 * c.noise(200, 1) + 0.1 * c.grad(0, 300, 0, H), 0, 1),
          [c.ph_wave(-1, 6, 600, wobble=4, seed=2)], spacing=11, wmax=2.0, solid=None, bands=[(0.05, 0.7)])
    # 两边的房子：很淡，往灭点收
    for k, (x0, x1, top) in enumerate(((0, 220, 80), (220, 420, 260), (420, 560, 380), (1560, 1920, 60), (1420, 1560, 300))):
        far = 1 - abs((x0 + x1) / 2 - VP[0]) / 1200
        bot = GROUND - 60 * far
        face = c.mask(poly([(x0, top), (x1, top + 30), (x1, bot), (x0, bot)]))
        hatch(c, face, np.clip(0.3 + 0.1 * far, 0, 1), [c.ph_lin(88, seed=3 + k)], spacing=11, wmax=2.0, solid=None)
        c.stroke(poly([(x0, top), (x1, top + 30), (x1, bot)]), 2.2, taper=0)
        for j in range(3):                                                  # 窗
            wy = top + 80 + j * 160
            if wy > bot - 100:
                break
            win = c.rect(x0 + (x1 - x0) * 0.3, wy, x0 + (x1 - x0) * 0.6, wy + 70)
            hatch(c, win, 0.55, [c.ph_lin(0, seed=60 + j), c.ph_lin(90, seed=70 + j)], spacing=9, wmax=2.0, solid=None)
            c.stroke(poly([(x0 + (x1 - x0) * 0.3, wy), (x0 + (x1 - x0) * 0.6, wy), (x0 + (x1 - x0) * 0.6, wy + 70),
                           (x0 + (x1 - x0) * 0.3, wy + 70), (x0 + (x1 - x0) * 0.3, wy)]), 2.0, taper=0)
    # 远处的街灯：一团光
    lx, ly = 1180, 470
    c.erase((1 - c.radial(lx, ly, 10, 220)) * 0.8)
    c.stroke([(lx, ly + 20), (lx, GROUND - 120)], 4, taper=0)
    c.stroke(poly([(lx - 14, ly - 10), (lx + 14, ly - 10), (lx + 10, ly + 20), (lx - 10, ly + 20), (lx - 14, ly - 10)]), 2.4, taper=0)
    # 地面：湿石板，近处深
    ground = c.rect(0, GROUND - 120, W, H)
    hatch(c, ground, np.clip(0.2 + 0.45 * c.grad(0, GROUND - 120, 0, H), 0, 1), [c.ph_lin(2, wobble=1.5, seed=8), c.ph_ray(*VP, r0=900)],
          spacing=9)
    for k in range(1, 6):
        y = GROUND - 120 + 160 * (k / 5) ** 1.6
        c.stroke(spline([(0, y), (W / 2, y + 4), (W, y)]), 1.6 + k * 0.4, taper=0)

    cart(c, rng)
    peddler(c, rng)

    # ============================================================ 癫狂层
    for sd in (-1, 1):                                                     # 兜帽里的两只眼
        eye(c, 470 + sd * 34, 452, 22, layer='mad', look=0.3, width=3.6, slit=True)
    for x, y in ((1300, 640), (1420, 655), (1560, 640), (1360, 790), (1500, 800)):   # 瓶子里的眼珠
        c.add(c.stroke(ellipse_pts(x, y, 13, 13), 3, closed=True, add=False), 'mad')
        c.add(c.ellipse(x + 3, y, 5, 5), 'mad')
    for x0 in (1110, 1330, 1550, 1760):                                    # 布篷的边垂成触须
        tentacle(c, [(x0, 462), (x0 + 10, 540), (x0 - 16, 610), (x0 + 6, 680)], 26, layer='mad', tone=0.5, taper=0.92, outline=3.0)


def cart(c, rng):
    """两轮推车：车厢在 x 1080–1800，布篷撑在四根杆上，架子上摆瓶罐，篷下吊着提灯和药瓶。"""
    x0, x1, y0, y1 = 1080, 1800, 560, 920
    # 车厢正面：木板
    box = c.rect(x0, y0 + 160, x1, y1)
    hatch(c, box, np.clip(0.7 + 0.12 * c.noise(40, 20), 0, 1), [c.ph_lin(2, wobble=1.0, seed=21), c.ph_lin(70, seed=22)], spacing=8)
    for y in np.arange(y0 + 200, y1, 40):
        c.stroke([(x0, y), (x1, y)], 2.0, taper=0)
    c.stroke(poly([(x0, y0 + 160), (x1, y0 + 160), (x1, y1), (x0, y1), (x0, y0 + 160)]), OUT, taper=0)
    # 架子：车厢上面两层，摆着瓶罐和书
    for k, y in enumerate((y0 + 160, y0 + 40)):
        c.stroke([(x0 + 20, y), (x1 - 20, y)], 8, taper=0)
        x = x0 + 50
        while x < x1 - 60:
            kind = rng.integers(0, 3)
            if kind == 0:                                                  # 瓶
                w, h = rng.uniform(26, 40), rng.uniform(70, 110)
                body = [(x - w / 2, y), (x - w / 2, y - h * 0.6), (x - w * 0.2, y - h * 0.75), (x - w * 0.2, y - h), (x + w * 0.2, y - h),
                        (x + w * 0.2, y - h * 0.75), (x + w / 2, y - h * 0.6), (x + w / 2, y)]
                m = c.mask(poly(body))
                c.clear(m)
                hatch(c, m, 0.3 + 0.4 * c.grad(x - w / 2, 0, x + w / 2, 0), [c.ph_lin(90, seed=int(x))], spacing=7.5)
                c.stroke(poly(body + [body[0]]), 2.8, taper=0)
                x += w + rng.uniform(16, 30)
            elif kind == 1:                                                # 罐
                r = rng.uniform(26, 38)
                m = c.ellipse(x, y - r, r, r)
                c.clear(m)
                lam = c.relief(m, radius=r * 0.6)
                hatch(c, m, np.clip(0.75 - 0.6 * lam, 0, 1), [c.ph_ring(x - r * 0.3, y - r * 1.3), c.ph_lin(40, seed=int(x))], spacing=7.5)
                c.stroke(ellipse_pts(x, y - r, r, r), 2.8, closed=True)
                c.paint(c.rect(x - r * 0.5, y - r * 2.1, x + r * 0.5, y - r * 1.8), 0.9)
                x += 2 * r + rng.uniform(16, 30)
            else:                                                          # 一摞书
                for j in range(rng.integers(2, 4)):
                    bw = rng.uniform(70, 100)
                    by = y - j * 22
                    m = c.rect(x - bw / 2, by - 22, x + bw / 2, by)
                    hatch(c, m, rng.uniform(0.5, 0.9), [c.ph_lin(0, seed=int(x) + j)], spacing=7.5)
                    c.stroke(poly([(x - bw / 2, by - 22), (x + bw / 2, by - 22), (x + bw / 2, by), (x - bw / 2, by), (x - bw / 2, by - 22)]),
                             2.4, taper=0)
                x += 110
    # 布篷：撑在四根杆上，下摆是一排垂下来的布角
    for x in (x0 + 20, x1 - 20):
        c.stroke([(x, 470), (x, y0 + 170)], 8, taper=0)
    awning = [(x0 - 40, 470), (x0 + 120, 380), (x1 - 120, 380), (x1 + 40, 470)]
    am = c.mask(poly(awning + [(x1 + 40, 480), (x0 - 40, 480)]))
    hatch(c, am, 0.6, [c.ph_lin(85, seed=23), c.ph_lin(20, seed=24)], spacing=8)
    stripes = c.lines(c.ph_lin(90, wobble=0.5, seed=25), 60, 26)
    c.add(am * stripes * 0.9)
    c.stroke(poly(awning), OUT, taper=0)
    for k in range(9):                                                     # 布角
        xa = x0 - 40 + k * (x1 - x0 + 80) / 9
        xb = xa + (x1 - x0 + 80) / 9
        flap = [(xa, 472), ((xa + xb) / 2, 520), (xb, 472)]
        fm = c.mask(poly(flap))
        hatch(c, fm, 0.55, [c.ph_lin(80, seed=26 + k)], spacing=7.5)
        c.stroke(poly(flap), 3, taper=0)
    # 篷下吊着的提灯和药瓶
    for x, ln, lamp in ((1180, 120, True), (1260, 70, False), (1640, 90, False), (1720, 140, True)):
        c.stroke([(x, 482), (x, 482 + ln)], 2.0, taper=0)
        if lamp:
            from cards.C02 import lantern
            lantern(c, Frame((x, 482 + ln + 80), scale=0.26))
        else:
            m = c.ellipse(x, 482 + ln + 16, 14, 20)
            c.clear(m)
            c.stroke(ellipse_pts(x, 482 + ln + 16, 14, 20), 2.4, closed=True)
            c.add(m * (c.yy > 482 + ln + 20) * 0.6)
    # 两只大轮子：辐条 + 轮圈
    for cx in (1230, 1650):
        cy, r = y1 + 40, 150
        rim = c.ellipse(cx, cy, r, r) * (1 - c.ellipse(cx, cy, r - 22, r - 22))
        c.clear(c.ellipse(cx, cy, r, r))
        hatch(c, rim, 0.85, [c.ph_ring(cx, cy), c.ph_lin(30, seed=27)], spacing=7)
        for k in range(10):
            a = 2 * math.pi * k / 10 + 0.2
            c.stroke([(cx + 20 * math.cos(a), cy + 20 * math.sin(a)), (cx + (r - 18) * math.cos(a), cy + (r - 18) * math.sin(a))], 7,
                     taper=0)
        c.stroke(ellipse_pts(cx, cy, r, r), OUT, closed=True)
        c.stroke(ellipse_pts(cx, cy, r - 22, r - 22), 3, closed=True)
        c.fill(c.ellipse(cx, cy, 24, 24), value=0.95)
        c.fill(c.shadow_of(c.ellipse(cx, cy + r, r * 0.9, 16), 0, 0, blur=4), value=0.9)
    # 把手：伸向商贩
    c.stroke([(x0, y0 + 260), (760, 820)], 12, taper=0)
    c.stroke([(760, 810), (760, 834)], 16, taper=0)


def peddler(c, rng):
    """商贩：高斗篷，兜帽里一片黑；左手（画面左）举着小铃，右手搭在把手上。整个人在 x 120–860。"""
    cloak = [(220, GROUND + 10), (260, 900), (300, 700), (330, 560), (360, 470), (390, 400), (430, 350), (470, 330), (520, 340),
             (570, 380), (610, 450), (640, 560), (680, 720), (720, 900), (780, GROUND + 10)]
    m = c.mask(spline(cloak, closed=True))
    lam = c.relief(m, radius=60, blur=4)
    hatch(c, m, np.clip(0.95 - 0.35 * lam, 0.55, 1), [c.ph_lin(86, seed=30), c.ph_lin(24, seed=31), c.ph_lin(-40, seed=32)], swell=1.3)
    c.outline(cloak, w=OUT + 1, light=0.4)
    for pts in ([(420, 520), (390, 800), (360, GROUND)], [(510, 520), (520, 820), (540, GROUND)], [(590, 560), (640, 860), (680, GROUND)]):
        c.line(pts, w=3, taper=0.3)                                         # 斗篷的长褶
    # 兜帽：开口里是全黑
    opening = [(420, 400), (450, 370), (500, 368), (530, 396), (536, 470), (500, 520), (450, 518), (414, 470)]
    c.fill(c.mask(spline(opening, closed=True)), value=1.0)
    c.outline(opening, w=STRUCT, light=0.2)
    # 举着铃的左手：袖口垂下来，手腕苍白，铃是个小钟形
    sleeve = [(330, 620), (290, 540), (270, 470), (310, 450), (350, 520), (380, 600)]
    sm = c.mask(spline(sleeve, closed=True))
    hatch(c, sm, 0.9, [c.ph_lin(60, seed=33)], spacing=7.5)
    c.outline(sleeve, w=STRUCT, light=0.3)
    hand(c, 288, 440, -100)
    bell = [(252, 368, 'c'), (262, 340), (266, 312), (288, 292), (310, 312), (314, 340), (324, 368, 'c')]   # 钟形：顶圆、口外撇
    bm = c.mask(spline(bell, closed=True))
    c.clear(bm)
    lamb = c.relief(bm, radius=14)
    hatch(c, bm, np.clip(0.7 - 0.6 * lamb, 0, 1), [c.ph_lin(85, seed=34)], spacing=7)
    c.outline(bell, w=STRUCT, light=0.3)
    c.fill(c.ellipse(288, 376, 9, 9), value=1.0)                           # 铃舌
    c.stroke([(288, 292), (288, 270)], 4, taper=0)                         # 铃柄
    for k in range(3):                                                     # 铃声：几道弧
        r = 70 + k * 40
        c.stroke(ellipse_pts(288, 330, r, r * 0.8, a0=200, a1=250), 3 - k * 0.6, taper=0.2)
        c.stroke(ellipse_pts(288, 330, r, r * 0.8, a0=290, a1=340), 3 - k * 0.6, taper=0.2)
    # 搭在把手上的右手
    c.line([(620, 640), (690, 740), (742, 806)], w=40, taper=(0.0, 0.1))
    hand(c, 760, 812, 20)


def hand(c, x, y, ang):
    """苍白瘦长的手：手掌 + 四根细长的手指，朝 ang 方向（度）。"""
    a = math.radians(ang)
    palm = c.ellipse(x, y, 22, 17, rot=ang)
    c.clear(palm)
    c.stroke(ellipse_pts(x, y, 22, 17, rot=ang), 2.6, closed=True)
    for k in range(4):
        fa = a + math.radians(-30 + k * 20)
        p0 = (x + 18 * math.cos(fa), y + 14 * math.sin(fa))
        p1 = (p0[0] + 40 * math.cos(fa), p0[1] + 40 * math.sin(fa))
        p2 = (p1[0] + 16 * math.cos(fa + 0.7), p1[1] + 16 * math.sin(fa + 0.7))
        f = c.stroke(spline([p0, p1, p2]), 9, add=False, taper=(0.0, 0.5))
        c.clear(f)
        c.add(c.stroke(spline([p0, p1, p2]), 9, add=False, taper=(0.0, 0.5)) - c.stroke(spline([p0, p1, p2]), 5, add=False, taper=(0.0, 0.5)))
