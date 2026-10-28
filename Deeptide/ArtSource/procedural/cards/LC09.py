"""LC09 压抑兽性：长鳞的手死死攥成拳，指缝里的爪被压回去一半；手腕上勒着一圈皮带（恢复理智、复归时抽牌）。"""
import numpy as np

from etch import Frame, spline
from cards import _wolf as w

NAME = '压抑兽性'


def draw(c):
    w.night(c, top=0.6, bottom=0.85, seed=121)
    F = Frame((640, 470), rot=-60, scale=1.35)
    w.claw_hand(c, F, claws=0.3, tone=0.45, curl=0.95, seed=121)
    strap = [(-160, -110), (-110, -110), (-100, 110), (-150, 110)]
    m = c.mask(spline(F(strap), closed=True))
    c.clear(m, layers=('ink',))
    c.engrave(m, 0.75, [c.ph_along(F.p(-130, -110), F.p(-130, 110), seed=122)], spacing=6, swell=1.0)
    c.outline(F.cp(strap), w=5)
    bx, by = F.p(-130, 0)
    c.stroke([(bx - 18, by - 22), (bx + 18, by - 22), (bx + 18, by + 22), (bx - 18, by + 22), (bx - 18, by - 22)], 5, taper=0)
    # 攥得发抖：拳头周围几道短短的颤线
    rng = np.random.default_rng(121)
    for _ in range(14):
        a = rng.uniform(0, 2 * np.pi)
        x, y = F.p(0, 0)
        r0 = rng.uniform(260, 300)
        c.line([(x + r0 * np.cos(a), y + r0 * np.sin(a)), (x + (r0 + 40) * np.cos(a + 0.03), y + (r0 + 40) * np.sin(a + 0.03))], w=3, taper=0.4)
