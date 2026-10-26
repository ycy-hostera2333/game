"""M02「潮下」普通战斗曲的样张（策划/13 §4.1、§7.4 第 2 件）：10 小节、76 BPM、4/4，可循环（31.6 秒）。

分轨（§3.1）：base 底（低音 D 持续音、低音提琴拨弦、远处闷鼓）、main 主（簧风琴和声 + 旧钢琴旋律，
叠夜拷贝的来源）、clarity 清明（小提琴泛音）、deep 深渊（深潮的呼吸 + 弓擦金属）。叠夜不另出文件，
游戏里运行时生成，预览里由 mixer.py 模拟。

和声每两小节一换：D5 → E♭ → Gm/D → Cm → D5。低音 D 的持续音一直不走，压在 E♭ 和弦底下就是一个小二度。
旋律稀疏、长音多：叠上错位的拷贝以后还得能听（§3.2 最后一条）。
"""
from __future__ import annotations

import numpy as np

import dsp
import instruments as ins
import mixer
from dsp import SR, n

BEAT = mixer.BEAT
BARS = 10
LOOP = n(BARS * 4 * BEAT)
TAIL = 8.0

# 各分轨的目标响度（LUFS），之后整体缩放到「全开 = −23 LUFS」（§6.3）
TARGET = {'base': -28.5, 'main': -26.0, 'clarity': -35.0, 'deep': -29.5}
FULL = -23.0


def at(bar: float, beat: float = 1.0) -> float:
    """第 bar 小节第 beat 拍（都从 1 数）对应的秒数。"""
    return ((bar - 1) * 4 + (beat - 1)) * BEAT


def _buf():
    return np.zeros((LOOP + n(TAIL), 2))


def _xfade_loop(x: np.ndarray, xf: float = 2.0) -> np.ndarray:
    """连续的织体（持续音、呼吸）：把循环末尾之后的一段等功率交叉淡化进开头。"""
    k = n(xf)
    out = x[:LOOP].copy()
    th = np.linspace(0, np.pi / 2, k)[:, None]
    out[:k] = x[:k] * np.sin(th) + x[LOOP:LOOP + k] * np.cos(th)
    return out


CHORDS = [  # (起始小节, 声部)
    (1, ['D3', 'A3', 'D4']),
    (3, ['Eb3', 'Bb3', 'G4']),
    (5, ['D3', 'G3', 'Bb3']),
    (7, ['C3', 'G3', 'Eb4']),
    (9, ['D3', 'A3', 'D4']),
]
ROOTS = {1: ('D2', 'Eb2'), 3: ('Eb2', 'D2'), 5: ('D2', 'Eb2'), 7: ('C2', 'Bb1'), 9: ('D2', 'Eb2')}

MELODY = [  # (小节, 拍, 音, 时值 拍, 力度)
    (1, 1, 'A4', 2, 0.55), (1, 3.5, 'D5', 1.5, 0.6),
    (2, 1, 'D5', 1, 0.5), (2, 2, 'Eb5', 1.5, 0.62), (2, 3.5, 'D5', 2.5, 0.5),
    (3, 1, 'Bb4', 2, 0.55), (3, 3, 'G4', 1, 0.5),
    (4, 2, 'F4', 1, 0.45), (4, 3, 'G4', 2, 0.55),
    (5, 1, 'Bb4', 1.5, 0.55), (5, 2.5, 'A4', 1.5, 0.5), (5, 4, 'G4', 1, 0.45),
    (6, 1, 'D4', 3, 0.5),
    (7, 1, 'Eb4', 1, 0.5), (7, 2, 'G4', 1, 0.5), (7, 3, 'C5', 2, 0.6),
    (8, 1.5, 'Bb4', 1.5, 0.5), (8, 3, 'A4', 2, 0.5),
    (9, 1, 'D5', 2, 0.6), (9, 3, 'A4', 2, 0.5),
    (10, 1, 'Eb5', 1, 0.6), (10, 2, 'D5', 3, 0.5),
]

CLARITY = [(1, 'A5'), (3, 'G5'), (5, 'Bb5'), (7, 'G5'), (9, 'A5')]


def base():
    x = _buf()
    drone_len = LOOP / SR + 2.5
    for note, g, p in (('D2', 0.22, -0.1), ('A2', 0.12, 0.1)):
        d = ins.harmonium(dsp.hz(note), drone_len, seed=10 + len(note), bright=0.6, attack=0, release=0)
        dsp.place(x, dsp.pan(d, p), 0, g)
    drone = _xfade_loop(x)
    x = _buf()
    for bar in range(1, BARS + 1):
        start = bar if bar % 2 else bar - 1
        root, neigh = ROOTS[start]
        for eighth, note, vel in ((0, root, 0.9), (3, root, 0.6), (6, neigh, 0.55)):
            s = ins.pizz(dsp.hz(note), 2.0, vel=vel, seed=bar * 10 + eighth)
            dsp.place(x, dsp.pan(s, -0.2), n(at(bar) + eighth * BEAT / 2), 0.8)
        dr = dsp.lowpass(ins.drum(seed=bar, f0=56), 1800)
        dsp.place(x, dr, n(at(bar)), 0.32)
        if bar % 2 == 0:
            dsp.place(x, dsp.lowpass(ins.drum(seed=50 + bar, f0=48), 1500), n(at(bar, 3)), 0.2)
    return drone + dsp.fold_tail(x, LOOP)


def main():
    x = _buf()
    for i, (bar, voices) in enumerate(CHORDS):
        length = 2 * 4 * BEAT + 0.5
        for j, v in enumerate(voices):
            h = ins.harmonium(dsp.hz(v), length, seed=20 + i * 3 + j, attack=0.25, release=0.6)
            dsp.place(x, dsp.pan(h, (-0.3, 0.0, 0.3)[j]), n(at(bar) - 0.05), 0.16)
    for k, (bar, beat, note, dur, vel) in enumerate(MELODY):
        p = ins.piano(dsp.hz(note), dur * BEAT + 2.5, vel=vel, hold=dur * BEAT, seed=60 + k)
        dsp.place(x, dsp.pan(p, -0.1), n(at(bar, beat)), 0.5)
    return dsp.fold_tail(x, LOOP)


def clarity():
    x = _buf()
    for k, (bar, note) in enumerate(CLARITY):
        length = 2 * 4 * BEAT + 1.0
        tone = ins.harmonic_tone(dsp.hz(note), length, seed=80 + k, attack=1.2, release=1.5)
        dsp.place(x, dsp.pan(tone, 0.3), n(at(bar) - 0.3), 1.0)
    return dsp.fold_tail(x, LOOP)


def deep():
    length = LOOP / SR + 2.5
    br = ins.deep_breath(length, period=2 * 4 * BEAT, seed=90)
    # 左右各一把，音高差 2 Hz，慢慢互相拍；循环靠交叉淡化接，所以不要起音
    metal = np.stack([ins.bowed_metal(620, length, seed=91, fade=False),
                      ins.bowed_metal(622, length, seed=92, fade=False)], axis=1)
    x = dsp.stereo(br) * 0.6 + metal * 0.05
    return _xfade_loop(x)


def stems():
    raw = {'base': base(), 'main': main(), 'clarity': clarity(), 'deep': deep()}
    for k in raw:
        raw[k] *= dsp.db(TARGET[k] - dsp.lufs(raw[k]))
    full = sum(raw.values())
    g = dsp.db(FULL - dsp.lufs(full))
    return {k: v * g for k, v in raw.items()}


def lunacy_drop():
    """进入癫狂的粗版乐句（S03 的占位，不在这次样张里）：一次深潮的呼吸 + 一道弓擦金属。"""
    br = ins.deep_breath(2.4, period=2.4, seed=95)
    br *= np.sin(np.pi * np.linspace(0, 1, len(br))) ** 0.5
    metal = ins.bowed_metal(587, 2.4, seed=96) * 0.08
    x = dsp.highpass(br * 0.9 + metal, 30)
    return x * dsp.db(-19 - dsp.momentary_max(x))


def lunacy_leave():
    """离开癫狂：一声倒放的长音（§3.4），0.6 秒。"""
    g = ins.glass(dsp.hz('A5'), 1.2, seed=97) + ins.harmonium(dsp.hz('D4'), 1.2, seed=98) * 0.15
    g = g[::-1][-n(0.6):]
    g = g * dsp.ramp(len(g), 0.05, 0.01)
    return g * dsp.db(-22 - dsp.momentary_max(g))


def build(out, log, preview):
    st = stems()
    for k, v in st.items():
        dsp.write_wav(out / 'music' / f'M02_{k}.wav', v)
        log(f'M02_{k}', v, loop=True)

    # 各档理智静态对照：每档一个循环
    for s in (10, 7, 4, 3, 2, 1):
        mix = mixer.render(st, np.full(LOOP, s, float))
        dsp.write_wav(preview / f'M02_L{s}.wav', mix, bits=16)
        log(f'M02_L{s}', mix)
    late = mixer.render(st, np.full(LOOP, 1, float), start=300.0)
    dsp.write_wav(preview / 'M02_L1_5min.wav', late, bits=16)
    log('M02_L1_5min', late)

    sweep, sanity_log = sweep_mix(st)
    dsp.write_wav(preview / 'M02_sweep.wav', sweep, bits=16)
    log('M02_sweep', sweep)
    return st, sanity_log


def sweep_mix(st):
    """理智从 10 一路掉到 1，再用祷告拉回 5：听叠夜层一层层叠上来、又退下去（§7.4 第 2 件）。"""
    import cues.sanity as san
    bar = 4 * BEAT
    total = 23 * bar
    count = n(total)
    steps, events, gaps = [(0.0, 10)], [], []
    s = 10
    log = []

    def lose(t0, k):
        nonlocal s
        for i in range(k):
            s -= 1
            t = t0 + i * 0.22
            steps.append((t, s))
            x = san.r01(s)
            events.append((t, x))
            log.append((round(t, 2), f'失去理智 → {s}'))
            if s == san.LUNACY_LINE:
                nxt = (np.floor(t / BEAT) + 1) * BEAT
                gaps.append((nxt, nxt + BEAT + 2.0))
                events.append((nxt + BEAT, lunacy_drop()))
                log.append((round(nxt, 2), '进入癫狂：空一拍 + 坠入（粗版）'))

    def gain(t0, k):
        nonlocal s
        for i in range(k):
            was = s
            s += 1
            t = t0 + i * 0.2
            steps.append((t, s))
            x = san.r02(s)
            events.append((t, x))
            log.append((round(t, 2), f'恢复理智 → {s}'))
            if was == san.LUNACY_LINE:
                events.append((t + 0.05, lunacy_leave()))
                log.append((round(t, 2), '离开癫狂：倒放长音'))

    lose(4 * bar, 3)
    lose(8 * bar, 3)
    lose(12 * bar + BEAT, 1)
    lose(15 * bar, 1)
    lose(18 * bar, 1)
    gain(20 * bar + 2 * BEAT, 4)
    sanity = mixer.timeline(count, steps)
    mix = mixer.render(st, sanity, events=events, gaps=gaps)
    mix *= dsp.ramp(count, 0.0, 2.5)[:, None]
    return mix, log
