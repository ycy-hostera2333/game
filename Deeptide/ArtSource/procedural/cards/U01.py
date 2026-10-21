"""U01 圣徽：胸前的圣徽，光芒用放射排线表现（策划/10 §4.6）。

能力牌圆窗。胸口的特写：深色风衣的两片翻领，中间挂着一枚圆形圣徽（外圈刻字纹、里面一个带光环的十字），
链子从上方垂下来；圣徽放光，周围的布料被一圈放射的白线切开。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, ellipse_pts, smoothstep, spline
from cards._common import burst

NAME = '圣徽'

CX, CY = 624, 470
R = 128


def draw(c):
    # ------------------------------------------------------------ 衣服：衬衫在中间，两片翻领
    c.engrave(np.ones((c.h, c.w), np.float32), 0.3 + 0.1 * c.noise(60, 1), [c.ph_lin(88, wobble=1.2, seed=2)], spacing=9)
    for sd in (-1, 1):
        lapel = [(CX + sd * 70, -20), (CX + sd * 120, 300), (CX + sd * 200, 560), (CX + sd * 180, 900), (CX + sd * 700, 900),
                 (CX + sd * 700, -20)]
        m = c.mask(spline(lapel, closed=True))
        lam = c.relief(m, radius=80, blur=5)
        c.engrave(m, np.clip(0.98 - 0.45 * lam + (0.0 if sd < 0 else 0.1), 0.5, 1),
                  [c.ph_lin(80 * sd, seed=3), c.ph_lin(-20 * sd, seed=4), c.ph_lin(40 * sd, seed=5)], spacing=8.5, swell=1.2)
        c.line([(CX + sd * 70, -20), (CX + sd * 120, 300), (CX + sd * 200, 560), (CX + sd * 180, 900)], w=OUTLINE, taper=0)
        c.line([(CX + sd * 104, 40), (CX + sd * 150, 300), (CX + sd * 226, 540)], w=2.4, taper=0.3)      # 翻领的缝线
    for y in (640, 780):                                                     # 衬衫扣子
        c.add(c.ellipse(CX, y, 9, 9))

    # ------------------------------------------------------------ 光：圣徽周围一圈放射的白线
    r = np.hypot(c.xx - CX, c.yy - CY)
    c.erase(1 - smoothstep(R + 20, R + 120, r))
    burst(c, CX, CY, R + 20, R + 330, n=56, width=5.0, erase=True, seed=1)
    burst(c, CX, CY, R + 30, R + 200, n=56, width=2.0, seed=2)

    # ------------------------------------------------------------ 链子
    for k, y in enumerate(np.arange(-10, CY - R, 22)):
        x = CX + (y - CY + R) * 0.0
        c.stroke(ellipse_pts(x, y, 7 if k % 2 else 4, 11), 2.6, closed=True)

    # ------------------------------------------------------------ 圣徽：外圈、刻纹、光环十字
    disk = c.ellipse(CX, CY, R, R)
    lam = c.relief(disk, radius=30, blur=2)
    c.engrave(disk, np.clip(0.35 - 0.4 * lam, 0, 1), [c.ph_ring(CX, CY), c.ph_lin(45)], spacing=6, wmax=2.2)
    c.stroke(ellipse_pts(CX, CY, R, R), STRUCT + 1, closed=True, light=0.4)
    c.stroke(ellipse_pts(CX, CY, R - 26, R - 26), 3, closed=True)
    for a in range(0, 360, 8):                                               # 外圈的刻纹
        q = math.radians(a)
        c.stroke([(CX + (R - 22) * math.cos(q), CY + (R - 22) * math.sin(q)), (CX + (R - 6) * math.cos(q), CY + (R - 6) * math.sin(q))],
                 2.0, taper=0)
    inner = c.ellipse(CX, CY, R - 30, R - 30)
    c.clear(inner)
    burst(c, CX, CY, 20, R - 34, n=32, width=1.6, seed=3)
    arm, w = 78, 15
    cross = [(-w, -arm, 'c'), (w, -arm, 'c'), (w, -w, 'c'), (arm, -w, 'c'), (arm, w, 'c'), (w, w, 'c'), (w, arm, 'c'), (-w, arm, 'c'),
             (-w, w, 'c'), (-arm, w, 'c'), (-arm, -w, 'c'), (-w, -w, 'c')]
    pts = [(CX + x, CY + y, k) for x, y, k in cross]
    m = c.mask(np.array([(x, y) for x, y, _ in pts]))
    c.clear(m)
    c.engrave(m * c.half_plane((CX + 60, CY - 60), (CX - 60, CY + 60)), 0.5, [c.ph_lin(-45, wobble=0)], spacing=5, wmax=2)
    c.stroke(np.array([(x, y) for x, y, _ in pts + pts[:1]]), 4, taper=0)
    c.stroke(ellipse_pts(CX, CY, 42, 42), 3.4, closed=True)                  # 十字中间的光环
    c.add(c.ellipse(CX, CY, 8, 8))
