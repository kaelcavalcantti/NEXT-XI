"""Trilha original de A CASA QUE RESPIRA — Parte 1.
Identidade: tema de cinco notas em ré menor ("o tema da casa": Ré–Fá–Mi–Si♭–Lá),
que aparece no piano (abertura e fotografia), na caixinha de música (título e quarto),
distorcido nas cordas graves (porão) e em cluster de metais no clímax (o duplo).
Instrumentos sintetizados: piano aditivo, caixinha de música, cordas (supersaw), violoncelo,
metais, tímpanos/impactos, drones e harmônicos."""
import numpy as np

from dsp import SR, secs, t_axis, white, pink, brown, lp, hp, bp, resonator, env_adsr, env_exp, pan, stereo, rng, place, curve, softclip
from sfx import sub_boom, riser, heartbeat

A4 = 440.0


def hz(note):
    """'D4', 'F#3', 'Bb2' -> Hz"""
    names = {'C': -9, 'D': -7, 'E': -5, 'F': -4, 'G': -2, 'A': 0, 'B': 2}
    n = names[note[0]]; i = 1
    if len(note) > 2 or (len(note) == 2 and note[1] in '#b'):
        if note[1] == '#': n += 1; i = 2
        elif note[1] == 'b': n -= 1; i = 2
    octv = int(note[i:])
    return A4 * 2 ** ((n + (octv - 4) * 12) / 12)


MOTIF = [('D5', 1.0), ('F5', 1.0), ('E5', 1.0), ('Bb4', 1.5), ('A4', 2.5)]


# ---------------- instrumentos ----------------
def saw(f, n, phase=0.0):
    t = t_axis(n)
    return (2 * ((f * t + phase) % 1.0) - 1).astype(np.float32)


def piano(f, dur=4.0, vel=0.7, seed=0):
    n = secs(dur); t = t_axis(n); r = rng(seed)
    B = 0.00035; x = np.zeros(n)
    for k in range(1, 14):
        fk = k * f * np.sqrt(1 + B * k * k)
        if fk > 16000: break
        a = vel ** (1 + 0.15 * k) / k ** 1.1
        tau = (3.8 / (1 + 0.35 * k)) * (220 / max(f, 80)) ** 0.3
        x += a * np.sin(2 * np.pi * fk * t + r.uniform(0, 6)) * (0.7 * np.exp(-t / tau) + 0.3 * np.exp(-t / (tau * 3)))
    ham = lp(white(n, seed), 3000) * np.exp(-t / 0.006) * 0.15 * vel
    e = np.clip(t / 0.002, 0, 1) * np.clip((dur - t) / 0.3, 0, 1)
    return ((x * 0.4 + ham) * e).astype(np.float32)


def musicbox(f, dur=2.5, seed=0, detune=0.0):
    n = secs(dur); t = t_axis(n); r = rng(seed)
    f = f * 2 ** (detune / 1200)
    x = np.zeros(n)
    for ratio, a, tau in [(1, 1.0, 1.4), (3.01, 0.35, 0.5), (5.43, 0.2, 0.25), (8.93, 0.1, 0.12), (2.0, 0.12, 0.9)]:
        x += a * np.sin(2 * np.pi * f * ratio * t + r.uniform(0, 6)) * np.exp(-t / tau)
    tick = bp(white(n, seed), 3000, 9000) * np.exp(-t / 0.002) * 0.15
    return ((x * 0.25 + tick) * np.clip(t / 0.001, 0, 1)).astype(np.float32)


def strings(freqs, dur, att=1.5, rel=2.0, cutoff=2400, seed=0, trem=0.0, vib=0.004, voices=5, level=0.15):
    n = secs(dur); t = t_axis(n); r = rng(seed)
    out = np.zeros((2, n), np.float32)
    for fi, f in enumerate(freqs):
        for v in range(voices):
            det = 1 + (v - voices / 2) * 0.0035 + r.uniform(-0.001, 0.001)
            vibr = 1 + vib * np.sin(2 * np.pi * (5 + r.uniform(-0.5, 0.5)) * t + r.uniform(0, 6))
            ph = np.cumsum(f * det * vibr) / SR
            s = (2 * ((ph + r.uniform(0, 1)) % 1.0) - 1).astype(np.float32)
            p = (v / (voices - 1)) * 1.4 - 0.7 if voices > 1 else 0
            out += pan(s, p * 0.8)
    out = lp(out, cutoff, 2)
    e = env_adsr(n, att, 0.3, 0.85, rel)
    if trem: e = e * (1 - trem * 0.5 * (1 + np.sin(2 * np.pi * 11 * t)))
    return out * e * level / max(1, len(freqs)) ** 0.5


def cello(f, dur, att=0.08, rel=0.4, seed=0, level=0.25, pizz=False):
    n = secs(dur); t = t_axis(n); r = rng(seed)
    vib = 1 + 0.005 * np.sin(2 * np.pi * 5.2 * t) * np.clip(t / 0.4, 0, 1)
    ph = np.cumsum(f * vib) / SR
    s = (2 * (ph % 1.0) - 1).astype(np.float32)
    s = resonator(s, 280, 3) * 0.5 + resonator(s, 820, 4) * 0.3 + lp(s, 1600) * 0.6
    bow = bp(white(n, seed), 1000, 4000) * 0.03
    e = env_exp(n, 0.25) if pizz else env_adsr(n, att, 0.1, 0.9, rel)
    return ((s + bow) * e * level).astype(np.float32)


def brass(freqs, dur, att=0.6, seed=0, level=0.2, growl=0.0):
    n = secs(dur); t = t_axis(n)
    out = np.zeros(n, np.float32)
    for f in freqs:
        for d in (0.997, 1.003):
            out += saw(f * d, n)
    e = env_adsr(n, att, 0.4, 0.9, 1.2)
    # filtro que "abre" com o envelope (timbre metálico)
    a = lp(out, 600) * (1 - e) + lp(out, 3200) * e
    if growl: a = softclip(a * (1 + growl * 2), 1.5 + growl)
    return (a * e * level / len(freqs) ** 0.5).astype(np.float32)


def choir(freqs, dur, vowel='a', att=2.0, seed=0, level=0.12):
    """Coro sintético (pulsos glotais + formantes) — 'aaa' sombrio."""
    n = secs(dur); t = t_axis(n); r = rng(seed)
    F = {'a': [(700, 1.0), (1150, 0.6), (2600, 0.25)], 'o': [(450, 1.0), (800, 0.5), (2500, 0.15)], 'u': [(320, 1.0), (750, 0.3), (2400, 0.1)]}[vowel]
    out = np.zeros((2, n), np.float32)
    for f in freqs:
        for v in range(4):
            vib = 1 + 0.006 * np.sin(2 * np.pi * (5.5 + r.uniform(-0.6, 0.6)) * t + r.uniform(0, 6))
            ph = np.cumsum(f * (1 + r.uniform(-0.004, 0.004)) * vib) / SR
            src = (2 * (ph % 1.0) - 1).astype(np.float32) + white(n, seed + v) * 0.05
            s = sum(resonator(src, fr, 8) * a for fr, a in F)
            out += pan(s.astype(np.float32), r.uniform(-0.7, 0.7))
    return out * env_adsr(n, att, 0.5, 0.9, 2.0) * level / len(freqs) ** 0.5


def drone(f, dur, seed=0, level=0.2, cutoff=500, beat=0.13):
    n = secs(dur); t = t_axis(n)
    x = (saw(f, n) * 0.4 + saw(f * (1 + beat / f), n) * 0.4 + np.sin(2 * np.pi * f / 2 * t) * 0.6).astype(np.float32)
    x = lp(x, cutoff)
    mod = 0.8 + 0.2 * np.sin(2 * np.pi * 0.07 * t + seed)
    return np.stack([x * mod, lp(saw(f * 1.002, n) * 0.4 + np.sin(2 * np.pi * f / 2 * t + 0.5) * 0.6, cutoff) * mod]) * level


def harmonics(freqs, dur, seed=0, level=0.04):
    n = secs(dur); t = t_axis(n); r = rng(seed)
    out = np.zeros((2, n), np.float32)
    for f in freqs:
        s = np.sin(2 * np.pi * f * t * (1 + 0.002 * np.sin(2 * np.pi * 0.3 * t + r.uniform(0, 6))))
        out += pan(s.astype(np.float32), r.uniform(-0.8, 0.8))
    return out * env_adsr(n, 2.0, 0.5, 0.9, 2.0) * level


def timpani(f=73.4, seed=0, level=0.6):
    n = secs(3.0); t = t_axis(n)
    x = np.sin(2 * np.pi * f * t) * np.exp(-t / 0.8) + np.sin(2 * np.pi * f * 1.5 * t) * np.exp(-t / 0.4) * 0.4
    x += lp(white(n, seed), 400) * np.exp(-t / 0.05)
    return (x * level).astype(np.float32)


# ---------------- trilha ----------------
def score(tl, total):
    S = {s['id']: s['start'] for s in tl['shots']}
    E = tl['events']
    n = secs(total) + secs(4)
    M = np.zeros((2, n), np.float32)   # música com reverb de sala grande
    D = np.zeros((2, n), np.float32)   # música "seca" (graves, impactos)
    beat = 0.9

    def motif_piano(t0, octave_shift=0, vel=0.55, tempo=1.0, seed=0, buf=M, last_long=True):
        t = t0
        for i, (nm, b) in enumerate(MOTIF):
            f = hz(nm) * 2 ** octave_shift
            place(buf, pan(piano(f, 5.0, vel, seed + i), -0.15 + 0.08 * i), t)
            place(buf, pan(piano(f / 2, 5.0, vel * 0.5, seed + 10 + i), 0.1), t)
            t += b * beat * tempo
        return t

    def motif_box(t0, tempo=1.0, slowdown=0.0, detune=0.0, seed=0, gain=1.0):
        t = t0
        for k in range(2):
            for i, (nm, b) in enumerate(MOTIF):
                f = hz(nm) * 2
                place(M, pan(musicbox(f, 3.0, seed + k * 10 + i, detune * (i % 2 * 2 - 1) + slowdown * k * 20), 0.25), t, gain)
                t += b * 0.62 * tempo * (1 + slowdown * (k * 5 + i) * 0.05)
            t += 0.5
        return t

    # ===== 1. ABERTURA (misteriosa, lenta, cinematográfica) =====
    place(D, drone(hz('D2'), 34.0, 1, 0.16, 380), 0.0)
    place(M, harmonics([hz('A5'), hz('D6'), hz('E6')], 30.0, 2, 0.02), 1.5)
    motif_piano(4.0, 0, 0.5, 1.25, seed=3)
    # ostinato de violoncelo pizzicato (estrada) — tensão crescente
    for k in range(int((33.2 - 13.0) / (beat * 0.75))):
        tt = 13.0 + k * beat * 0.75
        note = ['D2', 'D2', 'A1', 'Bb1'][k % 4]
        place(D, pan(cello(hz(note), 0.8, seed=k, level=0.18 + 0.12 * (tt - 13) / 20, pizz=True), -0.2), tt)
    place(M, strings([hz('D3'), hz('A3'), hz('F4')], 12.0, att=4, rel=2, cutoff=1500, seed=5, level=0.12), 17.0)
    # silhueta: cluster dissonante crescendo e corte seco
    cut = E['windowCut']
    place(M, strings([hz('D4'), hz('Eb4'), hz('A4'), hz('Bb4')], cut - 27.0, att=5.5, rel=0.05, cutoff=3000, seed=7, trem=0.6, level=0.2), 27.0)
    place(M, harmonics([hz('Eb6'), hz('A6')], cut - 28.5, 3, 0.03), 28.5)

    # ===== 2. TÍTULO =====
    place(D, sub_boom(4.0, 50, 26, 1) * 0.8, E['titleIn'])
    motif_box(S['title'] + 2.0, tempo=1.2, slowdown=0.2, detune=12, seed=20, gain=0.45)
    place(D, drone(hz('D2'), 7.0, 4, 0.08, 300), S['title'] + 0.8)

    # ===== 3. APARTAMENTO (melancólico e inquietante) =====
    a0 = S['apartment']
    prog = [['D3', 'F3', 'A3', 'D4'], ['Bb2', 'D3', 'F3', 'A3'], ['G2', 'Bb2', 'D3', 'F3'], ['A2', 'E3', 'A3', 'C#4']]
    for i, ch in enumerate(prog):
        place(M, strings([hz(x) for x in ch], 7.5, att=2.5, rel=2.5, cutoff=1300, seed=30 + i, level=0.09), a0 + 2.0 + i * 5.6)
    place(M, pan(piano(hz('A4'), 5, 0.3, 40), 0.2), a0 + 0.8)
    place(M, pan(piano(hz('D4'), 5, 0.25, 41), -0.2), a0 + 3.0)
    motif_piano(a0 + 16.8, 0, 0.32, 1.35, seed=45)

    # ===== 4. ESTRADA E CARRO =====
    c0 = S['car_exterior']
    place(D, drone(hz('D2'), 26.0, 6, 0.12, 420), c0)
    place(D, heartbeat(10.5, 70, 2) * 0.35, c0 + 0.5)
    place(M, strings([hz('D4'), hz('A4')], 9.0, att=2, rel=1.5, cutoff=1800, seed=50, level=0.07), c0)
    so = E['radioStaticOn']; sx = E['radioStaticOff']
    place(M, harmonics([hz('C#6'), hz('D6')], sx - so + 1.0, 8, 0.03), so)
    place(M, strings([hz('D3'), hz('Eb3'), hz('Ab3')], 6.0, att=0.3, rel=2.5, cutoff=1600, seed=55, trem=0.4, level=0.12), sx)
    place(D, sub_boom(3.0, 45, 30, 3) * 0.5, sx)

    # ===== 5. CHEGADA: a música some com a chuva =====
    ar = S['arrival']
    place(D, drone(hz('A1'), E['rainStop'] - ar + 1.5, 7, 0.12, 350) * np.clip(np.linspace(1.3, 0, secs(E['rainStop'] - ar + 1.5)), 0, 1), ar)
    place(M, harmonics([hz('A5'), hz('Bb5')], 10.0, 9, 0.015), E['carDoorClose'] + 1.0)

    # ===== 6. A PORTA E O CORREDOR (graves, notas espaçadas) =====
    place(D, drone(hz('D1') * 2, 12.0, 10, 0.09, 260), S['front_door'])
    place(D, sub_boom(4.5, 40, 25, 4) * 0.45, E['doorOpen'] + 0.3)
    h0 = S['hallway']
    for k, (tt, nm) in enumerate([(2.0, 'D2'), (6.5, 'Eb2'), (16.5, 'D2'), (20.5, 'A1')]):
        place(D, pan(piano(hz(nm), 6, 0.45, 60 + k), 0), h0 + tt)
        place(M, pan(piano(hz(nm) * 4 * 1.06, 4, 0.12, 70 + k), 0.5 * (-1) ** k), h0 + tt + 0.02)
    place(M, harmonics([hz('D6'), hz('Eb6')], 10.0, 11, 0.012), h0 + 1.0)

    # ===== 7. AS FOTOGRAFIAS (misteriosa e emocional) =====
    p0 = E['photoFound']
    place(M, strings([hz('D3'), hz('A3'), hz('F4')], 6.0, att=2.5, rel=2, cutoff=1500, seed=80, level=0.1), p0 - 0.5)
    place(M, strings([hz('Bb2'), hz('F3'), hz('D4')], 6.0, att=2, rel=2, cutoff=1500, seed=81, level=0.1), p0 + 4.8)
    place(M, strings([hz('G2'), hz('D3'), hz('Bb3'), hz('A4')], 7.0, att=2, rel=3, cutoff=1500, seed=82, level=0.1), p0 + 9.8)
    motif_piano(p0 + 0.6, 0, 0.42, 1.3, seed=85)
    motif_piano(p0 + 9.0, -1, 0.35, 1.5, seed=88)
    place(M, choir([hz('D4'), hz('A4')], 8.0, 'u', att=3, seed=89, level=0.06), p0 + 5.0)

    # ===== 8. 23h: badaladas com graves =====
    for c in E['clockChimes']:
        place(D, sub_boom(1.6, 42, 36, int(c * 10)) * 0.18, c)

    # ===== 9. ESCADA: trêmulo crescente =====
    s0 = S['stairs']
    place(D, sub_boom(3.0, 50, 28, 9) * 0.6, E['thudUpstairs'])
    place(M, strings([hz('D3'), hz('Eb3'), hz('A3')], 12.5, att=6, rel=1.5, cutoff=1800, seed=90, trem=0.8, level=0.13), s0 + 0.6)
    for k in range(int(11.5 / 0.6)):
        place(D, pan(cello(hz(['D2', 'Eb2'][k % 2]), 0.55, seed=100 + k, level=0.12 + 0.01 * k, pizz=True), -0.3), s0 + 1.5 + k * 0.6)

    # ===== 10. QUARTO INFANTIL (delicada e perturbadora) =====
    q0 = S['child_room']
    motif_box(q0 + 2.5, tempo=1.25, slowdown=0.0, detune=8, seed=110, gain=0.55)
    motif_box(q0 + 15.5, tempo=1.35, slowdown=0.35, detune=22, seed=120, gain=0.4)
    place(M, strings([hz('D4'), hz('F4'), hz('A4')], 26.0, att=6, rel=3, cutoff=1100, seed=125, level=0.06), q0 + 4.0)
    place(D, drone(hz('D2'), 28.0, 12, 0.06, 300), q0 + 2.0)

    # ===== 11. BATIDAS: silêncio; depois um grave volta =====
    k0 = S['knocks']
    place(D, drone(hz('Eb2'), 5.5, 13, 0.08, 280), k0 + 7.5)

    # ===== 12. PORTA DO PORÃO (suspense intenso e crescente) =====
    b0 = S['basement_door']
    place(D, drone(hz('D2'), 34.0, 14, 0.13, 420), b0)
    tempo_b = 0.3
    for k in range(int(11.5 / tempo_b)):
        tt = b0 + 0.3 + k * tempo_b
        place(D, pan(cello(hz(['D2', 'D2', 'Eb2', 'D2'][k % 4]), 0.28, seed=200 + k, level=0.08 + 0.006 * k, pizz=True), 0.2), tt)
    place(M, strings([hz('D4'), hz('Eb4'), hz('A4')], 6.0, att=4, rel=0.4, cutoff=2600, seed=210, trem=0.8, level=0.12), b0 + 4.0)
    place(D, sub_boom(3.0, 40, 28, 11) * 0.6, E['knobTurn'] + 0.1)
    place(M, harmonics([hz('D6'), hz('Eb6'), hz('A6')], 14.0, 15, 0.016), b0 + 11.0)
    place(D, heartbeat(9.0, 78, 4) * 0.45, b0 + 18.0)
    place(M, strings([hz('D3'), hz('Eb3'), hz('G#3'), hz('A3')], 7.0, att=3.5, rel=0.3, cutoff=2400, seed=220, trem=1.0, level=0.15), b0 + 21.0)
    place(D, sub_boom(4.5, 45, 22, 12) * 0.8, E['flicker'])
    place(M, choir([hz('D3'), hz('Ab3')], 7.0, 'o', att=2.0, seed=230, level=0.07), E['keyholeBreath'] - 0.5)

    # ===== 13. TELEFONEMA =====
    r0 = S['run_phone']
    for k, tt in enumerate([0.0, 0.8, 1.6, 2.4]):
        place(D, timpani(hz('D2'), 300 + k, 0.45), r0 + tt)
        place(M, strings([hz('D3'), hz('A3'), hz('D4')], 0.7, att=0.02, rel=0.4, cutoff=3000, seed=310 + k, level=0.14), r0 + tt)
    place(M, strings([hz('D3'), hz('A3')], 20.0, att=5, rel=2, cutoff=900, seed=320, level=0.06), r0 + 6.5)
    place(D, heartbeat(14.0, 66, 6) * 0.35, r0 + 12.0)
    place(M, riser(4.5, 7) * 0.5, E['turnToStairs'])
    place(M, strings([hz('D3'), hz('Eb3'), hz('A3'), hz('Bb3')], 5.0, att=4.0, rel=0.2, cutoff=2800, seed=330, trem=0.7, level=0.14), E['turnToStairs'])

    # ===== 14. O DUPLO (clímax cinematográfico) =====
    d0 = S['double_reveal']
    place(D, sub_boom(5.0, 38, 22, 13) * 0.9, d0 + 0.05)
    place(M, strings([hz('D2'), hz('Eb2'), hz('A2')], 9.0, att=1.0, rel=2.0, cutoff=900, seed=340, level=0.14), d0)
    place(D, timpani(hz('A1'), 341, 0.7), d0 + 4.6)
    place(M, harmonics([hz('Eb7'), hz('D7')], 4.0, 16, 0.02), E['headTilt'])
    sm = E['smile']
    for k in range(6):
        place(D, timpani(hz('D2') * (1 if k % 2 == 0 else 0.75), 350 + k, 0.7 + 0.05 * k), sm + k * 0.85)
    place(M, brass([hz('D3'), hz('Eb3'), hz('A3'), hz('Bb3')], 6.0, att=1.2, seed=360, level=0.22, growl=0.6), sm - 0.3)
    place(M, choir([hz('D4'), hz('Eb4'), hz('A4')], 6.0, 'a', att=1.0, seed=361, level=0.1), sm)
    place(M, strings([hz('D5'), hz('Eb5'), hz('A5'), hz('Bb5')], 6.0, att=1.5, rel=1.0, cutoff=4000, seed=362, trem=1.0, level=0.16), sm)

    # ===== 15. A DECISÃO: pulso que cresce e para de repente =====
    z0 = S['decision']; fz = E['freeze']
    k = 0; tt = z0 + 0.2
    while tt < fz - 0.1:
        place(D, timpani(hz('D2'), 400 + k, 0.35 + 0.3 * (tt - z0) / (fz - z0)), tt)
        if k % 2 == 0: place(D, pan(cello(hz(['D2', 'Eb2', 'D2', 'A1'][(k // 2) % 4]), 0.5, seed=420 + k, level=0.2, pizz=True), 0), tt)
        k += 1; tt += 0.75 - 0.25 * (tt - z0) / (fz - z0)
    lst = fz - z0 - 0.2
    place(M, strings([hz('D3'), hz('A3'), hz('D4'), hz('Eb4')], lst, att=3.0, rel=0.01, cutoff=2600, seed=430, trem=0.5, level=0.14), z0 + 0.2)
    place(M, brass([hz('D2'), hz('A2')], lst - 4, att=3.0, seed=431, level=0.12, growl=0.3), z0 + 4.2)
    # corte seco de toda a música no congelamento
    cutn = secs(fz)
    M[:, cutn:secs(E['knocks2'][0]) - 1] = 0; D[:, cutn:secs(E['knocks2'][0]) - 1] = 0

    # ===== 16. CARTELA E FIM =====
    c0 = S['choice_card']
    place(D, drone(hz('D2'), 17.0, 17, 0.05, 220), c0 + 0.5)
    place(M, harmonics([hz('D5'), hz('A5')], 16.0, 18, 0.012), c0 + 1.0)
    e0 = S['end_card']
    place(D, drone(hz('D2'), E['finalSound'] - e0 + 0.2, 19, 0.05, 220), e0)
    return M, D
