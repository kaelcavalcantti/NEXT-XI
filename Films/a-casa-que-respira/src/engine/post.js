// Pipeline de pós-produção: cena -> profundidade de campo -> bloom -> gradação/grão/vinheta/letterbox/sobreposição.
import * as THREE from 'three';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { FullScreenQuad } from 'three/addons/postprocessing/Pass.js';

const DOF_FRAG = /* glsl */`
uniform sampler2D tColor; uniform sampler2D tDepth;
uniform float cameraNear, cameraFar, focus, aperture, maxBlur;
uniform vec2 res;
varying vec2 vUv;
float linDepth(vec2 uv){
  float z = texture2D(tDepth, uv).x * 2.0 - 1.0;
  return 2.0 * cameraNear * cameraFar / (cameraFar + cameraNear - z * (cameraFar - cameraNear));
}
float coc(float d){ return clamp(abs(d - focus) / d * aperture, 0.0, maxBlur); }
void main(){
  float d = linDepth(vUv);
  float c = coc(d);
  vec4 base = texture2D(tColor, vUv);
  if (c < 0.6) { gl_FragColor = base; return; }
  vec3 acc = base.rgb; float wsum = 1.0;
  const float GA = 2.39996323;
  for (int i = 1; i < 28; i++) {
    float r = sqrt(float(i) / 28.0) * c;
    float a = float(i) * GA;
    vec2 o = vec2(cos(a), sin(a)) * r / res;
    vec2 uv = vUv + o;
    float sd = linDepth(uv);
    float sc = coc(sd);
    float w = (sd < d) ? smoothstep(r - 1.0, r + 1.0, sc) : 1.0; // evita vazamento do fundo sobre o primeiro plano
    vec3 s = texture2D(tColor, uv).rgb;
    acc += s * w; wsum += w;
  }
  gl_FragColor = vec4(acc / wsum, 1.0);
}`;

const FINAL_FRAG = /* glsl */`
uniform sampler2D tColor; uniform sampler2D tOverlay;
uniform float time, grain, vignette, letterbox, fade, desat, aberration, overlayOn, exposure, flash;
uniform vec3 lift, gain; uniform float gamma, sat, contrast;
uniform vec2 res;
varying vec2 vUv;
float h(vec2 p){ return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
vec3 aces(vec3 x){ return clamp((x*(2.51*x+0.03))/(x*(2.43*x+0.59)+0.14),0.0,1.0); }
void main(){
  vec2 uv = vUv;
  vec2 cc = uv - 0.5;
  float ab = aberration * dot(cc, cc);
  vec3 col;
  col.r = texture2D(tColor, uv - cc * ab).r;
  col.g = texture2D(tColor, uv).g;
  col.b = texture2D(tColor, uv + cc * ab).b;
  col *= exposure;
  col += flash;
  col = aces(col);
  col = pow(col, vec3(1.0 / 2.2));
  // gradação: lift/gamma/gain + saturação + contraste
  col = col * (gain - lift) + lift;
  col = pow(max(col, 0.0), vec3(1.0 / gamma));
  float l = dot(col, vec3(0.2126, 0.7152, 0.0722));
  col = mix(vec3(l), col, sat * (1.0 - desat));
  col = (col - 0.5) * contrast + 0.5;
  // vinheta
  float v = smoothstep(0.85, 0.2, length(cc * vec2(1.0, 0.82)) * 1.15);
  col *= mix(1.0, v, vignette);
  // grão de filme (dependente da luminância)
  float g = h(uv * res + fract(time * 7.13) * 400.0) - 0.5;
  float g2 = h(uv * res * 0.5 + fract(time * 3.71) * 300.0) - 0.5;
  col += (g * 0.7 + g2 * 0.5) * grain * (0.35 + 0.65 * (1.0 - l));
  col = max(col, 0.0);
  col *= (1.0 - fade);
  // letterbox
  float bar = letterbox * 0.5;
  if (uv.y < bar || uv.y > 1.0 - bar) col = vec3(0.0);
  vec4 ov = texture2D(tOverlay, uv);
  col = mix(col, ov.rgb, ov.a * overlayOn);
  gl_FragColor = vec4(col, 1.0);
}`;

const VERT = `varying vec2 vUv; void main(){ vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }`;

export class Post {
  constructor(renderer, w, h) {
    this.r = renderer; this.w = w; this.h = h;
    const depth = new THREE.DepthTexture(w, h); depth.type = THREE.UnsignedIntType;
    this.rtScene = new THREE.WebGLRenderTarget(w, h, { type: THREE.HalfFloatType, depthTexture: depth, samples: 0 });
    this.rtA = new THREE.WebGLRenderTarget(w, h, { type: THREE.HalfFloatType });
    this.dof = new FullScreenQuad(new THREE.ShaderMaterial({
      vertexShader: VERT, fragmentShader: DOF_FRAG, depthTest: false, depthWrite: false,
      uniforms: {
        tColor: { value: this.rtScene.texture }, tDepth: { value: depth },
        cameraNear: { value: 0.1 }, cameraFar: { value: 1000 }, focus: { value: 5 }, aperture: { value: 0 },
        maxBlur: { value: 14 }, res: { value: new THREE.Vector2(w, h) },
      },
    }));
    this.copy = new FullScreenQuad(new THREE.ShaderMaterial({
      vertexShader: VERT, depthTest: false, depthWrite: false,
      fragmentShader: `uniform sampler2D t; varying vec2 vUv; void main(){ gl_FragColor = texture2D(t, vUv); }`,
      uniforms: { t: { value: this.rtScene.texture } },
    }));
    this.bloom = new UnrealBloomPass(new THREE.Vector2(w / 2, h / 2), 0.6, 0.5, 0.82);
    this.overlayCanvas = document.createElement('canvas');
    this.overlayCanvas.width = w; this.overlayCanvas.height = h;
    this.overlayTex = new THREE.CanvasTexture(this.overlayCanvas);
    this.overlayTex.colorSpace = THREE.NoColorSpace;
    this.final = new FullScreenQuad(new THREE.ShaderMaterial({
      vertexShader: VERT, fragmentShader: FINAL_FRAG, depthTest: false, depthWrite: false,
      uniforms: {
        tColor: { value: this.rtA.texture }, tOverlay: { value: this.overlayTex },
        time: { value: 0 }, grain: { value: 0.06 }, vignette: { value: 0.75 }, letterbox: { value: 0.19 },
        fade: { value: 0 }, desat: { value: 0 }, aberration: { value: 0.006 }, overlayOn: { value: 0 },
        exposure: { value: 1 }, flash: { value: 0 },
        lift: { value: new THREE.Vector3(0.012, 0.02, 0.03) }, gain: { value: new THREE.Vector3(0.93, 1.0, 1.04) },
        gamma: { value: 1.0 }, sat: { value: 0.78 }, contrast: { value: 1.06 },
        res: { value: new THREE.Vector2(w, h) },
      },
    }));
    this.settings = this.final.material.uniforms;
    this.dofU = this.dof.material.uniforms;
  }

  render(scene, camera, opts = {}) {
    const r = this.r;
    r.setRenderTarget(this.rtScene);
    r.clear();
    if (scene) r.render(scene, camera);
    // profundidade de campo (opcional)
    r.setRenderTarget(this.rtA);
    if (opts.aperture > 0) {
      this.dofU.cameraNear.value = camera.near; this.dofU.cameraFar.value = camera.far;
      this.dofU.focus.value = opts.focus; this.dofU.aperture.value = opts.aperture;
      this.dof.render(r);
    } else {
      this.copy.render(r);
    }
    // bloom (compõe aditivamente sobre rtA)
    if ((opts.bloom ?? 0.6) > 0) {
      this.bloom.strength = opts.bloom ?? 0.6;
      this.bloom.threshold = opts.bloomThreshold ?? 0.82;
      this.bloom.radius = opts.bloomRadius ?? 0.5;
      this.bloom.render(r, null, this.rtA, 0, false);
    }
    r.setRenderTarget(null);
    this.final.render(r);
  }
}
