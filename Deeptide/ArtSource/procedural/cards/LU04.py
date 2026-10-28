"""LU04 围猎：雾里一圈成对的眼睛围着中间一个提灯的小人，越靠后的眼睛越小（本回合每打过一张攻击就多一段）。"""
import math

import numpy as np

from cards._common import person

NAME = '围猎'


def draw(c):
    lit = 1 - c.radial(624, 640, 60, 420, sy=0.5)
    c.engrave(np.ones((c.h, c.w), np.float32), np.clip(0.92 - 0.6 * lit + 0.04 * c.noise(80, 201), 0, 1),
              [c.ph_ring(624, 640, 1.0, 2.0), c.ph_lin(30, seed=202), c.ph_lin(-40, seed=203)], spacing=9, swell=1.2)
    person(c, 624, 760, 240, arms=[(0.6, 30)])
    c.erase(np.clip(1 - c.radial(690, 660, 4, 70), 0, 1))                  # 提灯
    c.add(c.ellipse(690, 660, 9, 12))
    rng = np.random.default_rng(201)
    for k in range(9):                                                     # 一圈眼睛
        a = math.radians(-180 + 180 * k / 8 + rng.uniform(-6, 6))
        x = 624 + 540 * math.cos(a)
        y = 600 + 380 * math.sin(a) * 0.9
        s = 0.9 + 0.9 * (y / 864)
        for dx in (-22, 22):
            c.add(c.ellipse(x + dx * s, y, 15 * s, 6.5 * s, -10 * dx / 22), 'abyss')
            c.erase(c.ellipse(x + dx * s, y, 14 * s, 6 * s, -10 * dx / 22))
            c.add(c.ellipse(x + dx * s + 2, y, 2.6 * s, 6 * s))
