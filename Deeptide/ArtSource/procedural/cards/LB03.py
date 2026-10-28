"""LB03 银链：长鳞的手腕上缠着三圈银链，一截链头垂下来；手指的爪正往回缩（复归）。"""
import numpy as np

from etch import Frame
from cards import _wolf as w

NAME = '银链'


def draw(c):
    w.night(c, top=0.85, bottom=0.6, seed=31)
    F = Frame((620, 450), rot=-14, scale=1.25)
    w.claw_hand(c, F, claws=0.45, tone=0.45, seed=31)
    for k in range(3):                                                         # 三圈链子缠在手腕上
        y0 = -90 + 20 * k
        pts = [F.p(-260 + 40 * k, y0 - 40), F.p(-230 + 40 * k, y0 + 60), F.p(-180 + 40 * k, 140)]
        w.chain(c, pts, link=24, w=4.0)
    tail = [F.p(-110, 110), F.p(-120, 220), F.p(-150, 330)]
    w.chain(c, tail, link=20, w=3.6)
