// O casarão de Augusto (exterior). Origem: centro do térreo no chão; fachada voltada para +z.
import * as THREE from 'three';
import * as TX from '../engine/tex.js';
import { roofPrism, windowMesh, merge, deadTree } from './kit.js';
import { rng } from '../lib/util.js';

export const HOUSE = { w: 16, d: 11, base: 0.8, floorH: 3.4 };
// Janela do quarto infantil (segundo andar, à esquerda de quem olha a fachada)
export const CHILD_WINDOW = new THREE.Vector3(-5.4, HOUSE.base + HOUSE.floorH + 1.65, HOUSE.d / 2 + 0.02);

export function buildHouseExterior() {
  const g = new THREE.Group(); g.name = 'house';
  const { w, d, base, floorH } = HOUSE;
  const wallH = floorH * 2;
  const sid = TX.siding({ base: [118, 124, 116] });
  const sidMat = new THREE.MeshStandardMaterial({ map: sid.map, normalMap: sid.normalMap, roughness: 0.85, color: 0xaab2aa });
  [sid.map, sid.normalMap].forEach(t => t.repeat.set(3, 2));
  const trim = new THREE.MeshStandardMaterial({ color: 0x2c2a27, roughness: 0.8 });
  const stoneTex = TX.plaster({ base: [90, 90, 86], seed: 31, dirt: 1 });
  const stone = new THREE.MeshStandardMaterial({ map: stoneTex.map, normalMap: stoneTex.normalMap, roughness: 0.95 });
  const sh = TX.shingles({ base: [44, 46, 50], seed: 12 });
  [sh.map, sh.normalMap].forEach(t => t.repeat.set(4, 3));
  const roofMat = new THREE.MeshStandardMaterial({ map: sh.map, normalMap: sh.normalMap, roughness: 0.5, color: 0x8c9094 });

  // Fundação e corpo
  const found = new THREE.Mesh(new THREE.BoxGeometry(w + 0.4, base, d + 0.4).translate(0, base / 2, 0), stone); g.add(found);
  // paredes com recorte simples: usamos caixa e janelas sobrepostas (rebaixadas por moldura)
  const body = new THREE.Mesh(new THREE.BoxGeometry(w, wallH, d).translate(0, base + wallH / 2, 0), sidMat); g.add(body);
  // faixa entre pavimentos e beirais
  const band = new THREE.Mesh(new THREE.BoxGeometry(w + 0.2, 0.25, d + 0.2).translate(0, base + floorH, 0), trim); g.add(band);
  const eave = new THREE.Mesh(new THREE.BoxGeometry(w + 0.5, 0.3, d + 0.5).translate(0, base + wallH, 0), trim); g.add(eave);
  // telhado principal (cumeeira ao longo de x)
  const roof = new THREE.Mesh(roofPrism(0.05), roofMat); roof.scale.set(w, 4.6, d); roof.position.y = base + wallH + 0.1; g.add(roof);
  // empena frontal central
  const cg = new THREE.Mesh(roofPrism(0.08), roofMat); cg.rotation.y = Math.PI / 2; cg.scale.set(4.5, 4.2, 6.4); cg.position.set(0, base + wallH + 0.1, d / 2 - 1.2); g.add(cg);
  const cgWall = new THREE.Mesh(roofPrism(0), sidMat); cgWall.rotation.y = Math.PI / 2; cgWall.scale.set(1.2, 4.0, 6.0); cgWall.position.set(0, base + wallH + 0.1, d / 2 + 0.38); g.add(cgWall);
  const front = new THREE.Mesh(new THREE.BoxGeometry(6, wallH, 0.8).translate(0, base + wallH / 2, d / 2 + 0.4), sidMat); g.add(front);
  // óculo no frontão
  const oculus = new THREE.Mesh(new THREE.CircleGeometry(0.55, 24), new THREE.MeshStandardMaterial({ color: 0x050708, roughness: 0.1, metalness: 0.5 }));
  oculus.position.set(0, base + wallH + 1.6, d / 2 + 0.82); g.add(oculus);
  // chaminé
  const ch = new THREE.Mesh(new THREE.BoxGeometry(1.1, 4, 1.1).translate(0, 2, 0), stone); ch.position.set(5.6, base + wallH + 2.2, -1.5); g.add(ch);

  // Varanda
  const porchD = 2.8;
  const porchFloor = new THREE.Mesh(new THREE.BoxGeometry(w + 0.4, 0.2, porchD).translate(0, base - 0.1, d / 2 + 0.8 + porchD / 2), trim); g.add(porchFloor);
  const porchFound = new THREE.Mesh(new THREE.BoxGeometry(w + 0.4, base - 0.2, porchD).translate(0, (base - 0.2) / 2, d / 2 + 0.8 + porchD / 2), stone); g.add(porchFound);
  const colGeo = new THREE.CylinderGeometry(0.13, 0.16, floorH - 0.2, 10).translate(0, (floorH - 0.2) / 2, 0);
  const colMat = new THREE.MeshStandardMaterial({ color: 0x8e918a, roughness: 0.8 });
  for (let i = 0; i < 6; i++) {
    const x = -w / 2 + 0.4 + i * (w - 0.8) / 5;
    if (Math.abs(x) < 1) continue;
    const c = new THREE.Mesh(colGeo, colMat); c.position.set(x, base, d / 2 + 0.8 + porchD - 0.3); g.add(c);
  }
  const porchRoof = new THREE.Mesh(new THREE.BoxGeometry(w + 0.6, 0.18, porchD + 0.5), roofMat);
  porchRoof.position.set(0, base + floorH - 0.05, d / 2 + 0.8 + porchD / 2 + 0.1); porchRoof.rotation.x = 0.12; g.add(porchRoof);
  // guarda-corpo
  const rail = []; for (let x = -w / 2 + 0.3; x < w / 2; x += 0.22) { if (Math.abs(x) < 1.2) continue; rail.push(new THREE.BoxGeometry(0.05, 0.8, 0.05).translate(x, base + 0.4, d / 2 + 0.8 + porchD - 0.3)); }
  rail.push(new THREE.BoxGeometry(w / 2 - 1.2, 0.08, 0.1).translate(-(w / 4 + 0.6), base + 0.82, d / 2 + 0.8 + porchD - 0.3));
  rail.push(new THREE.BoxGeometry(w / 2 - 1.2, 0.08, 0.1).translate(w / 4 + 0.6, base + 0.82, d / 2 + 0.8 + porchD - 0.3));
  g.add(new THREE.Mesh(merge(rail), trim));
  // escadas
  for (let i = 0; i < 4; i++) { const s = new THREE.Mesh(new THREE.BoxGeometry(2.2, 0.2, 0.32).translate(0, 0.1 + i * 0.2, 0), stone); s.position.z = d / 2 + 0.8 + porchD + 1.2 - i * 0.32; g.add(s); }

  // Porta principal (dupla) com bandeira — o pivô gira na dobradiça esquerda
  const doorZ = d / 2 + 0.81;
  const doorFrame = new THREE.Mesh(merge([
    new THREE.BoxGeometry(0.2, 2.9, 0.2).translate(-0.95, base + 1.45, 0), new THREE.BoxGeometry(0.2, 2.9, 0.2).translate(0.95, base + 1.45, 0),
    new THREE.BoxGeometry(2.1, 0.22, 0.2).translate(0, base + 2.95, 0),
  ]), trim); doorFrame.position.z = doorZ; g.add(doorFrame);
  const wood = TX.woodPlanks({ planks: 4, base: [60, 42, 30], dark: [28, 20, 15], seed: 9 });
  const doorMat = new THREE.MeshStandardMaterial({ map: wood.map, normalMap: wood.normalMap, roughness: 0.7 });
  const doorDark = new THREE.Mesh(new THREE.PlaneGeometry(1.7, 2.6), new THREE.MeshBasicMaterial({ color: 0x000000 }));
  doorDark.position.set(0, base + 1.3, doorZ - 0.15); g.add(doorDark);
  const doorL = new THREE.Group(); doorL.position.set(-0.85, base, doorZ);
  const leafL = new THREE.Mesh(new THREE.BoxGeometry(0.85, 2.6, 0.07).translate(0.425, 1.3, 0), doorMat); doorL.add(leafL);
  const doorR = new THREE.Group(); doorR.position.set(0.85, base, doorZ);
  const leafR = new THREE.Mesh(new THREE.BoxGeometry(0.85, 2.6, 0.07).translate(-0.425, 1.3, 0), doorMat); doorR.add(leafR);
  const brass = new THREE.MeshStandardMaterial({ color: 0x8a6a3a, metalness: 0.9, roughness: 0.35 });
  const knob = new THREE.Mesh(new THREE.SphereGeometry(0.045, 12, 8), brass); knob.position.set(0.74, 1.1, 0.07); doorL.add(knob);
  const plate = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.24, 0.02), brass); plate.position.set(0.74, 1.05, 0.045); doorL.add(plate);
  const keyhole = new THREE.Mesh(new THREE.CircleGeometry(0.012, 10), new THREE.MeshBasicMaterial({ color: 0 })); keyhole.position.set(0.74, 0.98, 0.056); doorL.add(keyhole);
  g.add(doorL, doorR);
  const transom = new THREE.Mesh(new THREE.PlaneGeometry(1.7, 0.4), new THREE.MeshStandardMaterial({ color: 0x080a0c, roughness: 0.1, metalness: 0.5 }));
  transom.position.set(0, base + 2.75, doorZ + 0.01); g.add(transom);

  // Janelas
  const rg = TX.rainGlass();
  const windows = [];
  const addWin = (x, y, z, opts = {}) => {
    const wm = windowMesh({ w: 1.1, h: 1.9, ...opts });
    wm.position.set(x, y, z);
    const pane = wm.userData.pane;
    if (!opts.lit) { pane.material.normalMap = rg.normalMap; pane.material.normalScale = new THREE.Vector2(0.6, 0.6); }
    g.add(wm); windows.push(wm);
    // venezianas
    for (const sx of [-1, 1]) {
      const sh2 = new THREE.Mesh(new THREE.BoxGeometry(0.58, 2.0, 0.05), new THREE.MeshStandardMaterial({ color: 0x2c3a34, roughness: 0.8 }));
      sh2.position.set(x + sx * 0.92, y, z + 0.06); sh2.rotation.y = sx * (opts.shutterOpen ?? 0.15); g.add(sh2);
    }
    return wm;
  };
  const zf = d / 2 + 0.02;
  for (const x of [-6.6, -4.2, 4.2, 6.6]) addWin(x, base + 1.6, zf);
  for (const x of [-6.6, 4.2, 6.6]) addWin(x, base + floorH + 1.65, zf);
  const childWin = addWin(CHILD_WINDOW.x, CHILD_WINDOW.y, zf + 0.16);
  childWin.userData.pane.material = new THREE.MeshStandardMaterial({ color: 0x223038, roughness: 0.05, metalness: 0.3, transparent: true, opacity: 0.42, normalMap: rg.normalMap, normalScale: new THREE.Vector2(0.6, 0.6) });
  addWin(0, base + floorH + 1.65, d / 2 + 0.82);

  // Quarto infantil por trás da janela: caixa interna escura, cortina e silhueta
  const room = new THREE.Group(); room.position.set(CHILD_WINDOW.x, CHILD_WINDOW.y, zf);
  const glow = new THREE.Mesh(new THREE.PlaneGeometry(1.15, 1.95), new THREE.MeshBasicMaterial({ color: 0x000000 }));
  glow.position.z = 0.03; room.add(glow);
  const curtainTex = TX.fabric({ base: [150, 150, 136], seed: 40 });
  const curtainGeo = new THREE.PlaneGeometry(1.15, 1.95, 40, 10);
  const cp = curtainGeo.attributes.position; for (let i = 0; i < cp.count; i++) cp.setZ(i, Math.sin(cp.getX(i) * 22) * 0.025);
  curtainGeo.computeVertexNormals();
  const curtainMat = new THREE.MeshStandardMaterial({ map: curtainTex.map, color: 0x6e706a, transparent: true, opacity: 0.88, side: THREE.DoubleSide, roughness: 1, emissive: 0x000000 });
  const curtain = new THREE.Mesh(curtainGeo, curtainMat); curtain.position.z = 0.11; room.add(curtain);
  // silhueta: forma humana recortada (cabeça + ombros + tronco)
  const silShape = new THREE.Shape();
  silShape.moveTo(-0.32, -1.1); silShape.lineTo(-0.3, 0.05); silShape.quadraticCurveTo(-0.28, 0.25, -0.1, 0.3);
  silShape.lineTo(-0.07, 0.38); silShape.absellipse(0, 0.52, 0.12, 0.16, -Math.PI * 0.62, Math.PI * 1.62, false);
  silShape.lineTo(0.1, 0.3); silShape.quadraticCurveTo(0.28, 0.25, 0.3, 0.05); silShape.lineTo(0.32, -1.1);
  const sil = new THREE.Mesh(new THREE.ShapeGeometry(silShape, 24), new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0 }));
  sil.position.set(0.12, -0.25, 0.07); sil.scale.setScalar(0.95); room.add(sil);
  g.add(room);

  // jardim abandonado: árvores secas e mato
  const r = rng(55);
  [[-11, 6, 7], [10.5, 8, 6.2], [-14, -3, 8], [13, -6, 7.5], [-7, 14, 5.5]].forEach(([x, z, h], i) => {
    const t = deadTree({ seed: 100 + i, height: h }); t.position.set(x, 0, z); t.rotation.y = r() * 6; g.add(t);
  });

  g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  sil.castShadow = false; glow.castShadow = false; curtain.castShadow = false;
  g.userData = { doorL, doorR, keyhole, childWin, curtain, curtainMat, sil, glow, windows };
  return g;
}
