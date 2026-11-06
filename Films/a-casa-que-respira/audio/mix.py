"""Mixagem final de A CASA QUE RESPIRA — Parte 1.
Camadas: ambiente contínuo, efeitos pontuais, passos (foley), diálogos, sons sobrenaturais e trilha.
Saídas: build/audio/mix.wav (master) e build/audio/stems/*.wav."""
import json
import os
import sys

import numpy as np
import soundfile as sf

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp import *  # noqa
import sfx as X
import music

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUILD = os.path.join(ROOT, 'build')
OUT = os.path.join(BUILD, 'audio')


def main():
    tl = json.load(open(os.path.join(BUILD, 'timeline.json')))
    vinfo = json.load(open(os.path.join(BUILD, 'voices.json')))
    S = {s['id']: s['start'] for s in tl['shots']}
    END = {s['id']: s['end'] for s in tl['shots']}
    E = tl['events']
    total = tl['total']
    N = secs(total)
    os.makedirs(os.path.join(OUT, 'stems'), exist_ok=True)

    bus = {k: np.zeros((2, N + secs(6)), np.float32) for k in ['amb', 'ext', 'int', 'car', 'dia', 'super', 'post']}
    P = lambda b, x, t, g=1.0, p=None: place(bus[b], pan(x, p) if (p is not None and np.ndim(x) == 1) else x, t, g)

    print('ambientes...')
    # ---------------- AMBIENTES ----------------
    # 1) exterior com chuva forte: abertura
    d = E['windowCut'] + 0.05
    P('amb', fade(X.rain(d, 1.0, 'exterior', 1), 2.5, 0.01), 0, 0.28)
    P('amb', fade(X.wind(d, 0.5, 2, trees=1.0), 3.0, 0.01), 0, 0.3)
    P('amb', fade(X.electric_flicker(6, 3) * 0.02, 0.5, 0.5), 3.0, 1.0, 0.5)
    # 2) apartamento: chuva no vidro, cidade abafada, ventoinha do notebook
    a0, a1 = S['apartment'], END['apartment']
    P('amb', fade(X.rain(a1 - a0, 0.8, 'window', 3), 0.4, 0.8), a0, 0.9)
    P('amb', fade(lp(X.rain(a1 - a0, 0.8, 'distant', 4), 900), 0.4, 0.8), a0, 0.7)
    P('amb', fade(X.room_tone(a1 - a0, 5, 0.3) + stereo(lp(brown(secs(a1 - a0), 6), 90) * 0.05), 0.4, 0.8), a0)
    P('amb', fade(bp(white(secs(a1 - a0), 7), 3000, 9000) * 0.004, 0.5, 0.8), a0, 1.0, -0.1)
    # 3) estrada: carro por fora
    c0, c1 = S['car_exterior'], END['car_exterior']
    P('amb', fade(X.rain(c1 - c0, 1.1, 'exterior', 8), 0.05, 0.02), c0, 0.3)
    P('amb', fade(X.wind(c1 - c0, 0.5, 9), 0.05, 0.02), c0, 0.25)
    tt = t_axis(secs(c1 - c0))
    passby = np.exp(-((tt - 4.6) / 1.3) ** 2) * 0.9 + np.exp(-((tt - 1.0) / 2.2) ** 2) * 0.35
    rpm = 1900 * (1 + 0.04 * np.tanh((4.6 - tt) * 2))  # efeito Doppler
    eng = X.engine(c1 - c0, rpm, 0.7, 10, interior=False) * passby
    tires = X.tires_wet(c1 - c0, 1.0, 11) * passby
    P('ext', pan(eng, np.clip((tt - 4.6) / 1.5, -0.8, 0.8)), c0, 0.45)
    P('ext', tires, c0, 0.45)
    for k in range(int((c1 - c0) / 0.625)):
        P('ext', X.wiper_cycle(k, interior=False) * 0.15 * passby[min(len(passby) - 1, secs(k * 0.625))], c0 + k * 0.625 - (c0 % 0.625))
    # 4) dentro do carro
    i0, i1 = S['car_interior'], END['car_interior']
    P('car', fade(X.rain(i1 - i0, 1.0, 'roof', 12), 0.03, 0.03), i0, 0.22)
    P('car', fade(lp(X.rain(i1 - i0, 1.0, 'exterior', 13), 1200), 0.03, 0.03), i0, 0.15)
    P('car', fade(stereo(X.engine(i1 - i0, 1700, 0.5, 14)), 0.03, 0.03), i0, 0.22)
    P('car', fade(lp(X.tires_wet(i1 - i0, 1.0, 15), 1500), 0.03, 0.03), i0, 0.2)
    P = P
    k = int(np.ceil(i0 / 0.625))
    while k * 0.625 < i1 - 0.3:
        P('car', X.wiper_cycle(k, True) * 0.22, k * 0.625, 1.0, 0.0); k += 1
    # rádio: canção, depois estática e o sussurro
    so, sx = E['radioStaticOn'], E['radioStaticOff']
    P('car', fade(X.radio_music(so - i0 + 0.3, 16), 0.05, 0.3), i0, 0.25, 0.1)
    st = X.radio_static(sx - so, 17)
    st *= curve(len(st), [(0, 0.0), (0.6, 0.5), (sx - so - 3.5, 0.7), (sx - so - 0.05, 1.0), (sx - so, 0.0)])
    P('car', st, so, 0.3, 0.1)
    # 5) chegada: a chuva para — silêncio absoluto (sem pássaros, sem insetos)
    r0, rs = S['arrival'], E['rainStop']
    rain_arr = X.rain(rs - r0 + 1.5, 1.0, 'exterior', 18)
    rain_arr *= curve(rain_arr.shape[1], [(0, 1), (rs - r0 - 0.4, 1), (rs - r0 + 1.2, 0)])
    P('amb', fade(rain_arr, 0.05, 0.1), r0, 0.3)
    P('amb', 0.3 * fade(X.wind(rs - r0 + 1.5, 0.4, 19) * curve(secs(rs - r0 + 1.5), [(0, 1), (rs - r0, 1), (rs - r0 + 1.2, 0.08)]), 0.05, 0.1), r0)
    P('amb', fade(lp(X.wind(END['front_door'] - rs, 0.22, 20), 500), 1.5, 0.3), rs + 0.5)
    stop, eoff = E['carStop'], r0 + 8.3
    ta = t_axis(secs(eoff - r0))
    rpm_a = np.where(ta < stop - r0, 1500 - 600 * ta / (stop - r0), 850)
    eng = X.engine(eoff - r0, rpm_a, 0.4, 21, interior=False)
    eng[-secs(0.7):] *= np.linspace(1, 0, secs(0.7)) ** 2
    P('ext', eng * 0.8, r0, 1.0, 0.2)
    grav = bp(white(secs(stop - r0), 22), 800, 6000) * (0.3 + 0.3 * np.abs(np.sin(t_axis(secs(stop - r0)) * 23))) * np.linspace(1, 0.2, secs(stop - r0))
    P('ext', grav * 0.25, r0, 1.0, 0.2)
    P('ext', X.click(23, (1500, 4000), 0.5), eoff, 1.0, 0.1)
    P('ext', X.car_door('open', 24) * 0.7, E['carDoorOpen'], 1.0, 0.25)
    P('ext', X.car_door('close', 25) * 0.9, E['carDoorClose'], 1.0, 0.2)
    # 6) interior da casa: tom de sala, vento contra as paredes, rangidos esparsos
    h0, h1 = S['front_door'] + 6.0, E['freeze']
    P('amb', fade(X.room_tone(h1 - h0, 26, 0.18), 2.0, 0.03), h0)
    P('amb', fade(lp(X.wind(h1 - h0, 0.22, 27, trees=0.1), 380), 2.0, 0.03), h0, 0.35)
    rr = rng(28)
    quiet = [(E['breathOff'], E['resumeWalk']), (E['knocks1'][0] - 0.5, S['knocks'] + 11), (E['phoneRing'] - 0.5, E['phoneAnswer2'] + 1)]
    tt = h0 + 3
    while tt < h1 - 2:
        if not any(a <= tt <= b for a, b in quiet):
            P('int', X.house_creak(int(tt * 7), rr.uniform(0.15, 0.4)), tt, 1.0, rr.uniform(-0.9, 0.9))
        tt += rr.uniform(4, 10)

    print('trovões e chuva...')
    # ---------------- TROVÕES (relâmpagos da imagem) ----------------
    th = {0: (2.0, 0.85), 1: (1.2, 0.6), 2: (0.25, 0.15), 3: (0.6, 0.35), 4: (0.4, 0.3)}
    for i, lt in enumerate(E['lightning']):
        delay, dist = th[i]
        x = X.thunder(dist, 30 + i, 9)
        if i == 4: x = lp(x, 700) * 0.8  # abafado dentro do carro
        if lt + delay < E['windowCut'] or i >= 3:
            P('amb' if i < 3 else ('ext' if i == 3 else 'car'), x, lt + delay, 0.45 if i < 3 else 0.6)
    # corta o trovão da janela no corte seco
    print('efeitos...')
    # ---------------- EFEITOS PONTUAIS ----------------
    for b in E['phoneBuzz']:
        P('int2' if False else 'ext', X.phone_vibrate(1.0, int(b * 10)) * 0.9, b, 1.0, 0.25)
    P('ext', X.paper_rustle(0.6, 40, 0.25), E['phoneAnswer'] - 0.2, 1.0, 0.1)
    P('ext', X.click(41, (1000, 4000), 0.25), E['phoneAnswer'] + 0.6, 1.0, 0.2)
    # porta da frente
    P('ext', X.keys_jingle(42) * 0.5, E['keyInsert'] - 1.1, 1.0, 0.15)
    P('ext', X.key_insert(43) * 0.4, E['keyInsert'] - 0.3, 1.0, 0.1)
    P('ext', X.lock_turn(44) * 0.45, E['keyTurn'], 1.0, 0.1)
    creak = X.door_creak_long(4.4, 45)
    P('int', creak * 0.5, E['doorOpen'], 1.0, -0.1)
    P('int', stereo(lp(brown(secs(4), 46), 120)) * env_adsr(secs(4), 1.5, 0.5, 0.6, 1.5) * 0.4, E['doorOpen'] + 0.3)
    # lanterna
    P('int', X.flashlight_click(47), E['flashlightOn'], 1.0, 0.2)
    # a casa respira
    b0, b1 = E['breathOn'][0], E['breathOff']
    br = X.house_breathing(b1 - b0, 4.2, 48, t0=b0)
    br = fade(br, 1.2, 0.02)
    P('super', pan(br, 0.0), b0, 0.35)
    b2 = E['breathOn'][1]; b3 = S['album'] + 1.5
    br2 = fade(X.house_breathing(b3 - b2, 4.2, 49, t0=b2), 0.6, 2.0)
    P('super', pan(br2, 0.0), b2, 0.35)
    # álbum, fotografia
    P('int', X.book_open(50) * 0.7, E['albumOpen'] - 0.2, 1.0, 0.0)
    for i, pf in enumerate(E['pageFlips']):
        P('int', X.page_flip(51 + i) * 0.6, pf - 0.05, 1.0, -0.1 + 0.05 * i)
    P('int', X.paper_rustle(1.0, 56, 0.3), E['photoFound'] + 0.1, 1.0, 0.05)
    P('int', X.paper_rustle(0.5, 57, 0.35), E['photoTurn'], 1.0, 0.05)
    # relógio
    cs = E['clockStart']
    P('int', X.clock_whirr(58) * 0.5, cs - 0.4, 1.0, 0.0)
    P('int', X.click(59, (300, 2000), 0.8), cs + 0.75, 1.0, 0.0)
    tk = cs + 1.0; k = 0
    while tk < S['stairs'] + 4.0:
        g = 0.5 if tk < S['stairs'] else 0.5 * (1 - (tk - S['stairs']) / 4.0)
        P('int', X.clock_tick(k % 2 == 1, 60 + k) * g, tk, 1.0, 0.0); tk += 1.0; k += 1
    for i, c in enumerate(E['clockChimes']):
        P('int', X.chime(146.83, 70 + i, 5.0) * 0.45, c, 1.0, 0.0)
    # escada: pancada no andar de cima
    P('int', X.heavy_thud(80) * 0.9, E['thudUpstairs'], 1.0, -0.2)
    for k in range(6):
        P('int', X.metal_clink(81 + k, 2400 + 300 * k, 0.25) * 0.05 * (1 - k / 6), E['thudUpstairs'] + 0.4 + k * 0.35, 1.0, 0.0)
    # quarto
    P('int', X.door_creak_long(1.6, 82) * 0.6, S['child_room'] + 0.4, 1.0, 0.3)
    P('int', X.box_open(83) * 0.7, E['boxOpen'] - 0.1, 1.0, -0.1)
    P('int', X.paper_rustle(1.4, 84, 0.35), E['boxOpen'] + 1.2, 1.0, -0.05)
    for k, tt in enumerate([S['child_room'] + 16.4, S['child_room'] + 24.2]):
        P('int', X.paper_rustle(0.4, 85 + k, 0.2), tt, 1.0, 0.0)
    # batidas sob o assoalho
    for i, kt in enumerate(E['knocks1']):
        P('super', X.knock_floor(90 + i) * 1.0, kt, 1.0, 0.0)
        P('int', X.metal_clink(95 + i, 1800, 0.12) * 0.04, kt + 0.02, 1.0, -0.3)
    P('int', X.paper_rustle(0.8, 97, 0.15), S['knocks'] + 8.0, 1.0, 0.0)
    P('int', X.house_creak(98, 0.35), S['knocks'] + 8.3, 1.0, 0.0)
    for k in range(3):  # cavalo de balanço sozinho
        P('int', X.friction_creak(0.7, (30, 18), (380, 900, 1700), 14, 99 + k) * 0.22, S['knocks'] + 10.6 + k * 1.43, 1.0, 0.6)
    # porta do porão
    P('int', X.knob_turn(2.2, 100) * 0.8, E['knobTurn'], 1.0, 0.05)
    P('int', X.knob_turn(1.6, 101) * 0.6, E['knobTurn'] + 3.6, 1.0, 0.05)
    P('int', X.electric_flicker(31 - 27.8, 102) * 0.35, E['flicker'], 1.0, 0.2)
    kb = E['keyholeBreath']; per = 3.2; kk = 0
    while kb + kk * per < END['basement_door'] - 0.4:
        bi = X.breath(True, per * 0.45, 110 + kk, size=1.5, wet=True)
        be = X.breath(False, per * 0.5, 120 + kk, size=1.6, wet=True)
        P('super', lp(bi, 1600) * 0.4, kb + kk * per, 1.0, 0.0)
        P('super', lp(be, 1300) * 0.45, kb + kk * per + per * 0.47, 1.0, 0.0)
        kk += 1
    # corrida e telefonema
    P('dia', X.panting(E['phoneAnswer2'] + 4 - S['run_phone'], 1.7, 130) * 0.18, S['run_phone'] + 0.3, 1.0, 0.0)
    rg = E['phoneRing']; ra = E['phoneAnswer2']
    P('ext', X.ringtone(ra - rg, 131) * 0.6 + X.phone_vibrate(ra - rg, 132, False) * 0.25, rg, 1.0, 0.1)
    P('ext', X.click(133, (1000, 4000), 0.4), ra, 1.0, 0.1)
    it0 = E['turnToStairs'] - 1.0
    gsm = X.gsm_interference(S['double_reveal'] + 7.5 - it0, 134, 1.0)
    gsm *= curve(len(gsm), [(0, 0), (2.0, 0.15), (5.0, 0.4), (7.0, 0.9), (9.5, 1.0), (11.0, 0.5), (len(gsm) / SR, 0.0)])
    P('super', gsm * 0.35, it0, 1.0, 0.1)
    # o duplo
    P('super', X.bone_crack(140) * 0.35, E['headTilt'] + 0.6, 1.0, -0.2)
    P('super', lp(X.breath(True, 2.0, 141, size=1.4), 1200) * 0.5, E['headTilt'] - 1.5, 1.0, -0.3)
    P('super', lp(X.breath(False, 2.5, 142, size=1.5), 1000) * 0.5, E['smile'] - 0.6, 1.0, -0.3)
    # Gabriel respira trêmulo durante a decisão
    P('dia', X.panting(E['freeze'] - S['decision'], 0.9, 150) * 0.08, S['decision'], 1.0, 0.0)

    print('passos...')
    # ---------------- PASSOS (da coreografia) ----------------
    for i, s in enumerate(tl['steps']):
        surf = s['surface']
        if surf == 'stairs' and s['walk'] == 'descend': surf = 'stairs'
        x = X.footstep(surf if surf != 'porch' else 'porch', 500 + i, 0.55 if s['careful'] else 0.8, s['run'])
        b = 'ext' if surf in ('gravel', 'porch') else 'int'
        g = 1.0
        if s['walk'] == 'descend': g = 1.25; x = lp(x, 2500)
        if s['t'] >= E['freeze']: continue
        P(b, x, s['t'], g, (-0.08 if s['foot'] else 0.08))

    print('vozes...')
    vox = np.zeros_like(bus['dia'])
    # ---------------- DIÁLOGOS E VOZES ----------------
    for line in tl['lines']:
        v, sr = sf.read(os.path.join(BUILD, 'voices', line['id'] + '.wav'), dtype='float32')
        if line['pitch']:
            v = pitch_shift(v, sr, line['pitch'], formant=line['fx'] not in ('double_whisper',))
        v = resample(v, sr)
        v = v / (np.sqrt(np.mean(v ** 2)) + 1e-9) * 0.08  # RMS ~ -22 dBFS
        out, target, gain = voice_fx(v, line, sr)
        P(target, out, line['t'] - 0.0, gain)
        place(vox, out, line['t'], gain)

    print('trilha...')
    M, D = music.score(tl, total)
    irM = make_ir(2.8, 7000, 0.03, 10, 501, 2.0)
    music_bus = (reverb(M, irM, wet=0.45, dry=0.8) + D * 0.9) * 0.55
    music_bus = music_bus[:, :bus['amb'].shape[1]] if music_bus.shape[1] >= bus['amb'].shape[1] else np.pad(music_bus, ((0, 0), (0, bus['amb'].shape[1] - music_bus.shape[1])))

    print('cartelas finais...')
    # ---------------- PÓS (tela preta, cartelas, fim) ----------------
    for i, kt in enumerate(E['knocks2']):
        P('post', X.knock_floor(600 + i) * 1.1, kt, 1.0, 0.0)
    db = E['deepBreath']
    P('post', lp(X.breath(True, 2.2, 610, size=2.4), 1300) * 0.8, db, 1.0, 0.0)
    P('post', lp(X.breath(False, 2.6, 611, size=2.6), 1100) * 0.8, db + 2.1, 1.0, 0.0)
    # título: uma respiração enorme
    ti = E['titleIn']
    P('post', lp(X.breath(True, 2.4, 620, size=3.0), 900) * 0.9, ti - 0.6, 1.0, 0.0)
    P('post', lp(X.breath(False, 3.2, 621, size=3.2), 800) * 0.9, ti + 1.9, 1.0, 0.0)
    # som final misterioso: sino ao contrário que suga para um golpe grave, e silêncio
    fs = E['finalSound']
    rev = X.reverse(X.chime(110.0, 630, 3.0)) * 1.2
    P('post', rev, fs - 3.0, 1.0, 0.0)
    rb = X.reverse(X.breath(False, 2.5, 631, size=2.0)) * 0.4
    P('post', lp(rb, 1500), fs - 2.5, 1.0, 0.0)
    P('post', X.sub_boom(2.5, 46, 24, 632) * 0.9, fs, 1.0, 0.0)
    P('post', X.knock_floor(633) * 0.6, fs, 1.0, 0.0)

    print('reverbs e soma...')
    # ---------------- PROCESSAMENTO DE BARRAMENTOS ----------------
    ir_house = make_ir(1.7, 5500, 0.02, 12, 701, 1.4, 0.8)
    ir_ext = make_ir(0.9, 6000, 0.01, 6, 702, 0.8)
    ir_car = make_ir(0.25, 5000, 0.002, 6, 703, 0.2)
    ir_under = make_ir(3.2, 2200, 0.03, 8, 704, 2.0, 0.5)
    intb = reverb(bus['int'], ir_house, wet=0.32, dry=1.0)
    extb = reverb(bus['ext'], ir_ext, wet=0.15, dry=1.0)
    carb = reverb(bus['car'], ir_car, wet=0.2, dry=1.0)
    sup = reverb(bus['super'], ir_under, wet=0.5, dry=0.9)
    post = reverb(bus['post'], ir_under, wet=0.55, dry=0.9)
    amb = bus['amb']
    dia = bus['dia']

    # ducking: trilha e ambiente abaixam sob os diálogos (a música nunca encobre as falas)
    env = lp(np.abs(vox).max(axis=0), 4.0)
    duck = np.clip(env / 0.03, 0, 1)
    music_bus *= (1 - 0.65 * duck)
    amb *= (1 - 0.4 * duck)
    carb *= (1 - 0.5 * duck)
    extb *= (1 - 0.3 * duck)

    # portas de silêncio: corte seco na janela, congelamento final
    n_all = amb.shape[1]
    gate = np.ones(n_all, np.float32)
    def mute(a, b, fi=0.004, fo=0.25):
        sa, sb = secs(a), secs(b); gate[sa:sb] = 0
        f = secs(fi); gate[max(0, sa - f):sa] = np.minimum(gate[max(0, sa - f):sa], np.linspace(1, 0, sa - max(0, sa - f)))
        g2 = secs(fo); gate[sb:sb + g2] = np.minimum(gate[sb:sb + g2], np.linspace(0, 1, len(gate[sb:sb + g2])))
    mute(E['windowCut'], S['apartment'], 0.004, 0.25)
    mute(E['freeze'], n_all / SR, 0.008, 0.0)
    # música: a cartela e o fim têm música própria após o congelamento
    mgate = gate.copy(); mgate[secs(S['title']):secs(S['apartment'])] = 1.0; mgate[secs(S['choice_card']):] = 1.0
    mgate[secs(E['freeze']):secs(S['choice_card'])] = 0.0
    fsn = secs(E['finalSound'] + 0.05); mgate[fsn:] = 0.0

    stems = {
        'ambiente': amb * gate, 'efeitos': (intb + extb + carb) * gate, 'sobrenatural': sup * gate,
        'dialogos': dia * gate, 'trilha': music_bus * mgate, 'cartelas': post,
    }
    # o diálogo D2 ("Agora podemos começar") está na tela preta (pós congelamento): vai pelo barramento pós
    mix = sum(stems.values())
    mix = mix[:, :N + secs(0.5)]
    mix = hp(mix, 25)
    mix = compress(mix, -20, 2.0, 0.01, 0.2, 2.0)
    # loudness integrada alvo -16 LUFS (cinema/streaming com dinâmica)
    import pyloudnorm as pyln
    meter = pyln.Meter(SR)
    L = meter.integrated_loudness(mix.T)
    mix = mix * 10 ** ((-16.0 - L) / 20)
    mix = limiter(mix, -1.0)
    print(f'loudness original {L:.1f} LUFS -> -16; pico {20*np.log10(np.max(np.abs(mix))):.2f} dBFS')
    mix = mix[:, :N]
    sf.write(os.path.join(OUT, 'mix.wav'), mix.T, SR, subtype='PCM_24')
    for k, v in stems.items():
        sf.write(os.path.join(OUT, 'stems', k + '.wav'), (v[:, :N] * 10 ** ((-16.0 - L) / 20)).T, SR, subtype='PCM_24')
    print('ok', os.path.join(OUT, 'mix.wav'))


def voice_fx(v, line, sr):
    """Cadeias de efeito por tipo de voz. Retorna (estéreo, barramento, ganho)."""
    fx = line['fx']; r = rng(hash(line['id']) % 1000)
    pad_ = lambda x, s=1.5: np.pad(x, (0, secs(s)))
    if fx in ('phone', 'phone_bad', 'phone_scream'):
        x = bp(v, 320, 3400, 3)
        drive = {'phone': 1.8, 'phone_bad': 2.4, 'phone_scream': 5.0}[fx]
        x = softclip(x * drive, 1.5) * 0.5
        if fx != 'phone':
            n = len(x); d = np.ones(n, np.float32)
            for k in range(int(n / SR * (3 if fx == 'phone_bad' else 6))):
                p = r.integers(0, n); m = secs(r.uniform(0.02, 0.09)); d[p:p + m] *= r.uniform(0.0, 0.4)
            x = x * lp(d, 200)
            x = x + bp(white(n, 7), 500, 4000) * 0.01 + X.gsm_interference(n / SR, 8, 0.6 if fx == 'phone_bad' else 1.5)[:n] * (0.04 if fx == 'phone_bad' else 0.1)
        x = compress(x, -24, 3, makeup_db=6)
        x = pad_(x, 0.3)
        g = {'phone': 1.0, 'phone_bad': 2.3, 'phone_scream': 2.2}[fx]
        return reverb(pan(x, 0.12), make_ir(0.3, 4000, 0.002, 4, 801, 0.3), wet=0.1), 'dia', g
    if fx == 'dry_room':
        x = pad_(v, 0.6)
        return reverb(pan(x, 0.0), make_ir(0.4, 7000, 0.003, 6, 802, 0.4), wet=0.12), 'dia', 1.0
    if fx == 'house':
        x = pad_(lp(v, 9000), 2.0)
        return reverb(pan(x, 0.0), make_ir(1.6, 5500, 0.015, 10, 803, 1.3, 0.8), wet=0.2), 'dia', 1.0
    if fx == 'letter':
        x = pad_(hp(lp(v, 8000), 90), 1.5)
        x = softclip(x * 1.3, 1.2) * 0.85
        return reverb(pan(x, 0.0), make_ir(1.2, 6500, 0.01, 8, 804, 0.9), wet=0.18), 'dia', 0.95
    if fx == 'radio_whisper':
        w = whisperize(v, SR)
        x = 0.75 * w + 0.3 * v
        x = bp(x, 400, 3200, 3); x = softclip(x * 2.0, 1.4) * 0.6
        x = pad_(x, 1.0)
        out = reverb(pan(x, 0.1), make_ir(0.6, 4000, 0.004, 6, 805, 0.5), wet=0.25)
        return out, 'super', 1.1
    if fx == 'behind_door':
        w = whisperize(v, SR)
        x = 0.65 * v + 0.45 * w
        x = lp(x, 1400, 2)
        ghost = lp(resample(v, SR, int(SR * 1.0)), 400)
        x = pad_(x, 3.0)
        out = reverb(pan(x, 0.0), make_ir(2.6, 2200, 0.02, 8, 806, 1.5, 0.5), wet=0.5, dry=0.9)
        return out, 'super', 0.7
    if fx == 'double_whisper':
        w = whisperize(v, SR)
        x = 0.85 * w + 0.2 * v
        low = lp(pitch_shift(v, SR, -12, formant=False), 600) * 0.25
        x = x + low[:len(x)]
        x = pad_(x, 2.0)
        p = 0.45 if line['id'] == 'D1' else 0.0
        st = widen(pan(x, p), 0.4)
        wet = 0.35 if line['id'] == 'D1' else 0.12
        return reverb(st, make_ir(1.8, 4500, 0.01, 8, 807, 1.0, 0.6), wet=wet), ('super' if line['id'] == 'D1' else 'post'), 0.9
    x = pad_(v, 1.0)
    return pan(x, 0.0), 'dia', 1.0


if __name__ == '__main__':
    main()
