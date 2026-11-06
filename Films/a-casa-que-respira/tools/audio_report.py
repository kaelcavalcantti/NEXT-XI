"""Relatório visual do áudio: níveis por camada (RMS em janelas) e espectrograma do master."""
import json, os, sys
import numpy as np, soundfile as sf
import matplotlib; matplotlib.use('Agg')
import matplotlib.pyplot as plt
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
B = os.path.join(ROOT, 'build')
tl = json.load(open(os.path.join(B, 'timeline.json')))
t0 = float(sys.argv[1]) if len(sys.argv) > 1 else 0; t1 = float(sys.argv[2]) if len(sys.argv) > 2 else tl['total']
fig, ax = plt.subplots(2, 1, figsize=(22, 10), sharex=True)
win = 0.25
for name in ['ambiente', 'efeitos', 'sobrenatural', 'dialogos', 'trilha', 'cartelas']:
    x, sr = sf.read(os.path.join(B, 'audio', 'stems', name + '.wav'), dtype='float32')
    x = x[int(t0 * sr):int(t1 * sr)].mean(axis=1)
    h = int(win * sr); n = len(x) // h
    r = np.sqrt(np.mean(x[:n * h].reshape(n, h) ** 2, axis=1))
    ax[0].plot(t0 + np.arange(n) * win, 20 * np.log10(r + 1e-6), label=name, lw=1)
m, sr = sf.read(os.path.join(B, 'audio', 'mix.wav'), dtype='float32')
m = m[int(t0 * sr):int(t1 * sr)].mean(axis=1)
ax[1].specgram(m, NFFT=4096, Fs=sr, noverlap=2048, cmap='magma', vmin=-130, xextent=(t0, t1))
ax[1].set_ylim(0, 12000)
for s in tl['shots']:
    if t0 <= s['start'] <= t1:
        for a in ax: a.axvline(s['start'], color='c', lw=0.6)
        ax[0].text(s['start'], -5, s['id'], rotation=90, fontsize=7, va='top')
for l in tl['lines']:
    if t0 <= l['t'] <= t1: ax[0].axvspan(l['t'], l['t'] + 1.5, color='y', alpha=0.15)
ax[0].set_ylim(-80, 0); ax[0].legend(loc='lower left'); ax[0].grid(alpha=0.3)
plt.tight_layout(); plt.savefig(sys.argv[3] if len(sys.argv) > 3 else '/tmp/claude-0/audio.png', dpi=60)
