"""S01 疯语（诅咒）：满纸潦草重复的字迹（策划/10 §4.5、§4.6）。

一张信纸写满了同一个「词」，一遍又一遍：上面几行还算工整，越往下越乱、越斜，行和行叠在一起；
正中间那个词被描了很多遍，又大又黑；几处墨渍和划掉的字。诅咒牌是墨渍破窗，四周会被裁掉，内容集中在中间。
"""
import math

import numpy as np

from etch import Frame, poly, scrawl, smoothstep, spline

NAME = '疯语'

WORD = 7                     # 那个被反复书写的词（随机种子）


def draw(c):
    rng = np.random.default_rng(4)

    # ------------------------------------------------------------ 纸：旧，边缘发黄发暗，淡淡的横格
    page = np.ones((c.h, c.w), np.float32)
    c.engrave(page, 0.05 + 0.4 * c.radial(624, 432, 380, 760, sx=1.5) ** 1.6 + 0.04 * c.noise(90, 1),
              [c.ph_lin(2, wobble=1.5, seed=2), c.ph_lin(70, seed=3)], spacing=10, bands=[(0.03, 0.5), (0.45, 0.85)])
    for y in np.arange(110, 830, 46):
        c.stroke(poly([(0, y + 6), (c.w, y + 2)]), 1.3, taper=0)
    c.stroke(poly([(170, 0), (176, c.h)]), 1.6, taper=0)                   # 页边线

    # ------------------------------------------------------------ 一行行重复的字：越往下越乱
    for i, y in enumerate(np.arange(104, 830, 46)):
        chaos = (i / 15) ** 1.4
        slope = rng.normal(0, 0.02 + 0.12 * chaos)
        h = 15 + 6 * chaos * rng.uniform(0.5, 1.5)
        x0 = 190 + rng.uniform(0, 70) + rng.uniform(-10, 40) * chaos
        scrawl(c, x0, 1180, y + rng.normal(0, 6 * chaos), h, seed=100 + i, word=WORD, jitter=0.6 + 3 * chaos,
               width=2.4 + 1.4 * chaos * rng.uniform(0.5, 1.2), slope=slope, gap=(14 + 10 * chaos, 22 + 14 * chaos))
    # 下半张：几行写歪了，压在别的行上
    for k in range(4):
        y = rng.uniform(470, 760)
        scrawl(c, rng.uniform(180, 380), rng.uniform(900, 1200), y, rng.uniform(18, 26), seed=200 + k, word=WORD,
               jitter=4.0, width=3.2, slope=rng.choice([-1, 1]) * rng.uniform(0.08, 0.2))

    # ------------------------------------------------------------ 正中间那个词：大，描了很多遍
    big = np.random.default_rng(WORD)
    from etch import cursive_word
    pts, wl = cursive_word(big, int(big.integers(4, 8)), 70, 0, 0, step=(46, 58))
    F = Frame((624 - wl / 2 - 20, 470), rot=-6)
    clear = np.zeros((c.h, c.w), np.float32)
    for k in range(7):
        jit = np.random.default_rng(300 + k).normal(0, 3.2, pts.shape)
        q = F(pts + jit)
        clear = np.maximum(clear, c.stroke(q, 26, taper=0.1, step=2.5, add=False))
    c.erase(clear * 0.95)                                                   # 下面的小字先让开
    for k in range(7):
        jit = np.random.default_rng(300 + k).normal(0, 3.2, pts.shape)
        c.stroke(F(pts + jit), 5.5 + k * 0.6, taper=0.1, step=2.5)
    # 下面两道划线
    c.stroke(spline([F.p(-30, 34), F.p(wl * 0.5, 44), F.p(wl + 40, 30)]), 7, taper=0.15)
    c.stroke(spline([F.p(-10, 54), F.p(wl * 0.5, 60), F.p(wl + 20, 52)]), 4.5, taper=0.2)

    # ------------------------------------------------------------ 划掉的字
    for _ in range(9):
        x, y = rng.uniform(220, 1100), rng.uniform(120, 800)
        ln = rng.uniform(60, 140)
        for j in range(int(rng.integers(2, 5))):
            c.stroke(spline([(x, y + rng.uniform(-6, 6)), (x + ln * 0.5, y + rng.uniform(-8, 8)), (x + ln, y + rng.uniform(-6, 6))]),
                     rng.uniform(2.6, 4.0), taper=0.1)

    # ------------------------------------------------------------ 墨渍：一大两小，带溅点和往下淌的痕
    for (bx, by, br) in ((930, 300, 52), (330, 640, 34), (1040, 690, 24)):
        a = np.linspace(0, 2 * math.pi, 40, endpoint=False)
        rr = br * (1 + 0.16 * np.sin(3 * a + rng.uniform(0, 6)) + 0.08 * np.sin(7 * a + rng.uniform(0, 6))
                   + 0.04 * np.sin(13 * a + rng.uniform(0, 6)))
        blot = [(bx + r * math.cos(t), by + r * math.sin(t)) for r, t in zip(rr, a)]
        c.fill(c.mask(spline(blot, closed=True)))
        for _ in range(int(br / 2.5)):
            t = rng.uniform(0, 2 * math.pi)
            d = br * rng.uniform(1.15, 2.6) ** 1.3
            r = rng.uniform(1.5, br * 0.2) * (1.6 - d / (br * 3.5))
            c.add(c.ellipse(bx + d * math.cos(t), by + d * math.sin(t), r, r * rng.uniform(0.7, 1.0), rot=math.degrees(t)))
        # 笔尖拖出来的一道
        t = rng.uniform(0, 2 * math.pi)
        c.stroke(spline([(bx + br * 0.7 * math.cos(t), by + br * 0.7 * math.sin(t)),
                         (bx + br * 1.6 * math.cos(t + 0.15), by + br * 1.6 * math.sin(t + 0.15)),
                         (bx + br * 2.3 * math.cos(t + 0.1), by + br * 2.3 * math.sin(t + 0.1))]), br * 0.22, taper=(0.0, 0.9))
