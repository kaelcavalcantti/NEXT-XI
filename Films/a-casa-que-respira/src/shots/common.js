// Utilidades compartilhadas pelos planos: cortes internos, sincronia labial, poses de mãos.
import * as THREE from 'three';
import { LINES, T } from '../timeline.js';
import { walkAt, walkById } from '../choreo.js';
import { clamp, smooth } from '../lib/util.js';

// Cortes dentro de um plano: list = [[t0, id], ...] -> { id, lt (tempo local do corte), t0, t1 }
export function cut(lt, list, end = 1e9) {
  let i = 0; while (i < list.length - 1 && lt >= list[i + 1][0]) i++;
  const t0 = list[i][0], t1 = i < list.length - 1 ? list[i + 1][0] : end;
  return { id: list[i][1], lt: lt - t0, t0, t1, u: clamp((lt - t0) / (t1 - t0)) };
}

// Sincronia labial: soma das falas ativas desse personagem
export function lips(ctx, who, t, human) {
  let jaw = 0, round = 0;
  for (const l of LINES) {
    if (l.who !== who) continue;
    const d = ctx.voices[l.id]?.dur ?? 2;
    if (t < l.t || t > l.t + d + 0.1) continue;
    const r = human.lipsAt(ctx.lipsync[l.id], t - l.t);
    jaw = Math.max(jaw, r.jaw); round = Math.max(round, r.round);
  }
  return { jaw, round };
}

export function speakingNow(ctx, id, t) { const l = LINES.find(x => x.id === id); const d = ctx.voices[id]?.dur ?? 2; return t >= l.t && t <= l.t + d; }
export const lineT = (id) => LINES.find(x => x.id === id).t;

// Aplica uma caminhada (choreo) ao personagem; offset/rot convertem para o espaço da cena.
export function applyWalk(h, id, t, { offset = [0, 0, 0], idleT = t, fear = 0.3, extra = {} } = {}) {
  const w = walkById(id); const s = walkAt(w, t);
  h.resetPose();
  if (s.moving) h.walk(s.phase, { careful: w.careful || 0, run: w.run || 0, ...extra });
  else h.idle(idleT, { fear });
  h.root.position.set(s.x + offset[0], s.y + offset[1], s.z + offset[2]);
  h.root.rotation.y = s.heading;
  return s;
}

// Mão direita segurando o celular como lanterna, apontando para "aim"
export function holdFlashlight(h, aim, { raise = 0 } = {}) {
  h.root.updateMatrixWorld(true);
  const chest = new THREE.Vector3(); h.j.chest.getWorldPosition(chest);
  const fwd = aim.clone().sub(chest); fwd.y = 0; fwd.normalize();
  const right = new THREE.Vector3(-fwd.z, 0, fwd.x).negate(); // lado direito do personagem
  const target = chest.clone().addScaledVector(fwd, 0.36).addScaledVector(right, 0.12).add(new THREE.Vector3(0, 0.08 + raise, 0));
  const pole = chest.clone().addScaledVector(right, 0.6).add(new THREE.Vector3(0, -0.5, 0));
  h.reach('R', target, pole);
  const dir = aim.clone().sub(target).normalize();
  h.orientHand('R', dir.clone().add(new THREE.Vector3(0, 0.25, 0)).normalize(), new THREE.Vector3(0, 1, 0).cross(dir).negate().normalize().add(new THREE.Vector3(0, -0.3, 0)));
  h.setGrip('R', 0.8, 0.8);
  h.attachPhone('front');
  h.ledMat.color.setScalar(1);
  return target;
}

// Celular no ouvido (direito)
export function phoneToEar(h, k = 1) {
  h.root.updateMatrixWorld(true);
  const head = new THREE.Vector3(); h.j.head.getWorldPosition(head);
  const q = new THREE.Quaternion(); h.j.head.getWorldQuaternion(q);
  const ear = new THREE.Vector3(-0.12, -0.03, 0.02).applyQuaternion(q).add(head);
  const rest = new THREE.Vector3(-0.25, -0.55, 0.15).applyQuaternion(q).add(head);
  const target = rest.lerp(ear, k);
  const pole = new THREE.Vector3(-0.35, -0.7, 0.25).applyQuaternion(q).add(head);
  h.reach('R', target, pole);
  const up = new THREE.Vector3(0, 1, 0).applyQuaternion(q);
  const fwd = new THREE.Vector3(0, 0, 1).applyQuaternion(q);
  h.orientHand('R', up.clone().addScaledVector(fwd, 0.4), new THREE.Vector3(1, 0, 0).applyQuaternion(q));
  h.setGrip('R', 0.75, 0.6);
  h.attachPhone(k > 0.4 ? 'ear' : 'look');
}

// Celular à frente do rosto (olhando a tela)
export function phoneLook(h) {
  h.root.updateMatrixWorld(true);
  const head = new THREE.Vector3(); h.j.head.getWorldPosition(head);
  const q = new THREE.Quaternion(); h.root.getWorldQuaternion(q);
  const target = new THREE.Vector3(-0.05, -0.3, 0.32).applyQuaternion(q).add(head);
  h.reach('R', target, new THREE.Vector3(-0.6, -0.6, 0).applyQuaternion(q).add(head));
  const fwd = new THREE.Vector3(0, 0, 1).applyQuaternion(q);
  h.orientHand('R', new THREE.Vector3(0, 1, 0).addScaledVector(fwd, 0.5), fwd.clone().negate());
  h.setGrip('R', 0.75, 0.7);
  h.attachPhone('look');
  return target;
}

// Tela do celular (canvas): chamada recebida, lanterna etc.
export function phoneScreen(h, mode, t = 0) {
  const c = h.screenTexCanvas, x = c.getContext('2d');
  x.fillStyle = '#05070a'; x.fillRect(0, 0, 256, 512);
  if (mode === 'off') { h.screenMat.color.set(0x000000); h.screenMat.map = null; h.screenMat.needsUpdate = true; return; }
  const g = x.createLinearGradient(0, 0, 0, 512); g.addColorStop(0, '#1b2a33'); g.addColorStop(1, '#05080a'); x.fillStyle = g; x.fillRect(0, 0, 256, 512);
  x.fillStyle = '#e8eef2'; x.textAlign = 'center';
  if (mode === 'call' || mode === 'unknown') {
    x.font = '22px Typewriter'; x.fillText(mode === 'unknown' ? 'chamada recebida' : 'chamada recebida', 128, 120);
    x.font = '30px Typewriter'; x.fillText(mode === 'unknown' ? 'Número' : 'Cartório', 128, 190); x.fillText(mode === 'unknown' ? 'desconhecido' : 'Pedra Funda', 128, 228);
    const p = 0.5 + 0.5 * Math.sin(t * 6);
    x.fillStyle = `rgba(60,200,110,${0.6 + 0.4 * p})`; x.beginPath(); x.arc(70, 420, 30, 0, 7); x.fill();
    x.fillStyle = 'rgba(210,60,50,0.9)'; x.beginPath(); x.arc(186, 420, 30, 0, 7); x.fill();
  } else if (mode === 'incall') {
    x.font = '26px Typewriter'; x.fillText('00:' + String(Math.floor(t) % 60).padStart(2, '0'), 128, 160);
  } else if (mode === 'glitch') {
    for (let i = 0; i < 40; i++) { x.fillStyle = `rgba(${Math.random() * 255},${Math.random() * 255},${Math.random() * 255},0.5)`; x.fillRect(0, Math.random() * 512, 256, Math.random() * 12); }
  }
  h.screenTex.needsUpdate = true;
  h.screenMat.map = h.screenTex; h.screenMat.color.set(0xffffff); h.screenMat.needsUpdate = true;
}

export const fearFace = (k, extra = {}) => ({ brow: 0.8 * k, eyesWide: 0.7 * k, ...extra });
export { smooth, clamp };
