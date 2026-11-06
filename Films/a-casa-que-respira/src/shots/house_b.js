// Dentro da casa (II): a porta do porão, o telefonema de Augusto, o duplo, a decisão.
import * as THREE from 'three';
import { EVENTS, shotById, LINES } from '../timeline.js';
import { makeCamera, placeCamera, camPath } from '../engine/scene.js';
import { kf, smooth, ease, noise1, clamp, lerp, pulse } from '../lib/util.js';
import { interiorWorld, updateInterior, coneGuard } from './interior_world.js';
import { cut, lips, applyWalk, holdFlashlight, fearFace, phoneToEar, phoneLook, phoneScreen, speakingNow } from './common.js';
import { walkById, walkAt } from '../choreo.js';
import { UP } from '../sets/interior.js';

const L = (id, ev) => ev - shotById[id].start;
const V = (x, y, z) => new THREE.Vector3(x, y, z);
const headPos = (h) => { const p = new THREE.Vector3(); h.j.head.getWorldPosition(p); return p.add(V(0, 0.08, 0)); };

// intensidade de uma voz (para fazer a luz sob a porta pulsar com a fala)
function voiceEnv(ctx, who, t) {
  let v = 0;
  for (const l of LINES) {
    if (l.who !== who) continue; const d = ctx.voices[l.id]?.dur ?? 2;
    if (t < l.t || t > l.t + d) continue;
    const data = ctx.lipsync[l.id]; const i = Math.floor((t - l.t) * 24);
    if (data && data[i]) v = Math.max(v, data[i][0]);
  }
  return v;
}

const KNOB = V(0.36, 1.06, -11.18), KEYHOLE = V(0.36, 0.94, -11.19);
const STAIR_TOP = V(-3.4, UP, -6.75);

function basementState(W, t, ctx, { ajar = 0, glow = 0 } = {}) {
  const ud = W.ud;
  const ve = voiceEnv(ctx, 'ela', t);
  const leak = 0.12 + 0.6 * ve + glow;
  ud.leakMat.color.setRGB(0.35 * leak, 0.42 * leak, 0.3 * leak);
  ud.leakFloor.material.color.setRGB(0.12 * leak, 0.15 * leak, 0.1 * leak);
  ud.bDoor.rotation.y = ajar;
  ud.beyondGlow.material.uniforms.intensity.value = glow * 0.35;
  return ve;
}

// ---------------- PORTA DO PORÃO ----------------
const basementDoor = {
  async create(ctx) {
    const W = interiorWorld(ctx);
    const cam = makeCamera(36);
    return {
      update(lt, t) {
        const g = W.gab; W.dbl.root.visible = false;
        if (ctx.sets.letter) ctx.sets.letter.visible = false;
        W.ud.frontDoor.rotation.y = 0.0;
        const id = lt < 3.6 ? 'toBasement' : lt < 10.2 ? 'approach' : 'back';
        const s = applyWalk(g, id, t, { fear: 0.75 });
        if (!s.moving) g.root.rotation.y = Math.PI;
        const ve = basementState(W, t, ctx);
        // maçaneta gira sozinha
        const kt = L('basement_door', EVENTS.knobTurn);
        W.ud.knob.rotation.z = -kf(lt, [[kt, 0], [kt + 2.2, 0.85], [kt + 3.6, 0.85], [kt + 5.2, 0]], ease.inOut);
        // lanterna: falha no fim
        const fl = L('basement_door', EVENTS.flicker);
        let flashOn = 1;
        if (lt > fl) { const n = noise1(lt * 18, 7); flashOn = n > 0.1 ? 0.15 + 0.85 * (lt < 31 ? Math.abs(noise1(lt * 31, 2)) : 0.3) : 0.0; }
        const aim = lt < 2 ? V(0.2, 1.2, -8.5).lerp(V(0.2, 1.1, -11.2), smooth(0.8, 2.0, lt)) : KNOB.clone().add(V(0, -0.1, 0));
        holdFlashlight(g, aim, {}); W.flashAim = aim;
        g.look(lt > 7.4 && lt < 11 ? KNOB : V(0.1, 1.4, -11.2), 1);
        const lp = lips(ctx, 'gabriel', t, g);
        const fear = 0.6 + 0.4 * smooth(10, 12, lt);
        g.updateFace(t, { ...fearFace(fear), jaw: lp.jaw, round: lp.round, blink: lt > 12 && lt < 19 ? 0 : null });
        updateInterior(W, t, { flashOn, coneInt: 0.06 });
        // buraco da fechadura: um brilho fraco que se apaga e volta — algo respira do outro lado
        const kb = L('basement_door', EVENTS.keyholeBreath);
        const breath = lt > kb ? 0.5 + 0.5 * Math.sin((lt - kb) * Math.PI * 2 / 3.2) : 1;
        const kg = 0.01 + 0.05 * breath * smooth(kb - 1, kb, lt);
        W.ud.keyholeMat.color.setRGB(kg * 0.7, kg * 0.85, kg * 0.6);
        const C = cut(lt, [[0, 'behind'], [3.6, 'doorPOV'], [7.4, 'knob'], [10.2, 'mcu'], [15.5, 'door'], [20.2, 'cu'], [23.2, 'wide'], [25.4, 'door2'], [28.0, 'flicker'], [31.0, 'keyhole']]);
        const hp = headPos(g);
        let ap = 0, focus = 2;
        if (C.id === 'behind') camPath(cam, C.lt, [[0, [0.75, 1.62, -5.0], [0.0, 1.25, -11.0]], [3.6, [0.6, 1.6, -6.2], [0.0, 1.2, -11.0]]], { shake: 0.01, fov: 38 });
        else if (C.id === 'doorPOV') { placeCamera(cam, [0.15, 1.45, -11.0], hp.toArray(), { shake: 0.006, t, fov: 40 }); ap = 1; focus = cam.position.distanceTo(hp); }
        else if (C.id === 'knob') { camPath(cam, C.lt, [[0, [0.05, 1.16, -10.75], KNOB.toArray()], [2.8, [0.1, 1.14, -10.85], KNOB.toArray()]], { shake: 0.002, fov: 34 }); ap = 6; focus = cam.position.distanceTo(KNOB); }
        else if (C.id === 'mcu') { placeCamera(cam, hp.clone().add(V(-0.55, -0.1, -0.75)).toArray(), hp.toArray(), { shake: 0.006, t, fov: 34 }); ap = 4; focus = 0.95; }
        else if (C.id === 'door') { camPath(cam, C.lt, [[0, [-0.4, 0.7, -9.6], [0.0, 0.9, -11.2]], [4.7, [-0.35, 0.65, -10.0], [0.0, 0.8, -11.2]]], { shake: 0.004, fov: 34 }); ap = 1.5; focus = 1.4; }
        else if (C.id === 'cu') { placeCamera(cam, hp.clone().add(V(0.2, -0.04, -0.6)).toArray(), hp.toArray(), { shake: 0.004, t, fov: 32 }); ap = 5; focus = 0.62; }
        else if (C.id === 'wide') camPath(cam, C.lt, [[0, [0.7, 2.2, -8.2], [0.0, 1.0, -10.6]], [2.2, [0.65, 2.15, -8.3], [0.0, 1.0, -10.6]]], { shake: 0.003, fov: 40 });
        else if (C.id === 'door2') { camPath(cam, C.lt, [[0, [0.1, 1.1, -9.9], [0.3, 1.0, -11.2]], [2.6, [0.2, 1.05, -10.4], [0.33, 1.0, -11.2]]], { shake: 0.003, fov: 34 }); ap = 2; focus = 1.0; }
        else if (C.id === 'flicker') { placeCamera(cam, hp.clone().add(V(-0.3, -0.05, -0.55)).toArray(), hp.toArray(), { shake: 0.006, t, fov: 32 }); ap = 5; focus = 0.63; }
        else { camPath(cam, C.lt, [[0, [0.36, 0.95, -10.75], KEYHOLE.toArray()], [3, [0.36, 0.942, -11.13], KEYHOLE.toArray()]], { shake: 0.001, fov: 30 }); ap = 3; focus = cam.position.distanceTo(KEYHOLE); }
        coneGuard(W, cam);
        return { scene: W.scene, camera: cam, aperture: ap, focus, bloom: 0.55, grade: { exposure: 1.9 } };
      },
    };
  },
};

// ---------------- TELEFONEMA ----------------
const runPhone = {
  async create(ctx) {
    const W = interiorWorld(ctx);
    const cam = makeCamera(36);
    return {
      update(lt, t) {
        const g = W.gab; const dbl = W.dbl;
        basementState(W, t, ctx);
        W.ud.knob.rotation.z = 0;
        const s = applyWalk(g, 'run', t, { fear: 0.9 });
        const ring = L('run_phone', EVENTS.phoneRing), ans = L('run_phone', EVENTS.phoneAnswer2), turnT = L('run_phone', EVENTS.turnToStairs);
        // ao final da corrida: parado de frente para a porta; depois vira devagar para a escada
        const turn = kf(lt, [[turnT, 0], [turnT + 3.6, 1]], ease.inOut2);
        if (lt > 3.4) g.root.rotation.y = lerp(0, -2.5, turn);
        g.root.position.x += 0.0;
        let flashOn = 1;
        if (lt < ring + 0.3) {
          const aim = V(s.x, 1.2, s.z).add(V(0, 0, 3)); holdFlashlight(g, aim, {}); W.flashAim = aim; phoneScreen(g, 'off');
        } else if (lt < ans) {
          phoneLook(g); phoneScreen(g, 'unknown', lt); flashOn = 0;
        } else {
          phoneToEar(g, smooth(ans, ans + 0.6, lt)); phoneScreen(g, 'incall', lt - ans); flashOn = 0;
        }
        const top = STAIR_TOP.clone().add(V(0, 1.65, 0));
        const phoneP = new THREE.Vector3(); g.phone.getWorldPosition(phoneP);
        const look = lt < ring + 0.3 ? V(s.x, 1.5, s.z + 3) : lt < ans ? phoneP : V(0.2, 1.55, 2).lerp(top, turn);
        g.look(look, 1, { maxYaw: 1.4 });
        const lp = lips(ctx, 'gabriel', t, g);
        const shock = smooth(9.5, 11, lt);
        g.updateFace(t, { ...fearFace(0.6 + 0.4 * shock), jaw: Math.max(lp.jaw, shock * 0.06 * (1 - lp.jaw)), round: lp.round, blink: lt > 24 && lt < 31 ? 0 : null });
        // o duplo já está lá em cima (só se percebe no fim)
        dbl.root.visible = lt > 29.0;
        poseDouble(dbl, t, { tilt: 0, smile: 0, at: STAIR_TOP });
        updateInterior(W, t, { flashOn, coneInt: 0.05 });
        // luz da tela do celular no rosto
        g.ledMat.color.setScalar(flashOn ? 1 : 0);
        W.bounce.intensity = flashOn ? W.bounce.intensity : (lt > ring ? 0.12 : 0);
        if (!flashOn) { W.bounce.color.set(0x8fb0d0); W.bounce.position.copy(headPos(g)).add(new THREE.Vector3(0, 0, 0.45).applyQuaternion(g.root.quaternion)); } else W.bounce.color.set(0xc8b8a8);
        const C = cut(lt, [[0, 'run'], [3.4, 'screen'], [7.0, 'mcu'], [12.2, 'cu'], [17.0, 'side'], [24.0, 'ecu'], [27.0, 'turn']]);
        const hp = headPos(g);
        let ap = 0, focus = 2;
        if (C.id === 'run') camPath(cam, C.lt, [[0, [0.7, 1.55, -0.35], hp.toArray()], [3.4, [0.75, 1.6, -0.3], hp.toArray()]], { shake: 0.03, fov: 40 });
        else if (C.id === 'screen') { placeCamera(cam, hp.clone().add(V(-0.18, 0.06, -0.2)).toArray(), phoneP.toArray(), { shake: 0.005, t, fov: 36 }); ap = 4; focus = cam.position.distanceTo(phoneP); }
        else if (C.id === 'mcu') { camPath(cam, C.lt, [[0, hp.clone().add(V(0.15, -0.12, 1.1)).toArray(), hp.toArray()], [5.2, hp.clone().add(V(0.12, -0.1, 0.85)).toArray(), hp.toArray()]], { shake: 0.006, fov: 34 }); ap = 3; focus = cam.position.distanceTo(hp); }
        else if (C.id === 'cu') { placeCamera(cam, hp.clone().add(V(-0.1, -0.05, 0.62)).toArray(), hp.toArray(), { shake: 0.004, t, fov: 30 }); ap = 5; focus = 0.62; }
        else if (C.id === 'side') { placeCamera(cam, hp.clone().add(V(0.75, -0.06, 0.35)).toArray(), hp.toArray(), { shake: 0.005, t, fov: 32 }); ap = 4; focus = 0.83; }
        else if (C.id === 'ecu') { const e = hp.clone().add(V(0, -0.02, 0)); placeCamera(cam, e.clone().add(V(0.03, 0, 0.36)).toArray(), e.toArray(), { shake: 0.002, t, fov: 26 }); ap = 6; focus = 0.36; }
        else { camPath(cam, C.lt, [[0, hp.clone().add(V(0.7, 0.05, 0.7)).toArray(), top.toArray()], [4, hp.clone().add(V(0.55, 0.1, 0.4)).toArray(), top.toArray()]], { shake: 0.006, fov: 36 }); ap = 1.5; focus = 1.0; }
        coneGuard(W, cam);
        return { scene: W.scene, camera: cam, aperture: ap, focus, bloom: 0.55, grade: { exposure: 1.9 } };
      },
    };
  },
};

// Pose do duplo: rígido, cabeça inclinada, sorriso que se abre.
function poseDouble(d, t, { tilt = 0, smile = 0, at, walk = null }) {
  if (walk) {
    const w = walkById('descend'); const s = walkAt(w, t);
    d.resetPose();
    if (s.moving) d.walk(s.phase, { careful: 1 }); else d.idle(t, { fear: 0, breathRate: 0.08 });
    d.root.position.set(s.x, s.y, s.z); d.root.rotation.y = 0;
  } else {
    d.resetPose(); d.idle(t, { fear: 0, breathRate: 0.06 });
    d.root.position.copy(at); d.root.rotation.y = 0;
  }
  d.armsDown(0);
  d.j.shoulderL.rotation.z = 0.04; d.j.shoulderR.rotation.z = -0.04;
  d.j.head.rotation.z = tilt * 0.5; d.j.neck.rotation.z = tilt * 0.15;
  d.j.head.rotation.x = 0.12;
  d.updateFace(t, { wide: smile, smile: smile * 0.6, blink: 0, eyesWide: 0.25, squint: smile * 0.2 });
}

// ---------------- O DUPLO ----------------
const doubleReveal = {
  async create(ctx) {
    const W = interiorWorld(ctx);
    const cam = makeCamera(36);
    return {
      update(lt, t) {
        const g = W.gab, d = W.dbl;
        basementState(W, t, ctx);
        g.resetPose(); g.idle(t, { fear: 1 });
        g.root.position.set(0.2, 0, -1.4); g.root.rotation.y = -2.5;
        d.root.visible = true;
        const tilt = kf(lt, [[L('double_reveal', EVENTS.headTilt), 0], [L('double_reveal', EVENTS.headTilt) + 2.6, 1]], ease.inOut2);
        const smile = kf(lt, [[L('double_reveal', EVENTS.smile), 0], [L('double_reveal', EVENTS.smile) + 1.6, 1]], ease.inOut);
        poseDouble(d, t, { tilt, smile, at: STAIR_TOP });
        // ele abaixa o celular e aponta a lanterna para o alto da escada
        const dh = headPos(d);
        const lower = smooth(1.0, 2.6, lt);
        let flashOn = 0;
        if (lower < 0.5) { phoneToEar(g, 1 - lower * 2); phoneScreen(g, 'incall', lt + 20); }
        else { holdFlashlight(g, dh.clone().add(V(0, -0.2, 0)), {}); W.flashAim = dh.clone().add(V(0, -0.15, 0)); flashOn = smooth(2.4, 2.7, lt) * (0.75 + 0.25 * Math.abs(noise1(t * 12, 4))); phoneScreen(g, 'glitch', t); }
        g.look(dh, 1, { maxYaw: 1.4, maxPitch: 0.8 });
        g.updateFace(t, { ...fearFace(1), jaw: 0.12 + 0.05 * noise1(t * 3, 2), blink: 0 });
        updateInterior(W, t, { flashOn, coneInt: 0.07 });
        W.bounce.intensity = 0.4;
        const C = cut(lt, [[0, 'up'], [4.4, 'gab'], [7.0, 'dblMed'], [12.6, 'dblCU'], [15.6, 'over']]);
        const hp = headPos(g);
        let ap = 0, focus = 2;
        if (C.id === 'up') { camPath(cam, C.lt, [[0, [-2.5, 0.55, 0.2], dh.toArray()], [4.4, [-2.65, 0.75, -0.6], dh.toArray()]], { shake: 0.006, fov: 34 }); ap = 1.5; focus = cam.position.distanceTo(dh); }
        else if (C.id === 'gab') { placeCamera(cam, hp.clone().add(V(-0.55, 0.08, -0.38)).toArray(), hp.toArray(), { shake: 0.008, t, fov: 32 }); ap = 5; focus = 0.68; }
        else if (C.id === 'dblMed') { camPath(cam, C.lt, [[0, [-3.15, 2.45, -3.7], dh.toArray()], [5.6, [-3.2, 2.8, -4.4], dh.toArray()]], { shake: 0.004, fov: 30 }); ap = 3; focus = cam.position.distanceTo(dh); }
        else if (C.id === 'dblCU') { camPath(cam, C.lt, [[0, dh.clone().add(V(0.1, -0.12, 0.85)).toArray(), dh.clone().add(V(0, -0.05, 0)).toArray()], [3, dh.clone().add(V(0.06, -0.08, 0.55)).toArray(), dh.clone().add(V(0, -0.05, 0)).toArray()]], { shake: 0.002, fov: 30 }); ap = 5; focus = cam.position.distanceTo(dh); }
        else { placeCamera(cam, dh.clone().add(V(0.35, 0.15, -0.5)).toArray(), hp.toArray(), { shake: 0.004, t, fov: 34 }); ap = 1.5; focus = cam.position.distanceTo(hp); }
        coneGuard(W, cam);
        return { scene: W.scene, camera: cam, aperture: ap, focus, bloom: 0.55, grade: { exposure: 1.9, aberration: 0.006 + 0.01 * smile } };
      },
    };
  },
};

// ---------------- A DECISÃO ----------------
const decision = {
  async create(ctx) {
    const W = interiorWorld(ctx);
    const cam = makeCamera(36);
    const freezeG = EVENTS.freeze;
    return {
      update(lt0, t0) {
        // congelamento: o tempo para no instante da escolha
        const t = Math.min(t0, freezeG), lt = t - shotById.decision.start;
        const frozen = t0 >= freezeG;
        const g = W.gab, d = W.dbl;
        // porta do porão entreaberta, luz fraca lá dentro
        basementState(W, t, ctx, { ajar: 0.32, glow: 0.35 + 0.08 * noise1(t * 2, 5) });
        d.root.visible = true;
        poseDouble(d, t, { tilt: 1, smile: 1, walk: true });
        const dh = headPos(d);
        g.resetPose(); g.idle(t, { fear: 1 });
        g.root.position.set(0.55, 0, -2.3);
        const bdoor = V(0.0, 1.1, -11.0);
        // olha para a escada, depois para o porão, e de novo...
        const lookK = kf(lt, [[0, 0], [2.0, 0], [2.6, 1], [5.0, 1], [5.6, 0], [7.6, 0], [8.1, 1], [8.9, 1], [9.4, 0], [10.4, 0], [11.2, 0.55]], ease.inOut);
        g.root.rotation.y = lerp(-2.45, -2.95, lookK);
        const look = dh.clone().lerp(bdoor, lookK);
        g.look(look, 1, { maxYaw: 1.3 });
        const aim = dh.clone().add(V(0, -0.5, 0)).lerp(bdoor, lookK);
        holdFlashlight(g, aim, {}); W.flashAim = aim;
        g.updateFace(t, { ...fearFace(1), jaw: 0.1, blink: lt > 7 ? 0 : null });
        phoneScreen(g, 'off');
        updateInterior(W, t, { flashOn: 0.85, coneInt: 0.07 });
        W.bounce.intensity = 0.45;
        const C = cut(lt, [[0, 'wide'], [2.4, 'basement'], [5.0, 'stairs'], [7.4, 'face'], [10.0, 'final']]);
        const hp = headPos(g);
        let ap = 0, focus = 2;
        if (C.id === 'wide') camPath(cam, C.lt, [[0, [1.6, 1.55, -0.25], [-1.6, 2.2, -6.0]], [2.4, [1.5, 1.55, -0.4], [-1.6, 2.1, -6.0]]], { shake: 0.006, fov: 42 });
        else if (C.id === 'basement') { camPath(cam, C.lt, [[0, hp.clone().add(V(0.15, -0.02, 0.1)).toArray(), bdoor.toArray()], [2.6, hp.clone().add(V(0.1, -0.05, -0.2)).toArray(), bdoor.toArray()]], { shake: 0.01, fov: 34 }); ap = 1; focus = 8; }
        else if (C.id === 'stairs') { camPath(cam, C.lt, [[0, hp.clone().add(V(0.1, 0, 0.1)).toArray(), dh.toArray()], [2.4, hp.clone().add(V(0.05, 0, -0.1)).toArray(), dh.toArray()]], { shake: 0.01, fov: 32 }); ap = 1; focus = cam.position.distanceTo(dh); }
        else if (C.id === 'face') { placeCamera(cam, hp.clone().add(V(-0.45, -0.05, -0.55)).toArray(), hp.toArray(), { shake: 0.004, t, fov: 32 }); ap = 5; focus = 0.72; }
        else { camPath(cam, C.lt, [[0, [2.3, 1.15, 0.0], [-1.0, 1.7, -6.8]], [3.6, [1.9, 1.2, -0.6], [-1.0, 1.7, -6.8]]], { shake: frozen ? 0 : 0.004, fov: 40 }); ap = 0.8; focus = 3; }
        coneGuard(W, cam);
        const fz = frozen ? 1 : 0;
        const flash = fz * pulse(t0, freezeG, 0.01, 0.25) * 0.25;
        return { scene: W.scene, camera: cam, aperture: ap, focus, bloom: 0.55, grade: { exposure: 1.9, desat: 0.55 * fz, flash, grain: 0.06 + 0.04 * fz } };
      },
    };
  },
};

export const shots = { basement_door: basementDoor, run_phone: runPhone, double_reveal: doubleReveal, decision };
