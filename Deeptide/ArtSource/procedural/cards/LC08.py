"""LC08 低嗥：兽形压低了头，耳朵往后贴，嘴角掀开一点；喉咙里的低吼是一圈圈往外的波纹（代价 1，跨线时多一点能量）。"""
import math

import numpy as np

from etch import Frame, ellipse_pts
from cards import _wolf as w

NAME = '低嗥'


def draw(c):
    w.night(c, top=0.85, bottom=0.65, seed=111)
    # 低吼的波纹：以喉咙为圆心，刻在暗底上
    for k in range(7):
        r = 200 + 70 * k
        cov = c.stroke(ellipse_pts(560, 600, r, r * 0.8, a0=-70, a1=60), 3.4 - 0.3 * k, add=False, taper=0.3)
        c.erase(cov * (0.95 - 0.08 * k))
    w.wolf_head(c, Frame((520, 470), rot=20, scale=1.8), jaw=9, seed=111)
