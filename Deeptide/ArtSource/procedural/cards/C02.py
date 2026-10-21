"""C02 守夜灯：窗台上一盏提灯，窗外雾里的影子不敢靠近（策划/10 §4.6）。

从屋里看一扇十字窗；窗台上立着一盏防风提灯，灯光穿过玻璃在雾里推开一个亮圈。
亮圈外面站着几个又高又瘦的影子，停在光的边上。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, smoothstep, spline

NAME = '守夜灯'

WIN = (150, 36, 1100, 618)             # 窗洞
SILL_Y = 618
FLAME = (624, 440)


def draw(c):
    x0, y0, x1, y1 = WIN
    fx, fy = FLAME
    rng = np.random.default_rng(3)

    # ------------------------------------------------------------ 窗外：雾，灯光推开一个亮圈
    glass = c.rect(x0, y0, x1, y1)
    r = np.hypot((c.xx - fx) / 1.25, c.yy - fy)
    fog = 0.04 + 0.6 * smoothstep(200, 400, r + 25 * c.noise(60, 2)) + 0.06 * c.noise(90, 3)
    c.engrave(glass, fog, [c.ph_wave(-3, 6, 340, wobble=3, seed=4), c.ph_lin(60, seed=5), c.ph_lin(-30, seed=6)], spacing=10)
    # 雾里的影子：几个又高又瘦的人形，站在亮圈外
    for sx, base, h, lean, seed in ((215, 560, 340, -4, 1), (318, 580, 250, 3, 2), (952, 570, 320, 5, 3), (1050, 585, 250, -3, 4),
                                    (560, 330, 150, 0, 5)):
        shadow_figure(c, sx, base, h, lean, seed, glass)
    # 亮圈的边：放射的细光线
    for a in np.arange(0, 360, 7.5):
        ang = math.radians(a + rng.uniform(-2, 2))
        r0 = 90 + rng.uniform(0, 30)
        r1 = 250 + rng.uniform(0, 90)
        p0 = (fx + r0 * math.cos(ang) * 1.25, fy + r0 * math.sin(ang))
        p1 = (fx + r1 * math.cos(ang) * 1.25, fy + r1 * math.sin(ang))
        c.erase(c.stroke(poly([p0, p1]), 2.6, add=False, taper=(0.3, 0.9)) * glass)

    # ------------------------------------------------------------ 窗框：十字棂，木头
    wall = np.ones((c.h, c.w), np.float32) * (1 - glass)
    c.engrave(wall, 0.8 + 0.1 * c.noise(80, 7), [c.ph_lin(85, seed=8), c.ph_lin(10, seed=9), c.ph_lin(-40, seed=10)], spacing=9,
              swell=1.0)
    frame_w = 22
    bars = [c.rect(x0, y0, x1, y0 + frame_w), c.rect(x0, y1 - frame_w, x1, y1), c.rect(x0, y0, x0 + frame_w, y1),
            c.rect(x1 - frame_w, y0, x1, y1), c.rect(620 - 12, y0, 620 + 12, y1), c.rect(x0, 300 - 12, x1, 300 + 12)]
    for b in bars:
        c.engrave(b, 0.62, [c.ph_lin(0, wobble=0.6), c.ph_lin(90, wobble=0.6)], spacing=7)
    for xs in ((x0, x1), ):
        pass
    for pts in ([(x0, y0), (x1, y0), (x1, y1), (x0, y1), (x0, y0)],
                [(x0 + frame_w, y0 + frame_w), (x1 - frame_w, y0 + frame_w), (x1 - frame_w, y1 - frame_w),
                 (x0 + frame_w, y1 - frame_w), (x0 + frame_w, y0 + frame_w)]):
        c.stroke(poly(pts), STRUCT, taper=0)
    for a, b in (((608, y0 + frame_w), (608, y1 - frame_w)), ((632, y0 + frame_w), (632, y1 - frame_w)),
                 ((x0 + frame_w, 288), (x1 - frame_w, 288)), ((x0 + frame_w, 312), (x1 - frame_w, 312))):
        c.stroke(poly([a, b]), 3.5, taper=0)
    # 玻璃上的一点反光
    for gx, gy in ((300, 120), (760, 110), (290, 400)):
        c.erase(c.stroke(poly([(gx, gy), (gx + 60, gy - 30)]), 4, add=False, taper=0.5))
        c.erase(c.stroke(poly([(gx + 10, gy + 18), (gx + 50, gy - 2)]), 2.5, add=False, taper=0.5))

    # ------------------------------------------------------------ 窗台
    sill = c.mask(poly([(x0 - 40, SILL_Y), (x1 + 40, SILL_Y), (x1 + 70, SILL_Y + 60), (x0 - 70, SILL_Y + 60)]))
    c.engrave(sill, 0.18 + 0.4 * c.radial(fx, SILL_Y + 30, 60, 600), [c.ph_lin(0, wobble=1.2, seed=11), c.ph_lin(70)], spacing=9)
    c.stroke(poly([(x0 - 40, SILL_Y), (x1 + 40, SILL_Y), (x1 + 70, SILL_Y + 60), (x0 - 70, SILL_Y + 60), (x0 - 40, SILL_Y)]),
             STRUCT, taper=0)
    front = c.mask(poly([(x0 - 70, SILL_Y + 60), (x1 + 70, SILL_Y + 60), (x1 + 70, SILL_Y + 92), (x0 - 70, SILL_Y + 92)]))
    c.engrave(front, 0.8, [c.ph_lin(0), c.ph_lin(60)], spacing=7)
    c.stroke(poly([(x0 - 70, SILL_Y + 92), (x1 + 70, SILL_Y + 92)]), STRUCT, taper=0)
    # 灯在窗台上的影子（灯在前面，影子落在后面玻璃下沿看不到；只在台面上一圈暗）
    c.engrave(c.ellipse(fx + 20, SILL_Y + 34, 130, 22), 0.6, [c.ph_lin(0, wobble=0.4)], spacing=6)

    lantern(c, Frame((fx, SILL_Y + 34), scale=1.22))


def shadow_figure(c, x, base, h, lean, seed, clip):
    """雾里一个瘦高的影子：头、窄肩、垂到膝的长臂。越远越淡。"""
    rng = np.random.default_rng(seed)
    F = Frame((x, base), rot=lean)
    w = h * 0.16
    body = [(-w * 0.5, 0), (-w * 0.62, -h * 0.5), (-w * 0.9, -h * 0.74), (-w * 0.5, -h * 0.82), (-w * 0.22, -h * 0.86),
            (-w * 0.3, -h * 0.96), (0, -h * 1.0), (w * 0.3, -h * 0.96), (w * 0.22, -h * 0.86), (w * 0.5, -h * 0.82),
            (w * 0.9, -h * 0.74), (w * 0.62, -h * 0.5), (w * 0.5, 0)]
    m = c.mask(spline(F(body), closed=True)) * clip
    arms = np.zeros_like(m)
    for sd in (-1, 1):
        arm = spline(F([(sd * w * 0.85, -h * 0.74), (sd * w * 1.15, -h * 0.5), (sd * w * (1.1 + rng.uniform(0, 0.3)), -h * 0.28),
                        (sd * w * 1.3, -h * 0.2)]))
        arms = np.maximum(arms, c.stroke(arm, w * 0.28, add=False, taper=(0.0, 0.6)))
    m = np.clip(m + arms * clip, 0, 1)
    m = np.clip(c.shadow_of(m, 0, 0, blur=3) * 1.4, 0, 1) * clip           # 雾里的影子，边缘是虚的
    fade = smoothstep(base + 10, base - h * 0.45, c.yy)                     # 脚下沉进雾里
    tone = (0.74 + 0.1 * rng.random()) * fade
    c.engrave(m, tone, [c.ph_wave(-3, 6, 340, wobble=3, seed=4), c.ph_lin(90, wobble=2, seed=seed)], spacing=10, swell=1.0,
              solid=None, grain=0.1)


def lantern(c, F):
    """防风提灯：油壶、玻璃罩、护丝、顶盖、提环。F 的原点在灯底，向上为 -y。"""
    fx, fy = F.p(0, 0)
    # 油壶
    tank = [(-92, 0), (-100, -30), (-80, -60), (80, -60), (100, -30), (92, 0)]
    m = c.mask(spline(F(tank), closed=True))
    lam = c.relief(m, radius=30, blur=2)
    c.engrave(m, np.clip(0.75 - 0.6 * lam + 0.25 * c.grad(fx - 100, 0, fx + 100, 0), 0.05, 1),
              [c.ph_axis(F.p(-100, -30), F.p(100, -30), bow=8, half=30), c.ph_lin(90)], spacing=7)
    c.outline(F.cp(tank), w=STRUCT + 1, light=0.4)
    c.line(F.cp([(-78, -58), (0, -54), (78, -58)]), w=3, taper=0.1)
    # 玻璃罩：留白，里面是火焰
    globe = [(-56, -60), (-82, -130), (-74, -200), (-40, -238), (40, -238), (74, -200), (82, -130), (56, -60)]
    gm = c.mask(spline(F(globe), closed=True))
    c.clear(gm)
    c.engrave(gm, 0.18 * c.grad(fx - 80, 0, fx + 80, 0), [c.ph_axis(F.p(0, -60), F.p(0, -238), bow=-10, half=80)], spacing=8, wmax=2)
    c.outline(F.cp(globe), w=STRUCT, light=0.4)
    flame = [(0, -200), (16, -160), (18, -128), (8, -104), (-8, -104), (-18, -128), (-16, -160)]
    c.clear(c.mask(spline(F(flame), closed=True)))
    c.outline(F.cp(flame), w=3, light=0)
    c.line(F.cp([(0, -104), (0, -92)]), w=4, taper=0)
    for a in np.arange(0, 360, 20):                                       # 火焰周围的光芒（玻璃里）
        ang = math.radians(a)
        p0 = F.p(36 * math.cos(ang), -150 + 52 * math.sin(ang))
        p1 = F.p(58 * math.cos(ang), -150 + 76 * math.sin(ang))
        c.stroke(poly([p0, p1]), 1.6, taper=(0.1, 0.9))
    # 护丝：几道竖弧
    for x in (-60, -20, 20, 60):
        c.line(F.cp([(x * 1.0, -62), (x * 1.42, -140), (x * 1.0, -236)]), w=4, taper=0)
    c.line(F.cp([(-84, -140), (0, -146), (84, -140)]), w=3.5, taper=0)
    # 顶盖 + 提环
    cap = [(-56, -236), (-48, -262), (-20, -280), (20, -280), (48, -262), (56, -236)]
    m = c.mask(spline(F(cap), closed=True))
    c.engrave(m, 0.7, [c.ph_lin(90), c.ph_lin(20)], spacing=6.5)
    c.outline(F.cp(cap), w=STRUCT, light=0.4)
    c.line(F.cp([(-60, -230), (-86, -300), (-40, -350), (40, -350), (86, -300), (60, -230)]), w=5, taper=0)
