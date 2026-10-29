"""拟音素材（策划/13 §5.1）：纸、布、木头、湿的东西、黄铜机件、低语。每个函数返回单声道浮点数组，峰值不定，用的地方再定音量。

和 instruments.py 一样，seed 决定随机的那部分，同一个 seed 每次合成结果一样。
保留音色（13 §3.6）不在这里：掐灭灯芯和钢琴（R01）、玻璃清音（R02）、潮钟在 instruments.py，只给对应的信号用；
这里的玻璃瓶、玻璃罩一律做成闷、短、不带音高的「磕」，免得听成恢复理智。
"""
from __future__ import annotations

import numpy as np

from dsp import (SR, bandpass, decay, highpass, lowpass, n, ramp, resonator, smooth_noise, to_peak, tvec)


def _rng(seed):
    return np.random.default_rng(seed)


def grains(length: float, rate, rng, lo: float, hi: float, dur=(0.002, 0.008), amp=(0.3, 1.0)) -> np.ndarray:
    """颗粒：按 rate（每秒颗数，常数或逐采样数组）随机撒下一颗颗短促的带通噪声。纸的脆响、盐粒、木头裂纹都靠它。"""
    cnt = n(length)
    r = np.full(cnt, float(rate)) if np.isscalar(rate) else np.asarray(rate, float)
    hits = np.nonzero(rng.random(cnt) < r / SR)[0]
    out = np.zeros(cnt)
    src = bandpass(rng.standard_normal(cnt + n(dur[1]) + 1), lo, hi)
    for k in hits:
        m = n(rng.uniform(*dur))
        if m < 2:
            continue
        seg = src[k:k + m] * decay(m, m / SR) * rng.uniform(*amp)
        out[k:k + m] += seg[:cnt - k]
    return out


def shape(cnt: int, peak_at: float, attack_pow: float = 1.5, release_pow: float = 2.0) -> np.ndarray:
    """先升后降的包络，peak_at 是峰值在全长里的位置（0..1）。"""
    u = np.linspace(0, 1, cnt)
    up = np.clip(u / max(peak_at, 1e-3), 0, 1) ** attack_pow
    down = np.clip((1 - u) / max(1 - peak_at, 1e-3), 0, 1) ** release_pow
    return np.where(u < peak_at, up, down)


# ---------- 纸 ----------

def paper(length: float, seed: int = 0, rate: float = 220, peak_at: float = 0.35, bright: float = 1.0) -> np.ndarray:
    """纸面摩擦：一层细的嘶声 + 密集的脆响颗粒。bright < 1 更闷（湿纸）。"""
    rng = _rng(seed)
    cnt = n(length)
    env = shape(cnt, peak_at)
    hiss = bandpass(rng.standard_normal(cnt), 1800 * bright, 9000 * bright) * 0.18
    crisp = grains(length, rate * env, rng, 1500 * bright, 7000 * bright)
    return (hiss * env + crisp) * ramp(cnt, 0.002, 0.01)


def page_flip(seed: int = 0, length: float = 0.22) -> np.ndarray:
    """翻一页：纸擦过 + 页面落下时的一点低频「扑」。"""
    rng = _rng(seed)
    cnt = n(length)
    x = paper(length, seed, rate=320, peak_at=0.45)
    flap = lowpass(rng.standard_normal(cnt), 500, 2) * shape(cnt, 0.7, 2, 3) * 0.7
    return x + flap


def tear(seed: int = 0, length: float = 0.18) -> np.ndarray:
    """纸撕开一半：颗粒越撕越密，纤维断开的声音带一点共振。"""
    rng = _rng(seed)
    cnt = n(length)
    u = np.linspace(0, 1, cnt)
    rate = 400 + 2600 * u ** 0.7
    x = grains(length, rate * (1 - u ** 6), rng, 900, 5200, dur=(0.001, 0.004))
    x = x + resonator(x, 2300, 4) * 0.4
    return x * ramp(cnt, 0.003, 0.02)


def stack_knock(seed: int = 0) -> np.ndarray:
    """一叠纸在桌上磕齐：两下（第二下轻），纸边的沙沙 + 木桌的闷声。"""
    rng = _rng(seed)
    out = np.zeros(n(0.42))
    for at, g in ((0.0, 1.0), (0.13, 0.6)):
        m = n(0.12)
        thump = lowpass(rng.standard_normal(m), 650, 2) * decay(m, 0.06)
        body = resonator(thump, 210, 6) * 0.5 + thump
        rustle = paper(0.12, seed + int(at * 100), rate=900, peak_at=0.05) * 0.5
        k = n(at)
        out[k:k + m] += (body + rustle) * g
    return out


def stamp(seed: int = 0) -> np.ndarray:
    """火漆印压下：软的「噗」+ 一点黏连拉开的声音。"""
    rng = _rng(seed)
    cnt = n(0.35)
    press = lowpass(rng.standard_normal(cnt), 380, 2) * decay(cnt, 0.08)
    press = press + resonator(press, 160, 5) * 0.8
    stick = np.zeros(cnt)
    k = n(0.16)
    m = cnt - k
    stick[k:] = bandpass(rng.standard_normal(m), 600, 2500) * shape(m, 0.2) * 0.12
    return (press + stick) * ramp(cnt, 0.001, 0.03)


def quill(length: float = 0.32, seed: int = 0) -> np.ndarray:
    """鹅毛笔划过纸：粘滑摩擦的细脉冲激励高处的共振，加一层笔尖的嘶声。"""
    rng = _rng(seed)
    cnt = n(length)
    env = shape(cnt, 0.25, 1.2, 1.6)
    rate = 300 + 500 * env + 80 * smooth_noise(cnt, 20, rng)
    ph = np.cumsum(rate) / SR
    pulses = np.zeros(cnt)
    idx = np.nonzero(np.diff(np.floor(ph)) > 0)[0]
    pulses[idx] = rng.uniform(0.4, 1.0, len(idx))
    x = sum(resonator(pulses, f, q) * g for f, q, g in ((2600, 10, 1.0), (4300, 12, 0.6), (6100, 14, 0.35)))
    hiss = bandpass(rng.standard_normal(cnt), 3000, 9000) * 0.05
    return (x * 0.25 + hiss) * env


# ---------- 木头、布 ----------

def knock(seed: int = 0, f: float = 190, length: float = 0.16, soft: float = 1.0) -> np.ndarray:
    """指节敲木头：一个短冲激激励几个木头的模态，偏闷（soft 越大越闷）。knock / click / bottle 都归一化到峰值 1，叠的时候按增益配比。"""
    rng = _rng(seed)
    cnt = n(length)
    imp = np.zeros(cnt)
    m = n(0.003)
    imp[:m] = lowpass(rng.standard_normal(m), 2500 / soft) * np.hanning(m)
    x = sum(resonator(imp, f * r, q) * g for r, q, g in ((1.0, 9, 1.0), (2.31, 11, 0.55), (3.9, 12, 0.3)))
    x = x + lowpass(imp, 900) * 2
    return to_peak(lowpass(x * decay(cnt, length * 0.6), 3500 / soft), 0)


def cloth(length: float = 0.3, seed: int = 0, flutter: float = 22, peak_at: float = 0.3,
          lo: float = 250, hi: float = 2800) -> np.ndarray:
    """布料扬起 / 收紧：带通噪声 + 每秒十几二十下的抖动（布面拍打）。"""
    rng = _rng(seed)
    cnt = n(length)
    t = tvec(cnt)
    env = shape(cnt, peak_at)
    flap = 0.6 + 0.4 * np.abs(np.sin(np.pi * flutter * t + rng.uniform(0, 3)))
    x = bandpass(rng.standard_normal(cnt), lo, hi) * env * flap
    return x * ramp(cnt, 0.004, 0.02)


def wood_crack(seed: int = 0) -> np.ndarray:
    """木板裂开：一下脆的断裂 + 一串越来越稀的纤维裂纹，底下木头共振。"""
    rng = _rng(seed)
    cnt = n(0.45)
    u = np.linspace(0, 1, cnt)
    x = grains(0.45, 2500 * np.exp(-u * 9), rng, 500, 4500, dur=(0.001, 0.006))
    m = n(0.02)
    snap = np.zeros(cnt)
    snap[:m] = highpass(rng.standard_normal(m), 800) * decay(m, 0.01) * 2.0
    body = resonator(x + snap, 320, 6) * 0.8 + resonator(x + snap, 780, 8) * 0.4
    thud = lowpass(rng.standard_normal(cnt), 260, 2) * decay(cnt, 0.08) * 0.8
    return (x + snap + body + thud) * ramp(cnt, 0.0005, 0.05)


# ---------- 金属机件、钱币、瓶子 ----------

def click(seed: int = 0, f: float = 2900, second: float = 0.028, length: float = 0.12) -> np.ndarray:
    """黄铜机件「咔哒」：两下很短的金属冲激，第二下轻、略高。高频阻尼大，不留「叮」的尾巴。"""
    rng = _rng(seed)
    cnt = n(length)
    out = np.zeros(cnt)
    for at, g, ff in ((0.0, 1.0, f), (second, 0.55, f * 1.18)):
        k = n(at)
        m = n(0.002)
        imp = np.zeros(cnt - k)
        imp[:m] = rng.standard_normal(m)
        y = sum(resonator(imp, ff * r, 25) * a for r, a in ((1.0, 1.0), (1.73, 0.6), (2.41, 0.35)))
        out[k:] += y * decay(cnt - k, 0.05) * g
    return to_peak(lowpass(out, 9000), 0)


def coins(seed: int = 0, count: int = 4) -> np.ndarray:
    """几枚湿硬币落在木桌上：每枚一下闷的金属碰撞（短、低通，像是沾了海水），错开几十毫秒。"""
    rng = _rng(seed)
    out = np.zeros(n(0.55))
    at = 0.0
    for i in range(count):
        m = n(0.16)
        t = tvec(m)
        f0 = rng.uniform(2100, 3300)
        y = sum(np.sin(2 * np.pi * f0 * r * t + rng.uniform(0, 6)) * a * decay(m, 0.09 / r)
                for r, a in ((1.0, 1.0), (2.37, 0.45), (3.92, 0.25)))
        y = lowpass(y, 5500) + lowpass(rng.standard_normal(m), 900) * decay(m, 0.02) * 0.6
        k = n(at)
        out[k:k + m] += y[:len(out) - k] * rng.uniform(0.5, 1.0) * (0.85 ** i)
        at += rng.uniform(0.035, 0.09)
    return out


def bottle(seed: int = 0, f: float = 1150) -> np.ndarray:
    """厚玻璃瓶轻碰：两个阻尼很大的模态加一下木头声，0.1 秒就没了（不是 R02 那种会唱的玻璃）。"""
    rng = _rng(seed)
    cnt = n(0.18)
    t = tvec(cnt)
    y = sum(np.sin(2 * np.pi * f * r * t + rng.uniform(0, 6)) * a * decay(cnt, 0.06)
            for r, a in ((1.0, 1.0), (2.64, 0.35)))
    return to_peak(to_peak(lowpass(y, 4000), 0) * 0.7 + knock(seed, 260, 0.18) * 0.5, 0)


def cork(seed: int = 0) -> np.ndarray:
    """拔软木塞：先一点吱扭的摩擦，然后「啵」。"""
    rng = _rng(seed)
    cnt = n(0.2)
    out = np.zeros(cnt)
    m = n(0.09)
    squeak = resonator(grains(0.09, 900, rng, 600, 3000), 900, 6) * shape(m, 0.6) * 0.5
    out[:m] += squeak
    k = n(0.09)
    p = n(0.03)
    tt = tvec(p)
    pop = np.sin(2 * np.pi * np.cumsum(700 - 400 * tt / 0.03) / SR) * decay(p, 0.02)
    out[k:k + p] += pop
    return out * ramp(cnt, 0.002, 0.02)


def drip(seed: int = 0, f: float = 1300, length: float = 0.12) -> np.ndarray:
    """一滴水 / 墨：气泡共振的音高往上滑的「啵」。"""
    rng = _rng(seed)
    m = n(length)
    t = tvec(m)
    f = f * rng.uniform(0.9, 1.1)
    fd = f * (1 + 1.6 * np.clip(t / 0.03, 0, 1))
    return np.sin(2 * np.pi * np.cumsum(fd) / SR) * decay(m, 0.035) * ramp(m, 0.0008)


def glug(seed: int = 0, length: float = 0.45, count: int = 3, f: float = 420) -> np.ndarray:
    """喝 / 倒：几个低沉的气泡「咕」，越来越快。"""
    rng = _rng(seed)
    out = np.zeros(n(length))
    at = 0.0
    for i in range(count):
        d = drip(seed + i, f * rng.uniform(0.85, 1.2), 0.1)
        k = n(at)
        out[k:k + len(d)] += d[:len(out) - k] * rng.uniform(0.6, 1.0)
        at += 0.13 * (0.85 ** i)
    return lowpass(out, 2000)


def liquid(length: float = 0.6, seed: int = 0, density: float = 30, fall: bool = True) -> np.ndarray:
    """墨往下流：一串小气泡 + 低通的水流噪声，fall = True 时音高和密度一路往下。"""
    rng = _rng(seed)
    cnt = n(length)
    u = np.linspace(0, 1, cnt)
    body = lowpass(rng.standard_normal(cnt), 500, 2) * (0.3 + 0.7 * np.abs(smooth_noise(cnt, 9, rng)))
    out = body * shape(cnt, 0.15, 1, 1.5) * 0.6
    at = 0.0
    while at < length - 0.08:
        prog = at / length
        f = (1600 - 1100 * prog) if fall else rng.uniform(500, 1500)
        d = drip(int(rng.integers(1 << 30)), f, 0.08) * (1 - prog) ** 0.7
        k = n(at)
        out[k:k + len(d)] += d[:cnt - k] * rng.uniform(0.2, 0.6)
        at += rng.exponential(1 / density)
    return out


# ---------- 人声质感（不是保留的耳语：保留的是疯语和 R03 那条，见 whisper） ----------

_VOWELS = [(750, 1150, 2500), (420, 2000, 2700), (320, 850, 2400), (520, 1500, 2550), (600, 1000, 2450)]


def breath(length: float = 0.5, seed: int = 0, peak_at: float = 0.6, pitch: float = 0.0,
           vowel: int = 3) -> np.ndarray:
    """吸气 / 叹气：共振峰塑形的气流噪声；pitch > 0 时混一点那个音高的浊音（叹气）。"""
    rng = _rng(seed)
    cnt = n(length)
    f1, f2, f3 = _VOWELS[vowel]
    air = rng.standard_normal(cnt)
    x = resonator(air, f1, 5) * 1.0 + resonator(air, f2, 7) * 0.6 + resonator(air, f3, 9) * 0.3
    x = bandpass(x, 300, 6000) * 0.3
    if pitch:
        t = tvec(cnt)
        f = pitch * (1 - 0.18 * t / length)
        ph = 2 * np.pi * np.cumsum(f) / SR
        voice = sum(np.sin(k * ph) / k ** 1.2 for k in range(1, 18))
        voice = resonator(voice, f1, 4) + resonator(voice, f2, 6) * 0.5
        x = x + lowpass(voice, 2500) * 0.05
    return x * shape(cnt, peak_at, 1.6, 1.4)


def whisper(length: float = 0.8, seed: int = 0, syllables: float = 5.0, clear: float = 0.0) -> np.ndarray:
    """听不清的低语（保留音色，13 §3.6：只给疯语和精神攻击用）：每个音节换一个元音的共振峰，
    中间夹着「s」「sh」的擦音。clear 越大，音节越整齐、越像听清了半句。"""
    rng = _rng(seed)
    cnt = n(length)
    t = tvec(cnt)
    out = np.zeros(cnt)
    at = rng.uniform(0, 0.05)
    while at < length - 0.05:
        dur = (1 / syllables) * rng.uniform(1 - 0.4 * (1 - clear), 1 + 0.4 * (1 - clear))
        m = n(min(dur, length - at))
        if m < n(0.03):
            break
        f1, f2, f3 = _VOWELS[int(rng.integers(len(_VOWELS)))]
        air = rng.standard_normal(m)
        v = resonator(air, f1, 6) + resonator(air, f2, 9) * 0.7 + resonator(air, f3, 11) * 0.35
        seg = bandpass(v, 250, 7000) * shape(m, 0.35, 1.3, 1.8) * rng.uniform(0.5, 1.0)
        if rng.random() < 0.45:   # 擦音
            s = min(m, n(rng.uniform(0.04, 0.09)))
            fr = highpass(rng.standard_normal(s), rng.choice([3800, 5200])) * shape(s, 0.5) * 0.5
            seg[:s] += fr
        k = n(at)
        out[k:k + m] += seg[:cnt - k]
        at += dur * rng.uniform(0.95, 1.15)
    wobble = 1 + 0.25 * smooth_noise(cnt, 3, rng)
    return out * wobble * ramp(cnt, 0.03, 0.08) * (0.5 + 0.5 * np.clip(t / 0.1, 0, 1))


# ---------- 其他 ----------

def whoosh(length: float = 0.3, seed: int = 0, lo: float = 200, hi: float = 2400, peak_at: float = 0.55) -> np.ndarray:
    """一道风：两段频带先后到峰（先低后高），听起来像掠过。"""
    rng = _rng(seed)
    cnt = n(length)
    a = bandpass(rng.standard_normal(cnt), lo, (lo + hi) / 3) * shape(cnt, peak_at * 0.8)
    b = bandpass(rng.standard_normal(cnt), (lo + hi) / 3, hi) * shape(cnt, min(0.95, peak_at * 1.15), 2.2)
    return (a + b * 0.7) * ramp(cnt, 0.004, 0.02)


def salt(length: float = 0.45, seed: int = 0) -> np.ndarray:
    """盐粒撒落：一大把细颗粒，先密后稀。"""
    rng = _rng(seed)
    cnt = n(length)
    u = np.linspace(0, 1, cnt)
    return grains(length, 1800 * np.exp(-u * 4) * shape(cnt, 0.08, 1, 0.3), rng, 4000, 12000,
                  dur=(0.0006, 0.002), amp=(0.2, 1.0))


def match(seed: int = 0) -> np.ndarray:
    """划火柴：一下擦（磷头的粗颗粒）+ 点着的「呼」，然后火苗抖几下。"""
    rng = _rng(seed)
    cnt = n(0.9)
    out = np.zeros(cnt)
    m = n(0.11)
    strike = grains(0.11, 3500, rng, 1500, 8000, dur=(0.001, 0.003)) * shape(m, 0.4) * 1.2
    out[:m] += strike
    k = n(0.08)
    f = cnt - k
    flame = lowpass(rng.standard_normal(f), 900, 2) * shape(f, 0.12, 1.2, 1.2)
    flame *= 0.8 + 0.4 * np.abs(smooth_noise(f, 14, rng))
    out[k:] += flame * 0.9 + bandpass(rng.standard_normal(f), 2000, 6000) * decay(f, 0.15) * 0.3
    return out * ramp(cnt, 0.001, 0.15)


def tremolo(f0s, length: float = 0.7, seed: int = 0, rate: float = 13.0) -> np.ndarray:
    """弦乐颤音：几根略走音的锯齿弦（只保留低的几个分音，偏暗）各自快速运弓，加弓毛噪声。"""
    rng = _rng(seed)
    cnt = n(length)
    t = tvec(cnt)
    out = np.zeros(cnt)
    for i, f0 in enumerate(f0s):
        fm = 1 + 0.002 * smooth_noise(cnt, 4, rng)
        ph = 2 * np.pi * np.cumsum(f0 * fm * (1 + rng.uniform(-0.002, 0.002))) / SR
        tone = sum(np.sin(k * ph + rng.uniform(0, 6)) / k for k in range(1, 12) if f0 * k < 7000)
        bow = np.abs(np.sin(np.pi * rate * t * rng.uniform(0.95, 1.05) + rng.uniform(0, 3))) ** 0.6
        out += tone * (0.55 + 0.45 * bow)
    out = lowpass(out, 3200) + bandpass(rng.standard_normal(cnt), 1500, 5000) * 0.02
    return out * shape(cnt, 0.35, 1.2, 1.5)


def slither(length: float = 0.9, seed: int = 0) -> np.ndarray:
    """湿滑的东西升起 / 蠕动：低通噪声慢慢起伏，夹着吸盘松开的「啵」。"""
    rng = _rng(seed)
    cnt = n(length)
    body = bandpass(rng.standard_normal(cnt), 120, 1100) * (0.4 + 0.6 * np.abs(smooth_noise(cnt, 6, rng)))
    out = body * shape(cnt, 0.55, 1.4, 1.5)
    for _ in range(int(length * 7)):
        at = rng.uniform(0.1, length - 0.1)
        m = n(0.05)
        tt = tvec(m)
        s = np.sin(2 * np.pi * np.cumsum(rng.uniform(250, 600) * (1 - 0.5 * tt / 0.05)) / SR) * decay(m, 0.03)
        k = n(at)
        out[k:k + m] += s[:cnt - k] * rng.uniform(0.2, 0.5)
    return out


def ground_crack(seed: int = 0) -> np.ndarray:
    """地面裂开：低沉的碎裂颗粒 + 一下闷响。"""
    rng = _rng(seed)
    cnt = n(0.5)
    u = np.linspace(0, 1, cnt)
    x = grains(0.5, 1600 * np.exp(-u * 6), rng, 200, 1800, dur=(0.002, 0.01))
    thud = lowpass(rng.standard_normal(cnt), 160, 2) * decay(cnt, 0.25) * 1.6
    return (x + thud) * ramp(cnt, 0.001, 0.05)
