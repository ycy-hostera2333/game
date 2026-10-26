"""离线模拟游戏里的混音（策划/13 §3.2–§3.4、§6.2），只用来渲染试听预览，不进游戏。

游戏里同一套规则由 AudioDirector 在运行时做：叠夜拷贝 = 同一条「主」分轨晚一点起播、pitch 偏几个音分；
低通、混响送量、深渊层音量按理智分级；理智音效响时压低音乐。这里把同样的东西用离线卷积和滤波算出来，
好在没接进 Unity 之前先听效果。
"""
from __future__ import annotations

import numpy as np

import dsp
from dsp import SR, n

BPM = 76
BEAT = 60 / BPM

# §3.2 叠夜拷贝：(起播延迟 拍, 音分, 相对「主」的 dB)
NIGHTS = [(1 / 3, -15, -6), (1 / 2, +20, -8), (3 / 4, -30, -10), (1.0, +40, -12)]

# §3.3 按理智分级：叠夜层数、叠夜低通、混响送量 dB、深渊层、清明层、「主」低通、音乐总线补偿 dB。
# 补偿是 build.py --calibrate 实测出来的：让各档的整体响度和理智 5–8 的一样（支柱 4「更浑，不是更响」）
LEVELS = {
    10: (0, None, -24, 0.0, 1, None, 0.0),
    9: (0, None, -24, 0.0, 1, None, 0.0),
    8: (0, None, -14, 0.0, 0, None, 0.0),
    7: (0, None, -14, 0.0, 0, None, 0.0),
    6: (0, None, -14, 0.0, 0, None, 0.0),
    5: (0, None, -14, 0.0, 0, None, 0.0),
    4: (1, 8000, -10, 0.40, 0, None, -1.0),
    3: (2, 5000, -8, 0.55, 0, None, -1.5),
    2: (3, 3500, -6, 0.70, 0, None, -2.5),
    1: (4, 2500, -4, 0.85, 0, 6000, -3.5),
}
CUTS = [None, 8000, 5000, 3500, 2500]
FADE = 0.5          # 每层 0.5 秒淡入淡出，和画面的补间一致（§3.3）
DUCK_DB = -4        # 理智音效响时音乐 −4 dB（§6.2）

_IR = None


def ir():
    """混响的冲激响应，校准到响度增益 0 dB：送量 −6 dB 就是湿声比干声低 6 dB。
    不校准的话，长尾巴叠起来的能量会让湿声比送量高出约 6.5 dB，癫狂档的湿声会盖过干声。"""
    global _IR
    if _IR is None:
        raw = dsp.reverb_ir(3.2, predelay=0.025, damp=0.4, seed=11)
        rng = np.random.default_rng(0)
        spec = np.fft.rfft(rng.standard_normal(n(20)))
        spec[1:] /= np.sqrt(np.arange(1, len(spec)))          # 粉红噪声，频谱接近音乐
        pink = np.fft.irfft(spec)
        pink /= np.max(np.abs(pink))
        wet = dsp.convolve(pink, raw)[n(4):n(20)]
        _IR = raw * dsp.db(dsp.lufs(pink[n(4):]) - dsp.lufs(wet))
    return _IR


def _smooth(x: np.ndarray, seconds: float = FADE) -> np.ndarray:
    """阶梯控制量 → 带线性过渡的曲线（盒式滤波，过渡 seconds 秒）。"""
    w = max(1, n(seconds))
    pad = np.concatenate([np.full(w, x[0]), x, np.full(w, x[-1])])
    c = np.cumsum(np.insert(pad, 0, 0))
    y = (c[w:] - c[:-w]) / w
    return y[w // 2: w // 2 + len(x)]


def render(stems: dict, sanity: np.ndarray, start: float = 0.0, events=(), gaps=()) -> np.ndarray:
    """stems：base / main / clarity / deep，等长的立体声循环。sanity：逐采样的理智值（整数）。
    start：开播前已经播了多少秒（模拟打了几分钟以后的错位）。events：[(秒, 单声道音效)]，理智音效，会触发压低。
    gaps：[(开始秒, 结束秒)]，音乐静音的区间（进入癫狂的空拍）。返回立体声。"""
    count = len(sanity)
    lv = np.vectorize(lambda s: max(1, min(10, int(s))))(sanity)
    nights = np.array([LEVELS[v][0] for v in lv], float)
    send = np.array([LEVELS[v][2] for v in lv], float)
    deep = np.array([LEVELS[v][3] for v in lv], float)
    clar = np.array([LEVELS[v][4] for v in lv], float)
    main_cut = np.array([1.0 if LEVELS[v][5] else 0.0 for v in lv])
    trim = np.array([LEVELS[v][6] for v in lv], float)

    def loop(x, rate=1.0, delay=0):
        return dsp.read_looped(x, count, rate, start=start, delay=delay)

    base = loop(stems['base'])
    main_dry = loop(stems['main'])
    main_lp = dsp.lowpass(main_dry, 6000)
    w = _smooth(main_cut)[:, None]
    main = (main_dry * (1 - w) + main_lp * w) * dsp.db(-1) ** _smooth(nights)[:, None]

    # 叠夜：每份拷贝一直在播（音量 0），理智变化时只调音量（§3.2）；低通按层数在 CUTS 之间交叉淡化
    copies = np.zeros((count, 2))
    for k, (d_beats, c, g) in enumerate(NIGHTS, start=1):
        gate = _smooth((nights >= k).astype(float))
        if not gate.any():
            continue
        x = dsp.read_looped(stems['main'], count, dsp.cents(c), start=start, delay=n(d_beats * BEAT))
        copies += x * (gate * dsp.db(g))[:, None]
    nights_mix = np.zeros((count, 2))
    sel = _smooth(nights)
    for k in range(1, 5):
        wk = np.clip(1 - np.abs(sel - k), 0, 1)
        if wk.any():
            nights_mix += dsp.lowpass(copies, CUTS[k]) * wk[:, None]

    music = base + main + nights_mix
    music += loop(stems['clarity']) * _smooth(clar)[:, None]
    music += loop(stems['deep']) * _smooth(deep)[:, None]

    gate = np.ones(count)
    for a, b in gaps:
        gate[n(a):n(b)] = 0
    gate = _smooth(gate, 0.03)
    music *= gate[:, None]

    fx = np.zeros((count, 2))
    duck = np.zeros(count)
    for at, sfx in events:
        dsp.place(fx, dsp.stereo(sfx), n(at))
        duck[n(at):n(at + 0.25)] = 1
    duck_env = _smooth(duck, 0.15)
    music *= dsp.db(DUCK_DB) ** duck_env[:, None]

    wet = dsp.convolve(music * (10 ** (_smooth(send) / 20))[:, None], ir())[:count]
    return (music + wet) * (10 ** (_smooth(trim) / 20))[:, None] + fx


def timeline(count: int, steps) -> np.ndarray:
    """steps：[(秒, 理智)]，从这一刻起理智变成这个值。"""
    s = np.full(count, steps[0][1], float)
    for at, v in steps:
        s[n(at):] = v
    return s
