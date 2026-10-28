"""LC12 伺机：齐腰的芦苇丛，后面伏着一头带鳞的狼，腿和肚子都没在草里，只有背脊和一只眼睛露出来（保留、格挡）。"""
import numpy as np

from etch import Frame, spline
from cards import _wolf as w

NAME = '伺机'


def draw(c):
    w.night(c, top=0.7, bottom=0.35, horizon=520, seed=151)
    w.moon(c, 1040, 150, 80, seed=151, craters=False)
    w.ground(c, 520, tone=0.7, seed=151)
    w.reeds(c, -20, 1270, 640, 320, n=110, seed=151, lean=-0.1, w=3.4)
    w.wolf_body(c, Frame((560, 860), scale=1.25), 'prowl', seed=151)
    w.reeds(c, -20, 1270, 880, 330, n=260, seed=152, lean=0.12, w=4.2)
