// A herança (apartamento), a viagem (carro na chuva, rádio) e a chegada ao casarão.
import * as THREE from 'three';
import { EVENTS, shotById } from '../timeline.js';
import { makeScene, makeCamera, placeCamera, camPath } from '../engine/scene.js';
import { G, makeRain, makeLightCone } from '../engine/fx.js';
import { kf, smooth, ease, noise1, fbm1, lightningAt, clamp, rng } from '../lib/util.js';
import * as TX from '../engine/tex.js';
import { exteriorWorld, updateExterior } from './opening.js';
import { getGabriel } from './interior_world.js';
import { roadX, terrainY, HOUSE_Z, HOUSE_Y } from '../sets/world.js';
import { buildCar, buildCarInterior, wiperAngle, drawRadio } from '../sets/car.js';
import { windowMesh } from '../sets/kit.js';
import { cut, lips, phoneToEar, phoneScreen, fearFace, applyWalk } from './common.js';
import * as P from '../sets/props.js';

const L = (id, ev) => ev - shotById[id].start;

// ---------------- APARTAMENTO ----------------
function buildApartment() {
  P.mats();
  const scene = makeScene({ fog: 0x0a0d10, density: 0.03 });
  const g = new THREE.Group(); scene.add(g);
  const wpT = TX.plaster({ base: [120, 124, 126], seed: 51, dirt: 0.25 });
  const wall = new THREE.MeshStandardMaterial({ map: wpT.map, normalMap: wpT.normalMap, roughness: 0.95, color: 0x8c949a, side: THREE.BackSide });
  const room = new THREE.Mesh(new THREE.BoxGeometry(4.5, 2.7, 4.2).translate(0, 1.35, 0), wall); room.receiveShadow = true; g.add(room);
  // recorte da janela: parede de fundo é substituída por moldura + vidro + paisagem
  const fl = TX.woodPlanks({ planks: 7, base: [110, 84, 60], dark: [60, 44, 32], seed: 52, worn: 0.1 });
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(4.5, 4.2).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ map: fl.map, normalMap: fl.normalMap, roughness: 0.6 }));
  floor.position.y = 0.002; floor.receiveShadow = true; g.add(floor);
  // parede da janela (com abertura)
  const s = new THREE.Shape(); s.moveTo(-2.25, 0); s.lineTo(2.25, 0); s.lineTo(2.25, 2.7); s.lineTo(-2.25, 2.7); s.lineTo(-2.25, 0);
  const hole = new THREE.Path(); hole.moveTo(-0.6, 0.9); hole.lineTo(1.0, 0.9); hole.lineTo(1.0, 2.3); hole.lineTo(-0.6, 2.3); hole.lineTo(-0.6, 0.9); s.holes.push(hole);
  const bw = new THREE.Mesh(new THREE.ShapeGeometry(s), new THREE.MeshStandardMaterial({ map: wpT.map, roughness: 0.95, color: 0x8c949a }));
  bw.position.z = -2.09; g.add(bw);
  const win = windowMesh({ w: 1.6, h: 1.4 }); win.position.set(0.2, 1.6, -2.08); win.userData.pane.visible = false; g.add(win);
  // vidro com gotas escorrendo
  const glass = new THREE.Mesh(new THREE.PlaneGeometry(1.6, 1.4), new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, uniforms: { time: G.time },
    vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }`,
    fragmentShader: /* glsl */`uniform float time; varying vec2 vUv;
      float h(vec2 p){ return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
      void main(){ float a = 0.0, sp = 0.0;
        for (int k = 0; k < 3; k++){ float sc = 18.0 + float(k) * 13.0; vec2 g = vUv * vec2(sc, sc * 0.875);
          vec2 id = floor(g); float r = h(id + float(k)); float fall = fract(time * (0.05 + r * 0.12) + r);
          vec2 f = fract(g) - vec2(0.5, 1.0 - fall);
          float d = length(f * vec2(1.0, 0.8)); float rad = 0.1 + 0.15 * h(id + 4.0);
          float dr = smoothstep(rad, rad * 0.5, d) * step(0.45, r);
          float trail = smoothstep(0.05, 0.0, abs(f.x)) * step(0.0, f.y) * step(f.y, 0.5) * step(0.7, r) * 0.4;
          a += dr + trail; sp += smoothstep(rad * 0.4, 0.0, length(f - vec2(-0.04, 0.05))) * step(0.45, r); }
        gl_FragColor = vec4(vec3(0.5, 0.6, 0.7) * 0.2 + vec3(1.0) * sp * 0.5, clamp(a * 0.35 + 0.06, 0.0, 0.8)); }`,
  }));
  glass.position.set(0.2, 1.6, -2.07); g.add(glass);
  // cidade lá fora (luzes desfocadas)
  const c = TX.canvas(1024, 512), x = c.getContext('2d'); const r = rng(12);
  const gr = x.createLinearGradient(0, 0, 0, 512); gr.addColorStop(0, '#05080c'); gr.addColorStop(1, '#101820'); x.fillStyle = gr; x.fillRect(0, 0, 1024, 512);
  for (let i = 0; i < 70; i++) { x.fillStyle = `rgba(30,36,44,1)`; const w = r.range(40, 120), hh = r.range(80, 330); x.fillRect(r() * 1024, 512 - hh, w, hh); }
  for (let i = 0; i < 260; i++) { const cx = r() * 1024, cy = r.range(200, 500), rr = r.range(3, 14); const warm = r() < 0.6; const g2 = x.createRadialGradient(cx, cy, 0, cx, cy, rr); g2.addColorStop(0, warm ? 'rgba(255,190,110,0.9)' : 'rgba(160,200,255,0.8)'); g2.addColorStop(1, 'rgba(0,0,0,0)'); x.fillStyle = g2; x.beginPath(); x.arc(cx, cy, rr, 0, 7); x.fill(); }
  const ct = new THREE.CanvasTexture(c); ct.colorSpace = THREE.SRGBColorSpace;
  const city = new THREE.Mesh(new THREE.PlaneGeometry(14, 7), new THREE.MeshBasicMaterial({ map: ct, color: 0x9aa0a8, fog: false }));
  city.position.set(0.5, 1.2, -8); g.add(city);
  const rain = makeRain({ count: 3000, box: [4, 4, 3], speed: 12, bright: 0.3 }); rain.material.uniforms.camPos = { value: new THREE.Vector3(0.2, 1.6, -3.6) }; g.add(rain);
  // mesa, notebook, luminária, cadeira, cama, estante
  const desk = P.table({ w: 1.5, d: 0.7, h: 0.75, round: false }); desk.position.set(0.2, 0, -1.65); g.add(desk);
  const lap = new THREE.Group(); lap.position.set(0.05, 0.775, -1.55);
  lap.add(new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.015, 0.24), new THREE.MeshStandardMaterial({ color: 0x2a2c30, metalness: 0.6, roughness: 0.4 })));
  const lid = new THREE.Group(); lid.position.set(0, 0.008, -0.12); lid.rotation.x = -0.25; lap.add(lid);
  lid.add(new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.23, 0.01).translate(0, 0.115, 0), new THREE.MeshStandardMaterial({ color: 0x2a2c30, metalness: 0.6, roughness: 0.4 })));
  const scr = new THREE.Mesh(new THREE.PlaneGeometry(0.31, 0.2), new THREE.MeshBasicMaterial({ color: new THREE.Color(0x6a8ab0).multiplyScalar(1.4) })); scr.position.set(0, 0.115, 0.006); lid.add(scr);
  g.add(lap);
  const lapLight = new THREE.RectAreaLight ? new THREE.PointLight(0x7090c0, 1.2, 2.5, 2) : null; lapLight.position.set(0.05, 1.0, -1.4); g.add(lapLight);
  const lamp = new THREE.Group(); lamp.position.set(-0.4, 0.775, -1.85);
  lamp.add(new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.07, 0.02, 16), P.M.iron));
  lamp.add(new THREE.Mesh(new THREE.CylinderGeometry(0.008, 0.008, 0.38, 6).translate(0, 0.19, 0), P.M.iron));
  const shade = new THREE.Mesh(new THREE.ConeGeometry(0.1, 0.12, 16, 1, true).translate(0, 0.4, 0), new THREE.MeshStandardMaterial({ color: 0x3a3020, side: THREE.DoubleSide })); lamp.add(shade);
  g.add(lamp);
  const lampL = new THREE.SpotLight(0xffb070, 3.5, 4, 0.9, 0.7, 1.5); lampL.position.set(-0.4, 1.12, -1.85); lampL.target.position.set(-0.1, 0.7, -1.4); lampL.castShadow = true; lampL.shadow.mapSize.set(1024, 1024); g.add(lampL, lampL.target);
  const chair = new THREE.Group(); chair.position.set(0.2, 0, -0.85);
  chair.add(new THREE.Mesh(new THREE.BoxGeometry(0.48, 0.05, 0.46).translate(0, 0.46, 0), P.M.darkWood));
  chair.add(new THREE.Mesh(new THREE.BoxGeometry(0.46, 0.5, 0.04).translate(0, 0.74, 0.22), P.M.darkWood));
  for (const [a, b] of [[1, 1], [1, -1], [-1, 1], [-1, -1]]) chair.add(new THREE.Mesh(new THREE.BoxGeometry(0.035, 0.46, 0.035).translate(a * 0.21, 0.23, b * 0.2), P.M.darkWood));
  g.add(chair);
  const bed = new THREE.Mesh(new THREE.BoxGeometry(1.5, 0.45, 2.0).translate(0, 0.225, 0), P.M.sheet); bed.position.set(-1.45, 0, 0.9); g.add(bed);
  const shelf = P.sideboard({ w: 1.2, h: 1.8, d: 0.35 }); shelf.position.set(2.05, 0, 0.4); shelf.rotation.y = -Math.PI / 2; g.add(shelf);
  for (let i = 0; i < 9; i++) { const b = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.26, 0.04 + (i % 3) * 0.01), new THREE.MeshStandardMaterial({ color: [0x5a3030, 0x30405a, 0x5a5a40][i % 3], roughness: 0.8 })); b.position.set(1.98, 1.95, -0.1 + i * 0.07); b.rotation.y = Math.PI / 2; g.add(b); }
  scene.add(new THREE.HemisphereLight(0x5a6a80, 0x1a1410, 0.25));
  const cityL = new THREE.DirectionalLight(0x7a8ca8, 0.6); cityL.position.set(0.4, 2.2, -6); cityL.target.position.set(0, 0.8, 0); scene.add(cityL, cityL.target);
  const phone = new THREE.Group();
  g.traverse(o => { if (o.isMesh && !o.material.transparent) { o.castShadow = true; o.receiveShadow = true; } });
  room.castShadow = false;
  return { scene, lap, lampL };
}

const apartment = {
  async create(ctx) {
    const A = buildApartment();
    const gab = getGabriel(ctx);
    const cam = makeCamera(35);
    const deskPhone = new THREE.Vector3(0.48, 0.775, -1.3);
    return {
      update(lt, t) {
        if (gab.root.parent !== A.scene) A.scene.add(gab.root);
        G.lightning.value = 0;
        gab.resetPose();
        // sentado à mesa, de frente para a janela (-z)
        gab.root.position.set(0.2, 0, -0.88); gab.root.rotation.y = Math.PI;
        gab.idle(t, { fear: 0 });
        gab.j.hips.position.y = 0.52; gab.j.hips.position.z = 0.0;
        for (const s of ['L', 'R']) { gab.j['thigh' + s].rotation.x = -1.5; gab.j['knee' + s].rotation.x = 1.45; gab.j['ankle' + s].rotation.x = 0.05; }
        gab.j.spine.rotation.x = 0.12; gab.j.chest.rotation.x = 0.06;
        gab.root.updateMatrixWorld(true);
        gab.reach('L', new THREE.Vector3(-0.08, 0.8, -1.38), new THREE.Vector3(-0.6, 0.6, -0.6));
        gab.setGrip('L', 0.35);
        const pick = L('apartment', EVENTS.phoneAnswer);
        const k = smooth(pick, pick + 1.0, lt);
        if (lt < pick) {
          gab.reach('R', new THREE.Vector3(0.36, 0.82, -1.3), new THREE.Vector3(0.9, 0.5, -0.6));
          gab.setGrip('R', 0.35);
          gab.phone.visible = false;
        } else {
          phoneToEar(gab, k);
        }
        // celular sobre a mesa vibrando antes de atender
        const buzz = EVENTS.phoneBuzz.some(b => t > b && t < b + 0.9) ? 1 : 0;
        if (lt < pick + 0.15) {
          if (gab.phone.parent !== A.scene) { A.scene.add(gab.phone); }
          gab.phone.visible = true;
          gab.phone.position.copy(deskPhone).add(new THREE.Vector3(Math.sin(t * 90) * 0.002 * buzz, 0.006, 0));
          gab.phone.rotation.set(-Math.PI / 2, 0, 0.3 + Math.sin(t * 70) * 0.01 * buzz);
          phoneScreen(gab, lt > 0.7 ? 'call' : 'off', t);
        } else {
          gab.attachPhone('ear');
          phoneScreen(gab, 'incall', lt - pick);
        }
        // rosto: escuta, estranhamento; fala G1
        const lp = lips(ctx, 'gabriel', t, gab);
        const puzzled = smooth(10, 12, lt) * 0.6;
        const C = cut(lt, [[0, 'wide'], [2.6, 'phone'], [4.4, 'mcu'], [13.6, 'cu'], [17.4, 'side']]);
        const look = C.id === 'cu' ? new THREE.Vector3(0.6, 1.3, -1.6) : new THREE.Vector3(0.0, 1.0, -1.6);
        gab.look(look.clone().add(new THREE.Vector3(Math.sin(lt * 0.4) * 0.1, 0, 0)), 0.7);
        gab.updateFace(t, { jaw: lp.jaw, round: lp.round, brow: puzzled + (lt > 18 ? 0.4 : 0), eyesWide: lt > 18.5 ? 0.25 : 0 });
        let ap = 0, focus = 2;
        if (C.id === 'wide') camPath(cam, C.lt, [[0, [-1.7, 1.55, 1.3], [0.2, 1.0, -1.7]], [2.6, [-1.55, 1.5, 1.05], [0.2, 1.0, -1.7]]], { shake: 0.01 });
        else if (C.id === 'phone') { camPath(cam, C.lt, [[0, [0.75, 1.05, -0.95], [0.45, 0.78, -1.3]], [1.8, [0.68, 1.0, -1.0], [0.42, 0.8, -1.3]]], { shake: 0.004 }); ap = 6; focus = 0.45; }
        else if (C.id === 'mcu') { camPath(cam, C.lt, [[0, [-0.35, 1.3, -1.75], [0.2, 1.28, -0.85]], [9.2, [-0.25, 1.3, -1.6], [0.2, 1.3, -0.85]]], { shake: 0.006 }); ap = 4; focus = cam.position.distanceTo(new THREE.Vector3(0.2, 1.3, -0.85)); }
        else if (C.id === 'cu') { camPath(cam, C.lt, [[0, [0.0, 1.3, -1.75], [0.2, 1.3, -0.85]], [3.8, [0.03, 1.3, -1.65], [0.2, 1.3, -0.85]]], { shake: 0.004 }); ap = 5; focus = 0.85; }
        else { camPath(cam, C.lt, [[0, [1.1, 1.35, -1.75], [0.15, 1.25, -0.9]], [6.6, [0.85, 1.32, -1.6], [0.15, 1.27, -0.9]]], { shake: 0.006 }); ap = 3; focus = 1.1; }
        cam.fov = { wide: 40, phone: 30, mcu: 30, cu: 28, side: 32 }[C.id]; cam.updateProjectionMatrix();
        const fade = smooth(23.2, 24, lt);
        return { scene: A.scene, camera: cam, aperture: ap, focus, bloom: 0.5, grade: { fade, sat: 0.72 } };
      },
    };
  },
};

// ---------------- CARRO ----------------
export function carWorld(ctx) {
  if (ctx.sets.carw) return ctx.sets.carw;
  const ex = exteriorWorld(ctx);
  const car = buildCar(); ex.scene.add(car);
  const inside = buildCarInterior(); car.add(inside);
  // luz interna do painel no rosto
  const dashL = new THREE.PointLight(0x5a90b0, 1.6, 1.6, 2); dashL.position.set(0.35, 1.05, 0.2); car.add(dashL);
  const radioL = new THREE.PointLight(0x70d090, 0, 1.2, 2); radioL.position.set(0.1, 1.05, 0.2); car.add(radioL);
  const rimL = new THREE.SpotLight(0x8aa4c8, 25, 14, 0.7, 0.5, 1.5); rimL.position.set(-4, 5, -4); rimL.target.position.set(0, 0.8, 0); car.add(rimL, rimL.target);
  const W = { ex, car, inside, iu: inside.userData, cu: car.userData, dashL, radioL };
  ctx.sets.carw = W;
  return W;
}

// posição do carro na estrada (z do mundo) -> pose
function placeCar(W, z, { lane = 1.5, stopAtHouse = false } = {}) {
  const x = roadX(z) + lane;
  const y = terrainY(x, z) + 0.02;
  const z2 = z - 1.5, x2 = roadX(z2) + lane, y2 = terrainY(x2, z2) + 0.02;
  W.car.position.set(x, y, z);
  W.car.lookAt(x2, y2, z2);
  W.car.updateMatrixWorld(true);
}

function carCommon(W, t, lt, { headOn = 1, wipers = 1, speed = 8 } = {}) {
  const cu = W.cu;
  cu.head.intensity = 60 * headOn;
  cu.cones.forEach(c => c.material.uniforms.intensity.value = 0.05 * headOn);
  cu.headMat.color.setRGB(4 * headOn + 0.05, 3.8 * headOn + 0.05, 3.4 * headOn + 0.05);
  cu.wheels.forEach(w => w.rotation.x = -t * speed / 0.33);
  const a = wiperAngle(t, wipers);
  cu.wipers.forEach(p => { p.rotation.set(-1.1, 0, -1.35 + a * 1.75); });
  W.iu.wipers.forEach(p => { p.rotation.set(-1.1, 0, -1.4 + a * 1.75); });
  W.iu.ws.material.uniforms.wiperOn.value = wipers;
  // a chuva ilumina-se no feixe dos faróis
  const ru = W.ex.rain.material.uniforms;
  W.car.localToWorld(ru.spotPos.value.set(0, 0.75, 2.3));
  ru.spotDir.value.set(0, -0.05, 1).transformDirection(W.car.matrixWorld);
  ru.spotCos.value = Math.cos(0.42); ru.spotInt.value = 2.2 * headOn;
}

function seatGabriel(gab, W, t, { lookRadio = 0 } = {}) {
  if (gab.root.parent !== W.car) W.car.add(gab.root);
  gab.resetPose();
  gab.root.position.set(0.4, 0.0, -0.42); gab.root.rotation.set(0, 0, 0);
  gab.idle(t, { fear: 0.2 });
  gab.j.hips.position.y = 0.6;
  for (const s of ['L', 'R']) { gab.j['thigh' + s].rotation.x = -1.35; gab.j['knee' + s].rotation.x = 1.15; }
  gab.j.spine.rotation.x = -0.12; gab.j.chest.rotation.x = 0.05;
  W.car.updateMatrixWorld(true);
  const wc = new THREE.Vector3(0.4, 0.98, 0.32);
  const steer = fbm1(t * 0.3, 9) * 0.12;
  const hl = W.car.localToWorld(wc.clone().add(new THREE.Vector3(0.17 * Math.cos(steer) , 0.08 + 0.17 * Math.sin(steer), -0.04)));
  const hr = W.car.localToWorld(wc.clone().add(new THREE.Vector3(-0.17 * Math.cos(steer), 0.08 - 0.17 * Math.sin(steer), -0.04)));
  gab.reach('L', hl, W.car.localToWorld(new THREE.Vector3(1.0, 0.4, -0.2)));
  gab.reach('R', hr, W.car.localToWorld(new THREE.Vector3(-0.3, 0.4, -0.2)));
  gab.setGrip('L', 0.85); gab.setGrip('R', 0.85);
  W.iu.wheel.rotation.z = steer;
  const ahead = W.car.localToWorld(new THREE.Vector3(0.4 + fbm1(t * 0.2, 2) * 0.3, 1.2, 8));
  const radio = W.car.localToWorld(new THREE.Vector3(0.0, 0.86, 0.46));
  gab.look(ahead.lerp(radio, lookRadio), 1);
}

const carExterior = {
  async create(ctx) {
    const W = carWorld(ctx);
    const gab = getGabriel(ctx);
    const cam = makeCamera(34);
    return {
      update(lt, t) {
        G.rain.value = 1;
        updateExterior(W.ex, t);
        W.car.visible = true; W.inside.visible = true;
        const z = -140 - lt * 8;
        placeCar(W, z);
        carCommon(W, t, lt);
        seatGabriel(gab, W, t);
        W.ex.rain.material.uniforms.clearR.value = 0;
        // câmera baixa à frente, recuando enquanto o carro se aproxima; depois passa ao lado
        const C = cut(lt, [[0, 'front'], [3.6, 'side']]);
        if (C.id === 'front') {
          const p = W.car.localToWorld(new THREE.Vector3(-2.6, 0.5, 9 - C.lt * 0.8));
          const l = W.car.localToWorld(new THREE.Vector3(0, 0.9, 0));
          placeCamera(cam, p.toArray(), l.toArray(), { shake: 0.03, t, fov: 30 });
        } else {
          const p = new THREE.Vector3(roadX(-178) - 4.5, terrainY(roadX(-178) - 4.5, -178) + 1.0, -178);
          const l = W.car.localToWorld(new THREE.Vector3(0, 0.8, 0.5));
          placeCamera(cam, p.toArray(), l.toArray(), { shake: 0.02, t, fov: 34 });
        }
        return { scene: W.ex.scene, camera: cam, bloom: 0.8, bloomThreshold: 0.75, grade: { exposure: 1.6 } };
      },
    };
  },
};

const carInterior = {
  async create(ctx) {
    const W = carWorld(ctx);
    const gab = getGabriel(ctx);
    const cam = makeCamera(40);
    return {
      update(lt, t) {
        G.rain.value = 1;
        updateExterior(W.ex, t);
        const z = -196 - lt * 8;
        placeCar(W, z);
        carCommon(W, t, lt);
        const sOn = L('car_interior', EVENTS.radioStaticOn), sOff = L('car_interior', EVENTS.radioStaticOff), look = L('car_interior', EVENTS.radioLook);
        const staticOn = lt > sOn && lt < sOff;
        drawRadio(W.iu, staticOn ? 'static' : 'normal', t);
        W.radioL.intensity = staticOn ? 0.5 + 0.4 * Math.abs(noise1(t * 20, 5)) : 0.12;
        const lookRadio = smooth(look - 0.3, look + 0.8, lt) * (1 - smooth(sOff + 0.8, sOff + 2.0, lt));
        seatGabriel(gab, W, t, { lookRadio });
        W.car.updateMatrixWorld(true);
        W.ex.rain.material.uniforms.clearPos.value.copy(W.car.localToWorld(new THREE.Vector3(0, 0.9, -0.3)));
        W.ex.rain.material.uniforms.clearR.value = 1.35;
        const fear = smooth(look, look + 1, lt) * 0.8;
        gab.updateFace(t, { ...fearFace(fear), squint: (1 - fear) * 0.15 });
        const C = cut(lt, [[0, 'pass'], [5.2, 'radio'], [10.6, 'face'], [14.6, 'hood']]);
        let ap = 0, focus = 1;
        const cw = (v) => W.car.localToWorld(new THREE.Vector3(...v)).toArray();
        if (C.id === 'pass') { placeCamera(cam, cw([-0.45, 1.22, -0.25 + C.lt * 0.02]), cw([0.42, 1.18, 0.1]), { shake: 0.012, t, fov: 46 }); ap = 2; focus = 0.85; }
        else if (C.id === 'radio') { placeCamera(cam, cw([-0.12 + C.lt * 0.01, 1.02, 0.18 + C.lt * 0.012]), cw([0.0, 0.86, 0.46]), { shake: 0.006, t, fov: 32 }); ap = 7; focus = 0.32; }
        else if (C.id === 'face') { placeCamera(cam, cw([0.02, 1.18, 0.12]), cw([0.4, 1.3, -0.4]), { shake: 0.008, t, fov: 38 }); ap = 4; focus = 0.65; }
        else { placeCamera(cam, cw([-0.15, 1.32, -1.25 + C.lt * 0.04]), cw([0.25, 1.12, 2.0]), { shake: 0.012, t, fov: 44 }); ap = 1.5; focus = 1.0; }
        return { scene: W.ex.scene, camera: cam, aperture: ap, focus, bloom: 0.7, bloomThreshold: 0.7, grade: { exposure: 1.6 } };
      },
    };
  },
};

// ---------------- CHEGADA ----------------
const H2W = (x, y, z) => new THREE.Vector3(x, HOUSE_Y + y, HOUSE_Z + z);

const arrival = {
  async create(ctx) {
    const W = carWorld(ctx);
    const gab = getGabriel(ctx);
    const cam = makeCamera(34);
    return {
      update(lt, t) {
        updateExterior(W.ex, t);
        // a chuva para de repente quando ele chega — silêncio
        const rs = L('arrival', EVENTS.rainStop);
        G.rain.value = 1 - smooth(rs - 0.4, rs + 1.2, lt);
        W.ex.rain.material.uniforms.clearR.value = 0;
        const stop = L('arrival', EVENTS.carStop);
        const zStop = HOUSE_Z + 16.8;
        const z = lt < stop ? zStop + 42 * Math.pow(1 - lt / stop, 1.7) : zStop;
        const lane = 1.5 + smooth(0, stop, lt) * 0.2;
        placeCar(W, z, { lane });
        const headOn = 1 - smooth(8.2, 8.4, lt);
        carCommon(W, t, lt, { headOn, wipers: lt < 8 ? 1 : 0, speed: lt < stop ? 8 * (1 - lt / stop) : 0 });
        const out = lt > 10.6;
        if (!out) { seatGabriel(gab, W, t); gab.updateFace(t, { squint: 0.1 }); }
        else {
          if (gab.root.parent !== W.ex.scene) W.ex.scene.add(gab.root);
          const s = applyWalk(gab, 'arrive', t, { offset: [0, HOUSE_Y, HOUSE_Z], fear: 0.2 });
          gab.root.position.y = terrainY(gab.root.position.x, gab.root.position.z) + 0.02;
          if (!s.moving && s.u === 0) gab.root.rotation.y = Math.PI * 1.02;
          gab.look(H2W(-3.5, 6.5, 0), 0.9, { maxPitch: 0.7 });
          gab.updateFace(t, { brow: 0.2 });
          gab.phone.visible = false;
        }
        const C = cut(lt, [[0, 'garden'], [5.4, 'stop'], [10.6, 'behind']]);
        if (C.id === 'garden') {
          // jardim abandonado, câmera baixa entre árvores secas; faróis surgem pelo portão
          camPath(cam, C.lt, [[0, H2W(-5.0, 1.9, 13.5).toArray(), H2W(1, 1.2, 26).toArray()], [5.4, H2W(-4.6, 2.0, 12.6).toArray(), H2W(2.2, 1.0, 20).toArray()]], { shake: 0.02, fov: 30 });
        } else if (C.id === 'stop') {
          camPath(cam, C.lt, [[0, H2W(7.5, 1.2, 21).toArray(), H2W(2.5, 1.0, 16).toArray()], [5.2, H2W(6.8, 1.3, 19.5).toArray(), H2W(1.5, 2.6, 10).toArray()]], { shake: 0.015, fov: 34 });
        } else {
          // atrás dele: o casarão se ergue
          camPath(cam, C.lt, [[0, H2W(2.4, 1.05, 18.8).toArray(), H2W(-0.5, 5.5, 0).toArray()], [3.4, H2W(2.2, 0.9, 17.8).toArray(), H2W(-1.0, 6.2, 0).toArray()]], { shake: 0.02, fov: 36 });
        }
        return { scene: W.ex.scene, camera: cam, bloom: 0.7, aperture: C.id === 'behind' ? 1.5 : 0, focus: 4, grade: { exposure: 1.7 } };
      },
    };
  },
};

const frontDoor = {
  async create(ctx) {
    const W = carWorld(ctx);
    const gab = getGabriel(ctx);
    const cam = makeCamera(34);
    const hu = W.ex.world.house.userData;
    const hw = W.ex.world.house;
    const key = new THREE.Group();
    const iron = new THREE.MeshStandardMaterial({ color: 0x3a3028, metalness: 0.9, roughness: 0.45 });
    key.add(new THREE.Mesh(new THREE.TorusGeometry(0.018, 0.005, 8, 20), iron));
    key.add(new THREE.Mesh(new THREE.CylinderGeometry(0.0035, 0.0035, 0.07, 8).translate(0, -0.05, 0), iron));
    key.add(new THREE.Mesh(new THREE.BoxGeometry(0.012, 0.01, 0.003).translate(0.006, -0.08, 0), iron));
    const porchLight = new THREE.PointLight(0x9ab0c8, 0, 9, 1.6);
    return {
      update(lt, t) {
        updateExterior(W.ex, t);
        G.rain.value = 0;
        W.car.position.set(0, -50, 0); // fora de cena
        if (gab.root.parent !== W.ex.scene) W.ex.scene.add(gab.root);
        if (porchLight.parent !== W.ex.scene) W.ex.scene.add(porchLight);
        porchLight.position.copy(H2W(1.4, 2.4, 9.4)); porchLight.intensity = 6;
        const s = applyWalk(gab, 'porch', t, { offset: [0, HOUSE_Y, HOUSE_Z], fear: 0.35 });
        // degraus: sobe até a varanda (y 0.8)
        const zl = gab.root.position.z - HOUSE_Z;
        const yl = zl > 10.3 ? 0 : zl > 9.1 ? (10.3 - zl) / 1.2 * 0.8 : 0.8;
        gab.root.position.y = HOUSE_Y + yl;
        if (!s.moving && s.u >= 1) gab.root.rotation.y = Math.PI;
        const kIn = L('front_door', EVENTS.keyInsert), kTurn = L('front_door', EVENTS.keyTurn), dOpen = L('front_door', EVENTS.doorOpen);
        // chave: mão direita vai à fechadura
        const lock = new THREE.Vector3(); hu.keyhole.getWorldPosition(lock);
        const reachK = smooth(kIn - 1.2, kIn, lt) * (1 - smooth(dOpen + 0.6, dOpen + 1.6, lt));
        if (lt > kIn - 1.4) {
          const tgt = lock.clone().add(new THREE.Vector3(0.02, -0.03, 0.12 - reachK * 0.04));
          const rest = gab.root.localToWorld(new THREE.Vector3(-0.22, 0.95, 0.15));
          gab.reach('R', rest.lerp(tgt, reachK), gab.root.localToWorld(new THREE.Vector3(-0.5, 0.6, -0.2)));
          gab.setGrip('R', 0.7, 0.9);
        }
        if (lt > kIn - 0.3 && lt < dOpen + 1.2) {
          if (key.parent !== W.ex.scene) W.ex.scene.add(key);
          const ins = smooth(kIn - 0.3, kIn, lt);
          key.position.copy(lock).add(new THREE.Vector3(0, 0.08, 0.05 * (1 - ins) + 0.015));
          key.rotation.set(Math.PI / 2 - 0.2, 0, smooth(kTurn, kTurn + 0.5, lt) * Math.PI / 2);
          key.visible = true;
        } else key.visible = false;
        // porta abre devagar (range longo)
        hu.doorL.rotation.y = -kf(lt, [[dOpen, 0], [dOpen + 0.5, 0.05], [dOpen + 1.0, 0.12], [dOpen + 4.2, 1.25]], ease.inOut);
        gab.look(H2W(0, 1.6, 5).add(new THREE.Vector3(0, 0, -3)), 0.8);
        gab.updateFace(t, { brow: 0.3, eyesWide: lt > dOpen ? 0.3 : 0 });
        const C = cut(lt, [[0, 'porch'], [3.6, 'key'], [6.0, 'door']]);
        let ap = 0, focus = 3;
        if (C.id === 'porch') camPath(cam, C.lt, [[0, H2W(-2.2, 1.6, 7.2).toArray(), H2W(0.9, 1.3, 12).toArray()], [3.6, H2W(-2.0, 1.8, 7.0).toArray(), H2W(0.4, 1.5, 9).toArray()]], { shake: 0.012, fov: 34 });
        else if (C.id === 'key') { const l = lock.clone(); placeCamera(cam, l.clone().add(new THREE.Vector3(-0.32, 0.1, 0.3)).toArray(), l.toArray(), { shake: 0.003, t, fov: 30 }); ap = 6; focus = 0.44; }
        else { camPath(cam, C.lt, [[0, H2W(1.3, 1.9, 11.0).toArray(), H2W(0, 1.6, 4).toArray()], [5, H2W(1.0, 1.85, 10.2).toArray(), H2W(0, 1.5, 3).toArray()]], { shake: 0.01, fov: 34 }); ap = 1.5; focus = 3.2; }
        return { scene: W.ex.scene, camera: cam, aperture: ap, focus, bloom: 0.6, grade: { exposure: 1.7 } };
      },
    };
  },
};

export const shots = { apartment, car_exterior: carExterior, car_interior: carInterior, arrival, front_door: frontDoor };
