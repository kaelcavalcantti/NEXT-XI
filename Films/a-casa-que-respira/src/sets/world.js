// Campo entre a cidade e a casa: relevo, estrada sinuosa, mata fechada e o casarão no alto.
import * as THREE from 'three';
import * as TX from '../engine/tex.js';
import { rng, smooth, noise1 } from '../lib/util.js';
import { makeTrees, makeLamp } from './kit.js';
import { buildHouseExterior, HOUSE } from './house.js';

export const HOUSE_Z = -430;
export function roadX(z) {
  const k = smooth(-95, -150, z) * (1 - smooth(-350, -405, z));
  return 22 * Math.sin((z + 95) * 0.0125) * k;
}
function hills(x, z) { return (noise1(x * 0.02, 3) + noise1(z * 0.017, 5) * 0.8 + noise1((x + z) * 0.045, 9) * 0.4) * 4; }
export function terrainY(x, z) {
  const rise = 9 * smooth(-120, -400, z);
  const roadMask = smooth(5, 14, Math.abs(x - roadX(z)));
  const houseMask = smooth(24, 46, Math.hypot(x, z - HOUSE_Z));
  const townMask = smooth(-100, -140, z);
  return rise + hills(x, z) * roadMask * houseMask * townMask + (1 - roadMask) * 0 - (1 - houseMask) * 0;
}
export const HOUSE_Y = terrainY(0, HOUSE_Z);

export function getWorld(ctx) {
  if (ctx.sets.world) return ctx.sets.world;
  const group = new THREE.Group();
  const r = rng(808);

  // Relevo
  const W = 700, D = 520, SX = 140, SZ = 104;
  const geo = new THREE.PlaneGeometry(W, D, SX, SZ).rotateX(-Math.PI / 2);
  geo.translate(0, 0, -95 - D / 2);
  const p = geo.attributes.position;
  for (let i = 0; i < p.count; i++) p.setY(i, terrainY(p.getX(i), p.getZ(i)));
  geo.computeVertexNormals();
  const dirt = TX.groundDirt({ seed: 19 });
  const rep = 70; [dirt.map, dirt.normalMap, dirt.roughnessMap].forEach(t => { t.repeat.set(rep, rep * D / W); });
  const terrain = new THREE.Mesh(geo, new THREE.MeshStandardMaterial({ map: dirt.map, normalMap: dirt.normalMap, roughnessMap: dirt.roughnessMap, color: 0x7f857a }));
  terrain.receiveShadow = true;
  group.add(terrain);

  // Estrada: fita seguindo o relevo
  const pts = [], uvs = [], idx = [];
  const z0 = -92, z1 = HOUSE_Z + 18, N = 400, half = 3.2;
  for (let i = 0; i <= N; i++) {
    const z = z0 + (z1 - z0) * i / N, x = roadX(z);
    const dz = 0.5, dx = roadX(z - dz) - x; const len = Math.hypot(dx, dz);
    const nx = dz / len, nz = dx / len; // normal lateral
    for (const s of [-1, 1]) {
      const px = x + nx * half * s, pz = z + nz * half * s;
      pts.push(px, terrainY(px, pz) + 0.06, pz); uvs.push((s + 1) / 2, i * 0.15);
    }
    if (i < N) { const a = i * 2; idx.push(a, a + 2, a + 1, a + 1, a + 2, a + 3); }
  }
  const rg = new THREE.BufferGeometry();
  rg.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
  rg.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
  rg.setIndex(idx); rg.computeVertexNormals();
  const asphalt = TX.wetRoad({ seed: 44 });
  [asphalt.map, asphalt.normalMap, asphalt.roughnessMap].forEach(t => t.repeat.set(1, 1));
  const road = new THREE.Mesh(rg, new THREE.MeshStandardMaterial({ map: asphalt.map, normalMap: asphalt.normalMap, roughnessMap: asphalt.roughnessMap, metalness: 0.2, color: 0x8a9096 }));
  road.receiveShadow = true;
  group.add(road);
  // faixa central apagada
  const lineMat = new THREE.MeshStandardMaterial({ color: 0x9a9480, roughness: 0.4 });
  const lines = new THREE.Group();
  for (let z = z0 - 4; z > HOUSE_Z + 40; z -= 7) {
    const m = new THREE.Mesh(new THREE.PlaneGeometry(0.12, 2.6).rotateX(-Math.PI / 2), lineMat);
    const x = roadX(z); m.position.set(x, terrainY(x, z) + 0.08, z); m.rotation.y = Math.atan2(roadX(z - 1) - x, -1) + Math.PI; lines.add(m);
  }
  group.add(lines);

  // Mata fechada ao longo da estrada (sem invadir a estrada e a clareira da casa)
  const trees = [];
  let tries = 0;
  while (trees.length < 1700 && tries < 30000) {
    tries++;
    const z = r.range(-110, -600), x = r.range(-330, 330);
    const dRoad = Math.abs(x - roadX(z));
    const dHouse = Math.hypot(x, z - HOUSE_Z);
    if (dRoad < 7 || dHouse < 34) continue;
    const dens = dRoad < 40 ? 1 : 0.35;
    if (r() > dens) continue;
    trees.push({ x, z, y: terrainY(x, z) - 0.2, h: r.range(8, 15) });
  }
  const forest = makeTrees(trees, { seed: 33, leafColor: 0x18231c, wind: 0.3 });
  group.add(forest);

  // Postes velhos ao longo do início da estrada
  for (let z = -100; z > -200; z -= 34) {
    const lp = makeLamp({ height: 6, coneInt: 0.07, poolSize: 8, poolInt: 0.25 });
    const x = roadX(z) + 4.2; lp.position.set(x, terrainY(x, z), z); lp.rotation.y = Math.PI; group.add(lp);
  }

  // Casa
  const house = buildHouseExterior();
  house.position.set(0, HOUSE_Y, HOUSE_Z);
  group.add(house);
  // portão de ferro
  const iron = new THREE.MeshStandardMaterial({ color: 0x141414, metalness: 0.7, roughness: 0.5 });
  const gate = new THREE.Group();
  for (const sx of [-1, 1]) {
    const pil = new THREE.Mesh(new THREE.BoxGeometry(0.6, 2.4, 0.6).translate(0, 1.2, 0), new THREE.MeshStandardMaterial({ color: 0x55574f, roughness: 0.95 }));
    pil.position.set(sx * 3.6, 0, 0); gate.add(pil);
    const leaf = new THREE.Group(); leaf.position.set(sx * 3.3, 0, 0); leaf.rotation.y = sx * -1.2;
    for (let i = 0; i < 11; i++) { const b = new THREE.Mesh(new THREE.CylinderGeometry(0.02, 0.02, 2, 5).translate(0, 1.1, 0), iron); b.position.x = -sx * (0.1 + i * 0.29); leaf.add(b); }
    const top = new THREE.Mesh(new THREE.BoxGeometry(3.1, 0.06, 0.05), iron); top.position.set(-sx * 1.55, 2.05, 0); leaf.add(top);
    const bot = top.clone(); bot.position.y = 0.3; leaf.add(bot);
    gate.add(leaf);
  }
  // muro baixo
  for (const sx of [-1, 1]) {
    const wall = new THREE.Mesh(new THREE.BoxGeometry(26, 1.1, 0.4).translate(sx * 16.9, 0.55, 0), new THREE.MeshStandardMaterial({ color: 0x4a4c46, roughness: 0.95 }));
    gate.add(wall);
  }
  gate.position.set(0, terrainY(0, HOUSE_Z + 22), HOUSE_Z + 22);
  gate.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  group.add(gate);

  // caminho de terra batida do portão até a varanda
  const path = new THREE.Mesh(new THREE.PlaneGeometry(5, 18).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x3a342c, roughness: 0.6, map: asphalt.map }));
  path.position.set(0, HOUSE_Y + 0.05, HOUSE_Z + 13); path.receiveShadow = true; group.add(path);

  const world = {
    group, house, terrain, forest, road,
    setVisible() {},
  };
  ctx.sets.world = world;
  return world;
}
export { HOUSE };
