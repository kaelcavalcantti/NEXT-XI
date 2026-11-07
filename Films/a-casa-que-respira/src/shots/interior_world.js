// Mundo interior compartilhado: cenário, luar pelas janelas, lanterna do celular, relâmpagos, Gabriel e o duplo.
import * as THREE from 'three';
import { EVENTS } from '../timeline.js';
import { makeScene } from '../engine/scene.js';
import { G, makeLightCone, dustFollowSpot } from '../engine/fx.js';
import { buildInterior, UP } from '../sets/interior.js';
import { Human } from '../char/human.js';
import { lightningAt } from '../lib/util.js';

// Gabriel é um único modelo (consistência total); cada cena o adota no update.
export function getGabriel(ctx) { if (!ctx.gab) ctx.gab = new Human({ seed: 1 }); return ctx.gab; }
export function getDouble(ctx) { if (!ctx.dbl) ctx.dbl = new Human({ seed: 7, double: true, skin: 0x8c7c72, iris: 0x070505, lipColor: 0x6a4a48, stubble: 0.55 }); return ctx.dbl; }

export function interiorWorld(ctx) {
  if (ctx.sets.interior) return ctx.sets.interior;
  const scene = makeScene({ fog: 0x06080a, density: 0.06 });
  const set = buildInterior(); scene.add(set);
  // ambiente muito baixo (frio)
  const hemi = new THREE.HemisphereLight(0x3a4a5c, 0x0c0a08, 0.12); scene.add(hemi);
  // luar entrando pelas janelas: spots frios com sombra + feixes volumétricos
  const moons = [];
  const addMoon = (pos, target, angle = 0.35, int = 3, cone = true, len = 9) => {
    const l = new THREE.SpotLight(0x8aa4c8, int, 30, angle, 0.6, 1.2);
    l.position.set(...pos); l.target.position.set(...target); scene.add(l, l.target);
    l.castShadow = true; l.shadow.mapSize.set(1024, 1024); l.shadow.bias = -0.0008; l.shadow.normalBias = 0.02;
    let c = null;
    if (cone) {
      c = makeLightCone({ length: len, radius: Math.tan(angle) * len * 0.9, color: 0x7f9cc4, intensity: 0.07, topRadius: 0.5 });
      c.position.set(...pos);
      const d = new THREE.Vector3(...target).sub(new THREE.Vector3(...pos)).normalize();
      c.quaternion.setFromUnitVectors(new THREE.Vector3(0, -1, 0), d); scene.add(c);
    }
    moons.push({ l, c, base: int });
    return l;
  };
  addMoon([2.5, 4.5, 4.5], [1.5, 0, -4], 0.32, 4);       // janela direita do hall
  addMoon([-2.5, 4.5, 4.5], [-1.8, 0, -4.5], 0.32, 3);   // janela esquerda do hall
  addMoon([15.5, 3.5, 3.0], [15, 0, -3.5], 0.4, 3.5);    // sala
  addMoon([-18, UP + 3.4, -7.9], [-12.5, UP, -8.5], 0.38, 3.2); // quarto
  // lanterna (celular) — posicionada a cada quadro na mão de Gabriel
  const flash = new THREE.SpotLight(0xfff2e0, 0, 18, 0.42, 0.55, 1.6);
  flash.castShadow = true; flash.shadow.mapSize.set(1024, 1024); flash.shadow.bias = -0.0006; flash.shadow.normalBias = 0.02; flash.shadow.camera.near = 0.05;
  scene.add(flash, flash.target);
  const flashCone = makeLightCone({ length: 5, radius: 1.9, color: 0xfff0dc, intensity: 0.0, topRadius: 0.01 });
  scene.add(flashCone);
  // luz de preenchimento suave que segue o rosto (rebatimento da lanterna)
  const bounce = new THREE.PointLight(0xc8b8a8, 0, 2.2, 2); scene.add(bounce);
  // luz de recorte atrás do duplo (janela da galeria)
  const dblRim = new THREE.SpotLight(0x8aa0c0, 0, 9, 0.5, 0.6, 1.4); dblRim.position.set(-2.6, UP + 2.8, -7.9); dblRim.target.position.set(-3.4, UP + 1.4, -6.6); scene.add(dblRim, dblRim.target);
  const gab = getGabriel(ctx); scene.add(gab.root);
  const dbl = getDouble(ctx); dbl.root.visible = false; scene.add(dbl.root);
  const W = { scene, set, ud: set.userData, hemi, moons, flash, flashCone, bounce, gab, dbl, UP, dblRim };
  ctx.sets.interior = W;
  return W;
}

// atualiza relâmpagos (pelas janelas) e lanterna
export function updateInterior(W, t, { flashOn = 1, lightningTimes = EVENTS.lightning, coneInt = 0.06, moonScale = 1 } = {}) {
  W.gab.root.visible = true;
  if (W.gab.root.parent !== W.scene) W.scene.add(W.gab.root);
  if (W.dbl.root.parent !== W.scene) W.scene.add(W.dbl.root);
  const L = lightningAt(t, lightningTimes);
  G.lightning.value = L;
  for (const m of W.moons) { m.l.intensity = m.base * moonScale * (1 + L * 6); if (m.c) m.c.material.uniforms.intensity.value = 0.07 * moonScale * (1 + L * 5); }
  W.hemi.intensity = 0.12 + L * 0.4;
  W.dblRim.intensity = W.dbl.root.visible ? 9 : 0;
  if (W.dbl.root.visible) { W.dbl.j.head.getWorldPosition(W.dblRim.target.position); W.dblRim.target.updateMatrixWorld(); }
  // lanterna na mão direita, apontando para frente do celular
  const g = W.gab;
  g.root.updateMatrixWorld(true);
  const hand = g.j.handR;
  const p = new THREE.Vector3(0, -0.11, 0.05).applyMatrix4(hand.matrixWorld);
  const fwd = new THREE.Vector3(0, -1, 0.25).transformDirection(hand.matrixWorld);
  if (W.flashAim) { fwd.copy(W.flashAim).sub(p).normalize(); }
  W.flash.position.copy(p);
  W.flash.target.position.copy(p).addScaledVector(fwd, 3); W.flash.target.updateMatrixWorld();
  // perto de superfícies a lanterna estoura: compensa pela distância até o alvo
  const dAim = W.flashAim ? W.flashAim.distanceTo(p) : 3;
  const near = Math.min(1, Math.max(0.04, (dAim / 1.8) ** 2));
  W.flash.intensity = 7 * flashOn * near; W.flash.angle = 0.42;
  W.flashCone.position.copy(p);
  W.flashCone.quaternion.setFromUnitVectors(new THREE.Vector3(0, -1, 0), fwd);
  W.flashCone.material.uniforms.intensity.value = coneInt * flashOn * Math.min(1, near * 1.5);
  for (const d of W.ud.dusts) dustFollowSpot(d, W.flash, flashOn * 1.5);
  // rebatimento perto do rosto
  const head = new THREE.Vector3(); g.j.head.getWorldPosition(head);
  W.bounce.position.copy(head).addScaledVector(fwd, 0.6).add(new THREE.Vector3(0, -0.2, 0));
  W.bounce.intensity = 0.35 * flashOn; W.bounce.distance = 2.2; W.bounce.color.set(0xc8b8a8);
  return L;
}

// Se a câmera estiver dentro (ou muito perto) do cone volumétrico da lanterna, atenua o cone.
export function coneGuard(W, cam) {
  const apex = W.flash.position, dir = W.flash.target.position.clone().sub(apex).normalize();
  const v = cam.position.clone().sub(apex); const d = v.length();
  const inside = d < 5.5 && v.normalize().dot(dir) > Math.cos(0.5);
  const close = d < 0.6;
  if (inside || close) W.flashCone.material.uniforms.intensity.value *= 0.12;
}
