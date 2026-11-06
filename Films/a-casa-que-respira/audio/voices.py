"""Gera as falas (PT-BR) com o modelo neural Kokoro-82M (ONNX, local, Apache-2.0).

Saídas:
  build/voices/<id>.wav   — voz seca, 24 kHz mono (efeitos aplicados no mix)
  build/voices.json       — duração de cada fala
  build/lipsync.json      — abertura de boca por quadro (24 fps) para falas de Gabriel/duplo
"""
import json
import os
import sys

import numpy as np
import soundfile as sf

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS = os.path.join(ROOT, 'models')
BUILD = os.path.join(ROOT, 'build')


def main():
    from kokoro_onnx import Kokoro
    tl = json.load(open(os.path.join(BUILD, 'timeline.json')))
    k = Kokoro(os.path.join(MODELS, 'kokoro-fp32.onnx'), os.path.join(MODELS, 'voices-ptbr.bin'))
    os.makedirs(os.path.join(BUILD, 'voices'), exist_ok=True)
    info, lips = {}, {}
    only = set(sys.argv[1:])
    for line in tl['lines']:
        if only and line['id'] not in only:
            continue
        style = sum(w * k.get_voice_style(n) for n, w in line['voice'].items())
        audio, sr = k.create(line['text'], voice=style.astype(np.float32), speed=line['speed'], lang='pt-br')
        audio = trim(audio, sr)
        sf.write(os.path.join(BUILD, 'voices', line['id'] + '.wav'), audio, sr)
        dur = len(audio) / sr
        info[line['id']] = {'dur': dur, 'sr': sr, 't': line['t']}
        lips[line['id']] = lipsync(audio, sr, tl['fps'])
        print(f"{line['id']:3s} {dur:5.2f}s  {line['text']}")
    path = os.path.join(BUILD, 'voices.json')
    if only and os.path.exists(path):
        old = json.load(open(path)); old.update(info); info = old
        oldl = json.load(open(os.path.join(BUILD, 'lipsync.json'))); oldl.update(lips); lips = oldl
    json.dump(info, open(path, 'w'), indent=1)
    json.dump(lips, open(os.path.join(BUILD, 'lipsync.json'), 'w'))
    check_overlaps(tl, info)


def trim(a, sr, thr=0.004):
    idx = np.where(np.abs(a) > thr)[0]
    if len(idx) == 0:
        return a
    s = max(0, idx[0] - int(0.03 * sr)); e = min(len(a), idx[-1] + int(0.08 * sr))
    return a[s:e]


def lipsync(a, sr, fps):
    """Abertura da boca (0..1) e arredondamento (0..1) por quadro, a partir de energia e centroide."""
    hop = sr // fps
    n = int(np.ceil(len(a) / hop)) + 2
    out = []
    for i in range(n):
        seg = a[max(0, i * hop - hop // 2): i * hop + hop]
        if len(seg) < 32:
            out.append([0, 0]); continue
        rms = float(np.sqrt(np.mean(seg ** 2)))
        sp = np.abs(np.fft.rfft(seg * np.hanning(len(seg))))
        f = np.fft.rfftfreq(len(seg), 1 / sr)
        cen = float((sp * f).sum() / (sp.sum() + 1e-9))
        openv = min(1.0, max(0.0, (20 * np.log10(rms + 1e-6) + 42) / 26))
        rnd = min(1.0, max(0.0, (1800 - cen) / 1400))  # sons graves -> boca arredondada (o/u)
        out.append([round(openv, 3), round(rnd, 3)])
    # suavização leve (inércia da mandíbula)
    arr = np.array(out)
    for i in range(1, len(arr)):
        arr[i, 0] = 0.6 * arr[i, 0] + 0.4 * arr[i - 1, 0]
    return arr.round(3).tolist()


def check_overlaps(tl, info):
    ev = sorted((v['t'], v['t'] + v['dur'], k) for k, v in info.items())
    for (s1, e1, k1), (s2, e2, k2) in zip(ev, ev[1:]):
        if e1 + 0.15 > s2:
            print(f'AVISO: {k1} termina em {e1:.2f}s e {k2} começa em {s2:.2f}s')
    shots = {s['id']: s for s in tl['shots']}
    for line in tl['lines']:
        if line['id'] in info:
            end = info[line['id']]['t'] + info[line['id']]['dur']
            if end > shots[line['shot']]['end']:
                print(f"AVISO: {line['id']} ultrapassa o fim do plano {line['shot']}")


if __name__ == '__main__':
    main()
