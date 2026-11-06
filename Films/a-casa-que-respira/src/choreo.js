// Coreografia de Gabriel: trajetos de caminhada por plano. É a mesma fonte para a animação
// (posição, direção e fase da passada) e para o áudio (instante e superfície de cada passo).
import { T, shotById } from './timeline.js';

// Coordenadas do interior: piso y=0, porta de entrada em z=0, casa se estende para -z.
// path: lista de [x, z]; stride: comprimento de um passo (m); surface: wood|stairs|dirt|gravel|porch|carpet
export const WALKS = [
  // chegada: do carro até a varanda (coordenadas do mundo exterior, relativas à casa)
  { id: 'arrive', shot: 'arrival', t0: 11.6, t1: 14.0, path: [[1.8, 15.5], [1.2, 13.4]], stride: 0.7, surface: 'gravel' },
  { id: 'porch', shot: 'front_door', t0: 0.0, t1: 3.6, path: [[1.2, 12.6], [0.6, 10.2], [0.25, 8.4]], stride: 0.66, surface: 'porch', stepsUp: [1.0, 1.9, 2.7] },
  // hall -> corredor das fotografias
  { id: 'enter', shot: 'hallway', t0: 1.6, t1: 6.2, path: [[0, -0.6], [0.8, -2.0], [3.6, -2.4]], stride: 0.55, surface: 'wood', careful: 1 },
  { id: 'corr1', shot: 'hallway', t0: 6.2, t1: 11.8, path: [[3.6, -2.4], [7.6, -2.4]], stride: 0.5, surface: 'wood', careful: 1 },
  { id: 'corr2', shot: 'hallway', t0: 15.0, t1: 23.0, path: [[7.6, -2.4], [11.8, -2.4], [13.6, -3.0]], stride: 0.5, surface: 'wood', careful: 1 },
  // até a mesa do álbum
  { id: 'toTable', shot: 'album', t0: 0.0, t1: 1.9, path: [[14.2, -2.4], [15.0, -2.85]], stride: 0.45, surface: 'carpet', careful: 1 },
  // escada
  { id: 'toStairs', shot: 'stairs', t0: 1.6, t1: 5.0, path: [[1.2, -2.4], [-1.8, -0.9], [-3.4, -0.9]], stride: 0.62, surface: 'wood' },
  { id: 'climb', shot: 'stairs', t0: 5.0, t1: 10.4, path: [[-3.4, -1.0], [-3.4, -6.4]], stride: 0.45, surface: 'stairs', careful: 1, climb: [0, 3.3] },
  { id: 'upper', shot: 'stairs', t0: 10.4, t1: 13.0, path: [[-3.4, -6.4], [-3.6, -7.3], [-6.0, -7.3]], stride: 0.5, surface: 'wood', careful: 1, y: 3.3 },
  // quarto
  { id: 'intoRoom', shot: 'child_room', t0: 0.6, t1: 5.4, path: [[-10.3, -7.3], [-11.6, -8.2], [-13.0, -9.3]], stride: 0.45, surface: 'wood', careful: 1, y: 3.3 },
  // descida e corredor do porão
  { id: 'toBasement', shot: 'basement_door', t0: 0.0, t1: 2.0, path: [[0.4, -6.2], [0.2, -8.3]], stride: 0.55, surface: 'wood', careful: 1 },
  { id: 'approach', shot: 'basement_door', t0: 3.6, t1: 7.4, path: [[0.2, -8.3], [0.1, -10.2]], stride: 0.42, surface: 'wood', careful: 1 },
  { id: 'back', shot: 'basement_door', t0: 10.2, t1: 11.0, path: [[0.1, -10.2], [0.1, -9.5]], stride: 0.35, surface: 'wood', backwards: true },
  // corrida até a porta da frente
  // o duplo desce a escada
  { id: 'descend', shot: 'decision', t0: 0.6, t1: 13.6, path: [[-3.4, -6.2], [-3.4, -3.9]], stride: 0.45, surface: 'stairs', climb: [3.3, 1.75], slow: 1, who: 'double' },
  { id: 'run', shot: 'run_phone', t0: 0.0, t1: 3.4, path: [[0.1, -9.4], [0.3, -5.0], [0.2, -1.4]], stride: 1.15, surface: 'wood', run: 1 },
];

// Estado de uma caminhada num instante absoluto
export function walkAt(w, t) {
  const t0 = T(w.shot, w.t0), t1 = T(w.shot, w.t1);
  const u = Math.min(1, Math.max(0, (t - t0) / (t1 - t0)));
  // comprimento total
  const segs = []; let L = 0;
  for (let i = 0; i < w.path.length - 1; i++) { const [ax, az] = w.path[i], [bx, bz] = w.path[i + 1]; const l = Math.hypot(bx - ax, bz - az); segs.push([ax, az, bx, bz, l]); L += l; }
  // perfil de velocidade com aceleração/desaceleração suaves
  const ease = u < 0.12 ? (u * u) / (2 * 0.12 * 0.88) : u > 0.88 ? 1 - ((1 - u) * (1 - u)) / (2 * 0.12 * 0.88) : (u - 0.06) / 0.88;
  let d = Math.min(L, Math.max(0, ease * L));
  let x = w.path[0][0], z = w.path[0][1], hx = 0, hz = 1;
  let acc = 0;
  for (let si = 0; si < segs.length; si++) {
    const [ax, az, bx, bz, l] = segs[si];
    if (d <= acc + l || si === segs.length - 1) {
      const k = Math.min(1, (d - acc) / l); x = ax + (bx - ax) * k; z = az + (bz - az) * k; hx = (bx - ax) / l; hz = (bz - az) / l; break;
    }
    acc += l;
  }
  const heading = Math.atan2(hx, hz) + (w.backwards ? Math.PI : 0);
  const phase = d / (w.stride * 2);
  const y = w.climb ? w.climb[0] + (w.climb[1] - w.climb[0]) * (d / L) : (w.y || 0);
  return { x, y, z, heading, phase, moving: u > 0 && u < 1, u, dist: d, len: L };
}

export function walkById(id) { return WALKS.find(w => w.id === id); }

export function buildChoreo() {
  const steps = [];
  for (const w of WALKS) {
    const t0 = T(w.shot, w.t0), t1 = T(w.shot, w.t1);
    let lastStep = -1;
    for (let t = t0; t <= t1; t += 1 / 240) {
      const s = walkAt(w, t);
      const k = Math.floor(s.phase * 2 + 0.25); // contato do calcanhar
      if (k !== lastStep && s.u > 0.02) {
        if (lastStep >= 0) steps.push({ t: +t.toFixed(3), surface: w.surface, run: w.run || 0, careful: w.careful || 0, foot: k % 2, walk: w.id });
        lastStep = k;
      }
    }
  }
  steps.sort((a, b) => a.t - b.t);
  return { steps, cues: [] };
}
