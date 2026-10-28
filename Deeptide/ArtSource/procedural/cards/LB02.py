"""LB02 嚎月：荒原上一块礁石，带鳞的狼坐在上面对着满月嚎；地平线上一线海，近处是被风压弯的荒草。"""
import numpy as np

from etch import Frame, spline
from cards import _wolf as w

NAME = '嚎月'


def draw(c):
    w.night(c, top=0.8, bottom=0.42, horizon=640, seed=21)
    w.moon(c, 860, 230, 150, seed=21)
    w.water(c, 600, tone=0.35, seed=21, mask=np.clip(c.yy - 600, 0, 1) * (c.yy < 650))
    w.ground(c, 640, tone=0.55, slope=-0.04, seed=21)
    rock = [(250, 900), (270, 700), (330, 640), (470, 610), (600, 630), (680, 700), (720, 900)]
    m = c.mask(spline(rock, closed=True))
    lam = c.relief(m, radius=80, blur=4)
    c.engrave(m, np.clip(0.9 - 0.7 * lam, 0, 1), [c.ph_lin(-30, seed=22), c.ph_lin(40, seed=23), c.ph_lin(85, seed=24)], spacing=8, swell=1.2)
    c.outline(rock, w=7)
    w.wolf_body(c, Frame((470, 628), scale=0.82), 'howl', seed=21)
    w.reeds(c, 0, 1248, 880, 170, n=60, seed=21, lean=0.25)
    # 嚎声：从嘴边往月亮去的三道弧
    for k, r in enumerate((60, 100, 140)):
        c.line([(640 + r * 0.6, 120 + 60 - r * 0.2), (660 + r * 0.8, 100 + 40 - r * 0.4), (680 + r, 80 + 20 - r * 0.5)], w=3.2 - 0.5 * k, taper=0.4)
