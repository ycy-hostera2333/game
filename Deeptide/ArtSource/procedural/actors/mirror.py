"""BOSS 倒影（Boss 档）：一面穿衣镜，镜子里是你湿透的背影（画法见 _mirror.py）。"""
from actors._mirror import SIZE, draw as _draw

NAME = "倒影"


def draw(c):
    _draw(c, cracked=False)
