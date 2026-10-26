"""程序化乐器和音效素材（策划/13 §2.1 的调色板）。每个函数返回单声道浮点数组，峰值不定，用的地方再定音量。

seed 决定随机的那部分（相位、拍频、抖动），同一个 seed 每次合成结果一样。
"""
from __future__ import annotations

import numpy as np

from dsp import (SR, bandpass, decay, highpass, lowpass, n, partials, ramp, resonator,
                 smooth_noise, tvec)


def _rng(seed):
    return np.random.default_rng(seed)


# ---------- 潮钟（保留音色，13 §3.6） ----------

# 小三度大钟的分音：(相对 prime 的比例, 振幅, t60 秒)。tierce 比 prime 高一个偏小的小三度，钟声发阴就靠它（13 §2.2）
_BELL = [
    (0.5, 0.50, 16.0),    # hum
    (1.0, 0.45, 11.0),    # prime
    (1.189, 0.70, 8.0),   # tierce
    (1.5, 0.22, 5.5),     # quint
    (2.0, 1.00, 6.5),     # nominal
    (2.52, 0.30, 3.4),
    (2.66, 0.32, 2.8),
    (3.01, 0.22, 2.2),
    (4.0, 0.20, 1.5),
    (4.95, 0.12, 1.0),
    (5.95, 0.09, 0.7),
    (7.07, 0.07, 0.5),
    (8.2, 0.05, 0.35),
]


def bell(prime_hz: float, length: float, decay_scale: float = 1.0, strike: float = 1.0,
         seed: int = 0) -> np.ndarray:
    rng = _rng(seed)
    cnt = n(length)
    ratios = [r * (1 + rng.uniform(-0.003, 0.003)) if r != 0.5 else r for r, _, _ in _BELL]
    amps = [a * rng.uniform(0.85, 1.15) for _, a, _ in _BELL]
    t60s = [t * decay_scale for _, _, t in _BELL]
    x = partials(prime_hz, cnt, ratios, amps, t60s, rng, beat=0.35)
    # 敲击：一小段带通噪声，金属的「当」
    k = n(0.03)
    hitn = bandpass(rng.standard_normal(k), 900, 6000) * decay(k, 0.025)
    x[:k] += hitn * 0.5 * strike
    return x * ramp(cnt, 0.0015, 0.08)


# ---------- 旧立式钢琴（R01） ----------

def piano(f0: float, length: float, vel: float = 0.7, hold: float | None = None,
          detune: float = 3.0, seed: int = 0) -> np.ndarray:
    """三根弦略微走音的旧钢琴：分音带一点非谐性，双段衰减（先快后慢），音锤在 1/8 处击弦。hold 秒后制音器落下。"""
    rng = _rng(seed)
    cnt = n(length)
    t = tvec(cnt)
    B = 0.0003
    count = min(28, int(15000 / f0))
    k = np.arange(1, count + 1)
    ratios = k * np.sqrt(1 + B * k ** 2)
    amps = np.abs(np.sin(np.pi * k / 8)) / k ** (1.4 - 0.6 * vel)
    amps /= 1 + (k * f0 / (2200 + 2800 * vel)) ** 2     # 旧琴的毡锤偏闷
    t60_1 = 9.0 * (146.8 / f0) ** 0.6
    out = np.zeros(cnt)
    for c in (-1, 0, 1):
        d = 2 ** (c * detune * rng.uniform(0.6, 1.3) / 1200)
        for r, a, kk in zip(ratios, amps, k):
            f = f0 * r * d
            if f > SR * 0.45:
                break
            t60 = t60_1 / (1 + 0.25 * (kk - 1))
            env = 0.65 * decay(cnt, t60 / 3.5) + 0.35 * decay(cnt, t60)
            out += a * env * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28))
    out /= 3
    m = n(0.014)
    out[:m] += lowpass(rng.standard_normal(m), 1800) * decay(m, 0.012) * 0.08 * vel
    if hold is not None:
        h = n(hold)
        if h < cnt:
            out[h:] *= decay(cnt - h, 0.35)
    return out * ramp(cnt, 0.001, 0.03)


# ---------- 玻璃清音（R02） ----------

def glass(f0: float, length: float, seed: int = 0) -> np.ndarray:
    rng = _rng(seed)
    cnt = n(length)
    x = partials(f0, cnt, [1, 2.32, 4.25, 6.63, 9.38], [1, 0.28, 0.11, 0.05, 0.025],
                 [3.2, 1.4, 0.7, 0.4, 0.25], rng, beat=0.9)
    k = n(0.004)
    x[:k] += highpass(rng.standard_normal(k), 5000) * 0.06
    return x * ramp(cnt, 0.002, 0.05)


# ---------- 簧风琴 ----------

def harmonium(f0: float, length: float, seed: int = 0, bright: float = 1.0,
              attack: float = 0.09, release: float = 0.3) -> np.ndarray:
    """脚踏簧风琴：锯齿状的簧片音 + 两个鼻音共振峰，踩风箱带来的轻微起伏，第二排簧片高 3 音分。"""
    rng = _rng(seed)
    cnt = n(length)
    fmod = 1 + 0.0006 * smooth_noise(cnt, 3, rng)
    phase = 2 * np.pi * np.cumsum(f0 * fmod) / SR
    count = int(min(40, 12000 / f0))
    out = np.zeros(cnt)
    for rank, (shift, level) in enumerate(((1.0, 1.0), (2 ** (3 / 1200), 0.35))):
        for k in range(1, count + 1):
            f = f0 * k * shift
            g = (1 + 1.5 * np.exp(-((f - 1100) / 400) ** 2) + 0.8 * np.exp(-((f - 2600) / 700) ** 2))
            g /= 1 + (f / (4500 * bright)) ** 4
            out += level * g / k ** 0.85 * np.sin(k * shift * phase + rng.uniform(0, 6.28))
    out *= 1 + 0.05 * smooth_noise(cnt, 1.2, rng)
    out += bandpass(rng.standard_normal(cnt), 1000, 4000) * 0.012
    return out * ramp(cnt, attack, release, 1.5)


# ---------- 低音提琴拨弦 ----------

def pizz(f0: float, length: float, vel: float = 0.8, seed: int = 0) -> np.ndarray:
    rng = _rng(seed)
    cnt = n(length)
    t = tvec(cnt)
    fmod = 1 + 0.004 * np.exp(-t / 0.03)            # 刚拨的一瞬间弦绷得紧，音高略高
    phase = 2 * np.pi * np.cumsum(f0 * fmod) / SR
    out = np.zeros(cnt)
    t60_1 = 1.8 * (73.4 / f0) ** 0.3
    for k in range(1, 25):
        if f0 * k > 6000:
            break
        a = abs(np.sin(np.pi * k * 0.2)) / k ** 1.1
        out += a * decay(cnt, t60_1 / (1 + 0.6 * (k - 1))) * np.sin(k * phase + rng.uniform(0, 6.28))
    m = n(0.025)
    out[:m] += lowpass(rng.standard_normal(m), 900) * decay(m, 0.02) * 0.25 * vel
    return out * vel * ramp(cnt, 0.002, 0.05)


# ---------- 远处闷鼓 ----------

def drum(seed: int = 0, f0: float = 58, length: float = 0.9) -> np.ndarray:
    rng = _rng(seed)
    cnt = n(length)
    t = tvec(cnt)
    f = f0 * (1 + 0.6 * np.exp(-t / 0.02))
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * decay(cnt, 0.55)
    skin = lowpass(rng.standard_normal(cnt), 900) * decay(cnt, 0.12) * 0.35
    return (body + skin) * ramp(cnt, 0.002, 0.05)


# ---------- 清明层：小提琴泛音 ----------

def harmonic_tone(f0: float, length: float, seed: int = 0, attack: float = 0.9,
                  release: float = 1.1) -> np.ndarray:
    rng = _rng(seed)
    cnt = n(length)
    t = tvec(cnt)
    vib = 0.0035 * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.6) / 0.8, 0, 1)
    fmod = 1 + vib + 0.0008 * smooth_noise(cnt, 2, rng)
    phase = 2 * np.pi * np.cumsum(f0 * fmod) / SR
    x = np.sin(phase) + 0.08 * np.sin(2 * phase) + 0.03 * np.sin(3 * phase)
    x += bandpass(rng.standard_normal(cnt), f0 * 0.9, f0 * 1.8) * 0.04
    return x * ramp(cnt, attack, release, 1.3)


# ---------- 深渊层（保留音色，13 §1.2 支柱 2） ----------

def deep_breath(length: float, period: float, seed: int = 0, lo: float = 36.71,
                hi: float = 38.89) -> np.ndarray:
    """深潮的呼吸：吸气时从 D1 滑到 E♭1，呼气滑回 D1（13 §2.3），叠一层低通噪声当气流。"""
    rng = _rng(seed)
    cnt = n(length)
    t = tvec(cnt)
    ph = (t % period) / period
    breath = np.sin(np.pi * ph) ** 2                    # 0 → 1 → 0，一个周期一吸一呼
    inhale = np.clip(np.sin(2 * np.pi * ph), 0, 1)      # 前半周期
    f = lo + (hi - lo) * inhale
    phase = 2 * np.pi * np.cumsum(f) / SR
    tone = np.sin(phase) + 0.45 * np.sin(2 * phase) + 0.2 * np.sin(3 * phase) + 0.08 * np.sin(4 * phase)
    air = lowpass(rng.standard_normal(cnt), 220, 4) * 3.0
    x = (tone * 0.8 + air) * (0.25 + 0.75 * breath)
    return highpass(x, 30, 4)


_METAL = [1.0, 1.594, 2.136, 2.296, 2.653, 3.156, 3.501, 4.06]


def bowed_metal(f0: float, length: float, seed: int = 0, fade: bool = True) -> np.ndarray:
    """弓擦镲 / 锯琴：非谐的金属分音各自慢慢起伏，加一点弓毛噪声。fade = False 时不带起音和收尾（循环织体用）。"""
    rng = _rng(seed)
    cnt = n(length)
    out = np.zeros(cnt)
    for i, r in enumerate(_METAL):
        f = f0 * r * (1 + rng.uniform(-0.004, 0.004))
        fmod = 1 + 0.002 * smooth_noise(cnt, 0.4, rng)
        amp = np.clip(0.55 + 0.6 * smooth_noise(cnt, 0.3, rng), 0, None) / (1 + 0.5 * i)
        out += amp * np.sin(2 * np.pi * np.cumsum(f * fmod) / SR + rng.uniform(0, 6.28))
    out += bandpass(rng.standard_normal(cnt), f0, f0 * 4) * 0.03
    return out * ramp(cnt, 2.0, 2.0, 1.5) if fade else out


# ---------- 环境 ----------

def waves(length: float, seed: int = 0, period: float = 7.5) -> np.ndarray:
    """浪拍木桩：每道浪一次起伏，低通的水体 + 浪头碎开的高频。返回立体声（左右两道独立的浪）。"""
    out = []
    for c in range(2):
        rng = _rng(seed * 2 + c)
        cnt = n(length)
        t = tvec(cnt)
        env = np.zeros(cnt)
        at = rng.uniform(0, period * 0.6)
        while at < length + period:
            d = t - at
            rise = np.clip(d / 2.6, 0, 1) ** 2
            fall = np.exp(-np.clip(d - 2.6, 0, None) / 1.4)
            env += np.where(d > 0, rise * fall, 0) * rng.uniform(0.6, 1.0)
            at += period * rng.uniform(0.75, 1.25)
        body = lowpass(rng.standard_normal(cnt), 650, 2) * (0.35 + env ** 1.5)
        wash = bandpass(rng.standard_normal(cnt), 1500, 6500) * np.roll(env, n(0.4)) ** 3 * 0.4
        rumble = lowpass(rng.standard_normal(cnt), 110, 2) * 0.9
        out.append(body + wash + rumble)
    return np.stack(out, axis=1)


def creak(length: float, seed: int = 0) -> np.ndarray:
    """木桩和绳子的吱呀：粘滑摩擦的脉冲串（每秒 18–60 下）激励几个木头共振峰。"""
    rng = _rng(seed)
    cnt = n(length)
    t = tvec(cnt)
    u = t / length
    rate = 18 + 42 * np.sin(np.pi * u) ** 1.5 + 6 * smooth_noise(cnt, 6, rng)
    ph = np.cumsum(rate) / SR
    pulses = np.zeros(cnt)
    idx = np.nonzero(np.diff(np.floor(ph)) > 0)[0]
    pulses[idx] = rng.uniform(0.5, 1.0, len(idx))
    x = sum(resonator(pulses, f, q) * g for f, q, g in
            ((430, 14, 1.0), (920, 18, 0.7), (1680, 20, 0.45), (2450, 22, 0.25)))
    return x * np.sin(np.pi * u) ** 0.6


def snuff(seed: int = 0) -> np.ndarray:
    """手指掐灭灯芯：一下短促的嘶声和一点「噗」。"""
    rng = _rng(seed)
    cnt = n(0.24)
    hiss = bandpass(rng.standard_normal(cnt), 2500, 9000)
    crackle = (rng.random(cnt) < 600 / SR) * rng.uniform(1, 4, cnt)
    sizzle = hiss * (1 + crackle) * decay(cnt, 0.2) * ramp(cnt, 0.004)
    m = n(0.05)
    sizzle[:m] += lowpass(rng.standard_normal(m), 500) * decay(m, 0.04) * 1.2
    return sizzle * ramp(cnt, 0.0, 0.03)


# ---------- 闷击（C01） ----------

_HIT = {
    # 主体从 f0 落到 f1：落点在 50 Hz 以上，笔记本喇叭也放得出「肉」；更低的分量交给重击的 sub
    'light': dict(f0=135, f1=72, body_t=0.16, thud_lp=1400, drops=10, dur=0.45, sub=0.0, crack=0.0, splash=0.55),
    'medium': dict(f0=118, f1=62, body_t=0.24, thud_lp=1200, drops=18, dur=0.6, sub=0.25, crack=0.15, splash=0.75),
    'heavy': dict(f0=104, f1=52, body_t=0.40, thud_lp=1000, drops=30, dur=0.9, sub=0.55, crack=0.3, splash=1.0),
}


def hit(tier: str, seed: int = 0) -> np.ndarray:
    """闷击 + 墨点溅开的湿声（13 §5.1：布、木头、湿的东西，不要金属声）。"""
    rng = _rng(seed)
    p = {k: (v * rng.uniform(0.92, 1.08) if isinstance(v, float) else v) for k, v in _HIT[tier].items()}
    cnt = n(p['dur'])
    t = tvec(cnt)
    f = p['f1'] + (p['f0'] - p['f1']) * np.exp(-t / 0.035)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * decay(cnt, p['body_t'])
    body = np.tanh(2.5 * body) / np.tanh(2.5)
    thud = lowpass(rng.standard_normal(cnt), p['thud_lp']) * decay(cnt, 0.07) * 0.9
    x = body + thud
    s = n(0.008)
    splash = bandpass(rng.standard_normal(cnt - s), 700, 3500) * decay(cnt - s, 0.06) * 0.45 * p['splash']
    x[s:] += splash
    for _ in range(p['drops']):
        at = 0.015 + rng.exponential(0.07)
        if at > p['dur'] - 0.06:
            continue
        m = n(rng.uniform(0.015, 0.03))
        tt = tvec(m)
        fd = rng.uniform(700, 2600) * (1 + 1.8 * tt / 0.02)
        drop = np.sin(2 * np.pi * np.cumsum(fd) / SR) * decay(m, 0.025)
        k = n(at)
        x[k:k + m] += drop[:cnt - k] * rng.uniform(0.04, 0.14) * p['splash'] * np.exp(-at / 0.15)
    if p['crack']:
        m = n(0.03)
        cr = resonator(highpass(rng.standard_normal(m), 1800) * decay(m, 0.008), 650, 8)
        x[:m] += cr * p['crack'] * 3
    if p['sub']:
        x += np.sin(2 * np.pi * 42 * t) * decay(cnt, 0.4) * p['sub'] * ramp(cnt, 0.004)
    return highpass(x, 30, 2) * ramp(cnt, 0.0005, 0.04)
