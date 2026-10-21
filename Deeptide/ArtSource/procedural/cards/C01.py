"""C01 祷告：烛光下握着念珠的双手（策划/10 §4.6）。

正面看合十的双手（两手对称，拇指并在中间，指尖在上面相碰）；念珠绕在拇指根上，十字架垂在下面。
左边一支蜡烛，光从左上来（10 §3），手的左侧亮、右侧沉进黑暗。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, resample, smoothstep, spline

NAME = '祷告'

FLAME = (300, 250)


def draw(c):
    H = Frame((770, 800), rot=0)            # 手的坐标：x 向右，y 向下；原点在两手腕中间
    fx, fy = FLAME

    # ------------------------------------------------------------ 背景：黑暗，蜡烛周围一圈光
    bg = np.ones((c.h, c.w), np.float32)
    dark = 0.5 + 0.45 * smoothstep(0, 1, c.radial(fx, fy, 60, 700, sx=1.15)) + 0.05 * c.noise(120, 1)
    c.engrave(bg, np.clip(dark, 0, 0.9), [c.ph_ray(fx, fy, r0=420) + 1.5 * c.noise(50, 2), c.ph_ring(fx, fy) + 2 * c.noise(60, 3),
                         c.ph_lin(30, seed=4)], spacing=10, swell=1.0, solid=None, grain=0.03)
    c.erase(1 - smoothstep(60, 170, np.hypot((c.xx - fx) / 1.1, c.yy - fy)))

    # ------------------------------------------------------------ 蜡烛
    cx = fx
    body = c.rect(cx - 46, fy + 92, cx + 46, 900)
    lam = c.relief(body, radius=40, blur=3)
    c.engrave(body, np.clip(0.5 - 0.55 * lam + 0.4 * c.grad(0, fy + 100, 0, 880), 0.02, 1),
              [c.ph_lin(90, wobble=0.6, seed=5), c.ph_axis((cx, fy + 92), (cx, 900), bow=-8, half=46, seed=6)], spacing=8)
    # 蜡烛顶上淌下来的蜡
    top = [(cx - 46, fy + 96), (cx - 30, fy + 86), (cx, fy + 84), (cx + 30, fy + 86), (cx + 46, fy + 96)]
    drips = [(cx - 46, fy + 96), (cx - 48, fy + 150), (cx - 40, fy + 162), (cx - 34, fy + 110), (cx - 10, fy + 104),
             (cx + 4, fy + 128), (cx + 12, fy + 104), (cx + 30, fy + 100), (cx + 40, fy + 210), (cx + 50, fy + 214), (cx + 48, fy + 98)]
    c.fill(c.mask(spline(drips + [(cx, fy + 84)], closed=True)), value=0.0)
    c.line(drips, w=STRUCT, taper=0.05)
    c.line(top, w=3, taper=0.2)
    c.line([(cx - 46, fy + 96), (cx - 46, 900)], w=OUTLINE * 0.7, taper=0)
    c.line([(cx + 46, fy + 96), (cx + 46, 900)], w=OUTLINE, taper=0)
    # 烛芯和火焰：火焰本身留白，只勾外形和里面一点焰心
    c.line([(cx, fy + 86), (cx + 2, fy + 62)], w=5, taper=0.1)
    flame = [(cx, fy - 120), (cx + 26, fy - 40), (cx + 30, fy + 14), (cx + 14, fy + 58), (cx - 14, fy + 58), (cx - 30, fy + 14),
             (cx - 24, fy - 40)]
    c.clear(c.mask(spline(flame, closed=True)))
    c.outline(flame, w=3.2, light=0)
    inner = [(cx, fy - 30), (cx + 11, fy + 10), (cx + 6, fy + 44), (cx - 6, fy + 44), (cx - 11, fy + 10)]
    c.engrave(c.mask(spline(inner, closed=True)), 0.4, [c.ph_lin(90, wobble=0.4)], spacing=6, wmax=2)
    c.outline(inner, w=2.2, light=0)

    # ------------------------------------------------------------ 衣袖：两只袖口从下面伸上来，向两边张开
    for sd in (-1, 1):
        sl = [(sd * 40, 140), (sd * 30, 40), (sd * 90, -20), (sd * 200, 10), (sd * 300, 140)]
        m = c.mask(spline(H(sl), closed=True))
        lam = c.relief(m, radius=60, blur=4)
        c.engrave(m, np.clip(0.95 - 0.45 * lam, 0.5, 1), [c.ph_lin(60 * sd, seed=7), c.ph_lin(-30 * sd, seed=8),
                                                         c.ph_lin(10, seed=9)], spacing=9, solid=None)
        c.outline(H.cp(sl if sd > 0 else sl[::-1]), w=OUTLINE)
        cuff = [(sd * 18, 40), (sd * 14, -26), (sd * 80, -52), (sd * 150, -36), (sd * 170, 20), (sd * 90, 40)]
        m = c.mask(spline(H(cuff), closed=True))
        c.engrave(m, (0.12 if sd < 0 else 0.45) + 0.3 * c.grad(*H.p(-170, 0), *H.p(170, 0)), [c.ph_lin(80 * sd, seed=10)], spacing=8)
        c.outline(H.cp(cuff if sd > 0 else cuff[::-1]), w=STRUCT)

    # ------------------------------------------------------------ 双手：左右对称；左手（画面左）迎着烛光，右手沉在暗里
    side = c.grad(*H.p(-170, 0), *H.p(170, 0))                         # 0 = 迎着烛光的一侧
    for sd in (-1, 1):
        X = (lambda pts, sd=sd: [(sd * p[0], p[1]) + tuple(p[2:]) for p in pts])
        shade = 0.0 if sd < 0 else 0.26
        # 手背（只看到靠外的一条）
        back = [(-22, 10), (-120, 6), (-150, -110), (-160, -230), (-128, -268), (-40, -282), (-8, -200), (-10, -60)]
        m = c.mask(spline(H(X(back)), closed=True))
        lam = c.relief(m, radius=60, blur=4)
        c.engrave(m, np.clip(-0.06 + shade + 0.25 * side + 0.32 * (1 - lam) ** 1.3, 0.0, 1),
                  [c.ph_axis(H.p(sd * -80, 0), H.p(sd * -80, -280), bow=-16, half=80, seed=11), c.ph_lin(sd * 75, seed=12),
                   c.ph_lin(sd * 20, seed=13)], spacing=8.5, swell=1)
        c.outline(H.cp(X(back) if sd > 0 else X(back)[::-1]), w=OUTLINE, light=0.45)
        for k in (0.35, 0.65):                                          # 手背上的筋
            c.line(H.cp(X([(-30 - 60 * k, -40), (-40 - 90 * k, -150), (-34 - 100 * k, -255)])), w=2.2, taper=0.45)
        # 四根手指：小指在最外、最矮，食指贴着中线
        fingers = [((-128, -226), (-110, -420), 46), ((-98, -250), (-74, -492), 50), ((-64, -262), (-34, -548), 54),
                   ((-30, -262), (-10, -508), 52)]
        for i, ((bx, by), (tx, ty), w) in enumerate(fingers):
            ang = math.degrees(math.atan2(ty - by, tx - bx)) + 90
            ln = math.hypot(tx - bx, ty - by)
            F = Frame(H.p(sd * bx, by), rot=sd * ang)
            hw = w / 2
            pts = [(-hw, 16), (-hw - 1, -ln * 0.5), (-hw + 4, -ln * 0.86), (-hw * 0.4, -ln - 2), (hw * 0.4, -ln - 2),
                   (hw - 4, -ln * 0.86), (hw + 1, -ln * 0.5), (hw, 16)]
            m = c.mask(spline(F(pts), closed=True))
            lam = c.relief(m, radius=hw * 0.9, blur=1.5)
            c.engrave(m, np.clip(-0.12 + shade + 0.24 * side + 0.42 * (1 - lam) ** 1.3, 0.0, 1),
                      [c.ph_axis(F.p(0, 16), F.p(0, -ln), bow=-8, half=hw, seed=14 + i), c.ph_lin(F.angle(90), seed=18 + i)],
                      spacing=7.5, wmax=2.8, swell=0.9)
            c.outline(F.cp(pts), w=STRUCT + 0.5, light=0.5)
            for jy, k in ((-ln * 0.46, 0.7), (-ln * 0.77, 0.55)):         # 指节的褶
                c.line(F.cp([(-hw * k, jy + 3), (0, jy - 2), (hw * k, jy + 2)]), w=2.2, taper=0.3)
            if i < 3:                                                    # 外侧三根看得到指甲
                nail = [(-hw * 0.5, -ln * 0.84), (-hw * 0.45, -ln + 8), (0, -ln + 2), (hw * 0.45, -ln + 8), (hw * 0.5, -ln * 0.84)]
                nm = c.mask(spline(F(nail), closed=True))
                c.engrave(nm, 0.15 + shade + 0.3 * side, [c.ph_lin(F.angle(90), wobble=0.3)], spacing=6, wmax=2)
                c.outline(F.cp(nail), w=2.0, light=0.3)
        # 拇指：贴着中线向上
        thumb = [(-8, -20), (-62, -60), (-74, -150), (-56, -250), (-30, -300), (-6, -296), (-4, -200)]
        m = c.mask(spline(H(X(thumb)), closed=True))
        lam = c.relief(m, radius=26, blur=2)
        c.engrave(m, np.clip(-0.12 + shade + 0.24 * side + 0.42 * (1 - lam) ** 1.3, 0.0, 1),
                  [c.ph_axis(H.p(sd * -40, -20), H.p(sd * -20, -300), bow=-8, half=30, seed=24), c.ph_lin(sd * 80, seed=25)],
                  spacing=7.5)
        c.outline(H.cp(X(thumb) if sd > 0 else X(thumb)[::-1]), w=STRUCT + 1, light=0.45)
        tnail = [(-12, -250), (-40, -262), (-34, -288), (-14, -292)]
        c.outline(H.cp(X(tnail) if sd > 0 else X(tnail)[::-1]), w=2.2, light=0.3)
        c.line(H.cp(X([(-50, -150), (-34, -160), (-16, -156)])), w=2.2, taper=0.3)
    c.line(H.cp([(0, -300), (0, -440), (0, -510)]), w=3.4, taper=0.3)      # 两手贴合的缝

    # ------------------------------------------------------------ 念珠：绕过手背，垂成一个圈，下面挂十字架
    loop = spline(H([(-120, -90), (-60, -130), (0, -140), (70, -128), (120, -90)]))
    hang = spline(H([(-120, -90), (-110, -10), (-60, 60), (0, 80), (60, 60), (110, -10), (120, -90)]))
    for path, front in ((loop, True), (hang, True)):
        pts = resample(path, 25)
        c.stroke(path, 2.0, taper=0)
        for i, (x, y) in enumerate(pts[1:-1]):
            r = 10.5
            b = c.ellipse(x, y, r, r)
            c.fill(b, value=0.0)
            c.engrave(b * (1 - smoothstep(-0.2, 0.6, ((c.xx - x) * -0.7 + (c.yy - y) * -0.7) / r)), 0.55,
                      [c.ph_lin(-45, wobble=0)], spacing=4.5, wmax=1.8, grain=0)
            c.stroke(ellipse_pts(x, y, r, r), 2.6, closed=True, light=0.5)
    # 十字架
    X = Frame(H.p(0, 86), rot=0)
    cross = [(-9, 0, 'c'), (9, 0, 'c'), (9, 30, 'c'), (34, 30, 'c'), (34, 48, 'c'), (9, 48, 'c'), (9, 112, 'c'), (-9, 112, 'c'),
             (-9, 48, 'c'), (-34, 48, 'c'), (-34, 30, 'c'), (-9, 30, 'c')]
    m = c.mask(X(cross))
    c.engrave(m, 0.3 + 0.5 * c.grad(*X.p(-34, 0), *X.p(34, 0)), [c.ph_lin(X.angle(90), wobble=0)], spacing=6, wmax=2.2)
    c.stroke(np.vstack([X(cross), X(cross[:1])]), STRUCT, taper=0)
