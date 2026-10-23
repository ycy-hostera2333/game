"""插图共用的线宽和画法（策划/10 §3）。

插图的工作画布等于导出尺寸（1920 × 1280），游戏里显示成 880–960 宽，大约缩小一半；卡图是 1248 宽的画布缩到 300 宽以下。
所以线宽按「显示出来和卡图在选牌面板里差不多粗」换算：外轮廓 6.5、结构线 4、排线 2.2、间距 7.5（显示约 3–4 像素一条）。
"""
import math

import numpy as np

from etch import Frame, ellipse_pts, poly, smoothstep, spline

OUT = 6.5          # 外轮廓
STRUCT = 4.0       # 结构线
HATCH = 2.2        # 排线线宽
GAP = 7.5          # 排线间距


def hatch(c, mask, tone, phases, spacing=GAP, wmax=HATCH + 0.5, **kw):
    """插图用的排线：默认间距和线宽换成插图的尺寸。"""
    return c.engrave(mask, tone, phases, spacing=spacing, wmax=wmax, **kw)


def ray_r0(r0, spacing=9.0):
    """放射排线的 r0（ph_ray 的相位 = 角度 × r0）：取到最近的「绕一圈正好整数条线」，左侧水平方向上就不会有一道接缝。"""
    n = round(2 * math.pi * r0 / spacing)
    return n * spacing / (2 * math.pi)


def night_sky(c, y1, top=0.72, bottom=0.42, seed=1, x0=0, x1=None):
    """夜空：上浓下淡的波浪横排线，暗处加一层斜线。返回遮罩。"""
    x1 = c.w if x1 is None else x1
    m = c.rect(x0, 0, x1, y1)
    tone = bottom + (top - bottom) * c.grad(0, y1, 0, 0) + 0.08 * c.noise(220, seed)
    hatch(c, m, np.clip(tone, 0, 1), [c.ph_wave(-2, 9, 700, wobble=5, seed=seed + 1), c.ph_lin(35, wobble=3, seed=seed + 2)],
          spacing=9, bands=[(0.05, 0.6), (0.5, 0.95)], solid=None)
    return m


def moon(c, x, y, r, halo=2.2):
    """月亮：在夜空里刻出一块留白，外面一圈同心的光晕线。"""
    c.erase(c.ellipse(x, y, r * halo, r * halo) * 0.55)
    c.clear(c.ellipse(x, y, r, r))
    ring = c.ph_ring(x, y)
    halo_m = c.ellipse(x, y, r * halo, r * halo) * (1 - c.ellipse(x, y, r + 6, r + 6))
    c.add(halo_m * c.lines(ring, 14, 1.4) * smoothstep(r * halo, r, ring))
    c.stroke(ellipse_pts(x, y, r, r), 2.5, closed=True)


def sea(c, y0, y1, tone=0.7, seed=3, x0=0, x1=None, glints=60, rng=None):
    """海面：波浪横排线，越近越粗越暗；水面上刻几道反光。返回遮罩。"""
    x1 = c.w if x1 is None else x1
    m = c.rect(x0, y0, x1, y1)
    near = c.grad(0, y0, 0, y1)
    t = np.clip(tone - 0.15 + 0.25 * near + 0.08 * c.noise(90, seed), 0, 1)
    # 透视：相位取对数，地平线处 7 像素一条，越近越疏；波纹是相位上的正弦，越近起伏越大（像素里自动放大）。
    # 插图在游戏里缩小一半显示，线距低于 6 像素会出摩尔纹
    d0 = 150.0
    a = GAP * d0 / 7
    ph = a * np.log(np.maximum(c.yy - y0 + d0, 1) / d0) + 2.2 * np.sin(2 * math.pi * c.xx / 340 + 2.5 * c.noise(120, seed + 1)) \
        + 1.2 * c.noise(40, seed + 3)
    hatch(c, m, t, [ph, c.ph_lin(-8, seed=seed + 2)], swell=1.0)
    rng = rng or np.random.default_rng(seed)
    for _ in range(glints):
        x, y = rng.uniform(x0, x1), rng.uniform(y0 + 4, y1 - 4)
        ln = rng.uniform(20, 80) * (0.5 + (y - y0) / max(y1 - y0, 1))
        c.erase(c.stroke([(x, y), (x + ln, y + rng.uniform(-2, 2))], 2.4, add=False, taper=0.5) * m)
    return m


def rock(c, pts, tone=0.85, light=0.45, seed=11, cracks=()):
    """礁石：浮雕明暗 + 三向排线，几道裂缝，背光一侧的粗轮廓。返回遮罩。"""
    m = c.mask(spline(pts, closed=True))
    lam = c.relief(m, radius=90, blur=6)
    hatch(c, m, np.clip(tone + 0.15 - 0.5 * lam + 0.06 * c.noise(50, seed), 0.3, 1),
          [c.ph_lin(38, seed=seed), c.ph_lin(-32, seed=seed + 1), c.ph_lin(84, seed=seed + 2)], swell=1.2)
    for crack in cracks:
        c.line(crack, w=3.5, taper=0.3)
    c.outline(pts, w=OUT, light=light)
    return m


def investigator(c, F, tone=0.95, lantern_side=None, coat_flare=0.0, hang=(-1, 1)):
    """调查员背影：宽檐帽、竖起的领子、长风衣到小腿，两臂垂在身侧；原点在脚底，高约 350（F.scale 换算到画布）。
    光从左上：右半边（画面右）压暗，加一层交叉线。lantern_side = -1 / 1 时那只手提一盏小灯；hang 是垂着的手臂（另一只由调用方自己画）。
    返回身体遮罩。"""
    s = F.scale
    coat = [(-34, -40), (-50, -110), (-46, -160), (-52, -210), (-62, -238, 'c'), (-30, -256), (-16, -262), (16, -262), (30, -256),
            (62, -238, 'c'), (54, -210), (48, -160), (52, -110), (40 + coat_flare, -40)]
    m = c.mask(spline(F.cp(coat), closed=True))
    lam = c.relief(m, radius=30 * s, blur=2)
    side = 1 - c.half_plane(F.p(0, 0), F.p(0, -400))                            # 1 = 身体右半（背光）
    hatch(c, m, np.clip(tone - 0.4 * lam + 0.08 * side, 0.45, 1),
          [c.ph_lin(F.angle(88), seed=20), c.ph_lin(F.angle(28), seed=21), c.ph_lin(F.angle(-40), seed=22)], swell=1.2)
    c.outline(F.cp(coat), w=STRUCT + 0.8, light=0.45)
    c.line(F.cp([(2, -236), (4, -140), (2, -46)]), w=2.6, taper=0.2)            # 后背的开衩
    c.line(F.cp([(-48, -158), (0, -152), (48, -158)]), w=3.4, taper=0.1)         # 腰带
    for k in (-30, 30):                                                         # 下摆的褶
        c.line(F.cp([(k * 0.7, -130), (k * 0.9, -80), (k, -44)]), w=2.2, taper=0.3)
    for sd in (-1, 1):                                                          # 两条腿和靴子
        leg = c.mask(F([(sd * 17 - 11, -44), (sd * 17 + 11, -44), (sd * 19 + 10, -8), (sd * 19 - 12, -8)]))
        c.fill(leg, value=tone)
        c.fill(c.ellipse(*F.p(sd * 20, -4), 15 * s, 7 * s), value=1.0)
    for sd in hang:                                                             # 手臂：从肩垂下，肘部微弯，手露出袖口
        arm = F.cp([(sd * 56, -232), (sd * 66, -180), (sd * 62, -128)])
        c.line(arm, w=20 * s, taper=(0.0, 0.15))
        c.fill(c.ellipse(*F.p(sd * 62, -118), 9 * s, 11 * s), value=0.85)
    collar = F.cp([(-30, -256), (-26, -286), (0, -280), (26, -286), (30, -256)])  # 竖起的领子
    cm = c.mask(spline(collar, closed=True))
    hatch(c, cm, 0.85, [c.ph_lin(F.angle(70), seed=23)], spacing=7)
    c.outline(collar, w=STRUCT, light=0.3)
    c.fill(c.ellipse(*F.p(0, -294), 20 * s, 22 * s), value=tone)               # 后脑勺、帽
    c.fill(c.ellipse(*F.p(0, -308), 60 * s, 12 * s), value=1.0)
    c.fill(c.mask(F([(-26, -308), (-22, -350), (22, -350), (26, -308)])), value=1.0)
    c.erase(c.stroke(F([(-24, -318), (22, -318)]), 3 * s, add=False) * 0.7)  # 帽带的一道反光
    if lantern_side:
        hx, hy = F.p(lantern_side * 62, -112)
        G = Frame((hx, hy + 60 * F.scale), rot=F.rot, scale=0.32 * F.scale)
        from cards.C02 import lantern
        lantern(c, G)
        c.line([(hx, hy), G.p(0, -350)], w=2.0, taper=0)
    return m


def hooded(c, x, y, h, tone=0.95, lean=0.0, layer='ink', hands=False, rng=None):
    """兜帽长袍的人：脚底在 (x, y)，高 h。袍子下摆拖地，兜帽尖。返回遮罩。"""
    F = Frame((x, y), rot=lean, scale=h / 380)
    # 下摆拖地，肩是两个角，兜帽尖向后倒
    robe = [(-64, 0), (-54, -120), (-48, -228), (-44, -262, 'c'), (-24, -290), (-30, -322), (-12, -356), (10, -378, 'c'),
            (22, -344), (32, -318), (26, -290), (44, -262, 'c'), (50, -228), (56, -120), (66, 0)]
    m = c.mask(spline(F.cp(robe), closed=True))
    if tone >= 0.98:                                                          # 逆光的剪影：实墨，不要排线的噪点
        c.fill(m, value=1.0, layer=layer)
    else:
        c.engrave(m, tone, [c.ph_lin(F.angle(88), seed=30), c.ph_lin(F.angle(30), seed=31)], spacing=7.5, wmax=2.6, layer=layer,
                  swell=1.3)
    c.outline(F.cp(robe), w=STRUCT, light=0.3, layer=layer)
    c.fill(c.ellipse(*F.p(-2, -316), 12 * F.scale, 17 * F.scale), value=1.0, layer=layer)   # 兜帽里是黑的
    for k in (-26, 0, 28):                                                    # 袍子的褶
        c.line(F.cp([(k * 0.6, -250), (k * 0.9, -130), (k, -10)]), w=2.2, taper=0.3, layer=layer)
    if hands:
        for sd in (-1, 1):
            c.fill(c.ellipse(*F.p(sd * 18, -196), 9 * F.scale, 7 * F.scale), value=0.0, layer=layer)
            c.stroke(ellipse_pts(*F.p(sd * 18, -196), 9 * F.scale, 7 * F.scale), 1.8, closed=True, layer=layer)
    return m


def flame(c, x, y, w, h, seed=0, layer='ink'):
    """火苗：七八条往上卷、彼此交叠的舌头，每条是一道 S 形的曲边，内部留白，外缘勾线，中间再刻几道细的火舌线。返回遮罩。"""
    rng = np.random.default_rng(seed)
    tongues = []
    n = 8
    for k in range(n):
        u = (k + 0.5) / n * 2 - 1                                            # -1 … 1
        base = x + u * w * 0.42
        hh = h * (1 - 0.55 * abs(u)) * rng.uniform(0.7, 1.05)
        bend = rng.uniform(-0.3, 0.3) * w - u * w * 0.12
        half = w * 0.16 * (1 - 0.3 * abs(u))
        pts = [(base - half, y), (base - half * 0.9 + bend * 0.2, y - hh * 0.35), (base - half * 0.3 + bend * 0.7, y - hh * 0.7),
               (base + bend, y - hh, 'c'), (base + half * 0.2 + bend * 0.55, y - hh * 0.62), (base + half * 0.8 + bend * 0.15, y - hh * 0.3),
               (base + half, y)]
        tongues.append(pts)
    m = np.zeros((c.h, c.w), np.float32)
    for pts in tongues:
        m = np.maximum(m, c.mask(spline(pts, closed=True)))
    c.clear(m, layers=(layer,))
    for pts in tongues:
        c.stroke(spline(pts[:-1]), 2.6, layer=layer, taper=(0.2, 0.3))
        mid = [lerp_pt(pts[1], pts[5], 0.5), lerp_pt(pts[2], pts[4], 0.5), pts[3][:2]]
        c.stroke(spline(mid), 1.6, layer=layer, taper=(0.3, 0.6))
    return m


def lerp_pt(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
