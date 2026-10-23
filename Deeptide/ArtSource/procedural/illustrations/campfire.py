"""篝火（策划/10 §8 P2：「中央火盆，调查员坐着的背影，三个选项」）。

礁石滩上的夜：天上几颗星，远处海面上灯塔扫过来一道光。画面中间是三条腿的铁火盆，火苗往上卷，火星往上飘；
火光在地上照出一圈亮，圈外全黑。调查员坐在左边的木箱上，背对我们、侧身朝着火伸手烤火，提灯放在脚边；
火光在他右侧（朝火那边）描出一道亮边，影子朝左后方拖出去。
癫狂层：火光照不到的黑暗里一对对眼睛，海里伸起来的一只巨手。
"""
import math

import numpy as np

from etch import Frame, ellipse_pts, poly, smoothstep, spline
from cards._common import burst, eye
from illustrations._kit import GAP, OUT, STRUCT, flame, hatch, night_sky, sea

NAME = '篝火'
SIZE = (1920, 1280)
HORIZON = 470
FIRE = (1080, 860)          # 火盆口的中心


def draw(c):
    rng = np.random.default_rng(9)
    W, H = c.w, c.h
    fx, fy = FIRE

    # ------------------------------------------------------------ 夜空、星、海、灯塔的光
    night_sky(c, HORIZON, top=0.95, bottom=0.78, seed=10)
    for _ in range(60):
        x, y = rng.uniform(0, W), rng.uniform(10, HORIZON - 40)
        r = rng.uniform(1.6, 3.4)
        c.erase(c.ellipse(x, y, r, r))
        if r > 3:
            c.erase(c.stroke([(x - 9, y), (x + 9, y)], 1.4, add=False, taper=0.5))
            c.erase(c.stroke([(x, y - 9), (x, y + 9)], 1.4, add=False, taper=0.5))
    sea(c, HORIZON, 640, tone=0.85, seed=11, glints=20, rng=rng)
    lx = 1700
    c.paint(c.mask(poly([(lx - 10, HORIZON), (lx - 6, HORIZON - 60), (lx + 6, HORIZON - 60), (lx + 10, HORIZON)])), 1.0)
    for a in (-8, -3):                                                     # 灯塔扫过来的一道光，刻在夜空里
        q = math.radians(180 + a)
        c.erase(c.stroke([(lx, HORIZON - 64), (lx + 1500 * math.cos(q), HORIZON - 64 + 1500 * math.sin(q))], 6, add=False,
                         taper=(0.0, 1.0)) * 0.7)
    c.stroke([(0, HORIZON), (W, HORIZON)], 3, taper=0)

    # ------------------------------------------------------------ 礁石滩：黑，火盆周围一圈被照亮
    ground = c.mask(spline([(-20, 640), (400, 610), (900, 630), (1400, 612), (1960, 640), (1960, H + 20), (-20, H + 20)], closed=True))
    lit = 1 - c.radial(fx, fy + 60, 80, 700, sx=1.0, sy=0.45)
    stones = c.noise(30, 12)
    hatch(c, ground, np.clip(0.96 - 0.8 * lit + 0.08 * stones, 0, 1), [c.ph_lin(6, wobble=2.5, seed=13), c.ph_lin(-40, seed=14),
                                                                        c.ph_lin(70, seed=15)], swell=1.3)
    for _ in range(40):                                                    # 圈里的卵石：亮面朝火
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(200, 560)
        x, y = fx + r * math.cos(a), fy + 70 + r * 0.42 * math.sin(a)
        rr = rng.uniform(10, 26)
        c.stroke(ellipse_pts(x, y, rr, rr * 0.55, a0=180, a1=360), 2.6, taper=0.2)
    c.stroke(spline([(-20, 640), (400, 610), (900, 630), (1400, 612), (1960, 640)]), 4, taper=0)

    # ------------------------------------------------------------ 火盆：先擦出一圈光，再画盆、腿和火
    c.erase((1 - c.radial(fx, fy - 140, 40, 420, sy=1.1)) * 0.85)
    burst(c, fx, fy - 170, 170, 360, n=36, width=2.4, erase=True, sy=0.95, seed=5)
    for foot in ((fx - 130, fy + 180), (fx + 130, fy + 180), (fx + 10, fy + 200)):   # 三条腿
        c.stroke(spline([(fx + (foot[0] - fx) * 0.3, fy + 30), ((fx + foot[0]) / 2, fy + 110), foot]), 12, taper=(0.0, 0.2))
    bowl = [(fx - 170, fy), (fx - 140, fy + 60), (fx - 60, fy + 92), (fx + 60, fy + 92), (fx + 140, fy + 60), (fx + 170, fy)]
    bm = c.mask(spline(bowl, closed=True))
    lam = c.relief(bm, radius=30)
    hatch(c, bm, np.clip(0.9 - 0.4 * lam, 0, 1), [c.ph_axis((fx - 170, fy), (fx + 170, fy), bow=-20, half=60, seed=16)], spacing=7.5)
    c.outline(bowl, w=OUT, light=0.3)
    c.stroke(ellipse_pts(fx, fy, 172, 22), STRUCT + 1, closed=True)
    for k in range(-2, 3):                                                 # 盆沿的铆钉
        c.fill(c.ellipse(fx + k * 64, fy + 28 + abs(k) * -6, 7, 7), value=1.0)
    flame(c, fx, fy - 6, 300, 380, seed=6)
    for _ in range(46):                                                    # 火星往上飘
        x = fx + rng.normal(0, 120) + rng.uniform(0, 160)
        y = fy - 300 - rng.uniform(0, 380)
        c.erase(c.ellipse(x, y, 3, 3))

    sitter(c, rng)

    # ============================================================ 癫狂层：黑暗里的眼睛，海里伸起来的巨手
    for x, y, s in ((180, 760, 22), (300, 700, 18), (1650, 760, 24), (1790, 820, 20), (240, 1120, 26), (1720, 1140, 28),
                    (600, 660, 14), (1500, 670, 14)):
        for sd in (-1, 1):
            eye(c, x + sd * s * 1.6, y, s, layer='mad', look=0.5 if x < fx else -0.5, width=3.0, slit=True)
    hand = [(380, 640), (360, 520), (330, 420), (300, 330), (330, 300), (360, 360), (380, 280), (410, 270), (420, 350), (440, 270),
            (470, 270), (470, 360), (500, 300), (530, 320), (500, 420), (480, 520), (470, 640)]
    c.stroke(spline(hand), 6, layer='mad', taper=0.1)
    hm = c.mask(spline(hand, closed=True))
    c.add(hm * c.lines(c.ph_lin(80, seed=17), 9, 2.2), 'mad')


def sitter(c, rng):
    """坐在木箱上的调查员：背影侧身朝右（朝火），双手伸向火；木箱、脚边的提灯、往左后拖的影子。"""
    # 影子：火光把他投到左后方
    sh = c.mask(poly([(560, 1060), (760, 1060), (200, 1230), (-40, 1230)]))
    c.fill(c.shadow_of(sh, 0, 0, blur=10), value=1.0)
    # 木箱
    front = [(520, 940), (760, 940), (760, 1070), (520, 1070)]
    top = [(520, 940), (760, 940), (800, 910), (560, 910)]
    side = [(760, 940), (800, 910), (800, 1036), (760, 1070)]
    for pts, tone in ((front, 0.62), (top, 0.3), (side, 0.25)):
        m = c.mask(poly(pts))
        hatch(c, m, tone + 0.1 * c.noise(30, 20), [c.ph_lin(0 if pts is not side else 90, wobble=1.2, seed=21), c.ph_lin(60, seed=22)],
              spacing=7.5)
        c.stroke(poly(pts + [pts[0]]), STRUCT, taper=0)
    for y in (980, 1020):
        c.stroke([(520, y), (760, y)], 2.4, taper=0)
    # 身体：背影，上身微微前倾朝火；风衣下摆盖在箱子上
    F = Frame((650, 930), rot=6, scale=1.0)
    coat = [(-110, 20), (-90, -60), (-70, -150), (-66, -230), (-40, -262), (30, -262), (62, -236), (70, -160), (88, -60), (120, 20)]
    m = c.mask(spline(F.cp(coat), closed=True))
    lam = c.relief(m, radius=34, blur=2)
    rim = smoothstep(0.0, 1.0, c.grad(600, 0, 740, 0))                   # 朝火的一侧（右）被照亮
    hatch(c, m, np.clip(0.98 - 0.35 * lam - 0.45 * rim * (lam > 0.2), 0.3, 1),
          [c.ph_lin(F.angle(86), seed=23), c.ph_lin(F.angle(28), seed=24)], swell=1.2)
    c.outline(F.cp(coat), w=STRUCT + 1, light=0.4)
    # 伸向火的胳膊：从右肩伸出去，手掌张开朝火
    arm = F.cp([(56, -220), (130, -190), (200, -175)])
    c.line(arm, w=26, taper=(0.0, 0.1))
    hx, hy = F.p(212, -174)
    c.clear(c.ellipse(hx, hy, 16, 20, rot=10))
    c.stroke(ellipse_pts(hx, hy, 16, 20, rot=10), 2.6, closed=True)
    for k in range(4):
        a = math.radians(-30 + k * 18)
        c.line([(hx + 10, hy - 10 + k * 7), (hx + 34 * math.cos(a), hy - 10 + k * 7 + 34 * math.sin(a))], w=6, taper=(0.0, 0.5))
    # 领子、头、帽
    c.fill(c.mask(spline(F.cp([(-34, -256), (-30, -284), (0, -278), (30, -284), (34, -256)]), closed=True)), value=0.85)
    c.fill(c.ellipse(*F.p(4, -296), 21, 24), value=0.97)
    c.fill(c.ellipse(*F.p(4, -312), 66, 13), value=1.0)
    c.fill(c.mask(F([(-24, -312), (-20, -356), (26, -356), (30, -312)])), value=1.0)
    c.erase(c.stroke(spline(F.cp([(60, -314), (66, -300), (40, -290)])), 3, add=False, taper=0.3))   # 帽檐右边一道火光
    c.erase(c.stroke(spline(F.cp([(64, -236), (74, -160), (92, -60), (120, 16)])), 4, add=False, taper=0.2))   # 风衣右边的亮边
    # 脚边的提灯（熄着的）
    from cards.C02 import lantern
    lantern(c, Frame((470, 1080), scale=0.42))
