"""C09 狂乱挥砍：手杖挥过留下多道残影弧线。癫狂层：残影变成多条手臂（策划/10 §4.6）。

调查员的半身剪影在左边，面朝右，右臂向右下挥出手杖；肩膀上方扇出三道淡淡的残影手杖，杖头拖出一束同心弧。
癫狂层给每一道残影都接上一条从肩膀长出来的手臂。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, smoothstep, spline

NAME = '狂乱挥砍'

SHOULDER = (310, 430)
ARM_LEN = 300
CANE_LEN = 400
CURRENT = (24, 40)                               # (手臂角度, 杖的角度)
GHOSTS = [(-74, -58), (-45, -27), (-16, 4)]


def hand_pos(arm_angle):
    a = math.radians(arm_angle)
    return (SHOULDER[0] + ARM_LEN * math.cos(a), SHOULDER[1] + ARM_LEN * math.sin(a))


def cane_pts(hand, angle, half=15, back=60):
    d = np.array([math.cos(math.radians(angle)), math.sin(math.radians(angle))])
    n = np.array([-d[1], d[0]])
    h = np.array(hand)
    b, t = h - d * back, h + d * CANE_LEN
    return [tuple(b + n * half), tuple(t + n * (half - 4)), tuple(t - n * (half - 4)), tuple(b - n * half)], d, n


def draw(c):
    sx, sy = SHOULDER
    # ------------------------------------------------------------ 背景：雾，右上亮
    bg = np.ones((c.h, c.w), np.float32)
    c.engrave(bg, 0.16 + 0.45 * c.grad(1000, 200, 300, 864) ** 1.3 + 0.05 * c.noise(120, 1),
              [c.ph_lin(-15, wobble=2, seed=2), c.ph_lin(60, seed=3)], spacing=10.5, bands=[(0.04, 0.5), (0.45, 0.85)])

    # ------------------------------------------------------------ 杖头划过的一束弧
    ang = np.degrees(np.arctan2(c.yy - sy, c.xx - sx))
    r = np.hypot(c.xx - sx, c.yy - sy)
    a0, a1 = GHOSTS[0][1] - 6, CURRENT[1] - 2
    fan = smoothstep(a0, a0 + 12, ang) * (1 - smoothstep(a1 - 1, a1 + 1, ang)) * smoothstep(380, 460, r) * (1 - smoothstep(700, 760, r))
    c.erase(fan * 0.9)
    k = np.clip((ang - a0) / (a1 - a0), 0, 1)
    arcs = c.lines(r + 3 * c.noise(30, 4), 15, 3.8 * k ** 1.6 * smoothstep(440, 560, r))
    c.add(arcs * fan)

    # ------------------------------------------------------------ 残影手杖：只有淡淡的外形和几笔排线
    for k, (arm_a, cane_a) in enumerate(GHOSTS):
        pts, d, n = cane_pts(hand_pos(arm_a), cane_a, half=17)
        m = c.mask(poly(pts))
        fade = (k + 1) / (len(GHOSTS) + 1)                                   # 越早的残影越淡
        c.clear(np.clip(c.shadow_of(m, 0, 0, blur=3) * 2, 0, 1))
        c.engrave(m, 0.25 + 0.4 * fade, [c.ph_along(pts[0], pts[1], wobble=0.3), c.ph_lin(cane_a + 90)], spacing=7, wmax=2.4)
        c.stroke(poly(pts + pts[:1]), 2.4 + 2.0 * fade, taper=0)
        # 握点的一团模糊（残影里的拳头）
        hx, hy = hand_pos(arm_a)
        fist = c.ellipse(hx, hy, 34, 30)
        c.clear(fist)
        c.engrave(fist, 0.2 + 0.4 * fade, [c.ph_lin(cane_a)], spacing=6.5, wmax=2.2)
        c.stroke(ellipse_pts(hx, hy, 34, 30), 2.0 + 1.6 * fade, closed=True)

    # ------------------------------------------------------------ 调查员：半身剪影，帽子 + 风衣，面朝右
    body = [(-40, 330), (140, 300), (250, 330), (360, 380), (400, 480), (410, 620), (430, 900), (-40, 900)]
    m = c.mask(spline(body, closed=True))
    lam = c.relief(m, radius=90, blur=5)
    c.engrave(m, np.clip(0.98 - 0.55 * lam, 0.45, 1), [c.ph_lin(80, seed=5), c.ph_lin(20, seed=6), c.ph_lin(-40, seed=7)],
              spacing=9, swell=1.0)
    c.outline(body, w=OUTLINE + 1, light=0.4)
    c.line([(250, 340), (290, 450), (300, 620), (320, 860)], w=3.5, taper=0.3)       # 风衣的缝
    c.line([(130, 312), (170, 400), (180, 520)], w=3.0, taper=0.3)                     # 衣领
    # 头 + 帽：3/4 背面，竖起的衣领挡住脖子
    head = c.ellipse(232, 262, 52, 62, rot=-8)
    lam = c.relief(head, radius=30, blur=2)
    c.engrave(head, np.clip(0.95 - 0.4 * lam, 0.5, 1), [c.ph_lin(60, seed=8), c.ph_lin(-30, seed=9)], spacing=8)
    c.stroke(ellipse_pts(232, 262, 52, 62, rot=-8), STRUCT + 1, closed=True)
    c.line([(276, 250), (288, 262), (282, 282)], w=3.0, taper=0.3)                     # 耳朵
    collar = [(150, 330), (170, 270), (205, 300), (262, 312), (300, 286), (320, 340), (250, 352)]
    m = c.mask(spline(collar, closed=True))
    c.engrave(m, 0.7, [c.ph_lin(70, seed=16), c.ph_lin(-20, seed=17)], spacing=8)
    c.outline(collar, w=STRUCT + 1, light=0.4)
    brim = c.ellipse(238, 214, 128, 25, rot=-6)
    crown = c.mask(spline([(168, 214), (178, 132), (238, 116), (306, 128), (312, 208)], closed=True))
    hat = np.clip(brim + crown, 0, 1)
    c.engrave(hat, 0.92, [c.ph_lin(84, seed=10), c.ph_lin(-6, seed=11)], spacing=7.5, swell=1)
    c.stroke(ellipse_pts(238, 214, 128, 25, rot=-6), STRUCT + 1, closed=True)
    c.line([(168, 214), (178, 132), (238, 116), (306, 128), (312, 208)], w=STRUCT + 1, taper=0)
    c.line([(174, 190), (240, 186), (310, 186)], w=6, taper=0.1)                        # 帽带

    # ------------------------------------------------------------ 此刻的手臂和手杖
    pts, d, n = cane_pts(hand_pos(CURRENT[0]), CURRENT[1], half=20)
    m = c.mask(poly(pts))
    v = c.ph_along(pts[0], pts[1], wobble=0) / 20
    c.engrave(m, np.clip(0.6 + 0.3 * v, 0.1, 1), [c.ph_along(pts[0], pts[1], wobble=0.4, seed=12), c.ph_lin(CURRENT[1] + 90)],
              spacing=7.5, swell=1)
    c.stroke(poly([pts[0], pts[1]]), 3.5, taper=0)
    c.stroke(poly([pts[3], pts[2]]), 6, taper=0)
    tx, ty = np.array(hand_pos(CURRENT[0])) - d * 66                           # 银杖头
    c.fill(c.ellipse(tx, ty, 20, 20), value=0.0)
    c.engrave(c.ellipse(tx, ty, 20, 20), 0.4, [c.ph_ring(tx, ty)], spacing=5, wmax=2)
    c.stroke(ellipse_pts(tx, ty, 20, 20), 3.6, closed=True)
    arm(c, CURRENT[0], 'ink', 0.95)

    # ============================================================ 癫狂层：每道残影都长出一条手臂，从背上、肩上、肋下各长出一条
    roots = [(200, 330), (290, 360), (372, 520)]
    bends = [-60, -50, 40]
    for (arm_a, _), root, bend in zip(GHOSTS, roots, bends):
        limb(c, root, hand_pos(arm_a), bend)


def limb(c, root, hand, bend):
    """癫狂层的手臂：从 root 长到 hand，中间弯一个肘；袖子排线 + 两侧轮廓 + 拳头。"""
    r, h = np.array(root, float), np.array(hand, float)
    d = h - r
    L = np.hypot(*d)
    n = np.array([-d[1], d[0]]) / L
    elbow = r + d * 0.5 + n * bend
    center = spline([tuple(r), tuple(elbow), tuple(h)])
    prof = lambda u: 1.0 - 0.3 * u
    m = c.stroke(center, 70, add=False, taper=0, profile=prof)
    t = np.gradient(center, axis=0)
    t /= np.hypot(t[:, 0], t[:, 1])[:, None]
    nn = np.stack([-t[:, 1], t[:, 0]], 1)
    u = np.linspace(0, 1, len(center))[:, None]
    half = 35 * (1 - 0.3 * u)
    c.engrave(m, 0.55, [c.ph_along(tuple(r), tuple(h), wobble=0.4), c.ph_lin(math.degrees(math.atan2(d[1], d[0])) + 90)],
              layer='mad', spacing=8, grain=0)
    for sd in (-1, 1):
        c.stroke(center + nn * half * sd, STRUCT, layer='mad', taper=(0.0, 0.05))
    # 肘弯的褶
    k = len(center) // 2
    c.stroke(spline([tuple(center[k] + nn[k] * 30), tuple(center[k] + t[k] * 8), tuple(center[k] - nn[k] * 30)]), 2.6, layer='mad',
             taper=0.3)
    # 拳头
    A = Frame(tuple(h), rot=math.degrees(math.atan2(t[-1][1], t[-1][0])))
    fist = [(-10, -32), (26, -36), (50, -20), (54, 14), (38, 34), (0, 36)]
    fm = c.mask(spline(A(fist), closed=True))
    c.engrave(fm, 0.35, [c.ph_lin(A.angle(90))], layer='mad', spacing=6.5, wmax=2.2, grain=0)
    c.outline(A.cp(fist), w=STRUCT, layer='mad', light=0.4)
    for y in (-18, -2, 14, 28):
        c.line(A.cp([(28, y - 6), (40, y), (48, y + 4)]), w=2.2, layer='mad', taper=0.3)


def arm(c, angle, layer, tone):
    """从肩膀伸到握点的袖子 + 拳头。"""
    A = Frame(SHOULDER, rot=angle)
    sleeve = [(-20, -58), (150, -50), (260, -40), (270, 0), (260, 40), (150, 50), (-20, 64)]
    m = c.mask(spline(A(sleeve), closed=True))
    if layer == 'ink':
        lam = c.relief(m, radius=40, blur=3)
        t = np.clip(tone - 0.5 * lam, 0.3, 1)
    else:
        t = np.full_like(m, 0.55)
    c.engrave(m, t, [c.ph_axis(A.p(0, 0), A.p(270, 0), bow=-14, half=55, seed=13), c.ph_lin(A.angle(10), seed=14)],
              layer=layer, spacing=8.5, grain=0 if layer == 'mad' else 0.06)
    c.outline(A.cp(sleeve), w=OUTLINE * 0.8, layer=layer, light=0.4)
    c.line(A.cp([(214, -46), (224, 0), (214, 48)]), w=3, layer=layer, taper=0.2)          # 袖口
    c.line(A.cp([(90, -54), (110, -10), (96, 40)]), w=2.6, layer=layer, taper=0.3)        # 肘弯的褶
    fist = [(260, -36), (300, -40), (326, -22), (330, 14), (314, 36), (274, 40)]
    m = c.mask(spline(A(fist), closed=True))
    lam = c.relief(m, radius=18, blur=1.5)
    c.engrave(m, np.clip(0.55 - 0.6 * lam, 0, 1), [c.ph_lin(A.angle(90), seed=15)], layer=layer, spacing=6.5, wmax=2.2,
              grain=0)
    c.outline(A.cp(fist), w=STRUCT, layer=layer, light=0.4)
    for y in (-20, -4, 12, 26):
        c.line(A.cp([(302, y - 6), (314, y), (322, y + 4)]), w=2.2, layer=layer, taper=0.3)
