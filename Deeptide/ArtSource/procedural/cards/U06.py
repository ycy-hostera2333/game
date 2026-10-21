"""U06 深渊献礼：把手伸进漆黑的水面，水下有手回握（深渊遮罩）（策划/10 §4.6）。

侧面看一片漆黑的水：水面是画面中间一条线。一只手臂从左上伸下来，从手腕往下没进水里；
水面下，从深处伸上来一只苍白细长的手，握住了这只手的手腕——这只水下的手走深渊遮罩，清醒面里也是青的。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, smoothstep, spline

NAME = '深渊献礼'

SURFACE = 420
WRIST = (640, 440)


def draw(c):
    rng = np.random.default_rng(6)
    wx, wy = WRIST
    above = c.rect(0, 0, c.w, SURFACE)
    below = 1 - above
    # ------------------------------------------------------------ 水面以上：暗的屋里 / 水面以下：更黑
    c.engrave(above, 0.55 + 0.2 * c.grad(0, 0, 1248, 400) + 0.05 * c.noise(80, 1),
              [c.ph_lin(-30, wobble=2, seed=2), c.ph_lin(50, seed=3)], spacing=10)
    c.engrave(below, 0.62 + 0.3 * smoothstep(0, 1, c.radial(wx, wy + 120, 60, 520)), [c.ph_wave(0, 3, 160, wobble=2, seed=4), c.ph_lin(30, seed=5),
                                                               c.ph_lin(-40, seed=6)], spacing=8, swell=1.3)
    # 水面：一条亮线 + 以入水点为中心的同心涟漪（透视的扁椭圆）
    c.erase(c.stroke([(0, SURFACE), (c.w, SURFACE)], 4, add=False, taper=0))
    for k in range(1, 6):
        e = ellipse_pts(wx - 10, SURFACE, 60 + k * 90, 8 + k * 6, a0=180, a1=360)
        c.erase(c.stroke(e, 3.0 - k * 0.3, add=False, taper=0.2))
        e2 = ellipse_pts(wx - 10, SURFACE, 60 + k * 90, 8 + k * 6, a0=0, a1=180)
        c.stroke(e2, 2.4, taper=0.2)

    # ------------------------------------------------------------ 手臂：从左上伸下来
    A = Frame((wx, wy), rot=-118)                                            # +x 指向肩膀（左上）
    sleeve = [(300, -66), (800, -120), (800, 120), (300, 70)]
    cuff = [(250, -60), (310, -66), (310, 70), (250, 64)]
    forearm = [(-40, -40), (100, -46), (255, -54), (255, 58), (100, 50), (-40, 40)]
    for pts, tone in ((forearm, 0.35), (cuff, 0.15), (sleeve, 0.95)):
        m = c.mask(spline(A(pts), closed=True)) * above
        lam = c.relief(m, radius=30, blur=2)
        c.engrave(m, np.clip(tone + 0.3 * (1 - lam) - 0.15, 0, 1), [c.ph_lin(A.angle(90)), c.ph_lin(A.angle(20)), c.ph_lin(A.angle(-40))],
                  spacing=8, swell=1)
        for a, b in ((pts[0], pts[1]), (pts[3], pts[2])):
            c.add(c.stroke(A([a, b]), STRUCT + 1, add=False, taper=0) * above)
    # 水下的那截手臂和手：被水折了一下，只剩淡淡的外形
    sub = Frame((wx - 20, wy), rot=-100)
    hand = [(-170, -36), (-60, -40), (20, -40), (20, 40), (-60, 44), (-170, 40), (-200, 0)]
    m = c.mask(spline(sub(hand), closed=True)) * below
    c.clear(m, layers=('ink',))
    c.engrave(m, 0.55, [c.ph_lin(0, wobble=0.5)], spacing=7, wmax=2.2)
    c.add(c.stroke(spline(sub(hand), closed=True), 3, closed=True, add=False) * below)
    for k in range(4):                                                       # 指尖
        c.add(c.stroke(spline(sub([(-170, -30 + k * 20), (-215, -34 + k * 22)])), 3, add=False, taper=0.3) * below)

    # ------------------------------------------------------------ 水下伸上来的手：细长、苍白，握住手腕（深渊遮罩）
    U = Frame((wx - 14, wy + 90), rot=-80)                                  # +x 指向上（手指尖），原点在手腕上的握点
    arm = spline(U([(-520, -40), (-360, -20), (-200, -6), (-60, 0)]))
    c.stroke(arm, 34, layer='abyss', taper=(0.0, 0.0), profile=lambda u: 0.8 + 0.3 * u)
    palm = [(-80, -30), (0, -40), (30, -20), (30, 30), (0, 44), (-80, 30)]
    c.fill(c.mask(spline(U(palm), closed=True)), layer='abyss', value=0.85)
    for k, (oy, ln) in enumerate(((-50, 96), (-24, 112), (6, 112), (34, 96))):   # 四根细长的手指绕过手腕
        f = spline(U([(20, oy * 0.6), (60, oy), (ln * 0.8, oy + 30), (ln, oy + 70 + k * 4)]))
        c.stroke(f, 15, layer='abyss', taper=(0.0, 0.6))
        for t in (0.45, 0.75):
            p = f[int(t * (len(f) - 1))]
            c.erase(c.ellipse(p[0], p[1], 3, 3), 'abyss')
    c.stroke(spline(U([(-20, 44), (30, 70), (70, 60)])), 14, layer='abyss', taper=(0.0, 0.6))   # 拇指
    c.layers['abyss'] *= below * smoothstep(SURFACE + 2, SURFACE + 14, c.yy)
    for _ in range(26):                                                      # 一串气泡往上
        x, y = wx + rng.normal(0, 60), rng.uniform(SURFACE + 20, 860)
        r = rng.uniform(3, 9)
        c.erase(c.ellipse(x, y, r + 2, r + 2))
        c.stroke(ellipse_pts(x, y, r, r), 1.8, closed=True)
