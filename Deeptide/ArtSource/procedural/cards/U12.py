"""U12 心智崩塌：碎裂的石膏头像，裂缝里是空的（策划/10 §4.6）。

一尊古典石膏胸像，侧脸朝右，立在暗色背景前；头顶缺了一大块，露出里面空空的壳（只有薄薄一层石膏和黑暗），
裂纹从缺口蔓延到额头和脸颊，几块碎片正往下掉。石膏是白的，只用很淡的排线塑形，在暗底上读得很清楚。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, smoothstep, spline

NAME = '心智崩塌'


def draw(c):
    rng = np.random.default_rng(12)
    F = Frame((590, 500), scale=0.8)

    # ------------------------------------------------------------ 背景：暗，左上一点光
    c.engrave(np.ones((c.h, c.w), np.float32), 0.6 + 0.3 * c.grad(200, 100, 1100, 800) + 0.05 * c.noise(100, 1),
              [c.ph_lin(-50, wobble=2, seed=2), c.ph_lin(40, seed=3), c.ph_lin(-5, seed=4)], spacing=9.5, swell=1.1)

    # ------------------------------------------------------------ 胸像：侧脸朝右 + 脖子 + 底座
    prof = [(-60, -330), (60, -320), (130, -260), (160, -180), (162, -120), (178, -84), (204, -30), (212, -6), (196, 4),
            (198, 26), (190, 42), (198, 58), (186, 70), (190, 92), (170, 118), (130, 128), (110, 180), (110, 260),
            (200, 330), (240, 420), (-260, 420), (-210, 330), (-150, 270), (-150, 180), (-190, 60), (-200, -120),
            (-170, -260)]
    head = c.mask(spline(F(prof), closed=True))
    c.clear(np.clip(c.shadow_of(head, 0, 0, blur=6) * 2, 0, 1))
    lam = c.relief(head, radius=80, blur=6)
    side = c.grad(*F.p(200, -100), *F.p(-200, 100))
    c.engrave(head, np.clip(0.02 + 0.32 * side + 0.25 * (1 - lam) ** 1.5, 0, 1),
              [c.ph_axis(F.p(0, -330), F.p(0, 420), bow=-34, half=200, seed=5), c.ph_lin(60, seed=6)], spacing=8.5, wmax=2.4)
    c.outline(F.cp(prof), w=OUTLINE, light=0.45)
    # 古典胸像的五官：杏仁眼没有瞳孔、直鼻、抿着的唇
    c.line(F.cp([(110, -78), (134, -90), (156, -78)]), w=4.5, taper=0.25)          # 上眼睑
    c.line(F.cp([(116, -62), (140, -58), (156, -70)]), w=2.4, taper=0.3)
    c.line(F.cp([(90, -112), (130, -126), (166, -112)]), w=3.4, taper=0.35)        # 眉弓
    c.line(F.cp([(186, -10), (172, 0), (178, 10)]), w=3.0, taper=0.3)              # 鼻翼
    c.line(F.cp([(196, 42), (178, 44), (166, 40)]), w=3.4, taper=0.3)
    c.line(F.cp([(100, 150), (60, 170), (20, 190)]), w=2.2, taper=0.4)             # 下颌
    # 头发：古典卷发，一圈圈的小螺旋（缺口那一块没有）
    for k in range(26):
        a = math.radians(rng.uniform(150, 290))
        rr = rng.uniform(140, 200)
        x, y = 0 + rr * math.cos(a) * 0.95 - 30, -120 + rr * math.sin(a) * 0.95
        if y < -250 and x > -40:
            continue
        t = np.linspace(0, 1.6 * math.pi, 30)
        sz = rng.uniform(14, 24)
        curl = np.stack([x + sz * (1 - t / 6) * np.cos(t), y + sz * (1 - t / 6) * np.sin(t)], 1)
        c.stroke(F(curl), 2.6, taper=(0.2, 0.6))
    c.line(F.cp([(130, -200), (60, -240), (-20, -250), (-120, -220), (-180, -150), (-190, -40), (-160, 60)]), w=3.0, taper=0.3)
    # 底座
    base = [(-300, 420), (280, 420), (300, 470), (-320, 470)]
    c.engrave(c.mask(F(base)), 0.35, [c.ph_lin(0)], spacing=7, wmax=2.2)
    c.stroke(F(base + base[:1]), STRUCT, taper=0)

    # ------------------------------------------------------------ 缺口：头顶碎掉一大块，里面是空的
    hole = []
    for k, a in enumerate(np.linspace(0, 2 * math.pi, 22, endpoint=False)):
        rr = (1 + 0.28 * rng.uniform(-1, 1)) * (1.0 if k % 2 == 0 else 0.78)
        hole.append((-10 + 130 * rr * math.cos(a), -250 + 70 * rr * math.sin(a)))
    hm = c.mask(F(hole)) * head
    c.clear(hm)
    # 壳的厚度：沿缺口边一窄条亮石膏，里面全黑
    inner = c.mask(F([(-10 + (x + 10) * 0.84, -250 + (y + 250) * 0.8 - 6) for x, y in hole]))
    c.engrave(hm, 0.2, [c.ph_lin(70)], spacing=6, wmax=2)
    c.fill(inner * hm, value=1.0)
    c.stroke(F(hole + hole[:1]), STRUCT, taper=0)
    # 往上崩飞的碎片
    for x, y, s in ((40, -400, 26), (-60, -440, 18), (130, -380, 16), (-140, -360, 14), (90, -470, 11)):
        n = 5
        ang = np.sort(rng.uniform(0, 2 * math.pi, n))
        pts = [(x + s * rng.uniform(0.6, 1.2) * math.cos(a), y + s * rng.uniform(0.6, 1.2) * math.sin(a), 'c') for a in ang]
        m = c.mask(F([p[:2] for p in pts]))
        c.clear(m)
        c.engrave(m * c.half_plane(F.p(x + 20, y - 20), F.p(x - 20, y + 20)), 0.5, [c.ph_lin(rng.uniform(0, 180))],
                  spacing=5, wmax=2)
        c.outline(F.cp(pts), w=3.0, light=0.4)

    # ------------------------------------------------------------ 裂纹：从缺口往下蔓延，分叉
    def crack(p, ang, ln, w, depth):
        pts = [p]
        for _ in range(int(ln / 18)):
            ang += rng.uniform(-0.6, 0.6)
            pts.append((pts[-1][0] + 18 * math.cos(ang), pts[-1][1] + 18 * math.sin(ang)))
        c.stroke(F(pts), w, taper=(0.0, 0.8))
        if depth > 0:
            for j in range(1, len(pts) - 1, 3):
                if rng.random() < 0.5:
                    crack(pts[j], ang + rng.choice([-1, 1]) * rng.uniform(0.5, 1.0), ln * 0.45, w * 0.6, depth - 1)

    for p, ang, ln in (((60, -190), 1.6, 260), ((-10, -182), 1.3, 340), ((-110, -200), 1.9, 300), ((110, -215), 1.0, 200)):
        crack(p, ang, ln, 5.0, 2)
