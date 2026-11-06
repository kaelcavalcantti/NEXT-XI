// Construção de cenas: céu noturno com nuvens e relâmpagos, ambiente, helpers de câmera.
import * as THREE from 'three';
import { G } from './fx.js';
import { kf, v3, fbm1 } from '../lib/util.js';

export function makeScene({ fog = 0x0d1418, density = 0.02 } = {}) {
  const s = new THREE.Scene();
  s.background = new THREE.Color(0x000000);
  s.fog = new THREE.FogExp2(fog, density);
  return s;
}

export function makeSky({ radius = 900, top = 0x05080c, horizon = 0x1c262c, moon = [0.3, 0.35, -1], cloudScale = 1 } = {}) {
  const geo = new THREE.SphereGeometry(radius, 48, 24);
  const mat = new THREE.ShaderMaterial({
    side: THREE.BackSide, depthWrite: false, fog: false,
    uniforms: {
      time: G.time, lightning: G.lightning, top: { value: new THREE.Color(top) }, horizon: { value: new THREE.Color(horizon) },
      moonDir: { value: new THREE.Vector3(...moon).normalize() }, cloudScale: { value: cloudScale }, flashDir: { value: new THREE.Vector3(-0.4, 0.4, -1).normalize() },
    },
    vertexShader: `varying vec3 vDir; void main(){ vDir = normalize(position); vec4 p = projectionMatrix * modelViewMatrix * vec4(position, 1.0); gl_Position = p.xyww; }`,
    fragmentShader: /* glsl */`
      uniform float time, lightning, cloudScale; uniform vec3 top, horizon, moonDir, flashDir; varying vec3 vDir;
      float h(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
      float n(vec2 p){ vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
        return mix(mix(h(i), h(i+vec2(1,0)), f.x), mix(h(i+vec2(0,1)), h(i+vec2(1,1)), f.x), f.y); }
      float fbm(vec2 p){ float a = 0.0, m = 0.5; for (int i = 0; i < 6; i++){ a += m * n(p); p = p * 2.02 + 3.1; m *= 0.5; } return a; }
      void main(){
        vec3 d = normalize(vDir);
        float y = max(d.y, 0.0);
        vec3 col = mix(horizon, top, pow(y, 0.55));
        vec2 uv = d.xz / (0.25 + y) * 1.6 * cloudScale + vec2(time * 0.012, time * 0.004);
        float c = fbm(uv);
        float c2 = fbm(uv * 2.3 + c * 1.5 - time * 0.01);
        float cloud = smoothstep(0.35, 0.8, c * 0.65 + c2 * 0.45);
        // brilho da lua atrás das nuvens
        float md = max(dot(d, moonDir), 0.0);
        vec3 moonGlow = vec3(0.28, 0.34, 0.4) * (pow(md, 60.0) * 1.5 + pow(md, 8.0) * 0.25) * (1.0 - cloud * 0.7);
        col += moonGlow;
        col = mix(col, horizon * 0.55 + vec3(0.02, 0.025, 0.03), cloud * 0.6);
        // relâmpago ilumina as nuvens por dentro
        float fd = max(dot(d, flashDir), 0.0);
        col += vec3(0.55, 0.62, 0.75) * lightning * (0.25 + 1.6 * pow(fd, 3.0)) * (0.35 + cloud);
        col *= smoothstep(-0.15, 0.05, d.y) * 0.85 + 0.15;
        gl_FragColor = vec4(col, 1.0);
      }`,
  });
  const m = new THREE.Mesh(geo, mat);
  m.renderOrder = -10;
  m.frustumCulled = false;
  return m;
}

// Câmera com trajetória por keyframes, tremor de mão e foco.
export function makeCamera(fov = 35) {
  const c = new THREE.PerspectiveCamera(fov, 16 / 9, 0.05, 1500);
  return c;
}
export function placeCamera(cam, pos, look, { shake = 0, t = 0, fov, roll = 0, seed = 0 } = {}) {
  cam.position.set(pos[0], pos[1], pos[2]);
  if (shake) {
    cam.position.x += fbm1(t * 0.7, seed + 1) * shake;
    cam.position.y += fbm1(t * 0.6, seed + 2) * shake;
  }
  const l = v3(look);
  if (shake) { l.x += fbm1(t * 0.5, seed + 3) * shake * 2; l.y += fbm1(t * 0.45, seed + 4) * shake * 2; }
  cam.up.set(Math.sin(roll), Math.cos(roll), 0);
  cam.lookAt(l);
  if (fov) { cam.fov = fov; cam.updateProjectionMatrix(); }
}
export function camPath(cam, t, keys, opts = {}) {
  const p = kf(t, keys.map(k => [k[0], k[1], k[3]]));
  const l = kf(t, keys.map(k => [k[0], k[2], k[3]]));
  placeCamera(cam, p, l, { ...opts, t });
}

// Luz-chave fria de "lua", com sombra opcional
export function moonLight(intensity = 0.25, pos = [-60, 90, -40], shadow = false, size = 60) {
  const l = new THREE.DirectionalLight(0x8fa6c4, intensity);
  l.position.set(...pos);
  if (shadow) {
    l.castShadow = true; l.shadow.mapSize.set(2048, 2048);
    const c = l.shadow.camera; c.left = -size; c.right = size; c.top = size; c.bottom = -size; c.near = 1; c.far = 400;
    l.shadow.bias = -0.0005; l.shadow.normalBias = 0.03;
  }
  return l;
}
