// Carro de Gabriel: sedã antigo. Frente em +z local, motorista à esquerda (+x local).
import * as THREE from 'three';
import { G, makeLightCone } from '../engine/fx.js';
import * as TX from '../engine/tex.js';

// fase do limpador: 0 (repouso) -> 1 (topo) -> 0, período P
export const WIPER_P = 1.25;
export const wiperAngle = (t, on = 1) => on ? (1 - Math.cos(2 * Math.PI * t / WIPER_P)) / 2 : 0;

export function buildCar() {
  const g = new THREE.Group(); g.name = 'car';
  const paint = new THREE.MeshPhysicalMaterial({ color: 0x2a3036, roughness: 0.35, metalness: 0.6, clearcoat: 1, clearcoatRoughness: 0.15 });
  const black = new THREE.MeshStandardMaterial({ color: 0x0c0c0d, roughness: 0.7 });
  const chrome = new THREE.MeshStandardMaterial({ color: 0x9aa0a4, metalness: 1, roughness: 0.25 });
  // perfil lateral (z, y)
  const prof = [[-2.2, 0.32], [2.15, 0.32], [2.25, 0.5], [2.2, 0.78], [1.1, 0.95], [0.25, 1.38], [-0.95, 1.4], [-1.75, 1.02], [-2.25, 0.95], [-2.3, 0.6]];
  const sh = new THREE.Shape(); prof.forEach(([z, y], i) => i ? sh.lineTo(z, y) : sh.moveTo(z, y));
  const body = new THREE.ExtrudeGeometry(sh, { depth: 1.72, bevelEnabled: true, bevelThickness: 0.06, bevelSize: 0.06, bevelSegments: 3, curveSegments: 4 });
  body.translate(0, 0, -0.86); body.rotateY(-Math.PI / 2); // extrusão ao longo de x
  const bodyM = new THREE.Mesh(body, paint); bodyM.castShadow = true; bodyM.receiveShadow = true; g.add(bodyM);
  g.userData.bodyMesh = bodyM;
  // vidros escuros laterais (externos)
  const glassM = new THREE.MeshStandardMaterial({ color: 0x06090c, roughness: 0.05, metalness: 0.7 });
  for (const s of [-1, 1]) {
    const ws = new THREE.Shape(); [[1.0, 1.0], [0.28, 1.33], [-0.92, 1.35], [-1.6, 1.03]].forEach(([z, y], i) => i ? ws.lineTo(z, y) : ws.moveTo(z, y));
    const wg = new THREE.ShapeGeometry(ws); wg.rotateY(s * -Math.PI / 2); wg.translate(s * 0.925, 0, 0);
    const wm = new THREE.Mesh(wg, glassM); g.add(wm);
  }
  // para-brisa (externo) — o shader de chuva está no interno
  const wsGeo = new THREE.PlaneGeometry(1.58, Math.hypot(0.85, 0.43));
  const wsExt = new THREE.Mesh(wsGeo, new THREE.MeshStandardMaterial({ color: 0x0a1014, roughness: 0.02, metalness: 0.6, transparent: true, opacity: 0.55 }));
  wsExt.position.set(0, 1.165, 0.68); wsExt.rotation.x = -Math.atan2(0.85, 0.43); wsExt.rotation.x = -(Math.PI / 2 - Math.atan2(0.43, 0.85)); g.add(wsExt);
  g.userData.wsExt = wsExt;
  // rodas
  const wheelG = new THREE.CylinderGeometry(0.33, 0.33, 0.22, 24).rotateZ(Math.PI / 2);
  const hubG = new THREE.CylinderGeometry(0.17, 0.17, 0.23, 16).rotateZ(Math.PI / 2);
  const wheels = [];
  for (const [x, z] of [[0.8, 1.4], [-0.8, 1.4], [0.8, -1.45], [-0.8, -1.45]]) {
    const w = new THREE.Group(); w.position.set(x, 0.33, z);
    w.add(new THREE.Mesh(wheelG, black)); w.add(new THREE.Mesh(hubG, chrome)); g.add(w); wheels.push(w);
  }
  // faróis e lanternas
  const headMat = new THREE.MeshBasicMaterial({ color: new THREE.Color(0xfff2d8).multiplyScalar(4) });
  const tailMat = new THREE.MeshBasicMaterial({ color: new THREE.Color(0xff1808).multiplyScalar(2) });
  for (const s of [-1, 1]) {
    const h = new THREE.Mesh(new THREE.CircleGeometry(0.11, 20), headMat); h.position.set(s * 0.62, 0.68, 2.26); h.rotation.y = 0; g.add(h);
    const tl = new THREE.Mesh(new THREE.PlaneGeometry(0.32, 0.1), tailMat); tl.position.set(s * 0.65, 0.82, -2.32); tl.rotation.y = Math.PI; g.add(tl);
  }
  const grille = new THREE.Mesh(new THREE.PlaneGeometry(0.8, 0.2), chrome); grille.position.set(0, 0.62, 2.27); g.add(grille);
  // luz dos faróis: um holofote com sombra + cones volumétricos
  const head = new THREE.SpotLight(0xfff0d8, 0, 60, 0.42, 0.45, 1.2);
  head.position.set(0, 0.75, 2.3); head.target.position.set(0, 0, 14); head.castShadow = true; head.shadow.mapSize.set(1024, 1024);
  g.add(head, head.target);
  const cones = [];
  for (const s of [-1, 1]) {
    const c = makeLightCone({ length: 16, radius: 4.5, color: 0xfff0d8, intensity: 0.06, topRadius: 0.1 });
    c.position.set(s * 0.62, 0.68, 2.3); c.rotation.x = -Math.PI / 2 + 0.06; g.add(c); cones.push(c);
  }
  // limpadores (externos)
  const wipers = [];
  for (const s of [-1, 1]) {
    const pv = new THREE.Group(); pv.position.set(s * 0.15 - 0.2, 0.96, 1.08); pv.rotation.x = -1.1;
    const arm = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.62, 0.02).translate(0, 0.31, 0), black); pv.add(arm);
    g.add(pv); wipers.push(pv);
  }
  g.userData = { ...g.userData, wheels, head, cones, headMat, tailMat, wipers };
  return g;
}

// Interior (visto de dentro): painel, volante, colunas, teto, bancos, rádio e para-brisa com chuva.
export function buildCarInterior() {
  const g = new THREE.Group();
  const dash = new THREE.MeshStandardMaterial({ color: 0x16171a, roughness: 0.8 });
  const fabricT = TX.fabric({ base: [60, 58, 56], seed: 77, weave: 2 });
  const seat = new THREE.MeshStandardMaterial({ color: 0x2a2826, map: fabricT.map, normalMap: fabricT.normalMap, roughness: 1 });
  const liner = new THREE.MeshStandardMaterial({ color: 0x3a3836, roughness: 1, side: THREE.BackSide });
  // casca da cabine (vista por dentro): teto, painéis das portas, assoalho, vidros laterais
  const roof = new THREE.Mesh(new THREE.PlaneGeometry(1.7, 2.0).rotateX(Math.PI / 2), liner.clone()); roof.material.side = THREE.DoubleSide; roof.position.set(0, 1.4, -0.7); g.add(roof);
  for (const s of [-1, 1]) {
    const door = new THREE.Mesh(new THREE.PlaneGeometry(2.4, 0.55), dash.clone()); door.material.side = THREE.DoubleSide;
    door.position.set(s * 0.86, 0.72, -0.3); door.rotation.y = Math.PI / 2; g.add(door);
    const sideGlass = new THREE.Mesh(new THREE.PlaneGeometry(2.2, 0.4), new THREE.MeshStandardMaterial({ color: 0x0a0e12, roughness: 0.05, metalness: 0.4, transparent: true, opacity: 0.35, side: THREE.DoubleSide }));
    sideGlass.position.set(s * 0.86, 1.2, -0.4); sideGlass.rotation.y = Math.PI / 2; g.add(sideGlass);
  }
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(1.7, 2.6).rotateX(-Math.PI / 2), dash); floor.position.set(0, 0.42, -0.4); g.add(floor);
  // remover a frente da caixa: o para-brisa ocupa
  const dashG = new THREE.BoxGeometry(1.64, 0.32, 0.5).translate(0, 0.86, 0.72);
  g.add(new THREE.Mesh(dashG, dash));
  const hood = new THREE.Mesh(new THREE.BoxGeometry(1.64, 0.06, 0.35).translate(0, 1.02, 0.55).rotateX(0), dash); g.add(hood);
  // painel de instrumentos (brilho)
  const cluster = new THREE.Mesh(new THREE.PlaneGeometry(0.34, 0.12), new THREE.MeshBasicMaterial({ color: new THREE.Color(0x40a0c0).multiplyScalar(0.35) }));
  cluster.position.set(0.4, 0.96, 0.465); cluster.rotation.x = -0.5; cluster.rotation.y = Math.PI; g.add(cluster);
  // volante (motorista à esquerda: +x)
  const wheel = new THREE.Group(); wheel.position.set(0.4, 0.98, 0.32); wheel.rotation.x = -0.45 + Math.PI / 2;
  wheel.add(new THREE.Mesh(new THREE.TorusGeometry(0.19, 0.018, 8, 32), dash));
  wheel.add(new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.03, 0.02), dash));
  wheel.add(new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 0.3, 8).rotateX(Math.PI / 2).translate(0, 0, -0.15), dash));
  g.add(wheel);
  // rádio no console central
  const radioC = TX.canvas(256, 64); const radioTex = new THREE.CanvasTexture(radioC); radioTex.colorSpace = THREE.SRGBColorSpace;
  const radio = new THREE.Mesh(new THREE.PlaneGeometry(0.18, 0.045), new THREE.MeshBasicMaterial({ map: radioTex, color: 0xffffff }));
  radio.position.set(0.0, 0.86, 0.465); radio.rotation.y = Math.PI; radio.rotation.x = 0.15; g.add(radio);
  const radioBody = new THREE.Mesh(new THREE.BoxGeometry(0.24, 0.1, 0.05), dash); radioBody.position.set(0, 0.86, 0.49); g.add(radioBody);
  for (const s of [-1, 1]) { const k = new THREE.Mesh(new THREE.CylinderGeometry(0.014, 0.014, 0.02, 12).rotateX(Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x777777, metalness: 0.8, roughness: 0.3 })); k.position.set(s * 0.1, 0.86, 0.46); g.add(k); }
  // bancos
  for (const x of [0.4, -0.4]) {
    g.add(new THREE.Mesh(new THREE.BoxGeometry(0.55, 0.14, 0.55).translate(x, 0.5, -0.35), seat));
    const back = new THREE.Mesh(new THREE.BoxGeometry(0.55, 0.7, 0.14).translate(0, 0.35, 0), seat); back.position.set(x, 0.55, -0.66); back.rotation.x = -0.2; g.add(back);
    const rest = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.2, 0.12), seat); rest.position.set(x, 1.38, -0.78); g.add(rest);
  }
  g.add(new THREE.Mesh(new THREE.BoxGeometry(1.6, 0.5, 0.5).translate(0, 0.65, -1.45), seat)); // banco traseiro
  // colunas A
  for (const s of [-1, 1]) { const p = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.6, 0.07), dash); p.position.set(s * 0.8, 1.16, 0.64); p.rotation.x = -(Math.PI / 2 - Math.atan2(0.43, 0.85)); g.add(p); }
  // retrovisor
  const mir = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.06, 0.02), dash); mir.position.set(0, 1.3, 0.38); g.add(mir);
  // para-brisa com chuva e limpador (shader)
  const ws = new THREE.Mesh(new THREE.PlaneGeometry(1.58, 0.95, 1, 1), new THREE.ShaderMaterial({
    transparent: true, depthWrite: false,
    uniforms: { time: G.time, lightning: G.lightning, wiperOn: { value: 1 }, wiperP: { value: WIPER_P }, outside: { value: 0.05 } },
    vertexShader: `varying vec2 vUv; varying vec3 vW; void main(){ vUv = uv; vec4 w = modelMatrix * vec4(position,1.0); vW = w.xyz; gl_Position = projectionMatrix * viewMatrix * w; }`,
    fragmentShader: /* glsl */`
      uniform float time, lightning, wiperOn, wiperP, outside; varying vec2 vUv;
      float h(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
      float sinceWipe(vec2 uv){
        if (wiperOn < 0.5) return 100.0;
        // dois limpadores pivotando na base; ângulo do fragmento em relação ao pivô
        vec2 piv = uv.x < 0.5 ? vec2(0.2, -0.05) : vec2(0.62, -0.05);
        vec2 d = uv - piv; float a = atan(d.x, d.y); // 0 = vertical
        float u = clamp((1.35 - a) / 2.5, 0.0, 1.0); // posição angular normalizada 0..1
        if (length(d * vec2(1.0, 1.6)) > 0.75) return 100.0;
        float t1 = wiperP / 6.28318 * acos(1.0 - 2.0 * u);
        float t2 = wiperP - t1;
        float ph = mod(time, wiperP);
        float s1 = mod(ph - t1, wiperP), s2 = mod(ph - t2, wiperP);
        return min(s1, s2);
      }
      void main(){
        vec2 uv = vUv;
        float acc = 0.0, spec = 0.0;
        float age = sinceWipe(uv);
        // gotas: grade em várias escalas
        for (int k = 0; k < 3; k++) {
          float sc = 22.0 + float(k) * 17.0;
          vec2 g = uv * vec2(sc * 1.66, sc);
          vec2 id = floor(g), f = fract(g) - 0.5;
          float r = h(id + float(k) * 7.0);
          vec2 off = vec2(h(id + 3.1) - 0.5, h(id + 5.7) - 0.5) * 0.6;
          float born = r * 1.2;
          float vis = smoothstep(born, born + 0.05, age);
          float rad = 0.12 + 0.18 * h(id + 9.3);
          float d = length((f - off) * vec2(1.0, 0.85));
          float drop = smoothstep(rad, rad * 0.6, d) * vis * step(0.35, r);
          acc += drop;
          spec += smoothstep(rad * 0.45, 0.0, length(f - off - vec2(-rad * 0.3, rad * 0.35))) * vis * step(0.35, r);
        }
        // escorrimento d'água (filetes)
        float streak = smoothstep(0.92, 1.0, h(vec2(floor(uv.x * 140.0), 3.0))) * fract(uv.y * 3.0 - time * 0.7) * step(0.5, age);
        float a = clamp(acc * 0.5 + streak * 0.25, 0.0, 0.85);
        vec3 col = vec3(0.12, 0.15, 0.17) * (0.5 + 3.0 * lightning) + vec3(0.9) * spec * (0.3 + 2.0 * lightning) * 0.6;
        gl_FragColor = vec4(col, a + outside);
      }`,
  }));
  ws.position.set(0, 1.165, 0.67); ws.rotation.set(-Math.atan2(0.85, 0.43), 0, 0); ws.material.side = THREE.DoubleSide;
  g.add(ws);
  // limpadores internos (silhueta vista de dentro)
  const wipers = [];
  for (const [x, len] of [[-0.4, 0.62], [0.32, 0.66]]) {
    const pv = new THREE.Group(); pv.position.set(x, 0.98, 1.0);
    const arm = new THREE.Mesh(new THREE.BoxGeometry(0.016, len, 0.016).translate(0, len / 2, 0), dash); pv.add(arm);
    pv.rotation.x = -1.1; g.add(pv); wipers.push(pv);
  }
  g.userData = { ws, wipers, radio, radioC, radioTex, wheel, cluster };
  return g;
}

export function drawRadio(ud, mode, t) {
  const x = ud.radioC.getContext('2d');
  x.fillStyle = '#020604'; x.fillRect(0, 0, 256, 64);
  const col = mode === 'static' ? '#9fe0a0' : '#5fc070';
  x.fillStyle = col; x.font = '34px Typewriter'; x.textAlign = 'center'; x.textBaseline = 'middle';
  if (mode === 'static') {
    const f = (88 + ((Math.sin(t * 3.1) * 0.5 + 0.5) * 20)).toFixed(1);
    x.globalAlpha = 0.6 + 0.4 * Math.random(); x.fillText(f + ' FM', 128, 32);
    for (let i = 0; i < 25; i++) { x.fillStyle = `rgba(160,230,160,${Math.random() * 0.4})`; x.fillRect(Math.random() * 256, Math.random() * 64, Math.random() * 30, 2); }
    x.globalAlpha = 1;
  } else x.fillText('101.3 FM', 128, 32);
  ud.radioTex.needsUpdate = true;
}
