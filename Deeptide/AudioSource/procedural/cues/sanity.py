"""理智音效组（策划/13 §5.2）：R01 失去理智、R02 恢复理智、C01 命中三档。

R01_<剩下的理智>.wav、R02_<恢复后的理智>.wav：音高 = 那个理智值在 D 弗里几亚上的位置，
R01 从 D3 往上排（0 = D3、4 = A3、9 = F4、10 = G4），R02 高一个八度（玻璃在低音区不像玻璃）。
"""
from __future__ import annotations

import numpy as np

import dsp
import instruments as ins

LUNACY_LINE = 4
MAX_SANITY = 12          # 事件能把上限往上加，多备两格
R_LOUDNESS = -16.0       # 最大瞬时响度，比音乐（−23 LUFS）高 6 dB 以上（§6.3）
C_PEAK = {'light': -11.0, 'medium': -8.0, 'heavy': -6.0}

# D 弗里几亚，从 D3 起往上
SCALE = ['D3', 'Eb3', 'F3', 'G3', 'A3', 'Bb3', 'C4', 'D4', 'Eb4', 'F4', 'G4', 'A4', 'Bb4']


SNUFF_LEAD = 0.03        # 先「嘶」一声，30 ms 后琴音落下：两个起音错开，峰值不叠在一起
SNUFF_PEAK = -10.0


def r01(left: int):
    """掐灭灯芯 + 一个旧钢琴音；癫狂段（≤ 4）下面加一个水下的低八度。已经按响度定好电平。"""
    f = dsp.hz(SCALE[left])
    note = ins.piano(f, 1.8, vel=0.62, hold=0.75, seed=100 + left)
    if left <= LUNACY_LINE:
        low = ins.piano(f / 2, 1.8, vel=0.5, hold=0.75, seed=200 + left)
        note = note + dsp.lowpass(low, 380, 4) * 0.6     # 只当衬底：报数的是上面那个音（笔记本喇叭放不出 120 Hz 以下）
    note *= dsp.db(R_LOUDNESS - dsp.momentary_max(note))
    x = np.zeros(len(note) + dsp.n(SNUFF_LEAD))
    dsp.place(x, dsp.to_peak(ins.snuff(seed=300 + left), SNUFF_PEAK), 0)
    dsp.place(x, note, dsp.n(SNUFF_LEAD))
    return x


def r02(after: int):
    x = ins.glass(dsp.hz(SCALE[after]) * 2, 2.2, seed=400 + after)
    return x * dsp.db(R_LOUDNESS - dsp.momentary_max(x))


def c01(tier: str, variant: int):
    return ins.hit(tier, seed={'light': 500, 'medium': 600, 'heavy': 700}[tier] + variant)


def build(out, log):
    sfx = out / 'sfx'
    for left in range(0, MAX_SANITY + 1):
        x = r01(left)
        dsp.write_wav(sfx / f'R01_{left}.wav', x)
        log(f'R01_{left}', x)
    for after in range(1, MAX_SANITY + 1):
        x = r02(after)
        dsp.write_wav(sfx / f'R02_{after}.wav', x)
        log(f'R02_{after}', x)
    for tier, peak in C_PEAK.items():
        for v in (1, 2, 3):
            x = dsp.to_peak(c01(tier, v), peak)
            dsp.write_wav(sfx / f'C01_{tier}_{v}.wav', x)
            log(f'C01_{tier}_{v}', x)
