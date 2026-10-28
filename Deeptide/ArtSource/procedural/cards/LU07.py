"""LU07 月下双生：满月下的山脊上，猎户和带鳞的狼背靠背站着，两道影子朝下坡拖得很长（本轮每跨一次线多一段）。"""
import numpy as np

from etch import Frame, spline
from cards import _wolf as w
from cards._common import person

NAME = '月下双生'


def draw(c):
    w.night(c, top=0.82, bottom=0.45, seed=231)
    w.moon(c, 624, 250, 190, seed=231)
    ridge = [(-20, 900), (-20, 720), (300, 640), (624, 610), (950, 640), (1270, 720), (1270, 900)]
    m = c.mask(spline(ridge, closed=True))
    c.engrave(m, np.clip(0.7 + 0.2 * c.grad(0, 610, 0, 864) + 0.05 * c.noise(40, 232), 0, 1),
              [c.ph_wave(0, 3, 200, seed=233), c.ph_lin(20, seed=234)], spacing=8, swell=1.1)
    c.line(spline(ridge[1:6]), w=6, taper=0.2)
    person(c, 560, 616, 250, hat=True, arms=[(0.5, 160)])                  # 猎户：面朝左
    w.wolf_body(c, Frame((700, 616), scale=0.45), 'howl', seed=231)
    # 两道长影子：往两边的下坡拖
    for sd, x in ((-1, 560), (1, 700)):
        shadow = c.mask([(x - 20 * sd, 620), (x + 20 * sd, 620), (x + 520 * sd, 760), (x + 470 * sd, 790)])
        c.add(shadow * 0.95)
