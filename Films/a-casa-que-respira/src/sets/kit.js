// Peças de cenário reutilizáveis: prismas de telhado, árvores com vento, postes, poças de luz.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { G, makeLightCone } from '../engine/fx.js';
import { rng } from '../lib/util.js';
import * as TX from '../engine/tex.js';

export const merge = (geos) => mergeGeometries(geos.map(g => g.index ? g.toNonIndexed() : g), false);

// Prisma de telhado de duas águas: largura 1 (x), profundidade 1 (z), altura 1, base em y=0.
export function roofPrism(overhang = 0.06) {
  const o = overhang, w = 0.5 + o, d = 0.5 + o;
  const v = [
    // água frontal (z+)
    -w, 0, d, w, 0, d, w, 1, 0, -w, 0, d, w, 1, 0, -w, 1, 0,
    // água traseira
    w, 0, -d, -w, 0, -d, -w, 1, 0, w, 0, -d, -w, 1, 0, w, 1, 0,
    // empenas
    -0.5, 0, -0.5, -0.5, 0, 0.5, -0.5, 1, 0,
    0.5, 0, 0.5, 0.5, 0, -0.5, 0.5, 1, 0,
  ];
  const uv = [0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 0.5, 1, 0, 0, 1, 0, 0.5, 1];
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(v, 3));
  g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  g.computeVertexNormals();
  return g;
}

// Injeta balanço de vento num material padrão (por altura do vértice).
export function windify(mat, { amp = 0.15, freq = 0.8, base = 0 } = {}) {
  mat.onBeforeCompile = (sh) => {
    sh.uniforms.time = G.time;
    sh.uniforms.windAmp = { value: amp };
    sh.vertexShader = sh.vertexShader
      .replace('#include <common>', '#include <common>\nuniform float time; uniform float windAmp;')
      .replace('#include <begin_vertex>', `#include <begin_vertex>
        {
          vec4 wp0 = vec4(transformed, 1.0);
          #ifdef USE_INSTANCING
            wp0 = instanceMatrix * wp0;
          #endif
          vec4 wpos = modelMatrix * wp0;
          float hgt = max(0.0, transformed.y - ${base.toFixed(2)});
          float ph = wpos.x * 0.13 + wpos.z * 0.11;
          float sw = sin(time * ${freq.toFixed(2)} + ph) * 0.6 + sin(time * ${(freq * 2.3).toFixed(2)} + ph * 1.7) * 0.3 + sin(time * ${(freq * 5.1).toFixed(2)} + ph * 3.0) * 0.1;
          transformed.x += sw * windAmp * hgt * hgt * 0.1;
          transformed.z += sw * windAmp * hgt * hgt * 0.05;
        }`);
  };
  return mat;
}

// Árvore frondosa (tronco + copas) instanciada.
export function makeTrees(list, { seed = 1, leafColor = 0x1d2a22, trunkColor = 0x2a221c, wind = 0.2, castShadow = false } = {}) {
  const r = rng(seed);
  const g = new THREE.Group();
  const bk = TX.bark();
  const trunkGeo = new THREE.CylinderGeometry(0.12, 0.28, 1, 7, 4).translate(0, 0.5, 0);
  const trunkMat = new THREE.MeshStandardMaterial({ color: trunkColor, map: bk.map, normalMap: bk.normalMap, roughness: 0.95 });
  const trunks = new THREE.InstancedMesh(trunkGeo, trunkMat, list.length);
  const blobs = [];
  const m = new THREE.Matrix4(), q = new THREE.Quaternion(), s = new THREE.Vector3(), p = new THREE.Vector3();
  list.forEach((t, i) => {
    const hgt = t.h ?? r.range(6, 11);
    p.set(t.x, t.y ?? 0, t.z); q.setFromEuler(new THREE.Euler(r.range(-0.05, 0.05), r() * 6.28, r.range(-0.05, 0.05)));
    s.set(1, hgt * 0.62, 1); m.compose(p, q, s); trunks.setMatrixAt(i, m);
    const nb = 5 + Math.floor(r() * 4);
    for (let k = 0; k < nb; k++) {
      const a = r() * 6.28, rr = r.range(0.4, 1.8) * hgt / 9;
      blobs.push([t.x + Math.cos(a) * rr, (t.y ?? 0) + hgt * r.range(0.55, 0.95), t.z + Math.sin(a) * rr, r.range(1.3, 2.4) * hgt / 9]);
    }
  });
  trunks.castShadow = castShadow;
  g.add(trunks);
  // copa: icosaedro deformado com ruído
  const leafGeo = new THREE.IcosahedronGeometry(1, 3);
  const pos = leafGeo.attributes.position; const rr = rng(seed + 9);
  for (let i = 0; i < pos.count; i++) {
    p.fromBufferAttribute(pos, i); const n = 0.75 + 0.5 * rr();
    p.multiplyScalar(n); p.y *= 0.8; pos.setXYZ(i, p.x, p.y, p.z);
  }
  leafGeo.computeVertexNormals();
  const leafMat = windify(new THREE.MeshStandardMaterial({ color: leafColor, roughness: 0.9, flatShading: true }), { amp: wind * 4, freq: 0.9 });
  const leaves = new THREE.InstancedMesh(leafGeo, leafMat, blobs.length);
  blobs.forEach((b, i) => { p.set(b[0], b[1], b[2]); q.setFromEuler(new THREE.Euler(r() * 3, r() * 3, r() * 3)); s.setScalar(b[3]); m.compose(p, q, s); leaves.setMatrixAt(i, m); });
  leaves.castShadow = castShadow;
  g.add(leaves);
  return g;
}

// Árvore seca com galhos recursivos (jardim abandonado).
export function deadTree({ seed = 3, height = 6, color = 0x231d18 } = {}) {
  const r = rng(seed); const geos = [];
  function branch(start, dir, len, rad, depth) {
    const end = start.clone().addScaledVector(dir, len);
    const geo = new THREE.CylinderGeometry(rad * 0.65, rad, len, 6, 1);
    geo.translate(0, len / 2, 0);
    const q = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
    geo.applyQuaternion(q); geo.translate(start.x, start.y, start.z);
    geos.push(geo);
    if (depth <= 0 || rad < 0.015) return;
    const n = depth > 2 ? 2 + Math.floor(r() * 2) : 2 + Math.floor(r() * 3);
    for (let i = 0; i < n; i++) {
      const nd = dir.clone().add(new THREE.Vector3(r.range(-0.9, 0.9), r.range(-0.1, 0.6), r.range(-0.9, 0.9))).normalize();
      branch(end, nd, len * r.range(0.55, 0.8), rad * 0.62, depth - 1);
    }
  }
  branch(new THREE.Vector3(0, 0, 0), new THREE.Vector3(0, 1, 0), height * 0.35, height * 0.045, 5);
  const geo = merge(geos);
  const bk = TX.bark();
  const mat = windify(new THREE.MeshStandardMaterial({ color, map: bk.map, roughness: 0.95 }), { amp: 0.05, freq: 0.6 });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.castShadow = true;
  return mesh;
}

// Poste de iluminação com lâmpada, cone volumétrico e poça de luz no chão.
export function makeLamp({ height = 6, color = 0xffc98a, cone = true, coneInt = 0.12, pool = true, poolSize = 9, poolInt = 0.5 } = {}) {
  const g = new THREE.Group();
  const metal = new THREE.MeshStandardMaterial({ color: 0x1c1e20, roughness: 0.6, metalness: 0.6 });
  const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.09, height, 8), metal);
  pole.position.y = height / 2; g.add(pole);
  const arm = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.035, 1.2, 6), metal);
  arm.rotation.z = Math.PI / 2; arm.position.set(0.55, height - 0.1, 0); g.add(arm);
  const head = new THREE.Mesh(new THREE.ConeGeometry(0.28, 0.25, 12, 1, true), metal);
  head.position.set(1.1, height - 0.2, 0); g.add(head);
  const bulbMat = new THREE.MeshBasicMaterial({ color: new THREE.Color(color).multiplyScalar(6) });
  const bulb = new THREE.Mesh(new THREE.SphereGeometry(0.11, 12, 8), bulbMat);
  bulb.position.set(1.1, height - 0.32, 0); g.add(bulb);
  g.userData = { bulb, bulbMat, color: new THREE.Color(color), lampPos: new THREE.Vector3(1.1, height - 0.32, 0) };
  if (cone) {
    const c = makeLightCone({ length: height - 0.3, radius: 2.4, color, intensity: coneInt, topRadius: 0.12 });
    c.position.copy(bulb.position); g.add(c); g.userData.cone = c;
  }
  if (pool) {
    const p = makeLightPool(poolSize, color, poolInt);
    p.position.set(1.1, 0.03, 0); g.add(p); g.userData.pool = p;
  }
  return g;
}

let poolTex = null;
export function makeLightPool(size, color, intensity) {
  if (!poolTex) {
    const c = TX.canvas(256, 256), x = c.getContext('2d');
    const gr = x.createRadialGradient(128, 128, 0, 128, 128, 128);
    gr.addColorStop(0, 'rgba(255,255,255,1)'); gr.addColorStop(0.3, 'rgba(255,255,255,0.45)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
    x.fillStyle = gr; x.fillRect(0, 0, 256, 256);
    poolTex = new THREE.CanvasTexture(c);
  }
  const m = new THREE.Mesh(new THREE.PlaneGeometry(size, size).rotateX(-Math.PI / 2),
    new THREE.MeshBasicMaterial({ map: poolTex, color: new THREE.Color(color).multiplyScalar(intensity), blending: THREE.AdditiveBlending, transparent: true, depthWrite: false }));
  m.renderOrder = 5;
  return m;
}

// Janela simples: moldura + vidro (escuro ou aceso)
export function windowMesh({ w = 1, h = 1.4, lit = 0, litColor = 0xffb060, frame = 0x2a2622, glass = 0x0b1014 } = {}) {
  const g = new THREE.Group();
  const fm = new THREE.MeshStandardMaterial({ color: frame, roughness: 0.8 });
  const t = 0.08;
  const parts = [[w + t * 2, t, 0, h / 2 + t / 2], [w + t * 2, t, 0, -h / 2 - t / 2], [t, h, -w / 2 - t / 2, 0], [t, h, w / 2 + t / 2, 0], [t * 0.6, h, 0, 0], [w, t * 0.6, 0, h * 0.1]];
  for (const [pw, ph, x, y] of parts) { const b = new THREE.Mesh(new THREE.BoxGeometry(pw, ph, 0.12), fm); b.position.set(x, y, 0.02); g.add(b); }
  const gm = lit > 0
    ? new THREE.MeshBasicMaterial({ color: new THREE.Color(litColor).multiplyScalar(lit) })
    : new THREE.MeshStandardMaterial({ color: glass, roughness: 0.08, metalness: 0.4 });
  const pane = new THREE.Mesh(new THREE.PlaneGeometry(w, h), gm);
  g.add(pane);
  g.userData.pane = pane;
  return g;
}
