// Abertura: sobrevoo da cidade, estrada até a casa, janela com silhueta, título.
import * as THREE from 'three';
import { EVENTS } from '../timeline.js';
import { makeScene, makeSky, makeCamera, camPath, moonLight } from '../engine/scene.js';
import { G, makeRain, makeSplashes, makeMist } from '../engine/fx.js';
import { lightningAt, kf, smooth, ease, noise1 } from '../lib/util.js';
import { buildTown } from '../sets/town.js';
import { getWorld, roadX, terrainY, HOUSE_Z, HOUSE_Y } from '../sets/world.js';
import { CHILD_WINDOW } from '../sets/house.js';

// Mundo exterior compartilhado (cidade + campo + casa), construído uma vez.
export function exteriorWorld(ctx) {
  if (ctx.sets.exterior) return ctx.sets.exterior;
  const scene = makeScene({ fog: 0x10171c, density: 0.0075 });
  scene.add(makeSky({}));
  const town = buildTown(); scene.add(town);
  const world = getWorld(ctx); scene.add(world.group);
  const hemi = new THREE.HemisphereLight(0x5a6e80, 0x14100c, 0.6); scene.add(hemi);
  const moon = moonLight(0.7, [-80, 120, -60]); scene.add(moon);
  const flashL = new THREE.DirectionalLight(0xc8d8ff, 0); flashL.position.set(-50, 100, -150); scene.add(flashL);
  const rain = makeRain({ count: 22000, box: [44, 26, 44], speed: 16, bright: 0.45 }); scene.add(rain);
  const mist = makeMist({ size: 900, y: 1.2, layers: 3, opacity: 0.16, scale: 0.012, color: 0x6b7a84 }); scene.add(mist);
  // lâmpada que pisca (rua principal)
  const lamps = town.userData.lamps;
  const ex = { scene, town, world, hemi, moon, flashL, rain, mist, lamps };
  ctx.sets.exterior = ex;
  return ex;
}

export function updateExterior(ex, t, { lightningTimes = EVENTS.lightning, flashScale = 1 } = {}) {
  const L = lightningAt(t, lightningTimes) * flashScale;
  G.lightning.value = L;
  ex.flashL.intensity = L * 3.2;
  ex.hemi.intensity = 0.6 + L * 1.2;
  // postes: alguns piscam de forma irregular
  ex.lamps.forEach((lp, i) => {
    if (i % 11 !== 3) return;
    const f = noise1(t * 9 + i, i) > 0.35 ? 0.15 : 1;
    lp.userData.bulbMat.color.copy(lp.userData.color).multiplyScalar(6 * f);
    if (lp.userData.cone) lp.userData.cone.material.uniforms.flicker.value = f;
    if (lp.userData.pool) lp.userData.pool.material.opacity = f;
  });
  return L;
}

const aerial = {
  async create(ctx) {
    const ex = exteriorWorld(ctx);
    const cam = makeCamera(38);
    return {
      update(lt, t) {
        G.rain.value = 1;
        updateExterior(ex, t);
        ex.world.setVisible({ town: true, far: true });
        // grua aérea lenta: entra pela borda sul, cruza a praça e segue para o norte
        camPath(cam, lt, [
          [0, [70, 46, 120], [10, 0, 10]],
          [7, [36, 38, 52], [0, 4, -20]],
          [13, [8, 34, -30], [0, 6, -160]],
        ], { shake: 0.25 });
        const fadeIn = 1 - smooth(0, 3.5, lt);
        return { scene: ex.scene, camera: cam, bloom: 0.7, grade: { fade: fadeIn, exposure: 1.5 } };
      },
    };
  },
};


const road = {
  async create(ctx) {
    const ex = exteriorWorld(ctx);
    const cam = makeCamera(36);
    return {
      update(lt, t) {
        G.rain.value = 1;
        updateExterior(ex, t);
        // continua o voo: deixa a cidade e segue a estrada pela mata até o casarão no alto
        const z = kf(lt, [[0, -112], [10, -318]], ease.inOut);
        const x = roadX(z) * 0.7 + 4;
        const y = terrainY(x, z) + kf(lt, [[0, 30], [10, 21]]);
        const look = [0, HOUSE_Y + 6, HOUSE_Z];
        const lookNear = [roadX(z - 60), terrainY(roadX(z - 60), z - 60), z - 60];
        const w = smooth(1.5, 8.5, lt);
        cam.position.set(x, y, z);
        cam.lookAt(look[0] * w + lookNear[0] * (1 - w), look[1] * w + lookNear[1] * (1 - w), look[2] * w + lookNear[2] * (1 - w));
        cam.position.y += noise1(lt * 0.6, 4) * 0.25;
        return { scene: ex.scene, camera: cam, bloom: 0.7, grade: { exposure: 1.6 } };
      },
    };
  },
};

const windowPush = {
  async create(ctx) {
    const ex = exteriorWorld(ctx);
    const cam = makeCamera(30);
    const hud = ex.world.house.userData;
    const win = new THREE.Vector3(CHILD_WINDOW.x, HOUSE_Y + CHILD_WINDOW.y, HOUSE_Z + CHILD_WINDOW.z + 0.16);
    return {
      update(lt, t) {
        G.rain.value = 1;
        updateExterior(ex, t);
        // aproximação lenta e contínua até a janela do segundo andar
        const p = kf(lt, [[0, [-1.0, HOUSE_Y + 9.5, HOUSE_Z + 46]], [10.4, [-4.6, win.y + 0.25, win.z + 3.6]]], ease.inOut);
        const l = kf(lt, [[0, [-0.5, HOUSE_Y + 5.5, HOUSE_Z]], [10.4, [win.x + 0.05, win.y + 0.05, win.z]]], ease.inOut);
        camPath(cam, lt, [[0, p, l], [1, p, l]], { shake: 0.04 });
        cam.fov = kf(lt, [[0, 32], [10.4, 24]]); cam.updateProjectionMatrix();
        // silhueta atrás da cortina: um instante
        const s0 = EVENTS.silhouette[0] - (t - lt), s1 = EVENTS.silhouette[1] - (t - lt);
        const on = smooth(s0, s0 + 0.35, lt) * (1 - smooth(s1 - 0.25, s1, lt));
        hud.sil.material.opacity = on;
        hud.glow.material.color.setRGB(0.05 * on, 0.07 * on, 0.06 * on);
        hud.curtainMat.emissive.setRGB(0.05 * on, 0.06 * on, 0.055 * on);
        hud.curtain.position.x = Math.sin(lt * 1.3) * 0.01 * on;
        const cutBlack = lt > (EVENTS.windowCut - (t - lt)) ? 1 : 0;
        return { scene: ex.scene, camera: cam, bloom: 0.6, aperture: 2.5, focus: cam.position.distanceTo(win), grade: { fade: cutBlack, exposure: 1.5 } };
      },
    };
  },
};

export const shots = { aerial_town: aerial, road_to_house: road, window_push: windowPush };
