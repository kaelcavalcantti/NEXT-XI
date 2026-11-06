// Cidade fictícia de Pedra Funda: casario colonial, praça com igreja, ruas de paralelepípedo molhadas, postes.
import * as THREE from 'three';
import { rng } from '../lib/util.js';
import * as TX from '../engine/tex.js';
import { roofPrism, makeTrees, makeLamp, merge } from './kit.js';

export function buildTown() {
  const r = rng(2024);
  const g = new THREE.Group();
  g.name = 'town';

  // Chão: grama/terra e ruas
  const dirt = TX.groundDirt();
  dirt.map.repeat.set(40, 40); dirt.normalMap.repeat.set(40, 40); dirt.roughnessMap.repeat.set(40, 40);
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(260, 225).rotateX(-Math.PI / 2).translate(0, 0, 17.5),
    new THREE.MeshStandardMaterial({ map: dirt.map, normalMap: dirt.normalMap, roughnessMap: dirt.roughnessMap, color: 0x8a8a80 }));
  ground.receiveShadow = true; g.add(ground);

  const road = TX.wetRoad({ cobble: true });
  const roadMat = new THREE.MeshStandardMaterial({ map: road.map, normalMap: road.normalMap, roughnessMap: road.roughnessMap, metalness: 0.15, color: 0x9aa0a6 });
  road.map.repeat.set(1, 30); road.normalMap.repeat.set(1, 30); road.roughnessMap.repeat.set(1, 30);
  const step = 26, n = 3; // ruas em x, z: -78..78
  const roadGeos = [];
  for (let i = -n; i <= n; i++) {
    roadGeos.push(new THREE.PlaneGeometry(7, 200).rotateX(-Math.PI / 2).translate(i * step, 0.02, 0));
    roadGeos.push(new THREE.PlaneGeometry(200, 7).rotateX(-Math.PI / 2).translate(0, 0.021, i * step));
  }
  const roads = new THREE.Mesh(merge(roadGeos), roadMat); roads.receiveShadow = true; g.add(roads);

  // Casario instanciado
  const palette = [[0.62, 0.6, 0.55], [0.55, 0.6, 0.62], [0.6, 0.55, 0.5], [0.45, 0.52, 0.5], [0.66, 0.64, 0.6], [0.5, 0.46, 0.44]];
  const houses = [];
  for (let bx = -n; bx < n; bx++) for (let bz = -n; bz < n; bz++) {
    const cx = (bx + 0.5) * step, cz = (bz + 0.5) * step;
    if (Math.abs(cx) < 14 && Math.abs(cz) < 14) continue; // praça
    // casas alinhadas nos quatro lados do quarteirão
    for (const side of [0, 1, 2, 3]) {
      let x = -step / 2 + 6;
      while (x < step / 2 - 6) {
        const w = r.range(5, 8.5), d = r.range(7, 9), h = r() < 0.2 ? r.range(5.5, 7) : r.range(3.2, 4.2);
        const off = x + w / 2; x += w + r.range(0, 0.8);
        if (x > step / 2 - 4) break;
        const inset = step / 2 - 3.5 - d / 2;
        let px, pz, rot;
        if (side === 0) { px = cx + off; pz = cz + inset; rot = 0; }
        else if (side === 1) { px = cx + off; pz = cz - inset; rot = Math.PI; }
        else if (side === 2) { px = cx + inset; pz = cz + off; rot = Math.PI / 2; }
        else { px = cx - inset; pz = cz + off; rot = -Math.PI / 2; }
        houses.push({ x: px, z: pz, w, d, h, rot, c: r.pick(palette), roofH: r.range(1.4, 2.4) });
      }
    }
  }
  const plaster = TX.plaster({ base: [200, 196, 186], dirt: 0.9 });
  const bodyMat = new THREE.MeshStandardMaterial({ map: plaster.map, normalMap: plaster.normalMap, roughness: 0.9 });
  const bodyGeo = new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0);
  const bodies = new THREE.InstancedMesh(bodyGeo, bodyMat, houses.length);
  const sh = TX.shingles({ base: [70, 48, 40], seed: 4 });
  const roofMat = new THREE.MeshStandardMaterial({ map: sh.map, normalMap: sh.normalMap, roughness: 0.55, color: 0x9a8a84 });
  const roofs = new THREE.InstancedMesh(roofPrism(0.08), roofMat, houses.length);
  const m = new THREE.Matrix4(), q = new THREE.Quaternion(), s = new THREE.Vector3(), p = new THREE.Vector3();
  const winLit = [], winDark = [], doors = [];
  houses.forEach((hs, i) => {
    q.setFromAxisAngle(new THREE.Vector3(0, 1, 0), hs.rot);
    m.compose(p.set(hs.x, 0, hs.z), q, s.set(hs.w, hs.h, hs.d)); bodies.setMatrixAt(i, m);
    bodies.setColorAt(i, new THREE.Color(...hs.c));
    m.compose(p.set(hs.x, hs.h, hs.z), q, s.set(hs.w, hs.roofH, hs.d)); roofs.setMatrixAt(i, m);
    // janelas na fachada (frente local +z)
    const floors = hs.h > 5 ? 2 : 1;
    const nw = Math.max(2, Math.floor(hs.w / 2.2));
    for (let f = 0; f < floors; f++) for (let k = 0; k < nw; k++) {
      const lx = -hs.w / 2 + (k + 0.5) * hs.w / nw;
      if (f === 0 && k === Math.floor(nw / 2)) { doors.push([hs, lx]); continue; }
      const ly = f === 0 ? 1.6 : 4.3;
      const v = new THREE.Vector3(lx, ly, hs.d / 2 + 0.02).applyQuaternion(q).add(new THREE.Vector3(hs.x, 0, hs.z));
      (r() < 0.13 ? winLit : winDark).push([v, hs.rot, r()]);
    }
  });
  bodies.instanceColor.needsUpdate = true;
  bodies.castShadow = bodies.receiveShadow = true; roofs.castShadow = true;
  g.add(bodies, roofs);

  const winGeo = new THREE.PlaneGeometry(0.95, 1.35);
  const mk = (list, mat) => {
    const im = new THREE.InstancedMesh(winGeo, mat, list.length);
    list.forEach(([v, rot, k], i) => { q.setFromAxisAngle(new THREE.Vector3(0, 1, 0), rot); m.compose(v, q, s.set(1, 1, 1)); im.setMatrixAt(i, m); im.setColorAt(i, new THREE.Color().setHSL(0.08, 0.7, 0.35 + 0.3 * k)); });
    return im;
  };
  const litMat = new THREE.MeshBasicMaterial({ color: 0xffffff });
  g.add(mk(winLit, litMat));
  const darkMat = new THREE.MeshStandardMaterial({ color: 0x0a0e12, roughness: 0.05, metalness: 0.6 });
  const dw = new THREE.InstancedMesh(winGeo, darkMat, winDark.length);
  winDark.forEach(([v, rot], i) => { q.setFromAxisAngle(new THREE.Vector3(0, 1, 0), rot); m.compose(v, q, s.set(1, 1, 1)); dw.setMatrixAt(i, m); });
  g.add(dw);
  const doorGeo = new THREE.PlaneGeometry(1.1, 2.2).translate(0, 1.1, 0);
  const dm = new THREE.InstancedMesh(doorGeo, new THREE.MeshStandardMaterial({ color: 0x2a1f18, roughness: 0.7 }), doors.length);
  doors.forEach(([hs, lx], i) => { q.setFromAxisAngle(new THREE.Vector3(0, 1, 0), hs.rot); const v = new THREE.Vector3(lx, 0, hs.d / 2 + 0.02).applyQuaternion(q).add(new THREE.Vector3(hs.x, 0, hs.z)); m.compose(v, q, s.set(1, 1, 1)); dm.setMatrixAt(i, m); });
  g.add(dm);

  // Igreja na praça
  const church = new THREE.Group();
  const cm = new THREE.MeshStandardMaterial({ map: plaster.map, normalMap: plaster.normalMap, color: 0xd8d4c8, roughness: 0.9 });
  const nave = new THREE.Mesh(new THREE.BoxGeometry(9, 8, 16).translate(0, 4, 0), cm); church.add(nave);
  const nroof = new THREE.Mesh(roofPrism(0.05), roofMat); nroof.scale.set(9, 3.5, 16); nroof.position.y = 8; church.add(nroof);
  const tower = new THREE.Mesh(new THREE.BoxGeometry(3.6, 15, 3.6).translate(0, 7.5, 0), cm); tower.position.z = 9; church.add(tower);
  const spire = new THREE.Mesh(new THREE.ConeGeometry(2.6, 5, 4).rotateY(Math.PI / 4), roofMat); spire.position.set(0, 17.5, 9); church.add(spire);
  const cross = new THREE.Mesh(merge([new THREE.BoxGeometry(0.15, 1.6, 0.15), new THREE.BoxGeometry(0.9, 0.15, 0.15).translate(0, 0.25, 0)]), new THREE.MeshStandardMaterial({ color: 0x222222 }));
  cross.position.set(0, 20.8, 9); church.add(cross);
  const rose = new THREE.Mesh(new THREE.CircleGeometry(0.8, 24), new THREE.MeshBasicMaterial({ color: new THREE.Color(0xffb070).multiplyScalar(0.8) }));
  rose.position.set(0, 12, 10.82); church.add(rose);
  church.position.set(0, 0, -3); church.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  g.add(church);

  // Árvores: praça e ruas
  const trees = [];
  for (let i = 0; i < 14; i++) { const a = i / 14 * Math.PI * 2; trees.push({ x: Math.cos(a) * 11, z: Math.sin(a) * 11 - 1, h: r.range(6, 8) }); }
  for (let i = 0; i < 40; i++) { const sx = r.int(-n, n) * step + r.pick([-4.6, 4.6]); trees.push({ x: sx, z: r.range(-85, 85), h: r.range(5, 8) }); }
  for (let i = 0; i < 90; i++) { const a = r() * 6.28, d = r.range(95, 125); trees.push({ x: Math.cos(a) * d, z: Math.sin(a) * d, h: r.range(7, 12) }); }
  g.add(makeTrees(trees, { seed: 7, wind: 0.25 }));

  // Postes nas ruas
  const lamps = [];
  for (let i = -n; i <= n; i++) for (let k = -3; k <= 3; k++) {
    const lp = makeLamp({ height: 5.5, coneInt: 0.09, poolSize: 10, poolInt: 0.35 });
    lp.position.set(i * step + 3.6, 0, k * step + 13); lp.rotation.y = Math.PI;
    g.add(lp); lamps.push(lp);
  }
  g.userData.lamps = lamps;
  return g;
}
