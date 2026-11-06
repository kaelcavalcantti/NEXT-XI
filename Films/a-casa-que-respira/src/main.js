// Ponto de entrada do renderizador. Expõe window.renderFrame(f) para a captura quadro a quadro.
import * as THREE from 'three';
import { SHOTS, FPS, WIDTH, HEIGHT, TOTAL } from './timeline.js';
import { Post } from './engine/post.js';
import { G } from './engine/fx.js';
import { SHOT_MODULES } from './shots/index.js';

const params = new URLSearchParams(location.search);
const scale = parseFloat(params.get('scale') || '1');
const W = Math.round(WIDTH * scale), H = Math.round(HEIGHT * scale);

const renderer = new THREE.WebGLRenderer({ antialias: false, preserveDrawingBuffer: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(1);
renderer.setSize(W, H);
renderer.toneMapping = THREE.NoToneMapping;
renderer.outputColorSpace = THREE.LinearSRGBColorSpace; // a gradação final faz a conversão
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
document.body.appendChild(renderer.domElement);

const post = new Post(renderer, W, H);

async function loadJSON(p, fallback) { try { const r = await fetch(p); if (!r.ok) throw 0; return await r.json(); } catch { return fallback; } }

const ctx = {
  renderer, post, W, H, scale, sets: {},
  lipsync: await loadJSON('../build/lipsync.json', {}),
  voices: await loadJSON('../build/voices.json', {}),
  overlay: post.overlayCanvas.getContext('2d'),
};
await document.fonts.load('40px Cinzel'); await document.fonts.load('40px CinzelB');
await document.fonts.load('40px Cormorant'); await document.fonts.load('40px CormorantI');
await document.fonts.load('40px Hand'); await document.fonts.load('40px Typewriter');

const instances = {};
async function getShot(id) {
  if (!instances[id] && SHOT_MODULES[id]) instances[id] = await SHOT_MODULES[id].create(ctx);
  return instances[id];
}

ctx.getShot = getShot;
function shotAt(t) { for (const s of SHOTS) if (t < s.end) return s; return SHOTS[SHOTS.length - 1]; }

const defaults = () => ({ grain: 0.06, vignette: 0.75, letterbox: 0.19, fade: 0, desat: 0, aberration: 0.006, overlayOn: 0, exposure: 1, flash: 0,
  sat: 0.78, contrast: 1.06, gamma: 1.0, lift: [0.012, 0.02, 0.03], gain: [0.93, 1.0, 1.04] });

const testId = params.get('test');
let testMod = null;
if (testId) testMod = (await import('./shots/tests.js')).TESTS[testId];

window.renderFrame = async (f) => {
  const t = f / FPS;
  const s = testMod ? { id: 'test_' + testId, start: 0 } : shotAt(t);
  if (testMod && !instances[s.id]) instances[s.id] = await testMod.create(ctx);
  const inst = await getShot(s.id);
  G.time.value = t;
  const out = inst.update(t - s.start, t) || {};
  const u = post.settings, d = { ...defaults(), ...(out.grade || {}) };
  for (const k of ['grain', 'vignette', 'letterbox', 'fade', 'desat', 'aberration', 'overlayOn', 'exposure', 'flash', 'sat', 'contrast', 'gamma']) u[k].value = d[k];
  u.lift.value.set(...d.lift); u.gain.value.set(...d.gain);
  u.time.value = t;
  if (out.overlayDirty) post.overlayTex.needsUpdate = true;
  if (out.camera) { G.camPos.value.copy(out.camera.position); out.camera.updateMatrixWorld(); }
  post.render(out.scene || null, out.camera || null, out);
  return s.id;
};

window.grab = (q = 0.95) => renderer.domElement.toDataURL('image/jpeg', q);
window.TOTAL_FRAMES = Math.round(TOTAL * FPS);
window.READY = true;
