"""地图节点（策划/10 §8）：源 128（Boss 320），实墨块 + 留白，剪影可读。
普通战斗 = 交叉的匕首 ／ 精英 = 獠牙面具 ／ 事件 = ? ／ 商店 = 天平 ／ 篝火 = 火盆 ／ 宝箱 = 铁箍箱 ／ Boss = 半沉的礼拜堂。
"""
import math

import numpy as np

from etch import ellipse_pts, poly, spline


def _dagger(c, s, ang, flip):
    a = math.radians(ang)
    d = np.array([math.cos(a), math.sin(a)])
    n = np.array([-d[1], d[0]])
    o = np.array([128, 128]) * s - d * 10 * s
    tip = o + d * 112 * s
    blade = [o + n * 14 * s, tip - d * 30 * s + n * 9 * s, tip, tip - d * 30 * s - n * 9 * s, o - n * 14 * s]
    c.add(c.mask(np.array(blade)))
    c.add(c.mask(np.array([o + n * 34 * s, o + n * 34 * s - d * 12 * s, o - n * 34 * s - d * 12 * s, o - n * 34 * s])))
    g = o - d * 12 * s
    c.add(c.mask(np.array([g + n * 9 * s, g + n * 9 * s - d * 52 * s, g - n * 9 * s - d * 52 * s, g - n * 9 * s])))
    p = g - d * 62 * s
    c.add(c.ellipse(p[0], p[1], 14 * s, 14 * s))


def battle(c):
    s = c.w / 256
    _dagger(c, s, -45, False)
    _dagger(c, s, -135, True)
    c.erase(c.stroke([(118 * s, 118 * s), (138 * s, 138 * s)], 8 * s, add=False, taper=0))


def elite(c):
    """獠牙面具：一张宽脸面具，两个斜眼洞，嘴里两排獠牙往外龇。"""
    s = c.w / 256
    mask = [(128, 20), (206, 48), (230, 120), (210, 196), (128, 240, 'c'), (46, 196), (26, 120), (50, 48)]
    c.add(c.mask(spline([(x * s, y * s) + tuple(r) for x, y, *r in mask], closed=True)))
    for sd in (-1, 1):
        eye = [(128 + sd * 22, 104), (128 + sd * 76, 84), (128 + sd * 70, 120), (128 + sd * 30, 124)]
        c.erase(c.mask(np.array(eye) * s))
    c.erase(c.mask(spline([(70 * s, 166 * s), (128 * s, 150 * s), (186 * s, 166 * s), (160 * s, 200 * s), (96 * s, 200 * s)],
                          closed=True)))
    for k, x in enumerate((84, 106, 128, 150, 172)):
        h = 30 if k in (0, 4) else 18
        c.add(c.mask(np.array([((x - 9) * s, 158 * s), ((x + 9) * s, 158 * s), (x * s, (158 + h) * s)])))
    for x in (100, 156):
        c.add(c.mask(np.array([((x - 8) * s, 204 * s), ((x + 8) * s, 204 * s), (x * s, 172 * s)])))
    c.erase(c.stroke(spline([(128 * s, 40 * s), (124 * s, 80 * s), (132 * s, 100 * s)]), 4 * s, add=False, taper=0.3))   # 额上的裂纹


def event(c):
    s = c.w / 256
    c.stroke(spline([(80 * s, 92 * s), (96 * s, 52 * s), (140 * s, 36 * s), (180 * s, 62 * s), (174 * s, 104 * s), (136 * s, 132 * s),
                     (126 * s, 166 * s)]), 34 * s, taper=(0.15, 0.0))
    c.add(c.ellipse(126 * s, 212 * s, 22 * s, 22 * s))


def shop(c):
    """天平：立柱、横梁、两只秤盘。"""
    s = c.w / 256
    c.stroke([(128 * s, 40 * s), (128 * s, 216 * s)], 14 * s, taper=0)
    c.add(c.mask(poly([(80 * s, 216 * s), (176 * s, 216 * s), (190 * s, 240 * s), (66 * s, 240 * s)])))
    c.stroke([(36 * s, 70 * s), (220 * s, 62 * s)], 12 * s, taper=0)
    c.add(c.ellipse(128 * s, 50 * s, 16 * s, 16 * s))
    for x, y in ((48, 70), (208, 62)):
        for dx in (-28, 28):
            c.stroke([(x * s, y * s), ((x + dx) * s, (y + 80) * s)], 3.6 * s, taper=0)
        pan = ellipse_pts(x * s, (y + 84) * s, 40 * s, 14 * s, a0=0, a1=180)
        c.add(c.mask(np.vstack([pan, [[(x - 40) * s, (y + 80) * s]]])))


def rest(c):
    """火盆：三脚的铁盆，盆里一团火。"""
    s = c.w / 256
    flame = [(128, 24), (160, 70), (176, 110), (160, 140), (96, 140), (80, 110), (100, 74), (112, 92)]
    c.add(c.mask(spline([(x * s, y * s) for x, y in flame], closed=True)))
    c.erase(c.mask(spline([(128 * s, 70 * s), (144 * s, 104 * s), (138 * s, 132 * s), (118 * s, 132 * s), (112 * s, 104 * s)],
                          closed=True)))
    bowl = ellipse_pts(128 * s, 140 * s, 88 * s, 46 * s, a0=0, a1=180)
    c.add(c.mask(np.vstack([bowl, [[40 * s, 140 * s]]])))
    c.erase(c.stroke(ellipse_pts(128 * s, 150 * s, 70 * s, 26 * s, a0=20, a1=160), 4 * s, add=False, taper=0.2))
    for x0, x1 in ((80, 56), (128, 128), (176, 200)):
        c.stroke([(x0 * s, 176 * s), (x1 * s, 236 * s)], 12 * s, taper=0)


def treasure(c):
    """铁箍箱：拱顶的木箱，两道铁箍，正中一把锁。"""
    s = c.w / 256
    lid = spline([(40 * s, 110 * s), (60 * s, 60 * s), (128 * s, 44 * s), (196 * s, 60 * s), (216 * s, 110 * s)])
    c.add(c.mask(np.vstack([lid, [[216 * s, 110 * s], [40 * s, 110 * s]]])))
    c.add(c.mask(poly([(40 * s, 118 * s), (216 * s, 118 * s), (216 * s, 220 * s), (40 * s, 220 * s)])))
    for x in (76, 180):                                                       # 铁箍：留白的竖条 + 铆钉
        c.erase(c.stroke([(x * s, 54 * s), (x * s, 218 * s)], 10 * s, add=False, taper=0))
        c.stroke([(x * s, 54 * s), (x * s, 218 * s)], 4 * s, taper=0)
    c.erase(c.stroke([(42 * s, 114 * s), (214 * s, 114 * s)], 6 * s, add=False, taper=0))
    c.erase(c.mask(poly([(110 * s, 104 * s), (146 * s, 104 * s), (146 * s, 150 * s), (110 * s, 150 * s)])))
    c.add(c.ellipse(128 * s, 120 * s, 8 * s, 8 * s))
    c.stroke([(128 * s, 120 * s), (128 * s, 140 * s)], 6 * s, taper=0)


def boss(c):
    """半沉的礼拜堂：尖顶的钟楼和一段山墙露出水面，玫瑰窗，水面一道道波纹。"""
    s = c.w / 640
    water = 430
    body = [(160, water), (160, 260), (320, 150, 'c'), (480, 260), (480, water)]
    c.add(c.mask(np.array([(x * s, y * s) for x, y, *_ in body])))
    tower = [(380, water), (380, 150), (420, 40, 'c'), (460, 150), (460, water)]
    c.add(c.mask(np.array([(x * s, y * s) for x, y, *_ in tower])))
    c.stroke([(420 * s, 40 * s), (420 * s, 0)], 8 * s, taper=0)
    c.stroke([(404 * s, 16 * s), (436 * s, 16 * s)], 8 * s, taper=0)
    c.erase(c.ellipse(320 * s, 280 * s, 56 * s, 56 * s))
    c.add(c.ellipse(320 * s, 280 * s, 16 * s, 16 * s))
    for j in range(8):
        q = 2 * math.pi * j / 8
        c.stroke([(320 * s + 18 * s * math.cos(q), 280 * s + 18 * s * math.sin(q)),
                  (320 * s + 54 * s * math.cos(q), 280 * s + 54 * s * math.sin(q))], 6 * s, taper=0)
    for x in (200, 250):                                                       # 尖拱窗
        c.erase(c.mask(spline([(x * s, 400 * s), (x * s, 340 * s), ((x + 18) * s, 314 * s, 'c'), ((x + 36) * s, 340 * s),
                               ((x + 36) * s, 400 * s)], closed=True)))
    c.erase(c.mask(spline([(404 * s, 230 * s), (404 * s, 190 * s), (420 * s, 168 * s, 'c'), (436 * s, 190 * s), (436 * s, 230 * s)],
                          closed=True)))
    for k in range(4):                                                         # 水面：一道道波纹，越往下越宽
        y = water + 18 + k * 40
        x0, x1 = 120 - k * 30, 520 + k * 30
        c.stroke(spline([(x * s, (y + 8 * math.sin(x / 30 + k)) * s) for x in np.linspace(x0, x1, 16)]), (16 - k * 2.5) * s, taper=(0.2, 0.2))
    c.erase(c.stroke(spline([(160 * s, (water + 2) * s), (320 * s, (water - 4) * s), (480 * s, (water + 2) * s)]), 6 * s, add=False, taper=0))


SPRITES = {
    'node_battle': (128, 128, battle),
    'node_elite': (128, 128, elite),
    'node_event': (128, 128, event),
    'node_shop': (128, 128, shop),
    'node_rest': (128, 128, rest),
    'node_treasure': (128, 128, treasure),
    'node_boss': (320, 320, boss),
}
