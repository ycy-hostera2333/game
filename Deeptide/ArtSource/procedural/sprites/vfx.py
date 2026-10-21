"""序列帧特效（策划/10 §9）：命中的墨点喷溅（3 种 × 6 帧，256²）、格挡被击破（6 帧，256²）。12 帧 / 秒播放。

每组按同一个随机种子生成「这一下溅出去的所有墨滴」，逐帧推进：
第 0 帧中心一团墨；1–2 帧往外炸开成尖刺和飞出去的墨滴；3–5 帧墨滴飞远、拉长、变小，中心那团被纸吃掉，最后只剩几点。
"""
import math

import numpy as np

from etch import ellipse_pts, spline

FRAMES = 6


def hit(seed):
    rng = np.random.default_rng(seed)
    n = 26
    angles = rng.uniform(0, 2 * math.pi, n)
    speeds = rng.uniform(0.45, 1.0, n)
    sizes = rng.uniform(0.5, 1.0, n)
    spikes = sorted(rng.uniform(0, 2 * math.pi, 9))
    spike_len = rng.uniform(0.5, 1.0, 9)

    def frame(t):
        def draw(c):
            s = c.w / 256
            cx = cy = 128 * s
            p = t / (FRAMES - 1)                                               # 0 → 1
            # 中心的墨团：先胀大，后半段被纸吃掉（用噪声挖洞）
            blob_r = (26 + 34 * min(p * 3, 1)) * s
            pts = []
            for j, a in enumerate(np.linspace(0, 2 * math.pi, 40, endpoint=False)):
                bump = 1 + 0.18 * math.sin(5 * a + seed) + 0.1 * math.sin(11 * a + seed * 2)
                pts.append((cx + blob_r * bump * math.cos(a), cy + blob_r * bump * math.sin(a)))
            blob = c.mask(spline(pts, closed=True))
            thr = 1.15 - 1.3 * max(p - 0.3, 0) / 0.7                                    # 墨团被纸吃掉：阈值越来越低，洞越来越多
            eaten = np.clip(((c.noise(10 * s, seed) + 1) / 2 - thr) * 14, 0, 1) if p > 0.3 else 0
            c.add(np.clip(blob - eaten, 0, 1))
            # 尖刺：从墨团往外扎，第 1–3 帧最长
            grow = math.sin(min(p * 1.6, 1) * math.pi)
            for a, L in zip(spikes, spike_len):
                if grow <= 0.02:
                    continue
                r0 = blob_r * 0.8
                r1 = blob_r + L * 70 * s * grow
                w = 12 * s * (1 - 0.5 * p)
                c.add(c.mask(np.array([(cx + r0 * math.cos(a - 0.12), cy + r0 * math.sin(a - 0.12)),
                                       (cx + r1 * math.cos(a), cy + r1 * math.sin(a)),
                                       (cx + r0 * math.cos(a + 0.12), cy + r0 * math.sin(a + 0.12))])))
                c.add(c.ellipse(cx + (r1 + 6 * s) * math.cos(a), cy + (r1 + 6 * s) * math.sin(a), w * 0.45, w * 0.45))
            # 飞出去的墨滴：越飞越远、越小，后段拉成泪滴
            if p > 0.1:
                for a, v, z in zip(angles, speeds, sizes):
                    r = (blob_r + 110 * s * v * min((p - 0.1) / 0.75, 1) ** 0.7)
                    if r > 124 * s:
                        continue
                    rad = z * 9 * s * (1 - 0.6 * p)
                    x, y = cx + r * math.cos(a), cy + r * math.sin(a)
                    tail = rad * (1 + 3 * p)
                    c.add(c.ellipse(x, y, rad + tail * 0.5, rad, rot=math.degrees(a)))
        return draw
    return frame


def shield_outline(s):
    pts = [(128, 30), (196, 52), (214, 60, 'c'), (208, 140), (174, 200), (128, 232, 'c'), (82, 200), (48, 140), (42, 60, 'c'), (60, 52)]
    return spline([(x * s, y * s) + tuple(r) for x, y, *r in pts], closed=True)


def shatter(c, t):
    s = c.w / 256
    p = t / (FRAMES - 1)
    outline = (shield_outline(s) - [128 * s, 132 * s]) * 0.62 + [128 * s, 132 * s]
    n = len(outline)
    pieces = 9
    rng = np.random.default_rng(4)
    cx, cy = 128 * s, 132 * s
    for k in range(pieces):
        seg = outline[int(k * n / pieces): int((k + 1) * n / pieces) + 1]
        m = seg.mean(0)
        d = (m - [cx, cy]) / max(np.hypot(*(m - [cx, cy])), 1)
        fly = 52 * s * p ** 0.8 * rng.uniform(0.7, 1.2)
        rot = math.radians(rng.uniform(-60, 60) * p)
        R = np.array([[math.cos(rot), -math.sin(rot)], [math.sin(rot), math.cos(rot)]])
        piece = (seg - m) @ R.T * (1 - 0.35 * p) + m + d * fly
        if t == 0:
            piece = seg
        c.stroke(piece, (16 - 6 * p) * s, taper=0)
    if t <= 1:                                                                  # 第 0、1 帧：盾面上的裂纹
        for a in (-1.2, -0.3, 0.6, 1.6, 2.6):
            pts = [(cx, cy)]
            for j in range(4):
                a += rng.uniform(-0.4, 0.4)
                pts.append((pts[-1][0] + 14 * s * math.cos(a), pts[-1][1] + 14 * s * math.sin(a)))
            c.stroke(np.array(pts), 5 * s, taper=(0.0, 0.7))
    if t >= 2:                                                                  # 飞散的碎屑
        for _ in range(14):
            a = rng.uniform(0, 2 * math.pi)
            r = (50 + 50 * p) * s * rng.uniform(0.6, 1.1)
            c.add(c.ellipse(cx + r * math.cos(a), cy + r * math.sin(a), 5 * s * (1.2 - p), 5 * s * (1.2 - p)))


SPRITES = {}
for v, seed in (('a', 11), ('b', 23), ('c', 37)):
    f = hit(seed)
    for t in range(FRAMES):
        SPRITES[f'vfx_hit_{v}_{t}'] = (256, 256, f(t))
for t in range(FRAMES):
    SPRITES[f'vfx_break_{t}'] = (256, 256, (lambda tt: (lambda c: shatter(c, tt)))(t))
