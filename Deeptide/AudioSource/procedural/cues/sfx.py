"""P0 音效（策划/13 §5.2）：界面 U、战斗 C、理智 R03–R06、恐惧 F、提灯 L、Boss 潮位 B。R01 / R02 / C01 在 sanity.py。

文件名 = <ID>[_<种类>]_<变体>.wav，例如 U04_attack_2.wav、C06_1.wav。游戏里按「ID[_种类]」播放，随机挑一个变体（13 §6.1）。
电平按峰值定（这些声音大多短于响度表的 400 ms 窗口，用 LUFS 量不准）：界面最轻，命中和理智类最重，都在理智音效 R01（−16 LUFS 瞬时）之下。
"""
from __future__ import annotations

import numpy as np

import dsp
import foley as fo
import instruments as ins


def _cat(*parts, gap=0.0):
    """把几段按顺序接起来（gap 秒的间隔，负数表示叠在一起）。"""
    out = np.zeros(0)
    for p in parts:
        k = max(0, len(out) + dsp.n(gap)) if len(out) else 0
        need = k + len(p)
        if need > len(out):
            out = np.concatenate([out, np.zeros(need - len(out))])
        out[k:k + len(p)] += p
    return out


def _mix(*layers):
    """layers = (数组, 增益, 起点秒)；返回叠好的单声道。"""
    length = max(dsp.n(at) + len(x) for x, _, at in layers)
    out = np.zeros(length)
    for x, g, at in layers:
        dsp.place(out, x, dsp.n(at), g)
    return out


# ---------- 界面 ----------

def u01(s):   # 悬停卡牌：纸边擦过，极轻
    return fo.paper(0.08, s, rate=500, peak_at=0.3)


def u02(s):   # 抽牌：翻纸
    return fo.page_flip(s, 0.13)


def u03(s):   # 洗牌：一叠纸磕齐
    return fo.stack_knock(s)


def u04_attack(s):   # 打出攻击：纸撕开一半 + 一道风
    return _mix((fo.tear(s, 0.16), 1.0, 0.0), (fo.whoosh(0.32, s, 300, 3000, 0.5), 0.55, 0.06))


def u04_skill(s):    # 打出技能：翻页
    return fo.page_flip(s + 10, 0.24)


def u04_power(s):    # 打出能力：火漆印压下
    return fo.stamp(s)


def u05(s):   # 打不出：指节敲木头，闷
    return _mix((fo.knock(s, 175, 0.18, soft=1.6), 1.0, 0.0), (fo.knock(s + 1, 190, 0.15, soft=1.8), 0.7, 0.11))


def u06(s):   # 结束回合：怀表合盖
    return _mix((fo.click(s, 2300, 0.018, 0.14), 1.0, 0.0), (fo.knock(s, 520, 0.09, soft=0.8), 0.6, 0.0))


def u07(s):   # 按钮、面板开合：纸页
    return fo.page_flip(s + 20, 0.16)


def u08(s):   # 地图选点：鹅毛笔在海图上划一道
    return fo.quill(0.34, s)


def u09(s):   # 金币：几枚湿硬币
    return fo.coins(s, 3 + s % 2)


def u10_card(s):     # 获得卡牌：纸塞进本子
    return _mix((fo.paper(0.28, s, rate=260, peak_at=0.6, bright=0.8), 1.0, 0.0),
                (fo.knock(s, 230, 0.1, soft=2.5), 0.4, 0.24))


def u10_relic(s):    # 获得遗物：放到木桌上
    return _mix((fo.knock(s, 150, 0.22, soft=1.3), 1.0, 0.0), (fo.click(s, 1900, 0.02, 0.1), 0.25, 0.005))


def u10_potion(s):   # 获得药水：玻璃瓶轻碰
    return _mix((fo.bottle(s, 1100), 1.0, 0.0), (fo.bottle(s + 1, 1240), 0.5, 0.07))


# ---------- 战斗 ----------

def c02(s):   # 全部被格挡：布和木头的闷响，没有湿声
    return _mix((fo.knock(s, 135, 0.2, soft=2.2), 1.0, 0.0), (fo.cloth(0.14, s, 30, 0.1, 200, 1800), 0.6, 0.0))


def c03(s):   # 获得格挡：风衣下摆扬起
    return fo.cloth(0.32, s, 16 + 3 * s, 0.35)


def c04(s):   # 格挡被击破：木板裂开
    return fo.wood_crack(s)


def c05(s):   # 敌人出招前冲（通用版；每个敌人的声音身份是 P1，§5.3）
    return _mix((fo.whoosh(0.28, s, 150, 1600, 0.7), 1.0, 0.0), (fo.knock(s, 95, 0.2, soft=3), 0.6, 0.18))


def c06(s):   # 敌人死亡：墨往下流 + 一声很低的叹气，0.6 秒
    return _mix((fo.liquid(0.6, s, 32), 1.0, 0.0), (fo.breath(0.55, s, 0.3, pitch=92, vowel=4), 0.9, 0.05))


def c07(s):   # 召唤：地面裂开 + 湿滑的东西升起
    return _mix((fo.ground_crack(s), 1.0, 0.0), (fo.slither(0.9, s), 0.9, 0.12))


def c08_buff(s):     # 增益：布料收紧
    return fo.cloth(0.18, s + 5, 34, 0.65, 400, 3500)


def c08_debuff(s):   # 减益：一滴墨落在纸上
    return _mix((fo.drip(s, 950, 0.1), 1.0, 0.0), (fo.paper(0.05, s, rate=900, peak_at=0.1, bright=0.7), 0.4, 0.004))


def c09(s):   # 用药水：拔软木塞，再喝
    return _mix((fo.cork(s), 1.0, 0.0), (fo.glug(s, 0.45), 0.8, 0.22))


def c10(s):   # 遗物触发：黄铜机件咔哒
    return fo.click(s, 2600 + 300 * s, 0.03)


# ---------- 理智（接在 R01 前面，R01 本身在 sanity.py） ----------

def r03(s):   # 精神攻击：贴着耳朵的一句低语（保留音色）
    x = fo.whisper(0.55, s, 6.0)
    return dsp.highpass(x, 200, 2)


def r04(s):   # 心防抵消：盐粒撒落 + 玻璃罩扣下（闷的一下，不是 R02 的玻璃）
    dome = _mix((dsp.lowpass(fo.knock(s, 430, 0.25, soft=1.0), 1800), 1.0, 0.0), (fo.bottle(s, 760), 0.3, 0.0))
    return _mix((fo.salt(0.45, s), 1.0, 0.0), (dome, 0.8, 0.28))


_WELL = None


def r05(s):   # 侵蚀：一滴水落进很深的井（长混响只在这一声里，§5.1 的「短、干」对它例外）
    global _WELL
    if _WELL is None:
        _WELL = dsp.reverb_ir(3.2, predelay=0.06, damp=0.3, seed=55)
    d = fo.drip(s, 780, 0.15)
    wet = dsp.convolve(d, _WELL)
    wet = dsp.lowpass(wet, 2600)
    dry = dsp.stereo(np.concatenate([d, np.zeros(len(wet) - len(d))]))
    x = dry * 0.7 + wet * 0.55
    return x[:dsp.n(2.4)]


def r06(s):   # 疯语在回合结束扣理智：耳语在这一刻听清半句
    return dsp.highpass(fo.whisper(0.6, s + 30, 4.2, clear=0.85), 200, 2)


# ---------- 恐惧、提灯、Boss ----------

def f01(s):   # 施加恐惧：弦乐颤音，从调查员一侧扫到目标一侧（左 → 右）
    x = fo.tremolo([dsp.hz('D4'), dsp.hz('Eb4'), dsp.hz('A4')], 0.75, s)
    p = np.linspace(-0.7, 0.7, len(x))
    th = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(th), x * np.sin(th)], axis=1)


def f02(s):   # 震慑：鹅毛笔划叉 + 一声被掐断的惊喘
    stroke1 = fo.quill(0.13, s)
    stroke2 = fo.quill(0.13, s + 1)
    gasp = fo.breath(0.2, s, 0.85, vowel=0)
    gasp[-dsp.n(0.006):] *= np.linspace(1, 0, dsp.n(0.006))   # 掐断
    return _mix((stroke1, 1.0, 0.0), (stroke2, 1.0, 0.16), (gasp, 1.6, 0.3))


def f03(s):   # 震慑跳过行动：一口发抖的吸气，然后什么也没发生
    x = fo.breath(0.7, s, 0.75, vowel=2)
    t = dsp.tvec(len(x))
    x *= 0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 7 * t))
    return np.concatenate([x, np.zeros(dsp.n(0.4))])


def l01(s):   # 点亮提灯：划火柴，灯焰起来
    return fo.match(s)


def b01(s):   # 潮位 +1：水下的钟一声（保留音色：潮钟）+ 水位上涨的咕嘟
    bell = ins.bell(dsp.hz('D2'), 3.2, decay_scale=0.35, strike=0.4, seed=900 + s)
    bell = dsp.lowpass(bell, 650, 4)
    bubbles = fo.glug(s, 1.0, 5, 300)
    return _mix((bell, 1.0, 0.0), (bubbles, 0.7, 0.25))


def b02(s):   # 潮位 −1：同一声钟，倒放
    x = b01(s)[::-1].copy()
    k = dsp.n(0.02)
    x[:k] *= np.linspace(0, 1, k)
    return x


# ID → (合成函数, 变体数, 峰值 dBFS)
CUES = {
    'U01': (u01, 3, -30), 'U02': (u02, 3, -24), 'U03': (u03, 1, -18),
    'U04_attack': (u04_attack, 3, -16), 'U04_skill': (u04_skill, 3, -20), 'U04_power': (u04_power, 2, -15),
    'U05': (u05, 2, -16), 'U06': (u06, 1, -16), 'U07': (u07, 3, -24), 'U08': (u08, 2, -20), 'U09': (u09, 2, -18),
    'U10_card': (u10_card, 1, -20), 'U10_relic': (u10_relic, 1, -17), 'U10_potion': (u10_potion, 1, -18),
    'C02': (c02, 3, -12), 'C03': (c03, 3, -18), 'C04': (c04, 2, -11), 'C05': (c05, 3, -16),
    'C06': (c06, 2, -14), 'C07': (c07, 1, -12), 'C08_buff': (c08_buff, 2, -20), 'C08_debuff': (c08_debuff, 2, -18),
    'C09': (c09, 1, -16), 'C10': (c10, 2, -18),
    'R03': (r03, 3, -14), 'R04': (r04, 1, -14), 'R05': (r05, 1, -14), 'R06': (r06, 2, -14),
    'F01': (f01, 1, -16), 'F02': (f02, 1, -14), 'F03': (f03, 1, -18), 'L01': (l01, 1, -14),
    'B01': (b01, 1, -12), 'B02': (b02, 1, -12),
}


def finish(x, peak):
    """切掉 30 Hz 以下（13 §6.3）、去掉尾巴上的静音、收尾 10 ms 淡出，再定峰值。"""
    x = dsp.highpass(x, 30, 2)
    env = np.abs(x) if x.ndim == 1 else np.max(np.abs(x), axis=1)
    loud = np.nonzero(env > np.max(env) * dsp.db(-60))[0]
    end = min(len(x), (loud[-1] if len(loud) else len(x) - 1) + dsp.n(0.02))
    x = x[:end].copy()
    k = min(len(x), dsp.n(0.01))
    fade = np.linspace(1, 0, k)
    x[-k:] *= fade if x.ndim == 1 else fade[:, None]
    return dsp.to_peak(x, peak)


def build(out, log, only=None):
    sfx = out / 'sfx'
    names = []
    for cid, (fn, variants, peak) in CUES.items():
        if only and cid not in only:
            continue
        for v in range(1, variants + 1):
            x = finish(fn(v), peak)
            name = f'{cid}_{v}'
            dsp.write_wav(sfx / f'{name}.wav', x)
            log(name, x)
            names.append(name)
    return names
