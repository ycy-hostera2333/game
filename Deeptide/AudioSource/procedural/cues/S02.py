"""S02「回潮」死亡乐句的样张（策划/13 §4.6、§7.4 第 1 件）。

游戏里分两段：前面的「倒放 1.5 秒」是运行时把刚播过的 2 秒混音反过来放（§6.1），不是素材；
后面的「静 1 秒 → 潮钟七下 → 栈桥的浪」是素材 S02_bells.wav。预览 S02_demo.wav 把两段接起来：
先是一段理智 3 的战斗和致命一击，然后倒放、安静、钟声。
"""
from __future__ import annotations

import numpy as np

import dsp
import instruments as ins
import mixer
from dsp import n

PRIME = dsp.hz('D2')      # 大钟：prime D2、nominal D3、hum D1（§2.2「潮钟定在低音 D」）
INTERVAL = 1.2
STRIKES = 7
LENGTH = 11.5
LOUDNESS = -20.0          # 死亡是一局里最重的时刻之一，比音乐（−23）响一点


def bells() -> np.ndarray:
    rng = np.random.default_rng(1)
    count = n(LENGTH)
    x = np.zeros((count, 2))
    for k in range(STRIKES):
        b = ins.bell(PRIME, LENGTH - k * INTERVAL, decay_scale=1 + 0.08 * k,
                     strike=rng.uniform(0.9, 1.0), seed=k)
        dsp.place(x, dsp.pan(b, rng.uniform(-0.06, 0.06)), n(k * INTERVAL))
    # 钟在教堂底下，你在栈桥上：隔得远，偏暗、混响多
    dry = dsp.lowpass(x, 5000)
    wet = dsp.convolve(dry, dsp.reverb_ir(4.5, predelay=0.04, damp=0.35, seed=3))[:count]
    out = dry * 0.7 + wet * 0.55
    w = ins.waves(LENGTH - 7.0, seed=4)
    w *= dsp.ramp(len(w), 2.5, 0.5)[:, None]
    dsp.place(out, w, n(7.0), 0.05)
    for at, p, seed in ((8.9, -0.45, 5), (10.2, 0.35, 6)):
        c = ins.creak(0.9, seed=seed)
        dsp.place(out, dsp.pan(dsp.highpass(c, 200), p), n(at), 0.02)
    out *= dsp.ramp(count, 0.0, 1.6, 1.5)[:, None]
    return out * dsp.db(LOUDNESS - dsp.lufs(out))


def rewind(x: np.ndarray, dur: float = 1.5) -> np.ndarray:
    """把 x 倒过来放，越倒越快（播放速度 0.8 → 2.0），底下垫一层倒放的混响。"""
    wet = dsp.convolve(x, mixer.ir())[:len(x) + n(0.8)]
    out = np.zeros((n(dur), 2))
    u = np.linspace(0, 1, len(out))
    rate = 0.8 + 1.2 * u ** 1.5
    pos = np.cumsum(rate) - rate[0]
    for src, g in ((x, 1.0), (wet, 0.45)):
        rev = src[::-1]
        p = pos * (len(rev) - 2) / pos[-1]
        i = np.floor(p).astype(int)
        f = (p - i)[:, None]
        out += (rev[i] * (1 - f) + rev[i + 1] * f) * g
    return out * dsp.ramp(len(out), 0.0, 0.015)[:, None]


def build(out, log, preview, stems):
    b = bells()
    dsp.write_wav(out / 'stingers' / 'S02_bells.wav', b)
    log('S02_bells', b)

    import cues.sanity as san
    pre = 2.6
    blow = dsp.to_peak(san.c01('heavy', 1), -6)
    battle = mixer.render(stems, np.full(n(pre), 3, float), start=95.0, events=[(pre - 0.3, blow)])
    demo = np.concatenate([battle, rewind(battle[-n(2.0):]), np.zeros((n(1.0), 2)), b])
    dsp.write_wav(preview / 'S02_demo.wav', demo, bits=16)
    log('S02_demo', demo)
