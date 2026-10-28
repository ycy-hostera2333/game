"""LU08 镜中之脸（双面）：一面带柄的手镜立在暗处，镜子里是一个戴宽檐帽的人的侧影（清醒面：掉理智到 4，抽牌）。
癫狂层：镜子里叠上一个狼头（兽形面：理智回到 5，格挡）。"""
import numpy as np

from etch import Frame, OUTLINE, STRUCT, ellipse_pts, spline
from cards import _wolf as w

NAME = '镜中之脸'
MX, MY, RX, RY = 624, 380, 230, 300


def draw(c):
    w.night(c, top=0.9, bottom=0.8, seed=241)
    # 镜柄
    handle = [(596, 680), (652, 680), (664, 900), (584, 900)]
    hm = c.mask(spline(handle, closed=True))
    c.engrave(hm, 0.5, [c.ph_axis((620, 680), (620, 900), half=40, bow=-8), c.ph_lin(60)], spacing=6.5)
    c.outline(handle, w=STRUCT)
    # 雕花的镜框
    frame = c.ellipse(MX, MY, RX + 40, RY + 40) * (1 - c.ellipse(MX, MY, RX, RY))
    lam = c.relief(c.ellipse(MX, MY, RX + 40, RY + 40), radius=40, blur=2)
    c.engrave(frame, np.clip(0.75 - 0.6 * lam + 0.15 * np.sin(np.arctan2(c.yy - MY, c.xx - MX) * 18) ** 2, 0, 1),
              [c.ph_ring(MX, MY, RX, RY), c.ph_lin(60)], spacing=6.5, swell=1.0)
    c.stroke(ellipse_pts(MX, MY, RX + 40, RY + 40), OUTLINE, closed=True)
    c.stroke(ellipse_pts(MX, MY, RX, RY), STRUCT, closed=True)
    # 镜面：浅，有几道斜的反光
    glass = c.ellipse(MX, MY, RX - 3, RY - 3)
    c.clear(glass)
    c.engrave(glass, np.clip(0.18 + 0.2 * c.grad(MX - RX, MY - RY, MX + RX, MY + RY), 0, 1), [c.ph_lin(-55, wobble=0.4)], spacing=9, wmax=2)
    for k, off in enumerate((-80, -40)):
        c.erase(c.stroke([(MX - RX + 60 + off, MY + 40), (MX + off + 120, MY - RY + 50)], 18 - 8 * k, add=False, taper=0.3) * glass)
    # 人的侧影：帽子、脸的侧线、领子，面朝左
    head = [(700, 540), (690, 430), (660, 380), (610, 360), (580, 380), (560, 420), (545, 440), (560, 452), (556, 480), (570, 500),
            (590, 520), (580, 560)]
    face = c.mask(spline(head + [(760, 640), (760, 540)], closed=True)) * glass
    c.engrave(face, 0.85, [c.ph_lin(70), c.ph_lin(10)], spacing=7, swell=1.3)
    hat = c.mask(spline([(520, 360), (600, 340), (720, 346), (760, 362), (700, 372), (600, 372)], closed=True)) + \
        c.mask(spline([(590, 350), (600, 250), (680, 240), (710, 352)], closed=True))
    c.fill(np.clip(hat, 0, 1) * glass)
    # 癫狂层：镜子里的狼头
    w.wolf_head(c, Frame((560, 440), scale=0.9), jaw=10, layer='mad',
                eye_layer='mad', tone=0.3, scaled=False, gills=False, seed=242)
