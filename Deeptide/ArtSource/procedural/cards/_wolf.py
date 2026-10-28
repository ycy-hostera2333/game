"""狼人卡图（LB / LC / LU / LR，策划/14 §2.5）共用的画法：狼头、狼身剪影、月亮、鳞、爪痕、血滴、银链、芦苇、水纹。

狼人是「半个属于海」的荒原猎户（14 §2.1）：兽形是带鳞、有鳃裂的狼，不是普通的狼。所以凡是兽的部分，
鳃和眼睛画进深渊遮罩（abyss，任何时候都着深渊青），鳞片用 scales() 的弧形排线。血一律实墨（版画里没有红色）。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, _line_cov, ellipse_pts, poly, smoothstep, spline


# ---------------------------------------------------------------- 背景

def night(c, top=0.75, bottom=0.45, horizon=None, seed=0, mask=None):
    """夜空：上暗下亮（月光在低处的雾里），三层交叉排线。horizon 给定时只画到这条线以上。"""
    m = np.ones((c.h, c.w), np.float32) if mask is None else mask
    if horizon is not None:
        m = m * (c.yy < horizon)
    tone = top + (bottom - top) * c.grad(0, 0, 0, horizon or c.h) + 0.05 * c.noise(120, seed + 1)
    c.engrave(m, np.clip(tone, 0, 1), [c.ph_lin(-8, wobble=2.0, seed=seed + 2), c.ph_lin(52, wobble=2, seed=seed + 3),
                                       c.ph_lin(-62, wobble=2, seed=seed + 4)],
              spacing=10, bands=[(0.04, 0.42), (0.38, 0.7), (0.66, 0.95)])
    return m


def moon(c, x, y, r, phase=None, glow=1.6, craters=True, layer='ink', seed=0):
    """满月（phase=None）或残月：月面留白、几块月海用细排线；外面一圈光晕把夜空的排线擦淡。
    phase = (dx, dy)：从这个方向压上来的暗影圆（月食 / 残月），被压住的部分画成实墨。"""
    if glow:
        halo = np.clip(1 - c.radial(x, y, r, r * glow), 0, 1) ** 1.5
        c.erase(halo * 0.85, layer)
    disc = c.ellipse(x, y, r, r)
    c.clear(disc, layers=(layer,))
    if craters:
        rng = np.random.default_rng(seed + 5)
        sea = np.zeros_like(disc)
        for _ in range(9):
            a, d = rng.uniform(0, 2 * math.pi), rng.uniform(0.15, 0.7) * r
            rr = rng.uniform(0.1, 0.24) * r
            sea = np.maximum(sea, 0.6 * c.ellipse(x + d * math.cos(a), y + d * math.sin(a), rr, rr * rng.uniform(0.6, 1.0), rng.uniform(0, 180)))
        sea = sea * (0.7 + 0.3 * c.noise(r * 0.3 + 4, seed + 4))
        tone = 0.32 * sea * (1 + 0.5 * c.noise(r * 0.15 + 4, seed + 6)) + 0.18 * c.radial(x, y, r * 0.55, r * 1.05)
        c.engrave(disc, np.clip(tone, 0, 1), [c.ph_lin(-30, wobble=0.6, seed=seed + 7)], layer=layer, spacing=7, wmax=2.2, grain=0)
    if phase is not None:
        dx, dy = phase
        shade = c.ellipse(x + dx, y + dy, r * 1.02, r * 1.02) * disc
        c.engrave(shade, 0.9, [c.ph_lin(40, seed=seed + 8), c.ph_lin(-40, seed=seed + 9)], layer=layer, spacing=7, swell=1.4)
    c.stroke(ellipse_pts(x, y, r, r), 3.0, layer=layer, closed=True)
    return disc


def ground(c, y0, tone=0.62, slope=0.0, seed=0, layer='ink'):
    """荒原 / 滩涂的地面：横向排线，近处更暗。返回遮罩。"""
    edge = y0 + slope * (c.xx - c.w / 2) + 10 * c.noise(90, seed + 1)
    m = np.clip(c.yy - edge, 0, 1).astype(np.float32)
    tone_f = tone + 0.25 * smoothstep(y0, c.h, c.yy) + 0.06 * c.noise(40, seed + 2)
    c.engrave(m, np.clip(tone_f, 0, 1), [c.ph_wave(slope * 40, 3, 260, seed=seed + 3), c.ph_lin(25, seed=seed + 4)],
              layer=layer, spacing=9, swell=1.0)
    return m


def reeds(c, x0, x1, base, h, n=40, seed=0, lean=0.0, layer='ink', w=3.0):
    """芦苇 / 荒草：一排从地面长出的细茎，顶端弯、收尖。"""
    rng = np.random.default_rng(seed)
    for _ in range(n):
        x = rng.uniform(x0, x1)
        hh = h * rng.uniform(0.55, 1.15)
        bend = (lean + rng.normal(0, 0.18)) * hh
        y = base + rng.uniform(-6, 10)
        c.line([(x, y), (x + bend * 0.25, y - hh * 0.55), (x + bend, y - hh)], w=w * rng.uniform(0.7, 1.2), layer=layer, taper=(0.0, 0.9))
        if rng.random() < 0.3:                                               # 穗
            c.line([(x + bend, y - hh), (x + bend * 1.25 + 8, y - hh * 1.12), (x + bend * 1.4 + 10, y - hh * 1.2)],
                   w=w * 2.2, layer=layer, taper=(0.3, 0.9))


def water(c, y0, tone=0.5, seed=0, layer='ink', mask=None):
    """海面：波浪排线，往近处变暗。返回遮罩。"""
    m = np.clip(c.yy - y0, 0, 1).astype(np.float32) if mask is None else mask
    tone_f = tone + 0.3 * smoothstep(y0, c.h, c.yy) + 0.08 * c.noise(50, seed + 1)
    c.engrave(m, np.clip(tone_f, 0, 1), [c.ph_wave(0, 4, 140, seed=seed + 2), c.ph_wave(0, 6, 90, seed=seed + 3, phase=1.0)],
              layer=layer, spacing=8, swell=1.1)
    return m


def ripples(c, x, y, radii, sy=0.28, w=2.6, layer='ink', erase=False):
    """水面的同心涟漪（扁椭圆）。erase=True 时在暗水面上刻白线。"""
    for k, r in enumerate(radii):
        cov = c.stroke(ellipse_pts(x, y, r, r * sy), w * (1 - 0.12 * k), closed=True, add=False)
        if erase:
            c.erase(cov, layer)
        else:
            c.add(cov, layer)


# ---------------------------------------------------------------- 鳞、毛

def scale_field(c, size, angle=0.0, jitter=0.12, seed=0):
    """鱼鳞格：返回 (弧线距离, 鳞内深浅)。每片鳞是开口朝 angle 反方向的半圆弧，奇数行错开半格，后一行压住前一行。
    angle 是鳞「往后长」的方向（度）：鳞尖指向这个方向。"""
    a = math.radians(angle)
    u = c.xx * math.cos(a) + c.yy * math.sin(a)
    v = -c.xx * math.sin(a) + c.yy * math.cos(a)
    if jitter:
        u = u + jitter * size * c.noise(size * 3, seed + 1)
        v = v + jitter * size * c.noise(size * 3, seed + 2)
    col = np.floor(v / size)
    uu = u / size - 0.5 * (col % 2)
    row = np.floor(uu)
    du = (uu - row) * size                                                    # 0..size，沿生长方向
    dv = v - (col + 0.5) * size                                               # -size/2..size/2
    r = np.hypot(du, dv)
    d = np.abs(r - 0.62 * size) * (du > 0) + (du <= 0) * size
    shade = np.clip(1 - r / (0.62 * size), 0, 1)                               # 鳞根（被上一片压住）暗
    return d, shade


def scales(c, mask, size=34, angle=0.0, tone=0.3, w=2.4, layer='ink', seed=0, shade=0.45):
    """在遮罩里画鳞：每片一道弧，鳞根加一点排线。"""
    d, sh = scale_field(c, size, angle, seed=seed)
    rim = _line_cov(d, w) * mask
    under = np.clip(tone + shade * sh, 0, 1)
    c.engrave(mask, under, [c.ph_lin(angle + 60, wobble=0.5, seed=seed + 3)], layer=layer, spacing=6.5, wmax=2.0, grain=0)
    c.add(rim, layer)
    return rim


def fur(c, mask, angle, length=26, density=0.004, w=2.2, seed=0, layer='ink', jitter=18, tone=None):
    """沿 angle（度，可以是函数 (x, y) → 度）方向的短毛：随机落点的收尖短线。tone 给定时按它决定落点的疏密（暗处密）。"""
    rng = np.random.default_rng(seed)
    ys, xs = np.nonzero(mask > 0.5)
    if len(xs) == 0:
        return
    n = int(len(xs) * density)
    idx = rng.choice(len(xs), size=min(n, len(xs)), replace=False)
    for i in idx:
        x, y = float(xs[i]), float(ys[i])
        if tone is not None and rng.random() > tone[int(y), int(x)]:
            continue
        ang = angle(x, y) if callable(angle) else angle
        ang = math.radians(ang + rng.normal(0, jitter))
        ln = length * rng.uniform(0.6, 1.3)
        bend = rng.normal(0, 0.15) * ln
        p1 = (x + ln * math.cos(ang) - bend * math.sin(ang), y + ln * math.sin(ang) + bend * math.cos(ang))
        c.line([(x, y), ((x + p1[0]) / 2 + bend * 0.3, (y + p1[1]) / 2), p1], w=w * rng.uniform(0.7, 1.2), layer=layer, taper=(0.1, 0.9))


def fringe(c, pts, angle_out, length=22, step=9, w=2.6, seed=0, layer='ink'):
    """沿一条轮廓往外的一排毛尖（剪影边缘的毛茸感）。angle_out：毛尖相对轮廓法线的偏角。"""
    rng = np.random.default_rng(seed)
    pts = np.asarray(pts, float)
    t = np.gradient(pts, axis=0)
    t /= np.maximum(np.hypot(t[:, 0], t[:, 1]), 1e-6)[:, None]
    nrm = np.stack([t[:, 1], -t[:, 0]], 1)
    s = np.concatenate([[0], np.cumsum(np.hypot(*np.diff(pts, axis=0).T))])
    acc = 0.0
    for i in range(len(pts)):
        if s[i] < acc:
            continue
        acc = s[i] + step * rng.uniform(0.7, 1.3)
        a = math.atan2(nrm[i, 1], nrm[i, 0]) + math.radians(angle_out + rng.normal(0, 12))
        ln = length * rng.uniform(0.5, 1.2)
        p0 = pts[i] - nrm[i] * 4
        c.line([tuple(p0), (p0[0] + ln * math.cos(a), p0[1] + ln * math.sin(a))], w=w, layer=layer, taper=(0.0, 0.9))


# ---------------------------------------------------------------- 狼头

# 侧面狼头，面朝 +x；局部坐标：后脑 x≈-150，鼻尖 x≈+175，下颌铰点 HINGE。上半个头（含上唇）和下颌分开画，张嘴就转下颌
SKULL = [(196, -4, 'c'), (170, -28), (100, -40), (40, -58), (-10, -92), (-80, -106), (-145, -80), (-190, -10),
         (-215, 130), (-120, 140), (-40, 60), (40, 28), (120, 18), (190, 10)]
JAW = [(-60, 40), (20, 30), (110, 22), (150, 26, 'c'), (140, 44), (60, 64), (-10, 82), (-60, 90)]
HINGE = (-50, 46)
EAR = [(-60, -96, 'c'), (-82, -178), (-96, -210, 'c'), (-124, -160), (-136, -90)]
EYE = (12, -52)


def wolf_head(c, F, jaw=0.0, snarl=True, layer='ink', tone=0.55, gills=True, scaled=True, eye_layer='abyss', seed=0,
              teeth=True, outline=OUTLINE):
    """侧面狼头（14 §2.1：带鳞、有鳃裂）。F：局部坐标系（面朝 +x）；jaw：下颌张开角（度）。返回整个头的遮罩。"""
    J = Frame(F.p(*HINGE), rot=F.angle(jaw), scale=F.scale)
    jaw_pts = [(x - HINGE[0], y - HINGE[1]) + ((t[2],) if len(t) > 2 else ()) for t in JAW for x, y in [t[:2]]]
    jaw_m = c.mask(spline(J(jaw_pts), closed=True))
    skull_m = c.mask(spline(F(SKULL), closed=True))
    ear_m = c.mask(spline(F(EAR), closed=True))
    mouth = None
    if jaw > 4:                                                                  # 张嘴：上下颌之间是黑的口腔
        mouth = c.mask(poly([F.p(-40, 40), F.p(150, 14), J.p(200, -20), J.p(170, -10), J.p(10, 10)]))
        c.fill(mouth, layer=layer)
    # 下颌
    lam = c.relief(jaw_m, radius=26 * F.scale, blur=2)
    c.engrave(jaw_m, np.clip(tone + 0.2 - 0.5 * lam, 0, 1), [c.ph_lin(F.angle(jaw + 5), seed=seed + 1), c.ph_lin(F.angle(70))],
              layer=layer, spacing=7, swell=1.0)
    c.outline(J.cp(jaw_pts), w=outline * 0.7, layer=layer)
    # 耳朵
    lam = c.relief(ear_m, radius=16 * F.scale, blur=1.5)
    c.engrave(ear_m, np.clip(tone + 0.3 - 0.4 * lam, 0, 1), [c.ph_axis(F.p(-110, -100), F.p(-96, -210), bow=6, half=30 * F.scale)],
              layer=layer, spacing=6.5)
    c.outline(F.cp(EAR), w=outline * 0.7, layer=layer)
    c.line(F.cp([(-90, -110), (-96, -160), (-100, -190)]), w=2.4, layer=layer, taper=0.3)
    # 头：浮雕 + 顺毛方向的排线，口鼻亮、后脑和颈暗
    lam = c.relief(skull_m, radius=60 * F.scale, blur=3)
    back = smoothstep(F.scale * 0, 1, np.clip(c.grad(*F.p(120, 0), *F.p(-200, 60)), 0, 1))
    head_tone = np.clip(tone - 0.45 * lam + 0.35 * back + 0.05 * c.noise(18, seed + 2), 0, 1)
    c.engrave(skull_m, head_tone, [c.ph_axis(F.p(180, -10), F.p(-200, 40), bow=-18 * F.scale, half=90 * F.scale, seed=seed + 3),
                                   c.ph_lin(F.angle(60), seed=seed + 4), c.ph_lin(F.angle(-20), seed=seed + 5)],
              layer=layer, spacing=7.5, swell=1.0)
    if scaled:                                                                    # 颈和颊后半边长着鳞
        sm = skull_m * smoothstep(0.35, 0.65, c.grad(*F.p(80, 0), *F.p(-180, 40)))
        scales(c, sm, size=26 * F.scale, angle=F.angle(170), tone=0.25, layer=layer, seed=seed + 6)
    fringe(c, F(spline(SKULL[4:9])), 20, length=22 * F.scale, step=8 * F.scale, layer=layer, seed=seed + 7)
    fringe(c, F(spline(SKULL[8:11])), 0, length=34 * F.scale, step=7 * F.scale, layer=layer, seed=seed + 8)   # 颈下的鬃毛
    c.line(F.cp(SKULL[:9]), w=outline, layer=layer, taper=(0.0, 0.6), light=0.45)
    c.line(F.cp(SKULL[10:] + [SKULL[0]]), w=outline * 0.8, layer=layer, taper=(0.5, 0.0))
    # 鼻、唇线、眉
    c.add(c.ellipse(*F.p(186, -16), 13 * F.scale, 10 * F.scale, F.angle(-20)), layer)
    c.line(F.cp([(190, 8), (120, 16), (60, 24), (0, 34), (-40, 44)]), w=4.2, layer=layer, taper=(0.1, 0.4))
    c.line(F.cp([(60, -50), (30, -66), (-10, -70)]), w=4.5 if snarl else 3.0, layer=layer, taper=0.3)
    if snarl:                                                                    # 龇牙时口鼻上的褶
        for k in range(3):
            c.line(F.cp([(120 - 18 * k, -28 + 2 * k), (110 - 18 * k, -10), (118 - 18 * k, 8)]), w=2.4, layer=layer, taper=0.3)
    # 眼：细长、竖瞳，画进深渊
    ex, ey = F.p(*EYE)
    eye_m = c.mask(spline(F([(-22, 2, 'c'), (0, -10), (26, -6, 'c'), (0, 8)]), closed=True))
    c.clear(eye_m, layers=(layer,))
    c.add(eye_m * 0.9, eye_layer)
    c.add(c.ellipse(ex + 2 * F.scale, ey - 1 * F.scale, 2.6 * F.scale, 7 * F.scale, F.angle(10)), layer)
    c.stroke(spline(F([(-24, 2), (0, -11), (28, -6)])), 3.6, layer=layer, taper=0.2)
    # 鳃裂：颈侧三道弯弧，画进深渊
    if gills:
        for k in range(3):
            pts = F([(-128 + 20 * k, -10 + 6 * k), (-142 + 20 * k, 30 + 6 * k), (-134 + 20 * k, 70 + 6 * k)])
            g = c.stroke(spline(pts), 7 * F.scale, add=False, taper=0.3)
            c.add(g, layer)
            c.add(c.stroke(spline(pts), 11 * F.scale, add=False, taper=0.3), eye_layer)
    if teeth:
        fangs(c, F, J, jaw, layer=layer)
    return np.maximum(np.maximum(skull_m, jaw_m), ear_m)


def fangs(c, F, J, jaw, layer='ink'):
    """上下两排牙：犬齿长而弯，其余小三角；嘴闭着时只露犬齿。"""
    top = [(176, 12, 8, 14), (152, 14, 9, 14), (126, 16, 12, 48), (100, 18, 9, 14), (76, 22, 9, 12)]
    bot = [(160, 9, 12), (140, 12, 40), (118, 8, 12), (96, 8, 12), (74, 8, 10)]
    show = jaw > 4
    for x, y, w, ln in top:
        if not show and ln < 30:
            continue
        pts = F([(x - w, y), (x + w, y), (x + w * 0.2, y + ln)])
        c.clear(c.mask(pts), layers=(layer,))
        c.stroke(np.vstack([pts, pts[:1]]), 2.4, layer=layer, taper=0)
    yj = 24 - HINGE[1]
    for x, w, ln in bot:
        if not show and ln < 30:
            continue
        xj = x - HINGE[0]
        pts = J([(xj - w, yj), (xj + w, yj), (xj - w * 0.2, yj - ln)])
        c.clear(c.mask(pts), layers=(layer,))
        c.stroke(np.vstack([pts, pts[:1]]), 2.4, layer=layer, taper=0)


def fang(c, F, length=200, width=60, curve=0.25, layer='ink', tone=0.25, root=True):
    """一颗大犬齿（獠牙）：根在局部原点，尖指向 +y，向 +x 弯。返回遮罩。"""
    L, W = length, width
    pts = [(-W / 2, 0), (-W * 0.45, L * 0.35), (-W * 0.25 + curve * L * 0.3, L * 0.7), (curve * L * 0.45, L, 'c'),
           (W * 0.3 + curve * L * 0.32, L * 0.62), (W * 0.48, L * 0.3), (W / 2, 0)]
    m = c.mask(spline(F(pts), closed=True))
    lam = c.relief(m, radius=W * 0.4 * F.scale, blur=2)
    c.engrave(m, np.clip(tone + 0.4 - 0.6 * lam, 0, 1), [c.ph_axis(F.p(0, 0), F.p(curve * L * 0.45, L), bow=-8, half=W / 2 * F.scale),
                                                       c.ph_lin(F.angle(70))], layer=layer, spacing=6.5)
    c.outline(F.cp(pts), w=STRUCT, layer=layer)
    c.erase(c.line(F.cp([(-W * 0.22, L * 0.12), (-W * 0.18, L * 0.45), (curve * L * 0.15, L * 0.75)]), w=5, add=False, taper=0.4), layer)
    return m


# ---------------------------------------------------------------- 狼身剪影

# 面朝 +x，地面 y=0，体长约 600。剪影用实墨 + 刻白的毛线，月光下的轮廓才清楚
BODY = {
    'howl': [(-250, -10), (-215, -60), (-195, -170), (-140, -262), (-80, -330), (-50, -395), (-45, -460), (-61, -582, 'c'),
             (-7, -518), (40, -538), (114, -592), (182, -652, 'c'), (179, -615), (128, -565), (77, -504), (75, -430), (92, -360),
             (104, -250), (86, -130), (104, -4, 'c'), (52, 0), (40, -130), (-30, -140), (-70, -70), (-46, -4, 'c'),
             (-140, -4)],
    'leap': [(-300, -190), (-240, -250), (-120, -268), (0, -285), (90, -318), (150, -355), (195, -372), (205, -418, 'c'),
             (232, -372), (300, -345), (352, -326, 'c'), (305, -310), (262, -306), (300, -290), (330, -282, 'c'), (262, -262),
             (290, -252), (420, -236), (442, -222, 'c'), (410, -200), (280, -212), (255, -198), (350, -140), (366, -116, 'c'),
             (322, -118), (205, -172), (120, -175), (0, -185), (-100, -185), (-170, -170), (-280, -110), (-380, -70),
             (-398, -62, 'c'), (-370, -90), (-250, -150), (-330, -130), (-450, -118), (-466, -112, 'c'), (-440, -140),
             (-320, -170)],
    'prowl': [(-300, -40), (-290, -190), (-240, -255), (-120, -262), (0, -250), (110, -268), (180, -262), (215, -292),
              (240, -248), (300, -205), (360, -170, 'c'), (310, -150), (260, -140), (210, -130), (190, -70),
              (205, 0, 'c'), (160, 0), (150, -60), (120, 0, 'c'), (80, 0), (100, -110), (40, -150), (-80, -150),
              (-170, -140), (-190, -60), (-160, 0, 'c'), (-205, 0), (-230, -80), (-250, 0, 'c'), (-290, 0), (-275, -90)],
}
TAIL = {
    'howl': [(-215, -40), (-300, -16), (-370, 0), (-420, 6)],
    'leap': [(-300, -190), (-400, -180), (-480, -150), (-550, -130)],
    'prowl': [(-290, -230), (-370, -210), (-430, -170), (-470, -120)],
}
EARS = {'howl': (-61, -582, 0), 'leap': (205, -418, 0), 'prowl': (215, -292, 0)}
EYES = {'howl': (42, -540), 'leap': (268, -345), 'prowl': (276, -205)}
GILLS = {'howl': (14, -460), 'leap': (215, -320), 'prowl': (200, -215)}


def wolf_body(c, F, pose='howl', layer='ink', tone=0.97, seed=0, eye=True, gills=True, rim=True):
    """狼的全身剪影（带鳞的背脊、颈侧鳃裂）。tone 高 = 几乎实墨；rim=True 时迎光一侧刻一道亮边。返回遮罩。"""
    pts = BODY[pose]
    m = c.mask(spline(F(pts), closed=True))
    tail = c.stroke(spline(F(TAIL[pose])), 70 * F.scale, add=False, taper=(0.0, 0.85))
    m = np.maximum(m, tail)
    ex, ey, _ = EARS[pose]                                                    # 耳朵：轮廓里那个尖角，再补一个尖三角
    m = np.maximum(m, c.mask(F([(ex - 30, ey + 50), (ex, ey - 12), (ex + 26, ey + 46)])))
    c.engrave(m, tone, [c.ph_lin(F.angle(80), seed=seed + 1), c.ph_lin(F.angle(20), seed=seed + 2)], layer=layer, spacing=7, swell=1.4)
    if rim:                                                                   # 迎光（左上）一侧的亮边：毛的白线
        inner = c.inside(m)
        lit = np.clip(c.shadow_of(m, 10 * F.scale, 10 * F.scale, blur=2), 0, 1)
        edge = m * (1 - lit) * (inner < 14 * F.scale)
        c.erase(edge * c.lines(c.ph_lin(F.angle(60), seed=seed + 3), 5, 2.2) * 0.9, layer)
        fringe(c, F(spline(pts[1:6])), -10, length=18 * F.scale, step=7 * F.scale, layer=layer, seed=seed + 4)
    # 背脊上的鳞：刻白的弧
    d, _ = scale_field(c, 22 * F.scale, F.angle(180), seed=seed + 5)
    back = m * (c.inside(m) > 6) * (c.inside(m) < 40 * F.scale)
    c.erase(_line_cov(d, 1.8) * back * 0.8, layer)
    if eye:
        hx, hy = EYES[pose]
        c.add(c.ellipse(*F.p(hx, hy), 9 * F.scale, 4 * F.scale, F.angle(-20)), 'abyss')
        c.erase(c.ellipse(*F.p(hx, hy), 8 * F.scale, 3.5 * F.scale, F.angle(-20)), layer)
    if gills:
        gx, gy = GILLS[pose]
        for k in range(3):
            g = c.line(F.cp([(gx - 16 * k, gy - 20 + 4 * k), (gx - 22 - 16 * k, gy + 10 + 4 * k), (gx - 18 - 16 * k, gy + 40 + 4 * k)]),
                       w=5 * F.scale, add=False, taper=0.3)
            c.add(g, 'abyss')
            c.erase(g * 0.9, layer)
    return m


# ---------------------------------------------------------------- 爪、血

def claw_marks(c, p0, p1, n=3, gap=60, width=34, layer='ink', jag=6, seed=0, curve=0.08):
    """n 道平行的爪痕：两头收尖的黑口子，边上有翻起的毛刺。返回所有口子的遮罩。"""
    rng = np.random.default_rng(seed)
    p0, p1 = np.array(p0, float), np.array(p1, float)
    d = p1 - p0
    L = np.hypot(*d)
    t = d / L
    nrm = np.array([-t[1], t[0]])
    acc = np.zeros((c.h, c.w), np.float32)
    for k in range(n):
        off = (k - (n - 1) / 2) * gap
        q0 = p0 + nrm * off + t * rng.uniform(-0.06, 0.06) * L
        q1 = p1 + nrm * off + t * rng.uniform(-0.1, 0.04) * L
        s = np.linspace(0, 1, 40)
        mid = q0[None] + (q1 - q0)[None] * s[:, None] + nrm[None] * (curve * L * np.sin(np.pi * s))[:, None]
        wid = width * np.sin(np.pi * s) ** 0.6 * rng.uniform(0.8, 1.1)
        jl = rng.uniform(-jag, jag, 40) * np.sin(np.pi * s)
        jr = rng.uniform(-jag, jag, 40) * np.sin(np.pi * s)
        left = mid + nrm[None] * (wid / 2 + jl)[:, None]
        right = mid - nrm[None] * (wid / 2 + jr)[:, None]
        m = c.mask(np.vstack([left, right[::-1]]))
        c.fill(m, layer=layer)
        c.stroke(left, 2.4, layer=layer, taper=0.2)
        for i in range(4, 36, 3):                                         # 毛刺：口子边上翻起的短线
            if rng.random() < 0.5:
                side = left if rng.random() < 0.5 else right
                sd = 1 if side is left else -1
                a = side[i]
                c.line([tuple(a), tuple(a + nrm * sd * rng.uniform(6, 16) + t * rng.uniform(-6, 10))], w=1.8, layer=layer, taper=(0, 0.8))
        acc = np.maximum(acc, m)
    return acc


def drop_pts(x, y, r, rot=0.0):
    """血滴（尖朝上）的控制点，中心在圆的部分。"""
    F = Frame((x, y), rot=rot)
    return F.cp([(0, -2.3 * r, 'c'), (0.45 * r, -1.0 * r), (r, 0.15 * r), (0.7 * r, 0.85 * r), (0, r * 1.02),
                 (-0.7 * r, 0.85 * r), (-r, 0.15 * r), (-0.45 * r, -1.0 * r)])


def drop(c, x, y, r, layer='ink', rot=0.0, shine=True):
    """一滴血：实墨，左上一点高光。"""
    pts = drop_pts(x, y, r, rot)
    m = c.mask(spline(pts, closed=True))
    c.fill(m, layer=layer)
    if shine and r > 6:
        c.erase(c.ellipse(x - 0.38 * r, y - 0.1 * r, 0.18 * r, 0.34 * r, 25), layer)
    return m


def drip(c, pts, w=16, layer='ink', bead=True):
    """顺着表面往下淌的血：一条变宽的线，末端一颗圆珠。"""
    pts = np.asarray(spline(pts))
    m = c.stroke(pts, w, layer=layer, taper=(0.6, 0.0))
    if bead:
        x, y = pts[-1]
        c.add(c.ellipse(x, y + w * 0.3, w * 0.85, w), layer)
    return m


def splatter(c, x, y, r, n=14, seed=0, layer='ink'):
    """溅开的血点：一团实墨加四周的小点。"""
    rng = np.random.default_rng(seed)
    a = np.linspace(0, 2 * math.pi, 26, endpoint=False)
    rr = r * (1 + 0.25 * rng.standard_normal(26).clip(-1.5, 1.5))
    c.fill(c.mask(spline(np.stack([x + rr * np.cos(a), y + rr * np.sin(a) * 0.8], 1), closed=True)), layer=layer)
    pts, rad = [], []
    for _ in range(n):
        ang, d = rng.uniform(0, 2 * math.pi), r * rng.uniform(1.3, 2.8)
        pts.append((x + d * math.cos(ang), y + d * math.sin(ang) * 0.8))
        rad.append(r * rng.uniform(0.06, 0.18))
    c.dots(pts, np.array(rad), layer)


def paw_print(c, x, y, s=1.0, rot=0.0, layer='ink', claws=True):
    """狼爪印：掌垫 + 四个趾垫 + 爪尖。朝 rot（0 = 朝上）。"""
    F = Frame((x, y), rot=rot, scale=s)
    pad = c.mask(spline(F([(-34, 10), (-20, -18), (20, -18), (34, 10), (16, 30), (0, 22), (-16, 30)]), closed=True))
    c.fill(pad, layer=layer)
    for tx, ty, rr in ((-40, -40, 14), (-14, -64, 15), (14, -64, 15), (40, -40, 14)):
        px, py = F.p(tx, ty)
        c.add(c.ellipse(px, py, rr * s, rr * 1.25 * s, F.angle(math.degrees(math.atan2(tx, -ty)))), layer)
        if claws:
            q = F.p(tx * 1.25, ty - 26)
            c.line([F.p(tx * 1.1, ty - 14), q], w=4 * s, layer=layer, taper=(0.0, 0.9))


# ---------------------------------------------------------------- 银链

def chain(c, pts, link=40, w=5.5, layer='ink', tone=0.15, step=None):
    """一串链环：沿折线每隔 step 一环，正面环（椭圆圈）和侧面环（扁条）交替。返回遮罩。"""
    path = np.asarray(spline(pts))
    s = np.concatenate([[0], np.cumsum(np.hypot(*np.diff(path, axis=0).T))])
    step = step or link * 1.5
    acc = np.zeros((c.h, c.w), np.float32)
    k = 0
    for d in np.arange(link * 0.5, s[-1] - link * 0.4, step / 2):
        i = int(np.searchsorted(s, d))
        i = min(max(i, 1), len(path) - 2)
        x, y = path[i]
        tx, ty = path[i + 1] - path[i - 1]
        ang = math.degrees(math.atan2(ty, tx))
        if k % 2 == 0:
            outer = c.ellipse(x, y, link, link * 0.62, ang)
            hole = c.ellipse(x, y, link * 0.62, link * 0.26, ang)
            m = outer * (1 - hole)
            c.clear(m, layers=(layer,))
            lam = c.relief(m, radius=link * 0.2, blur=1)
            c.engrave(m, np.clip(tone + 0.5 - 0.6 * lam, 0, 1), [c.ph_lin(ang + 70)], layer=layer, spacing=5.5, wmax=2.0, grain=0)
            c.stroke(ellipse_pts(x, y, link, link * 0.62, ang), w, layer=layer, closed=True)
            c.stroke(ellipse_pts(x, y, link * 0.62, link * 0.26, ang), w * 0.6, layer=layer, closed=True)
            c.erase(c.stroke(ellipse_pts(x, y, link * 0.82, link * 0.46, ang, a0=200, a1=250), w * 0.6, add=False), layer)
        else:
            m = c.ellipse(x, y, link * 1.0, link * 0.2, ang)
            c.clear(m, layers=(layer,))
            c.engrave(m, tone + 0.3, [c.ph_lin(ang + 90)], layer=layer, spacing=5, wmax=1.8, grain=0)
            c.stroke(ellipse_pts(x, y, link * 1.0, link * 0.2, ang), w * 0.8, layer=layer, closed=True)
        acc = np.maximum(acc, m)
        k += 1
    return acc


# ---------------------------------------------------------------- 手

def claw_hand(c, F, claws=1.0, layer='ink', tone=0.5, curl=0.0, scaled=True, seed=0, fingers=4, sleeve=True):
    """张开的手（手背朝观者），手指末端长出弯爪。局部坐标：手腕在 (-220, 0)，指尖朝 +x；claws 0..1 = 爪的长度。
    curl：手指往掌心弯的程度（0 张开，1 握拳）。返回遮罩。"""
    palm = [(-150, -60), (-60, -92), (40, -100), (70, -40), (76, 30), (50, 92), (-60, 86), (-150, 56)]
    m = c.mask(spline(F(palm), closed=True))
    lam = c.relief(m, radius=60 * F.scale, blur=3)
    c.engrave(m, np.clip(tone + 0.15 - 0.55 * lam + 0.2 * c.grad(*F.p(60, 0), *F.p(-150, 0)), 0, 1),
              [c.ph_axis(F.p(-150, 0), F.p(70, 0), bow=-14, half=100 * F.scale, seed=seed + 1), c.ph_lin(F.angle(60))],
              layer=layer, spacing=7.5)
    if scaled:
        sm = m * smoothstep(0.35, 0.75, c.grad(*F.p(60, 0), *F.p(-150, 0)))
        scales(c, sm, size=24 * F.scale, angle=F.angle(180), tone=0.2, layer=layer, seed=seed + 2)
    c.outline(F.cp(palm), w=OUTLINE * 0.85, layer=layer)
    for k in range(3):                                                        # 手背的筋
        y = -50 + 45 * k
        c.line(F.cp([(-120, y * 0.7), (-50, y * 0.95), (40, y)]), w=2.4, layer=layer, taper=0.4)
    spec = [(-70, 26, 150, -22), (-24, 30, 170, -6), (24, 30, 166, 8), (70, 26, 140, 22)][:fingers]
    for i, (fy, fw, ln, spread) in enumerate(spec):
        ln = ln * (1 - 0.35 * curl)
        G = Frame(F.p(60, fy), rot=F.angle(spread + 40 * curl * (1 if fy > 0 else 0.6)), scale=F.scale)
        hw = fw / 2
        pts = [(-8, -hw), (ln * 0.45, -hw - 1), (ln * 0.85, -hw * 0.7), (ln, 0, 'c'), (ln * 0.85, hw * 0.7), (ln * 0.45, hw + 1), (-8, hw)]
        fm = c.mask(spline(G(pts), closed=True))
        lam = c.relief(fm, radius=hw * F.scale, blur=1)
        c.engrave(fm, np.clip(tone + 0.15 - 0.6 * lam, 0, 1), [c.ph_axis(G.p(0, 0), G.p(ln, 0), bow=-5, half=hw * F.scale, seed=seed + 3 + i)],
                  layer=layer, spacing=6.5, wmax=2.4)
        c.outline(G.cp(pts), w=STRUCT, layer=layer)
        for j in (0.42, 0.75):
            c.line(G.cp([(ln * j - 3, -hw * 0.75), (ln * j + 3, 0), (ln * j - 3, hw * 0.75)]), w=2.0, layer=layer, taper=0.3)
        if claws > 0:
            cl = 70 * claws
            K = Frame(G.p(ln - 6, 0), rot=G.angle(0) + 0, scale=F.scale)
            cpts = [(0, -hw * 0.55), (cl * 0.5, -hw * 0.5), (cl, hw * 0.6, 'c'), (cl * 0.45, hw * 0.25), (0, hw * 0.5)]
            km = c.mask(spline(K(cpts), closed=True))
            c.fill(km, layer=layer)
            c.erase(c.line(K.cp([(cl * 0.1, -hw * 0.2), (cl * 0.55, -hw * 0.2), (cl * 0.85, hw * 0.3)]), w=2.2, add=False, taper=0.4), layer)
            m = np.maximum(m, km)
        m = np.maximum(m, fm)
    if sleeve:                                                                 # 粗呢袖口：手腕后面一截
        sl = [(-400, -100), (-140, -80), (-130, 0), (-140, 82), (-400, 110)]
        sm = c.mask(spline(F(sl), closed=True))
        c.engrave(sm, 0.7, [c.ph_axis(F.p(-400, 0), F.p(-140, 0), bow=-20, half=100 * F.scale), c.ph_lin(F.angle(70))],
                  layer=layer, spacing=8, swell=1.0)
        c.outline(F.cp(sl), w=OUTLINE, layer=layer)
        c.line(F.cp([(-146, -80), (-136, 0), (-146, 82)]), w=STRUCT, layer=layer)
        m = np.maximum(m, sm)
    return m
