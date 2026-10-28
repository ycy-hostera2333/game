"""LC05 连爪：钉起来的木板门，被一只爪一口气抓了三下——三组爪痕一组比一组低，木屑翻起来。"""
import numpy as np

from etch import OUTLINE
from cards import _wolf as w

NAME = '连爪'


def draw(c):
    # 竖木板：木纹 + 板缝
    plank = 156
    grain = c.xx + 18 * np.sin(c.yy / 140 + (c.xx // plank) * 1.7) + 6 * c.noise(40, 81)
    tone = 0.25 + 0.15 * np.sin((c.xx % plank) / plank * np.pi * 2) * 0 + 0.2 * c.grad(0, 0, 0, 864) + 0.06 * c.noise(80, 82)
    c.engrave(np.ones((c.h, c.w), np.float32), np.clip(tone, 0, 1), [grain, c.ph_lin(70, seed=83)], spacing=8, swell=1.0)
    for x in range(0, 1300, plank):
        c.line([(x, 0), (x + 3, 432), (x, 864)], w=6, taper=0)
        for y in (150, 700):
            c.add(c.ellipse(x + 30, y, 8, 8))
            c.add(c.ellipse(x + plank - 30, y, 8, 8))
    for k, (x0, y0) in enumerate(((330, 90), (470, 260), (610, 430))):
        w.claw_marks(c, (x0 + 260, y0), (x0 - 40, y0 + 360), n=4, gap=36, width=22, seed=81 + k, jag=5)
