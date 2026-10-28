"""BOSS 倒影 · 龟裂（第二阶段，生命 ≤ 50%）：镜面从胸口炸开，裂缝发青，碎片错开（画法见 _mirror.py）。"""
from actors._mirror import SIZE, draw as _draw

NAME = "倒影 · 龟裂"


def draw(c):
    _draw(c, cracked=True)
