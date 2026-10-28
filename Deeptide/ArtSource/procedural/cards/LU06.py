"""LU06 双相：半边亮、半边暗的上弦月，暗的那一半里刻着一个狼头的侧影（每轮第一次复归拿格挡）。圆窗。"""
import numpy as np

from etch import Frame
from cards import _wolf as w

NAME = '双相'


def draw(c):
    w.night(c, top=0.92, bottom=0.82, seed=221)
    disc = w.moon(c, 624, 432, 330, phase=(330, 0), glow=1.3, seed=221)
    # 暗半边里刻白的狼头：先画到空的墨层上取线，再从暗面里擦出来
    keep = np.copy(c.layers['ink'])
    c.layers['ink'][:] = 0
    w.wolf_head(c, Frame((740, 470), scale=0.95), jaw=0, tone=0.0, scaled=False, gills=True, seed=222)
    lines = np.clip(c.layers['ink'], 0, 1)
    c.layers['ink'][:] = keep
    dark = disc * (c.xx > 624)
    c.erase(lines * dark * 0.95)
    c.add(lines * (1 - dark) * (c.xx > 624))
