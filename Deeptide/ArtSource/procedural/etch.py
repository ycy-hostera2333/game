"""程序化版画画笔（策划/10 §3、§11）。

插图在工作画布（导出尺寸 × 2）上画，导出时 2 × 2 平均缩小。三层画布对应打包贴图的三个通道（10 §11.2）：
ink（墨）、abyss（深渊遮罩）、mad（癫狂层）。每层都是 0–1 的墨覆盖率，只有灰度，颜色交给着色器。

画法只有三种：
- 排线 engrave：明暗只靠排线的疏密和粗细（10 §1.1），一层到三层交叉，最暗处实墨
- 勾线 stroke：两端收尖，背光一侧加粗（光源统一在左上，10 §3）
- 实墨 fill / 留白 erase

坐标：像素，原点左上，y 向下。
"""
from __future__ import annotations

import math

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

W, H = 1248, 864                      # 卡图工作画布（导出 624 × 432）
LIGHT3 = np.array([-1.0, -1.0, 1.25])
LIGHT3 /= np.linalg.norm(LIGHT3)       # 光从左上
LIGHT2 = np.array([-0.7071, -0.7071])

# 10 §3 线宽（工作画布）
OUTLINE = 9.0       # 卡图外轮廓 8–10
STRUCT = 5.0        # 结构线 4–6
HATCH_W = 2.6       # 排线 2–3
HATCH_GAP = 9.0     # 排线间距 ≥ 8


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ====================================================================== 路径

def _cr(p0, p1, p2, p3, n):
    """向心 Catmull-Rom 一段（p1 → p2），n 个采样点，不含终点。"""
    def nxt(t, a, b):
        return t + max(float(np.hypot(*(b - a))), 1e-4) ** 0.5
    t0 = 0.0
    t1 = nxt(t0, p0, p1)
    t2 = nxt(t1, p1, p2)
    t3 = nxt(t2, p2, p3)
    t = np.linspace(t1, t2, n, endpoint=False)[:, None]
    a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1
    a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2
    a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3
    b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2
    b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3
    return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2


def spline(pts, closed=False, step=2.0):
    """过控制点的平滑曲线。点写成 (x, y, 'c') 表示尖角。"""
    P = [np.array(p[:2], float) for p in pts]
    corner = [len(p) > 2 and p[2] == 'c' for p in pts]
    n = len(P)
    out = []
    for i in range(n if closed else n - 1):
        i1, i2 = i, (i + 1) % n
        p1, p2 = P[i1], P[i2]
        p0 = P[(i1 - 1) % n] if (closed or i1 > 0) and not corner[i1] else 2 * p1 - p2
        p3 = P[(i2 + 1) % n] if (closed or i2 < n - 1) and not corner[i2] else 2 * p2 - p1
        k = max(2, int(math.ceil(float(np.hypot(*(p2 - p1))) / step)))
        out.append(_cr(p0, p1, p2, p3, k))
    if not closed:
        out.append(P[-1][None])
    return np.vstack(out)


def poly(pts):
    return np.array([p[:2] for p in pts], float)


def ellipse_pts(cx, cy, rx, ry, rot=0.0, a0=0.0, a1=360.0, n=None):
    """椭圆（或一段椭圆弧）上的点，角度用度。"""
    if n is None:
        n = max(16, int(abs(a1 - a0) / 360 * 2 * math.pi * max(rx, ry) / 2))
    t = np.radians(np.linspace(a0, a1, n))
    x, y = rx * np.cos(t), ry * np.sin(t)
    c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    return np.stack([cx + c * x - s * y, cy + s * x + c * y], 1)


def resample(pts, step):
    pts = np.asarray(pts, float)
    d = np.hypot(*np.diff(pts, axis=0).T)
    s = np.concatenate([[0], np.cumsum(d)])
    if s[-1] < 1e-6:
        return pts[:1]
    n = max(2, int(s[-1] / step) + 1)
    t = np.linspace(0, s[-1], n)
    return np.stack([np.interp(t, s, pts[:, 0]), np.interp(t, s, pts[:, 1])], 1)


def offset(pts, d):
    """把折线沿法线方向（前进方向的左侧为正… 图像坐标下即右手侧）平移 d。"""
    pts = np.asarray(pts, float)
    t = np.gradient(pts, axis=0)
    t /= np.maximum(np.hypot(t[:, 0], t[:, 1]), 1e-6)[:, None]
    nrm = np.stack([-t[:, 1], t[:, 0]], 1)
    return pts + nrm * d


def transform(pts, dx=0.0, dy=0.0, rot=0.0, scale=1.0, cx=0.0, cy=0.0):
    """绕 (cx, cy) 旋转（度）、缩放，再平移。"""
    pts = np.asarray(pts, float)
    c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    x, y = pts[:, 0] - cx, pts[:, 1] - cy
    return np.stack([cx + scale * (c * x - s * y) + dx, cy + scale * (s * x + c * y) + dy], 1)


class Frame:
    """局部坐标系：先缩放、旋转（度），再平移到 origin。画一个物件时在它自己的坐标里写点，再用它换到画布。"""

    def __init__(self, origin, rot=0.0, scale=1.0):
        self.ox, self.oy = origin
        self.rot, self.scale = rot, scale
        self.c, self.s = math.cos(math.radians(rot)), math.sin(math.radians(rot))

    def __call__(self, pts):
        """点列 → 画布坐标（N × 2 数组）。"""
        p = np.asarray([q[:2] for q in pts] if isinstance(pts, list) else pts, float) * self.scale
        return np.stack([self.ox + self.c * p[:, 0] - self.s * p[:, 1], self.oy + self.s * p[:, 0] + self.c * p[:, 1]], 1)

    def p(self, x, y):
        return tuple(self([(x, y)])[0])

    def cp(self, pts):
        """控制点 → 画布坐标，保留 'c' 尖角标记。"""
        xy = self([q[:2] for q in pts])
        return [tuple(a) + tuple(q[2:]) for a, q in zip(xy, pts)]

    def angle(self, a):
        return a + self.rot


def lerp(a, b, t):
    return np.asarray(a, float) * (1 - t) + np.asarray(b, float) * t


# ====================================================================== 画布

class Canvas:
    def __init__(self, w=W, h=H, seed=1):
        self.w, self.h = w, h
        self.seed = seed
        # fill 是角色图的轮廓（透明底素材才用，10 §11.2 的 A 通道）
        self.layers = {k: np.zeros((h, w), np.float32) for k in ('ink', 'abyss', 'mad', 'fill')}
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        self.xx, self.yy = xx + 0.5, yy + 0.5
        self._noise = {}

    # ------------------------------------------------------------ 基础

    def noise(self, scale, seed=0):
        """平滑噪声，约在 [-1, 1]，特征尺寸 scale 像素。"""
        key = (round(scale, 2), seed)
        if key not in self._noise:
            rng = np.random.default_rng(self.seed * 7919 + seed * 104729 + int(scale * 13))
            gh, gw = int(self.h / scale) + 4, int(self.w / scale) + 4
            g = rng.standard_normal((gh, gw)).astype(np.float32)
            n = ndi.zoom(g, scale, order=3)[: self.h, : self.w]
            n = ndi.gaussian_filter(n, scale * 0.25)
            n /= max(float(n.std()) * 2.2, 1e-6)
            self._noise[key] = np.clip(n, -1, 1)
        return self._noise[key]

    def mask(self, pts, ss=4):
        """闭合折线 → 抗锯齿遮罩。"""
        pts = np.asarray(pts, float)
        x0 = max(int(math.floor(pts[:, 0].min())) - 2, 0)
        y0 = max(int(math.floor(pts[:, 1].min())) - 2, 0)
        x1 = min(int(math.ceil(pts[:, 0].max())) + 2, self.w)
        y1 = min(int(math.ceil(pts[:, 1].max())) + 2, self.h)
        m = np.zeros((self.h, self.w), np.float32)
        if x1 <= x0 or y1 <= y0:
            return m
        img = Image.new('L', ((x1 - x0) * ss, (y1 - y0) * ss), 0)
        ImageDraw.Draw(img).polygon([((x - x0) * ss, (y - y0) * ss) for x, y in pts], fill=255)
        a = np.asarray(img, np.float32).reshape(y1 - y0, ss, x1 - x0, ss).mean((1, 3)) / 255
        m[y0:y1, x0:x1] = a
        return m

    def smask(self, pts, closed=True):
        """控制点 → 平滑闭合曲线 → 遮罩。"""
        return self.mask(spline(pts, closed=True))

    def ellipse(self, cx, cy, rx, ry, rot=0.0):
        c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        x, y = self.xx - cx, self.yy - cy
        u, v = (c * x + s * y) / rx, (-s * x + c * y) / ry
        r = np.sqrt(u * u + v * v)
        # 近似到边缘的像素距离
        d = (r - 1) * min(rx, ry)
        return np.clip(0.5 - d, 0, 1).astype(np.float32)

    def rect(self, x0, y0, x1, y1):
        return self.mask(poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)]))

    def half_plane(self, p0, p1):
        """直线 p0→p1 右手侧（图像坐标）为 1。"""
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        L = math.hypot(dx, dy)
        d = ((self.xx - p0[0]) * -dy + (self.yy - p0[1]) * dx) / L
        return np.clip(0.5 - d, 0, 1).astype(np.float32)

    # ------------------------------------------------------------ 合成

    def paint(self, mask, cov, layer='ink'):
        """遮罩范围内先涂成纸色，再画上 cov（画家算法：后画的挡住先画的）。"""
        L = self.layers[layer]
        L *= (1 - mask)
        L += np.asarray(cov, np.float32) * mask

    def fill(self, mask, layer='ink', value=1.0):
        self.paint(mask, value, layer)

    def add(self, cov, layer='ink'):
        L = self.layers[layer]
        np.maximum(L, cov, out=L)

    def erase(self, cov, layer='ink'):
        self.layers[layer] *= (1 - np.clip(cov, 0, 1))

    def clear(self, mask, layers=('ink', 'abyss', 'mad')):
        for k in layers:
            self.layers[k] *= (1 - mask)

    # ------------------------------------------------------------ 勾线

    def stroke(self, pts, w=STRUCT, layer='ink', taper=0.18, w_end=None, light=0.0, closed=False,
               wobble=0.0, step=2.0, add=True, profile=None):
        """变宽勾线。taper：两端收尖所占的长度比例（可给 (头, 尾)）；light：背光一侧加粗的幅度（闭合曲线顺时针时外法线朝外）。"""
        pts = np.asarray(pts, float)
        if closed:
            pts = np.vstack([pts, pts[:1]])
        pts = resample(pts, step)
        n = len(pts)
        if n < 2:
            return np.zeros((self.h, self.w), np.float32)
        d = np.hypot(*np.diff(pts, axis=0).T)
        s = np.concatenate([[0], np.cumsum(d)])
        u = s / max(s[-1], 1e-6)
        wid = np.full(n, float(w))
        if w_end is not None:
            wid = lerp(w, w_end, u)
        if profile is not None:
            wid = wid * profile(u)
        if not closed and taper:
            ta, tb = (taper, taper) if np.isscalar(taper) else taper
            f = np.ones(n)
            if ta > 0:
                f *= 0.12 + 0.88 * smoothstep(0, ta, u)
            if tb > 0:
                f *= 0.12 + 0.88 * smoothstep(0, tb, 1 - u)
            wid = wid * f
        if light:
            t = np.gradient(pts, axis=0)
            t /= np.maximum(np.hypot(t[:, 0], t[:, 1]), 1e-6)[:, None]
            nrm = np.stack([t[:, 1], -t[:, 0]], 1)   # 控制点按顺时针（看上去）给出时指向外侧
            facing = nrm @ LIGHT2                     # 1 = 迎光
            wid = wid * (1 - light * facing)
        if wobble:
            wid = wid * (1 + wobble * np.interp(u, np.linspace(0, 1, 9), np.random.default_rng(n).uniform(-1, 1, 9)))
        r = np.maximum(wid, 0.0) / 2
        out = np.zeros((self.h, self.w), np.float32)
        for i in range(n - 1):
            p0, p1, r0, r1 = pts[i], pts[i + 1], r[i], r[i + 1]
            rm = max(r0, r1) + 1.5
            x0 = max(int(min(p0[0], p1[0]) - rm), 0)
            x1 = min(int(max(p0[0], p1[0]) + rm) + 1, self.w)
            y0 = max(int(min(p0[1], p1[1]) - rm), 0)
            y1 = min(int(max(p0[1], p1[1]) + rm) + 1, self.h)
            if x1 <= x0 or y1 <= y0:
                continue
            X = self.xx[y0:y1, x0:x1] - p0[0]
            Y = self.yy[y0:y1, x0:x1] - p0[1]
            dx, dy = p1[0] - p0[0], p1[1] - p0[1]
            ll = dx * dx + dy * dy
            t = np.clip((X * dx + Y * dy) / ll, 0, 1) if ll > 1e-9 else np.zeros_like(X)
            dist = np.hypot(X - t * dx, Y - t * dy)
            rr = r0 + (r1 - r0) * t
            cov = _line_cov(dist, rr * 2)
            sub = out[y0:y1, x0:x1]
            np.maximum(sub, cov, out=sub)
        if add:
            self.add(out, layer)
        return out

    def outline(self, pts, w=OUTLINE, layer='ink', light=0.45, **kw):
        """闭合轮廓（控制点顺时针给出）：背光一侧加粗。"""
        return self.stroke(spline(pts, closed=True), w, layer=layer, closed=True, light=light, **kw)

    def line(self, pts, w=STRUCT, layer='ink', **kw):
        """开放的平滑勾线。"""
        return self.stroke(spline(pts), w, layer=layer, **kw)

    def dots(self, pts, r, layer='ink'):
        cov = np.zeros((self.h, self.w), np.float32)
        for (x, y), rr in zip(pts, np.broadcast_to(r, (len(pts),))):
            cov = np.maximum(cov, np.clip(rr + 0.5 - np.hypot(self.xx - x, self.yy - y), 0, 1))
        self.add(cov, layer)
        return cov

    # ------------------------------------------------------------ 排线

    @staticmethod
    def lines(phase, spacing, width, grad=None):
        """相位场的等值线：phase 每变化 spacing 一条线，线宽 width（像素，可以是数组）。"""
        if grad is None:
            gy, gx = np.gradient(phase)
            grad = np.clip(np.hypot(gx, gy), 0.25, 6)
        f = phase / spacing
        d = np.abs(f - np.floor(f) - 0.5) * spacing / grad
        return _line_cov(d, width)

    def engrave(self, mask, tone, phases, spacing=HATCH_GAP, wmax=HATCH_W + 0.6, layer='ink',
                solid=0.93, bands=None, grain=0.06, swell=0.8):
        """按色调排线。tone 0 = 纸，1 = 实墨。phases：一到三个相位场，依次在更暗的色调段加入（交叉排线）。
        swell：第一层线在更暗处继续变粗（铜版的「涨线」），暗部是粗的主线 + 细的交叉线，而不是一张均匀的网。"""
        tone = np.asarray(tone, np.float32)
        if grain:
            tone = tone + grain * self.noise(7, 11) * (tone > 0.02)
        if bands is None:
            bands = [(0.04, 0.36), (0.34, 0.64), (0.6, 0.9)]
        acc = np.zeros((self.h, self.w), np.float32)
        for k, ((lo, hi), ph) in enumerate(zip(bands, phases)):
            wid = wmax * np.sqrt(smoothstep(lo, hi, tone))
            if k == 0 and swell:
                wid = wid * (1 + swell * smoothstep(hi, 1.0, tone))
            elif k > 0:
                wid = wid * 0.8
            c = self.lines(ph, spacing, wid)
            acc = 1 - (1 - acc) * (1 - c)
        if solid is not None:
            acc = np.maximum(acc, smoothstep(solid, 1.0, tone))
        self.paint(mask, acc, layer)
        return acc

    # ------------------------------------------------------------ 相位场

    def ph_lin(self, angle, wobble=1.2, wscale=60, seed=0):
        """直排线，angle 为线的走向（度，0 = 水平）。"""
        a = math.radians(angle + 90)
        p = self.xx * math.cos(a) + self.yy * math.sin(a)
        if wobble:
            p = p + wobble * self.noise(wscale, seed)
        return p

    def ph_wave(self, angle, amp, wavelength, wobble=1.0, seed=0, phase=0.0):
        """波浪排线（水、布料）。"""
        a = math.radians(angle)
        along = self.xx * math.cos(a) + self.yy * math.sin(a)
        across = -self.xx * math.sin(a) + self.yy * math.cos(a)
        return across + amp * np.sin(2 * math.pi * along / wavelength + phase) + wobble * self.noise(60, seed)

    def ph_ring(self, cx, cy, sx=1.0, sy=1.0):
        """同心圆 / 同心椭圆。"""
        return np.hypot((self.xx - cx) / sx, (self.yy - cy) / sy)

    def ph_ray(self, cx, cy, r0=200.0):
        """放射线（光芒）。"""
        return np.arctan2(self.yy - cy, self.xx - cx) * r0

    def ph_axis(self, p0, p1, bow=0.0, half=50.0, wobble=0.8, seed=0):
        """横截圆柱的排线：线垂直于 p0→p1 轴，向 p0→p1 方向弯成弓形（bow 像素），表现圆柱体积。"""
        ux, uy = p1[0] - p0[0], p1[1] - p0[1]
        L = math.hypot(ux, uy)
        ux, uy = ux / L, uy / L
        X, Y = self.xx - p0[0], self.yy - p0[1]
        along = X * ux + Y * uy
        across = (-X * uy + Y * ux) / half
        return along - bow * np.clip(across, -1.5, 1.5) ** 2 + wobble * self.noise(50, seed)

    def ph_along(self, p0, p1, wobble=0.8, seed=0):
        """平行于 p0→p1 的排线。"""
        ux, uy = p1[0] - p0[0], p1[1] - p0[1]
        L = math.hypot(ux, uy)
        return (-(self.xx - p0[0]) * uy + (self.yy - p0[1]) * ux) / L + wobble * self.noise(50, seed)

    def ph_contour(self, mask, blur=3.0):
        """沿轮廓走的排线（等距线）。"""
        d = ndi.distance_transform_edt(mask > 0.5).astype(np.float32)
        return ndi.gaussian_filter(d, blur)

    # ------------------------------------------------------------ 明暗

    def inside(self, mask):
        return ndi.distance_transform_edt(mask > 0.5).astype(np.float32)

    def relief(self, mask, radius=40.0, blur=2.0, light=LIGHT3):
        """把剪影鼓成浮雕，按左上光源求漫反射（0–1）。"""
        d = np.minimum(self.inside(mask), radius) / radius
        h = np.sqrt(np.clip(1 - (1 - d) ** 2, 0, 1)) * radius
        if blur:
            h = ndi.gaussian_filter(h, blur)
        gy, gx = np.gradient(h)
        nz = np.ones_like(h)
        n = np.sqrt(gx * gx + gy * gy + nz)
        lam = (-gx * light[0] - gy * light[1] + light[2]) / n
        return np.clip(lam, 0, 1).astype(np.float32)

    def grad(self, x0, y0, x1, y1):
        """线性渐变：(x0, y0) 处 0，(x1, y1) 处 1。"""
        dx, dy = x1 - x0, y1 - y0
        ll = dx * dx + dy * dy
        return np.clip(((self.xx - x0) * dx + (self.yy - y0) * dy) / ll, 0, 1).astype(np.float32)

    def radial(self, cx, cy, r0, r1, sx=1.0, sy=1.0):
        """径向渐变：半径 r0 内 0，r1 外 1。"""
        r = np.hypot((self.xx - cx) / sx, (self.yy - cy) / sy)
        return np.clip((r - r0) / (r1 - r0), 0, 1).astype(np.float32)

    def shadow_of(self, mask, dx, dy, blur=1.0):
        """投影：遮罩平移 (dx, dy)。"""
        s = ndi.shift(mask, (dy, dx), order=1, mode='constant')
        if blur:
            s = ndi.gaussian_filter(s, blur)
        return np.clip(s, 0, 1)

    # ------------------------------------------------------------ 导出

    def export(self, layer):
        """2 × 2 平均缩小到导出尺寸。"""
        L = np.clip(self.layers[layer], 0, 1)
        return L.reshape(self.h // 2, 2, self.w // 2, 2).mean((1, 3))


def _line_cov(d, width):
    """宽 width 的线在距中心线 d 处的像素覆盖率（1 像素盒式滤波，细线自然变淡）。"""
    hw = np.asarray(width, np.float32) / 2
    return np.clip(np.minimum(d + 0.5, hw) - np.maximum(d - 0.5, -hw), 0, 1).astype(np.float32)


def cursive_word(rng, n, h, x=0.0, y=0.0, slant=0.25, step=(11, 15)):
    """一个手写花体「词」（不是真实文字）：带圈的摆线，偶尔一个高笔画或下沉笔画。返回点列和宽度。"""
    pts = []
    px = x
    for _ in range(n):
        tall = rng.random() < 0.22
        low = (not tall) and rng.random() < 0.12
        loop = rng.random() < 0.55
        hh = h * (1.9 if tall else 1.0) * rng.uniform(0.75, 1.05)
        st = rng.uniform(*step)
        t = np.linspace(0, 2 * math.pi, 18)
        r = st * (0.62 if loop else 0.3)
        xs = px + st * t / (2 * math.pi) - r * np.sin(t) * 0.55
        ys = y - hh * (1 - np.cos(t)) / 2
        if low:
            ys = np.where(t > math.pi, y + (h * 0.9) * np.sin(t - math.pi) * 1.0, ys)
        xs = xs + (y - ys) * slant                                          # 右斜
        pts.extend(zip(xs, ys))
        px += st
    return np.array(pts), px - x


def scrawl(c, x0, x1, y, h, seed=0, layer='ink', width=2.6, slope=0.0, word=None, jitter=1.0, gap=(16, 26)):
    """一行潦草的手写字，沿基线 y（可带斜率 slope）从 x0 写到 x1。word 给定时每个词都是同一个词（重复书写），
    jitter 控制每遍的走样程度。返回每个词的 (x 中心, y, 宽)。"""
    rng = np.random.default_rng(seed)
    x = x0
    words = []
    while x < x1 - 30:
        if word is not None:
            wr = np.random.default_rng(word)
            pts, wl = cursive_word(wr, int(wr.integers(4, 8)), h, x, 0)
            pts = pts + rng.normal(0, 0.9 * jitter, pts.shape)              # 每一遍都写得不太一样
        else:
            pts, wl = cursive_word(rng, int(rng.integers(3, 9)), h, x, 0)
        yy = y + (x - x0) * slope
        pts[:, 1] += yy + np.linspace(0, rng.uniform(-3, 3) * jitter, len(pts))
        w = width * rng.uniform(0.8, 1.25)
        c.stroke(pts, w, layer=layer, taper=0.12, step=1.2)
        if rng.random() < 0.3:                                             # t 的横杠
            tx = x + wl * rng.uniform(0.3, 0.7)
            c.stroke(poly([(tx - 10, yy - h * 1.3), (tx + 12, yy - h * 1.4)]), w * 0.8, layer=layer, taper=0.3)
        words.append((x + wl / 2, yy, wl))
        x += wl + rng.uniform(*gap)
    return words

