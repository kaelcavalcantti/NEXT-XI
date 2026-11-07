// Dentro da casa (I): a casa respira, o álbum e a fotografia, 23h, a escada, o quarto e as cartas, as batidas.
import * as THREE from 'three';
import { EVENTS, shotById } from '../timeline.js';
import { makeCamera, placeCamera, camPath } from '../engine/scene.js';
import { kf, smooth, ease, noise1, fbm1, clamp, lerp, pulse } from '../lib/util.js';
import * as TX from '../engine/tex.js';
import { interiorWorld, updateInterior, coneGuard } from './interior_world.js';
import { cut, lips, applyWalk, holdFlashlight, fearFace } from './common.js';
import { walkById, walkAt } from '../choreo.js';
import { Human } from '../char/human.js';
import { windowMesh } from '../sets/kit.js';
import { letterCanvas } from '../sets/props.js';
import { UP } from '../sets/interior.js';

const L = (id, ev) => ev - shotById[id].start;
const V = (x, y, z) => new THREE.Vector3(x, y, z);

// respiração da casa (envelope 0..1 com ciclo ~4.2s) — o áudio usa os mesmos tempos
export function houseBreath(t) {
  const on = EVENTS.breathOn.some((b, i) => t > b && t < (i === 0 ? EVENTS.breathOff : b + 30));
  return on ? 0.5 - 0.5 * Math.cos(2 * Math.PI * (t - 0.0) / 4.2) : 0;
}

function headPos(h) { const p = new THREE.Vector3(); h.j.head.getWorldPosition(p); return p.add(V(0, 0.08, 0)); }

// ---------------- CORREDOR ----------------
const hallway = {
  async create(ctx) {
    const W = interiorWorld(ctx);
    const cam = makeCamera(36);
    return {
      update(lt, t) {
        const g = W.gab; const ud = W.ud;
        ud.frontDoor.rotation.y = 1.35;
        W.dbl.root.visible = false;
        const id = lt < 6.2 ? 'enter' : lt < 15 ? 'corr1' : 'corr2';
        const s = applyWalk(g, id, t, { fear: 0.45 });
        const fOn = L('hallway', EVENTS.flashlightOn);
        const flashOn = smooth(fOn, fOn + 0.12, lt);
        // mira: à frente, varrendo as paredes com as fotografias
        const fwd = V(Math.sin(s.heading), 0, Math.cos(s.heading));
        const side = V(fwd.z, 0, -fwd.x);
        const sweep = Math.sin(lt * 0.55) * 0.9 + (lt > 11.8 && lt < 15 ? Math.sin((lt - 11.8) * 1.2) * 1.4 : 0);
        const aim = V(s.x, 1.35, s.z).addScaledVector(fwd, 3).addScaledVector(side, sweep);
        g.look(aim.clone().add(V(0, 0.3, 0)), 0.8);
        if (lt > fOn - 0.6) holdFlashlight(g, aim, { raise: -0.05 * (1 - flashOn) });
        W.flashAim = aim;
        const stopT = L('hallway', EVENTS.stopWalk);
        const listening = smooth(stopT, stopT + 0.5, lt) * (1 - smooth(14.6, 15.2, lt));
        g.updateFace(t, { ...fearFace(0.35 + 0.5 * listening), squint: 0 });
        updateInterior(W, t, { flashOn, coneInt: 0.05 });
        const C = cut(lt, [[0, 'door'], [5.4, 'follow'], [11.8, 'face'], [15.0, 'far'], [19.5, 'living']]);
        let ap = 0, focus = 3; const hp = headPos(g);
        if (C.id === 'door') camPath(cam, C.lt, [[0, [-2.2, 1.45, -5.4], [0.2, 1.3, -0.2]], [5.4, [-1.6, 1.5, -5.0], [1.2, 1.3, -1.6]]], { shake: 0.01, fov: 38 });
        else if (C.id === 'follow') { const p = hp.clone().addScaledVector(fwd, -0.95).addScaledVector(side, -0.25).add(V(0, 0.08, 0)); placeCamera(cam, p.toArray(), hp.clone().addScaledVector(fwd, 3).toArray(), { shake: 0.02, t, fov: 40 }); ap = 1.5; focus = 1.0; }
        else if (C.id === 'face') { const p = hp.clone().addScaledVector(fwd, 0.75).addScaledVector(side, 0.12); placeCamera(cam, p.toArray(), hp.toArray(), { shake: 0.006, t, fov: 32 }); ap = 5; focus = 0.75; }
        else if (C.id === 'far') { camPath(cam, C.lt, [[0, [11.6, 1.55, -2.4], [7.6, 1.4, -2.4]], [4.5, [11.3, 1.5, -2.45], [9.0, 1.4, -2.4]]], { shake: 0.006, fov: 34 }); ap = 1.2; focus = cam.position.distanceTo(hp); }
        else camPath(cam, C.lt, [[0, [17.3, 1.9, -6.3], [12.5, 1.2, -2.4]], [3.5, [17.0, 1.85, -6.0], [13.4, 1.2, -2.8]]], { shake: 0.008, fov: 40 });
        // "a casa respira": a lente pulsa com a respiração das paredes
        const br = houseBreath(t);
        cam.fov *= 1 - br * 0.018; cam.updateProjectionMatrix();
        coneGuard(W, cam);
        return { scene: W.scene, camera: cam, aperture: ap, focus, bloom: 0.6, grade: { exposure: 1.9 } };
      },
    };
  },
};

// ---------------- ÁLBUM ----------------
function boyPhotoTexture(ctx) {
  // fotografia de 1994: um menino na janela do segundo andar — com o rosto de Gabriel
  const scene = new THREE.Scene(); scene.background = new THREE.Color(0x1a1612);
  const boy = new Human({ seed: 1, jacket: 0x5a5048, shirt: 0x8a8278, stubble: 0 });
  boy.root.scale.setScalar(0.66); boy.headMesh.scale.multiplyScalar(1.22);
  boy.resetPose(); boy.idle(0.3); boy.armsDown(0);
  scene.add(boy.root);
  const cam = new THREE.PerspectiveCamera(30, 0.8, 0.05, 50);
  cam.position.set(0.12, 1.1, 2.0); cam.lookAt(0, 1.0, 0); cam.fov = 24; cam.updateProjectionMatrix();
  boy.root.updateMatrixWorld(true);
  boy.look(cam.position.clone(), 1); boy.updateFace(0, { blink: 0 });
  const win = windowMesh({ w: 1.1, h: 1.5, frame: 0xd8d0c0 }); win.position.set(0, 1.0, 0.6); win.children.slice(4, 6).forEach(c => c.visible = false); win.userData.pane.material = new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.05 }); scene.add(win);
  const wall = new THREE.Mesh(new THREE.PlaneGeometry(6, 4), new THREE.MeshStandardMaterial({ color: 0x8a8478, roughness: 1 }));
  const s = new THREE.Shape(); s.moveTo(-3, -1); s.lineTo(3, -1); s.lineTo(3, 3); s.lineTo(-3, 3);
  const h = new THREE.Path(); h.moveTo(-0.55, 0.25); h.lineTo(0.55, 0.25); h.lineTo(0.55, 1.75); h.lineTo(-0.55, 1.75); s.holes.push(h);
  const facade = new THREE.Mesh(new THREE.ShapeGeometry(s), new THREE.MeshStandardMaterial({ color: 0x9a968a, roughness: 1 })); facade.position.z = 0.62; scene.add(facade);
  const room = new THREE.Mesh(new THREE.BoxGeometry(4, 3, 3), new THREE.MeshStandardMaterial({ color: 0x3a342c, side: THREE.BackSide })); room.position.set(0, 1.4, -1); scene.add(room);
  const curt = new THREE.Mesh(new THREE.PlaneGeometry(0.45, 1.5), new THREE.MeshStandardMaterial({ color: 0xa09888, side: THREE.DoubleSide })); curt.position.set(-0.42, 1.0, 0.45); scene.add(curt);
  const sun = new THREE.DirectionalLight(0xfff4e0, 3.2); sun.position.set(0.6, 1.5, 3); scene.add(sun);
  scene.add(new THREE.HemisphereLight(0xdde4ea, 0x302820, 0.8));
  const rt = new THREE.WebGLRenderTarget(512, 640);
  const r = ctx.renderer; r.setRenderTarget(rt); r.render(scene, cam); r.setRenderTarget(null);
  return rt.texture;
}

function makePhoto(ctx) {
  const tex = boyPhotoTexture(ctx);
  const front = new THREE.ShaderMaterial({
    uniforms: { t: { value: tex } },
    vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }`,
    fragmentShader: /* glsl */`uniform sampler2D t; varying vec2 vUv;
      float h(vec2 p){ return fract(sin(dot(p, vec2(12.9898,78.233))) * 43758.5453); }
      void main(){
        vec2 uv = (vUv - 0.5) * 0.92 + 0.5;
        vec3 c = texture2D(t, uv).rgb;
        float l = dot(c, vec3(0.3, 0.59, 0.11));
        vec3 sep = vec3(l * 1.05, l * 0.9, l * 0.68) * 1.1 + vec3(0.06, 0.04, 0.02);
        sep += (h(vUv * 512.0) - 0.5) * 0.08;
        float v = smoothstep(0.75, 0.25, length(vUv - 0.5));
        sep *= 0.55 + 0.45 * v;
        float border = step(0.04, vUv.x) * step(vUv.x, 0.96) * step(0.04, vUv.y) * step(vUv.y, 0.96);
        vec3 col = mix(vec3(0.82, 0.78, 0.68), sep, border);
        float scratch = step(0.997, h(vec2(floor(vUv.x * 300.0), 1.0))) * 0.3;
        gl_FragColor = vec4(pow(col + scratch, vec3(2.2)), 1.0);
      }`,
  });
  const c = TX.canvas(512, 640), x = c.getContext('2d');
  x.fillStyle = '#cfc4a6'; x.fillRect(0, 0, 512, 640);
  for (let i = 0; i < 1500; i++) { x.fillStyle = `rgba(90,70,40,${Math.random() * 0.05})`; x.fillRect(Math.random() * 512, Math.random() * 640, 4, 4); }
  x.fillStyle = '#1c1a28'; x.font = '84px Hand'; x.textAlign = 'center';
  x.save(); x.translate(256, 320); x.rotate(-0.06); x.fillText('Gabriel — 1994', 0, 0); x.restore();
  x.font = '30px Typewriter'; x.fillStyle = 'rgba(80,60,40,0.6)'; x.fillText('FOTO ÓTICA PEDRA FUNDA', 256, 600);
  const bt = new THREE.CanvasTexture(c); bt.colorSpace = THREE.SRGBColorSpace;
  const g = new THREE.Group();
  const f = new THREE.Mesh(new THREE.PlaneGeometry(0.1, 0.125), front); g.add(f);
  const b = new THREE.Mesh(new THREE.PlaneGeometry(0.1, 0.125), new THREE.MeshStandardMaterial({ map: bt, roughness: 0.9 })); b.rotation.y = Math.PI; b.position.z = -0.0005; g.add(b);
  return g;
}

const album = {
  async create(ctx) {
    const W = interiorWorld(ctx);
    const cam = makeCamera(36);
    const photo = makePhoto(ctx);
    W.scene.add(photo);
    const au = W.ud.album.userData;
    return {
      update(lt, t) {
        const g = W.gab;
        W.dbl.root.visible = false; photo.visible = true;
        const s = applyWalk(g, 'toTable', t, { fear: 0.4 });
        if (!s.moving && s.u >= 1) g.root.rotation.y = Math.PI;
        g.j.spine.rotation.x += 0.18 * smooth(1.5, 2.5, lt); g.j.neck.rotation.x += 0.15;
        // álbum: capa abre, páginas viram
        const ao = L('album', EVENTS.albumOpen);
        au.frontPivot.rotation.z = -kf(lt, [[ao, 0], [ao + 0.9, Math.PI * 0.98]], ease.inOut);
        EVENTS.pageFlips.forEach((pt, i) => { const p = L('album', pt); au.pages[i].rotation.z = -kf(lt, [[p, 0], [p + 0.7, Math.PI * 0.97]], ease.inOut2) + i * 0.0; });
        const albumPos = new THREE.Vector3(); W.ud.album.getWorldPosition(albumPos);
        const pageR = albumPos.clone().add(V(-0.16, 0.07, 0.0)); // página da direita (vista dele)
        const found = L('album', EVENTS.photoFound), turn = L('album', EVENTS.photoTurn);
        // mão esquerda: vira páginas; depois pega a foto e a ergue
        g.root.updateMatrixWorld(true);
        const hp = headPos(g);
        const holdPos = hp.clone().add(V(0.02, -0.26, -0.34));
        const pick = smooth(found, found + 1.0, lt);
        const flipPhase = EVENTS.pageFlips.map(p => L('album', p)).reduce((acc, p) => acc + pulse(lt, p - 0.35, 0.3, 0.5), 0);
        let handT = pageR.clone().add(V(-0.08 + 0.12 * clamp(flipPhase), 0.03 + 0.04 * clamp(flipPhase), 0.08));
        if (lt < ao - 0.6) handT = g.root.localToWorld(V(0.25, 0.9, 0.1));
        handT.lerp(holdPos, pick);
        g.reach('L', handT, g.root.localToWorld(V(0.7, 0.6, -0.3)));
        g.setGrip('L', lerp(0.3, 0.75, pick), 0.9);
        // a foto: sobre a página até ser apanhada, depois na mão
        const onPage = pageR.clone().add(V(0, 0.002, 0));
        const inHand = holdPos.clone().add(V(0.0, -0.01, 0.05));
        photo.position.copy(onPage).lerp(inHand, pick);
        const turnA = smooth(turn, turn + 0.8, lt) * Math.PI;
        photo.rotation.set(lerp(-Math.PI / 2, -0.25, pick), turnA, 0);
        photo.visible = lt > EVENTS.pageFlips[3] - (t - lt) + 0.6;
        const aim = lt < found ? albumPos.clone() : inHand.clone();
        holdFlashlight(g, aim, { raise: 0.05 });
        W.flashAim = aim;
        const clockT = L('album', EVENTS.clockStart);
        const lookAt = lt > clockT + 0.4 ? V(15, 1.85, -6.6) : (lt < found ? albumPos : inHand);
        g.look(lookAt, 1);
        const lp = lips(ctx, 'gabriel', t, g);
        const shock = smooth(found + 2.0, found + 3.0, lt);
        g.updateFace(t, { ...fearFace(0.2 + 0.6 * shock), jaw: Math.max(lp.jaw, shock * 0.08), round: lp.round });
        // relógio parado (começa a funcionar no fim do plano)
        const gu = W.ud.gclock.userData; gu.set(11.0); gu.pend.rotation.z = lt > clockT ? 0.16 * Math.sin((lt - clockT) * Math.PI) * smooth(clockT, clockT + 0.5, lt) : 0.0;
        updateInterior(W, t, { flashOn: 1, coneInt: 0.04 });
        const C = cut(lt, [[0, 'arrive'], [2.4, 'overhead'], [10.4, 'pick'], [13.4, 'front'], [15.6, 'back'], [19.0, 'cu']]);
        let ap = 0, focus = 2;
        if (C.id === 'arrive') camPath(cam, C.lt, [[0, [15.3, 1.45, -5.2], [15.0, 1.3, -2.8]], [2.4, [15.2, 1.4, -5.0], [15.0, 1.15, -3.0]]], { shake: 0.006, fov: 36 });
        else if (C.id === 'overhead') { camPath(cam, C.lt, [[0, [15.35, 1.55, -3.9], [15.0, 0.78, -3.5]], [8, [15.25, 1.4, -3.85], [15.0, 0.78, -3.5]]], { shake: 0.004, fov: 40 }); ap = 4; focus = 0.85; }
        else if (C.id === 'pick') { camPath(cam, C.lt, [[0, [15.5, 1.15, -4.3], [15.0, 1.3, -2.9]], [3, [15.45, 1.2, -4.1], [15.0, 1.35, -2.9]]], { shake: 0.006, fov: 34 }); ap = 3; focus = 1.4; }
        g.root.visible = !(C.id === 'front' || C.id === 'back');
        if (C.id === 'front' || C.id === 'back') {
          const n = new THREE.Vector3(0, 0, 1).applyEuler(photo.rotation);
          if (C.id === 'back') n.negate();
          const d = C.id === 'front' ? lerp(0.24, 0.16, C.u) : 0.2;
          placeCamera(cam, photo.position.clone().addScaledVector(n.normalize(), d).add(V(0, 0.005, 0)).toArray(), photo.position.toArray(), { shake: 0.002, t, fov: 34 });
          ap = 6; focus = d;
          W.flash.intensity = 0.5; W.flash.angle = 0.7; W.flashCone.material.uniforms.intensity.value = 0;
        } else if (C.id === 'cu') { placeCamera(cam, hp.clone().add(V(0.1, -0.05, -0.55 + 0.12 * C.u)).toArray(), hp.clone().add(V(0, -0.02, 0)).toArray(), { shake: 0.004, t, fov: 32 }); ap = 5; focus = 0.5; }
        coneGuard(W, cam);
        return { scene: W.scene, camera: cam, aperture: ap, focus, bloom: 0.5, grade: { exposure: 1.9 } };
      },
    };
  },
};

// ---------------- RELÓGIO ----------------
const clock = {
  async create(ctx) {
    const W = interiorWorld(ctx);
    const cam = makeCamera(34);
    return {
      update(lt, t) {
        const g = W.gab; W.dbl.root.visible = false;
        g.resetPose(); g.idle(t, { fear: 0.6 });
        g.root.position.set(15.0, 0, -2.85); g.root.rotation.y = Math.PI + 0.05;
        const cp = V(15, 1.85, -6.6);
        g.look(cp, smooth(0, 1.2, lt) * 0.9 + 0.1);
        holdFlashlight(g, cp.clone().add(V(0, -0.4, 0)), {});
        W.flashAim = cp.clone().add(V(0, -0.3, 0));
        const gu = W.ud.gclock.userData; gu.set(11.0 + Math.max(0, lt - 0.5) / 3600);
        const clockStart = EVENTS.clockStart;
        gu.pend.rotation.z = 0.16 * Math.sin((t - clockStart) * Math.PI);
        const ch = EVENTS.clockChimes.reduce((a, c) => a + pulse(t, c, 0.01, 0.25), 0);
        g.updateFace(t, { ...fearFace(0.6 + 0.2 * ch) });
        updateInterior(W, t, { flashOn: 1, coneInt: 0.05 });
        const C = cut(lt, [[0, 'face'], [3.6, 'gabriel'], [6.6, 'low']]);
        let ap = 0, focus = 2;
        if (C.id === 'face') { camPath(cam, C.lt, [[0, [15.05, 1.85, -5.7], [15, 1.85, -6.63]], [3.6, [15.02, 1.85, -6.12], [15, 1.85, -6.63]]], { shake: 0.003, fov: 30 }); ap = 4; focus = cam.position.distanceTo(V(15, 1.85, -6.6)); }
        else if (C.id === 'gabriel') { const hp = headPos(g); camPath(cam, C.lt, [[0, [15.25, 1.6, -4.0], hp.toArray()], [3, [15.22, 1.62, -3.8], hp.toArray()]], { shake: 0.004, fov: 30 }); ap = 3; focus = cam.position.distanceTo(hp); }
        else camPath(cam, C.lt, [[0, [13.2, 0.35, -6.3], [15.2, 1.6, -4.5]], [3.4, [13.4, 0.4, -6.1], [15.2, 1.6, -4.5]]], { shake: 0.005, fov: 42 });
        // badalada: um leve tremor na imagem
        const sh = ch * 0.004; cam.position.x += sh * noise1(t * 40, 1); cam.position.y += sh * noise1(t * 40, 2);
        coneGuard(W, cam);
        return { scene: W.scene, camera: cam, aperture: ap, focus, bloom: 0.5, grade: { exposure: 1.9 } };
      },
    };
  },
};

// ---------------- ESCADA ----------------
const stairs = {
  async create(ctx) {
    const W = interiorWorld(ctx);
    const cam = makeCamera(38);
    return {
      update(lt, t) {
        const g = W.gab; W.dbl.root.visible = false;
        const id = lt < 5.0 ? 'toStairs' : lt < 10.4 ? 'climb' : 'upper';
        const s = applyWalk(g, id, t, { fear: 0.7 });
        if (id === 'toStairs' && s.u === 0) g.root.rotation.y = -Math.PI * 0.75;
        const thud = L('stairs', EVENTS.thudUpstairs);
        // lustre balança após a pancada no andar de cima
        W.ud.chand.rotation.z = 0.03 * Math.sin((lt - thud) * 2.4) * Math.exp(-(lt - thud) * 0.3) * (lt > thud ? 1 : 0);
        const fwd = V(Math.sin(s.heading), 0, Math.cos(s.heading));
        let aim;
        if (lt < 1.6) aim = V(-1.5, 4.5, -7.0); // olha para a galeria
        else if (id === 'climb') aim = V(-3.4, 4.0, -7.6);
        else aim = V(s.x, s.y + 1.3, s.z).addScaledVector(fwd, 3);
        g.look(aim, 1, { maxPitch: 0.8 });
        holdFlashlight(g, aim, {}); W.flashAim = aim;
        g.updateFace(t, { ...fearFace(0.75) });
        updateInterior(W, t, { flashOn: 1, coneInt: 0.06 });
        const C = cut(lt, [[0, 'look'], [1.6, 'cross'], [5.0, 'climb'], [10.4, 'upper']]);
        const hp = headPos(g);
        let ap = 0, focus = 2;
        if (C.id === 'look') { camPath(cam, C.lt, [[0, [2.0, 0.9, -1.6], [-0.6, 3.2, -6.5]], [1.6, [1.9, 0.95, -1.7], [-0.8, 3.4, -6.5]]], { shake: 0.006, fov: 40 }); }
        else if (C.id === 'cross') camPath(cam, C.lt, [[0, [0.5, 1.5, -4.6], hp.toArray()], [3.4, [-1.2, 1.6, -4.4], [-3.0, 1.4, -1.0]]], { shake: 0.01, fov: 40 });
        else if (C.id === 'climb') { const p = hp.clone().add(V(0.55, -0.35, 1.25)); placeCamera(cam, p.toArray(), hp.clone().add(V(-0.2, 0.3, -2)).toArray(), { shake: 0.02, t, fov: 44 }); ap = 1.2; focus = 1.4; }
        else { camPath(cam, C.lt, [[0, [-9.5, UP + 1.55, -7.35], hp.toArray()], [2.6, [-9.3, UP + 1.55, -7.3], hp.toArray()]], { shake: 0.006, fov: 36 }); ap = 1.0; focus = cam.position.distanceTo(hp); }
        coneGuard(W, cam);
        return { scene: W.scene, camera: cam, aperture: ap, focus, bloom: 0.6, grade: { exposure: 1.9 } };
      },
    };
  },
};

// ---------------- QUARTO + CARTA ----------------
const LETTER = ['Se você está lendo isso, significa que ela finalmente encontrou você.', '', 'Não confie nas vozes. Não responda quando ouvir seu nome.', '', 'E, acima de tudo, nunca abra a porta do porão.'];

function makeLetter() {
  const t = letterCanvas(LETTER);
  const m = new THREE.Mesh(new THREE.PlaneGeometry(0.21, 0.28), new THREE.MeshStandardMaterial({ map: t, roughness: 1, color: 0x9a9284, side: THREE.DoubleSide }));
  return m;
}

function roomPose(W, t, lt, letter, { reading = 1, tremble = 0 }) {
  const g = W.gab;
  const hp = headPos(g);
  const fwd = V(-1, 0, 0); // ele olha para a cama (-x)
  const read = hp.clone().addScaledVector(fwd, 0.36).add(V(0, -0.22, 0.04));
  letter.position.copy(read).add(V(0.003 * noise1(t * 30, 1) * tremble, 0.003 * noise1(t * 31, 2) * tremble, 0));
  letter.rotation.set(0, Math.PI / 2, 0); letter.rotateX(-0.45);
  const lh = read.clone().add(V(0.01, -0.13, -0.06));
  g.reach('L', lh, g.root.localToWorld(V(0.6, 0.7, -0.2)));
  g.setGrip('L', 0.6, 0.9);
  holdFlashlight(g, read.clone().add(V(0, -0.05, 0)), {});
  W.flashAim = read.clone().add(V(0, -0.06, 0));
  return read;
}

const childRoom = {
  async create(ctx) {
    const W = interiorWorld(ctx);
    const cam = makeCamera(36);
    const letter = makeLetter(); W.scene.add(letter);
    ctx.sets.letter = letter;
    return {
      update(lt, t) {
        const g = W.gab; W.dbl.root.visible = false;
        const s = applyWalk(g, 'intoRoom', t, { fear: 0.6 });
        if (!s.moving && s.u >= 1) g.root.rotation.y = -Math.PI / 2;
        W.ud.kidDoor.rotation.y = kf(lt, [[0.4, 0.55], [1.8, 1.45]], ease.out);
        const bo = L('child_room', EVENTS.boxOpen);
        W.ud.box.userData.lidPivot.rotation.x = -kf(lt, [[bo, 0], [bo + 1.2, 1.9]], ease.inOut);
        const take = smooth(bo + 1.2, bo + 2.6, lt);
        const boxPos = new THREE.Vector3(); W.ud.box.getWorldPosition(boxPos);
        let read = null;
        if (lt < 5.4) {
          const fwd = V(Math.sin(s.heading), 0, Math.cos(s.heading));
          const aim = V(s.x, UP + 1.2, s.z).addScaledVector(fwd, 3);
          g.look(aim, 0.9); holdFlashlight(g, aim, {}); W.flashAim = aim; letter.visible = false;
        } else if (lt < bo + 1.2) {
          // varre os desenhos com a lanterna
          const targets = [V(-13.0, UP + 1.5, -10.95), V(-14.95, UP + 1.5, -9.8), V(-12.0, UP + 1.5, -10.95), boxPos];
          const u = clamp((lt - 5.4) / (bo - 5.4));
          const k = u * 3; const i = Math.min(2, Math.floor(k));
          const aim = targets[i].clone().lerp(targets[i + 1], ease.inOut(k - i));
          g.look(aim, 1); holdFlashlight(g, aim, {}); W.flashAim = aim; letter.visible = false;
        } else {
          g.j.spine.rotation.x += 0.1 * (1 - take);
          letter.visible = true;
          const rd = roomPose(W, t, lt, letter, {});
          // da caixa para a mão
          const inBox = boxPos.clone().add(V(0, 0.08, 0));
          letter.position.copy(inBox).lerp(rd, take);
          g.look(letter.position, 1);
          read = rd;
        }
        g.updateFace(t, { ...fearFace(0.5 + (lt > 24 ? 0.3 : 0)), squint: lt > 12 && lt < 24 ? 0.15 : 0 });
        // cavalo de balanço parado
        W.ud.horse.rotation.z = 0;
        updateInterior(W, t, { flashOn: 1, coneInt: 0.05 });
        const C = cut(lt, [[0, 'door'], [5.4, 'pan'], [8.6, 'box'], [11.4, 'letter1'], [16.6, 'face1'], [18.6, 'letter2'], [24.6, 'face2'], [28.5, 'wide']]);
        const hp = headPos(g);
        let ap = 0, focus = 2;
        if (C.id === 'door') camPath(cam, C.lt, [[0, [-14.4, UP + 1.7, -10.5], [-10.2, UP + 1.3, -7.3]], [5.4, [-14.3, UP + 1.65, -10.3], [-12.4, UP + 1.2, -8.6]]], { shake: 0.006, fov: 40 });
        else if (C.id === 'pan') { placeCamera(cam, hp.clone().add(V(0.25, 0.02, 0.2)).toArray(), W.flashAim.toArray(), { shake: 0.012, t, fov: 40 }); ap = 1; focus = 2.5; }
        else if (C.id === 'box') { placeCamera(cam, boxPos.clone().add(V(0.45, 0.42, 0.1)).toArray(), boxPos.clone().add(V(0, 0.06, 0)).toArray(), { shake: 0.003, t, fov: 34 }); ap = 5; focus = 0.62; }
        else if (C.id === 'letter1' || C.id === 'letter2') {
          const lp = letter.position.clone();
          const n = new THREE.Vector3(0, 0, 1).applyQuaternion(letter.quaternion).normalize();
          const d = C.id === 'letter1' ? lerp(0.42, 0.36, C.u) : lerp(0.3, 0.26, C.u);
          const up = new THREE.Vector3(0, 1, 0).applyQuaternion(letter.quaternion);
          const focusPt = lp.clone().addScaledVector(up, C.id === 'letter1' ? 0.05 : -0.005);
          placeCamera(cam, focusPt.clone().addScaledVector(n, d).toArray(), focusPt.toArray(), { shake: 0.003, t, fov: 34 }); ap = 3; focus = d;
          W.flash.intensity = 0; W.flashCone.material.uniforms.intensity.value = 0; g.phone.visible = false;
          W.bounce.position.copy(lp).addScaledVector(n, 0.55).add(V(0, 0.25, 0)); W.bounce.intensity = 0.5; W.bounce.distance = 3; W.bounce.color.set(0xffe2c0);
        }
        else if (C.id === 'face1' || C.id === 'face2') { placeCamera(cam, hp.clone().add(V(-0.62, -0.06, 0.12)).toArray(), hp.toArray(), { shake: 0.004, t, fov: 32 }); ap = 5; focus = 0.62; }
        else camPath(cam, C.lt, [[0, [-11.0, UP + 0.7, -5.6], hp.toArray()], [3.5, [-11.1, UP + 0.75, -5.75], hp.toArray()]], { shake: 0.005, fov: 40 });
        coneGuard(W, cam);
        return { scene: W.scene, camera: cam, aperture: ap, focus, bloom: 0.5, grade: { exposure: 1.9 } };
      },
    };
  },
};

// ---------------- BATIDAS ----------------
const knocks = {
  async create(ctx) {
    const W = interiorWorld(ctx);
    const cam = makeCamera(36);
    return {
      update(lt, t) {
        const g = W.gab; W.dbl.root.visible = false;
        const letter = ctx.sets.letter; letter.visible = true;
        g.resetPose(); g.idle(t, { fear: 0.9 });
        g.root.position.set(-13.0, UP, -9.3);
        const knock = EVENTS.knocks1.reduce((a, k) => a + pulse(t, k, 0.005, 0.12), 0);
        const turn = kf(lt, [[8.0, 0], [11.0, 1]], ease.inOut);
        g.root.rotation.y = -Math.PI / 2 + turn * Math.PI * 0.85;
        roomPose(W, t, lt, letter, { tremble: 1 + knock * 3 + (lt > 3 ? 1.5 : 0) });
        // nome sussurrado: os olhos procuram de onde vem
        const d1 = EVENTS.knocks1[2] - (t - lt);
        const lookBack = smooth(5.6, 7.5, lt);
        const hp = headPos(g);
        const tgt = letter.position.clone().lerp(V(-10.2, UP + 1.5, -7.3), Math.max(lookBack * 0.5, turn));
        g.look(tgt, 1, { maxYaw: 1.4 });
        if (turn > 0.3) { const da = letter.position.clone().lerp(V(-10.2, UP + 1.2, -7.3), smooth(0.3, 1, turn)); holdFlashlight(g, da, {}); W.flashAim = da; }
        g.updateFace(t, { ...fearFace(0.6 + 0.4 * smooth(1, 3, lt)), blink: lt > 1 && lt < 8 ? 0 : null, jaw: smooth(5.2, 6, lt) * 0.12 * (1 - turn) });
        // objetos tremem com as batidas
        W.ud.box.position.y = UP + 0.63 + knock * 0.004;
        W.ud.horse.rotation.z = lt > 10.5 ? 0.06 * Math.sin((lt - 10.5) * 2.2) : knock * 0.01;
        updateInterior(W, t, { flashOn: 1, coneInt: 0.05 });
        const C = cut(lt, [[0, 'mcu'], [4.0, 'high'], [8.0, 'turn'], [11.0, 'pov']]);
        let ap = 0, focus = 2;
        if (C.id === 'mcu') { placeCamera(cam, hp.clone().add(V(-0.75, -0.08, -0.25)).toArray(), hp.toArray(), { shake: 0.004 + knock * 0.02, t, fov: 34 }); ap = 4; focus = 0.8; }
        else if (C.id === 'high') camPath(cam, C.lt, [[0, [-14.6, UP + 2.7, -5.4], [-13.0, UP + 0.8, -9.3]], [4, [-14.5, UP + 2.65, -5.55], [-13.0, UP + 0.9, -9.3]]], { shake: 0.004, fov: 38 });
        else if (C.id === 'turn') { const fw = V(-Math.cos(g.root.rotation.y + Math.PI / 2), 0, Math.sin(g.root.rotation.y + Math.PI / 2)); placeCamera(cam, hp.clone().add(V(-0.7, -0.05, 0.05)).toArray(), hp.toArray(), { shake: 0.005, t, fov: 32 }); ap = 5; focus = 0.7; }
        else camPath(cam, C.lt, [[0, hp.clone().add(V(0.1, 0, 0)).toArray(), [-10.2, UP + 1.3, -7.3]], [2, hp.clone().add(V(0.15, 0, 0)).toArray(), [-10.6, UP + 1.0, -6.6]]], { shake: 0.01, fov: 42 });
        coneGuard(W, cam);
        return { scene: W.scene, camera: cam, aperture: ap, focus, bloom: 0.5, grade: { exposure: 1.9 } };
      },
    };
  },
};

export const shots = { hallway, album, clock, stairs, child_room: childRoom, knocks };
