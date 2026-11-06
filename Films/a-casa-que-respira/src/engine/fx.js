// Efeitos atmosféricos: chuva, respingos, cones de luz volumétrica, névoa rasteira, poeira.
import * as THREE from 'three';
import { rng } from '../lib/util.js';

// Uniforms globais compartilhados por todos os shaders de efeito.
export const G = {
  time: { value: 0 },
  lightning: { value: 0 },
  camPos: { value: new THREE.Vector3() },
  rain: { value: 1 },
};

const fogChunk = /* glsl */`
  uniform vec3 fogColor; uniform float fogDensity;
  float fogF(float d){ return 1.0 - exp(-fogDensity * fogDensity * d * d); }`;

// ---------- CHUVA ----------
export function makeRain({ count = 16000, box = [40, 24, 40], speed = 15, wind = [2.2, 0, 0.6], len = 0.55, bright = 0.5, color = 0x9fb6c8 } = {}) {
  const r = rng(77);
  const base = new THREE.BufferGeometry();
  base.setAttribute('position', new THREE.Float32BufferAttribute([0, 0, 0, 0, 1, 0, 0.002, 0, 0, 0.002, 1, 0, 0, 0, 0, 0.002, 1, 0], 3));
  const geo = new THREE.InstancedBufferGeometry();
  geo.index = null;
  geo.setAttribute('position', base.getAttribute('position'));
  const seeds = new Float32Array(count * 4);
  for (let i = 0; i < count; i++) { seeds[i * 4] = r(); seeds[i * 4 + 1] = r(); seeds[i * 4 + 2] = r(); seeds[i * 4 + 3] = r(); }
  geo.setAttribute('seed', new THREE.InstancedBufferAttribute(seeds, 4));
  geo.instanceCount = count;
  const mat = new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
    uniforms: {
      time: G.time, lightning: G.lightning, camPos: G.camPos, amount: G.rain,
      box: { value: new THREE.Vector3(...box) }, speed: { value: speed }, wind: { value: new THREE.Vector3(...wind) },
      len: { value: len }, bright: { value: bright }, color: { value: new THREE.Color(color) },
      extraLight: { value: 0 },
      clearPos: { value: new THREE.Vector3(0, -999, 0) }, clearR: { value: 0 },
      spotPos: { value: new THREE.Vector3() }, spotDir: { value: new THREE.Vector3(0, 0, 1) }, spotCos: { value: 0.9 }, spotInt: { value: 0 },
    },
    vertexShader: /* glsl */`
      attribute vec4 seed; uniform float time, speed, len, amount, clearR; uniform vec3 box, camPos, wind, clearPos;
      varying float vA; varying float vDist; varying vec3 vW;
      void main(){
        vec3 vel = vec3(wind.x, -speed * (0.85 + 0.3 * seed.w), wind.z);
        vec3 p0 = seed.xyz * box + vel * time;
        vec3 rel = mod(p0 - camPos + box * 0.5, box) - box * 0.5;
        vec3 wp = camPos + rel;
        vec3 dir = normalize(vel);
        // linha orientada pela velocidade, com espessura na tela
        vec3 pos = wp + dir * position.y * len * (0.7 + 0.6 * seed.w);
        vec4 mv = viewMatrix * vec4(pos, 1.0);
        vec3 side = normalize(cross(dir, normalize(cameraPosition - wp)));
        mv = viewMatrix * vec4(pos + side * (position.x > 0.001 ? 0.006 : -0.006) * (1.0 + length(rel) * 0.05), 1.0);
        gl_Position = projectionMatrix * mv;
        vDist = -mv.z; vW = wp;
        vA = step(seed.w, amount) * (1.0 - position.y * 0.6) * step(clearR, length((wp - clearPos) * vec3(1.0, 1.4, 0.55)));
      }`,
    fragmentShader: /* glsl */`
      uniform float bright, lightning, extraLight, spotCos, spotInt; uniform vec3 color, spotPos, spotDir; varying float vA; varying float vDist; varying vec3 vW;
      void main(){
        float near = smoothstep(0.3, 1.5, vDist) * (1.0 - smoothstep(12.0, 22.0, vDist));
        vec3 dl = vW - spotPos; float dd = length(dl);
        float spot = spotInt * smoothstep(spotCos, spotCos + 0.04, dot(dl / dd, spotDir)) / (1.0 + dd * dd * 0.03);
        float a = vA * near * bright * (0.35 + 2.2 * lightning + extraLight + spot);
        gl_FragColor = vec4(color * a, 1.0);
      }`,
  });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.frustumCulled = false;
  mesh.renderOrder = 10;
  return mesh;
}

// ---------- RESPINGOS NO CHÃO ----------
export function makeSplashes({ count = 1500, area = 30, y = 0.01, center = [0, 0, 0], bright = 0.35 } = {}) {
  const r = rng(91);
  const geo = new THREE.InstancedBufferGeometry();
  const plane = new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2);
  geo.index = plane.index; geo.setAttribute('position', plane.getAttribute('position')); geo.setAttribute('uv', plane.getAttribute('uv'));
  const s = new Float32Array(count * 4);
  for (let i = 0; i < count; i++) { s[i * 4] = r(); s[i * 4 + 1] = r(); s[i * 4 + 2] = r(); s[i * 4 + 3] = r(); }
  geo.setAttribute('seed', new THREE.InstancedBufferAttribute(s, 4));
  geo.instanceCount = count;
  const mat = new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
    uniforms: { time: G.time, lightning: G.lightning, amount: G.rain, area: { value: area }, y: { value: y }, center: { value: new THREE.Vector3(...center) }, bright: { value: bright } },
    vertexShader: /* glsl */`
      attribute vec4 seed; uniform float time, area, y; uniform vec3 center; varying vec2 vUv; varying float ph; varying float vs;
      void main(){
        float period = 0.45 + seed.z * 0.5;
        float tt = time / period + seed.w * 10.0;
        float cyc = floor(tt);
        ph = fract(tt);
        vec2 off = fract(seed.xy + vec2(cyc * 0.6180339, cyc * 0.4142135)) - 0.5;
        vec3 p = center + vec3(off.x * area, y, off.y * area);
        float sz = 0.03 + ph * 0.12;
        vUv = uv; vs = seed.w;
        gl_Position = projectionMatrix * viewMatrix * vec4(p + position * sz, 1.0);
      }`,
    fragmentShader: /* glsl */`
      uniform float lightning, bright, amount; varying vec2 vUv; varying float ph; varying float vs;
      void main(){
        float d = length(vUv - 0.5) * 2.0;
        float ring = smoothstep(0.75, 0.9, d) * (1.0 - smoothstep(0.9, 1.0, d));
        float a = ring * (1.0 - ph) * bright * (0.4 + 2.0 * lightning) * step(vs, amount);
        gl_FragColor = vec4(vec3(0.7, 0.8, 0.9) * a, 1.0);
      }`,
  });
  const m = new THREE.Mesh(geo, mat); m.frustumCulled = false; return m;
}

// ---------- CONE DE LUZ VOLUMÉTRICA ----------
// Cone apontando para -Y local (gire o objeto para orientar). Aditivo, com ruído de partículas.
export function makeLightCone({ length = 6, radius = 2, color = 0xffe2b0, intensity = 0.25, noise = 1, topRadius = 0.05 } = {}) {
  const geo = new THREE.CylinderGeometry(topRadius, radius, length, 48, 24, true);
  geo.translate(0, -length / 2, 0);
  const mat = new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
    uniforms: { color: { value: new THREE.Color(color) }, intensity: { value: intensity }, len: { value: length }, time: G.time, noiseAmt: { value: noise }, flicker: { value: 1 } },
    vertexShader: /* glsl */`
      varying float vAlong; varying vec3 vN; varying vec3 vView; varying vec3 vW; uniform float len;
      void main(){
        vAlong = -position.y / len;
        vec4 w = modelMatrix * vec4(position, 1.0); vW = w.xyz;
        vN = normalize(mat3(modelMatrix) * normal);
        vView = normalize(cameraPosition - w.xyz);
        gl_Position = projectionMatrix * viewMatrix * w;
      }`,
    fragmentShader: /* glsl */`
      uniform vec3 color; uniform float intensity, time, noiseAmt, flicker; varying float vAlong; varying vec3 vN; varying vec3 vView; varying vec3 vW;
      float h(vec3 p){ return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }
      float n3(vec3 p){ vec3 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
        return mix(mix(mix(h(i), h(i+vec3(1,0,0)), f.x), mix(h(i+vec3(0,1,0)), h(i+vec3(1,1,0)), f.x), f.y),
                   mix(mix(h(i+vec3(0,0,1)), h(i+vec3(1,0,1)), f.x), mix(h(i+vec3(0,1,1)), h(i+vec3(1,1,1)), f.x), f.y), f.z); }
      void main(){
        float edge = pow(abs(dot(normalize(vN), vView)), 1.6);
        float fall = pow(1.0 - clamp(vAlong, 0.0, 1.0), 1.8) * smoothstep(0.0, 0.06, vAlong);
        float nz = mix(1.0, 0.55 + 0.9 * n3(vW * 2.2 + vec3(0.0, time * 0.25, time * 0.1)), noiseAmt);
        float a = edge * fall * nz * intensity * flicker;
        gl_FragColor = vec4(color * a, 1.0);
      }`,
  });
  const m = new THREE.Mesh(geo, mat);
  m.renderOrder = 20;
  return m;
}

// ---------- NÉVOA RASTEIRA (planos com ruído) ----------
export function makeMist({ size = 80, y = 0.4, layers = 3, color = 0x7d8d96, opacity = 0.22, speed = 0.15, scale = 0.05 } = {}) {
  const g = new THREE.Group();
  for (let i = 0; i < layers; i++) {
    const geo = new THREE.PlaneGeometry(size, size, 1, 1).rotateX(-Math.PI / 2);
    const mat = new THREE.ShaderMaterial({
      transparent: true, depthWrite: false,
      uniforms: { time: G.time, color: { value: new THREE.Color(color) }, opacity: { value: opacity }, seed: { value: i * 13.7 }, speed: { value: speed }, scale: { value: scale }, lightning: G.lightning },
      vertexShader: /* glsl */`varying vec3 vW; varying float vD; void main(){ vec4 w = modelMatrix * vec4(position,1.0); vW = w.xyz; vec4 mv = viewMatrix * w; vD = -mv.z; gl_Position = projectionMatrix * mv; }`,
      fragmentShader: /* glsl */`
        uniform vec3 color; uniform float opacity, time, seed, speed, scale, lightning; varying vec3 vW; varying float vD;
        float h(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        float n(vec2 p){ vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
          return mix(mix(h(i), h(i+vec2(1,0)), f.x), mix(h(i+vec2(0,1)), h(i+vec2(1,1)), f.x), f.y); }
        float fbm(vec2 p){ float a = 0.0, m = 0.5; for (int i = 0; i < 5; i++){ a += m * n(p); p *= 2.03; m *= 0.5; } return a; }
        void main(){
          vec2 p = vW.xz * scale + vec2(seed, seed * 0.7) + vec2(time * speed * 0.6, time * speed * 0.2);
          float d = fbm(p + fbm(p * 1.7 - time * speed * 0.3));
          float a = smoothstep(0.35, 0.85, d) * opacity * smoothstep(0.5, 6.0, vD);
          gl_FragColor = vec4(color * (1.0 + 2.5 * lightning), a);
        }`,
    });
    const m = new THREE.Mesh(geo, mat);
    m.position.y = y + i * 0.45;
    m.renderOrder = 15;
    g.add(m);
  }
  return g;
}

// ---------- POEIRA EM SUSPENSÃO (visível dentro de um cone de luz) ----------
export function makeDust({ count = 4000, box = [6, 3, 6], center = [0, 1.5, 0], size = 1.7 } = {}) {
  const r = rng(5);
  const pos = new Float32Array(count * 3), sd = new Float32Array(count);
  for (let i = 0; i < count; i++) { pos[i * 3] = r(); pos[i * 3 + 1] = r(); pos[i * 3 + 2] = r(); sd[i] = r(); }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  geo.setAttribute('sd', new THREE.BufferAttribute(sd, 1));
  const mat = new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
    uniforms: {
      time: G.time, box: { value: new THREE.Vector3(...box) }, center: { value: new THREE.Vector3(...center) }, size: { value: size },
      lPos: { value: new THREE.Vector3() }, lDir: { value: new THREE.Vector3(0, 0, -1) }, lCos: { value: 0.9 }, lInt: { value: 1 },
      ambient: { value: 0.0 }, color: { value: new THREE.Color(0xfff0d8) },
    },
    vertexShader: /* glsl */`
      attribute float sd; uniform float time, size; uniform vec3 box, center, lPos, lDir; uniform float lCos, lInt, ambient;
      varying float vL;
      void main(){
        vec3 p = position;
        p += vec3(sin(time * 0.13 + sd * 40.0), sin(time * 0.07 + sd * 17.0) - time * 0.004, cos(time * 0.11 + sd * 23.0)) * 0.03;
        p = fract(p);
        vec3 w = center + (p - 0.5) * box;
        vec3 d = w - lPos; float dist = length(d);
        float inCone = smoothstep(lCos, lCos + 0.03, dot(d / dist, lDir));
        vL = (inCone * lInt * 0.45 / (1.0 + dist * dist * 0.25) + ambient) * (0.3 + 0.7 * fract(sd * 7.3));
        vec4 mv = viewMatrix * vec4(w, 1.0);
        gl_Position = projectionMatrix * mv;
        gl_PointSize = size * (0.6 + sd) * (1080.0 / 900.0) / max(0.4, -mv.z) ;
      }`,
    fragmentShader: /* glsl */`
      uniform vec3 color; varying float vL;
      void main(){ float d = length(gl_PointCoord - 0.5); float a = smoothstep(0.5, 0.0, d) * vL; gl_FragColor = vec4(color * a, 1.0); }`,
  });
  const m = new THREE.Points(geo, mat); m.frustumCulled = false; m.renderOrder = 25;
  return m;
}

// Atualiza a poeira com a lanterna (SpotLight)
export function dustFollowSpot(dust, spot, intensity = 1) {
  const u = dust.material.uniforms;
  spot.getWorldPosition(u.lPos.value);
  const tgt = new THREE.Vector3(); spot.target.getWorldPosition(tgt);
  u.lDir.value.copy(tgt).sub(u.lPos.value).normalize();
  u.lCos.value = Math.cos(spot.angle);
  u.lInt.value = intensity;
}
