"""LU01 再生：皮肤上三道新爪伤，下半截已经被长出来的鳞盖住、合拢了（兽形时回合结束回血）。圆窗。"""
import numpy as np

from etch import smoothstep
from cards import _wolf as w

NAME = '再生'


def draw(c):
    skin = np.ones((c.h, c.w), np.float32)
    lam = 0.5 + 0.5 * c.noise(260, 161)
    c.engrave(skin, np.clip(0.35 - 0.2 * lam + 0.25 * c.radial(624, 432, 200, 520), 0, 1),
              [c.ph_axis((200, 100), (1050, 760), bow=-60, half=500, seed=162), c.ph_lin(60, seed=163)], spacing=8.5)
    cut = w.claw_marks(c, (840, 120), (420, 740), n=3, gap=90, width=46, seed=161, jag=7)
    healed = smoothstep(0.42, 0.6, c.grad(840, 120, 420, 740))           # 越往下愈合得越多
    cover = healed * np.clip(c.shadow_of(cut, 0, 0, blur=26) * 3, 0, 1)
    c.clear(cover)
    w.scales(c, cover, size=30, angle=-60, tone=0.15, w=2.8, seed=161)
