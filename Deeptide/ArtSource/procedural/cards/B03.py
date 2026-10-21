"""B03 禁忌一瞥：半开的书页，缝隙里露出一行字。癫狂层：字变成一排睁开的眼（策划/10 §4.6）。

一本厚皮面书平放在桌上，从前上方看；封面在靠近观者的一边被掀起一条缝，缝里是第一页，只看得到一行字。
缝里的纸是亮的（灯光斜着照进去），这样癫狂面里那排眼睛落在暗底上才看得清。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, lerp, poly, resample, smoothstep, spline

NAME = '禁忌一瞥'

# 书的几个关键点（画布坐标）
PAGE_NEAR_Y, PAGE_FRONT_Y = 652, 716        # 书页近边、书口（前切口）下沿
COVER_NEAR_Y = 548                           # 掀起的封面近边
HINGE_Y = 196                                # 书脊（远边）
NL, NR = 150, 1098                           # 近边左右
FL, FR = 268, 980                            # 远边左右


def x_at(y, left=True, near_y=PAGE_NEAR_Y):
    """透视：书的左右边随深度向内收。"""
    t = (near_y - y) / (near_y - HINGE_Y)
    return lerp(NL if left else NR, FL if left else FR, t)


def draw(c):
    # ------------------------------------------------------------ 背景：桌面 + 后面的黑暗
    bg = np.ones((c.h, c.w), np.float32)
    c.engrave(bg, 0.12 + 0.5 * c.radial(560, 470, 380, 900) ** 1.5 + 0.05 * c.noise(120, 1),
              [c.ph_wave(2, 5, 420, wobble=2, seed=2), c.ph_lin(70, seed=3), c.ph_lin(-35, seed=4)], spacing=11,
              bands=[(0.03, 0.5), (0.45, 0.8), (0.75, 0.95)])

    # 书的投影（光从左上，影子落在右下）：书底在桌面上的轮廓平移
    foot = c.mask(poly([(NL - 14, PAGE_FRONT_Y + 22), (NR + 14, PAGE_FRONT_Y + 22), (FR + 14, HINGE_Y + 96), (FL - 14, HINGE_Y + 96)]))
    sh = np.clip(c.shadow_of(foot, 54, 26, blur=3) - foot, 0, 1)
    c.engrave(sh, 0.72, [c.ph_lin(-20, seed=5), c.ph_lin(40, seed=6)], spacing=9, swell=1.0, solid=None)

    # ------------------------------------------------------------ 底封 + 书口（一层层纸边）
    back = c.mask(poly([(NL - 12, PAGE_FRONT_Y - 6), (NR + 12, PAGE_FRONT_Y - 6), (NR + 14, PAGE_FRONT_Y + 22),
                        (NL - 14, PAGE_FRONT_Y + 22)]))
    c.engrave(back, 0.8, [c.ph_lin(0, seed=7), c.ph_lin(60, seed=8)], spacing=8)
    c.stroke(poly([(NL - 14, PAGE_FRONT_Y + 22), (NR + 14, PAGE_FRONT_Y + 22)]), OUTLINE, taper=0)
    front = c.mask(poly([(NL, PAGE_NEAR_Y), (NR, PAGE_NEAR_Y), (NR, PAGE_FRONT_Y), (NL, PAGE_FRONT_Y)]))
    c.fill(front, value=0.0)
    edges = c.lines(c.yy + 0.8 * c.noise(40, 9) + 0.6 * np.sin(c.xx / 37), 4.2, 1.1)
    c.paint(front, np.clip(edges * (0.45 + 0.4 * c.grad(NL, 0, NR, 0)), 0, 1))
    # 右侧的书口也看得到一点（透视）
    side = c.mask(poly([(NR, PAGE_NEAR_Y), (x_at(HINGE_Y + 40, False), HINGE_Y + 40), (x_at(HINGE_Y + 40, False) + 2, HINGE_Y + 80),
                        (NR, PAGE_FRONT_Y)]))
    c.engrave(side, 0.6, [c.ph_along((NR, PAGE_FRONT_Y), (x_at(HINGE_Y, False), HINGE_Y + 60), wobble=0.3)], spacing=4.5, wmax=1.4,
              solid=None)

    # ------------------------------------------------------------ 缝里的第一页：近处亮，往里暗
    slit = c.mask(poly([(NL + 4, COVER_NEAR_Y + 20), (NR - 4, COVER_NEAR_Y + 20), (NR, PAGE_NEAR_Y), (NL, PAGE_NEAR_Y)]))
    depth = c.grad(0, PAGE_NEAR_Y, 0, COVER_NEAR_Y + 26)                   # 0 = 页边，1 = 缝的最里面
    across = np.abs(c.xx - 640) / 520
    c.engrave(slit, np.clip(0.04 + 0.95 * depth ** 2.2 + 0.35 * across ** 3, 0, 1),
              [c.ph_lin(0, wobble=0.6, seed=10), c.ph_lin(-62, seed=11), c.ph_lin(30, seed=12)], spacing=7, wmax=2.4)
    # 右侧看得到缝的侧面：一道从书脊收到近边的黑楔子
    wedge = c.mask(poly([(FR + 10, HINGE_Y + 6), (NR + 16, COVER_NEAR_Y + 26), (NR + 2, PAGE_NEAR_Y), (FR - 2, HINGE_Y + 26)]))
    c.engrave(wedge, 0.92, [c.ph_lin(60), c.ph_lin(-20)], spacing=7, swell=1.2)
    c.stroke(poly([(FR - 2, HINGE_Y + 26), (NR + 2, PAGE_NEAR_Y)]), 3.0, taper=0)
    # 那一行字：手写的花体，和页边平行
    line_y = PAGE_NEAR_Y - 30
    words = text_line(c, 300, 1000, line_y, 17)
    c.stroke(poly([(NL + 8, PAGE_NEAR_Y), (NR - 8, PAGE_NEAR_Y)]), 3.0, taper=0)

    # ------------------------------------------------------------ 封面：掀起一点，近边的厚度 + 封面
    cover_pts = [(FL - 14, HINGE_Y), (FR + 14, HINGE_Y), (NR + 18, COVER_NEAR_Y), (NL - 18, COVER_NEAR_Y)]
    thick_pts = [(NL - 18, COVER_NEAR_Y), (NR + 18, COVER_NEAR_Y), (NR + 18, COVER_NEAR_Y + 24), (NL - 18, COVER_NEAR_Y + 26)]
    cover = c.mask(poly(cover_pts))
    lit = c.grad(150, 150, 1100, 600)
    c.engrave(cover, np.clip(0.2 + 0.3 * lit + 0.06 * c.noise(30, 13), 0, 1),
              [c.ph_lin(-4, wobble=1.0, seed=14), c.ph_lin(56, seed=15), c.ph_lin(-40, seed=16)], spacing=8.5, swell=0.8)
    thick = c.mask(poly(thick_pts))
    c.engrave(thick, 0.75, [c.ph_lin(0, wobble=0.4), c.ph_lin(80)], spacing=6, wmax=2.4)
    c.stroke(poly(thick_pts + thick_pts[:1]), STRUCT, taper=0)
    c.stroke(poly(cover_pts + cover_pts[:1]), OUTLINE, taper=0, light=0.4)

    # 封面装饰：内框双线、四角包铜、中间一枚蚀刻纹章
    def cov(u, v):
        """封面上的点：u 0–1 左→右，v 0–1 远→近。"""
        y = lerp(HINGE_Y, COVER_NEAR_Y, v)
        xl = lerp(FL - 14, NL - 18, v)
        xr = lerp(FR + 14, NR + 18, v)
        return (lerp(xl, xr, u), y)

    for inset, w in ((0.05, 3.2), (0.075, 2.0)):
        q = [cov(inset, inset * 1.6), cov(1 - inset, inset * 1.6), cov(1 - inset, 1 - inset * 1.6), cov(inset, 1 - inset * 1.6)]
        c.stroke(poly(q + q[:1]), w, taper=0)
    for (u0, v0, du, dv) in ((0, 0, 1, 1), (1, 0, -1, 1), (0, 1, 1, -1), (1, 1, -1, -1)):
        tri = [cov(u0, v0), cov(u0 + du * 0.1, v0), cov(u0, v0 + dv * 0.16)]
        m = c.mask(poly(tri))
        c.engrave(m, 0.25, [c.ph_lin(30, wobble=0.3)], spacing=6, wmax=2)
        c.stroke(poly(tri + tri[:1]), 3.2, taper=0)
        c.dots([lerp(lerp(np.array(tri[0]), np.array(tri[1]), 0.5), np.array(tri[2]), 0.35)], 5)
    # 纹章：透视下的椭圆，里面一只闭着的眼（清醒时不睁开）
    ex, ey = cov(0.5, 0.47)
    for rx, ry, w in ((150, 94, 4.5), (128, 80, 2.4)):
        c.stroke(ellipse_pts(ex, ey, rx, ry), w, closed=True)
    c.erase(c.ellipse(ex, ey, 126, 78) * 0.85)
    c.engrave(c.ellipse(ex, ey, 126, 78), 0.3, [c.ph_ring(ex, ey, sy=0.63)], spacing=8, wmax=2.2)
    lid = spline([(ex - 74, ey), (ex - 30, ey + 18), (ex + 30, ey + 18), (ex + 74, ey)])
    c.stroke(lid, 5.0, taper=0.25)
    c.stroke(spline([(ex - 74, ey), (ex, ey - 22), (ex + 74, ey)]), 2.6, taper=0.3)
    for k in range(-3, 4):
        x = ex + k * 19
        y0 = ey + 18 - 0.0045 * (x - ex) ** 2
        c.stroke(poly([(x, y0 + 2), (x + k * 3, y0 + 22)]), 2.6, taper=(0.0, 0.7))
    for a in range(0, 360, 30):                                            # 一圈放射的刻线
        r0, r1 = 160, 196
        p0 = (ex + r0 * math.cos(math.radians(a)), ey + r0 * 0.63 * math.sin(math.radians(a)))
        p1 = (ex + r1 * math.cos(math.radians(a)), ey + r1 * 0.63 * math.sin(math.radians(a)))
        c.stroke(poly([p0, p1]), 3.0, taper=(0.0, 0.8))

    # 搭扣：一条皮带从封面上垂下来，搭在书口前面
    strap = [(NR - 170, cov(0, 0.8)[1] + 40), (NR - 120, cov(0, 0.8)[1] + 40), (NR - 116, PAGE_FRONT_Y + 40),
             (NR - 168, PAGE_FRONT_Y + 40)]
    m = c.mask(poly(strap))
    c.engrave(m, 0.6 + 0.2 * c.grad(NR - 170, 0, NR - 116, 0), [c.ph_lin(90, wobble=0.4), c.ph_lin(20)], spacing=7)
    c.stroke(poly(strap + strap[:1]), STRUCT, taper=0)
    bx, by = NR - 143, PAGE_FRONT_Y + 6
    buckle = c.rect(bx - 34, by - 26, bx + 34, by + 26)
    c.fill(buckle, value=0.0)
    c.engrave(buckle, 0.3 + 0.4 * c.grad(bx - 34, 0, bx + 34, 0), [c.ph_lin(-45, wobble=0.3)], spacing=6, wmax=2)
    c.stroke(poly([(bx - 34, by - 26), (bx + 34, by - 26), (bx + 34, by + 26), (bx - 34, by + 26), (bx - 34, by - 26)]), 4.2, taper=0)
    c.stroke(poly([(bx, by - 20), (bx, by + 20)]), 4, taper=0)

    # ============================================================ 癫狂层：字变成一排睁开的眼
    mad(c, words, line_y)


def text_line(c, x0, x1, y, h):
    """一行潦草的手写花体（不是任何真实文字）：用带圈的摆线写，偶尔一个高笔画。返回每个词的 (x 中心, 宽)。"""
    rng = np.random.default_rng(31)
    x = x0
    words = []
    while x < x1 - 40:
        n = int(rng.integers(4, 9))                                       # 一个词几个字母
        pts = []
        px = x
        for i in range(n):
            tall = rng.random() < 0.22
            loop = rng.random() < 0.55
            hh = h * (1.9 if tall else 1.0) * rng.uniform(0.75, 1.05)
            step = rng.uniform(11, 15)
            t = np.linspace(0, 2 * math.pi, 18)
            r = step * (0.62 if loop else 0.3)
            xs = px + step * t / (2 * math.pi) - r * np.sin(t) * 0.55
            ys = y - hh * (1 - np.cos(t)) / 2
            pts.extend(zip(xs, ys))
            px += step
        pts = np.array(pts)
        pts[:, 1] += np.linspace(0, rng.uniform(-3, 3), len(pts))
        c.stroke(pts, 2.6, taper=0.12, step=1.2)
        wl = px - x
        if rng.random() < 0.35:                                           # t 的横杠
            tx = x + wl * rng.uniform(0.3, 0.7)
            c.stroke(poly([(tx - 10, y - h * 1.3), (tx + 12, y - h * 1.4)]), 2.0, taper=0.3)
        words.append((x + wl / 2, wl))
        x += wl + rng.uniform(16, 26)
    return words


def mad(c, words, line_y):
    rng = np.random.default_rng(5)
    for i, (x, wl) in enumerate(words):
        w = max(min(wl * 0.62, 66), 40)
        h = w * 0.5
        y = line_y - 8 + rng.uniform(-4, 4)
        look = rng.uniform(-0.35, 0.35)
        upper = spline([(x - w, y, 'c'), (x - w * 0.4, y - h * 0.95), (x + w * 0.4, y - h * 0.95), (x + w, y, 'c')])
        lower = spline([(x + w, y, 'c'), (x + w * 0.4, y + h * 0.7), (x - w * 0.4, y + h * 0.7), (x - w, y, 'c')])
        eye = np.vstack([upper, lower])
        m = c.mask(eye)
        ir = h * 0.62
        cx = x + look * w * 0.5
        iris = c.ellipse(cx, y - 2, ir, ir) * m
        rings = c.lines(np.hypot(c.xx - cx, c.yy - y + 2) + 0.0 * c.xx, 4.5, 1.6)
        rays = c.lines(np.arctan2(c.yy - y + 2, c.xx - cx) * 16, 5, 1.6)
        c.add(np.maximum(rings, rays) * iris * (np.hypot(c.xx - cx, c.yy - y + 2) > ir * 0.45), 'mad')
        c.add(c.ellipse(cx, y - 2, ir * 0.45, ir * 0.5) * m, 'mad')
        c.add(c.stroke(ellipse_pts(cx, y - 2, ir, ir), 2.4, closed=True, add=False) * m, 'mad')
        c.stroke(upper, 5.5, layer='mad', taper=0.1)
        c.stroke(lower, 3.2, layer='mad', taper=0.1)
        # 上睫毛
        for t in np.linspace(0.15, 0.85, 6):
            k = int(t * (len(upper) - 1))
            p = upper[k]
            ang = math.atan2(p[1] - (y + h), p[0] - x)
            c.stroke(poly([p, (p[0] + 14 * math.cos(ang), p[1] + 14 * math.sin(ang))]), 2.4, layer='mad', taper=(0.0, 0.8))
    # 缝的深处还有几只更小的，半睁着
    for x in (380, 560, 760, 930):
        y = COVER_NEAR_Y + 34
        c.stroke(spline([(x - 26, y), (x, y - 9), (x + 26, y)]), 3, layer='mad', taper=0.2)
        c.stroke(spline([(x - 26, y), (x, y + 6), (x + 26, y)]), 2.2, layer='mad', taper=0.2)
        c.add(c.ellipse(x, y - 1, 6, 5), 'mad')
