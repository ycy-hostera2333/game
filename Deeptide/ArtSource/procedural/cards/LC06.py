"""LC06 扑杀（双面）：带鳞的狼从左下的荒草里腾空扑向右上，背后是低垂的月亮。
癫狂层：同一扑的第二个影子，错开半个身位（兽形面打两次）。"""
import numpy as np

from etch import Frame
from cards import _wolf as w

NAME = '扑杀'


def draw(c):
    w.night(c, top=0.8, bottom=0.45, horizon=720, seed=91)
    w.moon(c, 380, 300, 170, seed=91)
    w.ground(c, 720, tone=0.6, seed=91)
    w.reeds(c, 0, 1248, 860, 200, n=70, seed=91, lean=0.3)
    w.wolf_body(c, Frame((600, 620), rot=-14, scale=1.05), 'leap', seed=91)
    w.wolf_body(c, Frame((420, 560), rot=-10, scale=1.0), 'leap', layer='mad', tone=0.45, seed=92, eye=False, gills=False, rim=False)
