"""荒原猎户（狼人的人形，中档，策划/14 §2.1）：和调查员同一个 3/4 背身面朝右的站姿，换成猎户的行头——
不戴帽、乱发，肩上披一块浅色的兽皮，背上斜挎一支长猎枪，枪管从右肩上方伸出来。垂着的那只手和右颊长着鳞（深渊色）。
癫狂层：身后的影子里浮出一个比人还大的狼头（兽形快要涌上来）。真正跨过癫狂线时，战斗里会换成 player_werewolf_beast。
"""
import math

import numpy as np

from etch import STRUCT, Frame, ellipse_pts, smoothstep, spline
from cards import _wolf as w

NAME = '荒原猎户'
SIZE = (400, 680)

OUT_W = 11.0
FEET = 1306


def draw(c):
    # ------------------------------------------------------------ 形状（风衣、腿、靴照搬调查员，好让两个人站在同一个位置）
    coat = [(300, 330), (500, 320), (560, 400), (580, 560), (600, 800), (630, 1060), (640, 1140), (560, 1160), (420, 1150),
            (300, 1162), (190, 1146), (205, 1000), (220, 760), (232, 540), (250, 400)]
    legs = [[(270, 1140), (340, 1140), (334, 1290), (262, 1294)], [(430, 1140), (500, 1140), (510, 1292), (438, 1292)]]
    boots = [[(250, 1240), (346, 1238), (350, 1306), (236, 1308)], [(424, 1240), (518, 1240), (540, 1306), (430, 1308)]]
    head = (420, 236, 72, 86)
    pelt = [(240, 300), (300, 250), (420, 230), (540, 250), (600, 320), (620, 420), (560, 470), (480, 440), (420, 470), (350, 440),
            (280, 480), (220, 420)]
    arm = [(520, 380), (590, 470), (622, 640), (620, 760), (580, 770), (566, 650), (540, 500)]
    shadow = c.ellipse(420, FEET + 2, 300, 24)
    G = Frame((300, 880), rot=-62)                                           # 猎枪：斜挎在背上，枪托在左下，x 轴指向枪口（右上）
    barrel = [(0, -10), (860, -8), (860, 8), (0, 10)]
    stock = [(40, -18), (-160, -40), (-220, -56), (-230, 34), (-160, 34), (40, 18)]

    coat_m = c.mask(spline(coat, closed=True))
    arm_m = c.mask(spline(arm, closed=True))
    leg_m = np.clip(sum(c.mask(np.array(l)) for l in legs), 0, 1)
    boot_m = np.clip(sum(c.mask(spline(b, closed=True)) for b in boots), 0, 1)
    head_m = c.ellipse(*head)
    pelt_m = c.mask(spline(pelt, closed=True))
    gun_m = np.clip(c.mask(G(barrel)) + c.mask(spline(G(stock), closed=True)), 0, 1)
    fill = np.clip(coat_m + arm_m + leg_m + boot_m + head_m + pelt_m + gun_m + shadow, 0, 1)
    c.add(fill, 'fill')

    side = c.grad(230, 0, 640, 0)
    c.engrave(shadow, 0.9, [c.ph_lin(0, wobble=0.6), c.ph_lin(60)], spacing=7, swell=1.2)
    # ------------------------------------------------------------ 腿和靴子
    for l, b in zip(legs, boots):
        m = c.mask(np.array(l))
        c.engrave(m, 0.88, [c.ph_lin(84, seed=1), c.ph_lin(10, seed=2)], spacing=7.5, swell=1.2)
        c.stroke(np.array(l + l[:1]), STRUCT, taper=0)
        bm = c.mask(spline(b, closed=True))
        lam = c.relief(bm, radius=20, blur=1.5)
        c.engrave(bm, np.clip(0.95 - 0.5 * lam, 0.5, 1), [c.ph_lin(0, seed=3), c.ph_lin(70, seed=4)], spacing=7)
        c.outline(b, w=STRUCT + 1, light=0.4)
        for k in range(3):                                                   # 绑腿的皮绳
            y = b[0][1] - 20 - 30 * k
            c.line([(l[0][0] - 4, y), ((l[0][0] + l[1][0]) / 2, y + 8), (l[1][0] + 4, y)], w=3, taper=0.2)

    # ------------------------------------------------------------ 风衣：比调查员的短一截、更旧，下摆有破口
    lam = c.relief(coat_m, radius=130, blur=6)
    fold = np.sin((c.xx - 420) / 40 + 0.5 * np.sin(c.yy / 180))
    tone = np.clip(0.5 + 0.38 * side + 0.22 * (1 - lam) - 0.1 * fold * smoothstep(700, 1100, c.yy), 0, 1)
    c.engrave(coat_m, tone, [c.ph_lin(86, wobble=1.8, seed=5), c.ph_lin(22, seed=6), c.ph_lin(-38, seed=7)], spacing=8.5, swell=1.2)
    c.stroke(spline([(240, 720), (420, 740), (590, 724)]), 14, taper=0)      # 子弹带
    for x in range(270, 580, 34):
        c.erase(c.stroke([(x, 714), (x + 2, 730)], 6, add=False, taper=0.2))
    for pts in ([(300, 760), (280, 960), (250, 1140)], [(520, 760), (560, 960), (600, 1130)]):
        c.line(pts, w=3.2, taper=0.4)
    c.outline(coat, w=OUT_W, light=0.45)
    for x0 in (330, 470):                                                    # 下摆的破口
        c.line([(x0, 1156), (x0 + 14, 1100), (x0 + 30, 1158)], w=4, taper=0.2)

    # ------------------------------------------------------------ 猎枪和背带
    c.line([(250, 420), (380, 600), (520, 820)], w=12, taper=0)
    stock_m = c.mask(spline(G(stock), closed=True))
    c.clear(stock_m, layers=('ink',))
    c.engrave(stock_m, 0.45, [c.ph_along(G.p(-230, 0), G.p(40, 0), seed=24), c.ph_lin(G.angle(80))], spacing=6.5)
    c.outline(G.cp(stock), w=STRUCT)
    bm = c.mask(G(barrel))
    c.clear(bm, layers=('ink',))
    c.engrave(bm, 0.7, [c.ph_lin(G.angle(90))], spacing=5)
    c.stroke(G(barrel + barrel[:1]), 3.6, taper=0)
    c.erase(c.stroke(G([(20, -4), (840, -3)]), 2.6, add=False, taper=0.1))
    c.stroke(G([(60, 10), (70, 34), (110, 36), (120, 10)]), 3.4, taper=0)       # 扳机护圈

    # ------------------------------------------------------------ 兽皮披肩：盖住两肩，边缘全是毛
    lam = c.relief(pelt_m, radius=60, blur=4)
    c.engrave(pelt_m, np.clip(0.12 + 0.4 * side - 0.15 * lam, 0, 1), [c.ph_lin(80, wobble=2.2, seed=20), c.ph_lin(30, seed=21)],
              spacing=7, swell=1.0)
    w.fur(c, pelt_m, 95, length=30, density=0.004, seed=22, w=1.8)
    w.fringe(c, spline(pelt[5:] + pelt[:1]), 10, length=30, step=9, seed=23)
    c.line(spline(pelt[:6]), w=STRUCT, taper=0.2)

    # ------------------------------------------------------------ 近侧手臂：垂着，倒提猎枪
    lam = c.relief(arm_m, radius=30, blur=2)
    c.engrave(arm_m, np.clip(0.98 - 0.45 * lam, 0.5, 1), [c.ph_axis((520, 380), (600, 770), bow=-10, half=40), c.ph_lin(20)],
              spacing=8, swell=1.1)
    c.outline(arm, w=STRUCT + 1, light=0.4)
    fist = c.ellipse(600, 784, 30, 26)
    c.clear(fist, layers=('ink',))
    c.engrave(fist, 0.4, [c.ph_lin(30)], spacing=6, wmax=2.2)
    w.scales(c, fist, size=12, angle=60, tone=0.0, w=1.8, shade=0.2)
    c.add(fist * c.lines(c.ph_lin(60), 12, 3) * 0.7, 'abyss')
    c.stroke(ellipse_pts(600, 784, 30, 26), 4, closed=True)

    # ------------------------------------------------------------ 头：乱发的后脑，右边露出一条脸颊，颊上长着鳞
    lam = c.relief(head_m, radius=40, blur=2)
    c.engrave(head_m, np.clip(0.95 - 0.3 * lam, 0.65, 1), [c.ph_lin(80, wobble=2.5, seed=12), c.ph_lin(20, seed=13)], spacing=7,
              swell=1.3)
    cheek = [(474, 210), (494, 240), (496, 290), (472, 320), (452, 290)]
    ch = c.mask(spline(cheek, closed=True))
    c.clear(ch, layers=('ink',))
    c.engrave(ch, 0.25 + 0.5 * c.grad(0, 200, 0, 250), [c.ph_lin(80)], spacing=6, wmax=2.2)
    sc = ch * c.ellipse(476, 290, 30, 40)
    w.scales(c, sc, size=11, angle=100, tone=0.0, w=1.6, shade=0.2)
    c.add(sc * 0.6, 'abyss')
    c.outline(cheek, w=3.4, light=0.4)
    hair = [(340, 220), (360, 160), (410, 140), (470, 150), (500, 190), (500, 230)]
    c.line(spline(hair), w=STRUCT + 1, taper=0.2)
    w.fur(c, head_m * (c.yy < 300), 100, length=46, density=0.01, w=3.0, seed=25, jitter=12)   # 乱发：往下披
    w.fringe(c, spline([(350, 230), (360, 290), (390, 320), (440, 330)]), 150, length=30, step=8, w=3.0, seed=26)

    # ============================================================ 癫狂层：身后影子里的狼头
    w.wolf_head(c, Frame((330, 260), scale=1.25, rot=-8), jaw=16, layer='mad', eye_layer='mad', tone=0.35, scaled=False, gills=True,
                seed=26)
