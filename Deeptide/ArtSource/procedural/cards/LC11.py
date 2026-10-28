"""LC11 嗅迹（双面）：退潮的滩涂上一串爪印，从近处一直通到雾里（清醒面：抽牌、弃牌）。
癫狂层：每个爪印上升起一缕气味，弯弯绕绕地指向同一个方向（兽形面：看清意图）。"""
import math

import numpy as np

from cards import _wolf as w

NAME = '嗅迹'
HORIZON = 300


def draw(c):
    w.night(c, top=0.55, bottom=0.25, horizon=HORIZON, seed=141)
    flat = np.clip(c.yy - HORIZON, 0, 1).astype(np.float32)
    depth = np.clip((c.yy - HORIZON) / (864 - HORIZON), 0.02, 1)
    c.engrave(flat, np.clip(0.2 + 0.4 * depth + 0.08 * c.noise(40, 142), 0, 1),
              [np.clip(c.yy - HORIZON, 0, None) ** 0.75 * 6 + 2 * c.noise(30, 143), c.ph_lin(10, seed=144)],
              spacing=8, swell=1.0)
    for k in range(6):                                                      # 积水的亮条
        y = HORIZON + 20 + 22 * k ** 1.5
        c.erase(c.ellipse(600 + 300 * math.sin(k * 2.1), y, 90 + 16 * k, 3 + 2 * k) * 0.9)
    prints = []
    for k in range(9):
        t = k / 8
        y = 820 - 500 * (1 - (1 - t) ** 1.7)
        s = 1.25 * (1 - t) ** 1.4 + 0.12
        x = 560 + 120 * math.sin(t * 5) * (1 - t) + (60 if k % 2 else -60) * s
        prints.append((x, y, s))
        w.paw_print(c, x, y, s=s, rot=-8 + 6 * math.sin(k))
    rng = np.random.default_rng(141)
    for x, y, s in prints:                                                  # 癫狂层：气味
        pts = [(x, y - 40 * s)]
        for j in range(1, 6):
            pts.append((x + 90 * s * math.sin(j * 1.2 + rng.uniform(0, 1)) + 40 * j * s, y - 40 * s - 70 * s * j))
        c.line(pts, w=max(6 * s, 2.2), layer='mad', taper=(0.1, 0.9))
