"""C16 点亮提灯：划火柴点灯的手，光圈把雾推开（策划/10 §4.6）。

提灯在画面中间偏左，玻璃罩掀开一半；右边一只手捏着刚划着的火柴凑过去，火柴头一团亮。
以火柴为圆心，一圈亮把四周浓雾推开，雾的边是一圈卷起来的雾团。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, smoothstep, spline
from cards.C02 import lantern
from cards._common import burst

NAME = '点亮提灯'

MATCH = (640, 450)


def draw(c):
    rng = np.random.default_rng(16)
    mx, my = MATCH
    r = np.hypot((c.xx - mx) / 1.25, c.yy - my)

    # ------------------------------------------------------------ 雾：光圈外很暗，光圈里亮
    edge = 300 + 30 * c.noise(50, 1)
    c.engrave(np.ones((c.h, c.w), np.float32), np.clip(0.08 + 0.85 * smoothstep(edge - 120, edge + 80, r) + 0.04 * c.noise(40, 2), 0, 1),
              [c.ph_wave(-4, 7, 300, wobble=3, seed=3), c.ph_ring(mx, my, sx=1.25), c.ph_lin(60, seed=4)], spacing=9.5, swell=1.2)
    # 光圈的边：一圈雾卷
    for a in np.arange(0, 360, 26) + rng.uniform(-6, 6):
        q = math.radians(a)
        rr = 330 + rng.uniform(-20, 30)
        x, y = mx + rr * 1.25 * math.cos(q), my + rr * math.sin(q)
        t = np.linspace(0, 1.4 * math.pi, 60)
        s = rng.uniform(26, 44)
        curl = np.stack([x + s * (1 - t / 5) * np.cos(t + q + math.pi), y + s * (1 - t / 5) * np.sin(t + q + math.pi)], 1)
        c.erase(c.stroke(curl, 5, add=False, taper=(0.2, 0.7)))
        c.stroke(curl + np.array([math.cos(q), math.sin(q)]) * 6, 2.4, taper=(0.3, 0.7))
    burst(c, mx, my, 70, 260, n=40, width=2.4, erase=True, sy=0.8, seed=2)

    # ------------------------------------------------------------ 提灯
    lantern(c, Frame((470, 760), scale=1.15))

    # ------------------------------------------------------------ 手 + 火柴
    H = Frame((mx + 10, my + 10), rot=-12)                                   # 手的坐标：火柴在原点，手在 +x
    stick = [(0, -4), (120, -3), (120, 3), (0, 4)]
    c.fill(c.mask(H(stick)), value=0.35)
    c.stroke(H(stick + stick[:1]), 2.4, taper=0)
    head = c.ellipse(mx + 4, my + 6, 15, 11, rot=-12)
    c.fill(head, value=1.0)
    flame = [(mx - 8, my - 130), (mx + 34, my - 50), (mx + 30, my + 8), (mx, my + 24), (mx - 30, my + 4), (mx - 34, my - 50)]
    c.clear(c.mask(spline(flame, closed=True)))
    c.fill(head, value=1.0)
    c.outline(flame, w=3.4, light=0)
    inner = [(mx - 2, my - 60), (mx + 14, my - 20), (mx + 10, my + 8), (mx - 10, my + 8), (mx - 14, my - 20)]
    c.engrave(c.mask(spline(inner, closed=True)), 0.35, [c.ph_lin(90, wobble=0.3)], spacing=5, wmax=1.8)
    c.outline(inner, w=2.2, light=0)
    c.fill(head, value=1.0)
    # 捏火柴的手指：拇指和食指，后面是握着的拳
    fist = [(110, -60), (180, -70), (250, -50), (280, 10), (250, 70), (170, 80), (110, 50)]
    m = c.mask(spline(H(fist), closed=True))
    lam = c.relief(m, radius=40, blur=3)
    c.engrave(m, np.clip(0.45 - 0.4 * lam + 0.3 * c.grad(mx, 0, mx + 300, 0), 0.02, 1), [c.ph_lin(30), c.ph_lin(-50)], spacing=7.5)
    c.outline(H.cp(fist), w=STRUCT + 1, light=0.4)
    for pts, w in (([(60, -18), (90, -34), (130, -40), (150, -26), (124, -10), (70, -6)], STRUCT),
                   ([(56, 10), (90, 26), (140, 40), (160, 24), (130, 6), (70, 4)], STRUCT)):
        fm = c.mask(spline(H(pts), closed=True))
        lam = c.relief(fm, radius=12, blur=1)
        c.engrave(fm, np.clip(0.3 - 0.35 * lam + 0.2 * c.grad(mx, 0, mx + 200, 0), 0.0, 1), [c.ph_lin(30)], spacing=6.5, wmax=2.2)
        c.outline(H.cp(pts), w=w, light=0.4)
    for y in (-30, 30):
        c.line(H.cp([(100, y - 8), (106, y), (100, y + 8)]), w=2.2, taper=0.3)
    sleeve = [(250, -110), (500, -150), (520, 130), (250, 110)]
    m = c.mask(spline(H(sleeve), closed=True))
    c.engrave(m, 0.95, [c.ph_lin(60), c.ph_lin(-30), c.ph_lin(10)], spacing=8, swell=1)
    c.outline(H.cp(sleeve), w=OUTLINE, light=0.4)
    c.line(H.cp([(270, -100), (280, 0), (270, 100)]), w=STRUCT, taper=0.1)
