// Texturas procedurais (canvas 2D): madeira, papel de parede, telhas, reboco, asfalto molhado, casca...
// Cada função devolve { map, normalMap, roughnessMap } quando aplicável. Tudo determinístico.
import * as THREE from 'three';
import { rng } from '../lib/util.js';

const cache = new Map();

export function canvas(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return c; }

function hash2(x, y, s) { const v = Math.sin(x * 127.1 + y * 311.7 + s * 74.7) * 43758.5453; return v - Math.floor(v); }
// ruído de valor periódico (tileável)
export function vnoise(x, y, period, seed = 0) {
  const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
  const u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
  const p = period;
  const a = hash2(((xi % p) + p) % p, ((yi % p) + p) % p, seed), b = hash2((((xi + 1) % p) + p) % p, ((yi % p) + p) % p, seed);
  const c = hash2(((xi % p) + p) % p, (((yi + 1) % p) + p) % p, seed), d = hash2((((xi + 1) % p) + p) % p, (((yi + 1) % p) + p) % p, seed);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}
export function fbm(x, y, period, oct = 5, seed = 0) {
  let s = 0, amp = 0.5, f = 1, n = 0;
  for (let o = 0; o < oct; o++) { s += amp * vnoise(x * f, y * f, period * f, seed + o * 7); n += amp; amp *= 0.5; f *= 2; }
  return s / n;
}

// Gera um campo (Float32Array) e converte em mapas.
function field(w, h, fn) { const a = new Float32Array(w * h); for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) a[y * w + x] = fn(x, y); return a; }

function toTexture(c, { srgb = true, repeat = [1, 1] } = {}) {
  const t = new THREE.CanvasTexture(c);
  t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(repeat[0], repeat[1]);
  t.colorSpace = srgb ? THREE.SRGBColorSpace : THREE.NoColorSpace;
  t.anisotropy = 4;
  t.needsUpdate = true;
  return t;
}

function colorCanvas(w, h, fn) {
  const c = canvas(w, h), ctx = c.getContext('2d'), img = ctx.createImageData(w, h);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const [r, g, b] = fn(x, y); const i = (y * w + x) * 4;
    img.data[i] = r; img.data[i + 1] = g; img.data[i + 2] = b; img.data[i + 3] = 255;
  }
  ctx.putImageData(img, 0, 0); return c;
}

function normalFromHeight(hf, w, h, strength = 2) {
  return colorCanvas(w, h, (x, y) => {
    const l = hf[y * w + ((x - 1 + w) % w)], r = hf[y * w + ((x + 1) % w)];
    const u = hf[((y - 1 + h) % h) * w + x], d = hf[((y + 1) % h) * w + x];
    let nx = (l - r) * strength, ny = (d - u) * strength, nz = 1;
    const len = Math.hypot(nx, ny, nz); nx /= len; ny /= len; nz /= len;
    return [(nx * 0.5 + 0.5) * 255, (ny * 0.5 + 0.5) * 255, (nz * 0.5 + 0.5) * 255];
  });
}

function grayCanvas(f, w, h, lo = 0, hi = 1) {
  return colorCanvas(w, h, (x, y) => { const v = Math.max(0, Math.min(1, lo + (hi - lo) * f[y * w + x])) * 255; return [v, v, v]; });
}

function material(key, w, h, build, opts) {
  if (cache.has(key)) return cache.get(key);
  const { color, height, rough, normalStrength = 2 } = build(w, h);
  const res = {
    map: toTexture(colorCanvas(w, h, color), { repeat: opts?.repeat }),
    normalMap: height ? toTexture(normalFromHeight(height, w, h, normalStrength), { srgb: false, repeat: opts?.repeat }) : null,
    roughnessMap: rough ? toTexture(grayCanvas(rough, w, h), { srgb: false, repeat: opts?.repeat }) : null,
  };
  cache.set(key, res);
  return res;
}

const mix = (a, b, t) => a + (b - a) * t;
const mixc = (a, b, t) => [mix(a[0], b[0], t), mix(a[1], b[1], t), mix(a[2], b[2], t)];

// ---------- Madeira (tábuas de assoalho / paredes) ----------
export function woodPlanks({ planks = 6, base = [92, 64, 42], dark = [48, 32, 22], seed = 1, size = 512, worn = 0.4 } = {}) {
  return material(`wood${planks}-${seed}-${base}`, size, size, (w, h) => {
    const r = rng(seed); const offs = Array.from({ length: planks }, () => [r(), r() * 0.25 - 0.12, r()]);
    const height = field(w, h, (x, y) => {
      const pw = w / planks, pi = Math.floor(x / pw), px = (x % pw) / pw;
      const seam = Math.min(px, 1 - px) < 0.025 ? 0 : 1;
      const yy = (y / h + offs[pi][0]) % 1; const endSeam = Math.abs(yy - 0.5) < 0.004 && pi % 2 ? 0 : 1;
      return 0.5 * seam * endSeam + 0.5 * fbm(x / 6, y / 60, 64, 3, pi);
    });
    const color = (x, y) => {
      const pw = w / planks, pi = Math.floor(x / pw), px = (x % pw) / pw;
      const grain = fbm(px * 4 + pi * 3.1, y / h * 24 + offs[pi][0] * 10, 1024, 4, seed + pi);
      const ring = 0.5 + 0.5 * Math.sin((px * 9 + grain * 6 + offs[pi][2] * 20) * 3.14);
      const stain = fbm(x / 70, y / 70, w / 70, 4, seed + 99);
      let c = mixc(dark, base, 0.35 + 0.45 * ring * grain + offs[pi][1]);
      c = mixc(c, dark, Math.max(0, stain - 0.55) * 2 * worn);
      const s = height[y * w + x] < 0.3 ? 0.35 : 1;
      return c.map(v => v * s);
    };
    const rough = field(w, h, (x, y) => 0.55 + 0.35 * fbm(x / 30, y / 30, w / 30, 3, 5));
    return { color, height, rough, normalStrength: 3 };
  });
}

// ---------- Papel de parede damasco, desbotado e manchado ----------
export function wallpaper({ base = [70, 84, 78], pattern = [52, 64, 60], seed = 3, size = 512 } = {}) {
  return material(`wp-${seed}-${base}`, size, size, (w, h) => {
    const color = (x, y) => {
      const u = x / w, v = y / h;
      const cx = (u * 4) % 1 - 0.5, cy = (v * 3) % 1 - 0.5;
      const row = Math.floor(v * 3) % 2; const cx2 = ((u * 4 + row * 0.5) % 1) - 0.5;
      const r1 = Math.hypot(cx2 * 1.4, cy);
      const petal = Math.abs(Math.sin(Math.atan2(cy, cx2) * 4)) * 0.2 + 0.18;
      const motif = r1 < petal ? 1 : (Math.abs(cx) < 0.012 ? 0.6 : 0);
      const stripe = Math.abs(((u * 16) % 1) - 0.5) < 0.04 ? 0.5 : 0;
      let c = mixc(base, pattern, Math.max(motif, stripe));
      const stain = fbm(x / 90, y / 60, w / 90, 5, seed);
      const drip = Math.max(0, fbm(x / 8, y / 200, w / 8, 3, seed + 4) - 0.5) * (y / h);
      c = mixc(c, [38, 40, 30], Math.max(0, stain - 0.5) * 1.6 + drip * 1.2);
      const g = 0.85 + 0.3 * fbm(x / 3, y / 3, w / 3, 2, seed + 1);
      return c.map(vv => vv * g);
    };
    const height = field(w, h, (x, y) => fbm(x / 5, y / 5, w / 5, 3, seed + 7) * 0.4);
    return { color, height, rough: null, normalStrength: 1.2 };
  });
}

// ---------- Reboco/estuque com mofo ----------
export function plaster({ base = [118, 118, 108], seed = 5, size = 512, dirt = 0.6 } = {}) {
  return material(`pl-${seed}-${base}`, size, size, (w, h) => {
    const height = field(w, h, (x, y) => fbm(x / 10, y / 10, w / 10, 5, seed));
    const color = (x, y) => {
      const n = fbm(x / 60, y / 60, w / 60, 5, seed + 2);
      const crack = Math.abs(fbm(x / 25, y / 25, w / 25, 4, seed + 9) - 0.5) < 0.008 ? 0.6 : 1;
      let c = mixc(base, [58, 66, 54], Math.max(0, n - 0.45) * 2 * dirt);
      c = mixc(c, [40, 44, 36], Math.max(0, (y / h) - 0.7) * dirt);
      return c.map(v => v * crack * (0.92 + 0.16 * height[y * w + x]));
    };
    return { color, height, rough: null, normalStrength: 2.5 };
  });
}

// ---------- Ripas de madeira (fachada) ----------
export function siding({ base = [96, 100, 96], seed = 8, size = 512 } = {}) {
  return material(`sd-${seed}-${base}`, size, size, (w, h) => {
    const boards = 10;
    const height = field(w, h, (x, y) => { const v = (y / h * boards) % 1; return v * 0.8 + 0.2 * fbm(x / 40, y / 4, w / 40, 3, seed); });
    const color = (x, y) => {
      const v = (y / h * boards) % 1;
      const peel = fbm(x / 30, y / 30, w / 30, 5, seed + 1);
      let c = peel > 0.58 ? [62, 52, 42] : base;
      c = mixc(c, [44, 50, 42], Math.max(0, fbm(x / 80, y / 40, w / 80, 4, seed + 4) - 0.5) * 1.8);
      const shade = v > 0.92 ? 0.45 : 0.85 + 0.15 * v;
      return c.map(q => q * shade);
    };
    const rough = field(w, h, () => 0.8);
    return { color, height, rough, normalStrength: 4 };
  });
}

// ---------- Telhas de ardósia/cerâmica escura ----------
export function shingles({ base = [52, 50, 54], seed = 11, size = 512 } = {}) {
  return material(`sh-${seed}`, size, size, (w, h) => {
    const rows = 12, cols = 10;
    const height = field(w, h, (x, y) => {
      const ry = y / h * rows, row = Math.floor(ry), fy = ry - row;
      const fx = ((x / w * cols) + (row % 2) * 0.5) % 1;
      const gap = Math.min(fx, 1 - fx) < 0.03 ? 0 : 1;
      return (1 - fy) * gap * 0.8 + 0.2 * fbm(x / 6, y / 6, w / 6, 3, seed);
    });
    const color = (x, y) => {
      const row = Math.floor(y / h * rows), col = Math.floor(((x / w * cols) + (row % 2) * 0.5));
      const k = hash2(row, col, seed) * 0.3 + 0.8;
      const moss = Math.max(0, fbm(x / 50, y / 50, w / 50, 4, seed + 3) - 0.55) * 2;
      return mixc(base.map(v => v * k), [48, 60, 40], moss).map(v => v * (0.5 + 0.5 * height[y * w + x]));
    };
    return { color, height, rough: null, normalStrength: 5 };
  });
}

// ---------- Asfalto/paralelepípedo molhado ----------
export function wetRoad({ seed = 13, size = 512, cobble = false } = {}) {
  return material(`rd-${seed}-${cobble}`, size, size, (w, h) => {
    const height = field(w, h, (x, y) => {
      if (!cobble) return fbm(x / 4, y / 4, w / 4, 4, seed);
      const n = 12, cx = (x / w * n) % 1, cy = ((y / h * n) + (Math.floor(x / w * n) % 2) * 0.5) % 1;
      return Math.min(1, Math.min(cx, 1 - cx, cy, 1 - cy) * 8) * 0.8 + 0.2 * fbm(x / 5, y / 5, w / 5, 3, seed);
    });
    const puddle = field(w, h, (x, y) => fbm(x / 70, y / 70, w / 70, 5, seed + 2));
    const color = (x, y) => { const v = 30 + 26 * height[y * w + x]; const p = puddle[y * w + x] > 0.55 ? 0.6 : 1; return [v * p, v * p * 1.02, v * p * 1.05]; };
    const rough = field(w, h, (x, y) => puddle[y * w + x] > 0.55 ? 0.05 : 0.25 + 0.3 * height[y * w + x]);
    return { color, height, rough, normalStrength: 2 };
  });
}

// ---------- Terra/grama seca ----------
export function groundDirt({ seed = 17, size = 512 } = {}) {
  return material(`gd-${seed}`, size, size, (w, h) => {
    const height = field(w, h, (x, y) => fbm(x / 6, y / 6, w / 6, 5, seed));
    const color = (x, y) => {
      const g = fbm(x / 40, y / 40, w / 40, 5, seed + 5);
      const blade = fbm(x / 1.5, y / 9, w / 1.5, 2, seed + 8);
      const grass = mixc([42, 48, 30], [70, 74, 46], blade);
      const mud = [40, 34, 28];
      return mixc(mud, grass, Math.min(1, Math.max(0, (g - 0.4) * 3))).map(v => v * (0.75 + 0.5 * height[y * w + x]));
    };
    const rough = field(w, h, (x, y) => 0.65 + 0.3 * height[y * w + x]);
    return { color, height, rough, normalStrength: 3 };
  });
}

// ---------- Casca de árvore ----------
export function bark({ seed = 21, size = 256 } = {}) {
  return material(`bk-${seed}`, size, size, (w, h) => {
    const height = field(w, h, (x, y) => { const n = fbm(x / 4, y / 40, w / 4, 5, seed); return Math.pow(Math.abs(Math.sin(n * 12)), 0.6); });
    const color = (x, y) => { const v = 30 + 40 * height[y * w + x]; return [v, v * 0.92, v * 0.85]; };
    return { color, height, rough: null, normalStrength: 6 };
  });
}

// ---------- Tecido (cortina/roupa) ----------
export function fabric({ base = [60, 70, 66], seed = 23, size = 256, weave = 2 } = {}) {
  return material(`fb-${seed}-${base}`, size, size, (w, h) => {
    const height = field(w, h, (x, y) => 0.5 + 0.25 * Math.sin(x * weave) * Math.sin(y * weave) + 0.25 * fbm(x / 3, y / 3, w / 3, 2, seed));
    const color = (x, y) => { const n = fbm(x / 30, y / 30, w / 30, 4, seed); return base.map(v => v * (0.75 + 0.4 * n) * (0.9 + 0.1 * height[y * w + x])); };
    return { color, height, rough: null, normalStrength: 1.5 };
  });
}

// ---------- Mapa de gotas de chuva em vidro (normal) ----------
export function rainGlass({ seed = 31, size = 512 } = {}) {
  return material(`rg-${seed}`, size, size, (w, h) => {
    const r = rng(seed); const drops = [];
    for (let i = 0; i < 260; i++) drops.push([r() * w, r() * h, 2 + r() * r() * 9, r()]);
    const height = new Float32Array(w * h);
    for (const [cx, cy, rad, k] of drops) {
      for (let y = Math.floor(cy - rad * 3); y < cy + rad; y++) for (let x = Math.floor(cx - rad); x < cx + rad; x++) {
        const xx = ((x % w) + w) % w, yy = ((y % h) + h) % h;
        const dy = y > cy ? (y - cy) : (y - cy) / (k > 0.6 ? 3 : 1);
        const d = Math.hypot(x - cx, dy) / rad;
        if (d < 1) height[yy * w + xx] = Math.max(height[yy * w + xx], Math.sqrt(1 - d * d));
      }
    }
    const color = () => [255, 255, 255];
    return { color, height, rough: null, normalStrength: 6 };
  });
}
