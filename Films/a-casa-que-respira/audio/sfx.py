"""Efeitos sonoros sintetizados (todos originais, gerados por algoritmo).
Cada função devolve um array mono (n,) ou estéreo (2, n) a 48 kHz."""
import numpy as np
from scipy import signal

from dsp import (SR, secs, t_axis, white, pink, brown, lp, hp, bp, peak, resonator, env_exp, env_adsr, fade,
                 pan, stereo, rng, softclip, curve)


# ---------------- CHUVA ----------------
def rain(dur, intensity=1.0, mode='exterior', seed=0):
    """mode: exterior | roof (teto do carro) | window (vidro, abafado) | distant"""
    n = secs(dur); r = rng(seed)
    out = np.zeros((2, n), np.float32)
    for ch in range(2):
        hiss = bp(pink(n, seed + ch), 500, 9000, 2)
        mod = 0.85 + 0.15 * lp(white(n, seed + 10 + ch), 0.6) * 20
        # gotas: trem de impulsos esparsos
        rate = {'exterior': 1800, 'roof': 2600, 'window': 260, 'distant': 900}[mode] * intensity
        imp = np.zeros(n, np.float32)
        k = r.poisson(rate * dur)
        pos = r.integers(0, n, k); imp[pos] = r.uniform(0.2, 1.0, k) * r.choice([-1, 1], k)
        if mode == 'roof':
            drops = resonator(imp, 900, 6) * 0.5 + resonator(imp, 2200, 8) * 0.3 + bp(imp, 3000, 9000) * 0.4
            body = lp(hiss, 3000) * 0.6
            ch_sig = body * 0.25 + drops * 0.35
        elif mode == 'window':
            drops = resonator(imp, 2600, 12) * 0.7 + bp(imp, 4000, 10000) * 0.3
            ch_sig = lp(hiss, 1400) * 0.12 + drops * 0.4
        elif mode == 'distant':
            ch_sig = lp(hiss, 2500) * 0.18 + lp(bp(imp, 1500, 6000), 4000) * 0.15
        else:
            drops = bp(imp, 1500, 9000) * 0.35 + resonator(imp, 1100, 5) * 0.1
            ch_sig = hiss * 0.22 + drops * 0.5 + lp(brown(n, seed + 20 + ch), 180) * 0.06
        out[ch] = ch_sig * mod
    return out * intensity


# ---------------- TROVÃO ----------------
def thunder(dist=1.0, seed=0, dur=9.0):
    """dist: 0 (muito perto) .. 1 (longe)."""
    n = secs(dur); r = rng(seed); t = t_axis(n)
    out = np.zeros((2, n), np.float32)
    for ch in range(2):
        rum = brown(n, seed * 3 + ch)
        rum = lp(rum, 140 + 300 * (1 - dist), 2)
        envl = np.zeros(n)
        for k in range(r.integers(3, 7)):
            c = r.uniform(0.0, dur * 0.55); w = r.uniform(0.4, 2.2)
            envl += r.uniform(0.4, 1.0) * np.exp(-((t - c) / w) ** 2)
        envl *= np.exp(-t / (dur * 0.45)) * np.clip(t / 0.05, 0, 1)
        out[ch] = rum * envl * 0.9
        if dist < 0.5:  # estalo do raio
            crack = hp(white(n, seed + 50 + ch), 400) * env_exp(n, 0.05) * (0.5 - dist) * 2.4
            crackle = np.zeros(n, np.float32)
            for k in range(60):
                p = secs(r.uniform(0, 0.6)); crackle[p:p + 200] += r.uniform(-1, 1) * np.exp(-np.arange(min(200, n - p)) / 40)
            out[ch] += crack + hp(crackle, 800) * 0.5 * (0.5 - dist) * 2
    return out


# ---------------- VENTO / ÁRVORES ----------------
def wind(dur, strength=1.0, seed=0, trees=1.0):
    n = secs(dur); t = t_axis(n)
    out = np.zeros((2, n), np.float32)
    gust = 0.55 + 0.45 * np.sin(2 * np.pi * t / 7.3 + seed) * np.sin(2 * np.pi * t / 3.1 + 1.3 * seed)
    for ch in range(2):
        base = bp(pink(n, seed + ch), 120, 900) * 0.5
        whistle = resonator(pink(n, seed + 30 + ch), 420 + 80 * ch, 25) * 0.08
        rustle = bp(white(n, seed + 60 + ch), 2500, 9000) * 0.12 * trees
        out[ch] = (base + whistle + rustle * gust) * gust
    return out * strength


def room_tone(dur, seed=0, level=1.0):
    n = secs(dur)
    out = np.stack([lp(brown(n, seed + c), 160) * 0.05 + lp(pink(n, seed + 5 + c), 900) * 0.006 for c in range(2)])
    return out * level


# ---------------- CARRO ----------------
def engine(dur, rpm=1500, load=0.5, seed=0, interior=True):
    n = secs(dur); t = t_axis(n); r = rng(seed)
    rpm_t = rpm * (1 + 0.02 * lp(white(n, seed), 1.0) * 30) if np.isscalar(rpm) else rpm
    f0 = rpm_t / 60 * 2  # 4 cilindros
    ph = 2 * np.pi * np.cumsum(f0) / SR
    x = sum((1 / k) * np.sin(k * ph + r.uniform(0, 6)) for k in range(1, 12))
    x = lp(x.astype(np.float32), 350 if interior else 900)
    x += lp(white(n, seed + 1), 300) * 0.25
    return (x * (0.4 + 0.6 * load)).astype(np.float32)


def tires_wet(dur, speed=1.0, seed=0):
    n = secs(dur)
    return np.stack([bp(pink(n, seed + c), 700, 7000) * 0.3 * speed for c in range(2)])


def wiper_cycle(seed=0, interior=True):
    """Um varrido: chiado de borracha no vidro + batida no fim do curso."""
    n = secs(0.62); t = t_axis(n); r = rng(seed)
    sq_f = 900 + 400 * np.sin(np.pi * t / 0.62)
    squeak = np.sin(2 * np.pi * np.cumsum(sq_f) / SR) * (0.15 + 0.1 * r.random())
    swish = bp(white(n, seed), 1500, 6000) * 0.25
    e = np.sin(np.pi * np.clip(t / 0.62, 0, 1)) ** 1.5
    x = (squeak * 0.25 + swish) * e
    thump = np.zeros(n, np.float32); th = secs(0.06)
    thump[-th:] = np.sin(2 * np.pi * 95 * t_axis(th)) * np.exp(-t_axis(th) / 0.02)
    x = x + thump * 0.9
    return lp(x.astype(np.float32), 5000 if interior else 3000)


def car_door(kind='close', seed=0):
    r = rng(seed)
    if kind == 'open':
        n = secs(0.5); click = bp(white(n, seed), 2000, 7000) * env_exp(n, 0.004)
        creak = friction_creak(0.35, f0=(60, 30), res=(700, 1500, 2600), seed=seed) * 0.15
        x = click * 0.8; x[:len(creak)] += creak
        return x
    n = secs(0.9); t = t_axis(n)
    thump = np.sin(2 * np.pi * 70 * t) * np.exp(-t / 0.09) + lp(white(n, seed), 400) * np.exp(-t / 0.05)
    latch = bp(white(n, seed + 1), 2000, 6000) * np.exp(-np.maximum(0, t - 0.012) / 0.006) * (t > 0.012)
    rattle = bp(white(n, seed + 2), 800, 3000) * np.exp(-t / 0.15) * 0.15
    return (thump * 0.9 + latch * 0.5 + rattle).astype(np.float32)


# ---------------- MADEIRA / RANGIDOS ----------------
def friction_creak(dur, f0=(40, 25), res=(420, 980, 1900), q=14, seed=0, jitter=0.25):
    """Rangido por modelo stick-slip: trem de micro-impulsos com período variável, filtrado por ressonâncias."""
    n = secs(dur); r = rng(seed); t = t_axis(n)
    fa, fb = f0
    f = fa + (fb - fa) * (t / dur) + (fa * jitter) * np.sin(2 * np.pi * t * r.uniform(1.5, 4)) + fa * jitter * lp(white(n, seed), 8) * 6
    f = np.maximum(f, 4)
    ph = np.cumsum(f) / SR
    imp = np.zeros(n, np.float32)
    idx = np.where(np.diff(np.floor(ph)) > 0)[0]
    imp[idx] = r.uniform(0.5, 1.0, len(idx))
    x = sum(resonator(imp, fr * r.uniform(0.95, 1.05), q) * (1 / (i + 1)) for i, fr in enumerate(res))
    e = np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 0.6
    return (x * e).astype(np.float32)


def door_creak_long(dur=4.0, seed=0):
    """Porta antiga abrindo devagar: rangido longo com variações e gemido grave."""
    r = rng(seed); n = secs(dur)
    out = np.zeros(n, np.float32)
    k = dur / 4.0
    segs = [(0.0, 0.7 * k, (55, 35)), (0.6 * k, 1.4 * k, (30, 18)), (1.8 * k, 1.2 * k, (22, 40)), (2.8 * k, 1.15 * k, (45, 20))]
    for i, (s, d, f) in enumerate(segs):
        c = friction_creak(d, f0=f, res=(380, 760, 1450, 2600), q=18, seed=seed + i)
        p = secs(s); e = min(n, p + len(c))
        if e > p: out[p:e] += c[:max(0, e - p)] * r.uniform(0.6, 1.0)
    groan = friction_creak(dur * 0.9, f0=(14, 9), res=(160, 330), q=10, seed=seed + 9) * 0.6
    out[:len(groan)] += groan
    return out


def house_creak(seed=0, strength=1.0):
    r = rng(seed)
    d = r.uniform(0.3, 1.1)
    c = friction_creak(d, f0=(r.uniform(15, 40), r.uniform(10, 35)), res=(r.uniform(250, 450), r.uniform(700, 1100), r.uniform(1500, 2400)), q=12, seed=seed)
    return c * strength


def footstep(surface='wood', seed=0, intensity=1.0, run=0):
    r = rng(seed); n = secs(0.5); t = t_axis(n)
    x = np.zeros(n, np.float32)
    if surface in ('wood', 'stairs', 'porch'):
        heel = np.zeros(n, np.float32); heel[0] = 1; heel[secs(0.03 + r.uniform(0, 0.02))] = 0.6
        body_f = {'wood': 140, 'stairs': 115, 'porch': 175}[surface]
        thud = resonator(heel, body_f * r.uniform(0.9, 1.1), 8) * 2 + resonator(heel, body_f * 2.3, 10) + resonator(heel, 900, 6) * 0.3
        click = bp(white(n, seed), 1500, 6000) * np.exp(-t / 0.008) * 0.35
        scuff = bp(white(n, seed + 1), 800, 4000) * np.exp(-((t - 0.08) / 0.05) ** 2) * 0.08
        x = lp(thud, 2500) * np.exp(-t / 0.12) * 0.6 + click + scuff
        pcreak = {'wood': 0.25, 'stairs': 0.85, 'porch': 0.5}[surface]
        if r.random() < pcreak:
            c = friction_creak(r.uniform(0.25, 0.6), f0=(r.uniform(25, 45), r.uniform(15, 30)), res=(r.uniform(300, 500), r.uniform(800, 1200), 2000), q=14, seed=seed + 7)
            c *= 0.35 if surface != 'stairs' else 0.55
            p = secs(0.04); x2 = np.zeros(max(n, p + len(c)), np.float32); x2[:n] += x; x2[p:p + len(c)] += c; x = x2
    elif surface == 'gravel':
        for k in range(18):
            p = secs(r.uniform(0, 0.12)); g = bp(white(secs(0.02), seed + k), 1500, 8000) * np.exp(-t_axis(secs(0.02)) / 0.004) * r.uniform(0.2, 1)
            x[p:p + len(g)] += g[:max(0, n - p)]
        x += lp(white(n, seed + 99), 300) * np.exp(-t / 0.03) * 0.5
        x *= 0.7
    elif surface == 'carpet':
        x = lp(white(n, seed), 500) * np.exp(-t / 0.05) * 0.6 + resonator(np.eye(1, n)[0].astype(np.float32), 90, 5) * 0.4
    x = x * intensity * (1 + 0.6 * run)
    return x.astype(np.float32)


def knock_floor(seed=0):
    """Batida pesada vinda de baixo do assoalho (abafada, com corpo grave)."""
    n = secs(1.2); t = t_axis(n); r = rng(seed)
    imp = np.zeros(n, np.float32); imp[0] = 1.0
    body = resonator(imp, 72, 6) * 3 + resonator(imp, 155, 9) * 1.5 + resonator(imp, 340, 10) * 0.5
    sub = np.sin(2 * np.pi * 48 * t) * np.exp(-t / 0.18)
    x = lp(body, 700) * np.exp(-t / 0.3) + sub * 0.6 + lp(white(n, seed), 900) * np.exp(-t / 0.02) * 0.4
    return x.astype(np.float32)


def heavy_thud(seed=0):
    n = secs(3.0); t = t_axis(n)
    boom = np.sin(2 * np.pi * (52 - 12 * t) * t) * np.exp(-t / 0.35)
    imp = np.zeros(n, np.float32); imp[0] = 1
    wood = resonator(imp, 120, 6) * 2 + resonator(imp, 260, 8)
    dust = bp(white(n, seed), 3000, 10000) * (np.exp(-((t - 0.8) / 0.6) ** 2)) * 0.05 * (0.5 + 0.5 * np.sign(np.sin(t * 90)))
    return (boom * 0.9 + lp(wood, 900) * np.exp(-t / 0.25) + dust).astype(np.float32)


# ---------------- METAL / OBJETOS ----------------
def metal_clink(seed=0, f=3200, dur=0.35):
    r = rng(seed); n = secs(dur); t = t_axis(n)
    ratios = [1, 1.58, 2.31, 3.17, 4.4]
    x = sum(np.sin(2 * np.pi * f * k * r.uniform(0.98, 1.02) * t + r.uniform(0, 6)) * np.exp(-t / (0.08 / (i + 1) ** 0.5)) / (i + 1) for i, k in enumerate(ratios))
    return (x * 0.4).astype(np.float32)


def keys_jingle(seed=0, dur=0.9):
    r = rng(seed); n = secs(dur); x = np.zeros(n, np.float32)
    for k in range(9):
        c = metal_clink(seed + k, f=r.uniform(2500, 5200), dur=0.3)
        p = secs(r.uniform(0, dur - 0.3)); x[p:p + len(c)] += c * r.uniform(0.3, 1)
    return x


def key_insert(seed=0):
    n = secs(0.45); t = t_axis(n)
    scrape = bp(white(n, seed), 3000, 9000) * (0.3 + 0.7 * np.abs(np.sin(t * 60))) * np.exp(-t / 0.25) * 0.3
    tick = np.zeros(n, np.float32)
    for p in [0.1, 0.18, 0.26, 0.33]: tick[secs(p)] = 1
    return (scrape + bp(tick, 2000, 8000) * 0.6 + metal_clink(seed, 2900, 0.45)[:n] * 0.2).astype(np.float32)


def lock_turn(seed=0):
    n = secs(0.8); t = t_axis(n)
    grind = bp(white(n, seed), 1500, 5000) * np.exp(-((t - 0.25) / 0.12) ** 2) * 0.2
    clunk = np.zeros(n, np.float32); p = secs(0.42); m = n - p
    clunk[p:] = (np.sin(2 * np.pi * 180 * t_axis(m)) * np.exp(-t_axis(m) / 0.05) + bp(white(m, seed + 1), 1000, 6000) * np.exp(-t_axis(m) / 0.01))
    return (grind + clunk * 0.8).astype(np.float32)


def knob_turn(dur=2.2, seed=0):
    """Maçaneta de ferro girando devagar: rangido metálico + trinco."""
    sq = friction_creak(dur, f0=(28, 16), res=(1300, 2700, 4100), q=25, seed=seed, jitter=0.35) * 0.5
    r = rng(seed); x = sq.copy()
    for k in range(6):
        c = metal_clink(seed + k, f=r.uniform(1800, 3000), dur=0.12)
        p = secs(r.uniform(0.1, dur - 0.2)); x[p:p + len(c)] += c * 0.25
    return x


def click(seed=0, f=(1800, 6000), level=0.6):
    n = secs(0.05); t = t_axis(n)
    return (bp(white(n, seed), *f) * np.exp(-t / 0.003) * level).astype(np.float32)


def flashlight_click(seed=0):
    x = np.zeros(secs(0.12), np.float32); a = click(seed); b = click(seed + 1, level=0.4)
    x[:len(a)] += a; x[secs(0.05):secs(0.05) + len(b)] += b
    return x


def page_flip(seed=0):
    r = rng(seed); n = secs(0.7); t = t_axis(n)
    swish = bp(white(n, seed), 900, 7000) * np.sin(np.pi * np.clip(t / 0.5, 0, 1)) ** 2 * 0.35
    flutter = 0.6 + 0.4 * np.sin(2 * np.pi * 26 * t + r.uniform(0, 6))
    slap = np.zeros(n, np.float32); p = secs(0.5); m = n - p
    slap[p:] = lp(white(m, seed + 1), 2500) * np.exp(-t_axis(m) / 0.02) * 0.5
    return (swish * flutter + slap).astype(np.float32)


def paper_rustle(dur=1.2, seed=0, level=0.4):
    r = rng(seed); n = secs(dur); x = np.zeros(n, np.float32)
    for k in range(int(dur * 18)):
        g = secs(r.uniform(0.02, 0.09)); p = secs(r.uniform(0, dur - 0.1))
        seg = bp(white(g, seed + k), 1200, 8000) * np.hanning(g) * r.uniform(0.2, 1)
        x[p:p + g] += seg
    return x * level


def book_open(seed=0):
    c = friction_creak(0.6, f0=(70, 40), res=(900, 1900, 3200), q=10, seed=seed) * 0.15
    n = secs(1.3); x = np.zeros(n, np.float32); x[:len(c)] += c
    p = secs(0.85); m = n - p; t = t_axis(m)
    x[p:] += (lp(white(m, seed + 3), 900) * np.exp(-t / 0.03) + np.sin(2 * np.pi * 110 * t) * np.exp(-t / 0.05) * 0.6) * 0.7
    return x


def box_open(seed=0):
    c = friction_creak(1.0, f0=(35, 22), res=(500, 1300, 2600), q=16, seed=seed) * 0.4
    n = secs(1.6); x = np.zeros(n, np.float32); x[:len(c)] += c
    p = secs(1.1); m = n - p; t = t_axis(m)
    x[p:] += np.sin(2 * np.pi * 150 * t) * np.exp(-t / 0.04) * 0.4 + lp(white(m, seed), 1500) * np.exp(-t / 0.01) * 0.3
    return x


# ---------------- RELÓGIO ----------------
def clock_tick(tock=False, seed=0):
    n = secs(0.25); t = t_axis(n); imp = np.zeros(n, np.float32); imp[0] = 1; imp[secs(0.006)] = 0.5
    f = 2600 if not tock else 2100
    x = resonator(imp, f, 30) * 0.6 + resonator(imp, 820 if not tock else 700, 12) * 0.9 + resonator(imp, 5200, 40) * 0.2
    return (x * np.exp(-t / 0.05)).astype(np.float32)


def chime(f=147.0, seed=0, dur=6.0):
    """Gongo de carrilhão (barra metálica): parciais inarmônicos com decaimentos distintos."""
    n = secs(dur); t = t_axis(n); r = rng(seed)
    parts = [(0.5, 1.0, 3.2), (1.0, 0.8, 2.6), (2.0, 0.55, 1.6), (2.76, 0.4, 1.2), (5.4, 0.25, 0.6), (8.9, 0.12, 0.35), (1.19, 0.3, 2.0)]
    x = np.zeros(n)
    for ratio, a, tau in parts:
        fr = f * ratio * r.uniform(0.997, 1.003)
        x += a * np.sin(2 * np.pi * fr * t + r.uniform(0, 6)) * np.exp(-t / tau) * (1 + 0.05 * np.sin(2 * np.pi * 0.8 * t))
    strike = bp(white(n, seed), 1500, 6000) * np.exp(-t / 0.004) * 0.3
    return ((x * 0.25 + strike) * np.clip(t / 0.002, 0, 1)).astype(np.float32)


def clock_whirr(seed=0):
    n = secs(1.2); t = t_axis(n)
    f = 40 + 160 * (t / 1.2) ** 2
    ph = np.cumsum(f) / SR
    imp = (np.diff(np.floor(ph), prepend=0) > 0).astype(np.float32)
    x = resonator(imp, 2400, 20) * 0.4 + resonator(imp, 900, 10) * 0.3
    return (x * np.sin(np.pi * t / 1.2)).astype(np.float32)


# ---------------- RESPIRAÇÃO ----------------
def breath(inhale=True, dur=1.6, seed=0, size=1.0, wet=False):
    """Uma respiração (inspiração ou expiração) com formantes de trato vocal."""
    n = secs(dur); t = t_axis(n)
    nz = pink(n, seed) if inhale else brown(n, seed) * 0.6 + pink(n, seed + 1) * 0.4
    fm = [600, 1200, 2600] if inhale else [450, 1000, 2200]
    fm = [f / size for f in fm]
    x = sum(resonator(nz, f, 4) * g for f, g in zip(fm, [1.0, 0.6, 0.3])) + bp(nz, 300 / size, 4000 / size) * 0.4
    e = np.sin(np.pi * np.clip(t / dur, 0, 1)) ** (1.4 if inhale else 0.9)
    if not inhale: e *= np.exp(-t / (dur * 0.8))
    x = x * e
    if wet:
        r = rng(seed + 5)
        for k in range(int(dur * 10)):
            p = secs(r.uniform(0, dur)); c = click(seed + k, (300, 1500), r.uniform(0.05, 0.2))
            x[p:p + len(c)] += c[:max(0, n - p)]
    return (x / (np.max(np.abs(x)) + 1e-9) * 0.6).astype(np.float32)


def house_breathing(dur, period=4.2, seed=0, t0=0.0):
    """A casa respira: ciclo lento (inspira na subida, expira na descida), grave e enorme."""
    n = secs(dur); x = np.zeros(n, np.float32)
    k = 0; s = -((t0 % period))
    while s < dur:
        inh = breath(True, period * 0.48, seed + k, size=2.6)
        exh = breath(False, period * 0.55, seed + 100 + k, size=2.8)
        for seg, off in [(inh, 0.0), (exh, period * 0.46)]:
            p = secs(s + off)
            if p < 0:
                seg = seg[-p:]; p = 0
            if p >= n: continue
            e = min(n, p + len(seg)); x[p:e] += seg[:max(0, e - p)]
        s += period; k += 1
    t = t_axis(n)
    sub = np.sin(2 * np.pi * 38 * t) * (0.5 - 0.5 * np.cos(2 * np.pi * (t + t0) / period)) * 0.25
    return lp(x, 1400) * 0.8 + sub.astype(np.float32)


def heartbeat(dur, bpm=62, seed=0):
    n = secs(dur); x = np.zeros(n, np.float32); per = 60 / bpm
    for k in range(int(dur / per) + 1):
        for off, a in [(0, 1.0), (0.24, 0.7)]:
            p = secs(k * per + off); m = secs(0.18)
            if p + m > n: continue
            tt = t_axis(m); x[p:p + m] += np.sin(2 * np.pi * (55 - 25 * tt) * tt) * np.exp(-tt / 0.05) * a
    return x


def panting(dur, rate=1.6, seed=0):
    n = secs(dur); x = np.zeros(n, np.float32); per = 1 / rate; k = 0; s = 0.0
    while s < dur:
        for inh, off, d in [(True, 0, per * 0.42), (False, per * 0.45, per * 0.5)]:
            b = breath(inh, d, seed + k * 2 + inh, size=1.0) * (0.9 if not inh else 0.7)
            p = secs(s + off); e = min(n, p + len(b)); x[p:e] += b[:max(0, e - p)]
        s += per; k += 1
    return x


# ---------------- TELEFONE / RÁDIO / INTERFERÊNCIA ----------------
def phone_vibrate(dur=0.9, seed=0, on_wood=True):
    n = secs(dur); t = t_axis(n)
    m = np.sign(np.sin(2 * np.pi * 165 * t)) * 0.4 + np.sin(2 * np.pi * 330 * t) * 0.2
    gate = ((t % 0.45) < 0.32).astype(np.float32)
    x = m * gate
    if on_wood: x = resonator(x.astype(np.float32), 320, 4) * 0.5 + bp(x.astype(np.float32), 1500, 5000) * 0.3 + x * 0.2
    return (lp(x.astype(np.float32), 4000) * 0.5).astype(np.float32)


def ringtone(dur, seed=0):
    """Toque original (marimba digital), padrão repetido."""
    n = secs(dur); x = np.zeros(n, np.float32)
    notes = [659.25, 493.88, 659.25, 783.99, 739.99, 493.88]
    step = 0.16; cycle = len(notes) * step + 0.9
    s = 0.0
    while s < dur:
        for i, f in enumerate(notes):
            p = secs(s + i * step); m = secs(0.4)
            if p + m > n: break
            tt = t_axis(m)
            x[p:p + m] += (np.sin(2 * np.pi * f * tt) + 0.3 * np.sin(2 * np.pi * f * 4 * tt) * np.exp(-tt / 0.02)) * np.exp(-tt / 0.12)
        s += cycle
    return (x * 0.35).astype(np.float32)


def radio_static(dur, seed=0, tuning=True):
    n = secs(dur); t = t_axis(n); r = rng(seed)
    hiss = bp(white(n, seed), 400, 5000) * 0.35
    crack = np.zeros(n, np.float32); k = int(dur * 60); pos = r.integers(0, n, k); crack[pos] = r.uniform(-1, 1, k)
    crack = bp(crack, 800, 6000) * 0.9
    am = 0.6 + 0.4 * np.sin(2 * np.pi * 3.3 * t) * np.sin(2 * np.pi * 0.7 * t)
    x = (hiss * am + crack)
    if tuning:
        f = 1200 + 900 * np.sin(2 * np.pi * 0.23 * t) + 300 * np.sin(2 * np.pi * 1.7 * t)
        x += np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.05 * (0.5 + 0.5 * np.sin(2 * np.pi * 0.4 * t))
    return x.astype(np.float32)


def radio_music(dur, seed=0):
    """Canção lo-fi no rádio (violão em Karplus-Strong, valsa em lá menor) — original."""
    n = secs(dur); x = np.zeros(n, np.float32)
    chords = [[220.0, 261.63, 329.63], [174.61, 220.0, 261.63], [196.0, 246.94, 293.66], [164.81, 207.65, 246.94]]
    beat = 0.42; s = 0.0; k = 0
    r = rng(seed)
    while s < dur:
        ch = chords[(k // 3) % 4]
        f = ch[0] / 2 if k % 3 == 0 else ch[1 + (k % 2)]
        m = secs(1.6); p = secs(s)
        if p + m > n: break
        x[p:p + m] += karplus(f, 1.6, seed + k) * (0.8 if k % 3 == 0 else 0.5)
        s += beat; k += 1
    return bp(x, 300, 3500) * 0.6


def karplus(f, dur, seed=0):
    n = secs(dur); N = max(2, int(SR / f)); r = rng(seed)
    buf = r.uniform(-1, 1, N); out = np.zeros(n, np.float32)
    for i in range(n):
        out[i] = buf[i % N]
        buf[i % N] = 0.996 * 0.5 * (buf[i % N] + buf[(i + 1) % N])
    return out


def gsm_interference(dur, seed=0, density=1.0):
    """Zumbido de interferência de celular (rajadas TDMA) + falhas digitais."""
    n = secs(dur); t = t_axis(n); r = rng(seed)
    burst = ((t * 217) % 1 < 0.5).astype(np.float32) * ((t % 0.46) < 0.33)
    tone = np.sign(np.sin(2 * np.pi * 217 * t)) * 0.3
    x = bp((tone * burst).astype(np.float32), 200, 5000)
    gl = np.zeros(n, np.float32)
    for k in range(int(dur * 8 * density)):
        p = secs(r.uniform(0, dur)); m = secs(r.uniform(0.01, 0.08))
        gl[p:p + m] += np.sign(np.sin(2 * np.pi * r.uniform(300, 3000) * t_axis(min(m, n - p)))) * r.uniform(0.2, 0.6)
    return (x * 0.5 + gl * 0.3 * density).astype(np.float32)


def electric_flicker(dur, seed=0):
    n = secs(dur); t = t_axis(n); r = rng(seed)
    buzz = sum(np.sin(2 * np.pi * 120 * k * t) / k for k in range(1, 8)) * 0.1
    gate = (lp(white(n, seed), 20) * 30 > 0).astype(np.float32)
    cr = np.zeros(n, np.float32); pos = r.integers(0, n, int(dur * 40)); cr[pos] = r.uniform(-1, 1, len(pos))
    return (buzz * gate + bp(cr, 1500, 8000) * 0.6 * gate).astype(np.float32)


# ---------------- IMPACTOS / CINEMA ----------------
def sub_boom(dur=3.0, f0=55, f1=28, seed=0):
    n = secs(dur); t = t_axis(n)
    f = f0 + (f1 - f0) * (t / dur)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / (dur * 0.35))
    x += lp(white(n, seed), 200) * np.exp(-t / 0.15) * 0.4
    return (x * np.clip(t / 0.004, 0, 1)).astype(np.float32)


def riser(dur=4.0, seed=0):
    n = secs(dur); t = t_axis(n); u = t / dur
    nz = white(n, seed)
    x = np.zeros(n, np.float32)
    for i in range(0, n, 2048):
        f = 300 + 6000 * u[i] ** 2
        x[i:i + 2048] = bp(nz[i:i + 2048], f * 0.7, min(f * 1.4, 20000))
    tone = sum(np.sin(2 * np.pi * np.cumsum(110 * 2 ** (k + 2 * u)) / SR) for k in range(3)) * 0.1
    return ((x * 0.3 + tone) * u ** 2).astype(np.float32)


def bone_crack(seed=0):
    r = rng(seed); n = secs(0.6); x = np.zeros(n, np.float32)
    for k in range(14):
        c = click(seed + k, (600, 4000), r.uniform(0.2, 0.7)); p = secs(r.uniform(0, 0.5))
        x[p:p + len(c)] += c[:max(0, n - p)]
    return x


def reverse(x):
    return x[..., ::-1].copy()
