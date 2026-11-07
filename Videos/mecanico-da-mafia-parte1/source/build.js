import * as THREE from 'three';
import { Batch, BOX, CYL, CYL8, SPH, CONE, mat, mesh, grp, R, rnd, pick, brickTex, plasterTex, stripeTex, papelTex, textTex, tileTex, woodTex } from './lib.js';

// shared materials (lazy)
let M = null;
export function mats() {
  if (M) return M;
  const pl = (h, s, l) => { const t = plasterTex(h, s, l); return mat(0xffffff, { map: t.map, bump: t.bump, bs: 1.2, r: .92 }); };
  const br = (h, s, l) => { const t = brickTex(h, s, l); return mat(0xffffff, { map: t.map, bump: t.bump, bs: 2.5, r: .9 }); };
  M = {
    plaster: [pl(38, 62, 62), pl(14, 52, 50), pl(42, 48, 80), pl(176, 35, 40), pl(46, 70, 55), pl(8, 45, 62), pl(90, 22, 55), pl(30, 30, 72)],
    brick: [br(12, 55, 36), br(18, 50, 42), br(8, 45, 30)],
    void: mat(0x120c0a, { r: 1 }),
    voidLit: mat(0x3a2414, { e: 0xffa040, ei: .35, r: 1 }),
    trim: mat(0xEDE0C6, { r: .8 }), trimDark: mat(0x6b4a34, { r: .8 }),
    stone: mat(0xC9B79A, { r: .9 }),
    iron: mat(0x15110f, { r: .45, m: .6 }),
    wood: mat(0xffffff, { map: woodTex(24, 34), r: .8 }),
    shutter: [mat(0x2E7F6A, { r: .7 }), mat(0x7A3B2E, { r: .7 }), mat(0x3D5E8C, { r: .7 })],
    tinaco: mat(0x1c1c1c, { r: .6 }),
    green: mat(0x3d6a35, { r: .8 }), green2: mat(0x587d3a, { r: .8 }),
    pot: mat(0xA4502E, { r: .8 }),
    awn: [mat(0xffffff, { map: stripeTex('#C8352A', '#EFE6D2'), r: .9, side: THREE.DoubleSide }), mat(0xffffff, { map: stripeTex('#2E7F6A', '#EFE6D2'), r: .9, side: THREE.DoubleSide }), mat(0xffffff, { map: stripeTex('#E2B13A', '#3D5E8C'), r: .9, side: THREE.DoubleSide })],
    wire: mat(0x111111, { r: .6 }),
    pole: mat(0x6f665c, { r: .8 }),
    flags: ['#E8C547', '#3BA99C', '#F2EDE4', '#C9302C', '#5B9E4D', '#E0629A', '#3D6FB6'].map(c => mat(0xffffff, { map: papelTex(c), at: .5, side: THREE.DoubleSide, r: .9 })),
    tiles: mat(0xffffff, { map: tileTex(), r: .45, m: .1 }),
    lampGlow: mat(0xfff0c0, { e: 0xffc070, ei: 2 }),
  };
  return M;
}

const SIGNS = ['FARMACIA', 'TACOS EL GÜERO', 'ABARROTES', 'PELUQUERÍA', 'REFACCIONES', 'BIRRIA LUPITA', 'PAPELERÍA', 'TORTILLERÍA', 'NEVERÍA', 'ZAPATERÍA', 'MERCERÍA', 'CANTINA', 'LAVANDERÍA'];
const signCols = [['#1E3A5F', '#F4D35E'], ['#7A1F1A', '#F7E9C9'], ['#F2E6C9', '#B5322A'], ['#2E6B4F', '#F7F1DE'], ['#111', '#F2B33D']];

// building facade facing +z, centered x, from ground. returns group
export function building(w, h, d, o = {}) {
  const m = mats(); const b = new Batch(); const extra = new THREE.Group();
  const wall = o.wall ?? (rnd() < .35 ? pick(m.brick) : pick(m.plaster));
  const isBrick = m.brick.includes(wall);
  const trim = rnd() < .6 ? m.trim : m.trimDark;
  b.add(BOX, wall, [0, h / 2, -d / 2], [0, 0, 0], [w, h, d], .5);
  // base plinth
  b.add(BOX, m.stone, [0, .4, .05], [0, 0, 0], [w, .8, .12], .5);
  // cornice + parapet
  b.add(BOX, trim, [0, h - .1, .12], [0, 0, 0], [w + .3, .22, .35]);
  b.add(BOX, trim, [0, h - .45, .06], [0, 0, 0], [w + .1, .12, .2]);
  b.add(BOX, wall, [0, h + .45, -.15], [0, 0, 0], [w, .9, .3], .5);
  b.add(BOX, trim, [0, h + .92, -.15], [0, 0, 0], [w + .12, .08, .42]);
  // corner pilasters
  for (const s of [1, -1]) b.add(BOX, trim, [s * (w / 2 - .15), h / 2, .05], [0, 0, 0], [.3, h, .14]);
  const gH = 3.6, fh = 3.1; const nf = Math.max(1, Math.floor((h - gH) / fh));
  // ground floor
  const nOpen = Math.max(1, Math.floor(w / 3.4));
  const sp = w / nOpen;
  const awn = pick(m.awn);
  for (let i = 0; i < nOpen; i++) {
    const x = -w / 2 + sp * (i + .5);
    const ow = Math.min(2.4, sp - .7);
    const arch = rnd() < .45;
    b.add(BOX, rnd() < .4 ? m.voidLit : m.void, [x, 1.45, 0], [0, 0, 0], [ow, 2.6, .25]);
    b.add(BOX, trim, [x, 2.85, .08], [0, 0, 0], [ow + .3, .2, .16]);
    b.add(BOX, trim, [x - ow / 2 - .08, 1.45, .06], [0, 0, 0], [.14, 2.6, .12]);
    b.add(BOX, trim, [x + ow / 2 + .08, 1.45, .06], [0, 0, 0], [.14, 2.6, .12]);
    if (arch) b.add(CYL, trim, [x, 2.85, .06], [Math.PI / 2, 0, 0], [ow / 2 + .1, .12, .3]);
    // roll shutter half down
    if (rnd() < .35) b.add(BOX, m.iron, [x, 2.4, .1], [0, 0, 0], [ow, .7, .04]);
    if (i === 0 || rnd() < .5) {
      // awning
      b.add(BOX, awn, [x, 3.15, .55], [.45, 0, 0], [ow + .4, .04, 1.2]);
      b.add(BOX, awn, [x, 2.88, 1.08], [0, 0, 0], [ow + .4, .22, .03]);
    }
  }
  // shop sign (unique texture)
  if (o.sign !== false) {
    const [bg, fg] = pick(signCols); const txt = o.sign ?? pick(SIGNS);
    const t = textTex([{ t: txt, font: '150px "Bebas Neue"', color: fg }], { bg, w: 1024, h: 200, border: fg, bw: 10 });
    const sw = Math.min(w - .8, 5.5);
    mesh(BOX, mat(0xffffff, { map: t, r: .8 }), extra, [0, 3.55, .12], [sw, sw * 200 / 1024, .06]);
  }
  // upper floors
  const nw = Math.max(1, Math.floor(w / 2.5)); const ws = w / nw;
  const shutterM = pick(m.shutter);
  for (let f = 0; f < nf; f++) {
    const y0 = gH + f * fh + .5;
    b.add(BOX, trim, [0, y0 - .45, .06], [0, 0, 0], [w, .12, .14]); // belt course
    const balconyRow = rnd() < .5;
    for (let i = 0; i < nw; i++) {
      const x = -w / 2 + ws * (i + .5);
      const wh = 1.7, ww = Math.min(1.1, ws - .6);
      b.add(BOX, rnd() < .25 ? m.voidLit : m.void, [x, y0 + wh / 2, 0], [0, 0, 0], [ww, wh, .3]);
      // frame
      b.add(BOX, trim, [x, y0 + wh + .1, .07], [0, 0, 0], [ww + .35, .2, .16]);
      b.add(BOX, trim, [x - ww / 2 - .06, y0 + wh / 2, .05], [0, 0, 0], [.12, wh, .1]);
      b.add(BOX, trim, [x + ww / 2 + .06, y0 + wh / 2, .05], [0, 0, 0], [.12, wh, .1]);
      // window mullion
      b.add(BOX, m.trimDark, [x, y0 + wh / 2, -.05], [0, 0, 0], [.05, wh, .05]);
      b.add(BOX, m.trimDark, [x, y0 + wh * .62, -.05], [0, 0, 0], [ww, .05, .05]);
      if (balconyRow || rnd() < .3) {
        const bwid = ww + .7;
        b.add(BOX, m.stone, [x, y0 - .06, .38], [0, 0, 0], [bwid, .12, .75]);
        b.add(BOX, m.stone, [x, y0 - .18, .3], [0, 0, 0], [bwid - .2, .12, .5]);
        for (let k = 0; k <= 7; k++) b.add(BOX, m.iron, [x - bwid / 2 + .05 + k * (bwid - .1) / 7, y0 + .42, .72], [0, 0, 0], [.025, .85, .025]);
        for (const s of [1, -1]) for (let k = 0; k <= 2; k++) b.add(BOX, m.iron, [x + s * (bwid / 2 - .05), y0 + .42, .72 - k * .3], [0, 0, 0], [.025, .85, .025]);
        b.add(BOX, m.iron, [x, y0 + .86, .72], [0, 0, 0], [bwid, .04, .04]);
        b.add(BOX, m.iron, [x, y0 + .2, .72], [0, 0, 0], [bwid, .03, .03]);
        if (rnd() < .6) { b.add(BOX, m.pot, [x - bwid / 2 + .25, y0 + .15, .5], [0, 0, 0], [.22, .2, .22]); b.add(SPH, rnd() < .5 ? m.green : m.green2, [x - bwid / 2 + .25, y0 + .38, .5], [0, 0, 0], [.22, .2, .22]); }
      } else {
        b.add(BOX, m.stone, [x, y0 - .05, .14], [0, 0, 0], [ww + .4, .1, .3]); // sill
        if (rnd() < .5) { // shutters open
          b.add(BOX, shutterM, [x - ww / 2 - .3, y0 + wh / 2, .25], [0, .5, 0], [.5, wh, .04]);
          b.add(BOX, shutterM, [x + ww / 2 + .3, y0 + wh / 2, .25], [0, -.5, 0], [.5, wh, .04]);
        }
        if (rnd() < .3) b.add(BOX, m.trimDark, [x + ww / 2 - .1, y0 + .2, .35], [0, 0, 0], [.5, .35, .35]); // AC unit
      }
    }
  }
  // drain pipe
  b.add(CYL8, m.iron, [w / 2 - .45, h / 2, .18], [0, 0, 0], [.05, h, .05]);
  // fire escape ladder on brick buildings
  if (isBrick && rnd() < .7) {
    const lx = -w / 2 + .9;
    for (const s of [-.22, .22]) b.add(BOX, m.iron, [lx + s, (gH + h) / 2, .5], [0, 0, 0], [.04, h - gH, .04]);
    for (let y = gH; y < h; y += .3) b.add(BOX, m.iron, [lx, y, .5], [0, 0, 0], [.44, .025, .025]);
    for (let y = gH; y < h; y += .3) b.add(BOX, m.iron, [lx, y, .3], [0, 0, 0], [.03, .03, .4]);
  }
  // roof: tinacos, antenna, clothesline
  const nt = 1 + Math.floor(rnd() * 2);
  for (let i = 0; i < nt; i++) { const x = R(-w / 2 + 1, w / 2 - 1); b.add(CYL, m.tinaco, [x, h + 1.4, -1.5 - rnd() * 2], [0, 0, 0], [.55, 1.1, .55]); b.add(CYL, m.tinaco, [x, h + 2.0, -1.5], [0, 0, 0], [.3, .15, .3]); }
  if (rnd() < .6) { const x = R(-w / 2 + 1, w / 2 - 1); b.add(CYL8, m.iron, [x, h + 2, -1], [0, 0, 0], [.03, 2.2, .03]); b.add(BOX, m.iron, [x, h + 2.8, -1], [0, 0, 0], [.9, .03, .03]); b.add(BOX, m.iron, [x, h + 2.5, -1], [0, 0, 0], [.6, .03, .03]); }
  const g = b.build(); g.add(extra); return g;
}

// catenary helper: tube from a to b sagging
export function wire(b, m, a, c, sag = .8, r = .015, n = 16) {
  const pts = []; for (let i = 0; i <= n; i++) { const t = i / n; pts.push(new THREE.Vector3(a[0] + (c[0] - a[0]) * t, a[1] + (c[1] - a[1]) * t - Math.sin(Math.PI * t) * sag, a[2] + (c[2] - a[2]) * t)); }
  const geo = new THREE.TubeGeometry(new THREE.CatmullRomCurve3(pts), n * 2, r, 5, false); b.add(geo, m); return pts;
}
export function papelLine(b, a, c, sag = 1.0, spacing = .55) {
  const m = mats();
  const pts = wire(b, m.wire, a, c, sag, .012, 24);
  const curve = new THREE.CatmullRomCurve3(pts); const L = curve.getLength(); const n = Math.floor(L / spacing);
  const plane = new THREE.PlaneGeometry(.42, .52);
  const dir = new THREE.Vector3(c[0] - a[0], 0, c[2] - a[2]).normalize(); const ry = Math.atan2(dir.x, dir.z) - Math.PI / 2;
  for (let i = 1; i < n; i++) { const p = curve.getPointAt(i / n); b.add(plane, m.flags[i % m.flags.length], [p.x, p.y - .27, p.z], [R(-.15, .15), ry + R(-.1, .1), 0]); }
}
export function palm(b, x, z, h = 7) {
  const m = mats(); const trunk = mat(0x7a5a40, { r: .9 });
  let px = x, pz = z; const lean = R(-.15, .15);
  for (let i = 0; i < 10; i++) { b.add(CYL8, trunk, [px + lean * i * .3, i * h / 10 + h / 20, pz], [0, 0, lean * .3], [.22 - i * .008, h / 10 + .02, .22 - i * .008]); b.add(CYL8, m.trimDark, [px + lean * i * .3, i * h / 10, pz], [0, 0, 0], [.25 - i * .008, .05, .25 - i * .008]); }
  const tx = px + lean * 3, ty = h;
  for (let i = 0; i < 9; i++) { const a = i / 9 * Math.PI * 2; b.add(CONE, i % 2 ? m.green : m.green2, [tx + Math.cos(a) * 1.15, ty - .45, pz + Math.sin(a) * 1.15], [0, Math.PI - a, Math.PI / 2 + .45], [.3, 2.6, .06]); }
  b.add(SPH, m.trimDark, [tx, ty, pz], [0, 0, 0], [.35, .35, .35]);
}
export function streetLamp(b, x, z, h = 5.5, side = 1) {
  const m = mats();
  b.add(CYL8, m.iron, [x, h / 2, z], [0, 0, 0], [.07, h, .07]);
  b.add(CYL8, m.iron, [x, .3, z], [0, 0, 0], [.16, .6, .16]);
  b.add(BOX, m.iron, [x + side * .5, h, z], [0, 0, 0], [1, .05, .05]);
  b.add(CYL8, m.iron, [x + side * .95, h - .2, z], [0, 0, 0], [.2, .35, .2]);
  b.add(SPH, m.lampGlow, [x + side * .95, h - .4, z], [0, 0, 0], [.13, .1, .13]);
}

export function cathedral() {
  const m = mats(); const b = new Batch();
  const cw = mats().plaster[2];
  // main nave
  b.add(BOX, cw, [0, 9, -12], [0, 0, 0], [16, 18, 24], .3);
  b.add(BOX, m.trim, [0, 18.4, 0], [0, 0, 0], [16.6, .8, 1]);
  // pediment
  b.add(CYL, cw, [0, 18.8, -.2], [Math.PI / 2, 0, Math.PI / 2], [4, 16, 4]);
  b.add(BOX, m.void, [0, 4.5, .2], [0, 0, 0], [3.4, 7, .6]);
  b.add(CYL, m.void, [0, 8, .2], [Math.PI / 2, 0, 0], [1.7, .6, 1.7]);
  b.add(CYL, m.trim, [0, 12.5, .25], [Math.PI / 2, 0, 0], [1.6, .3, 1.6]);
  b.add(CYL, m.void, [0, 12.5, .4], [Math.PI / 2, 0, 0], [1.1, .2, 1.1]);
  for (const s of [-1, 1]) for (let k = 0; k < 3; k++) b.add(CYL8, m.trim, [s * (2.5 + k * 1.3), 9, .6], [0, 0, 0], [.3, 18, .3]);
  // dome
  b.add(CYL, cw, [0, 21, -14], [0, 0, 0], [4, 4, 4]);
  b.add(SPH, m.tiles, [0, 23, -14], [0, 0, 0], [4, 4.5, 4], .3);
  b.add(CYL8, m.trim, [0, 28, -14], [0, 0, 0], [.6, 2, .6]);
  // towers
  for (const s of [-1, 1]) {
    const x = s * 10;
    b.add(BOX, cw, [x, 12, -1], [0, 0, 0], [5, 24, 5], .3);
    for (let lv = 0; lv < 3; lv++) {
      const y = 24 + lv * 4.2, sz = 4.4 - lv * .6;
      b.add(BOX, cw, [x, y + 2, -1], [0, 0, 0], [sz, 4, sz], .3);
      b.add(BOX, m.trim, [x, y + .1, -1], [0, 0, 0], [sz + .5, .3, sz + .5]);
      for (const r of [0, Math.PI / 2, Math.PI, -Math.PI / 2]) b.add(BOX, m.void, [x + Math.sin(r) * sz / 2, y + 2, -1 + Math.cos(r) * sz / 2], [0, r, 0], [sz * .4, 2.6, .2]);
    }
    for (let k = 0; k < 4; k++) b.add(BOX, m.void, [x, 6 + k * 4.5, 1.55], [0, 0, 0], [1, 2, .2]);
    b.add(CONE, m.tiles, [x, 44, -1], [0, Math.PI / 4, 0], [2.4, 12, 2.4], .25);
    b.add(SPH, m.trim, [x, 50.3, -1], [0, 0, 0], [.35, .35, .35]);
    b.add(BOX, m.iron, [x, 51.4, -1], [0, 0, 0], [.08, 1.8, .08]); b.add(BOX, m.iron, [x, 51.7, -1], [0, 0, 0], [.8, .08, .08]);
  }
  return b.build();
}

// merge a built group (meshes w/ single materials) into batched meshes
export function flatten(group) {
  group.updateMatrixWorld(true); const inv = group.matrixWorld.clone().invert(); const b = new Batch();
  group.traverse(o => { if (o.isMesh && !Array.isArray(o.material)) {
    const mm = o.matrixWorld.clone().premultiply(inv);
    let g = o.geometry.index ? o.geometry.toNonIndexed() : o.geometry.clone();
    for (const k of Object.keys(g.attributes)) if (!['position', 'normal', 'uv'].includes(k)) g.deleteAttribute(k);
    g.clearGroups(); g.applyMatrix4(mm);
    if (!b.m.has(o.material)) b.m.set(o.material, []); b.m.get(o.material).push(g);
  } });
  return b.build();
}
