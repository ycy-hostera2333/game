"""LU05 狂性：一只兽眼的特写，竖瞳缩成一条缝，虹膜是深渊色，眼眶外一圈毛、再往外是鳞（每轮第一次变身抽 2）。圆窗。"""
import numpy as np

from cards import _wolf as w
from cards._common import eye

NAME = '狂性'


def draw(c):
    face = np.ones((c.h, c.w), np.float32)
    c.engrave(face, np.clip(0.4 + 0.45 * c.radial(624, 432, 160, 520) + 0.06 * c.noise(30, 211), 0, 1),
              [c.ph_ray(624, 432, 260) + 6 * c.noise(20, 212), c.ph_ring(624, 432), c.ph_lin(60, seed=213)], spacing=8, swell=1.0)
    w.scales(c, np.clip(c.radial(624, 432, 330, 380), 0, 1), size=34, angle=0, tone=0.5, seed=211)
    w.fur(c, np.clip(c.radial(624, 432, 200, 240), 0, 1) * (1 - c.radial(624, 432, 320, 370)),
          lambda x, y: np.degrees(np.arctan2(y - 432, x - 624)), length=40, density=0.002, seed=211)
    c.clear(c.ellipse(624, 432, 300, 150, -4))
    m = eye(c, 624, 432, 280, layer='ink', look=0.0, h=0.5, slit=True, width=10)
    c.add(c.ellipse(624, 432, 118, 118) * (1 - c.ellipse(624, 432, 36, 108)) * m * 0.85, 'abyss')
