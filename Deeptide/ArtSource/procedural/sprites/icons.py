"""图标（策划/10 §6.3、§6.4、§8）：意图 8 个（含震慑叉）、状态 9 个、敌人被动 4 个、格挡盾、样张遗物鲸油灯。

图标不用排线，只用实墨块 + 留白（10 §3）；外轮廓 6px、结构线 3px（工作画布）。全部是剪影，填成纯黑也认得出。
意图图标导出 128，状态图标导出 64（工作画布都是 2 倍）。在游戏里由 Image 着色：意图是墨圆上的纸色图标，状态是有色描边小牌里的彩色图标。
"""
import math

import numpy as np

from etch import ellipse_pts, poly, spline


def _fill(c, pts, smooth=True, value=1.0):
    m = c.mask(spline(pts, closed=True) if smooth else poly(pts))
    c.add(m * value)
    return m


def _cut(c, pts, smooth=True):
    m = c.mask(spline(pts, closed=True) if smooth else poly(pts))
    c.erase(m)
    return m


def _S(c, k):
    """把 256 工作画布上的坐标按画布大小缩放（状态图标是 128）。"""
    return c.w / 256 * k


# ====================================================================== 意图（256 工作画布）

def attack(c):
    s = c.w / 256
    # 剑：从左下指向右上
    a = math.radians(-45)
    d = np.array([math.cos(a), math.sin(a)])
    n = np.array([-d[1], d[0]])
    o = np.array([108, 150]) * s
    blade = [o + n * 21 * s, o + d * 120 * s + n * 15 * s, o + d * 166 * s, o + d * 120 * s - n * 15 * s, o - n * 21 * s]
    c.add(c.mask(np.array(blade)))
    c.erase(c.stroke([tuple(o + d * 12 * s), tuple(o + d * 112 * s)], 7 * s, add=False, taper=(0.0, 0.6)))   # 血槽
    guard = [o + n * 52 * s - d * 6 * s, o + n * 52 * s + d * 14 * s, o - n * 52 * s + d * 14 * s, o - n * 52 * s - d * 6 * s]
    c.add(c.mask(np.array(guard)))
    grip = [o + n * 12 * s, o + n * 12 * s - d * 56 * s, o - n * 12 * s - d * 56 * s, o - n * 12 * s]
    c.add(c.mask(np.array(grip)))
    p = o - d * 66 * s
    c.add(c.ellipse(p[0], p[1], 20 * s, 20 * s))
    c.erase(c.ellipse(p[0], p[1], 7 * s, 7 * s))
    for k in range(3):                                                    # 握柄缠绳：留白斜纹
        q = o - d * (14 + k * 14) * s
        c.erase(c.stroke([tuple(q + n * 9 * s), tuple(q - n * 9 * s - d * 6 * s)], 3 * s, add=False, taper=0))


def defend(c, cross=False):
    s = c.w / 256
    shield = [(128, 30), (196, 52), (214, 60, 'c'), (208, 140), (174, 200), (128, 232, 'c'), (82, 200), (48, 140), (42, 60, 'c'),
              (60, 52)]
    pts = [(x * s, y * s) + tuple(k) for x, y, *k in shield]
    _fill(c, pts)
    inner = [(x * s * 0.78 + 128 * s * 0.22, y * s * 0.78 + 134 * s * 0.22) + tuple(k) for x, y, *k in shield]
    _cut(c, inner)
    inner2 = [(x * s * 0.66 + 128 * s * 0.34, y * s * 0.66 + 136 * s * 0.34) + tuple(k) for x, y, *k in shield]
    _fill(c, inner2)
    if cross:                                                              # 心防：盾上一个留白十字
        c.erase(c.stroke([(128 * s, 78 * s), (128 * s, 190 * s)], 16 * s, add=False, taper=0))
        c.erase(c.stroke([(90 * s, 118 * s), (166 * s, 118 * s)], 16 * s, add=False, taper=0))
    else:                                                                  # 盾心的铆钉
        c.erase(c.ellipse(128 * s, 126 * s, 13 * s, 13 * s))


def buff(c):
    s = c.w / 256
    arrow = [(128, 22, 'c'), (220, 120, 'c'), (164, 120, 'c'), (164, 232, 'c'), (92, 232, 'c'), (92, 120, 'c'), (36, 120, 'c')]
    _fill(c, [(x * s, y * s, k) for x, y, k in arrow], smooth=False)
    for y in (150, 182, 212):                                              # 杆上三道留白
        c.erase(c.stroke([(100 * s, y * s), (156 * s, y * s)], 7 * s, add=False, taper=0))


def debuff(c):
    s = c.w / 256
    # 滴墨形的下箭头：一滴往下坠的墨，下端收成箭头
    drop = [(128, 24), (160, 80), (196, 136), (204, 168), (184, 200), (128, 236, 'c'), (72, 200), (52, 168), (60, 136), (96, 80)]
    _fill(c, [(x * s, y * s) + tuple(k) for x, y, *k in drop])
    chev = [(84, 130, 'c'), (128, 172, 'c'), (172, 130, 'c'), (172, 152, 'c'), (128, 196, 'c'), (84, 152, 'c')]
    _cut(c, [(x * s, y * s, k) for x, y, k in chev], smooth=False)
    c.erase(c.ellipse(112 * s, 84 * s, 9 * s, 14 * s, rot=-20))            # 墨滴的高光


def mind(c):
    s = c.w / 256
    W, H = 112 * s, 66 * s
    cx, cy = 128 * s, 134 * s
    upper = spline([(cx - W, cy, 'c'), (cx - W * 0.4, cy - H), (cx + W * 0.4, cy - H), (cx + W, cy, 'c')])
    lower = spline([(cx + W, cy, 'c'), (cx + W * 0.4, cy + H * 0.85), (cx - W * 0.4, cy + H * 0.85), (cx - W, cy, 'c')])
    eye = c.mask(np.vstack([upper, lower]))
    c.add(eye)
    inner = c.mask(np.vstack([upper, lower]) * 0.8 + np.array([cx, cy]) * 0.2)
    c.erase(inner)
    c.add(c.ellipse(cx, cy, 44 * s, 44 * s) * inner)
    c.erase(c.ellipse(cx, cy, 16 * s, 16 * s))
    c.add(c.ellipse(cx, cy, 9 * s, 9 * s))
    c.erase(c.ellipse(cx - 18 * s, cy - 18 * s, 7 * s, 7 * s))
    for k in range(7):                                                     # 睫毛
        t = -0.55 + k * 0.18
        a = -math.pi / 2 + t
        p = (cx + W * 0.95 * math.sin(t), cy - H * 1.0 * math.cos(t) * 0.96)
        q = (p[0] + 26 * s * math.cos(a), p[1] + 26 * s * math.sin(a))
        c.add(c.stroke([p, q], 9 * s, add=False, taper=(0.0, 0.8)))


def summon(c):
    s = c.w / 256
    c.add(c.ellipse(128 * s, 214 * s, 100 * s, 22 * s))                     # 水洼
    ctrl = [(150, 214), (170, 160), (150, 104), (104, 70), (90, 40), (116, 26), (128, 50), (112, 58)]
    center = spline([(x * s, y * s) for x, y in ctrl])
    body = c.stroke(center, 46 * s, add=False, taper=0, profile=lambda u: 1 - 0.88 * u)
    c.add(body)
    t = np.gradient(center, axis=0)
    t /= np.maximum(np.hypot(t[:, 0], t[:, 1]), 1e-6)[:, None]
    nn = np.stack([-t[:, 1], t[:, 0]], 1)
    for i in range(10, int(len(center) * 0.7), max(len(center) // 9, 1)):  # 吸盘：留白的小圆
        u = i / len(center)
        p = center[i] + nn[i] * 23 * s * (1 - 0.88 * u) * 0.35
        r = 7 * s * (1 - 0.8 * u)
        c.erase(c.ellipse(p[0], p[1], r, r))
    c.erase(c.stroke(spline([(60 * s, 212 * s), (128 * s, 206 * s), (196 * s, 212 * s)]), 4 * s, add=False, taper=0.3))


def unknown(c):
    s = c.w / 256
    q = spline([(80 * s, 92 * s), (96 * s, 52 * s), (140 * s, 38 * s), (178 * s, 64 * s), (172 * s, 104 * s), (136 * s, 130 * s),
                (126 * s, 162 * s)])
    c.stroke(q, 30 * s, taper=(0.15, 0.0))
    c.add(c.ellipse(126 * s, 206 * s, 19 * s, 19 * s))
    for p, r in (((98 * s, 214 * s), 5), ((150 * s, 222 * s), 4), ((160 * s, 190 * s), 3)):   # 溅出的墨点
        c.add(c.ellipse(p[0], p[1], r * s, r * s))


def stun(c):
    s = c.w / 256
    # 震慑：两道毛笔划出的叉，起笔重、收笔尖
    c.stroke(spline([(40 * s, 44 * s), (130 * s, 130 * s), (220 * s, 214 * s)]), 30 * s, taper=(0.05, 0.6))
    c.stroke(spline([(218 * s, 40 * s), (128 * s, 132 * s), (42 * s, 218 * s)]), 26 * s, taper=(0.05, 0.6))


def block(c):
    """格挡徽章：素面的盾，中间留给数字。"""
    s = c.w / 256
    shield = [(128, 18), (204, 42), (226, 52, 'c'), (220, 140), (182, 206), (128, 240, 'c'), (74, 206), (36, 140), (30, 52, 'c'),
              (52, 42)]
    _fill(c, [(x * s, y * s) + tuple(k) for x, y, *k in shield])
    rim = [(x * s * 0.9 + 128 * s * 0.1, y * s * 0.9 + 130 * s * 0.1) + tuple(k) for x, y, *k in shield]
    c.erase(c.stroke(spline(rim, closed=True), 5 * s, add=False, closed=True, taper=0))


# ====================================================================== 状态（128 工作画布，坐标仍按 256 写再缩放）

def strength(c):
    s = c.w / 256
    # 握拳：四个指节一排，拇指压在前面
    _fill(c, [(56 * s, 100 * s), (200 * s, 92 * s), (214 * s, 176 * s), (176 * s, 228 * s), (88 * s, 230 * s), (50 * s, 170 * s)])
    for k in range(4):
        x = 76 + k * 34
        c.add(c.ellipse(x * s, 92 * s, 20 * s, 24 * s))
        c.erase(c.stroke([((x + 16) * s, 76 * s), ((x + 16) * s, 128 * s)], 4 * s, add=False, taper=0.3))
    c.erase(c.stroke(spline([(60 * s, 150 * s), (120 * s, 140 * s), (170 * s, 150 * s)]), 7 * s, add=False, taper=0.2))   # 拇指的缝
    c.erase(c.stroke(spline([(80 * s, 190 * s), (120 * s, 200 * s)]), 4 * s, add=False, taper=0.4))


def dexterity(c):
    s = c.w / 256
    shaft = spline([(64 * s, 214 * s), (120 * s, 150 * s), (176 * s, 80 * s), (210 * s, 36 * s)])
    vane = [(70 * s, 200 * s), (96 * s, 130 * s), (150 * s, 70 * s), (210 * s, 36 * s, 'c'), (190 * s, 110 * s), (140 * s, 168 * s),
            (90 * s, 196 * s)]
    _fill(c, vane)
    for k in range(6):                                                     # 羽枝之间的缺口
        t = 0.2 + k * 0.12
        p = shaft[int(t * (len(shaft) - 1))]
        c.erase(c.stroke([tuple(p), (p[0] + 46 * s, p[1] + 10 * s)], 4 * s, add=False, taper=(0.0, 0.7)))
    c.erase(c.stroke(shaft[: int(len(shaft) * 0.8)], 5 * s, add=False, taper=0.1))
    c.stroke(shaft[: int(len(shaft) * 0.25)], 8 * s, taper=(0.0, 0.3))


def vulnerable(c):
    defend(c)
    s = c.w / 256
    crack = [(130, 22), (112, 80), (146, 116), (108, 164), (134, 236)]
    c.erase(c.stroke(poly([(x * s, y * s) for x, y in crack]), 16 * s, add=False, taper=0))


def weak(c):
    s = c.w / 256
    # 下垂的剑：剑身弯下去，剑尖朝下
    blade = spline([(110 * s, 96 * s), (150 * s, 110 * s), (176 * s, 150 * s), (180 * s, 196 * s), (170 * s, 236 * s)])
    c.stroke(blade, 30 * s, taper=(0.0, 0.85))
    c.erase(c.stroke(blade[: int(len(blade) * 0.7)], 5 * s, add=False, taper=(0.1, 0.6)))
    c.stroke([(84 * s, 64 * s), (128 * s, 120 * s)], 22 * s, taper=0)   # 护手
    c.stroke([(60 * s, 110 * s), (130 * s, 70 * s)], 16 * s, taper=0)
    c.stroke([(96 * s, 90 * s), (54 * s, 44 * s)], 18 * s, taper=0)      # 握柄
    c.add(c.ellipse(48 * s, 38 * s, 16 * s, 16 * s))


def frail(c):
    defend(c)
    s = c.w / 256
    for (x, y, r) in ((196, 66, 26), (52, 132, 22), (160, 208, 20)):       # 盾边被啃掉的缺口
        c.erase(c.ellipse(x * s, y * s, r * s, r * s))


def bound(c):
    s = c.w / 256
    for k in range(3):                                                     # 三道缠绕的绳
        y = 70 + k * 56
        band = spline([(36 * s, (y + 24) * s), (128 * s, (y - 14) * s), (220 * s, (y + 24) * s)])
        c.stroke(band, 30 * s, taper=0)
        for j in range(6):
            p = band[int((j + 0.5) / 6 * (len(band) - 1))]
            c.erase(c.stroke([(p[0] - 8 * s, p[1] - 12 * s), (p[0] + 8 * s, p[1] + 12 * s)], 4 * s, add=False, taper=0))
    c.add(c.ellipse(128 * s, 214 * s, 26 * s, 22 * s))                      # 结
    c.stroke([(128 * s, 214 * s), (106 * s, 246 * s)], 12 * s, taper=(0.0, 0.6))
    c.stroke([(128 * s, 214 * s), (154 * s, 248 * s)], 12 * s, taper=(0.0, 0.6))


def ward(c):
    defend(c, cross=True)


def fear(c):
    s = c.w / 256
    for r, w in ((104, 16), (72, 15), (40, 14)):
        c.stroke(ellipse_pts(128 * s, 128 * s, r * s, r * s), w * s, closed=True, taper=0)
    c.add(c.ellipse(128 * s, 128 * s, 14 * s, 14 * s))


def stunned(c):
    s = c.w / 256
    W, H = 104 * s, 60 * s
    cx, cy = 128 * s, 128 * s
    upper = spline([(cx - W, cy, 'c'), (cx - W * 0.4, cy - H), (cx + W * 0.4, cy - H), (cx + W, cy, 'c')])
    lower = spline([(cx + W, cy, 'c'), (cx + W * 0.4, cy + H * 0.85), (cx - W * 0.4, cy + H * 0.85), (cx - W, cy, 'c')])
    c.add(c.mask(np.vstack([upper, lower])))
    c.erase(c.mask(np.vstack([upper, lower]) * 0.78 + np.array([cx, cy]) * 0.22))
    c.add(c.ellipse(cx, cy, 30 * s, 30 * s))
    c.erase(c.stroke([(40 * s, 216 * s), (216 * s, 40 * s)], 30 * s, add=False, taper=0))
    c.stroke([(48 * s, 208 * s), (208 * s, 48 * s)], 16 * s, taper=0)


# ------------------------------------------------------------------ 敌人被动

def scent(c):
    """嗅到疯狂：一个鼻子的两只鼻孔 + 一点（在游戏里着深渊青）。"""
    s = c.w / 256
    nose = [(128, 28), (150, 110), (196, 168), (184, 206), (128, 214), (72, 206), (60, 168), (106, 110)]
    _fill(c, [(x * s, y * s) for x, y in nose])
    for sd in (-1, 1):
        c.erase(c.ellipse((128 + sd * 30) * s, 182 * s, 16 * s, 11 * s, rot=sd * 20))
    c.add(c.ellipse(212 * s, 56 * s, 22 * s, 22 * s))


def glare(c):
    """刺目强光：灯塔的灯室，四周放光。"""
    s = c.w / 256
    room = [(92, 92, 'c'), (164, 92, 'c'), (172, 176, 'c'), (84, 176, 'c')]
    _fill(c, [(x * s, y * s, k) for x, y, k in room], smooth=False)
    c.erase(c.mask(poly([(104 * s, 104 * s), (152 * s, 104 * s), (156 * s, 164 * s), (100 * s, 164 * s)])))
    c.add(c.ellipse(128 * s, 134 * s, 16 * s, 20 * s))
    _fill(c, [(70 * s, 92 * s, 'c'), (128 * s, 48 * s, 'c'), (186 * s, 92 * s, 'c')], smooth=False)
    c.stroke([(76 * s, 186 * s), (180 * s, 186 * s)], 14 * s, taper=0)
    for a in range(0, 360, 30):
        q = math.radians(a)
        if 60 < a < 120:
            continue
        c.stroke([(128 * s + 84 * s * math.cos(q), 134 * s + 84 * s * math.sin(q)),
                  (128 * s + 118 * s * math.cos(q), 134 * s + 118 * s * math.sin(q))], 10 * s, taper=(0.0, 0.7))


def faceless(c):
    """不可名状：修女头巾里一张光滑的白面。"""
    s = c.w / 256
    hood = [(128, 20), (196, 60), (222, 150), (212, 240, 'c'), (44, 240, 'c'), (34, 150), (60, 60)]
    _fill(c, [(x * s, y * s) + tuple(k) for x, y, *k in hood])
    c.erase(c.ellipse(128 * s, 128 * s, 56 * s, 76 * s))


def tide(c):
    """潮位：一根竖的水位计，刻度 + 半管水 + 顶上一道浪。"""
    s = c.w / 256
    c.stroke(poly([(92 * s, 30 * s), (92 * s, 226 * s), (164 * s, 226 * s), (164 * s, 30 * s)]), 14 * s, taper=0)
    c.add(c.mask(poly([(92 * s, 130 * s), (164 * s, 130 * s), (164 * s, 226 * s), (92 * s, 226 * s)])))
    c.erase(c.stroke(spline([(100 * s, 134 * s), (116 * s, 124 * s), (132 * s, 136 * s), (148 * s, 124 * s), (160 * s, 132 * s)]), 5 * s,
                     add=False, taper=0))
    for k in range(6):
        y = 46 + k * 32
        c.stroke([(166 * s, y * s), (200 * s, y * s)], (8 if k % 2 == 0 else 5) * s, taper=0)


# ------------------------------------------------------------------ 遗物：鲸油灯（样张三件套之一）

def whale_oil_lamp(c):
    s = c.w / 256
    # 底座、细柄、玻璃油壶（留白）、灯头、细长的灯罩
    c.add(c.mask(spline([(72 * s, 236 * s), (184 * s, 236 * s), (170 * s, 214 * s), (86 * s, 214 * s)], closed=True)))
    c.stroke([(128 * s, 216 * s), (128 * s, 168 * s)], 16 * s, taper=0)
    c.add(c.ellipse(128 * s, 182 * s, 22 * s, 8 * s))
    font = spline([(128 * s, 168 * s), (82 * s, 150 * s), (74 * s, 116 * s), (96 * s, 92 * s), (160 * s, 92 * s), (182 * s, 116 * s),
                   (174 * s, 150 * s)], closed=True)
    m = c.mask(font)
    c.add(m)
    c.erase(c.mask(font * 0.8 + np.array([128 * s, 124 * s]) * 0.2))
    c.add(c.mask(poly([(86 * s, 136 * s), (170 * s, 136 * s), (164 * s, 152 * s), (128 * s, 162 * s), (92 * s, 152 * s)])))   # 油
    c.erase(c.stroke([(96 * s, 112 * s), (104 * s, 140 * s)], 5 * s, add=False, taper=0.3))
    c.add(c.mask(poly([(104 * s, 92 * s), (152 * s, 92 * s), (146 * s, 76 * s), (110 * s, 76 * s)])))   # 灯头
    chimney = spline([(112 * s, 76 * s), (106 * s, 50 * s), (114 * s, 24 * s), (142 * s, 24 * s), (150 * s, 50 * s), (144 * s, 76 * s)],
                     closed=True)
    c.stroke(chimney, 8 * s, closed=True, taper=0)
    c.add(c.mask(spline([(128 * s, 34 * s), (138 * s, 54 * s), (134 * s, 70 * s), (122 * s, 70 * s), (118 * s, 54 * s)], closed=True)))
    c.erase(c.ellipse(128 * s, 60 * s, 4 * s, 7 * s))


I = 128      # 意图 / 遗物导出尺寸
S = 64       # 状态导出尺寸
SPRITES = {
    'intent_attack': (I, I, attack),
    'intent_defend': (I, I, defend),
    'intent_buff': (I, I, buff),
    'intent_debuff': (I, I, debuff),
    'intent_mind': (I, I, mind),
    'intent_summon': (I, I, summon),
    'intent_unknown': (I, I, unknown),
    'intent_stun': (I, I, stun),
    'icon_block': (I, I, block),
    'status_strength': (S, S, strength),
    'status_dexterity': (S, S, dexterity),
    'status_vulnerable': (S, S, vulnerable),
    'status_weak': (S, S, weak),
    'status_frail': (S, S, frail),
    'status_bound': (S, S, bound),
    'status_ward': (S, S, ward),
    'status_fear': (S, S, fear),
    'status_stun': (S, S, stunned),
    'passive_scent': (S, S, scent),
    'passive_glare': (S, S, glare),
    'passive_faceless': (S, S, faceless),
    'passive_tide': (S, S, tide),
    'relic_whale_oil_lamp': (I, I, whale_oil_lamp),
}
