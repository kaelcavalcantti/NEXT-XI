import * as THREE from 'three';
import { Batch, BOX, CYL, CYL8, SPH, CONE, mat, mesh, grp, R, rnd, pick, keys, sm, clamp, lerp, eio, eo, lin, makeSky, addLights, brickTex, plasterTex, concreteTex, asphaltTex, cobbleTex, woodTex, textTex, billTex, portraitTex, stripeTex } from './lib.js';
import { figure, face, walk, sit, suv, classic, beetle, bus, cashStack, photoFrame, phone } from './actors.js';
import { mats, building, wire, papelLine, palm, streetLamp, cathedral, flatten } from './build.js';

const PI = Math.PI;
export const ORANGE = 0xD97757;

// ---------- characters ----------
export function makeAlejandro() {
  const f = figure({ top: ORANGE, legs: ORANGE, sleeve: ORANGE, fore: 0xb97c55, skin: 0xb97c55, straps: 0xB4553A, collar: 0xEFE8DC, hairStyle: 'quiff', hair: 0x1a110c, shoe: 0x4a2e1c, joint: 0x8a4a30, outline: true });
  // wrench in right hand
  const w = grp(f.J.handR, [0, -.06, .03]); w.visible = false;
  const steel = mat(0xc9c4bc, { r: .25, m: 1 });
  mesh(BOX, steel, w, [0, -.08, 0], [.022, .22, .035]);
  mesh(new THREE.TorusGeometry(.03, .012, 6, 14), steel, w, [0, -.2, 0], [1, 1, 1], [PI / 2, 0, 0]);
  f.wrench = w;
  // rag in back pocket
  mesh(BOX, mat(0xC9302C, { r: .9 }), f.J.hips, [.08, -.05, -.13], [.08, .14, .02]);
  return f;
}
export function makeBoss() {
  return figure({ top: 0x1b1a20, legs: 0x1b1a20, sleeve: 0x1b1a20, fore: 0x1b1a20, skin: 0xa86e4a, tie: 0x8a1c14, shirt: 0x0d0d0f, hat: 0x141414, glasses: true, mustache: true, hair: 0x120c08, chain: true, watch: true, cuffs: 0xe9e2d4, bulk: 1.05, joint: 0x101014, shoe: 0x0a0a0a });
}
export function makeBruiser() {
  return figure({ top: 0x3a3a44, legs: 0x3a3a44, sleeve: 0x3a3a44, fore: 0x3a3a44, skin: 0x8f5c3e, tie: 0x111111, shirt: 0x141414, glasses: true, hair: 0x120c08, bulk: 1.28, joint: 0x1a1a1e, shoe: 0x0a0a0a });
}

function setCam(cam, p, l, fov = 45, roll = 0) {
  cam.position.set(...p); cam.up.set(Math.sin(roll), Math.cos(roll), 0); cam.lookAt(...l); cam.fov = fov; cam.updateProjectionMatrix();
}
function spinWheels(v, dist, r = .42) { for (const w of v.wheels) w.userData.spin.rotation.x = dist / r; }
function mouthEnv(env, lt0, lt) { if (!env) return 0; const i = Math.floor((lt - lt0) * 30); if (i < 0 || i >= env.length) return 0; return env[i]; }

function baseScene(env, sunDir, o = {}) {
  const scene = new THREE.Scene(); scene.environment = env; scene.environmentIntensity = o.envI ?? .35;
  const sky = makeSky(sunDir); scene.add(sky); scene.userData.sky = sky;
  const L = addLights(scene, sunDir, o); scene.background = new THREE.Color(0xE0481E);
  return { scene, sky, L };
}

// =====================================================================
// SET 1 — AVENUE IN GUADALAJARA
// =====================================================================
export function setAvenue(env, TL) {
  const sunDir = [-0.42, 0.16, -1];
  const { scene, sky, L } = baseScene(env, sunDir, { key: 3.6 });
  scene.fog = new THREE.Fog(0xEE9A62, 70, 360);
  const m = mats(); const b = new Batch();
  const asph = mat(0xffffff, { map: asphaltTex(), r: .85 });
  const side = mat(0xffffff, { map: concreteTex(64, 28), r: .9 });
  const curb = mat(0xB9A991, { r: .9 });
  const paint = mat(0xEFE3C2, { r: .7 });
  const grass = mat(0x4f6e35, { r: .95 });
  b.add(BOX, asph, [0, -.05, -80], [0, 0, 0], [16, .1, 400], .25);
  b.add(BOX, curb, [0, .1, -80], [0, 0, 0], [2.2, .2, 400], .5);
  b.add(BOX, grass, [0, .21, -80], [0, 0, 0], [1.9, .04, 400]);
  for (const s of [1, -1]) { b.add(BOX, side, [s * 9.6, .08, -80], [0, 0, 0], [3.2, .16, 400], .4); b.add(BOX, curb, [s * 8.05, .09, -80], [0, 0, 0], [.18, .18, 400]); }
  for (let z = 120; z > -260; z -= 7) for (const s of [1, -1]) b.add(BOX, paint, [s * 4.5, .005, z], [0, 0, 0], [.14, .01, 3.2]);
  // plaza + cathedral
  b.add(BOX, mat(0xffffff, { map: cobbleTex(), r: .9 }), [0, -.04, -235], [0, 0, 0], [60, .1, 50], .3);
  const cat = cathedral(); cat.position.set(0, 0, -232); scene.add(cat);
  // buildings
  for (const s of [1, -1]) {
    let z = 95;
    while (z > -215) {
      const w = R(7, 12); const far = z < -120;
      const h = far ? R(9, 17) : R(7.5, 14.5);
      if (rnd() < .1 && z < 60) { z -= 8; continue; } // cross street gap
      const g = building(w, h, 10, {}); g.position.set(s * 11, 0, z - w / 2); g.rotation.y = -s * PI / 2; scene.add(g);
      z -= w + .05;
    }
  }
  // median palms + lamps
  for (let z = 80; z > -205; z -= 14) { palm(b, 0, z, R(6.5, 8.5)); streetLamp(b, 0, z - 7, 6, rnd() < .5 ? 1 : -1); }
  // sidewalk poles + wires
  for (const s of [1, -1]) {
    let prev = null;
    for (let z = 90; z > -210; z -= 22) {
      const x = s * 8.6; b.add(CYL8, m.pole, [x, 4.2, z], [0, 0, 0], [.12, 8.4, .12]); b.add(BOX, m.pole, [x, 8, z], [0, 0, 0], [1.4, .1, .1]);
      if (prev) for (const dx of [-.6, 0, .6]) wire(b, m.wire, [x + dx, 7.95, prev], [x + dx, 7.95, z], .45, .015, 10);
      prev = z;
    }
    for (let z = 85; z > -200; z -= 26) { b.add(BOX, m.trimDark, [s * 9.9, .5, z], [0, 0, 0], [1.4, .1, .4]); b.add(BOX, m.iron, [s * 9.9, .25, z], [0, 0, 0], [.08, .5, .3]); b.add(CYL8, m.shutter[0], [s * 9.3, .5, z - 4], [0, 0, 0], [.25, .8, .25]); }
  }
  // papel picado across avenue
  for (let z = 70; z > -190; z -= 15) papelLine(b, [-11, R(7, 8.2), z + R(-2, 2)], [11, R(7, 8.2), z + R(-2, 2)], R(1.1, 1.8));
  const statics = b.build(); scene.add(statics);

  // traffic
  const traffic = [];
  const addCar = (obj, x, z0, v, dir) => { obj.g.position.set(x, 0, z0); obj.g.rotation.y = dir > 0 ? 0 : PI; scene.add(obj.g); traffic.push({ obj, x, z0, v, dir }); };
  const cols = [[0xEFE3C8, 0x2E8C86], [0x3D6FB6, 0xEFE3C8], [0xE2B13A, 0xF2EEE2], [0x8C2A2A, 0xEFE3C8], [0x2E6B4F, 0xEDE3C4]];
  addCar({ g: flatten(beetle(0x2E8C86).g), wheels: [] }, -2.8, -40, 10, 1);
  addCar({ g: flatten(classic(0xE2B13A, 0xF2EEE2).g), wheels: [] }, -6.2, -95, 9, 1);
  addCar({ g: flatten(bus().g), wheels: [] }, -6.2, -20, 8, 1);
  addCar({ g: flatten(beetle(0xF2EEE2).g), wheels: [] }, -2.8, -130, 12, 1);
  addCar({ g: flatten(classic(0x3D6FB6, 0xEFE3C8).g), wheels: [] }, -2.8, 10, 11, 1);
  addCar({ g: flatten(beetle(0xC9302C).g), wheels: [] }, 2.8, -32, 8.5, -1);
  addCar({ g: flatten(classic(0x2E6B4F, 0xEDE3C4).g), wheels: [] }, 6.0, -150, 6, -1);
  for (let i = 0; i < 6; i++) { const c = pick(cols); const o = { g: flatten((rnd() < .5 ? beetle(c[0]) : classic(c[0], c[1])).g), wheels: [] }; o.g.position.set(rnd() < .5 ? 7.4 : -7.4, 0, -60 - i * 22); o.g.rotation.y = o.g.position.x > 0 ? PI : 0; scene.add(o.g); }

  // hero SUV with the two men
  const S = suv(0x0c0c0f, { glassOp: .32 }); S.g.rotation.y = PI; scene.add(S.g);
  const drv = makeBruiser(), boss = makeBoss();
  for (const [f, x, z] of [[drv, .45, .25], [boss, .45, -.85]]) { f.root.scale.setScalar(.8); f.root.position.set(x, 1.25 - .5 * .8, z); S.body.add(f.root); }
  const wheelG = grp(S.body, [.45, 1.62, .78], [-1.0, 0, 0]); mesh(new THREE.TorusGeometry(.17, .025, 6, 18), mat(0x111111), wheelG);
  const cabinLight = new THREE.PointLight(0xffb070, 5, 3.5, 1.5); cabinLight.position.set(0, 1.95, -.2); S.body.add(cabinLight);
  const suvZ = t => 2 - 11 * (t - 5.2);

  const shots = [0, 6.4, 9.6];
  function update(t, cam) {
    for (const c of traffic) { c.obj.g.position.z = c.z0 + c.v * t * c.dir; }
    const z = suvZ(t); S.g.position.set(5.9, 0, z); spinWheels(S, 11 * t); S.body.position.y = Math.sin(t * 9) * .008;
    // men inside
    for (const f of [drv, boss]) { f.reset(); sit(f); }
    drv.J.shL.rotation.x = drv.J.shR.rotation.x = -1.15; drv.J.elL.rotation.x = drv.J.elR.rotation.x = -.35; drv.J.head.rotation.y = .25 * Math.sin(t * 1.3) ;
    boss.J.shL.rotation.x = -.9 + .35 * Math.sin(t * 3.1); boss.J.elL.rotation.x = -1.2 + .3 * Math.sin(t * 4.3); boss.J.shL.rotation.z = .3;
    boss.J.head.rotation.y = .15 + .1 * Math.sin(t * 2.2); boss.J.head.rotation.x = .05 * Math.sin(t * 5);
    face(boss, { mouth: Math.max(0, Math.sin(t * 13)) * .6 * (Math.sin(t * 2.1) > -.3 ? 1 : 0) , angry: .3 });
    face(drv, { angry: .5, mouth: .1 });
    let id;
    if (t < shots[1]) {
      id = 'a';
      const p = keys(t, [[0, [1.6, 10.5, 34]], [3.0, [1.7, 4.2, 15]], [6.4, [2.2, .45, -2.2]]], x => x < .5 ? 2 * x * x : 1 - Math.pow(-2 * x + 2, 2) / 2);
      const l = keys(t, [[0, [0, 32, -40]], [3.0, [0.5, 10, -60]], [6.4, [3.6, 1.6, -60]]]);
      setCam(cam, p, l, 52, keys(t, [[0, .03], [6.4, -.04]]));
      L.key.follow([0, 0, p[2] - 25], 45);
    } else if (t < shots[2]) {
      id = 'b'; const u = (t - shots[1]) / (shots[2] - shots[1]);
      setCam(cam, [5.9 - 8.2 + u * .9, 2.05, z + 1.2 - u * 1.6], [5.9, 1.65, z - .3 - u * .5], 38, 0);
      L.key.follow([5.9, 0, z], 18);
    } else {
      id = 'c'; const zc = suvZ(11.75) - 2.45;
      setCam(cam, [4.25, .38, zc], [5.6, 2.1, zc + 12], 56, .05 + (t - 9.6) * .02);
      L.key.follow([5.9, 0, zc + 8], 25);
    }
    return id;
  }
  return { scene, update, shots, sky };
}

// =====================================================================
// SET 2 — WORKSHOP INTERIOR (scenes 2 & 4)
// =====================================================================
export function setWorkshop(env, TL) {
  const sunDir = [0.72, 0.36, 0.6];
  const { scene, sky, L } = baseScene(env, sunDir, { key: 4.2, hemi: .45, envI: .25 });
  const m = mats(); const b = new Batch();
  const brick = m.brick[0]; const plaster = m.plaster[0]; const brick2 = m.brick[1];
  const floor = mat(0xffffff, { map: concreteTex(46, 30), r: .7 });
  b.add(BOX, floor, [0, -.05, -1], [0, 0, 0], [12, .1, 12], .3);
  b.add(BOX, brick, [0, 2.3, -6.15], [0, 0, 0], [10.6, 4.6, .3], .5);
  b.add(BOX, plaster, [-5.15, 2.3, -1], [0, 0, 0], [.3, 4.6, 10.6], .4);
  // right wall with window z[-3.4,-1.0] y[1.5,3.3]
  const rw = (y0, y1, z0, z1) => b.add(BOX, brick2, [5.15, (y0 + y1) / 2, (z0 + z1) / 2], [0, 0, 0], [.3, y1 - y0, z1 - z0], .5);
  rw(0, 1.5, -6.3, 4.3); rw(3.3, 4.6, -6.3, 4.3); rw(1.5, 3.3, -6.3, -3.4); rw(1.5, 3.3, -1.0, 4.3);
  b.add(BOX, m.stone, [5.0, 1.45, -2.2], [0, 0, 0], [.5, .1, 2.6]);
  for (let i = 0; i < 6; i++) b.add(CYL8, m.iron, [5.1, 2.4, -3.2 + i * .42], [0, 0, 0], [.02, 1.8, .02]);
  // front wall with door x[-3.4,3.4] y[0,3.7]
  b.add(BOX, plaster, [-4.3, 2.3, 4.3], [0, 0, 0], [1.8, 4.6, .3], .4); b.add(BOX, plaster, [4.3, 2.3, 4.3], [0, 0, 0], [1.8, 4.6, .3], .4); b.add(BOX, plaster, [0, 4.15, 4.3], [0, 0, 0], [6.8, .9, .3], .4);
  b.add(CYL, m.iron, [0, 3.95, 4.15], [0, 0, PI / 2], [.25, 6.8, .25]);
  // ceiling + beams + strung bulbs
  b.add(BOX, m.trimDark, [0, 4.65, -1], [0, 0, 0], [10.6, .1, 10.6]);
  for (let z = -5.5; z < 4; z += 1.5) b.add(BOX, m.wood, [0, 4.45, z], [0, 0, 0], [10.3, .25, .2]);
  const bulbs = wire(b, m.wire, [-5, 3.9, 3.5], [5, 3.9, -5.5], .7, .01, 20);
  const glow = mat(0xfff0c0, { e: 0xffc070, ei: 2.5 });
  for (let i = 2; i < bulbs.length - 1; i += 2) b.add(SPH, glow, [bulbs[i].x, bulbs[i].y - .1, bulbs[i].z], [0, 0, 0], [.05, .07, .05]);
  // outside: street + facades across
  b.add(BOX, mat(0xffffff, { map: cobbleTex(), r: .9 }), [0, -.06, 14], [0, 0, 0], [60, .1, 20], .3);
  for (const [x, w] of [[-9, 8], [0, 9.5], [9.5, 9]]) { const g = building(w, R(6, 8), 6, {}); g.position.set(x, 0, 19); g.rotation.y = PI; scene.add(g); }
  for (const [x, w] of [[10.5, 9]]) { const g = building(w, 7, 6, {}); g.position.set(14, 0, -2); g.rotation.y = -PI / 2; scene.add(g); }
  // workbench along left wall + pegboard
  const wood = m.wood;
  b.add(BOX, wood, [-4.6, .92, -3.2], [0, 0, 0], [.85, .08, 4.6]);
  for (const z of [-5.4, -1.0]) for (const x of [-4.9, -4.25]) b.add(BOX, m.iron, [x, .45, z], [0, 0, 0], [.06, .9, .06]);
  b.add(BOX, wood, [-4.6, .25, -3.2], [0, 0, 0], [.8, .04, 4.4]);
  for (let i = 0; i < 5; i++) b.add(BOX, pick([m.shutter[0], m.shutter[1], m.trimDark]), [-4.6, .37, -5 + i * .9], [0, 0, 0], [.5, .22, .5]);
  const peg = mat(0x8a6040, { r: .8 });
  b.add(BOX, peg, [-4.98, 2.0, -3.3], [0, 0, 0], [.04, 1.3, 3.8]);
  const steel = mat(0xb8b2aa, { r: .3, m: 1 }); const red = mat(0xB5322A, { r: .5 });
  for (let i = 0; i < 9; i++) { const z = -5 + i * .4; b.add(BOX, steel, [-4.94, 2.05 - (i % 3) * .05, z], [0, 0, 0], [.02, .4 + (i % 3) * .1, .04]); b.add(CYL8, steel, [-4.94, 2.3 - (i % 3) * .05, z], [0, 0, PI / 2], [.04, .02, .04]); }
  for (let i = 0; i < 4; i++) { const z = -4.8 + i * .55; b.add(BOX, red, [-4.94, 1.55, z], [0, 0, 0], [.03, .2, .04]); b.add(BOX, steel, [-4.94, 1.75, z], [0, 0, 0], [.02, .22, .015]); }
  b.add(BOX, m.trimDark, [-4.94, 1.6, -1.8], [0, 0, 0], [.03, .3, .5]);
  // back wall: shelves with cans, calendar, ladder
  for (const y of [1.6, 2.4]) { b.add(BOX, wood, [2.2, y, -5.85], [0, 0, 0], [3.2, .05, .45]); for (let i = 0; i < 9; i++) b.add(CYL, pick([red, m.shutter[0], m.shutter[2], mat(0xE2B13A, { r: .5 })]), [.8 + i * .33, y + .14, -5.85], [0, 0, 0], [.1, .25, .1]); }
  b.add(BOX, mat(0xffffff, { map: textTex([{ t: 'NOVIEMBRE', font: '90px Anton', color: '#B5322A', y: 70 }, { t: 'REFACCIONES DEL BAJÍO', font: '44px "Bebas Neue"', color: '#333', y: 470 }], { bg: '#F1E8D4', w: 512, h: 512 }), r: .9 }), [-1.8, 2.3, -5.99], [0, 0, 0], [.8, .8, .02]);
  for (const x of [3.9, 4.5]) b.add(BOX, wood, [x, 1.9, -5.7], [-.15, 0, 0], [.06, 3.8, .06]);
  for (let y = .3; y < 3.7; y += .35) b.add(BOX, wood, [4.2, y, -5.7 + y * .15 * 0 - (1.9 - y) * .15 * -1 * 0], [-.15, 0, 0], [.6, .04, .04]);
  // tires, drum
  const tire = new THREE.TorusGeometry(.33, .13, 10, 20);
  for (let i = 0; i < 4; i++) b.add(tire, mat(0x151313, { r: .85 }), [3.9, .13 + i * .26, -4.3], [PI / 2, 0, i * .3], [1, 1, 1]);
  b.add(CYL, red, [4.3, .45, 2.8], [0, 0, 0], [.32, .9, .32]); b.add(CYL, red, [3.6, .45, 3.1], [0, 0, 0], [.32, .9, .32]);
  // central workbench B2 (photo, bill, savings tin)
  b.add(BOX, wood, [-2.6, .92, -4.0], [0, 0, 0], [2.4, .08, .9]);
  for (const x of [-3.7, -1.5]) for (const z of [-4.35, -3.65]) b.add(BOX, m.iron, [x, .45, z], [0, 0, 0], [.06, .9, .06]);
  b.add(BOX, m.trimDark, [-1.65, 1.03, -4.2], [0, 0, 0], [.22, .14, .12]); // vise
  scene.add(b.build());

  // hood leaning on wall
  const car = classic(0x2E8C86, 0xEFE3C8); car.g.position.set(1.4, 0, -.2); car.g.rotation.y = PI + .38; scene.add(car.g);
  car.hood.visible = false;
  const hood = mesh(new THREE.BoxGeometry(1.62, .05, 1.72), mat(0x2E8C86, { r: .25, m: .35 }), scene, [4.85, .9, 1.2], [1, 1, 1], [0, PI / 2, 1.25]);
  // lamp over car
  const lampP = grp(scene, [.6, 4.4, -2.6]);
  mesh(CYL8, m.iron, lampP, [0, -.5, 0], [.01, 1, .01]);
  mesh(new THREE.ConeGeometry(.3, .25, 20, 1, true), mat(0x2E6B4F, { r: .5, m: .3, side: THREE.DoubleSide }), lampP, [0, -1.05, 0]);
  mesh(SPH, glow, lampP, [0, -1.13, 0], [.07, .07, .07], [0, 0, 0], false);
  const lamp = new THREE.PointLight(0xffc27a, 9, 7, 1.6); lamp.position.set(0, -1.25, 0); lampP.add(lamp);
  const benchLight = new THREE.PointLight(0xffb070, 4, 4, 1.6); benchLight.position.set(-2.6, 2.3, -3.3); scene.add(benchLight);
  // god-ray quads from window & door
  const rayTex = (() => { const c = document.createElement('canvas'); c.width = 64; c.height = 256; const x = c.getContext('2d'); const g = x.createLinearGradient(0, 0, 0, 256); g.addColorStop(0, 'rgba(255,200,130,0.0)'); g.addColorStop(.25, 'rgba(255,200,130,0.5)'); g.addColorStop(1, 'rgba(255,200,130,0)'); x.fillStyle = g; x.fillRect(0, 0, 64, 256); return new THREE.CanvasTexture(c); })();
  const rayM = new THREE.MeshBasicMaterial({ map: rayTex, transparent: true, opacity: .22, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
  for (let i = 0; i < 3; i++) { const r = mesh(new THREE.PlaneGeometry(1.0, 6), rayM, scene, [3.0, 1.5, -2.2 + (i - 1) * .7], [1, 1, 1], [0, PI / 2, 1.0], false); }

  // props on bench B2
  const photo = photoFrame(scene, [-3.35, .96, -3.95], [0, PI, 0], 1.1);
  const bill = mesh(new THREE.PlaneGeometry(.26, .34), mat(0xffffff, { map: billTex(), r: .8 }), scene, [-2.85, .965, -3.85], [1, 1, 1], [-PI / 2, 0, PI + .25]);
  const tin = grp(scene, [-1.95, .96, -3.8]);
  mesh(CYL, mat(0xffffff, { map: textTex([{ t: 'AHORROS', font: '110px Anton', color: '#F7E9C9' }], { bg: '#2E6B4F', w: 512, h: 160 }), r: .4, m: .4 }), tin, [0, .1, 0], [.09, .2, .09]);
  mesh(new THREE.TorusGeometry(.09, .008, 6, 20), mat(0xbbbbbb, { r: .2, m: 1 }), tin, [0, .2, 0], [1, 1, 1], [PI / 2, 0, 0]);
  const coin = mesh(CYL, mat(0xD9A74A, { r: .25, m: 1 }), scene, [-1.75, 1.0, -3.65], [.025, .006, .025]);
  const coin2 = mesh(CYL, mat(0xb8b2aa, { r: .25, m: 1 }), tin, [.0, .17, 0], [.022, .005, .022]);
  // cash pile (scene 4)
  const cash = []; const cashG = grp(scene, [-2.35, .96, -3.95]);
  const cpos = [[0, 0, 0], [.32, 0, 0], [0, 0, .17], [.32, 0, .17], [.16, .12, .08], [-.1, .12, .02], [.3, .12, .14], [.12, .24, .07]];
  for (const p of cpos) { const c = cashStack(cashG, p, [0, R(-.15, .15), 0]); c.userData.base = p; cash.push(c); }

  // characters
  const A = makeAlejandro(); scene.add(A.root);
  const boss = makeBoss(); scene.add(boss.root);

  const D2 = TL.dur.s2;
  const S2 = [0, 3.1, 4.1, 7.5];
  const S4 = [0, 1.7, 3.5, 5.3];
  // mode: 's2' | 's4'
  function update(t, cam, mode) {
    A.reset(); boss.reset(); A.wrench.visible = false;
    const car_front = new THREE.Vector3(0, 0, 3.05).applyAxisAngle(new THREE.Vector3(0, 1, 0), car.g.rotation.y).add(car.g.position);
    lamp.intensity = 9; benchLight.intensity = 4;
    let id;
    if (mode === 's2') {
      boss.root.visible = false; cashG.visible = false; coin.visible = true;
      // Alejandro at the car engine
      A.root.position.set(car_front.x + .25, 0, car_front.z - .3); A.root.rotation.y = car.g.rotation.y - PI;
      A.J.spine.rotation.x = .62; A.J.hips.rotation.x = .1; A.J.head.rotation.x = -.35;
      A.J.hipL.rotation.x = A.J.hipR.rotation.x = -.15; A.J.kneeL.rotation.x = A.J.kneeR.rotation.x = .2;
      A.J.shL.rotation.x = -.9; A.J.elL.rotation.x = -.7; A.J.shL.rotation.z = .25;
      A.wrench.visible = true; A.J.shR.rotation.x = -.95 + .18 * Math.sin(t * 9); A.J.elR.rotation.x = -.6 + .2 * Math.sin(t * 9 + 1); A.J.handR.rotation.z = .4 * Math.sin(t * 9);
      face(A, { brow: .1, look: 0, lookY: -.6, smile: .3 });
      if (t < S2[1]) {
        id = 'a';
        const p = keys(t, [[0, [-1.6, 2.5, 4.0]], [S2[1], [-.4, 1.85, 2.2]]], lin); const l = keys(t, [[0, [1.2, 1.4, -2.5]], [S2[1], [1.2, 1.45, -2.6]]], lin);
        setCam(cam, p, l, 46);
      } else if (t < S2[2]) {
        id = 'b'; const u = t - S2[1];
        // coin spins down next to the tin
        coin.position.set(-1.75, .968, -3.65); coin.rotation.set(PI / 2 * Math.max(0, 1 - u * 1.4) * Math.abs(Math.cos(u * 25)) , u * 9, 0);
        setCam(cam, [-1.45, 1.35, -3.15 + u * .08], [-1.9, 1.0, -3.8], 38);
      } else if (t < S2[3]) {
        id = 'c'; const u = (t - S2[2]) / (S2[3] - S2[2]);
        setCam(cam, keys(u, [[0, [-3.3, 1.55, -4.7]], [1, [-3.0, 1.3, -4.5]]], lin), keys(u, [[0, [-3.35, 1.12, -3.95]], [.55, [-3.2, 1.08, -3.9]], [1, [-2.85, .97, -3.85]]]), keys(u, [[0, 34], [1, 30]]));
      } else {
        id = 'd'; const u = t - S2[3];
        // he straightens up, looks at the bench, eyes pop
        A.J.spine.rotation.x = .15; A.J.head.rotation.x = 0; A.J.head.rotation.y = -.5; A.wrench.visible = true; A.J.shR.rotation.x = -.3; A.J.elR.rotation.x = -.5; A.J.handR.rotation.z = 0;
        A.J.shL.rotation.x = -.2; A.J.elL.rotation.x = -.3;
        const pop = sm(.5, .8, u);
        face(A, { brow: pop * .9, eye: 1 + pop * .35, pupil: 1 - pop * .4, mouth: pop * .7, sweat: sm(.9, 1.4, u), t: u, look: -.8 });
        const hp = new THREE.Vector3(A.root.position.x, 1.72, A.root.position.z);
        const dir = new THREE.Vector3(Math.sin(A.root.rotation.y - .5), 0, Math.cos(A.root.rotation.y - .5));
        const cp = hp.clone().addScaledVector(dir, keys(u, [[0, 2.0], [.6, 1.8], [.7, 1.35], [2.5, 1.25]], eo)); cp.y = 1.68;
        setCam(cam, [cp.x, cp.y, cp.z], [hp.x, 1.74, hp.z], keys(u, [[0, 40], [.7, 34]]), Math.sin(u * 40) * .01 * sm(.6, .8, u) * (1 - sm(1, 1.4, u)));
      }
    } else {
      // ---- scene 4: the deal at the bench ----
      coin.visible = false; boss.root.visible = true; cashG.visible = true;
      A.root.position.set(-2.55, 0, -4.85); A.root.rotation.y = 0;
      boss.root.position.set(-.9, 0, -3.65); boss.root.rotation.y = -PI / 2 - .3;
      // cash drops in the first 1.5s
      cash.forEach((c, i) => { const td = .15 + i * .14; const u = clamp((t - td) / .25); const bp = c.userData.base; c.position.set(bp[0], bp[1] + (1 - u * u) * .9, bp[2]); c.visible = t > td - .05; });
      boss.J.shR.rotation.x = keys(t, [[0, -1.0], [1.4, -1.0], [1.8, -.1]]); boss.J.elR.rotation.x = -.5;
      face(boss, { angry: .2, smile: .2 });
      if (t < S4[1]) {
        id = 'a';
        A.J.spine.rotation.x = .08; A.J.head.rotation.y = keys(t, [[0, .0], [.6, .45], [1.7, .4]]);
        A.J.shL.rotation.x = A.J.shR.rotation.x = -.35; A.J.elL.rotation.x = A.J.elR.rotation.x = -.9; A.J.shL.rotation.z = -.3; A.J.shR.rotation.z = .3; // arms crossed-ish
        face(A, { angry: .6, squint: .5, brow: -.3, look: .9, mouth: 0 });
        setCam(cam, keys(t, [[0, [-2.0, 1.35, -1.7]], [S4[1], [-2.15, 1.45, -2.3]]], lin), [-2.4, 1.45, -4.6], 40);
      } else if (t < S4[2]) {
        id = 'b'; const u = (t - S4[1]) / (S4[2] - S4[1]);
        A.J.spine.rotation.x = .2; A.J.head.rotation.x = .35; A.J.head.rotation.y = -.3;
        A.J.shR.rotation.x = -.6; A.J.elR.rotation.x = -.8;
        face(A, { worry: .7, brow: .2, lookY: -1 });
        setCam(cam, keys(u, [[0, [-2.75, 1.85, -5.35]], [1, [-2.95, 1.6, -4.95]]], lin), keys(u, [[0, [-3.0, 1.0, -3.6]], [1, [-3.3, 1.1, -3.8]]]), 42);
        benchLight.intensity = 6;
      } else if (t < S4[3]) {
        id = 'c'; const u = t - S4[2];
        // handshake over the bench
        A.J.shR.rotation.x = -1.25; A.J.elR.rotation.x = -.2; A.J.shR.rotation.z = .1;
        boss.root.position.set(-3.0, 0, -3.3); boss.root.rotation.y = PI; boss.J.spine.rotation.x = .2; A.J.spine.rotation.x = .25;
        boss.J.shR.rotation.x = -1.2; boss.J.elR.rotation.x = -.25; boss.J.shR.rotation.z = -.2;
        const shake = Math.sin(u * 14) * .12 * sm(.2, .4, u);
        A.J.shR.rotation.x += shake; boss.J.shR.rotation.x += shake;
        face(A, { brow: .1, smile: .4 });
        setCam(cam, keys(u, [[0, [-1.55, 1.38, -4.25]], [1.8, [-1.7, 1.33, -4.15]]], lin), [-2.8, 1.28, -4.05], 44, -.1);
      } else {
        id = 'd'; const u = t - S4[3];
        A.wrench.visible = true; A.J.shR.rotation.x = -.5; A.J.shR.rotation.z = -.7 - .15 * sm(.3, .6, u); A.J.elR.rotation.x = -1.9;
        A.J.head.rotation.z = .12 * sm(.2, .6, u);
        face(A, { brow: .7, smile: 1, eye: 1.1, mouth: .25 });
        setCam(cam, keys(u, [[0, [-2.45, 1.7, -3.2]], [2.2, [-2.5, 1.72, -3.5]]], lin), [-2.55, 1.72, -4.85], 38, .03);
      }
    }
    L.key.follow([0, 1, -1], 12);
    return id;
  }
  return { scene, update, sky };
}

// =====================================================================
// SET 3 — WORKSHOP EXTERIOR (the visit)
// =====================================================================
export function setStreet(env, TL) {
  const sunDir = [-0.9, 0.28, 0.45];
  const { scene, sky, L } = baseScene(env, sunDir, { key: 3.8 });
  scene.fog = new THREE.Fog(0xEE9A62, 40, 200);
  const m = mats(); const b = new Batch();
  const cob = mat(0xffffff, { map: cobbleTex(), r: .9 });
  const side = mat(0xffffff, { map: concreteTex(62, 28), r: .9 }); const curb = mat(0xB9A991, { r: .9 });
  b.add(BOX, cob, [0, -.05, 8], [0, 0, 0], [140, .1, 14], .35);
  b.add(BOX, side, [0, .07, .9], [0, 0, 0], [140, .14, 2.2], .4); b.add(BOX, curb, [0, .08, 2.0], [0, 0, 0], [140, .16, .16]);
  b.add(BOX, side, [0, .07, 15.6], [0, 0, 0], [140, .14, 2.4], .4); b.add(BOX, curb, [0, .08, 14.4], [0, 0, 0], [140, .16, .16]);
  // workshop facade (custom)
  const brick = m.brick[2], pl = m.plaster[4];
  const W = 11, H = 7.8;
  b.add(BOX, brick, [-W / 2 + .9, 2, -5], [0, 0, 0], [1.8, 4, 10], .5); b.add(BOX, brick, [W / 2 - 1.2, 2, -5], [0, 0, 0], [2.4, 4, 10], .5);
  b.add(BOX, pl, [0, 5.9, -5], [0, 0, 0], [W, 3.8, 10], .5);
  b.add(BOX, m.void, [-.45, 2, -1.2], [0, 0, 0], [6.8, 4, 2]);
  b.add(BOX, m.trim, [-.45, 4.08, .06], [0, 0, 0], [7.2, .25, .2]);
  b.add(CYL, m.iron, [-.45, 3.82, -.1], [0, 0, PI / 2], [.24, 6.8, .24]);
  for (const s of [-1, 1]) b.add(BOX, m.trim, [s * (W / 2 - .1), H / 2, .05], [0, 0, 0], [.25, H, .12]);
  b.add(BOX, m.trim, [0, H - .1, .14], [0, 0, 0], [W + .3, .22, .4]); b.add(BOX, pl, [0, H + .4, -.15], [0, 0, 0], [W, .8, .3], .5);
  b.add(BOX, m.trim, [0, H + .82, -.15], [0, 0, 0], [W + .1, .08, .42]);
  for (let i = 0; i < 3; i++) { const x = -3.4 + i * 3.4; b.add(BOX, m.void, [x, 6.1, 0], [0, 0, 0], [1.1, 1.5, .3]); b.add(BOX, m.stone, [x, 5.3, .14], [0, 0, 0], [1.5, .1, .3]); b.add(BOX, m.trim, [x, 6.95, .07], [0, 0, 0], [1.45, .18, .16]); for (let k = 0; k < 5; k++) b.add(BOX, m.iron, [x - .5 + k * .25, 6.1, .2], [0, 0, 0], [.025, 1.5, .025]); }
  // side window + small door right
  b.add(BOX, m.voidLit, [4.1, 1.9, 0], [0, 0, 0], [1.2, 1.6, .3]); b.add(BOX, m.stone, [4.1, 1.05, .14], [0, 0, 0], [1.5, .1, .3]);
  for (let k = 0; k < 5; k++) b.add(BOX, m.iron, [3.6 + k * .25, 1.9, .2], [0, 0, 0], [.025, 1.6, .025]);
  // interior glimpse
  b.add(new THREE.TorusGeometry(.33, .13, 10, 20), mat(0x151313, { r: .85 }), [-3.2, .13, -1.6], [PI / 2, 0, 0]); b.add(new THREE.TorusGeometry(.33, .13, 10, 20), mat(0x151313, { r: .85 }), [-3.2, .39, -1.6], [PI / 2, 0, 0]);
  b.add(CYL, mat(0xB5322A, { r: .5 }), [2.0, .45, -1.5], [0, 0, 0], [.3, .9, .3]);
  scene.add(b.build());
  // painted sign
  const sign = textTex([{ t: 'TALLER ALEJANDRO', font: '150px Anton', color: '#F7E9C9', stroke: '#7A1F1A', sw: 16, y: 105 }, { t: 'MECÁNICA EN GENERAL · FRENOS · AFINACIONES', font: '62px "Bebas Neue"', color: '#2E3F6B', y: 215 }], { bg: '#E2B13A', w: 1400, h: 270, border: '#7A1F1A', bw: 14 });
  mesh(BOX, mat(0xffffff, { map: sign, r: .85 }), scene, [-.45, 4.75, .1], [6.6, 6.6 * 270 / 1400, .04]);
  const inLight = new THREE.PointLight(0xffb070, 6, 6, 1.5); inLight.position.set(-.5, 3, -1.5); scene.add(inLight);
  // neighbours
  const b2 = new Batch();
  for (const [x, w] of [[-11.5, 11], [-22.5, 10], [-33, 10], [12, 12], [23.5, 10], [34, 11]]) { const g = building(w, R(7, 11), 8, {}); g.position.set(x, 0, 0); scene.add(g); }
  for (const [x, w] of [[-26, 10], [-15, 11], [-4, 10], [6.5, 10], [17, 11], [28, 10]]) { const g = building(w, R(6, 8), 8, {}); g.position.set(x, 0, 16.8); g.rotation.y = PI; scene.add(g); }
  for (let x = -30; x < 32; x += 9) papelLine(b2, [x, R(6.2, 7), .2], [x + R(-3, 3), R(6.2, 7), 16.6], R(1, 1.5));
  for (let x = -30; x < 30; x += 15) { b2.add(CYL8, m.pole, [x, 4, 14.8], [0, 0, 0], [.12, 8, .12]); }
  wire(b2, m.wire, [-30, 7.8, 14.8], [0, 7.8, 14.8], .9, .015, 20); wire(b2, m.wire, [0, 7.8, 14.8], [30, 7.8, 14.8], .9, .015, 20);
  wire(b2, m.wire, [-6, 7.8, 14.8], [-4.5, 6.4, .2], .6, .012, 12);
  streetLamp(b2, 6.5, 1.6, 5, -1);
  scene.add(b2.build());
  // repaired cars
  const c1 = classic(0xEFE3C8, 0xC9302C); c1.g.position.set(-6.8, 0, 4.4); c1.g.rotation.y = PI / 2; scene.add(c1.g);
  const c2 = classic(0x3D6FB6, 0xF2EEE2); c2.g.position.set(9.2, 0, 4.4); c2.g.rotation.y = -PI / 2; scene.add(c2.g);
  const sparkle = mesh(SPH, mat(0xffffff, { e: 0xfff4d0, ei: 4 }), scene, [-5.6, 1.06, 4.0], [.04, .04, .04], [0, 0, 0], false);
  // SUV
  const S = suv(0x0c0c0f, { glassOp: .6 }); S.g.rotation.y = -PI / 2; scene.add(S.g);
  const A = makeAlejandro(); scene.add(A.root);
  const boss = makeBoss(); scene.add(boss.root);
  const bru = makeBruiser(); scene.add(bru.root);
  // rag in Alejandro's hands
  const rag = mesh(BOX, mat(0xC9302C, { r: .9 }), A.J.handL, [0, -.08, .04], [.12, .14, .03]);

  const SH = [0, 2.9, 5.0, 8.2, 11.0];
  const stopX = 1.0, arrive = 2.6;
  const suvX = t => t < arrive ? stopX - 13 * Math.pow(1 - t / arrive, 2) * -1 * -1 + 0 : stopX;
  function update(t, cam, TLc) {
    // SUV comes from +x moving -x, decelerating to stop
    const u = clamp(t / arrive); const x = stopX + 16 * Math.pow(1 - u, 2);
    S.g.position.set(x, 0, 6.0); spinWheels(S, -(16 - 16 * Math.pow(1 - u, 2)) * -1);
    S.body.rotation.x = t > arrive - .3 && t < arrive + .5 ? -.025 * Math.sin((t - arrive + .3) / .8 * PI) : 0;
    S.doors.R.rotation.y = -keys(t, [[3.0, 0], [3.45, 1.05]], eo);
    S.doors.L.rotation.y = keys(t, [[3.1, 0], [3.5, 1.0]], eo);
    A.reset(); boss.reset(); bru.reset();
    // boss steps out (toward -z) then walks to Alejandro
    const bp0 = [0.95, 4.35], bp1 = [1.7, 2.35];
    boss.root.rotation.y = PI;
    if (t < 3.4) { boss.root.visible = false; }
    else {
      boss.root.visible = true;
      const ub = clamp((t - 3.4) / .7);
      if (t < 4.1) { boss.root.position.set(bp0[0], 0, bp0[1] + .6 * (1 - ub)); boss.J.hips.position.y = .62 + .33 * eo(ub); boss.J.hipL.rotation.x = -1.0 * (1 - ub); boss.J.kneeL.rotation.x = 1.1 * (1 - ub); boss.J.spine.rotation.x = .4 * (1 - ub); }
      else {
        const uw = clamp((t - 4.1) / 2.3); const px = lerp(bp0[0], bp1[0], eio(uw)), pz = lerp(bp0[1], bp1[1], eio(uw));
        boss.root.position.set(px, 0, pz); boss.root.rotation.y = PI - .25;
        if (uw < 1) walk(boss, (t - 4.1) * 7.5, 1);
      }
      boss.J.head.rotation.y = .1; face(boss, { angry: .25 });
    }
    // bruiser: from driver side (+z), around front (-x), to cream car
    const bpath = [[1.6, 7.6], [-1.9, 7.4], [-4.2, 6.2], [-4.6, 5.75]];
    if (t < 3.6) bru.root.visible = false;
    else {
      bru.root.visible = true; const ub = clamp((t - 3.6) / 3.3);
      // piecewise path
      const segs = []; let tot = 0; for (let i = 0; i < bpath.length - 1; i++) { const d = Math.hypot(bpath[i + 1][0] - bpath[i][0], bpath[i + 1][1] - bpath[i][1]); segs.push(d); tot += d; }
      let dd = eio(ub) * tot, i = 0; while (i < segs.length - 1 && dd > segs[i]) { dd -= segs[i]; i++; }
      const f = clamp(dd / segs[i]); const px = lerp(bpath[i][0], bpath[i + 1][0], f), pz = lerp(bpath[i][1], bpath[i + 1][1], f);
      bru.root.position.set(px, 0, pz); bru.root.rotation.y = Math.atan2(bpath[i + 1][0] - bpath[i][0], bpath[i + 1][1] - bpath[i][1]);
      if (ub < 1) walk(bru, (t - 3.6) * 7, 1);
      else { // inspect: bend + finger on the car
        bru.root.rotation.y = PI + .2; const ui = clamp((t - 6.9) / .6);
        bru.J.spine.rotation.x = .55 * eo(ui); bru.J.shR.rotation.x = -1.0 * eo(ui); bru.J.elR.rotation.x = -.3; bru.J.head.rotation.x = .2 * ui;
        bru.J.shR.rotation.y = .3 * Math.sin((t - 7.5) * 2) * ui;
        bru.J.hipL.rotation.x = bru.J.hipR.rotation.x = -.25 * ui;
      }
      face(bru, { angry: .4 });
    }
    sparkle.visible = t > 7.4 && t < 8.2; sparkle.scale.setScalar(.06 * Math.sin(clamp((t - 7.4) / .8) * PI));
    // Alejandro comes out of the door wiping hands
    const ua = clamp((t - 5.4) / 1.6);
    A.root.position.set(lerp(1.0, 2.1, eio(ua)), 0, lerp(-1.0, .55, eio(ua))); A.root.rotation.y = .1;
    if (ua > 0 && ua < 1) walk(A, (t - 5.4) * 7, .8);
    A.J.shL.rotation.x = -.7; A.J.elL.rotation.x = -1.0; A.J.shR.rotation.x = -.6 + .1 * Math.sin(t * 10); A.J.elR.rotation.x = -1.0;
    A.J.shL.rotation.z = -.2; A.J.shR.rotation.z = .25;
    face(A, { brow: .2, smile: .2 });
    let id;
    if (t < SH[1]) {
      id = 'a';
      setCam(cam, keys(t, [[0, [3.5, 1.2, 15.2]], [SH[1], [2.6, 1.0, 13.8]]], lin), keys(t, [[0, [3.5, 3.3, 3]], [SH[1], [0.5, 3.0, 2]]]), 50);
      L.key.follow([0, 0, 6], 22);
    } else if (t < SH[2]) {
      id = 'b'; const u = t - SH[1];
      // ground level at the passenger door, looking at the shoe stepping out
      setCam(cam, keys(u, [[0, [.2, .22, 2.4]], [2.1, [.35, .35, 2.6]]], lin), keys(u, [[0, [1.0, .45, 5.0]], [1.2, [1.0, .4, 4.4]], [2.1, [1.2, 1.2, 4.0]]]), 48, -.06);
      L.key.follow([1, 0, 4], 10);
    } else if (t < SH[3]) {
      id = 'c'; const u = t - SH[2];
      setCam(cam, keys(u, [[0, [.2, 1.6, 12.6]], [3.2, [-.4, 1.5, 11.8]]], lin), keys(u, [[0, [-1.6, 1.2, 7.2]], [1.9, [-4.4, 1.0, 5.6]], [3.2, [-.6, 1.4, 2.4]]]), 54);
      L.key.follow([-2, 0, 4], 12);
    } else if (t < SH[4]) {
      id = 'd'; const u = t - SH[3];
      boss.root.position.set(bp1[0], 0, bp1[1]); boss.root.rotation.y = PI - .25;
      const mo = mouthEnv(TLc.env.h3, TLc.h3 - 0, t);
      face(boss, { angry: .35, mouth: mo * .8 });
      boss.J.shL.rotation.x = -.6 - .2 * Math.sin(t * 3); boss.J.elL.rotation.x = -.9; boss.J.head.rotation.x = -.08;
      setCam(cam, keys(u, [[0, [1.9, .85, .75]], [2.8, [1.85, .95, 1.0]]], lin), [1.62, 1.78, 2.3], 40, .05);
      L.key.follow([1.7, 0, 2], 8);
    } else {
      id = 'e'; const u = t - SH[4];
      boss.root.position.set(bp1[0], 0, bp1[1]); boss.root.rotation.y = PI - .25;
      face(boss, { angry: .35, mouth: mouthEnv(TLc.env.h3, TLc.h3, t) * .8 });
      boss.J.shL.rotation.x = -1.0; boss.J.elL.rotation.x = -.7; boss.J.handL.rotation.z = .5; // gesture
      A.J.shL.rotation.x = -.2; A.J.shR.rotation.x = -.3;
      const pop = sm(.25, .6, u);
      face(A, { brow: .4 + pop * .6, eye: 1 + pop * .35, pupil: 1 - pop * .3, mouth: pop * .55 });
      A.J.head.rotation.x = -.05; A.J.spine.rotation.x = -.08 * pop;
      setCam(cam, keys(u, [[0, [.55, 1.9, 3.85]], [2.2, [.7, 1.85, 3.5]]], lin), [2.1, 1.65, .55], 36);
      L.key.follow([2, 0, 1], 8);
    }
    return id;
  }
  return { scene, update, sky, bru, boss, A };
}

// =====================================================================
// SET 5 — THE MAFIA GARAGE (scenes 5 & 6)
// =====================================================================
export function setGarage(env, TL) {
  const sunDir = [0.85, 0.32, 0.25];
  const { scene, sky, L } = baseScene(env, sunDir, { key: 5.0, hemi: .4, envI: .3 });
  const m = mats(); const b = new Batch();
  const floor = mat(0xffffff, { map: concreteTex(44, 26), r: .38, m: .05 });
  b.add(BOX, floor, [0, -.05, -17], [0, 0, 0], [30, .1, 54], .2);
  const brick = m.brick[0], brick2 = m.brick[2];
  const Wx = 14.2, H = 11;
  // back wall
  b.add(BOX, brick2, [0, H / 2, -42.2], [0, 0, 0], [28.8, H, .4], .5);
  // left wall
  b.add(BOX, brick, [-Wx, H / 2, -17], [0, 0, 0], [.4, H, 50.8], .5);
  // right wall with arched windows
  for (let z = -40; z <= 6; z += 6) {
    b.add(BOX, brick, [Wx, H / 2, z - 1.5], [0, 0, 0], [.4, H, 3.4], .5); // pier
    b.add(BOX, brick, [Wx, 2.6, z + 1.5], [0, 0, 0], [.4, 5.2, 2.7], .5); // below window
    b.add(BOX, brick, [Wx, 10.4, z + 1.5], [0, 0, 0], [.4, 1.2, 2.7], .5); // above
    b.add(BOX, m.stone, [Wx - .2, 5.15, z + 1.5], [0, 0, 0], [.5, .12, 2.8]);
    for (let k = 0; k < 3; k++) b.add(BOX, m.iron, [Wx, 7.5, z + .7 + k * .8], [0, 0, 0], [.05, 4.6, .05]);
    b.add(BOX, m.iron, [Wx, 7.5, z + 1.5], [0, 0, 0], [.05, .05, 2.7]);
  }
  // front wall with entrance x[-5,5] y[0,6.2]
  b.add(BOX, brick, [-9.6, H / 2, 8.2], [0, 0, 0], [9.2, H, .4], .5); b.add(BOX, brick, [9.6, H / 2, 8.2], [0, 0, 0], [9.2, H, .4], .5); b.add(BOX, brick, [0, 8.6, 8.2], [0, 0, 0], [10, 4.8, .4], .5);
  b.add(CYL, m.iron, [0, 6.0, 8.0], [0, 0, PI / 2], [.35, 10, .35]);
  // roof + trusses
  b.add(BOX, m.trimDark, [0, H + .1, -17], [0, 0, 0], [29, .2, 51]);
  for (let z = -40; z < 8; z += 4) { b.add(BOX, m.iron, [0, H - .4, z], [0, 0, 0], [28, .25, .2]); b.add(BOX, m.iron, [0, H - 1.6, z], [0, 0, 0], [28, .15, .15]); for (let x = -13; x <= 13; x += 2.6) b.add(BOX, m.iron, [x, H - 1, z], [0, 0, (x / 2.6 % 2 ? .6 : -.6)], [.08, 1.5, .08]); }
  // catwalk left wall
  b.add(BOX, m.iron, [-12.9, 4.8, -17], [0, 0, 0], [2.2, .1, 48]);
  for (let z = -40; z < 7; z += 1.2) b.add(BOX, m.iron, [-11.85, 5.3, z], [0, 0, 0], [.04, 1.0, .04]);
  b.add(BOX, m.iron, [-11.85, 5.8, -17], [0, 0, 0], [.05, .05, 48]); b.add(BOX, m.iron, [-11.85, 5.3, -17], [0, 0, 0], [.04, .04, 48]);
  for (let z = -38; z < 6; z += 8) b.add(BOX, m.iron, [-11.9, 2.4, z], [0, 0, 0], [.12, 4.8, .12]);
  for (let y = .3; y < 4.8; y += .35) b.add(BOX, m.iron, [-11.6, y, -6], [0, 0, 0], [.04, .04, .5]);
  for (const zz of [-6.25, -5.75]) b.add(BOX, m.iron, [-11.6, 2.4, zz], [0, 0, 0], [.05, 4.8, .05]);
  // floor markings + props
  const yellow = mat(0xE2B13A, { r: .6 });
  for (let z = -38; z < 6; z += 4.8) for (const s of [1, -1]) b.add(BOX, yellow, [s * 4.0, .005, z + 2.4], [0, s * .35, 0], [.12, .01, 4.4]);
  const red = mat(0xB5322A, { r: .5 });
  for (let i = 0; i < 5; i++) b.add(CYL, i % 2 ? red : m.shutter[2], [12.6 - (i % 3) * .7, .45, -30 + Math.floor(i / 2) * .7], [0, 0, 0], [.32, .9, .32]);
  for (let i = 0; i < 3; i++) { b.add(BOX, red, [-13.4, 1.0, -26 + i * 1.3], [0, 0, 0], [.7, 2.0, 1.2]); for (let k = 0; k < 5; k++) b.add(BOX, m.trimDark, [-13.04, .3 + k * .38, -26 + i * 1.3], [0, 0, 0], [.02, .04, 1.0]); }
  // mural
  b.add(BOX, mat(0xffffff, { map: textTex([{ t: 'TRANSPORTES DEL SOL', font: '150px Anton', color: '#F2B33D', stroke: '#2a1208', sw: 14 }], { w: 1400, h: 240 }), tr: true, r: .9 }), [0, 7.2, -41.95], [0, 0, 0], [12, 2.05, .02]);
  // outside ground
  b.add(BOX, mat(0xffffff, { map: cobbleTex(), r: .9 }), [0, -.06, 30], [0, 0, 0], [80, .1, 44], .3);
  scene.add(b.build());
  for (const [x, w] of [[-10, 10], [1, 11], [12, 10]]) { const g = building(w, R(7, 9), 6, {}); g.position.set(x, 0, 34); g.rotation.y = PI; scene.add(g); }
  // pendant lamps
  const glow = mat(0xfff0c0, { e: 0xffc070, ei: 3 });
  const lamps = [];
  for (let z = -36; z < 6; z += 8) {
    const lp = grp(scene, [0, H - .2, z]);
    mesh(CYL8, m.iron, lp, [0, -1.4, 0], [.012, 2.8, .012]);
    mesh(new THREE.ConeGeometry(.55, .4, 20, 1, true), mat(0x2E6B4F, { r: .4, m: .5, side: THREE.DoubleSide }), lp, [0, -2.9, 0]);
    mesh(SPH, glow, lp, [0, -3.05, 0], [.12, .1, .12], [0, 0, 0], false);
    const pl = new THREE.PointLight(0xffc27a, 22, 14, 1.4); pl.position.set(0, -3.3, 0); lp.add(pl); lamps.push({ lp, pl });
  }
  // light shafts
  const rayTex = (() => { const c = document.createElement('canvas'); c.width = 64; c.height = 256; const x = c.getContext('2d'); const g = x.createLinearGradient(0, 0, 0, 256); g.addColorStop(0, 'rgba(255,190,120,0.0)'); g.addColorStop(.2, 'rgba(255,190,120,0.55)'); g.addColorStop(1, 'rgba(255,190,120,0)'); x.fillStyle = g; x.fillRect(0, 0, 64, 256); return new THREE.CanvasTexture(c); })();
  const rayM = new THREE.MeshBasicMaterial({ map: rayTex, transparent: true, opacity: .16, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
  for (let z = -40; z <= 6; z += 6) for (let k = 0; k < 2; k++) mesh(new THREE.PlaneGeometry(2.4, 18), rayM, scene, [6, 4.5, z + 1.5 + k * .4], [1, 1, 1], [0, 0, -1.3 + k * .05], false);
  // parked SUVs
  const proto = flatten(suv(0x0c0c0f).g);
  const parked = [];
  for (let z = -38; z < 6; z += 4.8) for (const s of [1, -1]) {
    if (s < 0 && Math.abs(z - (-13.6)) < 1) continue; // hero slot
    const g = proto.clone(); g.position.set(s * 7.0, 0, z + 2.4); g.rotation.y = s > 0 ? -PI / 2 - .35 : PI / 2 + .35; scene.add(g); parked.push(g);
  }
  const hero = suv(0x0c0c0f); hero.g.position.set(-7.0, 0, -11.2); hero.g.rotation.y = PI / 2 + .35; scene.add(hero.g);
  // table with cash
  const table = grp(scene, [0, 0, -20]);
  mesh(BOX, m.wood, table, [0, .92, 0], [2.2, .08, 1.0]);
  for (const x of [-1, 1]) for (const z of [-.42, .42]) mesh(BOX, m.iron, table, [x, .45, z], [.06, .9, .06]);
  const stacks = []; const sg = grp(table, [0, .96, 0]);
  for (let i = 0; i < 18; i++) { const layer = Math.floor(i / 9); const k = i % 9; const p = [-.8 + (k % 5) * .33 + layer * .1, layer * .12, -.25 + Math.floor(k / 5) * .28 + layer * .05]; const c = cashStack(sg, p, [0, R(-.2, .2), 0]); c.userData.base = p; stacks.push(c); }
  const tower = []; for (let i = 0; i < 9; i++) { const c = cashStack(sg, [.6 + R(-.02, .02), .24 + i * .12, .1], [0, R(-.3, .3), 0]); tower.push(c); }
  const ph = phone(scene, [0, .96, -20]);
  const keysG = grp(scene, [0, 0, 0]); mesh(new THREE.TorusGeometry(.03, .006, 6, 14), mat(0xd9a74a, { r: .2, m: 1 }), keysG); mesh(BOX, mat(0x111111), keysG, [0, -.05, 0], [.03, .07, .015]); mesh(BOX, mat(0xcccccc, { r: .2, m: 1 }), keysG, [.03, -.04, 0], [.012, .06, .004]);
  const redSpot = new THREE.SpotLight(0xff2a14, 0, 14, .5, .6, 1.2); redSpot.position.set(1.5, 4.0, -26); redSpot.target.position.set(0, 1.5, -20.8); scene.add(redSpot); scene.add(redSpot.target);

  const A = makeAlejandro(); scene.add(A.root);
  const boss = makeBoss(); scene.add(boss.root);
  const bru = makeBruiser(); scene.add(bru.root);
  const S5 = [0, 1.9, 3.3];
  const S6 = [0, .9, 6.0];
  const phEar = phone(A.J.head, [-.135, .1, .02], [0, 0, PI / 2]); phEar.visible = false;
  function earPose() { A.J.shR.rotation.x = -.95; A.J.shR.rotation.z = -.35; A.J.elR.rotation.x = -2.2; A.J.head.rotation.z = -.08; ph.visible = false; phEar.visible = true; }
  function update(t, cam, mode, TLc) {
    phEar.visible = false;
    A.reset(); boss.reset(); bru.reset(); A.wrench.visible = false; keysG.visible = false; ph.visible = false; boss.root.visible = true; bru.root.visible = true;
    hero.hood.rotation.x = 0; redSpot.intensity = 0; for (const l of lamps) l.pl.intensity = 22;
    stacks.forEach(s => { s.visible = true; s.scale.setScalar(1); }); tower.forEach(s => s.visible = false);
    let id;
    if (mode === 's5') {
      if (t < S5[1]) {
        id = 'a'; boss.root.visible = false;
        const z = 3.2 - 1.45 * t; A.root.position.set(0, 0, z); A.root.rotation.y = PI; walk(A, t * 7.2, 1);
        face(A, { brow: .5, eye: 1.08, look: -.3, smile: .2 });
        A.J.head.rotation.y = .3 * Math.sin(t * 1.6);
        bru.root.position.set(1.7, 0, -1.6); bru.root.rotation.y = -PI / 2 - .6;
        // toss keys
        const tt = clamp((t - .7) / .75);
        bru.J.shR.rotation.x = keys(t, [[0, -.2], [.5, .4], [.75, -1.4], [1.1, -.8]]);
        face(bru, { angry: .3, smile: .2 });
        if (t > .7) {
          keysG.visible = true;
          if (tt < 1) { const p0 = new THREE.Vector3(1.4, 1.4, -1.5), p1 = new THREE.Vector3(.25, 1.0, z - .45); const p = p0.clone().lerp(p1, tt); p.y += Math.sin(tt * PI) * .8; keysG.position.copy(p); keysG.rotation.set(t * 14, t * 9, 0); }
          else { A.root.updateMatrixWorld(true); A.J.handR.getWorldPosition(keysG.position); keysG.rotation.set(0, 0, 0); }
        }
        if (t > 1.3) { A.J.shR.rotation.x = -1.2; A.J.elR.rotation.x = -.6; face(A, { brow: .8, smile: .7, eye: 1.1 }); }
        setCam(cam, [-.6, 1.0, z - 3.8], [0, 1.55, z + 1.5], 52, .03);
        L.key.follow([0, 0, -2], 20);
      } else if (t < S5[2]) {
        id = 'b'; const u = t - S5[1];
        A.root.position.set(0, 0, -20.85); A.root.rotation.y = 0;
        boss.root.position.set(1.35, 0, -19.6); boss.root.rotation.y = -PI / 2 - .4;
        boss.J.shR.rotation.x = -1.0 + .5 * Math.abs(Math.sin(u * 10)); boss.J.elR.rotation.x = -.5;
        face(boss, { smile: .5, angry: .1 });
        stacks.forEach((s, i) => { const td = i * .04; const k = clamp((u - td) / .12); s.visible = u > td; s.scale.setScalar(.3 + .7 * eo(k) + Math.sin(k * PI) * .2); });
        tower.forEach((s, i) => { const td = .55 + i * .06; s.visible = u > td; s.scale.setScalar(clamp((u - td) / .08)); });
        const pop = sm(.25, .45, u);
        face(A, { brow: .9 * pop, eye: 1 + .45 * pop, pupil: 1 - .2 * pop, mouth: .95 * pop });
        A.J.spine.rotation.x = -.12 * pop; A.J.shL.rotation.x = A.J.shR.rotation.x = -.5 * pop; A.J.elL.rotation.x = A.J.elR.rotation.x = -1.4 * pop; A.J.shL.rotation.z = -.5 * pop; A.J.shR.rotation.z = .5 * pop;
        setCam(cam, keys(u, [[0, [.15, 1.25, -17.9]], [1.4, [.1, 1.32, -18.4]]], lin), [0, 1.35, -20.6], 42, .02);
        L.key.follow([0, 0, -20], 10);
      } else {
        id = 'c'; const u = t - S5[2];
        // Alejandro at hero SUV front, closes the hood at 1.1
        const front = new THREE.Vector3(0, 0, 3.0).applyAxisAngle(new THREE.Vector3(0, 1, 0), hero.g.rotation.y).add(hero.g.position);
        A.root.position.copy(front); A.root.rotation.y = hero.g.rotation.y + PI;
        hero.hood.rotation.x = -keys(u, [[0, 1.05], [.8, 1.1], [1.1, 0]], x => x * x);
        A.J.shL.rotation.x = A.J.shR.rotation.x = keys(u, [[0, -2.5], [.8, -2.6], [1.1, -1.1], [1.6, -.2]]); A.J.elL.rotation.x = A.J.elR.rotation.x = keys(u, [[0, -.3], [1.1, -.1], [1.6, -.3]]);
        A.J.spine.rotation.x = keys(u, [[0, .1], [1.1, .35], [1.6, .05]]);
        // satisfied, then turns to boss
        A.J.head.rotation.y = keys(u, [[2.0, 0], [2.6, .9]]);
        face(A, { smile: keys(u, [[1.2, 0], [1.6, .9], [2.6, .9], [3.1, 0]]), brow: keys(u, [[1.6, .3], [2.7, .3], [3.1, -.1]]), worry: keys(u, [[2.7, 0], [3.2, .5]]) });
        hero.body.position.y = u > 1.1 && u < 1.35 ? -.03 * Math.sin((u - 1.1) / .25 * PI) : 0;
        // boss walks in
        const ub = clamp((u - 1.8) / 1.5);
        const bpos = new THREE.Vector3(lerp(.8, -2.9, eio(ub)), 0, lerp(-9.2, -11.6, eio(ub)));
        boss.root.position.copy(bpos); boss.root.rotation.y = Math.atan2(-3.7, -2.4);
        if (ub > 0 && ub < 1) walk(boss, u * 7, 1);
        if (ub >= 1) { boss.root.rotation.y = Math.atan2(front.x - bpos.x, front.z - bpos.z); boss.J.shR.rotation.x = -.8; boss.J.elR.rotation.x = -.6; }
        face(boss, { angry: .5 });
        const dim = sm(2.6, 3.4, u); for (const l of lamps) l.pl.intensity = 22 * (1 - .55 * dim) * (u > 3.0 && u < 3.15 ? .2 : 1);
        setCam(cam, keys(u, [[0, [-6.1, 1.55, -15.0]], [3.8, [-5.8, 1.6, -14.6]]], lin), keys(u, [[0, [-4.2, 1.3, -12.2]], [2.2, [-4.1, 1.35, -12.0]], [3.8, [-3.4, 1.5, -11.6]]]), 48, -.03);
        L.key.follow([-4, 0, -9], 12);
      }
    } else {
      // ---- scene 6: the phone call ----
      for (const l of lamps) l.pl.intensity = 10;
      stacks.forEach(s => { s.visible = true; s.scale.setScalar(1); });
      A.root.position.set(0, 0, -20.85); A.root.rotation.y = 0;
      boss.root.position.set(1.0, 0, -19.25); boss.root.rotation.y = -PI / 2 - .2;
      ph.visible = true;
      if (t < S6[1]) {
        id = 'a';
        const u = clamp(t / .55);
        ph.position.set(lerp(.75, .05, eo(u)), .965, lerp(-19.65, -19.95, eo(u)));
        ph.rotation.set(0, -.3 + .3 * eo(u) + (t > .5 ? Math.sin(t * 90) * .06 : 0), 0);
        ph.userData.screen.emissiveIntensity = t > .45 ? 1.3 + .4 * Math.sin(t * 20) : .2;
        boss.J.shR.rotation.x = -1.1 * (1 - u * .6); boss.J.elR.rotation.x = -.3;
        face(A, { lookY: -1, brow: .3 });
        setCam(cam, [.35, 1.55, -19.25], [.0, .96, -20.0], 40, -.05);
        L.key.follow([0, 0, -20], 8);
      } else if (t < S6[2]) {
        id = 'b'; const u = t - S6[1];
        // phone at his ear (right hand)
        earPose();
        const fear = sm(.6, 4.6, u);
        face(A, { brow: .2 + .9 * fear, worry: fear, eye: 1 + .4 * fear, pupil: 1 - .5 * fear, mouth: .1 + .35 * fear, sweat: sm(2.2, 4.0, u), t: u, look: Math.sin(u * 7) * .25 * fear });
        A.J.spine.rotation.x = -.05 * fear;
        redSpot.intensity = 40 * sm(2.0, 5.0, u);
        for (const l of lamps) l.pl.intensity = 10 * (1 - .4 * sm(2, 5, u));
        const shake = sm(3, 5, u) * .006;
        setCam(cam, keys(u, [[0, [.45, 1.62, -18.75]], [5.1, [.2, 1.7, -19.55]]], lin).map((v, i) => v + Math.sin(u * 31 + i) * shake), [0.05, 1.72, -20.8], 36, .03);
        L.key.follow([0, 0, -20], 8);
      } else {
        id = 'c'; const u = (t - S6[2]) / (TL.dur.s6 - S6[2]);
        earPose();
        face(A, { brow: 1.15, worry: 1, eye: 1.45, pupil: .45, mouth: .55 + .1 * Math.sin(t * 6), sweat: 1, t });
        redSpot.intensity = 55; for (const l of lamps) l.pl.intensity = 5 + (Math.sin(t * 37) > .7 ? 3 : 0);
        // dolly zoom on face
        const e = eio(clamp(u / .9));
        const d = lerp(3.0, .9, e); const fovFor = dd => 2 * Math.atan(.36 / dd) * 180 / PI;
        setCam(cam, [0.05, 1.73, -20.72 + d], [0.05, 1.73, -20.72], fovFor(d), 0);
        L.key.follow([0, 0, -20], 8);
      }
    }
    return id;
  }
  return { scene, update, sky };
}
