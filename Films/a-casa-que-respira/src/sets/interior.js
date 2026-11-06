// Interior do casarão. Piso térreo y=0, porta de entrada em z=0 (exterior em +z), casa para -z.
// Hall com pé-direito duplo (x -4..4, z -8..0), escada à esquerda subindo para -z, galeria ao fundo (y=3.3),
// corredor das fotografias (x 4..12, z -3.2..-1.6), sala (x 12..18, z -7..0), passagem do porão (x -1..1, z -11..-8),
// corredor superior (x -10..-4, z -8..-6.6) e quarto infantil (x -15..-10, z -11..-5).
import * as THREE from 'three';
import * as TX from '../engine/tex.js';
import { rng } from '../lib/util.js';
import { merge, windowMesh } from './kit.js';
import { makeLightCone, makeDust } from '../engine/fx.js';
import * as P from './props.js';

export const UP = 3.3; // altura do piso superior

// Parede plana com aberturas retangulares. Desenhada no plano XY local (largura w, altura h), normal +z.
function wall(w, h, holes = [], mat) {
  const s = new THREE.Shape(); s.moveTo(0, 0); s.lineTo(w, 0); s.lineTo(w, h); s.lineTo(0, h); s.lineTo(0, 0);
  for (const [x0, x1, y0, y1] of holes) { const p = new THREE.Path(); p.moveTo(x0, y0); p.lineTo(x0, y1); p.lineTo(x1, y1); p.lineTo(x1, y0); p.lineTo(x0, y0); s.holes.push(p); }
  const g = new THREE.ShapeGeometry(s); g.translate(-w / 2, 0, 0);
  const m = new THREE.Mesh(g, mat); m.receiveShadow = true; m.castShadow = true;
  return m;
}
// coloca parede: centro (cx, cz), orientação da normal ('+x','-x','+z','-z'), base y0
function place(m, cx, y0, cz, facing) {
  m.position.set(cx, y0, cz);
  m.rotation.y = { '+z': 0, '-z': Math.PI, '+x': Math.PI / 2, '-x': -Math.PI / 2 }[facing];
  return m;
}
const meterTex = (t, sx, sy) => { const c = t.clone(); c.needsUpdate = true; c.repeat.set(sx, sy); return c; };

export function buildInterior() {
  P.mats();
  const g = new THREE.Group(); g.name = 'interior';
  const r = rng(4242);
  // Materiais (texturas em "metros": ShapeGeometry usa UV = coordenadas)
  const wpT = TX.wallpaper({ base: [74, 88, 80], pattern: [52, 66, 60], seed: 3 });
  const wp = new THREE.MeshStandardMaterial({ map: meterTex(wpT.map, 0.55, 0.55), normalMap: meterTex(wpT.normalMap, 0.55, 0.55), roughness: 0.92, color: 0xa8b0a8 });
  const wpKT = TX.wallpaper({ base: [118, 104, 92], pattern: [96, 84, 76], seed: 8 });
  const wpKid = new THREE.MeshStandardMaterial({ map: meterTex(wpKT.map, 0.8, 0.8), normalMap: meterTex(wpKT.normalMap, 0.8, 0.8), roughness: 0.95, color: 0xb0a8a0 });
  const flT = TX.woodPlanks({ planks: 6, base: [84, 58, 40], dark: [38, 26, 18], seed: 5, worn: 0.6 });
  const floorMat = new THREE.MeshStandardMaterial({ map: flT.map, normalMap: flT.normalMap, roughnessMap: flT.roughnessMap, color: 0xa09080 });
  const plT = TX.plaster({ base: [150, 148, 138], seed: 9, dirt: 0.8 });
  const ceil = new THREE.MeshStandardMaterial({ map: plT.map, normalMap: plT.normalMap, roughness: 1, color: 0x8a8a84, side: THREE.DoubleSide });
  const trim = new THREE.MeshStandardMaterial({ color: 0x2a1e16, roughness: 0.6 });
  const panelT = TX.woodPlanks({ planks: 8, base: [64, 44, 32], dark: [30, 22, 16], seed: 6 });
  const wainscot = new THREE.MeshStandardMaterial({ map: panelT.map, normalMap: panelT.normalMap, roughness: 0.6, color: 0x9a8a7c });
  const darkDoor = new THREE.MeshStandardMaterial({ map: panelT.map, normalMap: panelT.normalMap, roughness: 0.55, color: 0x3a2c24 });

  const floorPlane = (x0, x1, z0, z1, y = 0, mat = floorMat) => {
    const m = new THREE.Mesh(new THREE.PlaneGeometry(x1 - x0, z1 - z0).rotateX(-Math.PI / 2), mat);
    const rx = (x1 - x0) / 2.2, rz = (z1 - z0) / 2.2;
    m.material = mat.clone(); ['map', 'normalMap', 'roughnessMap'].forEach(k => { if (mat[k]) { m.material[k] = meterTex(mat[k], rx, rz); } });
    m.position.set((x0 + x1) / 2, y, (z0 + z1) / 2); m.receiveShadow = true; g.add(m); return m;
  };
  const ceiling = (x0, x1, z0, z1, y) => { const m = new THREE.Mesh(new THREE.PlaneGeometry(x1 - x0, z1 - z0).rotateX(Math.PI / 2), ceil); m.position.set((x0 + x1) / 2, y, (z0 + z1) / 2); g.add(m); return m; };
  const baseboard = (len, cx, cz, facing, y = 0) => { const b = new THREE.Mesh(new THREE.BoxGeometry(len, 0.18, 0.03), trim); place(b, cx, y + 0.09, cz, facing); g.add(b); };
  const wains = (len, cx, cz, facing, y = 0, holes = []) => { const m = wall(len, 1.0, holes, wainscot); place(m, cx, y, cz, facing); m.position.add(new THREE.Vector3(0, 0, 0)); g.add(m); const rail = new THREE.Mesh(new THREE.BoxGeometry(len, 0.05, 0.04), trim); place(rail, cx, y + 1.0, cz, facing); g.add(rail); };
  const W = (len, h, holes, cx, y0, cz, facing, mat = wp) => { const m = wall(len, h, holes, mat); place(m, cx, y0, cz, facing); g.add(m); return m; };

  // ================= HALL =================
  floorPlane(-4, 4, -8, 0);
  ceiling(-4, 4, -8, 0, 6.6);
  // parede frontal (z=0) com porta e duas janelas altas
  W(8, 6.6, [[2.5, 5.5, 0, 2.9], [0.9, 2.1, 1.0, 3.6], [5.9, 7.1, 1.0, 3.6], [3.4, 4.6, 4.2, 5.6]], 0, 0, 0.0, '-z');
  // parede esquerda (x=-4): abertura para o corredor superior
  W(8, 6.6, [[6.6, 8.0, UP, UP + 2.4]], -4, 0, -4, '+x');
  // parede direita (x=4): abertura para o corredor das fotos
  W(8, 6.6, [[4.8, 6.4, 0, 2.6]], 4, 0, -4, '-x');
  // parede do fundo (z=-8): passagem do porão (sob a galeria) e porta superior
  W(8, 6.6, [[3.1, 4.9, 0, 2.5]], 0, 0, -8, '+z');
  // lambris
  wains(8, -4 + 0.005, -4, '+x'); wains(8, 4 - 0.005, -4, '-x', 0, [[4.8, 6.4, 0, 1.0]]);
  // galeria superior (piso e guarda-corpo)
  const galFloor = new THREE.Mesh(new THREE.BoxGeometry(8, 0.25, 1.6).translate(0, UP - 0.125, -7.2), floorMat); galFloor.receiveShadow = galFloor.castShadow = true; g.add(galFloor);
  const balG = [];
  for (let x = -2.7; x <= 3.9; x += 0.16) balG.push(new THREE.CylinderGeometry(0.022, 0.026, 0.9, 6).translate(x, UP + 0.45, -6.42));
  balG.push(new THREE.BoxGeometry(6.8, 0.07, 0.1).translate(0.6, UP + 0.93, -6.42));
  const bal = new THREE.Mesh(merge(balG), trim); bal.castShadow = true; g.add(bal);
  // viga sob a galeria
  g.add(new THREE.Mesh(new THREE.BoxGeometry(8, 0.3, 0.2).translate(0, UP - 0.3, -6.4), trim));
  // escada: degraus de x -4..-2.8, de z -1.4 (base) a -6.4 (topo)
  const steps = 11, run = 5.0 / steps, rise = UP / steps;
  const stairG = [];
  for (let i = 0; i < steps; i++) {
    const z = -1.4 - i * run;
    stairG.push(new THREE.BoxGeometry(1.2, 0.05, run + 0.03).translate(-3.4, (i + 1) * rise - 0.025, z - run / 2));
    stairG.push(new THREE.BoxGeometry(1.2, rise, 0.025).translate(-3.4, i * rise + rise / 2, z));
  }
  const stairs = new THREE.Mesh(merge(stairG), floorMat); stairs.castShadow = stairs.receiveShadow = true; g.add(stairs);
  // longarina e corrimão
  const sl = Math.hypot(5, UP), ang = Math.atan2(UP, 5);
  const stringer = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.35, sl), trim); stringer.position.set(-2.78, UP / 2, -3.9); stringer.rotation.x = ang; g.add(stringer);
  const rail = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.07, sl), trim); rail.position.set(-2.78, UP / 2 + 0.95, -3.9); rail.rotation.x = ang; g.add(rail);
  const posts = [];
  for (let i = 0; i < steps; i++) { const z = -1.4 - (i + 0.5) * run; posts.push(new THREE.CylinderGeometry(0.02, 0.02, 0.95, 6).translate(-2.78, (i + 1) * rise + 0.47, z)); }
  posts.push(new THREE.CylinderGeometry(0.07, 0.08, 1.3, 8).translate(-2.78, 0.65, -1.35));
  g.add(new THREE.Mesh(merge(posts), trim));
  // parte de baixo da escada (fechada)
  const under = new THREE.Mesh(new THREE.PlaneGeometry(sl, 0.01), trim);
  // lustre apagado
  const chand = new THREE.Group();
  chand.add(new THREE.Mesh(new THREE.CylinderGeometry(0.01, 0.01, 1.4, 4).translate(0, -0.7, 0), P.M.iron));
  chand.add(new THREE.Mesh(new THREE.TorusGeometry(0.45, 0.02, 6, 24).rotateX(Math.PI / 2).translate(0, -1.45, 0), P.M.brass));
  for (let i = 0; i < 8; i++) { const a = i / 8 * Math.PI * 2; const c = P.candlestick(); c.scale.setScalar(0.6); c.position.set(Math.cos(a) * 0.45, -1.47, Math.sin(a) * 0.45); chand.add(c); }
  chand.position.set(0, 6.6, -3.5); g.add(chand);
  // mobília do hall: aparador, espelho coberto, cabideiro, relógios parados, retratos
  const sb = P.sideboard(); sb.position.set(3.7, 0, -5.5); sb.rotation.y = -Math.PI / 2; g.add(sb);
  const mirror = new THREE.Mesh(new THREE.PlaneGeometry(0.9, 1.3), P.M.sheet); mirror.position.set(3.98, 1.9, -5.5); mirror.rotation.y = -Math.PI / 2; g.add(mirror);
  const hallClock = P.wallClock(7.62, 0.5); hallClock.position.set(-3.96, 2.4, -1.2); hallClock.rotation.y = Math.PI / 2; g.add(hallClock);
  const bigPortrait = P.framedPhoto(17, { w: 0.9, h: 1.2, people: 2 }); bigPortrait.position.set(1.0, 4.6, -7.97); g.add(bigPortrait);
  // janelas do hall (luar)
  for (const x of [-2.5, 2.5]) { const wm = windowMesh({ w: 1.2, h: 2.6 }); wm.position.set(x, 2.3, 0.02); wm.rotation.y = Math.PI; g.add(wm); }
  const transom = windowMesh({ w: 1.2, h: 1.4 }); transom.position.set(0, 4.9, 0.02); transom.rotation.y = Math.PI; g.add(transom);

  // Porta de entrada (interna): pivô na dobradiça
  const frontDoor = new THREE.Group(); frontDoor.position.set(-1.0, 0, -0.04);
  const fdLeaf = new THREE.Mesh(new THREE.BoxGeometry(1.0, 2.85, 0.07).translate(0.5, 1.425, 0), darkDoor); frontDoor.add(fdLeaf);
  const fdKnob = new THREE.Mesh(new THREE.SphereGeometry(0.045, 12, 8), P.M.brass); fdKnob.position.set(0.88, 1.05, -0.06); frontDoor.add(fdKnob);
  g.add(frontDoor);
  const frontDoor2 = new THREE.Mesh(new THREE.BoxGeometry(1.0, 2.85, 0.07).translate(-0.5, 1.425, 0), darkDoor); frontDoor2.position.set(1.0, 0, -0.04); g.add(frontDoor2);

  // ================= CORREDOR DAS FOTOGRAFIAS =================
  floorPlane(4, 12, -3.2, -1.6);
  ceiling(4, 12, -3.2, -1.6, 3.0);
  W(8, 3.0, [], 8, 0, -3.2, '+z'); W(8, 3.0, [], 8, 0, -1.6, '-z');
  wains(8, 8, -3.195, '+z'); wains(8, 8, -1.605, '-z');
  // fotografias e relógios parados nas duas paredes
  const corridorPhotos = [];
  for (let i = 0; i < 9; i++) {
    const side = i % 2 ? -1.62 : -3.18, facing = i % 2 ? Math.PI : 0;
    const ph = P.framedPhoto(30 + i, { w: r.range(0.26, 0.4), h: r.range(0.34, 0.5), people: r.int(1, 3), scratched: i === 5 });
    ph.position.set(4.6 + i * 0.85, r.range(1.55, 1.9), side); ph.rotation.y = facing; ph.rotation.z = r.range(-0.06, 0.06); g.add(ph); corridorPhotos.push(ph);
  }
  for (const [x, z, f, h] of [[6.0, -1.62, Math.PI, 2.3], [9.4, -3.18, 0, 11.1], [10.9, -1.62, Math.PI, 4.5]]) { const c = P.wallClock(h, 0.32); c.position.set(x, 2.3, z); c.rotation.y = f; g.add(c); }
  const cs = P.sideboard({ w: 1.2, h: 0.85, d: 0.35 }); cs.position.set(8.0, 0, -2.98); g.add(cs);
  const cand = P.candlestick(); cand.position.set(7.7, 0.85, -2.98); g.add(cand);
  const corridorClock = P.wallClock(9.25, 0.28); corridorClock.position.set(8.2, 1.15, -2.8); corridorClock.rotation.x = -0.15; g.add(corridorClock);

  // ================= SALA =================
  floorPlane(12, 18, -7, 0);
  ceiling(12, 18, -7, 0, 3.4);
  W(7, 3.4, [[1.6, 3.2, 0, 2.6]], 12, 0, -3.5, '+x');       // parede com passagem do corredor (z -3.2..-1.6)
  W(7, 3.4, [[2.4, 3.8, 0.9, 2.9]], 18, 0, -3.5, '-x');     // janela lateral
  W(6, 3.4, [], 15, 0, -7, '+z');
  W(6, 3.4, [[1.8, 3.2, 0.9, 2.9]], 15, 0, 0, '-z');
  wains(6, 15, -6.995, '+z');
  const rug = new THREE.Mesh(new THREE.PlaneGeometry(3.2, 2.4).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x3a2224, roughness: 1, map: TX.fabric({ base: [120, 60, 60], seed: 90, weave: 0.8 }).map }));
  rug.position.set(15, 0.005, -3.6); rug.receiveShadow = true; g.add(rug);
  const tbl = P.table({ w: 1.1, round: true }); tbl.position.set(15, 0, -3.8); g.add(tbl);
  const ch1 = P.sheetedChair(1); ch1.position.set(13.4, 0, -5.6); ch1.rotation.y = 0.6; g.add(ch1);
  const ch2 = P.sheetedChair(2); ch2.position.set(16.8, 0, -5.4); ch2.rotation.y = -0.7; g.add(ch2);
  const ch3 = P.sheetedChair(3); ch3.position.set(17.0, 0, -1.4); ch3.rotation.y = -2.2; g.add(ch3);
  const gclock = P.grandfatherClock(); gclock.position.set(15.0, 0, -6.78); g.add(gclock);
  const sideL = P.sideboard({ w: 1.8 }); sideL.position.set(17.75, 0, -5.6); sideL.rotation.y = -Math.PI / 2; g.add(sideL);
  for (let i = 0; i < 4; i++) { const ph = P.framedPhoto(60 + i, { w: 0.18, h: 0.24, people: r.int(1, 2) }); ph.position.set(17.7, 1.05, -6.2 + i * 0.4); ph.rotation.y = -Math.PI / 2 + 0.25; ph.rotation.x = -0.1; g.add(ph); }
  // álbum sobre a mesa: grupo próprio (animado nos planos)
  const album = buildAlbum(); album.position.set(15.0, 0.785, -3.55); album.rotation.y = Math.PI; g.add(album);
  // janelas da sala
  { const wm = windowMesh({ w: 1.4, h: 2.0 }); wm.position.set(15.5, 1.9, -0.02); wm.rotation.y = Math.PI; g.add(wm); }
  { const wm = windowMesh({ w: 1.4, h: 2.0 }); wm.position.set(17.98, 1.9, -3.9); wm.rotation.y = -Math.PI / 2; g.add(wm); }

  // ================= PASSAGEM DO PORÃO =================
  floorPlane(-1, 1, -11.2, -8);
  ceiling(-1, 1, -11.2, -8, 2.5);
  W(3.2, 2.5, [], -1, 0, -9.6, '+x'); W(3.2, 2.5, [], 1, 0, -9.6, '-x');
  wains(3.2, -0.995, -9.6, '+x'); wains(3.2, 0.995, -9.6, '-x');
  // parede final com a porta
  W(2, 2.5, [[0.5, 1.5, 0, 2.15]], 0, 0, -11.2, '+z', wp);
  const bDoor = new THREE.Group(); bDoor.position.set(-0.5, 0, -11.24);
  const bLeaf = new THREE.Mesh(new THREE.BoxGeometry(1.0, 2.15, 0.06).translate(0.5, 1.075, 0), new THREE.MeshStandardMaterial({ map: panelT.map, normalMap: panelT.normalMap, roughness: 0.5, color: 0x22180f }));
  bDoor.add(bLeaf);
  // almofadas da porta
  for (const [y, h] of [[0.55, 0.7], [1.5, 0.8]]) bDoor.add(new THREE.Mesh(new THREE.BoxGeometry(0.7, h, 0.02).translate(0.5, y, 0.035), new THREE.MeshStandardMaterial({ color: 0x1a120c, roughness: 0.6 })));
  const lockPlate = new THREE.Mesh(new THREE.BoxGeometry(0.09, 0.26, 0.012), P.M.iron); lockPlate.position.set(0.86, 1.0, 0.04); bDoor.add(lockPlate);
  const knob = new THREE.Group(); knob.position.set(0.86, 1.06, 0.06);
  const knobM = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.03, 0.05, 16).rotateX(Math.PI / 2), P.M.iron); knobM.position.z = 0.02; knob.add(knobM);
  const knobLever = new THREE.Mesh(new THREE.BoxGeometry(0.11, 0.022, 0.02).translate(0.045, 0, 0), P.M.iron); knobLever.position.z = 0.045; knob.add(knobLever);
  bDoor.add(knob);
  const keyholeMat = new THREE.MeshBasicMaterial({ color: 0x000000 });
  const keyhole = new THREE.Mesh(new THREE.ShapeGeometry((() => { const s = new THREE.Shape(); s.absarc(0, 0.012, 0.011, 0, Math.PI * 2); const t = new THREE.Shape(); t.moveTo(-0.007, 0.006); t.lineTo(0.007, 0.006); t.lineTo(0.012, -0.028); t.lineTo(-0.012, -0.028); return [s, t]; })()), keyholeMat);
  keyhole.position.set(0.86, 0.94, 0.047); bDoor.add(keyhole);
  // luz que vaza por baixo e pela fresta
  const leakMat = new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false });
  const leak = new THREE.Mesh(new THREE.PlaneGeometry(1.0, 0.02), leakMat); leak.position.set(0.5, 0.012, 0.02); bDoor.add(leak);
  const leakFloor = new THREE.Mesh(new THREE.PlaneGeometry(1.2, 0.8).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, map: (() => { const c = TX.canvas(64, 64), x = c.getContext('2d'); const gr = x.createLinearGradient(0, 0, 0, 64); gr.addColorStop(0, 'rgba(255,255,255,1)'); gr.addColorStop(1, 'rgba(255,255,255,0)'); x.fillStyle = gr; x.fillRect(0, 0, 64, 64); return new THREE.CanvasTexture(c); })() }));
  leakFloor.position.set(0, 0.006, -10.8); g.add(leakFloor);
  // interior escuro atrás da porta (visível quando entreaberta)
  const beyond = new THREE.Mesh(new THREE.PlaneGeometry(1.2, 2.3), new THREE.MeshBasicMaterial({ color: 0x000000 }));
  beyond.position.set(0, 1.1, -11.6); g.add(beyond);
  const beyondGlow = makeLightCone({ length: 2.6, radius: 1.2, color: 0x9ab07a, intensity: 0, topRadius: 0.4 });
  beyondGlow.rotation.x = -Math.PI / 2; beyondGlow.position.set(0, 1.2, -12.8); g.add(beyondGlow);
  g.add(bDoor);
  for (let i = 0; i < 3; i++) { const ph = P.framedPhoto(80 + i, { w: 0.24, h: 0.3, people: 1, scratched: i === 1 }); ph.position.set(-0.98, 1.6, -8.7 - i * 0.8); ph.rotation.y = Math.PI / 2; g.add(ph); }

  // ================= CORREDOR SUPERIOR =================
  floorPlane(-10, -4, -8, -6.6, UP);
  ceiling(-10, -4, -8, -6.6, UP + 2.8);
  W(6, 2.8, [], -7, UP, -8, '+z'); W(6, 2.8, [], -7, UP, -6.6, '-z');
  W(1.4, 2.8, [[0.2, 1.2, 0, 2.2]], -10, UP, -7.3, '+x');
  wains(6, -7, -7.995, '+z', UP); wains(6, -7, -6.605, '-z', UP);
  for (let i = 0; i < 3; i++) { const ph = P.framedPhoto(90 + i, { w: 0.28, h: 0.36, people: 1 }); ph.position.set(-5.2 - i * 1.5, UP + 1.65, -7.98); g.add(ph); }

  // ================= QUARTO INFANTIL =================
  floorPlane(-15, -10, -11, -5, UP);
  ceiling(-15, -10, -11, -5, UP + 2.9);
  W(6, 2.9, [[3.2, 4.2, 0, 2.2]], -10, UP, -8, '-x', wpKid);   // porta (z -7.8..-6.8)
  W(6, 2.9, [[2.2, 3.6, 0.9, 2.6]], -15, UP, -8, '+x', wpKid); // janela
  W(5, 2.9, [], -12.5, UP, -11, '+z', wpKid); W(5, 2.9, [], -12.5, UP, -5, '-z', wpKid);
  const kidDoor = new THREE.Group(); kidDoor.position.set(-10.02, UP, -6.8);
  kidDoor.add(new THREE.Mesh(new THREE.BoxGeometry(0.05, 2.2, 1.0).translate(0, 1.1, -0.5), darkDoor)); kidDoor.rotation.y = 0.55; g.add(kidDoor);
  const bed = P.smallBed(); bed.position.set(-13.9, UP, -9.6); g.add(bed);
  const box = P.letterBox(); box.position.set(-13.75, UP + 0.63, -9.35); box.rotation.y = 0.15; g.add(box);
  const horse = P.rockingHorse(); horse.position.set(-11.4, UP, -5.9); horse.rotation.y = 0.7; g.add(horse);
  const dl = P.doll(); dl.position.set(-14.75, UP + 0.86, -6.0); dl.rotation.y = Math.PI / 2 + 0.3; g.add(dl);
  const shelf = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.03, 1.2), P.M.wood); shelf.position.set(-14.85, UP + 0.85, -6.2); g.add(shelf);
  const mbox = new THREE.Mesh(new THREE.BoxGeometry(0.14, 0.09, 0.1), P.M.darkWood); mbox.position.set(-14.85, UP + 0.91, -6.6); g.add(mbox);
  const blocks = P.toyBlocks(); blocks.position.set(-12.0, UP, -8.4); g.add(blocks);
  const ball = new THREE.Mesh(new THREE.SphereGeometry(0.1, 16, 12), new THREE.MeshStandardMaterial({ color: 0x5a2420, roughness: 0.6 })); ball.position.set(-11.2, UP + 0.1, -10.2); g.add(ball);
  const drawings = [['house', -12.0, -10.98, 0], ['twins', -13.0, -10.98, 0], ['woman', -14.98, -9.8, Math.PI / 2], ['sun', -11.0, -10.98, 0]];
  for (const [k, x, z, ry] of drawings) { const d = P.childDrawing(k, x * 10); d.position.set(x, UP + 1.5 + (k === 'sun' ? 0.2 : 0), z); d.rotation.y = ry; g.add(d); }
  const kidWin = windowMesh({ w: 1.4, h: 1.7 }); kidWin.position.set(-14.98, UP + 1.75, -7.9); kidWin.rotation.y = Math.PI / 2; g.add(kidWin);
  const curt = new THREE.Mesh(new THREE.PlaneGeometry(0.7, 2.0, 20, 4), new THREE.MeshStandardMaterial({ color: 0x8a8278, roughness: 1, side: THREE.DoubleSide, transparent: true, opacity: 0.85 }));
  curt.position.set(-14.9, UP + 1.7, -7.25); curt.rotation.y = Math.PI / 2; g.add(curt);

  // tábuas do piso do quarto sobre o "vazio" (para as batidas: leve vibração)
  g.traverse(o => { if (o.isMesh && o.castShadow === false && !o.material.transparent) o.receiveShadow = true; });

  // poeira em suspensão por ambiente
  const dustHall = makeDust({ count: 1400, box: [8, 4, 8], center: [0, 1.8, -4] });
  const dustCorr = makeDust({ count: 1000, box: [9, 3, 2], center: [8, 1.5, -2.4] });
  const dustRoom = makeDust({ count: 1000, box: [6, 3, 7], center: [-12.5, UP + 1.5, -8] });
  const dustBase = makeDust({ count: 600, box: [2, 2.5, 3.5], center: [0, 1.25, -9.6] });
  const dustLiving = makeDust({ count: 1000, box: [6, 3, 7], center: [15, 1.5, -3.5] });
  g.add(dustHall, dustCorr, dustRoom, dustBase, dustLiving);

  // noite do lado de fora da porta de entrada
  const nightC = TX.canvas(64, 256), nx = nightC.getContext('2d'); const ng = nx.createLinearGradient(0, 0, 0, 256); ng.addColorStop(0, '#0e1620'); ng.addColorStop(0.7, '#1c2a34'); ng.addColorStop(1, '#0a0d10'); nx.fillStyle = ng; nx.fillRect(0, 0, 64, 256);
  const night = new THREE.Mesh(new THREE.PlaneGeometry(6, 5), new THREE.MeshBasicMaterial({ map: new THREE.CanvasTexture(nightC), fog: false }));
  night.position.set(0, 2.0, 2.5); night.rotation.y = Math.PI; g.add(night);
  g.userData = { frontDoor, bDoor, knob, keyhole, keyholeMat, leakMat, leakFloor, beyondGlow, gclock, album, box, kidDoor, chand, curt,
    dusts: [dustHall, dustCorr, dustRoom, dustBase, dustLiving], horse, corridorPhotos };
  return g;
}

// ---------- Álbum de fotografias ----------
export function buildAlbum() {
  P.mats();
  const g = new THREE.Group();
  const cover = new THREE.MeshStandardMaterial({ color: 0x3a1a16, roughness: 0.7 });
  const pageMat = new THREE.MeshStandardMaterial({ color: 0x2a2620, roughness: 0.9 });
  const W = 0.32, D = 0.4;
  // capa de trás + bloco de páginas (lado direito)
  const back = new THREE.Mesh(new THREE.BoxGeometry(W, 0.012, D).translate(W / 2, 0.006, 0), cover); g.add(back);
  const block = new THREE.Mesh(new THREE.BoxGeometry(W - 0.01, 0.03, D - 0.012).translate(W / 2, 0.026, 0), new THREE.MeshStandardMaterial({ color: 0xb8ab8c, roughness: 0.9 })); g.add(block);
  // capa da frente (articulada na lombada)
  const frontPivot = new THREE.Group(); g.add(frontPivot);
  const front = new THREE.Mesh(new THREE.BoxGeometry(W, 0.012, D).translate(W / 2, 0.048, 0), cover); frontPivot.add(front);
  const title = new THREE.Mesh(new THREE.PlaneGeometry(0.14, 0.04), new THREE.MeshStandardMaterial({ color: 0x8a6a3a, metalness: 0.8, roughness: 0.4 }));
  title.rotation.x = -Math.PI / 2; title.position.set(W / 2, 0.0545, -0.1); frontPivot.add(title);
  // páginas que viram (cada uma com fotos dos dois lados)
  const pages = [];
  for (let i = 0; i < 5; i++) {
    const pv = new THREE.Group(); pv.position.y = 0.042 + i * 0.0006;
    const pg = new THREE.Mesh(new THREE.BoxGeometry(W - 0.012, 0.0015, D - 0.016).translate(W / 2, 0, 0), pageMat);
    pv.add(pg);
    for (const side of [1, -1]) {
      const n = i === 4 && side === 1 ? 0 : 2;
      for (let k = 0; k < n; k++) {
        const t = new THREE.CanvasTexture(P.oldPhotoCanvas(200 + i * 10 + k + (side > 0 ? 0 : 5), { people: 1 + ((i + k) % 3), scratched: i === 2 && k === 1 }));
        t.colorSpace = THREE.SRGBColorSpace;
        const ph = new THREE.Mesh(new THREE.PlaneGeometry(0.11, 0.14), new THREE.MeshStandardMaterial({ map: t, roughness: 0.5 }));
        ph.rotation.x = -Math.PI / 2 * side; ph.rotation.z = (k - 0.5) * 0.06;
        ph.position.set(W / 2 + (k ? 0.065 : -0.065) * (side > 0 ? 1 : -1), 0.0009 * side, (k ? 0.08 : -0.08) * (side > 0 ? 1 : -1));
        if (side < 0) ph.rotation.y = Math.PI;
        pv.add(ph);
      }
    }
    g.add(pv); pages.push(pv);
  }
  g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  g.userData = { frontPivot, pages, W, D };
  return g;
}
