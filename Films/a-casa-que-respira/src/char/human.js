// Gabriel (e seu duplo): personagem procedural com esqueleto, rosto esculpido com morphs,
// olhos com íris, pálpebras que piscam, cabelo, jaqueta, jeans e botas. Animação procedural + IK de braço.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { rng, clamp, smooth, lerp, fbm1, noise1 } from '../lib/util.js';
import * as TX from '../engine/tex.js';

const HEAD_S = 0.114; // escala da cabeça (unidade -> metros)
const gauss = (d2, s) => Math.exp(-d2 / (2 * s * s));

// ---------------- ESCULTURA DA CABEÇA ----------------
// Espaço da cabeça: y de -1 (queixo) a +1 (topo); z frente; x direita. Escala HEAD_S -> metros.
// Proporções pela regra dos terços: linha do cabelo 0.55, sobrancelha 0.12, base do nariz -0.42, boca -0.6, queixo -1.
const curve = (y, pts) => { // spline Catmull-Rom (pts em y decrescente), sem patamares nos nós
  const n = pts.length;
  if (y >= pts[0][0]) return pts[0][1];
  if (y <= pts[n - 1][0]) return pts[n - 1][1];
  let i = 0; while (i < n - 2 && y < pts[i + 1][0]) i++;
  const [y0, v0] = pts[i], [y1, v1] = pts[i + 1];
  const vm = i > 0 ? pts[i - 1] : [2 * y0 - y1, 2 * v0 - v1], vp = i + 2 < n ? pts[i + 2] : [2 * y1 - y0, 2 * v1 - v0];
  const h = y1 - y0, t = (y - y0) / h;
  const m0 = (v1 - vm[1]) / (y1 - vm[0]) * h, m1 = (vp[1] - v0) / (vp[0] - y0) * h;
  const t2 = t * t, t3 = t2 * t;
  return (2 * t3 - 3 * t2 + 1) * v0 + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * v1 + (t3 - t2) * m1;
};
const W_ = y => Math.sqrt(Math.max(0, curve(y, [[1, 0.0], [0.92, 0.16], [0.7, 0.36], [0.3, 0.46], [0.0, 0.45], [-0.25, 0.41], [-0.5, 0.32], [-0.75, 0.18], [-0.92, 0.08], [-1, 0.02]])));
const DF = y => Math.sqrt(Math.max(0, curve(y, [[1, 0.0], [0.9, 0.22], [0.6, 0.52], [0.3, 0.7], [0.1, 0.73], [-0.2, 0.66], [-0.5, 0.62], [-0.8, 0.5], [-0.94, 0.34], [-1, 0.12]])));
const DB = y => Math.sqrt(Math.max(0, curve(y, [[1, 0.0], [0.88, 0.3], [0.5, 0.78], [0.15, 0.88], [-0.2, 0.66], [-0.45, 0.32], [-0.7, 0.14], [-1, 0.06]])));
export const FACE = { eyeX: 0.27, eyeY: 0.0, browY: 0.17, noseY: -0.42, mouthY: -0.61, chinY: -0.95, hairY: 0.55 };

function faceDZ(x, y) {
  const F = FACE; let dz = 0;
  for (const s of [-1, 1]) {
    const ex = x - s * F.eyeX;
    dz -= 0.09 * gauss(ex * ex * 0.8 + (y - F.eyeY) ** 2 * 1.2, 0.1);              // órbita
    dz += 0.05 * gauss(ex * ex * 0.5 + (y - F.browY - 0.04) ** 2 * 3, 0.1);         // arco superciliar
    dz += 0.035 * gauss((x - s * 0.42) ** 2 + (y + 0.25) ** 2, 0.12);               // maçã do rosto
    dz -= 0.025 * gauss(((x - s * 0.17) - (y + 0.5) * 0.3 * s) ** 2 * 8 + (y + 0.5) ** 2, 0.12); // sulco nasolabial
    dz -= 0.03 * gauss((x - s * 0.45) ** 2 + (y + 0.55) ** 2, 0.1);                 // bochecha côncava
    dz += 0.03 * gauss((x - s * 0.08) ** 2 * 3 + (y - 0.42) ** 2, 0.2);             // fronte
  }
  // nariz: ponte estreita, dorso, ponta, asas
  const nb = smooth(0.06, -0.12, y) * smooth(F.noseY + 0.02, F.noseY + 0.12, y);
  dz += (0.06 + 0.2 * smooth(0.05, F.noseY + 0.08, y)) * nb * gauss(x * x, 0.045 + 0.025 * smooth(0, F.noseY, y));
  dz += 0.04 * gauss(x * x * 1.3 + (y - F.noseY - 0.1) ** 2, 0.045);
  for (const s of [-1, 1]) dz += 0.05 * gauss((x - s * 0.1) ** 2 + (y - F.noseY - 0.06) ** 2, 0.04);
  // filtro, lábios, linha da boca, queixo
  dz += 0.015 * gauss(x * x * 4 + (y - F.mouthY - 0.1) ** 2, 0.05);
  const up = gauss(x * x * 0.8 + ((y - F.mouthY - 0.045) ** 2) * 5, 0.1);
  const lo = gauss(x * x * 1.0 + ((y - F.mouthY + 0.065) ** 2) * 5, 0.09);
  dz += 0.045 * up + 0.04 * lo;
  const line = gauss(((y - F.mouthY) ** 2) * 80, 0.25) * smooth(0.25, 0.14, Math.abs(x));
  dz -= 0.018 * line;
  dz -= 0.03 * gauss(x * x * 2 + (y - F.mouthY + 0.17) ** 2, 0.06);                 // sulco mentolabial
  dz += 0.04 * gauss(x * x * 0.7 + (y - F.chinY + 0.1) ** 2, 0.1);                   // queixo
  return { dz, lips: clamp((up + lo) * 1.25 - line * 0.4), line };
}

// ponto da esfera unitária -> ponto esculpido
function sculpt(px, py, pz) {
  const y = py;
  const c = Math.sqrt(Math.max(1e-6, 1 - py * py));
  const sx = px / c, cz = pz / c; // seno/cosseno no plano xz
  const w = W_(y);
  let x = w * sx, z;
  if (cz >= 0) z = DF(y) * Math.pow(cz, 0.78); else z = -DB(y) * Math.pow(-cz, 0.9);
  // mandíbula: ângulo marcado (alarga um pouco atrás no nível da boca)
  if (cz < 0.4 && cz > -0.3) x *= 1 + 0.04 * gauss((y + 0.55) ** 2, 0.15);
  const front = smooth(0.15, 0.6, cz);
  const f = faceDZ(x, y);
  z += f.dz * front;
  const stubble = smooth(-0.3, -0.55, y) * smooth(-0.2, 0.5, cz) * (1 - f.lips) * (1 - gauss(x * x + (y - FACE.noseY + 0.05) ** 2, 0.06));
  return { p: [x, y, z], lips: f.lips * front, stubble, front, cz, sx };
}
export function faceSurfaceZ(x, y) { // z da superfície frontal num (x,y)
  const w = W_(y); const sx = clamp(x / w, -0.999, 0.999); const cz = Math.sqrt(1 - sx * sx);
  return DF(y) * Math.pow(cz, 0.78) + faceDZ(x, y).dz * smooth(0.15, 0.6, cz);
}

function buildHead(skin, opts) {
  const geo = new THREE.SphereGeometry(1, 144, 112);
  const pos = geo.attributes.position;
  const colors = new Float32Array(pos.count * 3);
  const jaw = new Float32Array(pos.count * 3), smile = new Float32Array(pos.count * 3), brow = new Float32Array(pos.count * 3), round = new Float32Array(pos.count * 3), wide = new Float32Array(pos.count * 3);
  const base = new THREE.Color(skin), lipC = new THREE.Color(opts.lipColor), stubC = new THREE.Color(opts.stubbleColor);
  const pivot = new THREE.Vector3(0, -0.3, -0.1);
  const M = FACE.mouthY;
  for (let i = 0; i < pos.count; i++) {
    const s = sculpt(pos.getX(i), pos.getY(i), pos.getZ(i));
    const [x, y, z] = s.p;
    pos.setXYZ(i, x, y, z);
    const blot = 0.94 + 0.12 * (noise1(x * 13 + y * 7, 11) * 0.5 + 0.5);
    const c = base.clone().multiplyScalar(blot).lerp(lipC, s.lips * 0.7).lerp(stubC, s.stubble * opts.stubble);
    c.r *= 1 + 0.07 * gauss((Math.abs(x) - 0.42) ** 2 + (y + 0.2) ** 2, 0.15) * s.front;
    const sock = gauss((Math.abs(x) - FACE.eyeX) ** 2 + (y - FACE.eyeY) ** 2, 0.12) * s.front;
    c.multiplyScalar(1 - sock * 0.18);
    colors[i * 3] = c.r; colors[i * 3 + 1] = c.g; colors[i * 3 + 2] = c.b;
    const ax = Math.abs(x);
    // mandíbula: abaixo da linha da boca, à frente da orelha; transição alarga para as laterais (sem vinco)
    const y0 = M + 0.015 + 0.12 * smooth(0.18, 0.45, ax), y1 = M - 0.04 - 0.3 * smooth(0.15, 0.45, ax);
    const wj = smooth(y0, y1, y) * smooth(-0.35, 0.1, s.cz);
    if (wj > 0) {
      const ang = 0.24 * wj;
      const vy = y - pivot.y, vz = z - pivot.z;
      jaw[i * 3 + 1] = (vy * Math.cos(ang) + vz * Math.sin(ang)) - vy;
      jaw[i * 3 + 2] = (-vy * Math.sin(ang) + vz * Math.cos(ang)) - vz;
    }
    const corner = gauss((ax - 0.19) ** 2 + (y - M) ** 2, 0.07) * s.front;
    const cheek = gauss((ax - 0.36) ** 2 + (y + 0.3) ** 2, 0.12) * s.front;
    smile[i * 3] = Math.sign(x) * corner * 0.04; smile[i * 3 + 1] = corner * 0.08 + cheek * 0.035; smile[i * 3 + 2] = -corner * 0.03 + cheek * 0.012;
    const band = gauss((y - M) ** 2 * 10, 0.1) * smooth(0.5, 0.1, ax) * s.front;
    wide[i * 3] = Math.sign(x) * band * 0.2 * Math.min(1, ax * 4); wide[i * 3 + 1] = band * 0.22 * Math.min(1, (ax / 0.3) ** 2); wide[i * 3 + 2] = -band * 0.07 * Math.min(1, ax * 3);
    const bm = gauss((ax - 0.22) ** 2 * 0.6 + (y - FACE.browY - 0.03) ** 2 * 2.5, 0.1) * s.front;
    brow[i * 3 + 1] = bm * 0.05 * (1 + 0.6 * smooth(0.3, 0.08, ax));
    const lr = gauss(x * x * 0.8 + (y - M) ** 2 * 3, 0.12) * s.front;
    round[i * 3] = -x * lr * 0.35; round[i * 3 + 2] = lr * 0.04;
  }
  geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
  geo.morphAttributes.position = [jaw, smile, brow, round, wide].map(a => new THREE.BufferAttribute(a, 3));
  geo.morphTargetsRelative = true;
  geo.computeVertexNormals();
  const mat = new THREE.MeshPhysicalMaterial({
    vertexColors: true, roughness: 0.55, sheen: 0.4, sheenColor: new THREE.Color(0x804838), sheenRoughness: 0.6,
    clearcoat: 0.08, clearcoatRoughness: 0.5,
  });
  skinScatter(mat);
  const mesh = new THREE.Mesh(geo, mat);
  mesh.morphTargetInfluences = [0, 0, 0, 0, 0];
  return mesh;
}

// Aproximação de espalhamento subsuperficial: luz "envolve" o terminador com tom avermelhado.
function skinScatter(mat) {
  mat.onBeforeCompile = (sh) => {
    sh.fragmentShader = sh.fragmentShader.replace('#include <lights_fragment_end>', `#include <lights_fragment_end>
      reflectedLight.indirectDiffuse += reflectedLight.directDiffuse * vec3(0.28, 0.06, 0.03);`);
  };
}

function irisTexture(color) {
  const c = TX.canvas(256, 256), x = c.getContext('2d');
  const r = rng(5);
  x.fillStyle = '#d8d4cc'; x.fillRect(0, 0, 256, 256);
  // veias sutis
  for (let i = 0; i < 30; i++) { x.strokeStyle = 'rgba(150,40,40,0.18)'; x.beginPath(); const u = r() * 256; x.moveTo(u, 256); x.lineTo(u + r.range(-20, 20), r.range(80, 160)); x.stroke(); }
  // v (eixo y do canvas) = distância angular a partir do polo frontal
  const irisV = 0.165 * 256, pupV = 0.075 * 256;
  for (let y = 0; y < irisV; y++) {
    for (let u = 0; u < 256; u += 2) {
      const k = y / irisV;
      const fib = 0.75 + 0.5 * r();
      const col = new THREE.Color(color).multiplyScalar(fib * (0.7 + 0.6 * k) * (k > 0.88 ? 0.35 : 1));
      x.fillStyle = `rgb(${col.r * 255 | 0},${col.g * 255 | 0},${col.b * 255 | 0})`;
      x.fillRect(u, y, 2, 1);
    }
  }
  x.fillStyle = '#050505'; x.fillRect(0, 0, 256, pupV);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

// ---------------- CONSTRUÇÃO DO CORPO ----------------
const capsule = (r, len, rs = 12) => new THREE.CapsuleGeometry(r, len, 6, rs);

function taper(r0, r1, len, seg = 16, sx = 1, sz = 1) {
  const g = new THREE.CylinderGeometry(r1, r0, len, seg, 6, false);
  g.translate(0, -len / 2, 0); g.scale(sx, 1, sz);
  return g;
}

export class Human {
  constructor(opts = {}) {
    this.o = Object.assign({
      skin: 0x9c7560, lipColor: 0x8a5048, stubbleColor: 0x5a4a44, stubble: 0.45, iris: 0x4a3020, hair: 0x18110d,
      jacket: 0x2e3a2c, shirt: 0x5a5c5e, jeans: 0x2a3140, boots: 0x3a2618, seed: 1, double: false,
    }, opts);
    const o = this.o;
    this.root = new THREE.Group();
    this.j = {};
    const J = (name, parent, x, y, z) => { const g = new THREE.Group(); g.name = name; g.position.set(x, y, z); (parent ? this.j[parent] : this.root).add(g); this.j[name] = g; g.userData.rest = g.position.clone(); return g; };
    J('hips', null, 0, 0.98, 0);
    J('spine', 'hips', 0, 0.1, 0);
    J('chest', 'spine', 0, 0.2, 0);
    J('neck', 'chest', 0, 0.25, 0.0);
    J('head', 'neck', 0, 0.1, 0.01);
    for (const [s, sx] of [['L', 1], ['R', -1]]) {
      J('shoulder' + s, 'chest', sx * 0.185, 0.19, -0.01);
      J('elbow' + s, 'shoulder' + s, 0, -0.29, 0);
      J('wrist' + s, 'elbow' + s, 0, -0.255, 0);
      J('thigh' + s, 'hips', sx * 0.095, -0.05, 0);
      J('knee' + s, 'thigh' + s, 0, -0.445, 0);
      J('ankle' + s, 'knee' + s, 0, -0.43, 0);
    }
    this.lenUpper = 0.29; this.lenFore = 0.255;

    const jacketTex = TX.fabric({ base: [80, 96, 78], seed: 61, weave: 1.4 });
    const denim = TX.fabric({ base: [70, 84, 110], seed: 62, weave: 2.6 });
    const mJacket = new THREE.MeshStandardMaterial({ color: o.jacket, map: jacketTex.map, normalMap: jacketTex.normalMap, roughness: 0.9 });
    const mShirt = new THREE.MeshStandardMaterial({ color: o.shirt, roughness: 0.95 });
    const mJeans = new THREE.MeshStandardMaterial({ color: o.jeans, map: denim.map, normalMap: denim.normalMap, roughness: 0.92 });
    const mBoot = new THREE.MeshStandardMaterial({ color: o.boots, roughness: 0.6 });
    const mSkin = new THREE.MeshPhysicalMaterial({ color: o.skin, roughness: 0.6, sheen: 0.3, sheenColor: new THREE.Color(0x804838) });
    skinScatter(mSkin);
    const mHair = new THREE.MeshStandardMaterial({ color: o.hair, roughness: 0.55, metalness: 0.05 });
    this.mats = { mJacket, mShirt, mJeans, mBoot, mSkin, mHair };
    const add = (joint, geo, mat, x = 0, y = 0, z = 0) => { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); m.castShadow = true; m.receiveShadow = true; this.j[joint].add(m); return m; };

    // Tronco: abdômen e peito em jaqueta (seções elípticas), capuz, gola
    add('hips', taper(0.15, 0.155, 0.2, 20, 1, 0.68), mJeans, 0, 0.06, 0);
    add('spine', taper(0.15, 0.16, 0.24, 20, 1, 0.68), mJacket, 0, 0.22, 0.0);
    const chestG = taper(0.16, 0.19, 0.28, 24, 1, 0.6); chestG.translate(0, 0.28, 0);
    add('chest', chestG, mJacket, 0, 0, 0);
    add('chest', new THREE.SphereGeometry(0.19, 24, 12, 0, Math.PI * 2, 0, Math.PI / 2).scale(1.0, 0.36, 0.6), mJacket, 0, 0.28, 0);
    // capuz amontoado nas costas
    add('chest', new THREE.TorusGeometry(0.085, 0.032, 10, 24, Math.PI).rotateX(Math.PI / 2 - 0.3).rotateY(Math.PI).scale(1.2, 1, 0.9), mJacket, 0, 0.3, -0.035);
    // gola e camiseta à mostra
    add('chest', new THREE.CylinderGeometry(0.075, 0.085, 0.06, 16, 1, true), mShirt, 0, 0.29, 0.015);
    // zíper e bolsos
    const zip = new THREE.MeshStandardMaterial({ color: 0x111111, roughness: 0.4, metalness: 0.6 });
    add('chest', new THREE.BoxGeometry(0.008, 0.2, 0.006), zip, 0.02, 0.14, 0.117);
    add('spine', new THREE.BoxGeometry(0.008, 0.24, 0.006), zip, 0.02, 0.12, 0.112);
    // pescoço
    add('neck', taper(0.05, 0.056, 0.17, 16, 1, 1.05).translate(0, 0.135, 0), mSkin, 0, 0, 0.0);

    // Cabeça
    const head = buildHead(o.skin, o); head.scale.setScalar(HEAD_S); head.position.set(0, 0.1, 0.025);
    this.j.head.add(head); head.castShadow = true; head.receiveShadow = true;
    this.headMesh = head;
    // interior da boca e dentes
    const M = FACE.mouthY, mz = faceSurfaceZ(0, M);
    const mouthIn = new THREE.Mesh(new THREE.SphereGeometry(1, 16, 12).scale(0.17, 0.07, 0.1), new THREE.MeshStandardMaterial({ color: 0x1a0806, roughness: 1 }));
    mouthIn.position.set(0, M - 0.03, mz - 0.135); head.add(mouthIn);
    const teeth = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.04, 0.05), new THREE.MeshStandardMaterial({ color: o.double ? 0x9a9488 : 0xb8b2a6, roughness: 0.4 }));
    teeth.position.set(0, M + 0.015, mz - 0.075); head.add(teeth); this.teeth = teeth;
    // olhos
    const irisT = irisTexture(o.iris);
    const eyeMat = new THREE.MeshPhysicalMaterial({ map: irisT, roughness: 0.12, clearcoat: 1, clearcoatRoughness: 0.02 });
    this.eyes = [];
    const ER = 0.105;
    for (const s of [-1, 1]) {
      const ez = faceSurfaceZ(s * FACE.eyeX, FACE.eyeY) - 0.07;
      const eye = new THREE.Mesh(new THREE.SphereGeometry(ER, 32, 24).rotateX(Math.PI / 2), eyeMat);
      const holder = new THREE.Group(); holder.position.set(s * FACE.eyeX, FACE.eyeY, ez);
      holder.add(eye); head.add(holder);
      const lidGeo = new THREE.SphereGeometry(ER * 1.07, 32, 16, 0, Math.PI * 2, 0, Math.PI * 0.5);
      const lidMat = new THREE.MeshStandardMaterial({ color: new THREE.Color(o.skin).multiplyScalar(0.82), roughness: 0.6 });
      const up = new THREE.Mesh(lidGeo, lidMat); const upPivot = new THREE.Group(); upPivot.add(up); holder.add(upPivot);
      const lo = new THREE.Mesh(lidGeo, lidMat); lo.rotation.x = Math.PI; const loPivot = new THREE.Group(); loPivot.add(lo); holder.add(loPivot);
      const lash = new THREE.Mesh(new THREE.TorusGeometry(ER * 1.07, 0.007, 4, 24, Math.PI), new THREE.MeshBasicMaterial({ color: 0x0a0706 }));
      lash.rotation.x = Math.PI / 2; upPivot.add(lash);
      this.eyes.push({ eye, holder, upPivot, loPivot, side: s });
      const bpts = [0.08, 0.2, 0.33, 0.43].map((bx, k) => { const by = FACE.browY + [0.0, 0.04, 0.035, 0.0][k]; return new THREE.Vector3(s * bx, by, faceSurfaceZ(s * bx, by) + 0.008); });
      const browM = new THREE.Mesh(new THREE.TubeGeometry(new THREE.CatmullRomCurve3(bpts), 14, 0.024, 6), mHair);
      head.add(browM); this.eyes[this.eyes.length - 1].brow = browM;
    }
    // orelhas
    for (const s of [-1, 1]) {
      const earMat = new THREE.MeshStandardMaterial({ color: new THREE.Color(o.skin).multiplyScalar(0.93), roughness: 0.6 });
      const ear = new THREE.Mesh(new THREE.SphereGeometry(1, 16, 12).scale(0.07, 0.25, 0.15), earMat);
      ear.position.set(s * 0.64, -0.08, -0.08); ear.rotation.y = s * 0.3; ear.rotation.x = -0.15; head.add(ear);
      const inner = new THREE.Mesh(new THREE.SphereGeometry(1, 12, 8).scale(0.03, 0.13, 0.06), new THREE.MeshStandardMaterial({ color: 0x5a3428, roughness: 1 }));
      inner.position.set(s * 0.67, -0.08, -0.07); inner.rotation.y = s * 0.3; head.add(inner);
    }
    this.buildHair(head, mHair);

    // Braços (mangas) e mãos
    for (const [s, sx] of [['L', 1], ['R', -1]]) {
      add('shoulder' + s, new THREE.SphereGeometry(0.068, 16, 12), mJacket, 0, 0, 0);
      add('shoulder' + s, taper(0.062, 0.052, 0.29, 14), mJacket);
      add('elbow' + s, new THREE.SphereGeometry(0.052, 14, 10), mJacket);
      add('elbow' + s, taper(0.052, 0.044, 0.23, 14), mJacket);
      add('elbow' + s, new THREE.CylinderGeometry(0.046, 0.046, 0.04, 14).translate(0, -0.225, 0), mJacket);
      this.buildHand(s, sx, mSkin);
      // pernas
      add('thigh' + s, new THREE.SphereGeometry(0.085, 14, 10), mJeans);
      add('thigh' + s, taper(0.085, 0.058, 0.445, 14), mJeans);
      add('knee' + s, new THREE.SphereGeometry(0.058, 12, 10), mJeans);
      add('knee' + s, taper(0.056, 0.046, 0.4, 14), mJeans);
      // bota
      const boot = mergeGeometries([
        new THREE.CylinderGeometry(0.05, 0.055, 0.12, 14).translate(0, -0.0, 0),
        new THREE.BoxGeometry(0.1, 0.075, 0.25, 2, 2, 4).translate(0, -0.055, 0.055),
        new THREE.SphereGeometry(0.052, 12, 8).scale(1, 0.75, 1.0).translate(0, -0.055, 0.17),
      ].map(g => g.index ? g.toNonIndexed() : g));
      add('ankle' + s, boot, mBoot);
      add('ankle' + s, new THREE.BoxGeometry(0.105, 0.02, 0.29).translate(0, -0.09, 0.06), new THREE.MeshStandardMaterial({ color: 0x14100c, roughness: 0.9 }));
    }

    // Celular (mão direita) com lanterna
    const phone = new THREE.Group();
    const body = new THREE.Mesh(new THREE.BoxGeometry(0.072, 0.15, 0.009), new THREE.MeshStandardMaterial({ color: 0x0c0c0e, roughness: 0.3, metalness: 0.4 }));
    phone.add(body);
    this.screenMat = new THREE.MeshBasicMaterial({ color: 0x000000 });
    const screen = new THREE.Mesh(new THREE.PlaneGeometry(0.066, 0.142), this.screenMat); screen.position.z = 0.0048; phone.add(screen);
    this.screenTexCanvas = TX.canvas(256, 512);
    this.screenTex = new THREE.CanvasTexture(this.screenTexCanvas); this.screenTex.colorSpace = THREE.SRGBColorSpace;
    const led = new THREE.Mesh(new THREE.CircleGeometry(0.004, 10), new THREE.MeshBasicMaterial({ color: 0xffffff }));
    led.position.set(0.022, 0.06, -0.0048); led.rotation.y = Math.PI; phone.add(led);
    this.ledMat = led.material;
    phone.visible = false;
    this.phone = phone; this.phoneScreen = screen;
    this.root.traverse(m => { if (m.isMesh) m.frustumCulled = false; });
    this.blinkSeed = o.seed * 13.7;
    this.face = { jaw: 0, round: 0, smile: 0, brow: 0, wide: 0, eyesWide: 0, blink: 0, squint: 0 };
    this.eyeTarget = null;
  }

  buildHair(head, mat) {
    // calota: cópia esculpida, expandida, mantendo só a região do couro cabeludo
    const g = new THREE.SphereGeometry(1, 160, 120);
    const p = g.attributes.position; const keep = new Uint8Array(p.count);
    const r = rng(this.o.seed + 3);
    const hairline = (phi) => curve(-Math.abs(phi), [[0, 0.52], [0.55, 0.56], [0.95, 0.4], [1.22, 0.0], [1.4, 0.12], [1.8, 0.12], [2.2, -0.25], [2.7, -0.55], [Math.PI, -0.6]].map(([a, v]) => [-a, v]));
    for (let i = 0; i < p.count; i++) {
      const px = p.getX(i), py = p.getY(i), pz = p.getZ(i);
      const s = sculpt(px, py, pz);
      const phi = Math.atan2(px, pz);
      const hl = hairline(phi);
      keep[i] = py > hl - 0.12 ? 1 : 0;
      const n = new THREE.Vector3(px, py, pz);
      const th = -0.012 + 0.075 * smooth(hl - 0.04, hl + 0.16, py) + 0.03 * smooth(hl + 0.2, 0.9, py)
        + 0.018 * noise1(px * 9 + pz * 7, 3) * smooth(hl, hl + 0.2, py) + 0.012 * noise1(py * 13 + px * 5, 7);
      p.setXYZ(i, s.p[0] + n.x * th, s.p[1] + n.y * th, s.p[2] + n.z * th);
    }
    const idx = g.index.array, nidx = [];
    for (let i = 0; i < idx.length; i += 3) if (keep[idx[i]] && keep[idx[i + 1]] && keep[idx[i + 2]]) nidx.push(idx[i], idx[i + 1], idx[i + 2]);
    g.setIndex(nidx); g.computeVertexNormals();
    const cap = new THREE.Mesh(g, mat); cap.castShadow = true; head.add(cap);
    // mechas: tubos afunilados que caem da coroa (silhueta despenteada)
    const geos = [];
    for (let k = 0; k < 360; k++) {
      const a = r() * Math.PI * 2, el = r.range(0.25, 1.0);
      const start = new THREE.Vector3(Math.cos(a) * Math.cos(el) * 0.62, 0.35 + 0.6 * Math.sin(el), Math.sin(a) * Math.cos(el) * 0.8);
      let dirOut = start.clone().setY(0).normalize();
      if (start.z > 0.1) dirOut = new THREE.Vector3(Math.sign(start.x || 1) * 0.9, 0.25, -0.2).normalize();
      const fwd = new THREE.Vector3(0, 0, start.z > 0.1 ? 0 : -1);
      const len = r.range(0.22, 0.42);
      const pts = [];
      for (let q = 0; q <= 5; q++) {
        const t = q / 5;
        const pt = start.clone()
          .addScaledVector(dirOut, t * len * 0.7)
          .addScaledVector(fwd, t * len * 0.35 * (start.z > 0 ? 1 : -0.2))
          .add(new THREE.Vector3(0, -t * t * len * (start.z > 0.1 ? 0.3 : 0.8), 0));
        // projeta para fora da calota
        const s = sculpt(...pt.clone().normalize().toArray());
        const rad = new THREE.Vector3(...s.p).length() + 0.06 + 0.04 * (1 - t);
        pt.normalize().multiplyScalar(Math.max(pt.length(), rad));
        pts.push(pt);
      }
      const c = new THREE.CatmullRomCurve3(pts);
      const tg = new THREE.TubeGeometry(c, 8, r.range(0.015, 0.03), 4);
      // afunila
      const tp = tg.attributes.position;
      for (let i = 0; i < tp.count; i++) {
        const seg = Math.floor(i / 5) / 8;
        const cpt = c.getPoint(Math.min(1, seg));
        const v = new THREE.Vector3().fromBufferAttribute(tp, i).sub(cpt).multiplyScalar(1 - seg * 0.85).add(cpt);
        tp.setXYZ(i, v.x, v.y, v.z);
      }
      // evita mechas na frente do rosto
      if (start.z > 0.3 && start.y < 0.62) continue;
      geos.push(tg.toNonIndexed());
    }
    const strands = new THREE.Mesh(mergeGeometries(geos), mat); strands.castShadow = true; head.add(strands);
  }

  buildHand(s, sx, mSkin) {
    const wrist = this.j['wrist' + s];
    const hand = new THREE.Group(); wrist.add(hand); this.j['hand' + s] = hand;
    const palm = new THREE.Mesh(new THREE.BoxGeometry(0.075, 0.085, 0.028, 2, 2, 2), mSkin); palm.position.set(0, -0.045, 0);
    palm.geometry.translate(0, 0, 0); hand.add(palm);
    const fingers = [];
    for (let f = 0; f < 4; f++) {
      const fx = (f - 1.5) * 0.019 * sx;
      const base = new THREE.Group(); base.position.set(fx, -0.088, 0); hand.add(base);
      const l1 = 0.036 - Math.abs(f - 1.3) * 0.004, l2 = 0.026;
      const seg1 = new THREE.Mesh(capsule(0.0085, l1, 8).translate(0, -l1 / 2, 0), mSkin); base.add(seg1);
      const j2 = new THREE.Group(); j2.position.y = -l1; base.add(j2);
      const seg2 = new THREE.Mesh(capsule(0.0078, l2, 8).translate(0, -l2 / 2, 0), mSkin); j2.add(seg2);
      fingers.push([base, j2]);
    }
    const thumb = new THREE.Group(); thumb.position.set(sx * 0.036, -0.03, 0.012); thumb.rotation.z = sx * 0.6; thumb.rotation.x = 0.5; hand.add(thumb);
    const t1 = new THREE.Mesh(capsule(0.01, 0.04, 8).translate(0, -0.02, 0), mSkin); thumb.add(t1);
    hand.userData = { fingers, thumb };
    hand.traverse(m => { if (m.isMesh) m.castShadow = true; });
  }

  // curl: 0 aberta, 1 fechada (segurando)
  setGrip(s, curl, thumbIn = 0.5) {
    const h = this.j['hand' + s].userData;
    for (const [b, j2] of h.fingers) { b.rotation.x = curl * 1.2; j2.rotation.x = curl * 1.4; }
    h.thumb.rotation.x = 0.5 + thumbIn * 0.6;
  }

  // ------------- POSES -------------
  resetPose() {
    for (const k in this.j) { this.j[k].rotation.set(0, 0, 0); if (this.j[k].userData.rest) this.j[k].position.copy(this.j[k].userData.rest); }
    this.j.hips.position.y = 0.98;
    this.armsDown(0);
    this.setGrip('L', 0.25); this.setGrip('R', 0.25);
  }

  armsDown(t) {
    for (const [s, sx] of [['L', 1], ['R', -1]]) {
      this.j['shoulder' + s].rotation.set(0.05 + 0.02 * Math.sin(t * 0.9), 0, sx * 0.1);
      this.j['elbow' + s].rotation.set(0.22, 0, 0);
      this.j['wrist' + s].rotation.set(0, sx * 0.2, 0);
    }
  }

  // Respiração e balanço em pé. fear: postura tensa.
  idle(t, { fear = 0, breathRate = 0.25 } = {}) {
    const br = Math.sin(t * Math.PI * 2 * (breathRate + fear * 0.25));
    this.j.chest.rotation.x = -0.02 + br * 0.015 * (1 + fear);
    this.j.chest.scale.set(1 + br * 0.008, 1, 1 + br * 0.012 * (1 + fear));
    this.j.hips.position.x = fbm1(t * 0.3, this.o.seed) * 0.012;
    this.j.hips.rotation.z = fbm1(t * 0.25, this.o.seed + 3) * 0.015;
    this.j.spine.rotation.x = 0.03 + fear * 0.06;
    this.j.neck.rotation.x = fear * 0.04;
    this.j.shoulderL.position.y = 0.19 + fear * 0.015; this.j.shoulderR.position.y = 0.19 + fear * 0.015;
    for (const s of ['L', 'R']) { this.j['thigh' + s].rotation.set(0, 0, 0); this.j['knee' + s].rotation.set(0.04 + fear * 0.05, 0, 0); this.j['ankle' + s].rotation.set(-0.04 - fear * 0.05, 0, 0); }
    this.j.hips.position.y = 0.975 - fear * 0.015;
    this.j.thighL.rotation.z = 0.03; this.j.thighR.rotation.z = -0.03;
  }

  // Caminhada procedural. phase em ciclos (1 = passada completa), careful: passos cautelosos.
  walk(phase, { careful = 0, run = 0 } = {}) {
    const p = phase * Math.PI * 2;
    const ampT = lerp(lerp(0.42, 0.3, careful), 0.75, run);
    const ampK = lerp(lerp(0.75, 0.55, careful), 1.4, run);
    for (const [s, off] of [['L', 0], ['R', Math.PI]]) {
      const q = p + off;
      this.j['thigh' + s].rotation.x = -Math.sin(q) * ampT - run * 0.15;
      const kneeBend = Math.max(0, Math.sin(q - 1.2)) * ampK + 0.08;
      this.j['knee' + s].rotation.x = kneeBend;
      this.j['ankle' + s].rotation.x = Math.sin(q) * 0.2 - kneeBend * 0.25;
      this.j['thigh' + s].rotation.z = (s === 'L' ? 1 : -1) * 0.03;
    }
    this.j.hips.position.y = 0.97 - Math.abs(Math.cos(p)) * lerp(0.025, 0.06, run) * (1 - careful * 0.5) - run * 0.06 - careful * 0.02;
    this.j.hips.rotation.y = Math.sin(p) * 0.08;
    this.j.hips.rotation.z = Math.cos(p) * 0.03;
    this.j.spine.rotation.y = -Math.sin(p) * 0.1;
    this.j.spine.rotation.x = 0.03 + careful * 0.06 + run * 0.25;
    this.j.chest.rotation.y = -Math.sin(p) * 0.06;
    const armA = lerp(lerp(0.3, 0.12, careful), 0.8, run);
    this.j.shoulderL.rotation.set(Math.sin(p) * armA + run * 0.1, 0, 0.1);
    this.j.shoulderR.rotation.set(-Math.sin(p) * armA + run * 0.1, 0, -0.1);
    this.j.elbowL.rotation.x = 0.3 + Math.max(0, Math.sin(p)) * 0.4 + run * 1.0;
    this.j.elbowR.rotation.x = 0.3 + Math.max(0, -Math.sin(p)) * 0.4 + run * 1.0;
  }

  // IK de dois ossos: posiciona a mão (punho) num ponto do mundo, com cotovelo apontando para pole.
  reach(s, target, pole) {
    const sh = this.j['shoulder' + s], el = this.j['elbow' + s];
    this.root.updateMatrixWorld(true);
    const S = new THREE.Vector3().setFromMatrixPosition(sh.matrixWorld);
    const a = this.lenUpper, b = this.lenFore;
    const T = target.clone();
    let d = T.clone().sub(S); const dist = clamp(d.length(), 0.05, a + b - 0.002); d.normalize();
    const cosA = clamp((a * a + dist * dist - b * b) / (2 * a * dist), -1, 1); const sinA = Math.sqrt(1 - cosA * cosA);
    const P = pole.clone().sub(S); const pd = P.sub(d.clone().multiplyScalar(P.dot(d))).normalize();
    const E = S.clone().addScaledVector(d, a * cosA).addScaledVector(pd, a * sinA);
    const Tn = S.clone().addScaledVector(d, dist);
    const setWorld = (joint, from, to, bendRef) => {
      const Y = from.clone().sub(to).normalize(); // eixo +Y local aponta para o pai
      let Z = bendRef.clone().sub(Y.clone().multiplyScalar(bendRef.dot(Y)));
      if (Z.lengthSq() < 1e-8) Z = new THREE.Vector3(0, 0, 1); Z.normalize();
      const X = new THREE.Vector3().crossVectors(Y, Z).normalize();
      Z.crossVectors(X, Y);
      const m = new THREE.Matrix4().makeBasis(X, Y, Z);
      const qw = new THREE.Quaternion().setFromRotationMatrix(m);
      const pq = new THREE.Quaternion(); joint.parent.getWorldQuaternion(pq);
      joint.quaternion.copy(pq.invert().multiply(qw));
      joint.updateMatrixWorld(true);
    };
    setWorld(sh, S, E, Tn.clone().sub(E));
    // antebraço: dobra no mesmo plano (o "Z" do braço aponta para a mão)
    const fwd = S.clone().sub(E).normalize().negate();
    setWorld(el, E, Tn, fwd.clone().negate().add(pd.clone().multiplyScalar(-0.001)));
  }

  // Orienta o punho no mundo (direção dos dedos e da palma).
  orientHand(s, fingersDir, palmDir) {
    const w = this.j['wrist' + s];
    const Y = fingersDir.clone().normalize().negate();
    let Z = palmDir.clone().sub(Y.clone().multiplyScalar(palmDir.dot(Y))).normalize();
    const X = new THREE.Vector3().crossVectors(Y, Z).normalize(); Z.crossVectors(X, Y);
    const qw = new THREE.Quaternion().setFromRotationMatrix(new THREE.Matrix4().makeBasis(X, Y, Z));
    const pq = new THREE.Quaternion(); w.parent.getWorldQuaternion(pq);
    w.quaternion.copy(pq.invert().multiply(qw));
  }

  // Cabeça olha para um ponto (dividido entre pescoço e cabeça), olhos acompanham.
  look(target, weight = 1, { maxYaw = 1.3, maxPitch = 0.6 } = {}) {
    this.root.updateMatrixWorld(true);
    const hw = new THREE.Vector3().setFromMatrixPosition(this.j.neck.matrixWorld);
    const dir = target.clone().sub(hw);
    const inv = new THREE.Quaternion(); this.j.chest.getWorldQuaternion(inv); inv.invert();
    dir.applyQuaternion(inv);
    const yaw = clamp(Math.atan2(dir.x, dir.z), -maxYaw, maxYaw) * weight;
    const pitch = clamp(-Math.atan2(dir.y, Math.hypot(dir.x, dir.z)), -maxPitch, maxPitch) * weight;
    this.j.neck.rotation.y = yaw * 0.45; this.j.neck.rotation.x += pitch * 0.4;
    this.j.head.rotation.y = yaw * 0.55; this.j.head.rotation.x = pitch * 0.6;
    this.eyeTarget = target;
  }

  // ------------- ROSTO -------------
  updateFace(t, f = {}) {
    const F = Object.assign({ jaw: 0, round: 0, smile: 0, brow: 0, wide: 0, eyesWide: 0, blink: null, squint: 0 }, f);
    const inf = this.headMesh.morphTargetInfluences;
    inf[0] = clamp(F.jaw); inf[1] = clamp(F.smile); inf[2] = clamp(F.brow); inf[3] = clamp(F.round); inf[4] = clamp(F.wide);
    this.teeth.position.y = -0.455; this.teeth.visible = F.jaw > 0.05 || F.wide > 0.2;
    // piscar determinístico
    let blink = F.blink;
    if (blink === null) {
      const period = 3.6, ph = (t + this.blinkSeed) / period, k = Math.floor(ph), fr = (ph - k) * period;
      const jitter = (Math.sin(k * 91.7) * 0.5 + 0.5) * 1.6;
      const bt = fr - jitter;
      blink = bt > 0 && bt < 0.16 ? Math.sin(bt / 0.16 * Math.PI) : 0;
    }
    const open = 1 - blink;
    const upRest = -0.32 - F.eyesWide * 0.3 + F.squint * 0.25;
    for (const e of this.eyes) {
      e.upPivot.rotation.x = lerp(1.62, upRest, open);
      e.loPivot.rotation.x = lerp(-0.05, 0.5 + F.eyesWide * 0.12 - F.squint * 0.2, open);
      e.brow.position.y = F.brow * 0.06 + F.eyesWide * 0.02;
      e.brow.rotation.z = -e.side * F.brow * 0.12;
      // olhos para o alvo
      if (this.eyeTarget) {
        this.root.updateMatrixWorld(true);
        const wp = new THREE.Vector3(); e.holder.getWorldPosition(wp);
        const d = this.eyeTarget.clone().sub(wp);
        const q = new THREE.Quaternion(); e.holder.getWorldQuaternion(q); d.applyQuaternion(q.invert());
        e.eye.rotation.y = clamp(Math.atan2(d.x, d.z), -0.5, 0.5);
        e.eye.rotation.x = clamp(-Math.atan2(d.y, Math.hypot(d.x, d.z)), -0.4, 0.4);
      }
      // micro-sacadas
      e.eye.rotation.y += noise1(Math.floor(t * 3) * 0.7, 4) * 0.025;
    }
  }

  // Abertura de boca a partir do lipsync (array por quadro [abertura, arredondamento])
  lipsAt(data, localT, fps = 24) {
    if (!data || localT < 0) return { jaw: 0, round: 0 };
    const i = Math.floor(localT * fps); if (i >= data.length) return { jaw: 0, round: 0 };
    const [o, r] = data[i]; const [o2, r2] = data[Math.min(data.length - 1, i + 1)];
    const fr = localT * fps - i;
    return { jaw: lerp(o, o2, fr) * 0.85, round: lerp(r, r2, fr) * 0.6 * lerp(o, o2, fr) };
  }

  // Celular na mão direita: 'ear' (ligação), 'front' (lanterna à frente), 'look' (olhando a tela)
  attachPhone(mode) {
    const hand = this.j.handR;
    if (this.phone.parent !== hand) hand.add(this.phone);
    this.phone.visible = !!mode;
    this.phone.position.set(-0.005, -0.075, 0.03);
    this.phone.rotation.set(0, 0, 0);
    if (mode === 'ear') { this.phone.rotation.set(0.2, Math.PI, 0); this.phone.position.set(0.0, -0.08, 0.02); }
    if (mode === 'front') { this.phone.rotation.set(-Math.PI / 2 + 0.15, Math.PI, 0); this.phone.position.set(0, -0.07, 0.03); }
    if (mode === 'look') { this.phone.rotation.set(-0.2, 0, 0); this.phone.position.set(0, -0.08, 0.035); }
  }
}
