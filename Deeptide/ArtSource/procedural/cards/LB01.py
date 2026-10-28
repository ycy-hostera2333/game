"""LB01 獠牙（双面）：兽形的侧脸，闭着嘴、上犬齿压在下唇外。癫狂层：同一个头往前咬了第二口的残影（兽形面打两次）。

狼头占画面左大半，面朝右，后面是月光下的夜雾；残影只画轮廓和牙，落在右边浅的雾里。
"""
import numpy as np

from etch import Frame
from cards import _wolf as w

NAME = '獠牙'


def draw(c):
    w.night(c, top=0.72, bottom=0.4, seed=11)
    w.moon(c, 1060, 170, 110, seed=11)
    w.wolf_head(c, Frame((460, 470), rot=4, scale=1.75), jaw=0, seed=11)
    # 癫狂层：往前一口的残影，嘴张开
    w.wolf_head(c, Frame((760, 480), rot=-2, scale=1.5), jaw=26, layer='mad', eye_layer='mad', tone=0.25, scaled=False,
                gills=False, seed=12)
