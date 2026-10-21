"""地图底图（策划/10 §8）：竖向卷动的羊皮纸海图，1920 × 3400。从下往上走：灰潮镇的港口 → 外海 → Boss 所在的深渊。

装饰都推到两边，中间约 1100 宽留给 7 列节点和连线：
- 下端：灰潮镇的海岸线（码头、房屋、教堂尖顶的俯视小图），等高线一样的岸边排线
- 两边：水深数字、罗盘玫瑰、恒向线（从罗盘放出去的直线）、一条海蛇（老海图的插画）
- 上端：深渊，一圈圈往里收的漩涡排线，越往上墨越重
- 四周：图框（双线 + 经纬刻度）、右下角一块写着「灰潮镇外海」的图题框
"""
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from etch import ellipse_pts, poly, smoothstep, spline

NAME = '地图底图'
SIZE = (1920, 3400)
GUIDES = False
MAX_INK = 0.5
MAX_MAD = 0.0


def text_mask(c, text, x, y, size, anchor='mm', font='C:/Windows/Fonts/NotoSerifSC-VF.ttf'):
    """一段字 → 覆盖率（地图上的字也是墨）。"""
    img = Image.new('L', (c.w, c.h), 0)
    try:
        f = ImageFont.truetype(font, size)
    except OSError:
        f = ImageFont.load_default()
    ImageDraw.Draw(img).text((x, y), text, fill=255, font=f, anchor=anchor)
    return np.asarray(img, np.float32) / 255


def draw(c):
    rng = np.random.default_rng(3)
    W, H = c.w, c.h

    # ------------------------------------------------------------ 羊皮纸：一点点不均匀的斑
    c.engrave(np.ones((H, W), np.float32), 0.06 + 0.1 * c.noise(240, 1) + 0.12 * c.radial(W / 2, H / 2, 900, 2000, sx=0.6),
              [c.ph_lin(30, wobble=4, seed=2)], spacing=16, wmax=1.8, solid=None)

    # ------------------------------------------------------------ 深渊：上端一圈圈往里收的漩涡
    ax, ay = W / 2, 330
    r = np.hypot(c.xx - ax, (c.yy - ay) * 1.4)
    a = np.arctan2((c.yy - ay) * 1.4, c.xx - ax)
    swirl = r + 60 * a
    depth = (1 - smoothstep(150, 900, r)) * (1 - smoothstep(900, 1300, c.yy))
    c.engrave(np.ones((H, W), np.float32), np.clip(0.15 + 0.85 * depth, 0, 1) * (1 - smoothstep(900, 1300, c.yy)),
              [swirl, c.ph_ray(ax, ay, r0=300)], spacing=14, wmax=2.6, swell=1.2, solid=None)

    # ------------------------------------------------------------ 海：水深数字（两边）和等深线
    for _ in range(140):
        x = rng.choice([rng.uniform(60, 400), rng.uniform(W - 400, W - 60)])
        y = rng.uniform(900, H - 700)
        depth_m = int(8 + (H - y) / 40 + rng.normal(0, 6))
        c.add(text_mask(c, str(max(depth_m, 3)), x, y, 26) * 0.8)
    for k in range(5):                                                         # 等深线：几条横跨的虚线
        y0 = 1300 + k * 420
        pts = [(x, y0 + 60 * math.sin(x / 300 + k) + 30 * math.sin(x / 97)) for x in np.linspace(0, W, 80)]
        line = spline(pts)
        for i in range(0, len(line) - 6, 12):
            c.stroke(line[i:i + 6], 2.2, taper=0)

    # ------------------------------------------------------------ 罗盘玫瑰 + 恒向线
    cx, cy = 300, 1500
    for j in range(32):                                                        # 恒向线：淡淡的长直线
        q = 2 * math.pi * j / 32
        c.stroke([(cx, cy), (cx + 3000 * math.cos(q), cy + 3000 * math.sin(q))], 1.2 if j % 4 else 2.0, taper=0)
    c.erase(c.ellipse(cx, cy, 180, 180))
    for rr, w in ((170, 4), (150, 2)):
        c.stroke(ellipse_pts(cx, cy, rr, rr), w, closed=True, taper=0)
    for j in range(16):
        q = 2 * math.pi * j / 16 - math.pi / 2
        L = 150 if j % 4 == 0 else (100 if j % 2 == 0 else 70)
        w = 26 if j % 4 == 0 else 16
        tip = (cx + L * math.cos(q), cy + L * math.sin(q))
        left = (cx + w * math.cos(q - math.pi / 2), cy + w * math.sin(q - math.pi / 2))
        right = (cx + w * math.cos(q + math.pi / 2), cy + w * math.sin(q + math.pi / 2))
        c.add(c.mask(poly([(cx, cy), left, tip])))                              # 一半实墨、一半留白：版画罗盘的画法
        c.stroke(poly([(cx, cy), right, tip, (cx, cy)]), 2.2, taper=0)
    c.add(text_mask(c, 'N', cx, cy - 200, 46))

    # ------------------------------------------------------------ 海蛇：右边一条老海图里的海怪
    sx, sy = 1620, 2050
    body = spline([(sx - 120, sy + 60), (sx - 60, sy - 40), (sx, sy + 40), (sx + 60, sy - 40), (sx + 120, sy + 40), (sx + 170, sy - 10)])
    c.stroke(body, 34, taper=(0.2, 0.7))
    c.erase(c.stroke(body, 10, add=False, taper=(0.2, 0.8)))
    head = [(sx - 120, sy + 60), (sx - 170, sy + 10), (sx - 210, sy + 20), (sx - 190, sy + 70), (sx - 140, sy + 84)]
    c.add(c.mask(spline(head, closed=True)))
    c.erase(c.ellipse(sx - 172, sy + 34, 7, 6))
    for k in range(5):                                                         # 背鳍
        x = sx - 70 + k * 50
        c.stroke([(x, sy + 10 * math.cos(k * 3)), (x + 10, sy - 50 + 10 * math.cos(k * 3))], 5, taper=(0.0, 0.8))
    for y in (sy + 70, sy + 90):                                               # 海蛇周围的浪
        c.stroke(spline([(x, y + 8 * math.sin(x / 30)) for x in np.linspace(sx - 260, sx + 230, 20)]), 2.4, taper=0.2)

    # ------------------------------------------------------------ 灰潮镇：下端的海岸线和俯视的小镇
    coast = [(0, 2800), (300, 2860), (520, 2780), (760, 2900), (980, 2860), (1200, 2920), (1420, 2840), (1650, 2900), (1920, 2830),
             (1920, 3400), (0, 3400)]
    land = c.mask(spline(coast, closed=True))
    c.engrave(land, 0.2 + 0.1 * c.noise(80, 5), [c.ph_lin(60, wobble=2, seed=6)], spacing=12, wmax=2)
    shore = spline(coast[:9])
    c.stroke(shore, 5, taper=0)
    for k in range(1, 5):                                                      # 岸外一圈圈的水线
        c.stroke(shore + np.array([0, -k * 16]), 2.4 - k * 0.4, taper=0)
    # 码头：伸进海里的几道栈桥
    for x in (700, 900, 1100):
        c.add(c.mask(poly([(x - 14, 2700), (x + 14, 2700), (x + 14, 2890), (x - 14, 2890)])) * 0.8)
    # 房子：一片片俯视的小屋顶（菱形排线），一座教堂的十字形屋顶
    for _ in range(90):
        x, y = rng.uniform(80, W - 80), rng.uniform(2960, 3300)
        w, h = rng.uniform(40, 80), rng.uniform(30, 60)
        roof = c.rect(x - w / 2, y - h / 2, x + w / 2, y + h / 2)
        c.paint(roof, 0.5 * c.lines(c.ph_lin(45, wobble=0), 6, 2) + 0.05)
        c.stroke(poly([(x - w / 2, y - h / 2), (x + w / 2, y - h / 2), (x + w / 2, y + h / 2), (x - w / 2, y + h / 2), (x - w / 2, y - h / 2)]),
                 2.2, taper=0)
    ch = (960, 3120)
    for dx, dy, w, h in ((0, 0, 60, 220), (0, -40, 180, 50)):
        m = c.rect(ch[0] + dx - w / 2, ch[1] + dy - h / 2, ch[0] + dx + w / 2, ch[1] + dy + h / 2)
        c.paint(m, 0.75)
    c.paint(c.rect(840, 3250, 1080, 3330), 0.0)
    c.add(text_mask(c, '灰潮镇', 960, 3290, 64) * 0.9)

    # ------------------------------------------------------------ 图框：双线 + 经纬刻度；图题框
    for inset, w in ((24, 6), (44, 2)):
        c.stroke(poly([(inset, inset), (W - inset, inset), (W - inset, H - inset), (inset, H - inset), (inset, inset)]), w, taper=0)
    for y in np.arange(44, H - 44, 100):
        c.add(c.rect(24, y, 44, y + 50) * (int(y / 100) % 2))
        c.add(c.rect(W - 44, y, W - 24, y + 50) * (int(y / 100) % 2))
    for x in np.arange(44, W - 44, 100):
        c.add(c.rect(x, 24, x + 50, 44) * (int(x / 100) % 2))
        c.add(c.rect(x, H - 44, x + 50, H - 24) * (int(x / 100) % 2))
    bx, by, bw, bh = 1480, 2560, 360, 180
    c.paint(c.rect(bx - bw / 2, by - bh / 2, bx + bw / 2, by + bh / 2), 0.0)
    for inset, w in ((0, 4), (10, 1.6)):
        c.stroke(poly([(bx - bw / 2 + inset, by - bh / 2 + inset), (bx + bw / 2 - inset, by - bh / 2 + inset),
                       (bx + bw / 2 - inset, by + bh / 2 - inset), (bx - bw / 2 + inset, by + bh / 2 - inset),
                       (bx - bw / 2 + inset, by - bh / 2 + inset)]), w, taper=0)
    c.add(text_mask(c, '灰潮镇外海', bx, by - 24, 46))
    c.add(text_mask(c, '水深以英寻计', bx, by + 40, 26) * 0.8)
