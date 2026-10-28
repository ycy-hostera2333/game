"""LC04 舔舐伤口（双面）：横过画面的前臂，三道爪伤，一截布条正缠上去（清醒面：格挡）。
癫狂层：伤口上长出来盖住它的鳞（兽形面：回血）。"""
import numpy as np

from etch import Frame, OUTLINE, STRUCT, spline
from cards import _wolf as w

NAME = '舔舐伤口'


def draw(c):
    w.night(c, top=0.8, bottom=0.6, seed=71)
    A = Frame((620, 450), rot=-8)
    arm = [(-700, -120), (-200, -128), (300, -110), (700, -96), (700, 110), (300, 116), (-200, 130), (-700, 140)]
    m = c.mask(spline(A(arm), closed=True))
    lam = c.relief(m, radius=110, blur=5)
    c.engrave(m, np.clip(0.55 - 0.55 * lam, 0, 1), [c.ph_axis(A.p(-700, 0), A.p(700, 0), bow=-30, half=130, seed=72),
                                                  c.ph_along(A.p(-700, 0), A.p(700, 0), seed=73)], spacing=8)
    c.line(A.cp(arm[:4]), w=OUTLINE)
    c.line(A.cp(arm[4:]), w=OUTLINE)
    cut = w.claw_marks(c, A.p(-160, -80), A.p(80, 70), n=3, gap=34, width=20, seed=71, jag=3)
    # 布条：从右边绕上来的三圈，盖住伤口的右半
    for k in range(3):
        x = 140 + 70 * k
        band = [(x - 30, -140), (x + 20, -140), (x + 60, 140), (x + 10, 140)]
        bm = c.mask(A(band))
        c.clear(bm, layers=('ink',))
        c.engrave(bm, 0.12 + 0.2 * c.grad(*A.p(0, -140), *A.p(0, 140)), [c.ph_along(A.p(x - 30, -140), A.p(x + 10, 140), seed=74 + k)],
                  spacing=6, wmax=2.0)
        c.stroke(A(band + [band[0]]), 3.4, taper=0)
    c.line(A.cp([(350, 130), (420, 220), (520, 260), (640, 250)]), w=26, taper=(0.0, 0.5))            # 垂下的布头
    c.erase(c.line(A.cp([(352, 130), (422, 218), (520, 256), (630, 248)]), w=18, add=False, taper=(0.0, 0.5)))
    # 癫狂层：伤口上和周围长出的鳞
    patch = c.ellipse(*A.p(-50, 0), 260, 150, -8)
    w.scales(c, patch, size=30, angle=172, tone=0.0, w=3.0, layer='mad', shade=0.3)
