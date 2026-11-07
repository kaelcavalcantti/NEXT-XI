// Objetos de cena: móveis, molduras com fotografias, relógios, brinquedos, cartas, álbum.
import * as THREE from 'three';
import * as TX from '../engine/tex.js';
import { rng } from '../lib/util.js';
import { merge } from './kit.js';

export const M = {};
export function mats() {
  if (M.wood) return M;
  const w = TX.woodPlanks({ planks: 3, base: [70, 46, 32], dark: [30, 20, 14], seed: 71, worn: 0.3 });
  M.wood = new THREE.MeshStandardMaterial({ map: w.map, normalMap: w.normalMap, roughness: 0.55, color: 0xbbaa99 });
  M.darkWood = new THREE.MeshStandardMaterial({ map: w.map, normalMap: w.normalMap, roughness: 0.5, color: 0x5a4a40 });
  M.brass = new THREE.MeshStandardMaterial({ color: 0x8a6a3a, metalness: 0.9, roughness: 0.35 });
  M.iron = new THREE.MeshStandardMaterial({ color: 0x1a1a1a, metalness: 0.7, roughness: 0.6 });
  const sheet = TX.fabric({ base: [196, 192, 180], seed: 72, weave: 3 });
  M.sheet = new THREE.MeshStandardMaterial({ map: sheet.map, normalMap: sheet.normalMap, roughness: 1, color: 0xbab6aa });
  M.glass = new THREE.MeshStandardMaterial({ color: 0x0b0f12, roughness: 0.05, metalness: 0.5, transparent: true, opacity: 0.5 });
  M.paper = new THREE.MeshStandardMaterial({ color: 0xcfc4a8, roughness: 0.9 });
  return M;
}

// ---------- Fotografias antigas (canvas) ----------
export function oldPhotoCanvas(seed, { w = 256, h = 320, people = 1, scratched = false, oval = false } = {}) {
  const r = rng(seed); const c = TX.canvas(w, h), x = c.getContext('2d');
  const g = x.createLinearGradient(0, 0, 0, h); g.addColorStop(0, '#a8987a'); g.addColorStop(1, '#6e5f48');
  x.fillStyle = g; x.fillRect(0, 0, w, h);
  // fundo de estúdio
  x.fillStyle = 'rgba(60,48,36,0.5)'; x.fillRect(0, h * 0.55, w, h * 0.45);
  for (let p = 0; p < people; p++) {
    const cx = w * (people === 1 ? 0.5 : 0.28 + 0.44 * p / Math.max(1, people - 1)), hy = h * r.range(0.28, 0.36), hr = w * (people > 2 ? 0.075 : 0.1);
    x.fillStyle = `rgb(${40 + r() * 20},${32 + r() * 15},${24 + r() * 10})`;
    x.beginPath(); x.ellipse(cx, h * 0.95, hr * 2.6, h * 0.5, 0, Math.PI, 0); x.fill(); // tronco
    x.fillStyle = `rgb(${150 + r() * 30},${128 + r() * 20},${100 + r() * 20})`;
    x.beginPath(); x.ellipse(cx, hy, hr * 0.82, hr, 0, 0, Math.PI * 2); x.fill(); // rosto
    x.fillStyle = 'rgba(30,24,18,0.9)';
    x.beginPath(); x.ellipse(cx, hy - hr * 0.55, hr * 0.9, hr * 0.55, 0, Math.PI, 0); x.fill(); // cabelo
    x.fillStyle = 'rgba(30,22,16,0.7)';
    x.fillRect(cx - hr * 0.42, hy - hr * 0.1, hr * 0.22, hr * 0.12); x.fillRect(cx + hr * 0.2, hy - hr * 0.1, hr * 0.22, hr * 0.12);
    x.fillRect(cx - hr * 0.25, hy + hr * 0.45, hr * 0.5, hr * 0.07);
    if (scratched && p === people - 1) {
      x.strokeStyle = 'rgba(230,220,200,0.9)'; x.lineWidth = 2;
      for (let k = 0; k < 40; k++) { x.beginPath(); x.moveTo(cx + r.range(-hr, hr), hy + r.range(-hr, hr)); x.lineTo(cx + r.range(-hr, hr), hy + r.range(-hr, hr)); x.stroke(); }
    }
  }
  // desgaste: grão, manchas, vinheta
  const img = x.getImageData(0, 0, w, h);
  for (let i = 0; i < img.data.length; i += 4) { const n = (r() - 0.5) * 30; img.data[i] += n; img.data[i + 1] += n; img.data[i + 2] += n; }
  x.putImageData(img, 0, 0);
  const vg = x.createRadialGradient(w / 2, h / 2, w * 0.2, w / 2, h / 2, w * 0.75); vg.addColorStop(0, 'rgba(0,0,0,0)'); vg.addColorStop(1, 'rgba(30,20,10,0.75)');
  x.fillStyle = vg; x.fillRect(0, 0, w, h);
  if (oval) { x.globalCompositeOperation = 'destination-in'; x.beginPath(); x.ellipse(w / 2, h / 2, w * 0.45, h * 0.46, 0, 0, Math.PI * 2); x.fill(); x.globalCompositeOperation = 'source-over'; }
  x.strokeStyle = 'rgba(240,230,210,0.25)'; x.lineWidth = 1;
  for (let k = 0; k < 6; k++) { x.beginPath(); const yy = r() * h; x.moveTo(0, yy); x.lineTo(w, yy + r.range(-20, 20)); x.stroke(); }
  return c;
}

export function framedPhoto(seed, { w = 0.32, h = 0.42, people = 1, scratched = false, frameColor = 0x2a1c12 } = {}) {
  const g = new THREE.Group();
  const t = new THREE.CanvasTexture(oldPhotoCanvas(seed, { people, scratched })); t.colorSpace = THREE.SRGBColorSpace;
  const pic = new THREE.Mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshStandardMaterial({ map: t, roughness: 0.4 }));
  pic.position.z = 0.012; g.add(pic);
  const fm = new THREE.MeshStandardMaterial({ color: frameColor, roughness: 0.5, metalness: 0.2 });
  const b = 0.035;
  g.add(new THREE.Mesh(merge([
    new THREE.BoxGeometry(w + b * 2, b, 0.03).translate(0, h / 2 + b / 2, 0), new THREE.BoxGeometry(w + b * 2, b, 0.03).translate(0, -h / 2 - b / 2, 0),
    new THREE.BoxGeometry(b, h, 0.03).translate(-w / 2 - b / 2, 0, 0), new THREE.BoxGeometry(b, h, 0.03).translate(w / 2 + b / 2, 0, 0),
  ]), fm));
  const glass = new THREE.Mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshStandardMaterial({ color: 0x000000, roughness: 0.05, metalness: 1, transparent: true, opacity: 0.25 }));
  glass.position.z = 0.016; g.add(glass);
  return g;
}

// ---------- Relógio de parede parado ----------
function clockFaceCanvas(roman = true) {
  const c = TX.canvas(256, 256), x = c.getContext('2d');
  x.fillStyle = '#d9cfb4'; x.beginPath(); x.arc(128, 128, 126, 0, Math.PI * 2); x.fill();
  const g = x.createRadialGradient(128, 128, 40, 128, 128, 128); g.addColorStop(0, 'rgba(0,0,0,0)'); g.addColorStop(1, 'rgba(70,50,20,0.5)'); x.fillStyle = g; x.fill();
  x.fillStyle = '#1a140e'; x.font = '26px Cormorant'; x.textAlign = 'center'; x.textBaseline = 'middle';
  const R = ['XII', 'I', 'II', 'III', 'IIII', 'V', 'VI', 'VII', 'VIII', 'IX', 'X', 'XI'];
  for (let i = 0; i < 12; i++) { const a = i / 12 * Math.PI * 2; x.fillText(roman ? R[i] : String(i || 12), 128 + Math.sin(a) * 98, 128 - Math.cos(a) * 98); }
  for (let i = 0; i < 60; i++) { const a = i / 60 * Math.PI * 2; x.fillRect(128 + Math.sin(a) * 118 - 1, 128 - Math.cos(a) * 118 - 1, 2, 2); }
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}
function hands(len, w, color = 0x111111) {
  const m = new THREE.MeshStandardMaterial({ color, metalness: 0.5, roughness: 0.5 });
  const g = new THREE.Mesh(new THREE.BoxGeometry(w, len, 0.004).translate(0, len / 2 - len * 0.12, 0), m);
  const p = new THREE.Group(); p.add(g); return p;
}
export function wallClock(hour = 3.2, size = 0.36) {
  mats();
  const g = new THREE.Group();
  const face = new THREE.Mesh(new THREE.CircleGeometry(size / 2, 48), new THREE.MeshStandardMaterial({ map: clockFaceCanvas(), roughness: 0.6 }));
  face.position.z = 0.03; g.add(face);
  const rim = new THREE.Mesh(new THREE.TorusGeometry(size / 2 + 0.015, 0.025, 8, 48), M.darkWood); rim.position.z = 0.03; g.add(rim);
  const back = new THREE.Mesh(new THREE.CylinderGeometry(size / 2 + 0.02, size / 2 + 0.02, 0.05, 32).rotateX(Math.PI / 2), M.darkWood); g.add(back);
  const hh = hands(size * 0.28, 0.012), mh = hands(size * 0.42, 0.008);
  hh.position.z = mh.position.z = 0.04; g.add(hh, mh);
  const set = (h) => { hh.rotation.z = -(h % 12) / 12 * Math.PI * 2; mh.rotation.z = -(h % 1) * Math.PI * 2; };
  set(hour); g.userData.set = set;
  return g;
}

// ---------- Relógio de pêndulo (carrilhão) ----------
export function grandfatherClock() {
  mats();
  const g = new THREE.Group();
  const W = 0.55, D = 0.32;
  g.add(new THREE.Mesh(new THREE.BoxGeometry(W + 0.08, 0.3, D + 0.06).translate(0, 0.15, 0), M.darkWood));
  g.add(new THREE.Mesh(new THREE.BoxGeometry(W - 0.06, 1.25, D).translate(0, 0.92, 0), M.darkWood));
  g.add(new THREE.Mesh(new THREE.BoxGeometry(W + 0.06, 0.6, D + 0.04).translate(0, 1.85, 0), M.darkWood));
  const crown = new THREE.Mesh(new THREE.CylinderGeometry(0.34, 0.34, D + 0.06, 24, 1, false, -Math.PI / 2, Math.PI).rotateX(Math.PI / 2).rotateZ(Math.PI / 2).scale(1, 0.5, 1), M.darkWood);
  crown.position.y = 2.15; crown.rotation.z = Math.PI / 2; g.add(crown);
  const cap = new THREE.Mesh(new THREE.BoxGeometry(W + 0.12, 0.06, D + 0.08).translate(0, 2.18, 0), M.darkWood); g.add(cap);
  const face = new THREE.Mesh(new THREE.CircleGeometry(0.2, 48), new THREE.MeshStandardMaterial({ map: clockFaceCanvas(), roughness: 0.6 }));
  face.position.set(0, 1.85, D / 2 + 0.03); g.add(face);
  const ring = new THREE.Mesh(new THREE.TorusGeometry(0.21, 0.012, 8, 48), M.brass); ring.position.copy(face.position); g.add(ring);
  const hh = hands(0.11, 0.012), mh = hands(0.17, 0.008);
  hh.position.set(0, 1.85, D / 2 + 0.04); mh.position.set(0, 1.85, D / 2 + 0.045); g.add(hh, mh);
  // pêndulo atrás de vidro
  const pend = new THREE.Group(); pend.position.set(0, 1.5, D / 2 - 0.05);
  const rod = new THREE.Mesh(new THREE.BoxGeometry(0.012, 0.85, 0.01).translate(0, -0.425, 0), M.brass); pend.add(rod);
  const bob = new THREE.Mesh(new THREE.CylinderGeometry(0.075, 0.075, 0.02, 32).rotateX(Math.PI / 2), M.brass); bob.position.y = -0.85; pend.add(bob);
  g.add(pend);
  const win = new THREE.Mesh(new THREE.PlaneGeometry(W - 0.2, 1.0), M.glass); win.position.set(0, 0.98, D / 2 + 0.001); g.add(win);
  const dark = new THREE.Mesh(new THREE.PlaneGeometry(W - 0.2, 1.0), new THREE.MeshStandardMaterial({ color: 0x080604 })); dark.position.set(0, 0.98, D / 2 - 0.1); g.add(dark);
  g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  g.userData = { hh, mh, pend, set: (h) => { hh.rotation.z = -(h % 12) / 12 * Math.PI * 2; mh.rotation.z = -(h % 1) * Math.PI * 2; } };
  return g;
}

// ---------- Móveis ----------
export function table({ w = 1.2, d = 0.8, h = 0.76, round = true } = {}) {
  mats();
  const g = new THREE.Group();
  const top = round ? new THREE.CylinderGeometry(w / 2, w / 2, 0.05, 40) : new THREE.BoxGeometry(w, 0.05, d);
  g.add(new THREE.Mesh(top.translate(0, h, 0), M.wood));
  if (round) g.add(new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.09, h, 12).translate(0, h / 2, 0), M.darkWood), new THREE.Mesh(new THREE.CylinderGeometry(0.32, 0.36, 0.06, 24).translate(0, 0.03, 0), M.darkWood));
  else for (const [x, z] of [[1, 1], [1, -1], [-1, 1], [-1, -1]]) g.add(new THREE.Mesh(new THREE.BoxGeometry(0.06, h, 0.06).translate(x * (w / 2 - 0.06), h / 2, z * (d / 2 - 0.06)), M.darkWood));
  g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  return g;
}

// poltrona coberta por lençol
export function sheetedChair(seed = 1) {
  mats();
  const geo = new THREE.BoxGeometry(0.9, 1.0, 0.85, 12, 12, 12);
  const p = geo.attributes.position; const r = rng(seed);
  for (let i = 0; i < p.count; i++) {
    let x = p.getX(i), y = p.getY(i) + 0.5, z = p.getZ(i);
    // encosto alto atrás, assento baixo na frente
    if (z > -0.15 && y > 0.5) y = 0.5 + (y - 0.5) * 0.15;
    if (y > 0.92) { x *= 0.85; }
    const drape = Math.max(0, 0.5 - y) * 0.12;
    x *= 1 + drape; z *= 1 + drape;
    x += Math.sin(y * 18 + seed) * 0.012; z += Math.cos(x * 15 + seed) * 0.01;
    p.setXYZ(i, x, Math.max(0, y), z);
  }
  geo.computeVertexNormals();
  const m = new THREE.Mesh(geo, M.sheet); m.castShadow = true; m.receiveShadow = true;
  return m;
}

export function sideboard({ w = 1.6, h = 0.9, d = 0.45 } = {}) {
  mats();
  const g = new THREE.Group();
  g.add(new THREE.Mesh(new THREE.BoxGeometry(w, h - 0.12, d).translate(0, h / 2 + 0.06, 0), M.darkWood));
  g.add(new THREE.Mesh(new THREE.BoxGeometry(w + 0.06, 0.04, d + 0.04).translate(0, h, 0), M.wood));
  for (let i = 0; i < 3; i++) g.add(new THREE.Mesh(new THREE.SphereGeometry(0.018, 8, 6).translate(-w / 3 + i * w / 3, h * 0.6, d / 2 + 0.01), M.brass));
  g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  return g;
}

export function candlestick() {
  mats();
  const g = new THREE.Group();
  g.add(new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.07, 0.02, 16).translate(0, 0.01, 0), M.brass));
  g.add(new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.016, 0.2, 10).translate(0, 0.11, 0), M.brass));
  g.add(new THREE.Mesh(new THREE.CylinderGeometry(0.014, 0.014, 0.1, 10).translate(0, 0.26, 0), new THREE.MeshStandardMaterial({ color: 0xc8c0a8, roughness: 0.8 })));
  return g;
}

// ---------- Quarto infantil ----------
export function smallBed() {
  mats();
  const g = new THREE.Group();
  g.add(new THREE.Mesh(new THREE.BoxGeometry(0.95, 0.3, 1.75).translate(0, 0.35, 0), M.wood));
  const quilt = TX.fabric({ base: [120, 110, 120], seed: 81, weave: 1.2 });
  const qg = new THREE.BoxGeometry(0.98, 0.14, 1.6, 10, 2, 16); const qp = qg.attributes.position;
  for (let i = 0; i < qp.count; i++) qp.setY(i, qp.getY(i) + Math.sin(qp.getX(i) * 9) * 0.01 + Math.sin(qp.getZ(i) * 7) * 0.012);
  qg.computeVertexNormals();
  g.add(new THREE.Mesh(qg.translate(0, 0.56, 0.05), new THREE.MeshStandardMaterial({ map: quilt.map, normalMap: quilt.normalMap, color: 0x8a7d86, roughness: 1 })));
  g.add(new THREE.Mesh(new THREE.BoxGeometry(0.6, 0.12, 0.32, 6, 2, 4).translate(0, 0.66, -0.66), M.sheet));
  g.add(new THREE.Mesh(new THREE.BoxGeometry(1.0, 0.9, 0.06).translate(0, 0.45, -0.9), M.wood));
  g.add(new THREE.Mesh(new THREE.BoxGeometry(1.0, 0.55, 0.06).translate(0, 0.28, 0.9), M.wood));
  for (const x of [-0.47, 0.47]) for (const z of [-0.9, 0.9]) g.add(new THREE.Mesh(new THREE.SphereGeometry(0.035, 10, 8).translate(x, z < 0 ? 0.92 : 0.57, z), M.wood));
  g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  return g;
}

export function rockingHorse() {
  mats();
  const g = new THREE.Group();
  const paint = new THREE.MeshStandardMaterial({ color: 0x6e5a48, roughness: 0.7 });
  const rocker = new THREE.TorusGeometry(0.6, 0.02, 6, 32, Math.PI * 0.5).rotateZ(Math.PI * 1.25);
  for (const z of [-0.12, 0.12]) { const m = new THREE.Mesh(rocker.clone().translate(0, 0.62, z), M.darkWood); g.add(m); }
  g.add(new THREE.Mesh(new THREE.CapsuleGeometry(0.11, 0.42, 6, 12).rotateZ(Math.PI / 2).translate(0, 0.45, 0), paint));
  g.add(new THREE.Mesh(new THREE.CapsuleGeometry(0.06, 0.2, 6, 10).rotateZ(-0.6).translate(0.3, 0.62, 0), paint));
  g.add(new THREE.Mesh(new THREE.CapsuleGeometry(0.065, 0.14, 6, 10).rotateZ(Math.PI / 2 - 0.3).translate(0.42, 0.72, 0), paint));
  for (const [x, z] of [[0.18, 0.08], [0.18, -0.08], [-0.18, 0.08], [-0.18, -0.08]]) g.add(new THREE.Mesh(new THREE.CylinderGeometry(0.025, 0.025, 0.3, 8).translate(x, 0.24, z), paint));
  const eye = new THREE.Mesh(new THREE.SphereGeometry(0.012, 8, 6), new THREE.MeshStandardMaterial({ color: 0x050505, roughness: 0.1 }));
  eye.position.set(0.44, 0.76, 0.055); g.add(eye); const e2 = eye.clone(); e2.position.z = -0.055; g.add(e2);
  g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  return g;
}

export function doll() {
  const g = new THREE.Group();
  const dress = new THREE.MeshStandardMaterial({ color: 0x6a4a50, roughness: 1 });
  const porcelain = new THREE.MeshPhysicalMaterial({ color: 0xd8cbbb, roughness: 0.3, clearcoat: 0.6 });
  g.add(new THREE.Mesh(new THREE.ConeGeometry(0.09, 0.2, 16).translate(0, 0.1, 0), dress));
  const head = new THREE.Mesh(new THREE.SphereGeometry(0.055, 16, 12), porcelain); head.position.y = 0.25; g.add(head);
  const hair = new THREE.Mesh(new THREE.SphereGeometry(0.06, 16, 12, 0, Math.PI * 2, 0, Math.PI * 0.55), new THREE.MeshStandardMaterial({ color: 0x2a1a10, roughness: 0.8 }));
  hair.position.y = 0.255; hair.rotation.x = -0.4; g.add(hair);
  for (const s of [-1, 1]) { const e = new THREE.Mesh(new THREE.SphereGeometry(0.009, 8, 6), new THREE.MeshStandardMaterial({ color: 0x050505, roughness: 0.05 })); e.position.set(s * 0.02, 0.26, 0.048); g.add(e); }
  g.rotation.x = -0.25;
  return g;
}

export function toyBlocks(seed = 3) {
  const r = rng(seed); const g = new THREE.Group();
  const cols = [0x6a3a30, 0x3a4a5a, 0x5a5a30, 0x4a3a50];
  for (let i = 0; i < 7; i++) {
    const m = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.07, 0.07), new THREE.MeshStandardMaterial({ color: r.pick(cols), roughness: 0.8 }));
    const stack = i < 3; m.position.set(stack ? 0 : r.range(-0.3, 0.3), stack ? 0.035 + i * 0.07 : 0.035, stack ? 0 : r.range(-0.3, 0.3)); m.rotation.y = r() * 3;
    m.castShadow = true; g.add(m);
  }
  return g;
}

// Desenho infantil (giz de cera) — casa, figuras; um deles com duas figuras iguais.
export function childDrawing(kind, seed = 1) {
  const r = rng(seed); const c = TX.canvas(256, 200), x = c.getContext('2d');
  x.fillStyle = '#d8d0bc'; x.fillRect(0, 0, 256, 200);
  x.lineWidth = 3; x.lineCap = 'round';
  const wob = (pts, col) => { x.strokeStyle = col; x.beginPath(); pts.forEach(([a, b], i) => { const px = a + r.range(-1.5, 1.5), py = b + r.range(-1.5, 1.5); i ? x.lineTo(px, py) : x.moveTo(px, py); }); x.stroke(); };
  const person = (cx, by, h, col) => {
    x.strokeStyle = col; x.beginPath(); x.arc(cx, by - h * 0.82, h * 0.13, 0, Math.PI * 2); x.stroke();
    wob([[cx, by - h * 0.7], [cx, by - h * 0.3]], col); wob([[cx - h * 0.2, by - h * 0.55], [cx + h * 0.2, by - h * 0.55]], col);
    wob([[cx, by - h * 0.3], [cx - h * 0.15, by]], col); wob([[cx, by - h * 0.3], [cx + h * 0.15, by]], col);
  };
  if (kind === 'house') {
    wob([[60, 170], [60, 90], [128, 40], [196, 90], [196, 170], [60, 170]], '#3a3a44');
    wob([[85, 110], [110, 110], [110, 135], [85, 135], [85, 110]], '#3a3a44');
    x.fillStyle = '#202024'; x.fillRect(150, 108, 22, 26);
    person(161, 132, 28, '#a01818');
    wob([[0, 180], [256, 182]], '#3a5a30');
  } else if (kind === 'twins') {
    person(90, 175, 110, '#2a2a6a'); person(170, 175, 110, '#2a2a6a');
    x.fillStyle = '#101010'; x.fillRect(160, 72, 6, 4); x.fillRect(176, 72, 6, 4);
    wob([[164, 90], [172, 94], [180, 90]], '#a01818');
  } else if (kind === 'woman') {
    x.fillStyle = '#141414'; x.beginPath(); x.moveTo(128, 30); x.lineTo(90, 185); x.lineTo(166, 185); x.fill();
    x.beginPath(); x.arc(128, 40, 18, 0, Math.PI * 2); x.fill();
    wob([[20, 190], [236, 190]], '#5a4020');
    person(40, 185, 50, '#2a2a6a');
  } else {
    x.fillStyle = '#e0c040'; x.beginPath(); x.arc(200, 40, 22, 0, Math.PI * 2); x.fill();
    person(80, 180, 90, '#2a2a6a'); person(130, 180, 120, '#5a2a2a');
  }
  x.fillStyle = '#2a2a2a'; x.font = '16px Hand'; x.fillText(kind === 'twins' ? 'eu e eu' : (kind === 'woman' ? 'não abra' : 'Gabriel'), 14, 22);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
  const m = new THREE.Mesh(new THREE.PlaneGeometry(0.42, 0.33), new THREE.MeshStandardMaterial({ map: t, roughness: 1 }));
  m.rotation.z = r.range(-0.08, 0.08);
  return m;
}

// Caixa de madeira com tampa articulada e cartas dentro
export function letterBox() {
  mats();
  const g = new THREE.Group();
  const W = 0.36, D = 0.26, H = 0.12;
  const walls = merge([
    new THREE.BoxGeometry(W, 0.012, D).translate(0, 0.006, 0),
    new THREE.BoxGeometry(W, H, 0.015).translate(0, H / 2, D / 2), new THREE.BoxGeometry(W, H, 0.015).translate(0, H / 2, -D / 2),
    new THREE.BoxGeometry(0.015, H, D).translate(W / 2, H / 2, 0), new THREE.BoxGeometry(0.015, H, D).translate(-W / 2, H / 2, 0),
  ]);
  g.add(new THREE.Mesh(walls, M.darkWood));
  const lidPivot = new THREE.Group(); lidPivot.position.set(0, H, -D / 2); g.add(lidPivot);
  const lid = new THREE.Mesh(new THREE.BoxGeometry(W + 0.01, 0.02, D + 0.01).translate(0, 0.01, D / 2), M.darkWood); lidPivot.add(lid);
  const lock = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.04, 0.01), M.brass); lock.position.set(0, H - 0.02, D / 2 + 0.008); g.add(lock);
  const letters = [];
  for (let i = 0; i < 5; i++) {
    const l = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.004, 0.2), M.paper); l.position.set((i - 2) * 0.008, 0.02 + i * 0.012, (i % 2) * 0.01); l.rotation.y = (i - 2) * 0.05; g.add(l); letters.push(l);
  }
  g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  g.userData = { lidPivot, letters };
  return g;
}

// Folha de carta manuscrita (canvas) — texto de Augusto
export function letterCanvas(lines, revealed = 1) {
  const c = TX.canvas(768, 1024), x = c.getContext('2d');
  const r = rng(99);
  const g = x.createLinearGradient(0, 0, 768, 1024); g.addColorStop(0, '#d9ceb0'); g.addColorStop(1, '#b9a882');
  x.fillStyle = g; x.fillRect(0, 0, 768, 1024);
  for (let i = 0; i < 2500; i++) { x.fillStyle = `rgba(90,70,40,${r() * 0.06})`; x.fillRect(r() * 768, r() * 1024, r() * 6, r() * 6); }
  x.strokeStyle = 'rgba(120,100,70,0.25)'; x.lineWidth = 2;
  x.beginPath(); x.moveTo(0, 512); x.lineTo(768, 520); x.stroke();
  x.fillStyle = 'rgba(70,50,30,0.15)'; x.beginPath(); x.ellipse(600, 860, 90, 60, 0.3, 0, Math.PI * 2); x.fill();
  x.fillStyle = '#1e1a2a'; x.font = '44px Hand'; x.textBaseline = 'alphabetic';
  let y = 120;
  x.font = '38px Hand'; x.fillText('Pedra Funda, 3 de junho de 1994', 200, 80);
  x.font = '46px Hand';
  const total = lines.join(' ').length; let shown = 0;
  for (const ln of lines) {
    if (ln === '') { y += 40; continue; }
    const words = ln.split(' '); let line = '';
    for (const w of words) {
      const test = line ? line + ' ' + w : w;
      if (x.measureText(test).width > 640) { x.fillText(line, 64, y); y += 62; line = w; } else line = test;
    }
    x.fillText(line, 64, y); y += 74;
    shown += ln.length;
  }
  x.font = '48px Hand'; x.fillText('— Augusto', 420, Math.min(990, y + 30));
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
  return t;
}
