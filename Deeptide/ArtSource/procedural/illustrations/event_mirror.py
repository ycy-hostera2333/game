"""事件 4 镜中倒影：废屋里立着一面落地镜，镜中的你比你慢半拍（Data/events.csv）。

废屋的一角：后墙的条纹墙纸剥落了几块，露出板条；左边一扇破窗，月光斜着落在地板上；地板往里收。
右中立着一面椭圆的落地镜（雕花木框、两只脚），镜子里是正面的调查员——帽檐下一片黑，左手刚抬到一半；
画面左下是调查员本人的背影，手已经抬到了胸前（倒影慢了半拍）。
癫狂层：镜中人的帽檐下咧开一排牙，镜框边上爬出几条细触须。
"""
import math

import numpy as np

from etch import Frame, ellipse_pts, poly, resample, smoothstep, spline
from cards._common import tentacle
from illustrations._kit import GAP, OUT, STRUCT, hatch, investigator

NAME = '镜中倒影'
SIZE = (1920, 1280)
FLOOR_Y = 900            # 后墙和地板的交线
VP = (1000, 640)


def draw(c):
    rng = np.random.default_rng(4)
    W, H = c.w, c.h

    # ------------------------------------------------------------ 后墙：竖条纹墙纸，剥落的地方露出斜板条
    wall = c.rect(0, 0, W, FLOOR_Y)
    stripes = np.abs(((c.xx / 60) % 2) - 1) < 0.5
    hatch(c, wall, np.clip(0.4 + 0.18 * stripes + 0.22 * c.grad(0, 0, 0, FLOOR_Y) * 0 + 0.25 * c.radial(600, 380, 300, 1300)
                           + 0.08 * c.noise(60, 1), 0, 1), [c.ph_lin(90, wobble=1.0, seed=2), c.ph_lin(30, seed=3)], spacing=8)
    for k, (x, y, w, h) in enumerate(((1560, 90, 260, 330), (560, 470, 200, 250), (1760, 600, 150, 230))):
        # 撕开的墙纸：锯齿状的边，里面是一条条斜钉的板条，板条之间是黑缝
        n = 14
        t = np.linspace(0, 2 * math.pi, n, endpoint=False)
        rr = 1 + 0.28 * rng.uniform(-1, 1, n)
        edge = [(x + w / 2 + w / 2 * rr[i] * math.cos(t[i]), y + h / 2 + h / 2 * rr[i] * math.sin(t[i]), 'c') for i in range(n)]
        peel = c.mask(poly(edge))
        c.paint(peel, 0.92)
        for j in range(int(h / 30) + 2):                                   # 板条（亮）
            yy = y + j * 30 - 10
            lath = c.mask(poly([(x - 20, yy), (x + w + 20, yy - 24), (x + w + 20, yy - 6), (x - 20, yy + 18)])) * peel
            c.clear(lath)
            hatch(c, lath, 0.3, [c.ph_lin(-5, seed=40 + j)], spacing=8, wmax=2.0)
        c.stroke(poly(edge + [edge[0]]), 3.4, taper=0)
        for i in range(0, n, 3):                                           # 卷起来的墙纸边：亮的小三角
            p0 = edge[i]
            flap = [(p0[0], p0[1]), (p0[0] + 26 * math.cos(t[i] + 0.4), p0[1] + 26 * math.sin(t[i] + 0.4)),
                    (p0[0] + 30 * math.cos(t[i] - 0.3), p0[1] + 30 * math.sin(t[i] - 0.3))]
            c.clear(c.mask(poly(flap)))
            c.stroke(poly(flap + [flap[0]]), 2.2, taper=0)
    # 踢脚线
    c.paint(c.rect(0, FLOOR_Y - 40, W, FLOOR_Y), 0.8)
    c.stroke([(0, FLOOR_Y - 40), (W, FLOOR_Y - 40)], 3, taper=0)
    c.stroke([(0, FLOOR_Y), (W, FLOOR_Y)], 4, taper=0)

    # ------------------------------------------------------------ 窗：左上，破了一格，月光斜着进来
    win = (110, 130, 470, 560)
    x0, y0, x1, y1 = win
    c.clear(c.rect(x0, y0, x1, y1))
    hatch(c, c.rect(x0, y0, x1, y1), 0.12 + 0.2 * c.grad(0, y0, 0, y1), [c.ph_lin(-10, seed=12)], spacing=10, wmax=2.0)
    for x in (x0, (x0 + x1) / 2, x1):
        c.stroke([(x, y0), (x, y1)], 12, taper=0)
    for y in (y0, (y0 + y1) / 2, y1):
        c.stroke([(x0 - 6, y), (x1 + 6, y)], 12, taper=0)
    # 右下那格碎了：黑洞洞的一块，边上留着几片尖玻璃
    hole = [((x0 + x1) / 2 + 6, (y0 + y1) / 2 + 6), (x1 - 6, (y0 + y1) / 2 + 6), (x1 - 6, y1 - 6), ((x0 + x1) / 2 + 6, y1 - 6)]
    c.fill(c.mask(poly(hole)), value=0.97)
    for shard in ([(hole[0][0], hole[0][1]), (hole[0][0] + 90, hole[0][1]), (hole[0][0] + 30, hole[0][1] + 70)],
                  [(hole[1][0], hole[1][1]), (hole[1][0], hole[1][1] + 120), (hole[1][0] - 60, hole[1][1] + 20)],
                  [(hole[3][0], hole[3][1]), (hole[3][0] + 70, hole[3][1]), (hole[3][0], hole[3][1] - 100)]):
        c.clear(c.mask(poly(shard)))
        hatch(c, c.mask(poly(shard)), 0.15, [c.ph_lin(-10, seed=13)], spacing=9, wmax=2.0)
        c.stroke(poly(shard + [shard[0]]), 2.4, taper=0)
    # 月光：从窗户落到地板上的一块斜光（把这块地方的墨擦淡）
    beam = c.mask(poly([(x0, y0), (x1, y0), (1120, H), (430, H)]))
    floor_part = c.yy > FLOOR_Y
    c.erase(beam * 0.35 * (1 - floor_part) * smoothstep(y0, 900, c.yy))

    # ------------------------------------------------------------ 地板：朝灭点收的木板，月光照到的地方亮
    fl = c.rect(0, FLOOR_Y, W, H)
    d0 = 80.0
    rows = 7.5 * d0 / 9 * np.log(np.maximum(c.yy - FLOOR_Y + d0, 1) / d0)
    lightpatch = c.mask(poly([(560, FLOOR_Y), (900, FLOOR_Y), (1240, H), (520, H)]))
    hatch(c, fl, np.clip(0.75 - 0.45 * lightpatch + 0.08 * c.noise(50, 20), 0, 1),
          [c.ph_ray(VP[0], VP[1] - 900, r0=900), rows], spacing=9, swell=1.1)
    for k in range(-9, 12):                                                # 木板的缝：朝灭点放射
        xb = 960 + k * 150
        c.stroke([(VP[0] + (xb - VP[0]) * 0.27, FLOOR_Y), (xb, H)], 2.6, taper=0)
    # 灰尘里的脚印：一串椭圆，从左下走向镜子
    for k in range(5):
        fx, fy = 500 + k * 110 + (k % 2) * 30, 1220 - k * 60
        c.erase(c.ellipse(fx, fy, 22 - k * 2, 9 - k) * 0.6)

    mirror(c, rng)
    # 调查员本人：左下的背影，手已经抬到胸前
    F = Frame((420, 1420), scale=2.6)
    investigator(c, F, tone=0.97, hang=(-1,))
    # 本人的左手（画面右）已经抬到胸前：盖掉垂着的那只，画成弯起来的胳膊
    c.line(F.cp([(56, -232), (78, -196), (52, -176), (20, -186)]), w=20 * F.scale, taper=(0.0, 0.15))


def mirror(c, rng):
    """椭圆落地镜：雕花木框、两只脚。镜子里是正面的倒影。"""
    cx, cy, rx, ry = 1340, 560, 190, 330
    glass = c.ellipse(cx, cy, rx, ry)
    # 镜面：深色的倒影空间（墙和窗映在里面，更暗一点）
    c.clear(glass)
    hatch(c, glass, np.clip(0.72 + 0.15 * c.grad(cx - rx, 0, cx + rx, 0) + 0.06 * c.noise(40, 30), 0, 1),
          [c.ph_lin(90, seed=31), c.ph_lin(20, seed=32)], spacing=8)
    c.erase(glass * c.mask(poly([(cx - rx, cy - ry), (cx - rx + 70, cy - ry), (cx - 40, cy + ry), (cx - rx, cy + ry)])) * 0.45)  # 反光
    # 镜中的调查员：正面，帽檐下一片黑，左手（画面右）抬到一半
    F = Frame((cx + 10, cy + ry - 30), scale=1.25)
    coat = [(-36, 0), (-48, -100), (-56, -190), (-46, -240), (-24, -262), (24, -262), (46, -240), (56, -190), (48, -100), (36, 0)]
    m = c.mask(spline(F(coat), closed=True)) * glass
    hatch(c, m, 0.9, [c.ph_lin(86, seed=33), c.ph_lin(30, seed=34)], spacing=7, swell=1.2)
    c.add(c.stroke(spline(F(coat), closed=True), STRUCT, closed=True, add=False) * glass)
    c.add(c.ellipse(*F.p(0, -284), 22 * F.scale, 26 * F.scale) * glass)
    c.add(c.ellipse(*F.p(0, -302), 58 * F.scale, 12 * F.scale) * glass)
    c.add(c.mask(F([(-26, -302), (-22, -344), (22, -344), (26, -302)])) * glass)
    c.erase(c.stroke(F([(-14, -270), (14, -270)]), 3, add=False) * 0.6 * glass)   # 下巴一道微光
    c.add(c.stroke(spline(F([(46, -232), (62, -190), (70, -160)])), 15 * F.scale, add=False, taper=(0.0, 0.3)) * glass)
    c.add(c.stroke(spline(F([(-46, -232), (-60, -170), (-56, -120)])), 15 * F.scale, add=False, taper=(0.0, 0.3)) * glass)
    # 木框：两圈勾线之间是一圈雕花（交替的小卷草）
    for d in (0, 34):
        c.stroke(ellipse_pts(cx, cy, rx + d, ry + d), OUT if d else STRUCT, closed=True)
    ring = c.ellipse(cx, cy, rx + 34, ry + 34) * (1 - c.ellipse(cx, cy, rx + 2, ry + 2))
    hatch(c, ring, 0.6, [c.ph_ring(cx, cy, 1.0, ry / rx), c.ph_lin(45, seed=35)], spacing=7)
    for t in np.linspace(0, 2 * math.pi, 26, endpoint=False):
        px, py = cx + (rx + 17) * math.cos(t), cy + (ry + 17) * math.sin(t)
        c.clear(c.ellipse(px, py, 7, 7))
        c.stroke(ellipse_pts(px, py, 7, 7), 2.2, closed=True)
    # 顶上的雕饰和两只脚
    crest = [(cx - 60, cy - ry - 30), (cx - 20, cy - ry - 80), (cx, cy - ry - 110), (cx + 20, cy - ry - 80), (cx + 60, cy - ry - 30)]
    cm = c.mask(spline(crest + [(cx, cy - ry - 20)], closed=True))
    hatch(c, cm, 0.7, [c.ph_lin(60, seed=36)], spacing=7)
    c.outline(crest + [(cx, cy - ry - 20)], w=STRUCT)
    for sd in (-1, 1):
        leg = [(cx + sd * 120, cy + ry - 30), (cx + sd * 170, cy + ry + 60), (cx + sd * 210, cy + ry + 130)]
        c.line(leg, w=18, taper=(0.0, 0.3))
    c.line([(cx - 150, cy + ry + 40), (cx + 150, cy + ry + 40)], w=10, taper=0)

    # ============================================================ 癫狂层：镜中人咧开的牙，镜框边爬出的触须
    mouth = spline(F.cp([(-22, -274), (0, -264), (22, -274)]))
    c.stroke(mouth, 4, layer='mad', taper=0)
    for k in range(9):                                                     # 咧开的一排尖牙
        x, y = F.p(-20 + k * 5, -272 + 4 * math.sin(k / 8 * math.pi))
        c.stroke([(x - 3, y), (x, y + 14), (x + 3, y)], 3.2, layer='mad', taper=0)
    for sd in (-1, 1):                                                     # 帽檐下一对亮着的眼
        c.add(c.ellipse(*F.p(sd * 9, -292), 5 * F.scale, 3 * F.scale), 'mad')
    for t, ln, sd in ((0.3, 220, 1), (1.4, 180, -1), (2.6, 260, 1), (4.0, 200, -1), (5.2, 240, 1)):
        px, py = cx + (rx + 30) * math.cos(t), cy + (ry + 30) * math.sin(t)
        ox, oy = math.cos(t), math.sin(t)
        ctrl = [(px, py), (px + ox * ln * 0.4 - oy * 40 * sd, py + oy * ln * 0.4 + ox * 40 * sd),
                (px + ox * ln * 0.8 + oy * 30 * sd, py + oy * ln * 0.8 - ox * 30 * sd), (px + ox * ln, py + oy * ln)]
        tentacle(c, ctrl, 26, layer='mad', tone=0.6, taper=0.92, outline=3.0)
