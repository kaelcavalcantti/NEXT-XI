import * as THREE from 'three';
import { BOX, CYL, CYL8, SPH, CONE, rbox, mat, mesh, grp, addOutline, R, rnd, pick, moneyTex, edgeTex, textTex, portraitTex } from './lib.js';

const CAP_T = new THREE.CapsuleGeometry(0.17, 0.26, 6, 16);
const CAP_A = new THREE.CapsuleGeometry(0.058, 0.19, 5, 12);
const CAP_F = new THREE.CapsuleGeometry(0.05, 0.18, 5, 12);
const CAP_L = new THREE.CapsuleGeometry(0.082, 0.27, 5, 12);
const CAP_S = new THREE.CapsuleGeometry(0.066, 0.28, 5, 12);

// ---------------- puppet figure ----------------
export function figure(o) {
  const root = new THREE.Group(); const J = {};
  const m = {
    top: mat(o.top, { r: .75 }), legs: mat(o.legs ?? o.top, { r: .75 }), sleeve: mat(o.sleeve ?? o.top, { r: .75 }),
    fore: mat(o.fore ?? o.sleeve ?? o.top, { r: .7 }),
    skin: mat(o.skin ?? 0xb97c55, { r: .55 }), shoe: mat(o.shoe ?? 0x16110e, { r: .4 }), joint: mat(o.joint ?? 0x2a211c, { r: .45 }),
    hair: mat(o.hair ?? 0x1d140f, { r: .7 }), white: mat(0xf6f0e6, { r: .3 }), black: mat(0x0b0909, { r: .3 }),
    lens: mat(0x050505, { r: .05, m: .6 }), mouth: mat(0x3a1412, { r: .6 }),
  };
  const bw = o.bulk ?? 1;
  const G = (parent, p, name) => { const g = grp(parent, p); if (name) J[name] = g; return g; };
  const hips = G(root, [0, 0.95, 0], 'hips');
  mesh(SPH, m.legs, hips, [0, 0, 0], [.18 * bw, .13, .13]);
  const spine = G(hips, [0, 0.05, 0], 'spine');
  mesh(CAP_T, m.top, spine, [0, .25, 0], [bw, 1, .78]);
  if (o.collar) mesh(CYL, mat(o.collar, { r: .6 }), spine, [0, .5, .0], [.09, .05, .08]);
  if (o.straps) { // overall straps + bib pocket
    const sm = mat(o.straps, { r: .7 });
    mesh(BOX, sm, spine, [.09, .33, .12], [.04, .32, .02], [-.12, 0, 0]);
    mesh(BOX, sm, spine, [-.09, .33, .12], [.04, .32, .02], [-.12, 0, 0]);
    mesh(BOX, sm, spine, [0, .2, .135], [.15, .1, .02]);
  }
  if (o.tie) { mesh(BOX, mat(o.tie, { r: .5 }), spine, [0, .33, .135], [.045, .26, .02], [-.08, 0, 0]); mesh(BOX, mat(o.shirt ?? 0x111111, { r: .6 }), spine, [0, .4, .118], [.1, .14, .02], [-.1, 0, 0]); }
  if (o.chain) mesh(new THREE.TorusGeometry(.08, .008, 6, 20), mat(0xE8B931, { r: .2, m: 1 }), spine, [0, .44, .1], [1, 1.2, 1], [1.2, 0, 0]);
  const neck = G(spine, [0, .53, 0], 'neck'); mesh(CYL, m.skin, neck, [0, .04, 0], [.052, .09, .052]);
  const head = G(neck, [0, .09, 0], 'head');
  mesh(SPH, m.skin, head, [0, .11, 0], [.118, .138, .122]);
  // ears
  mesh(SPH, m.skin, head, [.118, .1, 0], [.02, .035, .025]); mesh(SPH, m.skin, head, [-.118, .1, 0], [.02, .035, .025]);
  // face
  const eyeL = G(head, [.043, .125, .1], 'eyeL'), eyeR = G(head, [-.043, .125, .1], 'eyeR');
  for (const e of [eyeL, eyeR]) { mesh(SPH, m.white, e, [0, 0, 0], [.024, .026, .02]).userData.noOutline = true; }
  J.pupL = mesh(SPH, m.black, eyeL, [0, 0, .017], [.012, .013, .008]); J.pupR = mesh(SPH, m.black, eyeR, [0, 0, .017], [.012, .013, .008]);
  J.pupL.userData.noOutline = J.pupR.userData.noOutline = true;
  J.browL = mesh(BOX, m.hair, head, [.045, .165, .108], [.05, .013, .014]); J.browR = mesh(BOX, m.hair, head, [-.045, .165, .108], [.05, .013, .014]);
  J.browL.userData.noOutline = J.browR.userData.noOutline = true;
  mesh(SPH, m.skin, head, [0, .095, .122], [.02, .03, .022]);
  J.mouth = mesh(SPH, m.mouth, head, [0, .045, .11], [.032, .008, .012]); J.mouth.userData.noOutline = true;
  if (o.mustache) { const mm = mesh(BOX, m.hair, head, [0, .066, .118], [.06, .012, .012]); mm.userData.noOutline = true; }
  if (o.hairStyle === 'quiff') {
    mesh(SPH, m.hair, head, [0, .16, -.01], [.124, .1, .128]);
    mesh(SPH, m.hair, head, [0, .23, .05], [.09, .045, .07], [-.3, 0, 0]);
    mesh(BOX, m.hair, head, [.112, .12, -.01], [.02, .07, .07]); mesh(BOX, m.hair, head, [-.112, .12, -.01], [.02, .07, .07]);
  } else if (o.hairStyle === 'bun') {
    mesh(SPH, m.hair, head, [0, .16, -.01], [.125, .1, .128]); mesh(SPH, m.hair, head, [0, .2, -.11], [.06, .06, .06]);
  }
  if (o.hat) {
    const hm = mat(o.hat, { r: .6 });
    mesh(CYL, hm, head, [0, .225, 0], [.2, .014, .2]);
    mesh(CYL, hm, head, [0, .29, 0], [.125, .12, .125]);
    mesh(CYL, mat(0x7a1c14, { r: .6 }), head, [0, .245, 0], [.128, .025, .128]);
  }
  if (o.glasses) {
    J.glasses = grp(head, [0, .125, .118]);
    mesh(BOX, m.lens, J.glasses, [.045, 0, 0], [.062, .036, .012]); mesh(BOX, m.lens, J.glasses, [-.045, 0, 0], [.062, .036, .012]);
    mesh(BOX, m.lens, J.glasses, [0, .01, 0], [.03, .008, .01]);
  }
  if (o.cap) { const cm = mat(o.cap, { r: .7 }); mesh(SPH, cm, head, [0, .19, 0], [.125, .07, .13]); mesh(BOX, cm, head, [0, .19, .13], [.15, .015, .1]); }
  // sweat drops
  J.sweat = [];
  for (let i = 0; i < 3; i++) { const d = mesh(SPH, mat(0xbfe6ff, { r: .05, tr: true, op: .85 }), head, [.11 - i * .02 * 0, .2 - i * .03, .07 + i * .01], [.014, .022, .014], [0, 0, 0], false); d.visible = false; d.userData.noOutline = true; d.userData.base = d.position.clone(); J.sweat.push(d); }
  // arms
  for (const s of [1, -1]) {
    const k = s > 0 ? 'L' : 'R';
    const sh = G(spine, [s * .225 * bw, .44, 0], 'sh' + k);
    mesh(SPH, m.sleeve, sh, [0, 0, 0], [.075, .075, .075]);
    mesh(CAP_A, m.sleeve, sh, [0, -.15, 0], [bw, 1, bw]);
    const el = G(sh, [0, -.3, 0], 'el' + k);
    mesh(SPH, m.joint, el, [0, 0, 0], [.055, .055, .055]);
    mesh(CAP_F, m.fore, el, [0, -.13, 0], [bw, 1, bw]);
    const hd = G(el, [0, -.27, 0], 'hand' + k);
    mesh(SPH, m.skin, hd, [0, -.035, .01], [.046, .062, .032]);
    mesh(SPH, m.skin, hd, [s * -.03, -.02, .035], [.016, .03, .016]);
    if (o.watch && s < 0) mesh(CYL, mat(0xE8B931, { r: .2, m: 1 }), el, [0, -.24, 0], [.055, .025, .055]);
    if (o.cuffs) mesh(CYL, mat(o.cuffs, { r: .5 }), el, [0, -.23, 0], [.052, .03, .052]);
    // legs
    const hp = G(hips, [s * .1, -.03, 0], 'hip' + k);
    mesh(CAP_L, m.legs, hp, [0, -.21, 0], [bw, 1, bw]);
    const kn = G(hp, [0, -.43, 0], 'knee' + k);
    mesh(SPH, m.joint, kn, [0, 0, 0], [.07, .07, .07]);
    mesh(CAP_S, m.legs, kn, [0, -.22, 0]);
    const ft = G(kn, [0, -.44, 0], 'foot' + k);
    mesh(BOX, m.shoe, ft, [0, -.035, .05], [.12, .075, .27]);
  }
  const fig = { root, J, mats: m };
  fig.reset = () => {
    for (const k in J) { const j = J[k]; if (j.isGroup && !['eyeL', 'eyeR', 'glasses'].includes(k)) j.rotation.set(0, 0, 0); }
    J.hips.position.set(0, .95, 0);
    J.shL.rotation.z = .08; J.shR.rotation.z = -.08; J.elL.rotation.x = -.1; J.elR.rotation.x = -.1;
    face(fig, {});
  };
  if (o.outline) addOutline(root);
  fig.reset();
  return fig;
}

// expression: {brow: -1 angry .. 1 raised, eye: scale, pupil: scale, mouth: open 0..1, smile, sweat:0..1, t}
export function face(f, e) {
  const J = f.J; const b = e.brow ?? 0; const eye = e.eye ?? 1; const pu = e.pupil ?? 1;
  J.browL.position.y = .165 + b * .022; J.browR.position.y = .165 + b * .022;
  const ang = (e.angry ?? 0) * .45 - (e.worry ?? 0) * .35;
  J.browL.rotation.z = -ang; J.browR.rotation.z = ang;
  J.eyeL.scale.set(eye, eye * (e.squint ? 1 - e.squint * .55 : 1), eye); J.eyeR.scale.copy(J.eyeL.scale);
  J.pupL.scale.set(.012 * pu, .013 * pu, .008); J.pupR.scale.copy(J.pupL.scale);
  J.pupL.position.x = J.pupR.position.x = (e.look ?? 0) * .008;
  J.pupL.position.y = J.pupR.position.y = (e.lookY ?? 0) * .008;
  const mo = e.mouth ?? 0;
  J.mouth.scale.set(.032 * (1 + (e.smile ?? 0) * .4 - mo * .2), .008 + mo * .03, .012);
  J.mouth.position.y = .045 - mo * .006 + (e.smile ?? 0) * .002;
  const sw = e.sweat ?? 0; const t = e.t ?? 0;
  J.sweat.forEach((d, i) => { d.visible = sw > i * .33; const ph = (t * .7 + i * .37) % 1; d.position.copy(d.userData.base); d.position.y -= ph * .08; d.scale.setScalar(1); d.scale.set(.014, .022, .014); });
}

export function walk(f, ph, amt = 1) {
  const J = f.J; const s = Math.sin(ph), c = Math.cos(ph);
  J.hipL.rotation.x = -s * .45 * amt; J.hipR.rotation.x = s * .45 * amt;
  J.kneeL.rotation.x = Math.max(0, Math.sin(ph + 1.3)) * .7 * amt + .05;
  J.kneeR.rotation.x = Math.max(0, Math.sin(ph + 1.3 + Math.PI)) * .7 * amt + .05;
  J.footL.rotation.x = -.1 * s * amt; J.footR.rotation.x = .1 * s * amt;
  J.shL.rotation.x = s * .35 * amt; J.shR.rotation.x = -s * .35 * amt;
  J.elL.rotation.x = -.25 - Math.max(0, s) * .3 * amt; J.elR.rotation.x = -.25 - Math.max(0, -s) * .3 * amt;
  J.hips.position.y = .95 - .035 * amt + Math.abs(c) * .035 * amt;
  J.spine.rotation.y = s * .08 * amt; J.hips.rotation.y = -s * .06 * amt;
}
export function sit(f) {
  const J = f.J; J.hipL.rotation.x = J.hipR.rotation.x = -1.5; J.kneeL.rotation.x = J.kneeR.rotation.x = 1.45; J.hips.position.y = .5;
}

// ---------------- vehicles ----------------
const darkM = mat(0x0a0909, { r: .6 });
const rubber = mat(0x141212, { r: .85 });
const chrome = mat(0xe8e2da, { r: .12, m: 1, env: 1.4 });
function wheel(parent, p, r = .42, wcol = 0x777777, whitewall = false) {
  const w = grp(parent, p);
  const spin = grp(w); w.userData.spin = spin;
  mesh(CYL, rubber, spin, [0, 0, 0], [r, .3, r], [0, 0, Math.PI / 2]);
  mesh(CYL8, mat(wcol, { r: .25, m: .9 }), spin, [0, 0, 0], [r * .6, .32, r * .6], [0, 0, Math.PI / 2]);
  mesh(BOX, darkM, spin, [0, 0, 0], [.34, r * 1.0, .06]);
  if (whitewall) mesh(new THREE.TorusGeometry(r * .78, r * .1, 8, 24), mat(0xf2ede2, { r: .5 }), spin, [Math.sign(p[0]) * .16, 0, 0], [1, 1, 1], [0, Math.PI / 2, 0]);
  return w;
}
export function suv(color = 0x0d0d10, o = {}) {
  const g = new THREE.Group(); const paint = mat(color, { r: .22, m: .55, env: 1.3 });
  const glass = mat(0x10141c, { r: .05, m: .3, tr: true, op: o.glassOp ?? .55, env: 1.6, dw: false });
  const body = grp(g);
  mesh(rbox(2.0, .92, 4.9, .16), paint, body, [0, .96, 0]);
  // fenders
  for (const zz of [1.55, -1.55]) for (const s of [1, -1]) mesh(rbox(.16, .26, 1.15, .06), darkM, body, [s * 1.0, .82, zz]);
  // roof & pillars
  mesh(rbox(1.84, .1, 3.0, .05), paint, body, [0, 2.15, -.35]);
  for (const s of [1, -1]) {
    for (const [zz, w] of [[1.1, .14], [.0, .12], [-1.0, .12], [-1.8, .16]]) mesh(BOX, paint, body, [s * .9, 1.78, zz], [.06, .72, w]);
    mesh(BOX, glass, body, [s * .92, 1.78, -.35], [.02, .62, 2.9], [0, 0, 0], false);
    mesh(BOX, chrome, body, [s * .95, 1.44, -.35], [.02, .04, 2.95]);
    mesh(BOX, darkM, body, [s * .98, 1.3, 1.0], [.12, .1, .2]); // mirror
    mesh(BOX, chrome, body, [s * .7, 2.24, -.35], [.05, .05, 2.7]); // roof rail
    mesh(BOX, darkM, body, [s * 1.01, .98, -.05], [.01, .62, .015]); // door seams
    mesh(BOX, darkM, body, [s * 1.01, .98, -1.05], [.01, .62, .015]);
    mesh(BOX, chrome, body, [s * 1.015, 1.12, .35], [.02, .03, .18]); // handles
    mesh(BOX, chrome, body, [s * 1.015, 1.12, -.65], [.02, .03, .18]);
  }
  mesh(BOX, glass, body, [0, 1.8, 1.17], [1.76, .66, .02], [-.35, 0, 0], false);
  mesh(BOX, glass, body, [0, 1.8, -1.86], [1.76, .6, .02], [.2, 0, 0], false);
  // interior
  const seatM = mat(0x2a1a14, { r: .7 });
  for (const zz of [.25, -.85]) { mesh(BOX, seatM, body, [0, 1.55, zz - .3], [1.6, .6, .15]); }
  mesh(BOX, darkM, body, [0, 1.48, .95], [1.7, .12, .4]); // dash
  // front
  mesh(BOX, mat(0x111111, { r: .3, m: .8 }), body, [0, 1.0, 2.46], [1.3, .42, .04]);
  for (let i = 0; i < 6; i++) mesh(BOX, chrome, body, [0, .84 + i * .065, 2.48], [1.28, .018, .02]);
  const hl = mat(0xfff1c8, { e: 0xffe3a0, ei: 2.2 });
  for (const s of [1, -1]) { mesh(BOX, hl, body, [s * .74, 1.18, 2.45], [.36, .12, .04]); mesh(BOX, mat(0xff2a10, { e: 0xff2010, ei: 1.5 }), body, [s * .82, 1.15, -2.45], [.22, .28, .04]); }
  mesh(BOX, darkM, body, [0, .6, 2.47], [2.0, .22, .12]); mesh(BOX, darkM, body, [0, .6, -2.47], [2.0, .22, .12]);
  mesh(BOX, mat(0xe6dfcf, { r: .5 }), body, [0, .62, -2.54], [.5, .14, .01]); // plate
  // hood (pivot at back edge)
  const hoodP = grp(body, [0, 1.43, .55]);
  mesh(rbox(1.9, .06, 1.9, .03), paint, hoodP, [0, 0, .95]);
  mesh(BOX, mat(0x2a2a2a, { r: .5, m: .6 }), body, [0, 1.41, 1.5], [1.4, .02, 1.5]);
  // doors (driver side +x, passenger -x) pivot at front edge
  const doors = {};
  for (const s of [1, -1]) {
    const d = grp(body, [s * 1.0, 0, .48]);
    mesh(BOX, paint, d, [s * .02, .98, -.52], [.05, .86, 1.04]);
    mesh(BOX, glass, d, [s * .03, 1.75, -.52], [.02, .58, .96], [0, 0, 0], false);
    mesh(BOX, paint, d, [s * .02, 1.72, -.03], [.05, .64, .08]);
    mesh(BOX, chrome, d, [s * .05, 1.12, -.2], [.02, .03, .18]);
    mesh(BOX, mat(0x1c1210, { r: .8 }), body, [s * .99, 1.0, 0], [.02, .8, .98], [0, 0, 0], false); // opening
    doors[s > 0 ? 'L' : 'R'] = d;
  }
  const wheels = [];
  for (const zz of [1.55, -1.55]) for (const s of [1, -1]) wheels.push(wheel(g, [s * .95, .42, zz], .42, 0x2a2a2a));
  return { g, body, hood: hoodP, doors, wheels, paint };
}

export function classic(c1, c2, o = {}) {
  const g = new THREE.Group(); const p1 = mat(c1, { r: .25, m: .35, env: 1.2 }); const p2 = mat(c2, { r: .25, m: .35, env: 1.2 });
  const glass = mat(0x1a222c, { r: .05, m: .3, tr: true, op: .5, env: 1.5, dw: false });
  const body = grp(g);
  mesh(rbox(1.86, .62, 5.0, .24), p1, body, [0, .72, 0]);
  mesh(rbox(1.9, .14, 5.04, .07), p2, body, [0, .82, 0]); // side stripe
  for (const s of [1, -1]) { mesh(rbox(.3, .2, 1.7, .08), p1, body, [s * .78, 1.05, 1.55]); mesh(rbox(.3, .14, 1.4, .07), p1, body, [s * .78, 1.04, -1.75]); }
  // cabin
  mesh(rbox(1.62, .1, 2.2, .05), p2, body, [0, 1.6, -.4]);
  for (const s of [1, -1]) {
    for (const zz of [.62, -.4, -1.42]) mesh(BOX, p2, body, [s * .78, 1.32, zz], [.06, .56, .1]);
    mesh(BOX, glass, body, [s * .8, 1.32, -.4], [.02, .5, 2.1], [0, 0, 0], false);
  }
  mesh(BOX, glass, body, [0, 1.32, .72], [1.5, .55, .02], [-.4, 0, 0], false);
  mesh(BOX, glass, body, [0, 1.32, -1.52], [1.5, .55, .02], [.4, 0, 0], false);
  mesh(BOX, mat(0x3b2a20, { r: .7 }), body, [0, 1.15, -.3], [1.5, .3, 1.8]);
  // bumpers & lights
  for (const zz of [2.53, -2.53]) mesh(CYL, chrome, body, [0, .5, zz], [.075, 1.96, .075], [0, 0, Math.PI / 2]);
  mesh(BOX, chrome, body, [0, .76, 2.5], [1.0, .26, .04]);
  for (let i = 0; i < 4; i++) mesh(BOX, darkM, body, [0, .68 + i * .055, 2.52], [.95, .02, .02]);
  const hl = mat(0xfff3cf, { e: 0xffe3a0, ei: 1.6, r: .1 });
  for (const s of [1, -1]) { mesh(CYL, hl, body, [s * .66, .86, 2.48], [.12, .06, .12], [Math.PI / 2, 0, 0]); mesh(CYL, chrome, body, [s * .66, .86, 2.46], [.14, .05, .14], [Math.PI / 2, 0, 0]); mesh(BOX, mat(0xd8261a, { e: 0xaa1a10, ei: 1 }), body, [s * .7, .85, -2.5], [.25, .12, .04]); }
  // engine (under hood)
  const eng = grp(body, [0, 1.03, 1.5]);
  mesh(BOX, mat(0x3a3f45, { r: .5, m: .7 }), eng, [0, .04, 0], [.8, .14, .9]);
  for (let i = 0; i < 4; i++) mesh(CYL, mat(0x8c2a1e, { r: .5, m: .3 }), eng, [.18 * (i % 2 ? 1 : -1), .13, -.3 + Math.floor(i / 2) * .5], [.07, .1, .07]);
  mesh(CYL, chrome, eng, [0, .14, .1], [.18, .06, .18]);
  mesh(BOX, mat(0x222222), eng, [-.55, .08, .2], [.18, .16, .28]); // battery
  mesh(new THREE.TorusGeometry(.1, .02, 6, 16), rubber, eng, [.3, .12, .4], [1, 1, 1], [0, Math.PI / 2, 0]);
  const hoodP = grp(body, [0, 1.12, .72]);
  mesh(rbox(1.62, .05, 1.72, .03), p1, hoodP, [0, 0, .86]);
  mesh(BOX, chrome, hoodP, [0, .03, 1.2], [.06, .02, .9]);
  const wheels = [];
  for (const zz of [1.6, -1.6]) for (const s of [1, -1]) wheels.push(wheel(g, [s * .86, .36, zz], .36, 0xcfcfcf, true));
  return { g, body, hood: hoodP, wheels };
}

export function beetle(c) {
  const g = new THREE.Group(); const p = mat(c, { r: .3, m: .3, env: 1.1 });
  const body = grp(g);
  mesh(SPH, p, body, [0, .78, 0], [.82, .55, 1.95]);
  mesh(SPH, mat(0x1a222c, { r: .05, m: .3, env: 1.5 }), body, [0, 1.12, -.25], [.66, .4, .95]);
  mesh(SPH, p, body, [0, 1.2, -.3], [.62, .32, .85]);
  for (const s of [1, -1]) for (const zz of [1.1, -1.15]) mesh(SPH, p, body, [s * .7, .62, zz], [.25, .3, .5]);
  for (const s of [1, -1]) mesh(SPH, mat(0xfff2c8, { e: 0xffe0a0, ei: 1.2 }), body, [s * .62, .8, 1.5], [.1, .1, .08]);
  mesh(CYL, chrome, body, [0, .42, 1.85], [.05, 1.6, .05], [0, 0, Math.PI / 2]);
  const wheels = [];
  for (const zz of [1.1, -1.15]) for (const s of [1, -1]) wheels.push(wheel(g, [s * .72, .33, zz], .33, 0xbbbbbb));
  return { g, body, wheels };
}
export function bus() {
  const g = new THREE.Group(); const p = mat(0xE6D7B0, { r: .4, m: .2 }); const s2 = mat(0x2E7F6A, { r: .4, m: .2 });
  const body = grp(g);
  mesh(rbox(2.5, 2.6, 10.5, .2), p, body, [0, 1.75, 0]);
  mesh(BOX, s2, body, [0, 1.0, 0], [2.52, .5, 10.45]);
  for (const s of [1, -1]) for (let i = 0; i < 6; i++) mesh(BOX, mat(0x1a222c, { r: .1, m: .3 }), body, [s * 1.26, 2.3, -4 + i * 1.55], [.02, .9, 1.3]);
  mesh(BOX, mat(0x1a222c, { r: .1, m: .3 }), body, [0, 2.2, 5.26], [2.2, 1.2, .02]);
  mesh(BOX, textTex([{ t: 'CENTRO · CATEDRAL', font: '110px "Bebas Neue"', color: '#ffcf5a' }], { bg: '#111', w: 1024, h: 160, grain: false }) && mat(0xffffff, { map: textTex([{ t: 'CENTRO · CATEDRAL', font: '110px "Bebas Neue"', color: '#ffcf5a' }], { bg: '#111', w: 1024, h: 160, grain: false }), e: 0xffffff, emap: null, ei: .1 }), body, [0, 2.95, 5.27], [2.0, .3, .02]);
  const wheels = [];
  for (const zz of [3.6, -3.6]) for (const s of [1, -1]) wheels.push(wheel(g, [s * 1.1, .5, zz], .5, 0x999999));
  return { g, body, wheels };
}

// ---------------- props ----------------
export function cashStack(parent, p, r = [0, 0, 0]) {
  const s = grp(parent, p, r);
  const top = mat(0xffffff, { map: moneyTex(), r: .7 }); const side = mat(0xffffff, { map: edgeTex(), r: .8 });
  const m = new THREE.Mesh(BOX, [side, side, top, side, side, side]); m.scale.set(.3, .12, .15); m.position.y = .06; m.castShadow = m.receiveShadow = true; s.add(m);
  mesh(BOX, mat(0xEDE3C4, { r: .6 }), s, [0, .06, 0], [.06, .125, .155]);
  return s;
}
export function photoFrame(parent, p, r = [0, 0, 0], sc = 1) {
  const f = grp(parent, p, r); f.scale.setScalar(sc);
  const wood = mat(0x6b3b1e, { r: .4, m: .1 });
  mesh(BOX, wood, f, [0, .16, 0], [.26, .32, .03]);
  mesh(BOX, mat(0xd9a74a, { r: .3, m: .8 }), f, [0, .16, .016], [.22, .28, .004]);
  mesh(new THREE.PlaneGeometry(.18, .225), mat(0xffffff, { map: portraitTex(), r: .5 }), f, [0, .16, .02]);
  mesh(BOX, wood, f, [0, .1, -.06], [.04, .2, .02], [.5, 0, 0]);
  return f;
}
export function phone(parent, p, r = [0, 0, 0]) {
  const g = grp(parent, p, r);
  mesh(rbox(.08, .012, .16, .006), mat(0x111114, { r: .2, m: .6 }), g, [0, .006, 0]);
  const scr = textTex([{ t: 'DESCONOCIDO', font: '64px Anton', color: '#ffffff', y: 210 }, { t: 'llamada entrante…', font: '34px sans-serif', color: '#ffb0a0', y: 280 }], { bg: '#1b0c0c', w: 512, h: 1024, grain: false });
  const cx = scr.image.getContext('2d'); cx.fillStyle = '#2bd46a'; cx.beginPath(); cx.arc(140, 860, 60, 0, 7); cx.fill(); cx.fillStyle = '#e23b2e'; cx.beginPath(); cx.arc(372, 860, 60, 0, 7); cx.fill();
  cx.fillStyle = '#777'; cx.beginPath(); cx.arc(256, 520, 110, 0, 7); cx.fill(); cx.fillStyle = '#ccc'; cx.font = '140px Anton'; cx.textAlign = 'center'; cx.fillText('?', 256, 575); scr.needsUpdate = true;
  const sm = mat(0x000000, { emap: scr, e: 0xffffff, ei: 1.2, map: scr });
  const s = mesh(new THREE.PlaneGeometry(.072, .148), sm, g, [0, .0125, 0], [1, 1, 1], [-Math.PI / 2, 0, 0], false);
  g.userData.screen = sm; return g;
}
