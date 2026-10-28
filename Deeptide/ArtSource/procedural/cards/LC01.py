"""LC01 撕咬：兽形的头从左上往右下扑咬，嘴张到最大；咬合处溅出的血点落在画面右下。"""
import numpy as np

from etch import Frame
from cards import _wolf as w

NAME = '撕咬'


def draw(c):
    w.night(c, top=0.55, bottom=0.8, seed=41)
    w.wolf_head(c, Frame((480, 360), rot=24, scale=1.7), jaw=34, seed=41)
    w.splatter(c, 980, 700, 34, n=22, seed=41)
    for x, y, r in ((900, 560, 14), (1040, 600, 10), (1110, 760, 18), (860, 780, 9)):
        w.drop(c, x, y, r, rot=200)
