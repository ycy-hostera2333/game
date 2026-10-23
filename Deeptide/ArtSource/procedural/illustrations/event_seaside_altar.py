"""事件 3 海边祭坛：退潮后露出一座石坛，坛上的凹槽还是湿的（Data/events.csv）。

退潮的滩涂，天刚蒙蒙亮：远处一线退远的海，近处是有波纹的湿沙和几块映着天光的水洼，散着石头和海草。
画面中间一座方石坛（3/4 视角，顶面 + 正面 + 侧面），顶上刻着螺旋形的槽，槽里的水着深渊青，顺着正面的沟淌下来。
癫狂层：远处的浅水里站着一排戴兜帽的高个子，石坛正面睁开一只刻出来的眼。
"""
import math

import numpy as np

from etch import Frame, ellipse_pts, poly, resample, smoothstep, spline
from cards._common import eye
from illustrations._kit import GAP, OUT, STRUCT, hatch, hooded, night_sky, rock

NAME = '海边祭坛'
SIZE = (1920, 1280)
HORIZON = 470
VP = (980, HORIZON)


def draw(c):
    rng = np.random.default_rng(3)
    W, H = c.w, c.h

    # ------------------------------------------------------------ 天：黎明前，地平线最亮，几条低云
    night_sky(c, HORIZON, top=0.62, bottom=0.12, seed=5)
    for k, (y, x0, x1) in enumerate(((150, 200, 900), (230, 1100, 1800), (330, 500, 1300))):
        cl = c.smask([(x0, y), ((x0 + x1) / 2, y - 26), (x1, y + 4), ((x0 + x1) / 2, y + 22)])
        hatch(c, cl, 0.55, [c.ph_wave(-2, 4, 200, seed=6 + k), c.ph_lin(25, seed=9 + k)], spacing=8)
        c.line([(x0, y), ((x0 + x1) / 2, y - 26), (x1, y + 4)], w=2.4, taper=0.3)
    # 退远的海：地平线上一窄条
    far = c.rect(0, HORIZON - 4, W, HORIZON + 26)
    hatch(c, far, 0.6, [c.ph_lin(0, wobble=0.8, seed=12)], spacing=7)
    c.stroke([(0, HORIZON - 4), (W, HORIZON - 4)], 2.6, taper=0)

    # ------------------------------------------------------------ 滩涂：透视的沙纹，越近越疏越粗
    flat = c.rect(0, HORIZON + 26, W, H)
    d0 = 60.0
    ph = 7.5 * d0 / 7 * np.log(np.maximum(c.yy - HORIZON + d0, 1) / d0) + 1.4 * np.sin(c.xx / 70 + 2 * c.noise(200, 13))
    near = c.grad(0, HORIZON, 0, H)
    hatch(c, flat, np.clip(0.12 + 0.32 * near + 0.22 * c.noise(70, 14) + 0.1 * c.noise(20, 17), 0, 1), [ph, c.ph_lin(-62, seed=15)],
          swell=1.0)
    # 水洼：映着天光（留白 + 几道横线），边上一圈湿沙的暗边
    for k, (cx, cy, rx) in enumerate(((330, 700, 220), (1560, 640, 170), (1370, 1060, 300), (240, 1120, 200))):
        ry = rx * (0.12 + 0.12 * (cy - HORIZON) / (H - HORIZON))
        n = 11
        t = np.linspace(0, 2 * math.pi, n, endpoint=False)
        rr = 1 + 0.22 * rng.uniform(-1, 1, n)
        pts = [(cx + rx * rr[i] * math.cos(t[i]), cy + ry * rr[i] * math.sin(t[i])) for i in range(n)]
        pm = c.smask(pts)
        c.paint(c.smask([(x + (x - cx) * 0.08, y + (y - cy) * 0.25) for x, y in pts]), 0.7)   # 水洼边上一圈湿沙
        c.clear(pm)
        hatch(c, pm, 0.1 + 0.25 * c.grad(0, cy - ry, 0, cy + ry), [c.ph_lin(0, wobble=0.3, seed=16 + k)], spacing=9, wmax=2.0)
        for j in range(3):                                                 # 天光的倒影
            yy = cy - ry * 0.3 + j * ry * 0.3
            c.erase(c.stroke([(cx - rx * 0.5 + j * 20, yy), (cx + rx * 0.3, yy)], 2.4, add=False, taper=0.4) * pm)
    # 石头和海草
    for x, y, s in ((140, 860, 70), (520, 620, 34), (1720, 760, 60), (1630, 1180, 90), (700, 1180, 60), (1210, 600, 26)):
        rock(c, [(x - s, y), (x - s * 0.6, y - s * 0.7), (x + s * 0.2, y - s * 0.9), (x + s, y - s * 0.2), (x + s * 0.8, y + s * 0.2),
                 (x - s * 0.7, y + s * 0.25)], tone=0.85, seed=x)
        c.fill(c.shadow_of(c.ellipse(x + s * 0.4, y + s * 0.25, s * 1.0, s * 0.18), 0, 0, blur=4) * (c.yy > y), value=0.9)

    altar(c, rng)

    # ============================================================ 癫狂层：浅水里站着的一排高个子，石坛正面的眼
    for k, x in enumerate((180, 420, 1490, 1690, 1850)):
        hooded(c, x, HORIZON + 22, 210 + 30 * (k % 2), tone=0.8, layer='mad')
    eye(c, 772, 760, 64, layer='mad', look=0.0, width=6.0, lash=True)


def altar(c, rng):
    """方石坛：顶面是平行四边形，正面朝左下，右面朝右下（光从左上，右面最暗）。"""
    top = [(700, 556), (1080, 514), (1220, 590), (840, 636)]
    front = [(700, 556), (840, 636), (842, 960), (712, 896)]
    side = [(840, 636), (1220, 590), (1208, 890), (842, 960)]
    # 影子：从基座往右下拖在湿沙上，贴着石坛的底边
    sh = c.mask(poly([(842, 960), (1208, 890), (1500, 950), (1150, 1040)]))
    c.fill(c.shadow_of(sh, 0, 0, blur=8), value=0.9)
    c.fill(c.mask(poly([(700, 890), (842, 952), (1210, 884), (1230, 900), (842, 976), (690, 904)])), value=1.0)   # 底边的接地线
    # 石头：排线抖得厉害、浓淡斑驳（风化的石面），不要规整的网格
    stone = c.noise(26, 30) + 0.6 * c.noise(9, 36)
    for pts, base, ph in ((front, 0.42, c.ph_lin(88, wobble=3.5, wscale=30, seed=31)), (side, 0.76, c.ph_lin(84, wobble=3.5, wscale=30, seed=32)),
                          (top, 0.16, c.ph_lin(-8, wobble=3.0, wscale=30, seed=33))):
        m = c.mask(poly(pts))
        hatch(c, m, np.clip(base + 0.22 * stone, 0, 1), [ph, c.ph_lin(40, wobble=3.0, wscale=30, seed=34)], spacing=8, swell=1.3,
              bands=[(0.04, 0.45), (0.5, 0.9)])
    for crack in ([(760, 600), (790, 690), (770, 760), (800, 860)], [(1000, 640), (1030, 720), (1010, 800)],
                  [(1140, 620), (1120, 700), (1150, 780), (1130, 850)]):
        c.line(crack, w=3.2, taper=0.3)
        c.erase(c.stroke(offset_pts(crack, -4), 2.2, add=False, taper=0.3))
    for pts in (front, side, top):
        c.stroke(poly(pts + [pts[0]]), OUT, taper=0, light=0.3)
    # 石块的接缝：正面、侧面各两道
    for y in (740, 840):
        c.line([(704, y - 140 + 40), (841, y)], w=2.4, taper=0.1)
        c.line([(841, y), (1212, y - 46)], w=2.4, taper=0.1)
    for p0, p1 in (((700, 556), (720, 600)), ((1080, 514), (1100, 540)), ((1220, 590), (1196, 610))):   # 磕掉的棱角
        c.clear(c.ellipse(p0[0] + (p1[0] - p0[0]) * 0.3, p0[1] + (p1[1] - p0[1]) * 0.3, 16, 10))
        c.line([p0, p1], w=3, taper=0.2)
    # 顶面的螺旋槽：一圈圈往里收，槽是黑的，槽里的水着深渊青
    cx, cy = 962, 576
    spiral = []
    for t in np.linspace(0, 3.6 * math.pi, 300):
        r = 10 + 14 * t
        spiral.append((cx + r * math.cos(t) * 1.35, cy + r * math.sin(t) * 0.33))
    spiral = np.array(spiral)
    inside_top = c.mask(poly(top))
    groove = c.stroke(spiral, 14, add=False, taper=(0.4, 0.0)) * inside_top
    c.add(groove)
    c.add(c.stroke(spiral, 8, add=False, taper=(0.4, 0.0)) * inside_top, 'abyss')
    # 从螺旋尽头通到边缘的沟，再沿正面往下淌
    channel = [tuple(spiral[-1]), (850, 630), (842, 660)]
    c.stroke(spline(channel), 12, taper=0)
    c.add(c.stroke(spline(channel), 7, add=False, taper=0), 'abyss')
    drip = [(838, 660), (832, 760), (840, 840), (830, 930)]
    c.add(c.stroke(spline(drip), 7, add=False, taper=(0.0, 0.6)), 'abyss')
    c.stroke(spline(drip), 3, taper=(0.0, 0.6))
    # 基座上的藤壶和挂着的海草
    for _ in range(26):
        x = rng.uniform(780, 1170)
        y = 880 + (x - 870) * -0.19 if x > 870 else 870 + (x - 770) * 0.5
        y += rng.uniform(-20, 4)
        r = rng.uniform(5, 11)
        c.clear(c.ellipse(x, y, r, r * 0.8))
        c.stroke(ellipse_pts(x, y, r, r * 0.8), 2.2, closed=True)
        c.fill(c.ellipse(x, y, r * 0.35, r * 0.3), value=1.0)
    for x0 in (800, 950, 1100):
        y0 = 650 if x0 < 870 else 640 - (x0 - 870) * 0.1
        c.line([(x0, y0), (x0 + 10, y0 + 60), (x0 - 6, y0 + 140), (x0 + 12, y0 + 210)], w=10, taper=(0.1, 0.7))


def offset_pts(pts, d):
    from etch import offset
    return offset(spline(pts), d)
