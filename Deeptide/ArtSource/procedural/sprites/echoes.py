"""回响图标（策划/15 §3.3、09 M7.7）12 个，和两个新 Boss 的被动图标。

和 icons.py 同一套规矩：不用排线，只用实墨块 + 留白；256 工作画布的坐标按 c.w / 256 缩放，填成纯黑也认得出。
回响图标导出 128（回响卡上 150、理智条旁 32 都用它），被动图标导出 64。颜色由游戏给：卡上是档位的字色，理智条旁同理。
"""
import math

import numpy as np

from etch import ellipse_pts, poly, spline


def _s(c):
    return c.w / 256


def _shape(c, pts, smooth=True):
    s = _s(c)
    q = [(x * s, y * s) + tuple(k) for x, y, *k in pts]
    return c.mask(spline(q, closed=True) if smooth else poly([p[:2] for p in q]))


def _fill(c, pts, smooth=True):
    m = _shape(c, pts, smooth)
    c.add(m)
    return m


def _cut(c, pts, smooth=True):
    c.erase(_shape(c, pts, smooth))


def _line(c, pts, w, smooth=True, erase=False, taper=0.0):
    s = _s(c)
    q = [(x * s, y * s) for x, y in pts]
    m = c.stroke(spline(q) if smooth and len(q) > 2 else np.array(q, float), w * s, add=False, taper=taper)
    if erase:
        c.erase(m)
    else:
        c.add(m)


def _ring(c, x, y, rx, ry, w, a0=0.0, a1=360.0, erase=False):
    s = _s(c)
    m = c.stroke(ellipse_pts(x * s, y * s, rx * s, ry * s, a0=a0, a1=a1), w * s, add=False, taper=0, closed=(a1 - a0 >= 360))
    if erase:
        c.erase(m)
    else:
        c.add(m)


def _waves(c, y, amp, n=3, w=14, x0=30, x1=226):
    xs = np.linspace(x0, x1, 40)
    _line(c, [(x, y + amp * math.sin((x - x0) / (x1 - x0) * n * 2 * math.pi)) for x in xs], w)


# ------------------------------------------------------------------ 守夜

def wick(c):
    """灯芯：一截蜡烛，烛泪挂在边上；火苗又高又亮（火苗里留白一道芯）。"""
    _fill(c, [(84, 132), (172, 132), (172, 236), (84, 236)], smooth=False)
    _fill(c, [(84, 132), (100, 160), (110, 132)])                          # 烛泪
    _fill(c, [(150, 132), (162, 176), (172, 132)])
    _line(c, [(128, 132), (128, 108)], 8)
    _fill(c, [(128, 20, 'c'), (156, 62), (156, 92), (128, 112, 'c'), (100, 92), (100, 62)])
    _cut(c, [(128, 54, 'c'), (138, 78), (128, 100, 'c'), (118, 78)])


def baffle(c):
    """挡板：一扇木板窗挡，三块竖板 + 一道横档，板缝留白；中间钉着一枚护符（菱形）。"""
    _fill(c, [(48, 36), (208, 36), (208, 222), (48, 222)], smooth=False)
    for x in (100, 156):
        _line(c, [(x, 40), (x, 218)], 7, erase=True)
    _line(c, [(44, 92), (212, 92)], 10, erase=True)
    _line(c, [(44, 104), (212, 104)], 6)
    _cut(c, [(128, 120), (162, 158), (128, 196), (94, 158)], smooth=False)
    _fill(c, [(128, 136), (146, 158), (128, 180), (110, 158)], smooth=False)


# ------------------------------------------------------------------ 堕落

def shoal(c):
    """浅滩：一道浪压得很低，底下就是石头——水浅，一脚就踩进去。"""
    _waves(c, 96, 16, n=2, w=16)
    _waves(c, 138, 10, n=3, w=10)
    for x, y, rx, ry in ((70, 200, 36, 22), (134, 206, 30, 18), (190, 196, 40, 26)):
        c.add(c.ellipse(x * _s(c), y * _s(c), rx * _s(c), ry * _s(c)))
    _line(c, [(30, 230), (226, 230)], 10)


def sleepless(c):
    """不眠：一只撑开的眼，眼白里几道血丝，下眼睑底下两道黑眼圈。"""
    _fill(c, [(24, 118, 'c'), (80, 66), (176, 66), (232, 118, 'c'), (176, 166), (80, 166)])
    _cut(c, [(44, 118, 'c'), (92, 82), (164, 82), (212, 118, 'c'), (164, 152), (92, 152)])
    c.add(c.ellipse(128 * _s(c), 118 * _s(c), 34 * _s(c), 34 * _s(c)))
    c.erase(c.ellipse(116 * _s(c), 106 * _s(c), 9 * _s(c), 9 * _s(c)))
    for pts in ([(52, 118), (70, 112), (84, 120)], [(200, 116), (184, 110), (172, 120)]):
        _line(c, pts, 5)
    for k in range(5):                                                      # 睫毛：一根根往上撑着
        a = math.radians(-150 + 30 * k)
        _line(c, [(128 + 84 * math.cos(a), 118 + 60 * math.sin(a)), (128 + 110 * math.cos(a), 118 + 84 * math.sin(a))], 9)
    _line(c, [(70, 190), (128, 204), (186, 190)], 9)
    _line(c, [(90, 218), (128, 228), (166, 218)], 7)


# ------------------------------------------------------------------ 传染

def aftershock(c):
    """余悸：一点震开的三圈波，波是断的，还在往外抖。"""
    c.add(c.ellipse(128 * _s(c), 128 * _s(c), 22 * _s(c), 22 * _s(c)))
    for r, gaps in ((56, ((20, 60), (200, 240))), (88, ((100, 140), (300, 330))), (118, ((30, 55), (160, 190), (250, 275)))):
        a = 0
        for g0, g1 in gaps + ((360, 360),):
            if g0 > a:
                _ring(c, 128, 128, r, r, 14 - r / 18, a0=a, a1=g0)
            a = g1


def spread(c):
    """蔓延：一滴墨裂成三滴，沿着枝杈往外淌。"""
    c.add(c.ellipse(128 * _s(c), 132 * _s(c), 40 * _s(c), 40 * _s(c)))
    for a in (-90, 30, 150):
        r = math.radians(a)
        x, y = 128 + 108 * math.cos(r), 132 + 100 * math.sin(r)
        _line(c, [(128, 132), (128 + 70 * math.cos(r + 0.2), 132 + 66 * math.sin(r + 0.2)), (x, y)], 12)
        c.add(c.ellipse(x * _s(c), y * _s(c), 22 * _s(c), 22 * _s(c)))
        for side in (-0.6, 0.6):
            _line(c, [(128 + 60 * math.cos(r), 132 + 56 * math.sin(r)),
                      (128 + 84 * math.cos(r + side), 132 + 78 * math.sin(r + side))], 6)
    c.erase(c.ellipse(116 * _s(c), 120 * _s(c), 9 * _s(c), 12 * _s(c)))


# ------------------------------------------------------------------ 中立

def notes_addendum(c):
    """笔记补页：一本合着的笔记本（书脊一道留白），书口夹着一张多出来的纸条，纸条上几道字。"""
    _fill(c, [(54, 44), (190, 44), (190, 222), (54, 222)], smooth=False)
    _line(c, [(74, 44), (74, 222)], 7, erase=True)
    _line(c, [(54, 196), (190, 196)], 5, erase=True)
    _fill(c, [(150, 70), (232, 84), (222, 168), (142, 156)], smooth=False)
    _cut(c, [(158, 80), (222, 92), (214, 158), (150, 148)], smooth=False)
    for y in (104, 120, 136):
        _line(c, [(162, y), (204, y + 6)], 5)


def satchel(c):
    """行囊：一只皮挎包，翻盖 + 扣带，背带绕上去；包身里多了一格（侧袋）。"""
    _line(c, [(60, 110), (70, 40), (128, 20), (186, 40), (196, 110)], 12)
    _fill(c, [(36, 100), (220, 100), (226, 228), (30, 228)], smooth=False)
    _cut(c, [(44, 108), (212, 108), (204, 156, 'c'), (128, 168), (52, 156, 'c')])
    _fill(c, [(52, 112), (204, 112), (198, 150, 'c'), (128, 160), (58, 150, 'c')])
    _fill(c, [(118, 150), (138, 150), (138, 194), (118, 194)], smooth=False)
    _cut(c, [(122, 172), (134, 172), (134, 186), (122, 186)], smooth=False)
    _line(c, [(180, 176), (180, 222)], 6, erase=True)


def sea_chart(c):
    """海图：一张两头卷着的海图，上面一道虚线航路，终点一个叉。"""
    _fill(c, [(40, 56), (216, 56), (216, 200), (40, 200)], smooth=False)
    for x in (40, 216):
        c.add(c.ellipse(x * _s(c), 128 * _s(c), 16 * _s(c), 78 * _s(c)))
    _cut(c, [(58, 70), (198, 70), (198, 186), (58, 186)], smooth=False)
    for k in range(6):
        t0, t1 = k / 6, k / 6 + 0.09
        p = lambda t: (72 + 100 * t, 172 - 90 * t + 30 * math.sin(t * 6))
        _line(c, [p(t0), p(t1)], 7, smooth=False)
    _line(c, [(162, 74), (190, 102)], 10, smooth=False)
    _line(c, [(190, 74), (162, 102)], 10, smooth=False)


# ------------------------------------------------------------------ 狼人

def deep_scar(c):
    """深痕：三道爪痕，又深又长，尾端劈开。"""
    for k in range(3):
        x0 = 64 + 46 * k
        _fill(c, [(x0, 28, 'c'), (x0 + 22, 40), (x0 + 30, 150), (x0 + 14, 232, 'c'), (x0 + 2, 150), (x0 - 4, 40)])
        _line(c, [(x0 + 14, 60), (x0 + 16, 200)], 4, erase=True)


def tidal_range(c):
    """潮差：一轮满月，月下两道水线——一道高、一道低。"""
    c.add(c.ellipse(128 * _s(c), 70 * _s(c), 46 * _s(c), 46 * _s(c)))
    c.erase(c.ellipse(112 * _s(c), 58 * _s(c), 12 * _s(c), 12 * _s(c)))
    _waves(c, 150, 10, n=3, w=12)
    _waves(c, 204, 10, n=3, w=12)


def blood_to_sea(c):
    """血入海：一滴血落进水里，水面一圈圈散开，水下拖着一道淡下去的血丝。"""
    _fill(c, [(128, 16, 'c'), (148, 58), (158, 84), (148, 106), (128, 114), (108, 106), (98, 84), (108, 58)])
    c.erase(c.ellipse(116 * _s(c), 82 * _s(c), 6 * _s(c), 12 * _s(c)))
    for rx, w in ((40, 10), (78, 8), (112, 6)):
        _ring(c, 128, 160, rx, rx * 0.22, w)
    for k, y in enumerate((186, 204, 220)):
        _line(c, [(128 - 6 * k, y), (134 + 8 * k, y + 12)], 9 - 2 * k)


# ------------------------------------------------------------------ 新 Boss 的被动（64）

def brood(c):
    """产卵（鳞母）：三枚挨着的卵，每枚里一只亮着的小眼（留白）。"""
    for x, y, r in ((88, 150, 52), (168, 158, 46), (130, 84, 44)):
        c.add(c.ellipse(x * _s(c), y * _s(c), r * 0.8 * _s(c), r * _s(c)))
        c.erase(c.ellipse((x + 6) * _s(c), y * _s(c), 10 * _s(c), 14 * _s(c)))
        c.add(c.ellipse((x + 6) * _s(c), y * _s(c), 3 * _s(c), 10 * _s(c)))


def mirror(c):
    """镜面（倒影）：一面带柄的手镜，镜面留白，里面一道裂。"""
    c.add(c.ellipse(128 * _s(c), 96 * _s(c), 76 * _s(c), 88 * _s(c)))
    c.erase(c.ellipse(128 * _s(c), 96 * _s(c), 56 * _s(c), 68 * _s(c)))
    _fill(c, [(114, 180), (142, 180), (150, 240), (106, 240)], smooth=False)
    _line(c, [(100, 52), (126, 92), (112, 120), (150, 150)], 7, smooth=False)


E = 128
P = 64
SPRITES = {
    'echo_wick': (E, E, wick),
    'echo_baffle': (E, E, baffle),
    'echo_shoal': (E, E, shoal),
    'echo_sleepless': (E, E, sleepless),
    'echo_aftershock': (E, E, aftershock),
    'echo_spread': (E, E, spread),
    'echo_notes_addendum': (E, E, notes_addendum),
    'echo_satchel': (E, E, satchel),
    'echo_sea_chart': (E, E, sea_chart),
    'echo_deep_scar': (E, E, deep_scar),
    'echo_tidal_range': (E, E, tidal_range),
    'echo_blood_to_sea': (E, E, blood_to_sea),
    'passive_brood': (P, P, brood),
    'passive_mirror': (P, P, mirror),
}
