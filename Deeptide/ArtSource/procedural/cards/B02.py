"""B02 防御：风衣下摆扬起，挡住扑面而来的雾和碎石（策划/10 §4.6）。

调查员在画面左外侧，面朝右；厚呢风衣的下摆被甩起来，像一面弧形的盾占住画面左半边。
雾和碎石从右边扑过来，打在衣摆上。布料用「以左上为焦点的放射褶」来画：褶脊亮、褶谷暗，排线顺着褶走。
"""
import math

import numpy as np

from etch import OUTLINE, STRUCT, Frame, ellipse_pts, poly, resample, smoothstep, spline

NAME = '防御'

FX, FY = -160, -120          # 褶子汇聚的点（衣服挂在肩上，在画面左上外侧）
FOLDS = 7.0                  # 横跨衣摆的褶数


def hem_radius(theta):
    """下摆的轮廓：θ（度，从焦点量）→ 半径。褶脊处向外鼓。"""
    return 900 + 5.2 * (theta - 10) + 74 * np.sin(np.radians(theta) * FOLDS * 2.6 + 0.6)


def draw(c):
    rng = np.random.default_rng(12)

    # ------------------------------------------------------------ 背景：夜色，四周暗；雾从右边涌来
    bg = np.ones((c.h, c.w), np.float32)
    sky = 0.62 + 0.15 * c.radial(950, 450, 200, 700) + 0.06 * c.noise(150, 2)
    c.engrave(bg, sky, [c.ph_lin(-3, wobble=2, seed=3), c.ph_lin(62, seed=4), c.ph_lin(-50, seed=5)], spacing=10)

    # 雾：几条从右往左涌的雾带，雾里留白，边上是波浪线
    fog = np.zeros((c.h, c.w), np.float32)
    for cx, cy, rx, ry in ((1120, 430, 420, 170), (960, 610, 360, 130), (1080, 250, 300, 110), (820, 420, 240, 120),
                           (1180, 720, 300, 140), (860, 780, 260, 90), (760, 260, 180, 80)):
        fog = np.maximum(fog, np.clip(1 - c.radial(cx, cy, 0, 1.0, sx=rx, sy=ry), 0, 1) ** 0.7)
    fog = np.clip(fog * 1.3 + 0.3 * c.noise(70, 5), 0, 1)
    c.erase(smoothstep(0.1, 0.65, fog))
    band = smoothstep(0.05, 0.4, fog) * (1 - smoothstep(0.55, 0.9, fog))
    c.add(c.lines(c.ph_wave(-5, 10, 300, wobble=5, seed=6), 13, 2.4 * band))
    # 雾卷：几道不到一圈的钩形卷线（版画里画云雾的办法）
    for cx, cy, r0, w, flip in ((990, 330, 64, 3.4, 1), (1120, 560, 80, 3.6, 1), (860, 650, 56, 3.0, -1),
                                (1160, 200, 50, 2.8, 1), (930, 520, 44, 2.6, -1), (1200, 760, 60, 3.0, 1)):
        t = np.linspace(0, 1.5 * math.pi, 160)
        rr = r0 * (1 - 0.55 * t / (1.5 * math.pi))
        curl = np.stack([cx + rr * np.cos(flip * t + 1.2) * 1.4, cy + rr * np.sin(flip * t + 1.2)], 1)
        tail = np.stack([np.linspace(cx + r0 * 1.4 * math.cos(1.2) + 260, cx + r0 * 1.4 * math.cos(1.2), 40),
                         cy + r0 * math.sin(1.2) + np.zeros(40)], 1)
        c.stroke(np.vstack([tail, curl]), w, taper=(0.5, 0.3))

    # ------------------------------------------------------------ 衣摆
    theta = np.degrees(np.arctan2(c.yy - FY, c.xx - FX))
    r = np.hypot(c.xx - FX, c.yy - FY)
    th = np.linspace(-6, 100, 600)
    hem = np.stack([FX + hem_radius(th) * np.cos(np.radians(th)), FY + hem_radius(th) * np.sin(np.radians(th))], 1)
    coat_pts = np.vstack([[[FX, FY]], hem])
    coat = c.mask(coat_pts)

    fold = np.sin(np.radians(theta) * FOLDS * 2.6 + 0.6 + 0.25 * np.sin(r / 140))   # 1 = 褶脊
    near_hem = smoothstep(-220, 0, r - hem_radius(theta))
    # 光从左上：褶脊亮，褶谷暗；越靠近身体越暗；下摆边上翻出一点里衬
    tone = 0.6 - 0.36 * fold + 0.16 * c.grad(800, 400, 100, 0) - 0.1 * near_hem
    tone = np.clip(tone + 0.05 * c.noise(40, 7), 0.1, 1)
    c.engrave(coat, tone, [c.ph_ray(FX, FY, r0=900) + 1.2 * c.noise(50, 8), c.ph_ring(FX, FY) + 2 * c.noise(60, 9),
                           c.ph_lin(24, seed=9)], spacing=9.5, swell=1.2)
    # 褶谷的墨线：从焦点往外，在下摆前收尖
    for k in range(-2, 12):
        a = (math.pi * (2 * k + 1.5) - 0.6) / (FOLDS * 2.6)              # sin = -1 的地方
        a_deg = math.degrees(a)
        if a_deg < -4 or a_deg > 98:
            continue
        rh = float(hem_radius(np.array([a_deg]))[0])
        r0 = 380 + 160 * ((k * 37) % 5) / 5
        pts = [(FX + rr * math.cos(a + 0.012 * math.sin(rr / 90)), FY + rr * math.sin(a + 0.012 * math.sin(rr / 90)))
               for rr in np.linspace(r0, rh - 8, 30)]
        c.stroke(np.array(pts), 4.2, taper=(0.6, 0.08))
    # 下摆翻起的一窄条里衬：在褶脊处露出来，迎光发亮
    lining = coat * smoothstep(-46, -30, r - hem_radius(theta)) * smoothstep(0.1, 0.5, fold)
    c.engrave(lining, 0.12 + 0.2 * (1 - fold), [c.ph_ring(FX, FY)], spacing=8, wmax=2.2)
    # 下摆：粗轮廓 + 贴边的一道缝线
    c.stroke(resample(hem, 2), OUTLINE + 2, taper=0.0, light=0.4)
    inner = np.stack([FX + (hem_radius(th) - 50) * np.cos(np.radians(th)), FY + (hem_radius(th) - 50) * np.sin(np.radians(th))], 1)
    seam = resample(inner, 2)
    for i in range(0, len(seam) - 8, 14):
        c.stroke(seam[i:i + 8], 2.2, taper=0.0)

    # ------------------------------------------------------------ 碎石：从右边飞来，后面拖着速度线
    stones = [(820, 300, 52), (900, 520, 66), (760, 640, 38), (1000, 240, 30), (1080, 440, 42), (840, 760, 44),
              (1030, 690, 28), (1150, 330, 22), (700, 470, 30), (1150, 600, 32), (930, 130, 24), (640, 330, 20)]
    for i, (sx, sy, sr) in enumerate(stones):
        n = rng.integers(5, 8)
        ang = np.sort(rng.uniform(0, 2 * math.pi, n))
        rad = sr * rng.uniform(0.65, 1.15, n)
        pts = [(sx + rr * math.cos(a), sy + rr * math.sin(a) * 0.85, 'c') for a, rr in zip(ang, rad)]
        # 速度线
        for j in range(3):
            oy = (j - 1) * sr * 0.55 + rng.uniform(-4, 4)
            ln = sr * rng.uniform(2.5, 4.5)
            c.stroke(poly([(sx + sr * 0.9, sy + oy), (sx + sr * 0.9 + ln, sy + oy - ln * 0.08)]), 3.0, taper=(0.05, 0.9))
        m = c.mask(spline(pts, closed=True, step=1.5))
        lam = c.relief(m, radius=sr * 0.5, blur=1)
        c.engrave(m, np.clip(0.85 - 0.8 * lam, 0.1, 1), [c.ph_lin(rng.uniform(0, 180), wobble=0.4), c.ph_lin(rng.uniform(0, 180), wobble=0.4)],
                  spacing=6.5, wmax=2.4, grain=0)
        c.outline(pts, w=STRUCT, light=0.5)
    # 细砂
    for _ in range(40):
        x, y = rng.uniform(620, 1220), rng.uniform(120, 800)
        rr = rng.uniform(2.5, 5)
        c.add(c.ellipse(x, y, rr, rr * 0.8))
        c.stroke(poly([(x + rr, y), (x + rr + rng.uniform(15, 40), y - 2)]), 1.6, taper=(0.05, 0.9))
    # 打在衣摆上的几处：墨点溅开
    for hx, hy in ((612, 430), (520, 680), (700, 190)):
        for k in range(7):
            a = rng.uniform(-1.2, 1.2)
            ln = rng.uniform(14, 34)
            c.stroke(poly([(hx + 8 * math.cos(a), hy + 8 * math.sin(a)), (hx + ln * math.cos(a), hy + ln * math.sin(a))]), 2.6, taper=(0.1, 0.8))
