"""LR02 人狼同体：一个头两张脸，后脑长在一起——左边是戴帽的人朝左的侧脸，右边是狼朝右的侧脸（双面牌两面都结算）。圆窗。"""
import numpy as np

from etch import Frame, OUTLINE, spline
from cards import _wolf as w

NAME = '人狼同体'


def draw(c):
    w.night(c, top=0.6, bottom=0.8, seed=291)
    w.moon(c, 624, 432, 400, glow=0, craters=False, seed=291)
    # 狼：右半，面朝右
    wolf = w.wolf_head(c, Frame((760, 470), scale=1.15), jaw=0, seed=291)
    # 人：左半，面朝左，实墨剪影（帽、额、鼻、唇、下巴、颈）
    face = [(640, 760), (600, 640), (560, 600), (520, 560), (480, 520), (470, 500), (484, 480), (470, 440), (440, 420), (452, 400),
            (470, 340), (520, 300), (640, 300)]
    m = c.mask(spline(face, closed=True))
    c.engrave(m, 0.95, [c.ph_lin(70, seed=292), c.ph_lin(10, seed=293)], spacing=7, swell=1.4)
    brim = c.mask(spline([(380, 300), (500, 270), (650, 266), (700, 290), (600, 310), (480, 312)], closed=True))
    crown = c.mask(spline([(470, 290), (480, 170), (560, 140), (640, 160), (660, 290)], closed=True))
    c.fill(np.clip(brim + crown, 0, 1))
    c.erase(c.line([(482, 272), (520, 180), (600, 150)], w=4, add=False, taper=0.3))
    c.erase(c.line([(400, 296), (520, 278), (660, 276)], w=3, add=False, taper=0.3))
    # 接缝：一道从帽顶到颈后的竖线，两边的东西在这里长到一起
    c.line([(640, 160), (650, 400), (630, 600), (650, 780)], w=OUTLINE, taper=0.3)
