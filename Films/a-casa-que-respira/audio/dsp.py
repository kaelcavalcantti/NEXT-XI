"""Biblioteca de DSP para o design de som: ruídos, filtros, envelopes, reverberação por convolução,
panorama, sussurro por LPC, distorção, compressão. Tudo determinístico (sementes fixas)."""
import os
import subprocess
import tempfile

import numpy as np
from scipy import signal
import soundfile as sf

SR = 48000


def rng(seed):
    return np.random.default_rng(seed)


def t_axis(n):
    return np.arange(n) / SR


def secs(x):
    return int(round(x * SR))


# ---------------- ruídos ----------------
def white(n, seed=0):
    return rng(seed).standard_normal(n).astype(np.float32)


def pink(n, seed=0):
    w = white(n, seed)
    b, a = [0.049922035, -0.095993537, 0.050612699, -0.004408786], [1, -2.494956002, 2.017265875, -0.522189400]
    return (signal.lfilter(b, a, w) * 3.5).astype(np.float32)


def brown(n, seed=0):
    w = white(n, seed)
    y = signal.lfilter([1], [1, -0.995], w)
    return (y / (np.std(y) + 1e-9)).astype(np.float32)


# ---------------- filtros ----------------
def _sos(kind, f, order=2):
    nyq = SR / 2
    if kind in ('lowpass', 'highpass'):
        return signal.butter(order, min(f / nyq, 0.999), btype=kind, output='sos')
    return signal.butter(order, [max(f[0] / nyq, 1e-4), min(f[1] / nyq, 0.999)], btype=kind, output='sos')


def lp(x, f, order=2):
    return signal.sosfilt(_sos('lowpass', f, order), x, axis=-1).astype(np.float32)


def hp(x, f, order=2):
    return signal.sosfilt(_sos('highpass', f, order), x, axis=-1).astype(np.float32)


def bp(x, f0, f1, order=2):
    return signal.sosfilt(_sos('bandpass', (f0, f1), order), x, axis=-1).astype(np.float32)


def peak(x, f, q=4.0, gain_db=6.0):
    b, a = signal.iirpeak(f / (SR / 2), q)
    y = signal.lfilter(b, a, x, axis=-1)
    g = 10 ** (gain_db / 20) - 1
    return (x + g * y).astype(np.float32)


def resonator(x, f, q=30.0):
    """Ressonador (corpo de madeira, metal)."""
    b, a = signal.iirpeak(min(f, SR / 2 - 100) / (SR / 2), q)
    return signal.lfilter(b, a, x).astype(np.float32)


def tv_lowpass(x, cut):
    """Passa-baixa de 1 polo variável no tempo (cut: array de Hz por amostra)."""
    a = np.exp(-2 * np.pi * np.asarray(cut) / SR)
    y = np.zeros_like(x)
    z = 0.0
    # vetoriza em blocos para velocidade
    for i in range(0, len(x), 256):
        blk = x[i:i + 256]; aa = a[i:i + 256] if np.ndim(a) else a
        aa = float(np.mean(aa))
        yb, zf = signal.lfilter([1 - aa], [1, -aa], blk, zi=[z])
        y[i:i + 256] = yb; z = zf[0]
    return y


# ---------------- envelopes ----------------
def env_adsr(n, a=0.01, d=0.1, s=0.7, r=0.2):
    na, nd, nr = secs(a), secs(d), secs(r)
    ns = max(0, n - na - nd - nr)
    e = np.concatenate([np.linspace(0, 1, max(1, na)), np.linspace(1, s, max(1, nd)), np.full(ns, s), np.linspace(s, 0, max(1, nr))])
    return np.pad(e, (0, max(0, n - len(e))))[:n].astype(np.float32)


def env_exp(n, tau):
    return np.exp(-t_axis(n) / tau).astype(np.float32)


def fade(x, fin=0.01, fout=0.01):
    x = x.copy(); n = x.shape[-1]
    a, b = min(n, secs(fin)), min(n, secs(fout))
    if a > 0: x[..., :a] *= np.linspace(0, 1, a)
    if b > 0: x[..., n - b:] *= np.linspace(1, 0, b)
    return x


def curve(n, points):
    """Automação: points = [(tempo_s, valor), ...] interpolação linear."""
    ts = np.array([p[0] for p in points]); vs = np.array([p[1] for p in points])
    return np.interp(t_axis(n), ts, vs).astype(np.float32)


# ---------------- estéreo ----------------
def pan(x, p):
    """p em [-1, 1] (pode ser array). Lei de potência constante."""
    p = np.asarray(p)
    ang = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(ang), x * np.sin(ang)]).astype(np.float32)


def stereo(x):
    return np.stack([x, x]) if x.ndim == 1 else x


def widen(st, amount=0.3, delay_ms=11):
    d = secs(delay_ms / 1000)
    m = (st[0] + st[1]) / 2; s = (st[0] - st[1]) / 2
    s2 = s + amount * np.concatenate([np.zeros(d), m[:-d]]) if d > 0 else s
    return np.stack([m + s2, m - s2]).astype(np.float32)


# ---------------- reverberação ----------------
_ir_cache = {}


def make_ir(t60=1.8, damp=6000, pre=0.012, er=8, seed=1, size=1.0, bright=1.0):
    key = (t60, damp, pre, er, seed, size, bright)
    if key in _ir_cache: return _ir_cache[key]
    n = secs(t60 * 1.2 + pre)
    out = []
    for ch in range(2):
        r = rng(seed * 10 + ch)
        tail = r.standard_normal(n).astype(np.float32)
        t = t_axis(n)
        # decaimento dependente de frequência: duas bandas
        lo = lp(tail, 800); hi = tail - lo
        e_lo = np.exp(-6.9 * t / t60); e_hi = np.exp(-6.9 * t / (t60 * 0.45))
        tail = lo * e_lo + hi * e_hi * bright
        tail = lp(tail, damp)
        tail[:secs(pre)] = 0
        # reflexões iniciais
        for k in range(er):
            p = secs(pre * 0.3 + r.uniform(0.002, 0.04) * size)
            if p < n: tail[p] += r.uniform(-0.6, 0.6) * (1 - k / er)
        tail /= np.sqrt(np.sum(tail ** 2)) + 1e-9
        out.append(tail)
    ir = np.stack(out)
    _ir_cache[key] = ir
    return ir


def reverb(x, ir, wet=0.3, dry=1.0):
    st = stereo(x)
    w = np.stack([signal.fftconvolve(st[c], ir[c])[:st.shape[1]] for c in range(2)])
    return (st * dry + w * wet).astype(np.float32)


def reverb_tail(x, ir, wet=0.3, dry=1.0):
    """Igual a reverb, mas mantém a cauda (saída mais longa)."""
    st = stereo(x)
    w = np.stack([signal.fftconvolve(st[c], ir[c]) for c in range(2)])
    out = w * wet
    out[:, :st.shape[1]] += st * dry
    return out.astype(np.float32)


# ---------------- dinâmica / saturação ----------------
def softclip(x, drive=1.0):
    return (np.tanh(x * drive) / np.tanh(drive)).astype(np.float32)


def compress(x, thresh_db=-18, ratio=3.0, attack=0.005, release=0.12, makeup_db=0.0):
    st = stereo(x)
    lvl = np.max(np.abs(st), axis=0)
    a_a, a_r = np.exp(-1 / (attack * SR)), np.exp(-1 / (release * SR))
    # envelope por blocos (rápido)
    blk = 64
    nb = int(np.ceil(len(lvl) / blk))
    pk = np.array([lvl[i * blk:(i + 1) * blk].max() for i in range(nb)])
    env = np.zeros(nb); e = 0.0
    aa, ar = a_a ** blk, a_r ** blk
    for i in range(nb):
        v = pk[i]; e = aa * e + (1 - aa) * v if v > e else ar * e + (1 - ar) * v; env[i] = e
    db = 20 * np.log10(env + 1e-9)
    gr = np.minimum(0, (thresh_db - db) * (1 - 1 / ratio))
    g = 10 ** ((gr + makeup_db) / 20)
    g = np.repeat(g, blk)[:len(lvl)]
    out = st * g
    return out if x.ndim == 2 else out[0]


def limiter(st, ceiling_db=-1.0, lookahead=0.005, release=0.08):
    c = 10 ** (ceiling_db / 20)
    lvl = np.max(np.abs(st), axis=0)
    la = secs(lookahead)
    # máximo deslizante (lookahead)
    from scipy.ndimage import maximum_filter1d
    m = maximum_filter1d(lvl, size=2 * la + 1)
    need = np.minimum(1.0, c / (m + 1e-9))
    # suaviza liberação
    blk = 32; nb = int(np.ceil(len(need) / blk))
    nmin = np.array([need[i * blk:(i + 1) * blk].min() for i in range(nb)])
    g = np.zeros(nb); cur = 1.0; ar = np.exp(-blk / (release * SR))
    for i in range(nb):
        cur = nmin[i] if nmin[i] < cur else ar * cur + (1 - ar) * nmin[i]; g[i] = cur
    g = np.repeat(g, blk)[:len(lvl)]
    return (st * g).astype(np.float32)


# ---------------- voz: sussurro por LPC ----------------
def lpc(frame, order):
    r = np.correlate(frame, frame, 'full')[len(frame) - 1:len(frame) + order]
    if r[0] <= 1e-9: return np.zeros(order + 1), 0
    from scipy.linalg import solve_toeplitz
    a = solve_toeplitz(r[:order], -r[1:order + 1])
    a = np.concatenate([[1.0], a])
    err = r[0] + np.dot(a[1:], r[1:order + 1])
    return a, max(err, 1e-12)


def whisperize(x, sr, order=24, seed=3):
    """Substitui a excitação vocal por ruído mantendo o envelope espectral (voz sussurrada)."""
    n = len(x); win = int(0.025 * sr); hop = win // 4
    w = np.hanning(win)
    out = np.zeros(n + win); norm = np.zeros(n + win)
    noise = rng(seed).standard_normal(n + win)
    for s in range(0, n - win, hop):
        fr = x[s:s + win] * w
        a, err = lpc(fr, order)
        if err <= 1e-10: continue
        g = np.sqrt(err / win)
        exc = noise[s:s + win] * g
        y = signal.lfilter([1.0], a, exc)
        out[s:s + win] += y * w; norm[s:s + win] += w ** 2
    out = out[:n] / (norm[:n] + 1e-6)
    out = hp(out.astype(np.float32), 300)
    return out / (np.max(np.abs(out)) + 1e-9) * np.max(np.abs(x))


def pitch_shift(x, sr, semis, formant=True):
    """Pitch shift de alta qualidade via ffmpeg/rubberband."""
    if semis == 0: return x
    with tempfile.TemporaryDirectory() as d:
        a, b = os.path.join(d, 'a.wav'), os.path.join(d, 'b.wav')
        sf.write(a, x, sr)
        ratio = 2 ** (semis / 12)
        f = f"rubberband=pitch={ratio:.6f}" + (":formant=preserved" if formant else "")
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', a, '-af', f, b], check=True)
        y, _ = sf.read(b, dtype='float32')
    return y


def resample(x, sr_in, sr_out=SR):
    from math import gcd
    g = gcd(sr_in, sr_out)
    return signal.resample_poly(x, sr_out // g, sr_in // g).astype(np.float32)


def place(dst, src, t, gain=1.0):
    """Mistura src (mono ou estéreo) em dst (estéreo) no tempo t (s)."""
    s = secs(t)
    src = stereo(src) * gain
    if s < 0:
        src = src[:, -s:]; s = 0
    e = min(dst.shape[1], s + src.shape[1])
    if e > s: dst[:, s:e] += src[:, :e - s]


def norm(x, peak_val=0.9):
    m = np.max(np.abs(x)) + 1e-9
    return (x / m * peak_val).astype(np.float32)
