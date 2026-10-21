"""卡框套件（策划/10 §4.1–§4.3、§4.5）：四档稀有度的框饰、癫狂面的侵蚀变体、四种插图窗的线脚、三个流派徽记。

全部是单色精灵，在卡面上由 Image 着色（清醒面墨色、癫狂面发光青），换色板不用重画。
- frame_<稀有度>：整张卡 720 × 1008 的覆盖层（卡面源尺寸），画外框和框饰；名字旗带的底色和文字仍由卡面自己画，这里只画旗带两头折回去的尾巴
  基础 = 单线框；普通 = 单线框 + 四角铆饰；罕见 = 双线框 + 卡名单层旗带；稀有 = 双线框 + 蚀刻花边 + 卡名双层旗带（10 §4.2）
- frame_erosion：癫狂面才显现的「侵蚀」：边框上长出藤壶和小触须（10 §4.3）
- window_<窗形>：插图窗的线脚，和 Shapes.Window 的几何一致，盖在卡图上面
- seal_<流派>：卡名左边的徽记（守夜 = 提灯 ／ 堕落 = 裂眼 ／ 传染 = 波纹）
"""
import math

import numpy as np

from etch import ellipse_pts, poly, resample, spline

CW, CH = 720, 1008          # 卡面源尺寸

# 卡面上的位置（CardView 的 Top(x, y, w, h)，左上为原点）
BANNER = (196, 38, 430, 68)
BANNER_BACK = (204, 46, 430, 68)


def rrect(x0, y0, x1, y1, r, n=40):
    """圆角矩形的点列（顺时针）。"""
    pts = []
    for cx, cy, a0 in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
        for t in np.linspace(a0, a0 + 90, n):
            pts.append((cx + r * math.cos(math.radians(t)), cy + r * math.sin(math.radians(t))))
    return np.array(pts)


def K(c):
    return c.w / CW                                                         # 工作画布 / 卡面源尺寸


def border(c, inset, width, r=36):
    k = K(c)
    pts = rrect(inset * k, inset * k, c.w - inset * k, c.h - inset * k, max(r - inset, 6) * k)
    c.stroke(pts, width * k, closed=True, taper=0, wobble=0.04)


def rivet(c, x, y, r):
    k = K(c)
    x, y, r = x * k, y * k, r * k
    c.add(c.ellipse(x, y, r, r))
    c.erase(c.ellipse(x - r * 0.3, y - r * 0.3, r * 0.32, r * 0.32))       # 高光
    c.erase(c.stroke(ellipse_pts(x, y, r * 0.7, r * 0.7, a0=200, a1=320), r * 0.12, add=False, taper=0.3))
    for a in (45, 135, 225, 315):                                           # 四瓣花托
        q = math.radians(a)
        c.add(c.ellipse(x + r * 1.45 * math.cos(q), y + r * 1.45 * math.sin(q), r * 0.42, r * 0.42))


def corner_flourish(c, x, y, sx, sy, size):
    """角花：贴着内框角的一个卷草。sx、sy = ±1 表示朝哪个角。"""
    k = K(c)
    s = size * k
    X = lambda u, v: (x * k + sx * u * s, y * k + sy * v * s)
    c.stroke(spline([X(0, 1.0), X(0, 0.25), X(0.25, 0), X(1.0, 0)]), 3.2 * k, taper=(0.3, 0.3))
    c.stroke(spline([X(0.18, 0.62), X(0.18, 0.2), X(0.4, 0.14), X(0.52, 0.3), X(0.36, 0.4)]), 2.6 * k, taper=(0.1, 0.6))
    c.stroke(spline([X(0.62, 0.18), X(0.2, 0.18)]), 2.2 * k, taper=0.3)
    c.add(c.ellipse(*X(0.36, 0.4), 3.2 * k, 3.2 * k))


def banner_tails(c, box, layer_depth=0):
    """旗带两头折到后面的尾巴：一段斜着的布，末端剪成燕尾，折痕处一道暗面。"""
    k = K(c)
    x, y, w, h = box
    for sd in (-1, 1):
        ex = x if sd < 0 else x + w
        fold = [(ex, y + 10), (ex + sd * 34, y + 22), (ex + sd * 34, y + h + 14), (ex, y + h - 4)]
        tail = [(ex + sd * 30, y + 22), (ex + sd * 92, y + 18), (ex + sd * 70, y + h * 0.6 + 16), (ex + sd * 92, y + h + 18),
                (ex + sd * 30, y + h + 14)]
        tm = c.mask(np.array(tail) * k)
        c.erase(tm)                                                         # 尾巴是纸色（只画边和排线）
        c.add(c.lines(c.ph_lin(-60 * sd, wobble=0.3), 7 * k, 1.6 * k) * tm * 0.8)
        c.stroke(np.array(tail + tail[:1]) * k, 3.4 * k, taper=0)
        fm = c.mask(np.array(fold) * k)
        c.add(fm * 0.85)                                                    # 折回去的一截在暗处
        c.stroke(np.array(fold + fold[:1]) * k, 3.0 * k, taper=0)


def lace(c, inset, step=22):
    """蚀刻花边：沿两道框之间走的一串扇贝弧 + 小点。"""
    k = K(c)
    path = rrect(inset * k, inset * k, c.w - inset * k, c.h - inset * k, max(36 - inset, 8) * k, n=30)
    path = resample(np.vstack([path, path[:1]]), step * k / 2)
    t = np.gradient(path, axis=0)
    t /= np.maximum(np.hypot(t[:, 0], t[:, 1]), 1e-6)[:, None]
    nrm = np.stack([t[:, 1], -t[:, 0]], 1)                                  # 指向卡外
    for i in range(0, len(path) - 2, 2):
        a, b = path[i], path[i + 2]
        m = (a + b) / 2 - nrm[i + 1] * 3.5 * k
        c.stroke(spline([tuple(a), tuple(m), tuple(b)]), 1.8 * k, taper=0.2)
        c.add(c.ellipse(*(path[i + 1] + nrm[i + 1] * 2.5 * k), 1.6 * k, 1.6 * k))


# ====================================================================== 四档框饰

def frame_basic(c):
    border(c, 4, 7)


def frame_common(c):
    border(c, 4, 7)
    for x, y in ((40, 40), (CW - 40, 40), (40, CH - 40), (CW - 40, CH - 40)):
        rivet(c, x, y, 9)


def frame_uncommon(c):
    border(c, 4, 7)
    border(c, 17, 3.2)
    for x, y, sx, sy in ((24, 24, 1, 1), (CW - 24, 24, -1, 1), (24, CH - 24, 1, -1), (CW - 24, CH - 24, -1, -1)):
        corner_flourish(c, x, y, sx, sy, 70)
    banner_tails(c, BANNER)


def frame_rare(c):
    border(c, 3, 9)
    border(c, 19, 3.2)
    lace(c, 11)
    for x, y in ((24, 24), (CW - 24, 24), (24, CH - 24), (CW - 24, CH - 24)):   # 四角的星形徽章
        k = K(c)
        c.add(c.ellipse(x * k, y * k, 15 * k, 15 * k))
        c.erase(c.ellipse(x * k, y * k, 11 * k, 11 * k))
        pts = []
        for j in range(16):
            r = (10 if j % 2 == 0 else 4) * k
            q = math.pi * j / 8
            pts.append((x * k + r * math.cos(q), y * k + r * math.sin(q)))
        c.add(c.mask(np.array(pts)))
    for x, y, sx, sy in ((30, 30, 1, 1), (CW - 30, 30, -1, 1), (30, CH - 30, 1, -1), (CW - 30, CH - 30, -1, -1)):
        corner_flourish(c, x + sx * 16, y + sy * 16, sx, sy, 64)
    banner_tails(c, BANNER_BACK)
    banner_tails(c, BANNER)


def frame_erosion(c):
    """癫狂面的侵蚀：左右边框和底边上长出一簇簇藤壶，两条小触须从角上卷进来。"""
    rng = np.random.default_rng(7)
    k = K(c)
    clusters = [(8, 260, 5), (12, 520, 4), (8, 760, 6), (CW - 8, 330, 5), (CW - 10, 640, 4), (CW - 8, 880, 5), (300, CH - 8, 4),
                (520, CH - 6, 5), (140, 8, 3)]
    for cx, cy, n in clusters:
        for _ in range(n):
            x = cx + rng.normal(0, 14)
            y = cy + rng.normal(0, 22)
            s = rng.uniform(9, 17)
            cone = [(x - s, y + s * 0.4), (x - s * 0.5, y - s * 0.6), (x + s * 0.5, y - s * 0.6), (x + s, y + s * 0.4)]
            a = math.atan2(y - CH / 2, x - CW / 2)
            rot = np.array([[math.cos(a + math.pi / 2), -math.sin(a + math.pi / 2)], [math.sin(a + math.pi / 2), math.cos(a + math.pi / 2)]])
            P = (np.array(cone) - [x, y]) @ rot.T + [x, y]
            m = c.mask(spline([tuple(p) for p in P * k], closed=True))
            c.add(m)
            top = ([x, y] + (np.array([0, -s * 0.6]) @ rot.T)) * k
            c.erase(c.ellipse(top[0], top[1], s * 0.42 * k, s * 0.2 * k, rot=math.degrees(a)))
            c.erase(c.stroke(spline([tuple(p) for p in P[:2] * k]), 1.6 * k, add=False, taper=0.3))
    from cards._common import tentacle
    for ctrl, w in (([(0, CH - 120), (40, CH - 170), (30, CH - 240), (70, CH - 280), (96, CH - 250)], 26),
                    ([(CW, 170), (CW - 46, 200), (CW - 40, 250), (CW - 80, 270)], 20)):
        m = tentacle(c, [(x * k, y * k) for x, y in ctrl], w * k, tone=1.0, taper=0.9, outline=2.0 * k)
        c.add(m)


# ====================================================================== 插图窗线脚（和 Shapes.Window 一致的几何，导出尺寸 = 卡面上的窗）

def _window_sdf(c, kind, ww, wh):
    """窗形到边缘的距离（像素，负 = 窗内），照 Shapes.Window 的公式按工作画布换算。"""
    s = c.w / ww                                                            # 工作画布 / 窗尺寸
    hw, hh = ww / 2, wh / 2                                                 # Shapes 里的半尺寸
    x = c.xx / s / 2
    y = (c.h - c.yy) / s / 2
    if kind == 'arch':
        pad = 2
        half = hw / 2 - pad
        spring = hh * 0.62
        px = np.abs(x - hw / 2)
        u = np.clip(px / half, 0, 1)
        rise = hh - pad - spring
        top = spring + rise * (1 - u ** 1.5)
        slope = rise * 1.5 * np.sqrt(u) / half
        d = np.maximum(np.maximum((y - top) / np.sqrt(1 + slope * slope), px - half), pad - y)
    elif kind == 'circle':
        d = np.hypot(x - hh / 2, y - hh / 2) - (hh / 2 - 2)
    elif kind == 'blot':
        px, py = (x - hw / 2) / 1.4, y - hh / 2
        a = np.arctan2(py, px)
        r = 92 * (1 + 0.08 * np.sin(6 * a + 0.5) + 0.05 * np.sin(13 * a + 2) + 0.03 * np.sin(23 * a))
        d = np.hypot(px, py) - r
    else:
        px, py = np.abs(x - hw / 2), np.abs(y - hh / 2)
        box = np.maximum(px - (hw / 2 - 2), py - (hh / 2 - 2))
        corner = (px + py - (hw / 2 + hh / 2 - 26)) * 0.7071
        d = np.maximum(box, corner)
    return d * 2 * s                                                        # → 工作画布像素


def _band(d, a, b, aa=1.0):
    """离边缘 a 到 b 像素（向内为正）之间的一条带。"""
    inside = -d
    return np.clip(np.minimum(inside - a + aa, b - inside + aa) / aa, 0, 1) * (inside >= a - aa) * (inside <= b + aa)


def window(kind, ww, wh):
    def draw(c):
        k = c.w / ww
        d = _window_sdf(c, kind, ww, wh)
        if kind == 'blot':
            # 诅咒的墨渍窗：一道粗的毛边 + 往外溅的墨点
            edge = d + 3 * k * c.noise(6 * k, 3)
            c.add(_band(edge, -2 * k, 7 * k, 1.2))
            rng = np.random.default_rng(11)
            for _ in range(40):
                a = rng.uniform(0, 2 * math.pi)
                r = rng.uniform(8, 26) * k
                cx, cy = c.w / 2, c.h / 2
                rx = 92 * 1.4 * 2 * k * (1 + 0.08 * math.sin(6 * math.atan2(math.sin(a), math.cos(a) / 1.4) + 0.5))
                ry = 92 * 2 * k
                x = cx + (rx + r) * math.cos(a)
                y = cy + (ry + r) * math.sin(a)
                if 0 < x < c.w and 0 < y < c.h:
                    s = rng.uniform(1.5, 4.5) * k
                    c.add(c.ellipse(x, y, s, s))
            return
        c.add(_band(d, -1 * k, 6 * k))                                       # 主线
        c.add(_band(d, 11 * k, 13 * k))                                      # 内线
        # 两线之间：一串珠点（哥特线脚上的珠饰）
        ring = (-d > 7.5 * k) & (-d < 10.5 * k)
        beads = c.lines(np.arctan2(c.yy - c.h / 2, c.xx - c.w / 2) * c.w / 2, 10 * k, 4.5 * k) * ring
        c.add(beads * 0.9)
        if kind == 'arch':
            pass
        elif kind == 'bevel':                                                # 倒角上的小玫瑰
            for sx in (0.0, 1.0):
                for sy in (0.0, 1.0):
                    x = 18 * k + sx * (c.w - 36 * k)
                    y = 18 * k + sy * (c.h - 36 * k)
                    c.add(c.ellipse(x, y, 8 * k, 8 * k))
                    c.erase(c.ellipse(x, y, 3 * k, 3 * k))
                    for j in range(6):
                        q = math.pi * j / 3
                        c.add(c.ellipse(x + 11 * k * math.cos(q), y + 11 * k * math.sin(q), 3.5 * k, 3.5 * k))
        elif kind == 'circle':                                               # 圆窗外圈的 12 个刻痕（像玫瑰窗）
            cx, cy, r = c.w / 2, c.h / 2, c.w / 2 - 4 * k
            for j in range(12):
                q = 2 * math.pi * j / 12
                c.stroke([(cx + (r - 14 * k) * math.cos(q), cy + (r - 14 * k) * math.sin(q)),
                          (cx + (r - 2 * k) * math.cos(q), cy + (r - 2 * k) * math.sin(q))], 4 * k, taper=0)
    return draw


# ====================================================================== 流派徽记

def seal_vigil(c):
    """守夜：提灯。"""
    s = c.w / 256
    c.stroke(spline([(92 * s, 54 * s), (128 * s, 20 * s), (164 * s, 54 * s)]), 12 * s, taper=0)
    c.add(c.mask(poly([(84 * s, 54 * s), (172 * s, 54 * s), (160 * s, 78 * s), (96 * s, 78 * s)])))
    glass = spline([(96 * s, 78 * s), (82 * s, 140 * s), (96 * s, 196 * s), (160 * s, 196 * s), (174 * s, 140 * s), (160 * s, 78 * s)],
                   closed=True)
    c.stroke(glass, 12 * s, closed=True, taper=0)
    c.add(c.mask(spline([(128 * s, 96 * s), (146 * s, 140 * s), (138 * s, 172 * s), (118 * s, 172 * s), (110 * s, 140 * s)], closed=True)))
    c.add(c.mask(poly([(76 * s, 196 * s), (180 * s, 196 * s), (190 * s, 232 * s), (66 * s, 232 * s)])))


def seal_fallen(c):
    """堕落：一只裂开的眼。"""
    s = c.w / 256
    W, H = 112 * s, 60 * s
    cx, cy = 128 * s, 128 * s
    upper = spline([(cx - W, cy, 'c'), (cx - W * 0.4, cy - H), (cx + W * 0.4, cy - H), (cx + W, cy, 'c')])
    lower = spline([(cx + W, cy, 'c'), (cx + W * 0.4, cy + H * 0.85), (cx - W * 0.4, cy + H * 0.85), (cx - W, cy, 'c')])
    c.add(c.mask(np.vstack([upper, lower])))
    c.erase(c.mask(np.vstack([upper, lower]) * 0.76 + np.array([cx, cy]) * 0.24))
    c.add(c.ellipse(cx, cy, 30 * s, 30 * s))
    crack = [(150, 20), (136, 70), (156, 100), (120, 150), (140, 190), (118, 240)]
    c.erase(c.stroke(poly([(x * s, y * s) for x, y in crack]), 14 * s, add=False, taper=0))


def seal_contagion(c):
    """传染：一圈圈向外的波纹。"""
    s = c.w / 256
    for r, w in ((108, 18), (72, 16), (36, 14)):
        c.stroke(ellipse_pts(128 * s, 128 * s, r * s, r * s), w * s, closed=True, taper=0)
    c.add(c.ellipse(128 * s, 128 * s, 12 * s, 12 * s))


SPRITES = {
    'frame_basic': (CW, CH, frame_basic),
    'frame_common': (CW, CH, frame_common),
    'frame_uncommon': (CW, CH, frame_uncommon),
    'frame_rare': (CW, CH, frame_rare),
    'frame_erosion': (CW, CH, frame_erosion),
    'window_arch': (624, 432, window('arch', 624, 432)),
    'window_bevel': (624, 432, window('bevel', 624, 432)),
    'window_circle': (432, 432, window('circle', 432, 432)),
    'window_blot': (624, 432, window('blot', 624, 432)),
    'seal_vigil': (96, 96, seal_vigil),
    'seal_fallen': (96, 96, seal_fallen),
    'seal_contagion': (96, 96, seal_contagion),
}
