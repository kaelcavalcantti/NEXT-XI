import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

// ---------- deterministic random ----------
let seed = 7;
export function rnd() { seed |= 0; seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }
export const R = (a, b) => a + (b - a) * rnd();
export const pick = a => a[Math.floor(rnd() * a.length)];

// ---------- easing / keys ----------
export const clamp = (x, a = 0, b = 1) => Math.min(b, Math.max(a, x));
export const lerp = (a, b, t) => a + (b - a) * t;
export const sm = (a, b, t) => { const x = clamp((t - a) / (b - a)); return x * x * (3 - 2 * x); };
export const eio = x => x < .5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2;
export const eo = x => 1 - Math.pow(1 - x, 3);
export const lin = x => x;
export function keys(t, k, ease = eio) {
  if (t <= k[0][0]) return k[0][1];
  if (t >= k[k.length - 1][0]) return k[k.length - 1][1];
  for (let i = 0; i < k.length - 1; i++) {
    if (t < k[i + 1][0]) {
      const u = ease((t - k[i][0]) / (k[i + 1][0] - k[i][0]));
      const a = k[i][1], b = k[i + 1][1];
      return Array.isArray(a) ? a.map((v, j) => lerp(v, b[j], u)) : lerp(a, b, u);
    }
  }
}
// bounce-in 0..1
export const pop = x => { x = clamp(x); return x === 0 ? 0 : 1 + 2.2 * Math.pow(x - 1, 3) + 1.2 * Math.pow(x - 1, 2) * 0 + Math.sin(x * Math.PI) * 0.18 * (1 - x); };

// ---------- shared geometry ----------
export const BOX = new THREE.BoxGeometry(1, 1, 1);
export const CYL = new THREE.CylinderGeometry(1, 1, 1, 20);
export const CYL8 = new THREE.CylinderGeometry(1, 1, 1, 8);
export const SPH = new THREE.SphereGeometry(1, 22, 16);
export const CONE = new THREE.ConeGeometry(1, 1, 20);
export const rbox = (w, h, d, r = .1, s = 3) => new RoundedBoxGeometry(w, h, d, s, r);

// ---------- materials ----------
export function mat(color, o = {}) {
  const m = new THREE.MeshStandardMaterial({
    color, roughness: o.r ?? 0.8, metalness: o.m ?? 0, map: o.map || null,
    bumpMap: o.bump || null, bumpScale: o.bs ?? 1.5,
    emissive: o.e ?? 0x000000, emissiveIntensity: o.ei ?? 1, emissiveMap: o.emap || null,
    transparent: !!o.tr, opacity: o.op ?? 1, side: o.side ?? THREE.FrontSide,
    alphaTest: o.at ?? 0, depthWrite: o.dw ?? true,
  });
  if (o.env !== undefined) m.envMapIntensity = o.env;
  return m;
}

// ---------- canvas textures ----------
export function cv(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return [c, c.getContext('2d')]; }
export function tex(c, srgb = true) {
  const t = new THREE.CanvasTexture(c); t.wrapS = t.wrapT = THREE.RepeatWrapping;
  if (srgb) t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8; return t;
}
const hsl = (h, s, l, a = 1) => `hsla(${h},${s}%,${l}%,${a})`;

function speckle(x, w, h, n, dark = .12, light = .08) {
  for (let i = 0; i < n; i++) {
    x.fillStyle = rnd() < .5 ? `rgba(0,0,0,${rnd() * dark})` : `rgba(255,235,210,${rnd() * light})`;
    x.fillRect(rnd() * w, rnd() * h, 1 + rnd() * 3, 1 + rnd() * 3);
  }
}

export function brickTex(H = 12, S = 55, L = 38) {
  const [c, x] = cv(512, 512); const [b, y] = cv(512, 512);
  x.fillStyle = hsl(32, 18, 58); x.fillRect(0, 0, 512, 512);
  y.fillStyle = '#222'; y.fillRect(0, 0, 512, 512);
  const bh = 32, bw = 64;
  for (let r = 0; r < 16; r++) {
    const off = (r % 2) * bw / 2;
    for (let col = -1; col < 9; col++) {
      const px = col * bw + off, py = r * bh;
      const l = L + (rnd() - .5) * 14, h = H + (rnd() - .5) * 10, s = S + (rnd() - .5) * 15;
      x.fillStyle = hsl(h, s, l); x.fillRect(px + 3, py + 3, bw - 6, bh - 6);
      x.fillStyle = hsl(h, s, l + 7, .5); x.fillRect(px + 3, py + 3, bw - 6, 4);
      x.fillStyle = hsl(h, s, l - 10, .5); x.fillRect(px + 3, py + bh - 8, bw - 6, 5);
      for (let k = 0; k < 10; k++) { x.fillStyle = `rgba(0,0,0,${rnd() * .18})`; x.fillRect(px + 3 + rnd() * (bw - 8), py + 3 + rnd() * (bh - 8), 2 + rnd() * 5, 1 + rnd() * 3); }
      const g = 170 + rnd() * 70 | 0; y.fillStyle = `rgb(${g},${g},${g})`; y.fillRect(px + 3, py + 3, bw - 6, bh - 6);
    }
  }
  speckle(x, 512, 512, 1500);
  return { map: tex(c), bump: tex(b, false) };
}

export function plasterTex(H, S, L) {
  const [c, x] = cv(512, 512); const [b, y] = cv(512, 512);
  x.fillStyle = hsl(H, S, L); x.fillRect(0, 0, 512, 512);
  y.fillStyle = '#888'; y.fillRect(0, 0, 512, 512);
  for (let i = 0; i < 260; i++) {
    const px = rnd() * 512, py = rnd() * 512, r = 10 + rnd() * 60;
    const g = x.createRadialGradient(px, py, 0, px, py, r);
    const d = rnd() < .5 ? -1 : 1;
    g.addColorStop(0, hsl(H + d * 3, S + d * 4, L + d * (3 + rnd() * 6), .45)); g.addColorStop(1, hsl(H, S, L, 0));
    x.fillStyle = g; x.fillRect(px - r, py - r, r * 2, r * 2);
    const gg = y.createRadialGradient(px, py, 0, px, py, r * .6); const v = 100 + rnd() * 80 | 0;
    gg.addColorStop(0, `rgba(${v},${v},${v},.5)`); gg.addColorStop(1, 'rgba(136,136,136,0)'); y.fillStyle = gg; y.fillRect(px - r, py - r, r * 2, r * 2);
  }
  // patches of exposed brick
  for (let i = 0; i < 3; i++) {
    const px = rnd() * 440, py = rnd() * 440;
    for (let r = 0; r < 3; r++) for (let k = 0; k < 3; k++) {
      x.fillStyle = hsl(14, 50, 40 + rnd() * 8, .8); x.fillRect(px + k * 22 + (r % 2) * 11, py + r * 11, 19, 8);
    }
  }
  // cracks
  for (let i = 0; i < 6; i++) {
    let px = rnd() * 512, py = rnd() * 512; x.strokeStyle = hsl(H, S - 10, L - 25, .5); x.lineWidth = 1.2; x.beginPath(); x.moveTo(px, py);
    for (let k = 0; k < 12; k++) { px += (rnd() - .5) * 18; py += rnd() * 12; x.lineTo(px, py); } x.stroke();
  }
  speckle(x, 512, 512, 2500, .1, .1);
  return { map: tex(c), bump: tex(b, false) };
}

export function concreteTex(L = 50, H = 30) {
  const [c, x] = cv(512, 512);
  x.fillStyle = hsl(H, 10, L); x.fillRect(0, 0, 512, 512);
  for (let i = 0; i < 300; i++) {
    const px = rnd() * 512, py = rnd() * 512, r = 8 + rnd() * 50; const g = x.createRadialGradient(px, py, 0, px, py, r);
    g.addColorStop(0, hsl(H, 10, L + (rnd() - .5) * 14, .35)); g.addColorStop(1, hsl(H, 10, L, 0)); x.fillStyle = g; x.fillRect(px - r, py - r, 2 * r, 2 * r);
  }
  x.strokeStyle = hsl(H, 10, L - 18, .8); x.lineWidth = 2; x.strokeRect(1, 1, 510, 510);
  speckle(x, 512, 512, 3000);
  return tex(c);
}

export function asphaltTex() {
  const [c, x] = cv(512, 512);
  x.fillStyle = '#3b332e'; x.fillRect(0, 0, 512, 512);
  for (let i = 0; i < 9000; i++) { const v = 35 + rnd() * 50 | 0; x.fillStyle = `rgba(${v + 8},${v},${v - 4},.6)`; x.fillRect(rnd() * 512, rnd() * 512, 1 + rnd() * 2, 1 + rnd() * 2); }
  for (let i = 0; i < 5; i++) { x.strokeStyle = 'rgba(20,16,14,.6)'; x.lineWidth = 3; x.beginPath(); let px = rnd() * 512, py = rnd() * 512; x.moveTo(px, py); for (let k = 0; k < 10; k++) { px += (rnd() - .5) * 40; py += (rnd() - .5) * 40; x.lineTo(px, py); } x.stroke(); }
  return tex(c);
}

export function cobbleTex() {
  const [c, x] = cv(512, 512);
  x.fillStyle = '#4a3c33'; x.fillRect(0, 0, 512, 512);
  for (let r = 0; r < 12; r++) for (let k = 0; k < 10; k++) {
    const px = k * 52 + (r % 2) * 26 + R(-3, 3), py = r * 43 + R(-3, 3);
    x.fillStyle = hsl(25 + R(-8, 8), 18, 40 + R(-8, 10)); x.beginPath(); x.ellipse(px + 24, py + 20, 22, 18, R(-.3, .3), 0, Math.PI * 2); x.fill();
    x.fillStyle = 'rgba(255,220,180,.12)'; x.beginPath(); x.ellipse(px + 20, py + 14, 12, 7, 0, 0, Math.PI * 2); x.fill();
  }
  speckle(x, 512, 512, 2000);
  return tex(c);
}

export function woodTex(H = 28, L = 38) {
  const [c, x] = cv(256, 256);
  x.fillStyle = hsl(H, 45, L); x.fillRect(0, 0, 256, 256);
  for (let i = 0; i < 4; i++) { x.fillStyle = hsl(H, 40, L - 12); x.fillRect(0, i * 64, 256, 3); }
  for (let i = 0; i < 160; i++) { x.strokeStyle = hsl(H, 40, L + R(-10, 8), .35); x.lineWidth = R(.5, 2); x.beginPath(); const y0 = R(0, 256); x.moveTo(0, y0); x.bezierCurveTo(80, y0 + R(-6, 6), 170, y0 + R(-6, 6), 256, y0); x.stroke(); }
  return tex(c);
}

export function tileTex() {
  const [c, x] = cv(256, 256);
  for (let r = 0; r < 8; r++) for (let k = 0; k < 8; k++) {
    x.fillStyle = (r + k) % 2 ? '#E2B13A' : (r % 3 === 0 ? '#2F5D9E' : '#D9A030');
    x.fillRect(k * 32, r * 32, 32, 32); x.strokeStyle = 'rgba(60,30,10,.5)'; x.strokeRect(k * 32 + .5, r * 32 + .5, 31, 31);
    x.fillStyle = 'rgba(255,255,255,.15)'; x.fillRect(k * 32 + 3, r * 32 + 3, 12, 5);
  }
  return tex(c);
}

export function stripeTex(a, b, n = 8) {
  const [c, x] = cv(256, 64);
  for (let i = 0; i < n; i++) { x.fillStyle = i % 2 ? a : b; x.fillRect(i * 256 / n, 0, 256 / n, 64); }
  speckle(x, 256, 64, 300, .15, .05);
  return tex(c);
}

export function papelTex(color) {
  const [c, x] = cv(128, 160);
  x.fillStyle = color; x.fillRect(0, 0, 128, 150);
  x.globalCompositeOperation = 'destination-out';
  for (let i = 0; i < 9; i++) { x.beginPath(); x.arc(i * 16 + 8, 154, 9, 0, Math.PI * 2); x.fill(); }
  x.beginPath(); x.moveTo(64, 30); x.lineTo(90, 70); x.lineTo(64, 110); x.lineTo(38, 70); x.closePath(); x.fill();
  for (let i = 0; i < 6; i++) { x.beginPath(); x.arc(20 + (i % 3) * 44, i < 3 ? 22 : 122, 7, 0, Math.PI * 2); x.fill(); }
  for (let i = 0; i < 4; i++) { x.fillRect(12 + i * 32, 60, 6, 20); }
  x.globalCompositeOperation = 'source-over';
  x.fillStyle = color; x.beginPath(); x.arc(64, 70, 10, 0, Math.PI * 2); x.fill();
  const t = tex(c); return t;
}

export function textTex(lines, o = {}) {
  const w = o.w ?? 1024, h = o.h ?? 256; const [c, x] = cv(w, h);
  if (o.bg) { x.fillStyle = o.bg; x.fillRect(0, 0, w, h); }
  if (o.grain !== false && o.bg) speckle(x, w, h, w * h / 60, .12, .08);
  if (o.border) { x.strokeStyle = o.border; x.lineWidth = o.bw ?? 12; x.strokeRect(o.bw / 2 || 6, o.bw / 2 || 6, w - (o.bw || 12), h - (o.bw || 12)); }
  x.textAlign = 'center'; x.textBaseline = 'middle';
  let yy = o.y0 ?? h / 2 - (lines.length - 1) * (o.lh ?? 0) / 2;
  for (const L of lines) {
    x.font = L.font; x.fillStyle = L.color ?? '#fff';
    if (L.stroke) { x.lineWidth = L.sw ?? 10; x.strokeStyle = L.stroke; x.lineJoin = 'round'; x.strokeText(L.t, w / 2, L.y ?? yy); }
    x.fillText(L.t, w / 2, L.y ?? yy); yy += o.lh ?? 0;
  }
  return tex(c);
}

export function portraitTex() {
  const [c, x] = cv(256, 320);
  const g = x.createLinearGradient(0, 0, 0, 320); g.addColorStop(0, '#E9C98F'); g.addColorStop(1, '#C9894E'); x.fillStyle = g; x.fillRect(0, 0, 256, 320);
  // flowers (marigold)
  for (let i = 0; i < 7; i++) { x.fillStyle = i % 2 ? '#E8A33A' : '#D9503A'; x.beginPath(); x.arc(20 + i * 36, 28 + (i % 2) * 12, 13, 0, 7); x.fill(); }
  x.fillStyle = '#2E7F7A'; x.beginPath(); x.ellipse(128, 330, 112, 92, 0, 0, 7); x.fill(); // blouse
  x.fillStyle = '#F2E6D0'; x.beginPath(); x.ellipse(128, 250, 34, 16, 0, 0, 7); x.fill(); // collar
  x.fillStyle = '#B57B55'; x.fillRect(110, 200, 36, 46); // neck
  x.fillStyle = '#8f8a86'; x.beginPath(); x.ellipse(128, 128, 70, 80, 0, 0, 7); x.fill(); // hair back
  x.beginPath(); x.arc(128, 52, 28, 0, 7); x.fill(); // bun
  x.fillStyle = '#C48B60'; x.beginPath(); x.ellipse(128, 145, 54, 66, 0, 0, 7); x.fill(); // face
  x.fillStyle = '#a8a29c'; x.beginPath(); x.ellipse(128, 92, 58, 28, 0, Math.PI, 0); x.fill();
  x.strokeStyle = '#3a2418'; x.lineWidth = 4; x.lineCap = 'round';
  x.beginPath(); x.arc(106, 140, 10, Math.PI * 1.1, Math.PI * 1.9); x.stroke();
  x.beginPath(); x.arc(150, 140, 10, Math.PI * 1.1, Math.PI * 1.9); x.stroke();
  x.beginPath(); x.arc(128, 168, 22, Math.PI * .15, Math.PI * .85); x.stroke();
  x.fillStyle = 'rgba(220,90,80,.35)'; x.beginPath(); x.arc(96, 165, 11, 0, 7); x.arc(160, 165, 11, 0, 7); x.fill();
  x.fillStyle = '#E3B23C'; x.beginPath(); x.arc(74, 162, 5, 0, 7); x.arc(182, 162, 5, 0, 7); x.fill();
  speckle(x, 256, 320, 1500, .08, .06);
  return tex(c);
}

export function billTex() {
  const [c, x] = cv(256, 340);
  x.fillStyle = '#F4EEDD'; x.fillRect(0, 0, 256, 340);
  x.fillStyle = '#2b2b2b'; x.textAlign = 'center'; x.font = 'bold 20px "Bebas Neue"'; x.fillText('CLÍNICA RENAL DEL SOL', 128, 34);
  x.font = '13px sans-serif'; x.fillText('Guadalajara, Jal.', 128, 52);
  x.textAlign = 'left'; x.font = '14px sans-serif';
  const it = ['Hemodiálisis x12', 'Medicamentos', 'Laboratorio', 'Consultas', 'Trasplante (dep.)'];
  it.forEach((s, i) => { x.fillText(s, 18, 92 + i * 26); x.textAlign = 'right'; x.fillText('$ ' + (12 + i * 37) + ',500', 238, 92 + i * 26); x.textAlign = 'left'; });
  x.fillRect(18, 228, 220, 2);
  x.font = '30px Anton'; x.fillStyle = '#C0201A'; x.fillText('TOTAL', 18, 268); x.textAlign = 'right'; x.fillText('$480,000', 238, 268);
  x.save(); x.translate(150, 310); x.rotate(-.2); x.strokeStyle = '#C0201A'; x.lineWidth = 3; x.strokeRect(-60, -18, 120, 34); x.font = '24px Anton'; x.textAlign = 'center'; x.fillText('URGENTE', 0, 8); x.restore();
  speckle(x, 256, 340, 800, .06, .02);
  return tex(c);
}

export function moneyTex() {
  const [c, x] = cv(256, 128);
  x.fillStyle = '#86A86E'; x.fillRect(0, 0, 256, 128);
  x.strokeStyle = '#3F5E36'; x.lineWidth = 4; x.strokeRect(6, 6, 244, 116);
  x.fillStyle = '#5E8250'; x.beginPath(); x.ellipse(128, 64, 34, 42, 0, 0, 7); x.fill();
  x.fillStyle = '#E9E4C8'; x.font = '44px Anton'; x.textAlign = 'center'; x.textBaseline = 'middle'; x.fillText('$', 128, 66);
  x.fillStyle = '#2E4A28'; x.font = '26px Anton'; x.fillText('1000', 40, 30); x.fillText('1000', 216, 98);
  x.fillStyle = '#E9E4C8'; x.fillRect(0, 50, 256, 0); return tex(c);
}
export function edgeTex() {
  const [c, x] = cv(64, 128); for (let i = 0; i < 64; i++) { x.fillStyle = i % 2 ? '#7E9E66' : '#D9DCC3'; x.fillRect(0, i * 2, 64, 2); } return tex(c);
}

// ---------- batcher ----------
export class Batch {
  constructor() { this.m = new Map(); }
  add(geo, material, p = [0, 0, 0], r = [0, 0, 0], s = [1, 1, 1], uv = 0) {
    let g = geo.index ? geo.toNonIndexed() : geo.clone();
    for (const k of Object.keys(g.attributes)) if (!['position', 'normal', 'uv'].includes(k)) g.deleteAttribute(k);
    g.clearGroups();
    const m = new THREE.Matrix4().compose(new THREE.Vector3(...p), new THREE.Quaternion().setFromEuler(new THREE.Euler(...r)), new THREE.Vector3(...s));
    g.applyMatrix4(m);
    if (uv) {
      const P = g.attributes.position, N = g.attributes.normal, U = g.attributes.uv;
      for (let i = 0; i < P.count; i++) {
        const ax = Math.abs(N.getX(i)), ay = Math.abs(N.getY(i)), az = Math.abs(N.getZ(i));
        let u, v; if (ax >= ay && ax >= az) { u = P.getZ(i); v = P.getY(i); } else if (ay >= az) { u = P.getX(i); v = P.getZ(i); } else { u = P.getX(i); v = P.getY(i); }
        U.setXY(i, u * uv, v * uv);
      }
    }
    if (!this.m.has(material)) this.m.set(material, []);
    this.m.get(material).push(g);
  }
  build(cast = true, recv = true) {
    const grp = new THREE.Group();
    for (const [m, list] of this.m) { const mesh = new THREE.Mesh(mergeGeometries(list), m); mesh.castShadow = cast; mesh.receiveShadow = recv; grp.add(mesh); }
    return grp;
  }
}

// add a single mesh helper
export function mesh(geo, m, parent, p = [0, 0, 0], s = [1, 1, 1], r = [0, 0, 0], shadow = true) {
  const x = new THREE.Mesh(geo, m); x.position.set(...p); x.scale.set(...s); x.rotation.set(...r);
  x.castShadow = shadow; x.receiveShadow = shadow; if (parent) parent.add(x); return x;
}
export function grp(parent, p = [0, 0, 0], r = [0, 0, 0]) { const g = new THREE.Group(); g.position.set(...p); g.rotation.set(...r); if (parent) parent.add(g); return g; }

// ---------- sky ----------
const skyVS = `varying vec3 vDir; void main(){ vDir = position; gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.); gl_Position.z = gl_Position.w*0.99999; }`;
const skyFS = `
uniform vec3 sunDir; uniform float linearOut; uniform float darken;
varying vec3 vDir;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
float fbm(vec2 p){float v=0.,a=.5;for(int i=0;i<5;i++){v+=a*noise(p);p*=2.03;a*=.5;}return v;}
void main(){
  vec3 d=normalize(vDir); float h=d.y; float az=atan(d.x,d.z);
  vec3 haze=vec3(0.98,0.70,0.42); vec3 red=vec3(0.878,0.282,0.118); vec3 deep=vec3(0.74,0.21,0.10); vec3 violet=vec3(0.36,0.17,0.34);
  vec3 c=mix(haze,red,smoothstep(-0.01,0.20,h));
  c=mix(c,deep,smoothstep(0.32,0.62,h));
  c=mix(c,violet,smoothstep(0.66,0.93,h));
  float band=exp(-pow((h-0.07)/0.035,2.))*1.0+exp(-pow((h-0.17)/0.05,2.))*0.95+exp(-pow((h-0.31)/0.06,2.))*0.85+exp(-pow((h-0.47)/0.07,2.))*0.6;
  float n=fbm(vec2(az*3.0,h*14.0));
  float n2=fbm(vec2(az*11.0,h*45.0+3.));
  float cl=band*(n*1.0+n2*0.35);
  float body=smoothstep(0.52,0.70,cl);
  float edge=smoothstep(0.42,0.55,cl)-body;
  vec2 sxz=normalize(sunDir.xz); float sunAmt=max(dot(normalize(d.xz+1e-4),sxz),0.);
  vec3 cloudCol=mix(vec3(0.60,0.15,0.08),vec3(0.82,0.30,0.13),sunAmt);
  vec3 gold=vec3(1.0,0.80,0.36);
  c=mix(c,cloudCol,body*0.82);
  c+=gold*max(edge,0.)*(0.35+0.75*sunAmt);
  float sd=max(dot(d,normalize(sunDir)),0.);
  c+=vec3(1.,0.86,0.55)*smoothstep(0.9993,0.9997,sd)*1.2+vec3(1.,0.6,0.28)*pow(sd,10.)*0.30;
  c=mix(c,haze,(1.-smoothstep(0.0,0.10,h))*0.45);
  if(h<0.0) c=haze*0.92;
  c*=darken;
  c=min(c,vec3(1.));
  if(linearOut>0.5) c=pow(c,vec3(2.2));
  gl_FragColor=vec4(c,1.);
}`;
export function makeSky(sunDir, linear = false) {
  const m = new THREE.ShaderMaterial({ uniforms: { sunDir: { value: new THREE.Vector3(...sunDir).normalize() }, linearOut: { value: linear ? 1 : 0 }, darken: { value: 1 } }, vertexShader: skyVS, fragmentShader: skyFS, side: THREE.BackSide, depthWrite: false, fog: false, toneMapped: false });
  const s = new THREE.Mesh(new THREE.SphereGeometry(900, 48, 32), m); s.frustumCulled = false; s.renderOrder = -1; return s;
}

// ---------- lights ----------
export function addLights(scene, sunDir, o = {}) {
  const dir = new THREE.Vector3(...sunDir).normalize();
  const key = new THREE.DirectionalLight(o.color ?? 0xffb26e, o.key ?? 3.4);
  key.castShadow = true; key.shadow.mapSize.set(2048, 2048); key.shadow.bias = -0.0003; key.shadow.normalBias = 0.03;
  const c = key.shadow.camera; c.near = 1; c.far = 400; scene.add(key); scene.add(key.target);
  const fill = new THREE.DirectionalLight(0x8aa8ea, (o.key ?? 3.4) * 0.25);
  fill.position.set(-dir.x * 50, 30, -dir.z * 50); scene.add(fill);
  const hemi = new THREE.HemisphereLight(0xff9a6a, 0x2a1c18, o.hemi ?? 0.35); scene.add(hemi);
  key.follow = (p, ext = 30) => {
    key.target.position.set(...p); key.position.set(p[0] + dir.x * 150, p[1] + dir.y * 150, p[2] + dir.z * 150);
    c.left = -ext; c.right = ext; c.top = ext; c.bottom = -ext; c.updateProjectionMatrix();
  };
  return { key, fill, hemi };
}

// ---------- outline ----------
export const outlineMat = new THREE.ShaderMaterial({
  uniforms: { th: { value: 0.011 } },
  vertexShader: `uniform float th; void main(){ vec4 mv=modelViewMatrix*vec4(position,1.); vec3 n=normalize(normalMatrix*normal); mv.xyz+=n*th; gl_Position=projectionMatrix*mv; }`,
  fragmentShader: `void main(){ gl_FragColor=vec4(0.03,0.015,0.01,1.); }`, side: THREE.BackSide,
});
export function addOutline(root) {
  const list = []; root.traverse(o => { if (o.isMesh && !o.userData.noOutline) list.push(o); });
  for (const o of list) { const m = new THREE.Mesh(o.geometry, outlineMat); m.userData.noOutline = true; m.castShadow = false; o.add(m); }
}
