"""合成音频：python build.py [sanity] [S02] [M02] …（不给就全做）。

- ../sfx、../stingers、../music：48 kHz / 24 bit WAV，交给 Unity 的素材（策划/13 §6.3、§6.4）
- preview/（不进仓库）：混好的试听预览（16 bit）、listen.html 试听页、report.txt 检查结果、频谱图
"""
from __future__ import annotations

import argparse
import json
import pathlib
import sys
import time

import numpy as np

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import dsp  # noqa: E402

OUT = HERE.parent
PREVIEW = HERE / 'preview'

_rows = []


def log(name, x, loop=False):
    """记一行检查结果：长度、整体响度、最大瞬时响度、真峰值；循环曲再查首尾接缝。"""
    row = {'name': name, 'sec': round(len(x) / dsp.SR, 2), 'lufs': round(dsp.lufs(x), 1),
           'mmax': round(dsp.momentary_max(x), 1), 'tp': round(dsp.true_peak_db(x), 1)}
    if loop:
        s = dsp.stereo(x)
        jump = np.max(np.abs(s[0] - s[-1]))
        typical = np.percentile(np.abs(np.diff(s, axis=0)), 99.9)
        # 接缝处首尾两个采样的落差，和全曲相邻采样落差的 99.9 分位比：< 1 就听不出咔哒声
        row['seam'] = f'接缝落差 {jump / max(typical, 1e-9):.2f}×（< 1 无咔哒）'
    _rows.append(row)
    flag = '  ← 真峰值超过 −1 dBTP' if row['tp'] > -1 else ''
    print(f"  {name:<16} {row['sec']:>6.2f}s  {row['lufs']:>6.1f} LUFS  瞬时最大 {row['mmax']:>6.1f}  "
          f"真峰值 {row['tp']:>5.1f}{'  ' + row['seam'] if loop else ''}{flag}")


def spectro(path, out):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    x = dsp.read_wav(path)
    m = x.mean(axis=1) if x.ndim == 2 else x
    fig, ax = plt.subplots(figsize=(12, 4), dpi=90)
    ax.specgram(m, NFFT=4096, Fs=dsp.SR, noverlap=3072, cmap='magma', vmin=-150)
    ax.set_yscale('symlog', linthresh=200)
    ax.set_ylim(20, 16000)
    ax.set_title(path.name)
    fig.tight_layout()
    fig.savefig(out)
    plt.close(fig)


def write_page(sanity_log):
    """试听页：三件样张 + 闭眼听理智的小测验（§7.4 第 3 件）。"""
    import cues.sanity as san
    files = {
        'r01': [f'../../sfx/R01_{i}.wav' for i in range(0, san.MAX_SANITY + 1)],
        'r02': [f'../../sfx/R02_{i}.wav' for i in range(1, san.MAX_SANITY + 1)],
    }
    levels = [(10, '理智 10 · 清明'), (7, '理智 7 · 清醒'), (4, '理智 4 · 癫狂（叠夜 1 层）'),
              (3, '理智 3（2 层）'), (2, '理智 2（3 层）'), (1, '理智 1（4 层）')]
    def audio(src, label, note=''):
        return (f'<div class="row"><div class="lab">{label}<small>{note}</small></div>'
                f'<audio controls preload="none" src="{src}"></audio></div>')
    hits = ''.join(audio(f'../../sfx/C01_{t}_{v}.wav', f'C01 {name} #{v}')
                   for t, name in (('light', '轻 <10'), ('medium', '中 10–19'), ('heavy', '重 ≥20'))
                   for v in (1, 2, 3))
    stems = ''.join(audio(f'../../music/M02_{k}.wav', f'分轨 {k}', note)
                    for k, note in (('base', '低音 D 持续音 + 拨弦 + 闷鼓'), ('main', '簧风琴 + 旧钢琴，叠夜的来源'),
                                    ('clarity', '清明时才开'), ('deep', '癫狂时按 40–85% 开')))
    lv = ''.join(audio(f'M02_L{s}.wav', label) for s, label in levels)
    timeline = ''.join(f'<li><b>{t:.1f}s</b> {what}</li>' for t, what in sanity_log)
    html = PAGE.format(hits=hits, stems=stems, levels=lv, timeline=timeline,
                       files=json.dumps(files), line=san.LUNACY_LINE)
    (PREVIEW / 'listen.html').write_text(html, encoding='utf-8')


PAGE = """<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>深潮音频样张</title>
<style>
:root{{--paper:#E8E0CC;--shade:#CFC6B0;--ink:#1C1A17;--mid:#4A453C;--teal:#1A615E;--gold:#7D5914;--blood:#8E2A2A}}
@media (prefers-color-scheme:dark){{:root{{--paper:#0F1B1B;--shade:#1d2c2b;--ink:#D8E4DF;--mid:#9fb1ab;--teal:#4FC2B0;--gold:#D9A84A;--blood:#E0736A}}}}
body{{background:var(--paper);color:var(--ink);font:16px/1.6 "Microsoft YaHei","PingFang SC",sans-serif;margin:0;padding:24px 16px 64px}}
main{{max-width:880px;margin:0 auto}}
h1{{font-family:"Source Han Serif SC","Noto Serif SC","STSong",serif;font-size:30px;margin:0 0 4px}}
h2{{font-family:"Source Han Serif SC","Noto Serif SC","STSong",serif;border-top:2px solid var(--ink);padding-top:14px;margin-top:36px}}
p,li{{color:var(--mid)}} b{{color:var(--ink)}}
.row{{display:flex;align-items:center;gap:12px;flex-wrap:wrap;margin:6px 0}}
.lab{{width:230px;flex:none}} .lab small{{display:block;color:var(--mid);font-size:13px}}
audio{{flex:1;min-width:240px;height:36px}}
button{{font:inherit;background:var(--shade);color:var(--ink);border:1px solid var(--ink);border-radius:4px;padding:6px 12px;cursor:pointer}}
button.big{{background:var(--teal);color:var(--paper);border-color:var(--teal);padding:8px 18px}}
.keys{{display:flex;gap:6px;flex-wrap:wrap;margin:10px 0}} .keys button{{width:44px}}
.keys button.mad{{border-color:var(--teal);color:var(--teal)}}
#verdict{{min-height:1.6em;font-weight:bold}} .ok{{color:var(--gold)}} .no{{color:var(--blood)}}
ol{{padding-left:20px}}
</style></head><body><main>
<h1>深潮 · 音频样张三件套</h1>
<p>策划/13 §7.4。戴耳机听一遍，再用笔记本喇叭听一遍。验收要你本人判断的有两条：战斗曲连续听 20 分钟烦不烦；闭着眼能不能听出还剩几格理智。</p>

<h2>1　S02 回潮</h2>
<p>一段理智 3 的战斗，致命一击，然后刚才那 2 秒倒放 1.5 秒（越倒越快）、静 1 秒、潮钟七下（每一下余音更长）、栈桥的浪。游戏里倒放是运行时做的，素材只有后半段。</p>
<div class="row"><div class="lab">预览：死亡 → 回潮</div><audio controls preload="none" src="S02_demo.wav"></audio></div>
<div class="row"><div class="lab">素材 S02_bells<small>静 → 七下 → 浪</small></div><audio controls preload="none" src="../../stingers/S02_bells.wav"></audio></div>

<h2>2　M02 潮下 + 叠夜</h2>
<p>先听扫描：理智 10 → 7 → 4（进入癫狂，空一拍 + 粗版的「坠入」）→ 3 → 2 → 1，最后祷告拉回 5。叠夜的拷贝从开战起一直在播，理智只调音量；拷贝变速变调，所以越播越错开。</p>
<div class="row"><div class="lab">理智扫描 10 → 1 → 5<small>72 秒，含理智音效</small></div><audio controls preload="none" src="M02_sweep.wav"></audio></div>
<ol>{timeline}</ol>
<p>各档单独听（每段一个循环，31.6 秒），注意总响度基本不变：癫狂是更浑，不是更响。</p>
{levels}
<div class="row"><div class="lab">理智 1 · 打到第 5 分钟<small>拷贝已经错开 2–7 拍</small></div><audio controls preload="none" src="M02_L1_5min.wav"></audio></div>
<p>分轨（循环素材，直接拖进播放器循环听可以查接缝）：</p>
{stems}

<h2>3　理智音效组</h2>
<p>R01 失去理智：音高 = 失去后剩下的理智（0 = D3 … 10 = G4），≤ 4 时下面多一个水下的低八度。R02 恢复理智：同一个音阶，高一个八度的玻璃声。</p>
<div class="row"><button onclick="scale('r01')">R01 从 10 一路扣到 0</button><button onclick="scale('r02')">R02 从 1 一路回到 10</button></div>
<h3>闭眼测验</h3>
<p>点「出题」：从某个理智（5–10）开始连扣 1–4 格，你听完猜最后剩几格。记三个数：猜中、差 1 以内、癫狂与否判断对。</p>
<div class="row"><button class="big" onclick="trial()">出题</button><button onclick="replay()">再听一遍</button><span id="score"></span></div>
<div class="keys" id="keys"></div>
<div id="verdict"></div>
<h3>C01 命中三档</h3>
{hits}
</main>
<script>
const F={files}, LINE={line};
const cache={{}};
function snd(src){{ if(!cache[src]) cache[src]=new Audio(src); const a=cache[src]; a.currentTime=0; return a; }}
function seq(list,gap){{ list.forEach((src,i)=>setTimeout(()=>snd(src).play(),i*gap)); }}
function scale(kind){{ const out = kind==='r01' ? [9,8,7,6,5,4,3,2,1,0].map(i=>F.r01[i])
  : [2,3,4,5,6,7,8,9,10].map(i=>F.r02[i-1]); seq(out,420); }}
let cur=null, last=[], st={{n:0,exact:0,near:0,zone:0}};
const keys=document.getElementById('keys');
for(let i=0;i<=9;i++){{ const b=document.createElement('button'); b.textContent=i; if(i<=LINE) b.className='mad'; b.onclick=()=>guess(i); keys.appendChild(b); }}
function trial(){{ const start=5+Math.floor(Math.random()*6); const k=1+Math.floor(Math.random()*4);
  cur=start-k; last=[]; for(let s=start-1;s>=cur;s--) last.push(F.r01[s]);
  document.getElementById('verdict').textContent='听……'; seq(last,220); }}
function replay(){{ if(last.length) seq(last,220); }}
function guess(g){{ if(cur===null) return; st.n++; const d=Math.abs(g-cur);
  if(d===0) st.exact++; if(d<=1) st.near++; if((g<=LINE)===(cur<=LINE)) st.zone++;
  const v=document.getElementById('verdict');
  v.innerHTML = d===0 ? '<span class="ok">对，剩 '+cur+'</span>' : '<span class="no">剩 '+cur+'，你猜 '+g+'</span>';
  document.getElementById('score').textContent='第 '+st.n+' 题：猜中 '+st.exact+'，差 1 以内 '+st.near+'，癫狂判断对 '+st.zone;
  cur=null; }}
</script></body></html>
"""


def write_sfx_page(names):
    """P0 音效的试听页：按 13 §5.2 的分组列出每个变体。"""
    import cues.sfx as sfx
    groups = (('U', '界面'), ('C', '战斗'), ('R', '理智（游戏里后面会接 R01）'), ('F', '恐惧'), ('L', '提灯'), ('B', 'Boss 潮位'))
    body = ''
    for prefix, title in groups:
        rows = ''.join(f'<div class="row"><div class="lab">{nm}<small>{(sfx.CUES[nm.rsplit("_", 1)[0]][0].__doc__ or "")}</small></div>'
                       f'<audio controls preload="none" src="../../sfx/{nm}.wav"></audio></div>'
                       for nm in names if nm.startswith(prefix))
        if rows:
            body += f'<h2>{title}</h2>{rows}'
    notes = {fn.__name__: line.split('#', 1)[1].strip() for fn, _, _ in sfx.CUES.values()
             for line in [__import__('inspect').getsource(fn).splitlines()[0]] if '#' in line}
    for nm in names:
        key = sfx.CUES[nm.rsplit('_', 1)[0]][0].__name__
        body = body.replace(f'{nm}<small></small>', f'{nm}<small>{notes.get(key, "")}</small>', 1)
    html = PAGE.split('<main>')[0] + '<main><h1>深潮 · P0 音效</h1><p>策划/13 §5.2。R01 / R02 / C01 在 listen.html。</p>' + body + '</main></body></html>'
    (PREVIEW / 'sfx.html').write_text(html.replace('{{', '{').replace('}}', '}'), encoding='utf-8')


def calibrate():
    """实测各档理智的整体响度，给出 mixer.LEVELS 最后一列「音乐总线补偿」的建议值（以理智 7 为准，取到 0.5 dB）。"""
    import mixer
    stems = {k: dsp.read_wav(OUT / 'music' / f'M02_{k}.wav') for k in ('base', 'main', 'clarity', 'deep')}
    L = len(stems['main'])
    ref = dsp.lufs(mixer.render(stems, np.full(L, 7, float)))
    print(f'理智 7：{ref:.1f} LUFS')
    for s in (10, 4, 3, 2, 1):
        m = dsp.lufs(mixer.render(stems, np.full(L, s, float)))
        now = mixer.LEVELS[s][6]
        print(f'理智 {s:>2}：{m:6.1f} LUFS，现在补偿 {now:+.1f}，建议 {round((now + ref - m) * 2) / 2:+.1f} dB')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('cues', nargs='*', default=['M02', 'sanity', 'S02', 'sfx'])
    ap.add_argument('--spectro', action='store_true', help='给主要文件画频谱图（preview/_spec_*.png）')
    ap.add_argument('--calibrate', action='store_true', help='只实测各档响度，给出总线补偿建议值')
    args = ap.parse_args()
    PREVIEW.mkdir(exist_ok=True)
    if args.calibrate:
        calibrate()
        return
    import cues.M02 as m02
    import cues.S02 as s02
    import cues.sanity as san
    t0 = time.time()
    stems, sanity_log = None, []
    want = set(args.cues)
    if 'M02' in want:
        print('M02 潮下')
        stems, sanity_log = m02.build(OUT, lambda *a, **k: log(*a, **k), PREVIEW)
        (PREVIEW / 'sweep_log.json').write_text(json.dumps(sanity_log, ensure_ascii=False), encoding='utf-8')
    if 'sanity' in want:
        print('理智音效组')
        san.build(OUT, log)
    if 'S02' in want:
        print('S02 回潮')
        if stems is None:
            stems = {k: dsp.read_wav(OUT / 'music' / f'M02_{k}.wav') for k in ('base', 'main', 'clarity', 'deep')}
        s02.build(OUT, log, PREVIEW, stems)
    if 'sfx' in want:
        print('P0 音效')
        import cues.sfx as sfx
        write_sfx_page(sfx.build(OUT, log))
    if not sanity_log and (PREVIEW / 'sweep_log.json').exists():
        sanity_log = json.loads((PREVIEW / 'sweep_log.json').read_text(encoding='utf-8'))
    write_page(sanity_log)
    if args.spectro:
        for p in (OUT / 'stingers' / 'S02_bells.wav', PREVIEW / 'M02_L10.wav', PREVIEW / 'M02_L1.wav',
                  OUT / 'sfx' / 'R01_4.wav', OUT / 'sfx' / 'C01_heavy_1.wav', PREVIEW / 'S02_demo.wav'):
            if p.exists():
                spectro(p, PREVIEW / f'_spec_{p.stem}.png')
    lines = [f"{r['name']}\t{r['sec']}s\t{r['lufs']} LUFS\t瞬时最大 {r['mmax']}\t真峰值 {r['tp']}\t{r.get('seam', '')}"
             for r in _rows]
    (PREVIEW / 'report.txt').write_text('\n'.join(lines), encoding='utf-8')
    print(f'完成，{time.time() - t0:.0f} 秒。试听：{PREVIEW / "listen.html"}')


if __name__ == '__main__':
    main()
