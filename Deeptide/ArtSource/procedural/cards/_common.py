"""几张卡图共用的画法：触须、眼睛、小人剪影、放射光。"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, spline


def tentacle(c, ctrl, width, layer='ink', tone=0.55, suckers=True, taper=0.9, outline=4.0, hatch_angle=None, grain=0.0):
    """一条触须：沿控制点的中心线，从 width 收到尖；外侧两条轮廓，内侧一排吸盘。返回遮罩。"""
    center = spline(ctrl)
    u = np.linspace(0, 1, len(center))
    m = c.stroke(center, width, add=False, taper=0, profile=lambda uu: 1 - taper * uu)
    t = np.gradient(center, axis=0)
    t /= np.maximum(np.hypot(t[:, 0], t[:, 1]), 1e-6)[:, None]
    nn = np.stack([-t[:, 1], t[:, 0]], 1)
    half = width / 2 * (1 - taper * u)
    if hatch_angle is None:
        d = center[-1] - center[0]
        hatch_angle = math.degrees(math.atan2(d[1], d[0])) + 90
    lam = c.relief(m, radius=max(width * 0.35, 4), blur=1.5)
    c.engrave(m, np.clip(tone + 0.35 * (1 - lam) - 0.3, 0, 1) if layer == 'ink' else np.full_like(m, tone),
              [c.ph_lin(hatch_angle, wobble=0.4), c.ph_lin(hatch_angle - 70)], layer=layer, spacing=7, wmax=2.4, grain=grain)
    for sd in (-1, 1):
        c.stroke(center + nn * half[:, None] * sd, outline * (1.25 if sd > 0 else 0.85), layer=layer, taper=(0.0, 0.2))
    if suckers:
        step = max(int(width / 2.2), 8)
        for i in range(step, len(center) - step, step):
            p = center[i] - nn[i] * half[i] * 0.45
            r = max(half[i] * 0.36, 2.0)
            if r < 2.5:
                break
            c.clear(c.ellipse(p[0], p[1], r, r), layers=(layer,))
            c.stroke(ellipse_pts(p[0], p[1], r, r), 1.8, layer=layer, closed=True)
            c.add(c.ellipse(p[0], p[1], r * 0.35, r * 0.35), layer)
    return m


def eye(c, x, y, w, layer='ink', look=0.0, h=0.5, slit=False, lash=False, width=4.2):
    """一只杏仁眼：上下眼睑、带放射纹的虹膜、瞳孔。"""
    hh = w * h
    upper = spline([(x - w, y, 'c'), (x - w * 0.4, y - hh), (x + w * 0.4, y - hh), (x + w, y, 'c')])
    lower = spline([(x + w, y, 'c'), (x + w * 0.4, y + hh * 0.8), (x - w * 0.4, y + hh * 0.8), (x - w, y, 'c')])
    m = c.mask(np.vstack([upper, lower]))
    c.clear(m, layers=(layer,))
    ir = hh * 0.85
    ix = x + look * w * 0.45
    r = np.hypot(c.xx - ix, c.yy - y)
    c.add(c.ellipse(ix, y, ir, ir) * m * c.lines(np.arctan2(c.yy - y, c.xx - ix) * max(ir / 2.2, 6), 4.5, 1.6)
          * (r > ir * 0.45), layer)
    if slit:
        c.add(c.ellipse(ix, y, ir * 0.3, ir * 0.9) * m, layer)
    else:
        c.add(c.ellipse(ix, y, ir * 0.45, ir * 0.45) * m, layer)
    c.add(c.stroke(ellipse_pts(ix, y, ir, ir), 2.2, closed=True, add=False) * m, layer)
    c.stroke(upper, width, layer=layer, taper=0.1)
    c.stroke(lower, width * 0.6, layer=layer, taper=0.1)
    if lash:
        for t in np.linspace(0.15, 0.85, 7):
            p = upper[int(t * (len(upper) - 1))]
            a = math.atan2(p[1] - (y + hh), p[0] - x)
            c.stroke([tuple(p), (p[0] + w * 0.25 * math.cos(a), p[1] + w * 0.25 * math.sin(a))], 2.4, layer=layer,
                     taper=(0.0, 0.8))
    return m


def person(c, x, y, h, tone=0.95, hat=True, lean=0.0, arms=None, layer='ink'):
    """远处的小人剪影（风衣 + 帽），脚底在 (x, y)，高 h。arms：每只手 (肩高比例, 角度度) 的列表，None = 垂着。"""
    F = Frame((x, y), rot=lean, scale=h / 340)
    body = [(-34, 0), (-40, -120), (-50, -200), (-34, -240), (34, -240), (50, -200), (40, -120), (34, 0)]
    m = c.mask(spline(F(body), closed=True))
    m = np.maximum(m, c.ellipse(*F.p(0, -270), 24 * h / 340, 28 * h / 340))
    if hat:
        m = np.maximum(m, c.ellipse(*F.p(0, -290), 52 * h / 340, 10 * h / 340))
        m = np.maximum(m, c.mask(F([(-24, -290), (-20, -334), (20, -334), (24, -290)])))
    for frac, ang in (arms or []):
        a = math.radians(ang)
        sx = 40 if math.cos(a) > 0 else -40
        p0 = F.p(sx, -240 + 40 * (1 - frac))
        p1 = (p0[0] + h * 0.42 * math.cos(math.radians(ang + lean)), p0[1] + h * 0.42 * math.sin(math.radians(ang + lean)))
        m = np.maximum(m, c.stroke([p0, p1], h * 0.07, add=False, taper=(0.0, 0.3)))
    c.fill(m, value=tone, layer=layer)
    return m


def burst(c, x, y, r0, r1, n=48, width=2.6, erase=False, sy=1.0, jitter=0.15, seed=0, layer='ink'):
    """放射光芒：n 道从 r0 到 r1 的短线（erase=True 时是在暗底上刻出的白线）。"""
    rng = np.random.default_rng(seed)
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-jitter, jitter) * 2 * math.pi / n
        rr1 = r1 * (1 + rng.uniform(-0.25, 0.25)) * (1.25 if k % 4 == 0 else 1)
        cov = c.stroke([(x + r0 * math.cos(a), y + r0 * sy * math.sin(a)), (x + rr1 * math.cos(a), y + rr1 * sy * math.sin(a))],
                       width, add=False, taper=(0.15, 0.9))
        if erase:
            c.erase(cov, layer)
        else:
            c.add(cov, layer)


def wraith(c, x, y, h, lean=0.0, flip=False, tone=0.95, layer='ink'):
    """往后缩的影子：瘦长的人形，向后仰（lean 度），一只手臂抬起挡在脸前、手指张开，另一只向后伸。
    脚底在 (x, y)，高 h；flip=True 时朝左缩（脸朝右）。返回遮罩。"""
    s = h / 400
    sd = -1 if flip else 1
    F = Frame((x, y), rot=lean * sd, scale=s)

    def X(pts):
        return [(sd * p[0], p[1]) for p in pts]

    torso = X([(-26, -210), (-34, -300), (-20, -330), (22, -330), (34, -300), (24, -210), (14, -120), (-14, -120)])
    m = c.mask(spline(F(torso), closed=True))
    m = np.maximum(m, c.ellipse(*F.p(sd * -10, -362), 20 * s, 26 * s, rot=lean * sd))
    limbs = [
        X([(-12, -128), (-30, -60), (-40, 0)]),                    # 腿
        X([(12, -128), (30, -60), (54, 0)]),
        X([(-24, -318), (-60, -330), (-40, -380), (-20, -392)]),   # 抬起挡脸的手臂（肘弯在外）
        X([(26, -316), (70, -280), (120, -250)]),                  # 向后伸的手臂
    ]
    for k, limb in enumerate(limbs):
        w = (22 if k < 2 else 15) * s
        m = np.maximum(m, c.stroke(spline(F(limb)), w, add=False, taper=(0.0, 0.35)))
    # 挡脸那只手的手指、向后那只手的手指：张开的细线
    for root, base_ang in ((X([(-20, -392)])[0], -90), (X([(120, -250)])[0], 20)):
        for j in range(-2, 3):
            a = math.radians(F.angle(base_ang * sd + (180 if flip and base_ang == 20 else 0) + j * 18))
            p0 = F.p(*root)
            ln = 30 * s
            m = np.maximum(m, c.stroke([p0, (p0[0] + ln * math.cos(a), p0[1] + ln * math.sin(a))], 5 * s, add=False,
                                       taper=(0.0, 0.8)))
    c.clear(np.clip(c.shadow_of(m, 0, 0, blur=3 * s + 1) * 2.2, 0, 1), layers=(layer,))
    c.engrave(m, tone, [c.ph_lin(F.angle(85), seed=7), c.ph_lin(F.angle(20), seed=8)], layer=layer, spacing=7, swell=1.3)
    return m
