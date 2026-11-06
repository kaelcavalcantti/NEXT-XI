// Utilidades determinísticas: PRNG, ruído, curvas e keyframes.
import * as THREE from 'three';

export function rng(seed = 1) {
  let a = seed >>> 0;
  const f = () => {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  f.range = (lo, hi) => lo + (hi - lo) * f();
  f.int = (lo, hi) => Math.floor(lo + (hi - lo + 1) * f());
  f.pick = arr => arr[Math.floor(f() * arr.length)];
  f.gauss = () => { let s = 0; for (let i = 0; i < 4; i++) s += f(); return (s - 2) / 0.577; };
  return f;
}

export const clamp = (x, a = 0, b = 1) => Math.min(b, Math.max(a, x));
export const lerp = (a, b, t) => a + (b - a) * t;
export const smooth = (a, b, x) => { const t = clamp((x - a) / (b - a)); return t * t * (3 - 2 * t); };
export const smoother = (a, b, x) => { const t = clamp((x - a) / (b - a)); return t * t * t * (t * (t * 6 - 15) + 10); };
export const ease = {
  linear: t => t,
  inOut: t => t * t * (3 - 2 * t),
  inOut2: t => t * t * t * (t * (t * 6 - 15) + 10),
  in: t => t * t,
  out: t => 1 - (1 - t) * (1 - t),
  in3: t => t * t * t,
  out3: t => 1 - Math.pow(1 - t, 3),
};

// Ruído de valor 1D suave (determinístico) — para tremores de câmera, vento, chamas.
function hash1(n) { const s = Math.sin(n * 127.1 + 311.7) * 43758.5453; return s - Math.floor(s); }
export function noise1(x, seed = 0) {
  const i = Math.floor(x), f = x - i;
  const u = f * f * (3 - 2 * f);
  return lerp(hash1(i + seed * 17.13), hash1(i + 1 + seed * 17.13), u) * 2 - 1;
}
export function fbm1(x, seed = 0, oct = 3) {
  let a = 0, amp = 0.5, fr = 1;
  for (let o = 0; o < oct; o++) { a += amp * noise1(x * fr, seed + o * 3.7); amp *= 0.5; fr *= 2.03; }
  return a;
}

// Keyframes: kf(t, [[t0, v0], [t1, v1], ...], easeFn). v pode ser número ou array.
export function kf(t, keys, e = ease.inOut) {
  if (t <= keys[0][0]) return keys[0][1];
  for (let i = 0; i < keys.length - 1; i++) {
    const [t0, v0, e0] = keys[i], [t1, v1] = keys[i + 1];
    if (t <= t1) {
      const u = (e0 || e)((t - t0) / (t1 - t0));
      if (Array.isArray(v0)) return v0.map((x, j) => lerp(x, v1[j], u));
      return lerp(v0, v1, u);
    }
  }
  return keys[keys.length - 1][1];
}
export const v3 = (a) => new THREE.Vector3(a[0], a[1], a[2]);

// Pulso: envelope de ataque/decaimento em torno de um evento
export function pulse(t, t0, attack = 0.02, decay = 0.4) {
  if (t < t0) return 0;
  const d = t - t0;
  if (d < attack) return d / attack;
  return Math.exp(-(d - attack) / decay);
}

// Relâmpago: flash múltiplo com cintilação (mesmo padrão usado pelo áudio).
export function lightningAt(t, times) {
  let v = 0;
  for (const t0 of times) {
    const d = t - t0;
    if (d < 0 || d > 1.6) continue;
    v = Math.max(v,
      pulse(t, t0, 0.015, 0.06) * 1.0 +
      pulse(t, t0 + 0.13, 0.01, 0.05) * 0.7 +
      pulse(t, t0 + 0.31, 0.02, 0.22) * 0.9);
  }
  return v;
}
