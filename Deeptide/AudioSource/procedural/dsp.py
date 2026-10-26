"""音频合成的底层工具：音高、包络、滤波、混响、声像、响度、读写 WAV。

全部按 48 kHz 浮点处理。单声道是 (n,)，立体声是 (n, 2)。规格见 策划/13 §6.3。
"""
from __future__ import annotations

import pathlib
import wave

import numpy as np
from scipy import signal

SR = 48000

_NOTE = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}


def n(seconds: float) -> int:
    return int(round(seconds * SR))


def tvec(count: int) -> np.ndarray:
    return np.arange(count) / SR


def midi(name: str) -> int:
    """'D3'、'Eb4'、'F#2' → MIDI 号。"""
    step = _NOTE[name[0]]
    rest = name[1:]
    while rest and rest[0] in 'b#':
        step += -1 if rest[0] == 'b' else 1
        rest = rest[1:]
    return 12 * (int(rest) + 1) + step


def hz(note) -> float:
    m = midi(note) if isinstance(note, str) else note
    return 440.0 * 2 ** ((m - 69) / 12)


def cents(c: float) -> float:
    return 2 ** (c / 1200)


def db(x: float) -> float:
    return 10 ** (x / 20)


# ---------- 包络 ----------

def decay(count: int, t60: float) -> np.ndarray:
    """指数衰减，t60 秒后降 60 dB。"""
    return 10 ** (-3 * tvec(count) / max(t60, 1e-4))


def ramp(count: int, attack: float, release: float = 0.0, curve: float = 1.0) -> np.ndarray:
    """起音 attack 秒线性升到 1，最后 release 秒降到 0（curve > 1 更陡）。"""
    env = np.ones(count)
    a, r = min(n(attack), count), min(n(release), count)
    if a > 0:
        env[:a] = np.linspace(0, 1, a) ** curve
    if r > 0:
        env[count - r:] *= np.linspace(1, 0, r) ** curve
    return env


def smooth_noise(count: int, rate: float, rng: np.random.Generator) -> np.ndarray:
    """每秒约 rate 个控制点的平滑随机曲线，值域约 −1..1，用来做慢速的抖动和起伏。"""
    pts = max(4, int(count / SR * rate) + 4)
    knots = rng.uniform(-1, 1, pts)
    x = np.linspace(0, pts - 3, count)
    i = np.floor(x).astype(int)
    f = x - i
    # Catmull-Rom
    p0, p1, p2, p3 = knots[i], knots[i + 1], knots[i + 2], knots[np.minimum(i + 3, pts - 1)]
    return 0.5 * ((2 * p1) + (-p0 + p2) * f + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f ** 2
                  + (-p0 + 3 * p1 - 3 * p2 + p3) * f ** 3)


# ---------- 振荡器 ----------

def sine(freq, count: int, phase: float = 0.0) -> np.ndarray:
    """freq 可以是常数或逐采样的数组（滑音、颤音）。"""
    if np.isscalar(freq):
        return np.sin(2 * np.pi * freq * tvec(count) + phase)
    return np.sin(2 * np.pi * np.cumsum(freq) / SR + phase)


def partials(f0, count: int, ratios, amps, t60s, rng, beat: float = 0.0,
             fmod: np.ndarray | None = None) -> np.ndarray:
    """加法合成：每个分音 = 比例 × f0，各自的振幅和衰减。beat > 0 时每个分音拆成两个相差 beat Hz 的模态（拍频）。"""
    out = np.zeros(count)
    nyq = SR / 2 * 0.95
    for r, a, t60 in zip(ratios, amps, t60s):
        f = f0 * r
        if f >= nyq or a == 0:
            continue
        env = decay(count, t60) * a
        freq = f if fmod is None else f * fmod
        if beat > 0:
            d = beat * rng.uniform(0.5, 1.5) / 2
            out += env * 0.5 * (sine(freq + d, count, rng.uniform(0, 6.28))
                                + sine(freq - d, count, rng.uniform(0, 6.28)))
        else:
            out += env * sine(freq, count, rng.uniform(0, 6.28))
    return out


# ---------- 滤波 ----------

def _sos(kind: str, fc, order: int):
    return signal.butter(order, fc, btype=kind, fs=SR, output='sos')


def lowpass(x, fc: float, order: int = 2):
    fc = min(fc, SR / 2 * 0.98)
    return signal.sosfilt(_sos('lowpass', fc, order), x, axis=0)


def highpass(x, fc: float, order: int = 2):
    return signal.sosfilt(_sos('highpass', fc, order), x, axis=0)


def bandpass(x, lo: float, hi: float, order: int = 2):
    return signal.sosfilt(_sos('bandpass', [lo, min(hi, SR / 2 * 0.98)], order), x, axis=0)


def resonator(x, f: float, q: float):
    """二阶带通共振（窄峰），木头吱呀、共振峰用。"""
    b, a = signal.iirpeak(f, q, fs=SR)
    return signal.lfilter(b, a, x, axis=0)


def peaking(x, f: float, gain_db: float, q: float = 1.0):
    """RBJ 峰值均衡。"""
    A = 10 ** (gain_db / 40)
    w = 2 * np.pi * f / SR
    alpha = np.sin(w) / (2 * q)
    b = [1 + alpha * A, -2 * np.cos(w), 1 - alpha * A]
    a = [1 + alpha / A, -2 * np.cos(w), 1 - alpha / A]
    return signal.lfilter(np.array(b) / a[0], np.array(a) / a[0], x, axis=0)


# ---------- 空间 ----------

def reverb_ir(rt60: float, predelay: float = 0.02, damp: float = 0.45, seed: int = 7) -> np.ndarray:
    """立体声冲激响应：早期反射 + 指数衰减的噪声尾巴；高频衰减得比低频快（damp = 高频 t60 / 低频 t60）。
    左右用不相关的噪声，叠成单声道不会抵消（13 §6.3「单声道兼容」）。"""
    rng = np.random.default_rng(seed)
    length = n(rt60 * 1.1 + predelay)
    ir = np.zeros((length, 2))
    p = n(predelay)
    tail = length - p
    for c in range(2):
        noise = rng.standard_normal(tail)
        lo = lowpass(noise, 700) * decay(tail, rt60)
        mid = bandpass(noise, 700, 4000) * decay(tail, rt60 * (1 + damp) / 2)
        hi = highpass(noise, 4000) * decay(tail, rt60 * damp)
        body = (lo + mid * 0.8 + hi * 0.5) * ramp(tail, 0.012)
        ir[p:, c] = body
        for _ in range(9):   # 早期反射
            k = p + n(rng.uniform(0.004, 0.06))
            if k < length:
                ir[k, c] += rng.uniform(0.2, 0.6) * rng.choice([-1, 1])
    return ir / np.sqrt(np.sum(ir ** 2) / 2)


def convolve(x, ir) -> np.ndarray:
    """x 单声道或立体声，ir 立体声；输出立体声，长度 = len(x) + len(ir) − 1。"""
    x2 = stereo(x)
    return np.stack([signal.fftconvolve(x2[:, c], ir[:, c]) for c in range(2)], axis=1)


def stereo(x) -> np.ndarray:
    return np.stack([x, x], axis=1) if x.ndim == 1 else x


def pan(x, p: float) -> np.ndarray:
    """等功率声像，p = −1 最左，+1 最右。"""
    th = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(th), x * np.sin(th)], axis=1)


# ---------- 拼接 ----------

def place(buf: np.ndarray, x: np.ndarray, at: int, gain: float = 1.0):
    """把 x 叠进 buf 的 at 处（越界部分丢掉）；单声道叠进立体声时两边都加。"""
    if at >= len(buf):
        return
    if at < 0:
        x, at = x[-at:], 0
    end = min(len(buf), at + len(x))
    seg = x[:end - at] * gain
    if buf.ndim == 2 and seg.ndim == 1:
        seg = seg[:, None]
    buf[at:end] += seg


def fold_tail(x: np.ndarray, loop_len: int) -> np.ndarray:
    """循环曲：把超出循环长度的尾巴（还在响的音、混响）叠回开头（13 §6.3「混响尾巴预先折回循环开头」）。"""
    out = x[:loop_len].copy()
    rest = x[loop_len:]
    while len(rest):
        k = min(len(rest), loop_len)
        out[:k] += rest[:k]
        rest = rest[k:]
    return out


def read_looped(x: np.ndarray, count: int, rate: float, start: float = 0.0, delay: int = 0) -> np.ndarray:
    """模拟 Unity 的 AudioSource：loop = true、pitch = rate（变速变调）、PlayScheduled 晚 delay 个采样起播。
    start 是起播前已经播过的秒数（用来模拟战斗打了几分钟以后的错位）。线性插值。"""
    L = len(x)
    out = np.zeros((count,) + x.shape[1:])
    k = np.arange(count - delay) if delay < count else np.arange(0)
    pos = (start * SR + k * rate) % L
    i = np.floor(pos).astype(int)
    f = pos - i
    j = (i + 1) % L
    if x.ndim == 2:
        f = f[:, None]
    out[delay:] = x[i] * (1 - f) + x[j] * f
    return out


# ---------- 响度（ITU-R BS.1770-4） ----------

_K1 = (np.array([1.53512485958697, -2.69169618940638, 1.19839281085285]),
       np.array([1.0, -1.69065929318241, 0.73248077421585]))
_K2 = (np.array([1.0, -2.0, 1.0]), np.array([1.0, -1.99004745483398, 0.99007225036621]))


def _kweight(x):
    x2 = stereo(x)
    y = signal.lfilter(*_K1, x2, axis=0)
    return signal.lfilter(*_K2, y, axis=0)


def _blocks(x, win=0.4, hop=0.1):
    y = _kweight(x)
    w, h = n(win), n(hop)
    if len(y) < w:
        y = np.pad(y, ((0, w - len(y)), (0, 0)))
    starts = range(0, len(y) - w + 1, h)
    return np.array([np.sum(np.mean(y[s:s + w] ** 2, axis=0)) for s in starts])


def lufs(x) -> float:
    """整体响度（门限积分）。"""
    z = _blocks(x)
    l = -0.691 + 10 * np.log10(z + 1e-12)
    z = z[l > -70]
    if not len(z):
        return -70.0
    rel = -0.691 + 10 * np.log10(np.mean(z)) - 10
    l = -0.691 + 10 * np.log10(z)
    z = z[l > rel]
    return float(-0.691 + 10 * np.log10(np.mean(z)))


def momentary_max(x) -> float:
    """最大瞬时响度（400 ms 窗），短音效用。"""
    return float(np.max(-0.691 + 10 * np.log10(_blocks(x, 0.4, 0.05) + 1e-12)))


def true_peak_db(x) -> float:
    up = signal.resample_poly(stereo(x), 4, 1, axis=0)
    return float(20 * np.log10(np.max(np.abs(up)) + 1e-12))


def peak_db(x) -> float:
    return float(20 * np.log10(np.max(np.abs(x)) + 1e-12))


def to_peak(x, target_db: float):
    return x * db(target_db) / (np.max(np.abs(x)) + 1e-12)


# ---------- 文件 ----------

def write_wav(path, x, bits: int = 24):
    """写 PCM WAV（默认 24 bit，13 §6.3）。超过 0 dBFS 的部分直接报错，不偷偷削波。"""
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    x = np.asarray(x, dtype=np.float64)
    if not np.all(np.isfinite(x)):
        raise ValueError(f'{path.name}: 有 NaN / Inf')
    peak = np.max(np.abs(x))
    if peak > 1.0:
        raise ValueError(f'{path.name}: 峰值 {20 * np.log10(peak):+.2f} dBFS，超过 0')
    ch = 1 if x.ndim == 1 else x.shape[1]
    full = 2 ** (bits - 1) - 1
    q = np.round(x * full).astype(np.int32).reshape(-1)
    if bits == 24:
        b = q.astype('<i4').view(np.uint8).reshape(-1, 4)[:, :3].tobytes()
    else:
        b = q.astype('<i2').tobytes()
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(ch)
        w.setsampwidth(bits // 8)
        w.setframerate(SR)
        w.writeframes(b)


def read_wav(path) -> np.ndarray:
    with wave.open(str(path), 'rb') as w:
        ch, width, frames = w.getnchannels(), w.getsampwidth(), w.readframes(w.getnframes())
    if width == 3:
        raw = np.frombuffer(frames, np.uint8).reshape(-1, 3)
        q = (raw[:, 0].astype(np.int32) | (raw[:, 1].astype(np.int32) << 8)
             | (raw[:, 2].astype(np.int32) << 16))
        q = np.where(q >= 2 ** 23, q - 2 ** 24, q)
        x = q / (2 ** 23 - 1)
    else:
        x = np.frombuffer(frames, '<i2') / 32767
    return x.reshape(-1, ch) if ch > 1 else x
