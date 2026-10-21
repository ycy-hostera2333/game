"""C08 坠落：从码头边向黑水坠落的背影，水下有一只巨眼（深渊遮罩）（策划/10 §4.6）。

站在码头上往前看：左下角是码头边的木板和桩，前面是黑水，远处是雾；
调查员背对我们，刚从码头边栽下去，张着手，帽子飞了；水面下一只巨大的眼睛正对着他，只有这只眼着深渊青。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, smoothstep, spline

NAME = '坠落'

HORIZON = 210
EYE = (640, 650)


def draw(c):
    ex, ey = EYE

    # ------------------------------------------------------------ 雾和天：上面亮
    sky = c.rect(0, 0, c.w, HORIZON + 2)
    c.engrave(sky, 0.04 + 0.22 * c.grad(0, 0, 0, HORIZON) ** 2 + 0.05 * c.noise(80, 1),
              [c.ph_wave(0, 4, 300, wobble=2, seed=2)], spacing=11)
    # 远处的灯塔剪影和雾里的光
    c.fill(c.mask(poly([(1010, HORIZON), (1022, 120), (1046, 120), (1058, HORIZON)])), value=0.8)
    c.fill(c.mask(poly([(1014, 122), (1034, 96), (1054, 122)])), value=0.9)
    for a in (-18, -8, 2):
        c.stroke(poly([(1034, 112), (1034 + 260 * math.cos(math.radians(180 + a)), 112 + 260 * math.sin(math.radians(180 + a)))]),
                 2.0, taper=(0.0, 1.0))

    # ------------------------------------------------------------ 黑水：近处的排线疏、远处密；眼睛周围一圈圈涟漪
    water = 1 - sky
    depth = c.grad(0, HORIZON, 0, c.h)                                     # 0 = 远，1 = 近
    r_eye = np.hypot((c.xx - ex) / 1.9, c.yy - ey)
    tone = 0.9 - 0.08 * depth + 0.05 * c.noise(50, 3)
    # 透视：越近的水，波纹越宽；相位按深度压缩，让线距从远处 6px 渐变到近处 13px
    yd = (c.yy - HORIZON) / (c.h - HORIZON)
    ripple = (c.h - HORIZON) * (yd + 0.55 * yd * yd) / 1.55 + 5 * np.sin(c.xx / (40 + 80 * yd) + 2.5 * c.noise(90, 4)) * (0.2 + yd)
    c.engrave(water, tone, [ripple, c.ph_lin(-4, wobble=3, seed=6)], spacing=9, swell=1.3, solid=None,
              bands=[(0.04, 0.45), (0.6, 0.95)])
    # 水面上的反光：几道短白线
    rng = np.random.default_rng(8)
    for _ in range(60):
        y = rng.uniform(HORIZON + 10, c.h)
        x = rng.uniform(0, c.w)
        ln = 10 + 50 * (y - HORIZON) / (c.h - HORIZON)
        c.erase(c.stroke(poly([(x, y), (x + ln, y + rng.uniform(-1, 1))]), 1.5 + 2 * (y - HORIZON) / c.h, add=False, taper=0.5))
    c.stroke(poly([(0, HORIZON), (c.w, HORIZON)]), 3, taper=0)

    # ------------------------------------------------------------ 巨眼：深渊遮罩
    W, Hh = 470, 190
    upper = spline([(ex - W, ey, 'c'), (ex - W * 0.45, ey - Hh), (ex + W * 0.45, ey - Hh), (ex + W, ey, 'c')])
    lower = spline([(ex + W, ey, 'c'), (ex + W * 0.45, ey + Hh * 0.8), (ex - W * 0.45, ey + Hh * 0.8), (ex - W, ey, 'c')])
    eye = c.mask(np.vstack([upper, lower]))
    ir = 160
    iris = c.ellipse(ex, ey - 6, ir, ir * 0.92) * eye
    pupil = c.ellipse(ex, ey - 6, 22, ir * 0.78)
    # 眼白：淡淡的青色排线；虹膜：放射线 + 同心圈；瞳孔是一道竖缝，纯墨
    sclera = 0.55 * c.lines(ripple, 9, 3.4) * (1 - smoothstep(0.6, 1.0, np.hypot((c.xx - ex) / W, (c.yy - ey) / Hh)))
    c.paint(eye, sclera, 'abyss')
    rr = np.hypot(c.xx - ex, (c.yy - ey + 6) / 0.92)
    rad = c.lines(np.arctan2(c.yy - ey + 6, c.xx - ex) * 120 + 3 * c.noise(20, 10), 8, 4.2 * (0.5 + 0.5 * rr / ir))
    rings = c.lines(rr, 30, 3.0)
    glow = 0.35 * (1 - smoothstep(ir * 0.3, ir, rr))
    c.paint(iris, np.clip(np.maximum(np.maximum(rad, rings), glow), 0, 1), 'abyss')
    c.add(c.stroke(ellipse_pts(ex, ey - 6, ir, ir * 0.92), 9, closed=True, add=False) * eye, 'abyss')
    c.stroke(upper, 12, layer='abyss', taper=0.08)
    c.stroke(lower, 8, layer='abyss', taper=0.08)
    c.clear(pupil, layers=('abyss',))
    c.fill(pupil, value=1.0)
    # 眼睛在水下：水面的波纹从上面切过，把青色切碎一点
    c.erase(c.lines(ripple, 9, 1.4) * eye * 0.9, 'abyss')

    # ------------------------------------------------------------ 码头：左下角，木板 + 桩，近大远小
    dock_pts = [(-10, 560), (300, 600), (420, 864), (-10, 864)]
    dock = c.mask(poly(dock_pts))
    lam = c.grad(0, 560, 300, 864)
    c.engrave(dock, 0.35 + 0.35 * lam, [c.ph_lin(52, wobble=1.5, seed=11), c.ph_lin(-20, seed=12)], spacing=9)
    for k in range(1, 6):
        t = k / 6
        p0 = (-10 + 0 * t, 560 + 304 * t)
        p1 = (300 + 120 * t, 600 + 264 * t)
        c.stroke(poly([p0, p1]), 3.5, taper=0)
        for x in np.linspace(p0[0] + 40, p1[0] - 30, 3):                    # 钉子
            y = p0[1] + (p1[1] - p0[1]) * (x - p0[0]) / (p1[0] - p0[0])
            c.dots([(x, y - 9 - 6 * t)], 2.5 + 2 * t)
    c.stroke(poly([(-10, 560), (300, 600), (420, 864)]), OUTLINE + 2, taper=0)
    # 码头边缘的厚度 + 一根桩
    edge = c.mask(poly([(300, 600), (330, 610), (450, 864), (420, 864)]))
    c.fill(edge, value=0.85)
    pile = c.mask(poly([(270, 610), (330, 618), (360, 864), (290, 864)]))
    pile = pile * (1 - dock)
    c.engrave(pile, 0.7, [c.ph_lin(84, seed=13), c.ph_lin(0, seed=14)], spacing=8)
    # 栏杆上的一盏提灯（码头上的光源）
    c.stroke(poly([(60, 572), (54, 400)]), 10, taper=0)
    lx, ly = 70, 380
    c.stroke(spline([(lx - 20, ly - 30), (lx, ly - 46), (lx + 20, ly - 30)]), 3, taper=0)
    lamp = c.mask(poly([(lx - 22, ly - 26), (lx + 22, ly - 26), (lx + 26, ly + 30), (lx - 26, ly + 30)]))
    c.fill(lamp, value=0.0)
    c.stroke(poly([(lx - 22, ly - 26), (lx + 22, ly - 26), (lx + 26, ly + 30), (lx - 26, ly + 30), (lx - 22, ly - 26)]), 4, taper=0)
    c.stroke(poly([(lx, ly - 26), (lx, ly + 30)]), 2.4, taper=0)

    # ------------------------------------------------------------ 坠落的人：背影，头朝下栽向那只眼
    figure(c, Frame((560, 360), rot=24, scale=0.9))


def figure(c, F):
    """背对我们的调查员：张开双臂，风衣下摆向上翻，帽子飞在头上方。局部坐标：头在 -y。"""
    parts = []
    coat = [(-46, -60), (46, -60), (70, 40), (104, 120), (60, 104), (30, 130), (0, 104), (-36, 136), (-64, 104), (-104, 126), (-74, 36)]
    legs = [[(-30, 100), (-14, 100), (-10, 210), (-30, 214)], [(10, 100), (28, 100), (40, 204), (22, 210)]]
    arms = [[(-40, -50), (-130, -110), (-176, -126), (-182, -112), (-130, -88), (-46, -26)],
            [(40, -50), (128, -116), (172, -140), (180, -126), (130, -94), (46, -26)]]
    head = (0, -92, 30, 34)
    hat = Frame(F.p(-30, -200), rot=F.rot - 50)

    # 先在水上留出一圈白边，让黑衣服从黑水里分出来
    sil = np.zeros((c.h, c.w), np.float32)
    for poly_pts in [coat] + legs + arms:
        sil = np.maximum(sil, c.mask(spline(F(poly_pts), closed=True)))
    sil = np.maximum(sil, c.ellipse(*F.p(head[0], head[1]), head[2], head[3], rot=F.rot))
    halo = np.clip(c.shadow_of(sil, 0, 0, blur=9) * 2.2, 0, 1)
    c.clear(halo, layers=('ink', 'abyss'))

    for leg in legs:
        m = c.mask(spline(F(leg), closed=True))
        c.engrave(m, 0.8, [c.ph_lin(F.angle(80), seed=20), c.ph_lin(F.angle(-10), seed=21)], spacing=7)
        c.outline(F.cp(leg), w=STRUCT)
        c.fill(c.ellipse(*F.p(np.mean([p[0] for p in leg[2:]]), 214), 16, 9, rot=F.rot))  # 鞋
    m = c.mask(spline(F(coat), closed=True))
    lam = c.relief(m, radius=40, blur=3)
    c.engrave(m, np.clip(0.95 - 0.6 * lam, 0.3, 1), [c.ph_lin(F.angle(90), seed=22), c.ph_lin(F.angle(20), seed=23),
                                                    c.ph_lin(F.angle(-40), seed=24)], spacing=7.5, swell=1.0)
    for pts in ([(-20, -50), (-30, 40), (-50, 110)], [(18, -50), (30, 40), (46, 104)], [(0, -56), (0, 100)]):
        c.line(F.cp(pts), w=3, taper=0.3)
    c.outline(F.cp(coat), w=OUTLINE, light=0.4)
    for arm in arms:
        m = c.mask(spline(F(arm), closed=True))
        lam = c.relief(m, radius=16, blur=1.5)
        c.engrave(m, np.clip(0.95 - 0.6 * lam, 0.3, 1), [c.ph_lin(F.angle(30), seed=25), c.ph_lin(F.angle(-50), seed=26)], spacing=7)
        c.outline(F.cp(arm), w=STRUCT + 1, light=0.4)
    # 手：张开的五指，几笔
    for hx, hy, a in ((-182, -120, 200), (178, -134, -20)):
        p = F.p(hx, hy)
        for k in range(-2, 3):
            ang = math.radians(F.angle(a + k * 18))
            c.stroke(poly([p, (p[0] + 26 * math.cos(ang), p[1] + 26 * math.sin(ang))]), 4.5, taper=(0.0, 0.6))
    hx, hy = F.p(head[0], head[1])
    hm = c.ellipse(hx, hy, head[2], head[3], rot=F.rot)
    c.engrave(hm, 0.9, [c.ph_lin(F.angle(70), seed=27), c.ph_lin(F.angle(-30), seed=28)], spacing=6)
    c.stroke(ellipse_pts(hx, hy, head[2], head[3], rot=F.rot), STRUCT + 1, closed=True)
    # 帽子：在头上方翻着飞
    brim = c.ellipse(*hat.p(0, 0), 58, 18, rot=hat.rot)
    crown = c.mask(hat([(-30, 0), (-26, -48), (26, -48), (30, 0)]))
    c.engrave(np.clip(brim + crown, 0, 1), 0.85, [c.ph_lin(hat.angle(90)), c.ph_lin(hat.angle(10))], spacing=6.5)
    c.stroke(ellipse_pts(*hat.p(0, 0), 58, 18, rot=hat.rot), STRUCT, closed=True)
    c.stroke(hat([(-30, 0), (-26, -48), (26, -48), (30, 0)]), STRUCT, taper=0)
    # 帽子划过的几道弧
    for k in range(3):
        c.stroke(spline([hat.p(40 + k * 6, 30 + k * 14), hat.p(90, 70 + k * 10), hat.p(150, 80 + k * 8)]), 2.0, taper=(0.9, 0.1))
