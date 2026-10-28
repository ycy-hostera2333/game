"""LU09 嗜血：圆窗正中一颗悬着的大血滴，鼓起的表面上映出一只深渊色的兽眼；四周往下落着小血滴（敌人流血时你回血）。"""
import numpy as np

from etch import ellipse_pts, spline
from cards import _wolf as w

NAME = '嗜血'


def draw(c):
    w.night(c, top=0.88, bottom=0.7, seed=251)
    # 外圈一道淡的光晕，让大血滴从暗底里跳出来
    c.erase(np.clip(1 - c.radial(624, 470, 150, 330), 0, 1) * 0.8)
    pts = w.drop_pts(624, 500, 170)
    m = c.mask(spline(pts, closed=True))
    c.fill(m)
    # 血滴里的反光：一道弧形高光 + 映出的兽眼
    c.erase(c.stroke(ellipse_pts(624, 500, 130, 130, a0=200, a1=250), 16, add=False, taper=0.4))
    c.erase(c.ellipse(560, 430, 14, 22, 30))
    eye_m = c.mask(spline([(560, 540, 'c'), (624, 512), (700, 528, 'c'), (630, 560)], closed=True))
    c.erase(eye_m * 0.95)
    c.add(eye_m * 0.9, 'abyss')
    c.add(c.ellipse(632, 536, 5, 16, 6))
    rng = np.random.default_rng(251)
    for _ in range(9):                                                      # 小血滴
        a = rng.uniform(0, 2 * np.pi)
        d = rng.uniform(260, 380)
        w.drop(c, 624 + d * np.cos(a), 450 + d * np.sin(a) * 0.9, rng.uniform(9, 18))
