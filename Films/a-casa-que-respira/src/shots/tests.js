// Cenas de teste (desenvolvimento): retrato e corpo inteiro do personagem.
import * as THREE from 'three';
import { Human } from '../char/human.js';
import { makeScene, makeCamera, placeCamera } from '../engine/scene.js';

export const TESTS = {
  char: {
    async create(ctx) {
      const scene = makeScene({ fog: 0x0a0d10, density: 0.01 });
      const h = new Human(); scene.add(h.root);
      const floor = new THREE.Mesh(new THREE.PlaneGeometry(20, 20).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x333333 }));
      floor.receiveShadow = true; scene.add(floor);
      const key = new THREE.SpotLight(0xffe6c8, 30, 10, 0.6, 0.5); key.position.set(1.2, 2.4, 2); key.castShadow = true; scene.add(key);
      const rim = new THREE.DirectionalLight(0x88aaff, 1.2); rim.position.set(-2, 2, -2); scene.add(rim);
      scene.add(new THREE.HemisphereLight(0x8899aa, 0x222018, 0.4));
      const cam = makeCamera(30);
      return {
        update(lt) {
          h.resetPose();
          const mode = Math.floor(lt) % 6;
          if (mode < 2) h.idle(lt, { fear: mode });
          else if (mode < 4) h.walk(lt * 0.9, { careful: mode - 2 });
          else { h.idle(lt); h.reach('R', new THREE.Vector3(-0.1, 1.55, 0.25), new THREE.Vector3(-0.6, 1.0, -0.2)); h.attachPhone('ear'); }
          h.look(lt >= 9 ? new THREE.Vector3(0, 1.65, 3) : cam.position.clone(), 1);
          h.updateFace(lt, { jaw: mode === 1 ? 0.6 : 0, brow: mode === 1 ? 1 : 0, eyesWide: mode === 1 ? 0.6 : 0, wide: mode === 5 ? 1 : 0 });
          key.target.position.set(0, 1.4, 0); key.target.updateMatrixWorld();
          const close = lt >= 6; const prof = lt >= 9;
          if (prof) placeCamera(cam, [0.75, 1.66, 0.25], [0, 1.64, 0.03], { fov: 26 });
          else if (close) placeCamera(cam, [0.25, 1.67, 0.75], [0, 1.64, 0], { fov: 26 });
          else placeCamera(cam, [1.6, 1.3, 3.6], [0, 0.95, 0], { fov: 32 });
          return { scene, camera: cam, bloom: 0.3, grade: { letterbox: 0 } };
        },
      };
    },
  },
};

import { interiorWorld, updateInterior } from './interior_world.js';
TESTS.interior = {
  async create(ctx) {
    const W = interiorWorld(ctx);
    const cam = makeCamera(40);
    const views = [
      [[0.5, 1.6, -0.8], [-1.5, 2.5, -7]], [[3.8, 1.6, -2.4], [12, 1.5, -2.4]], [[13, 1.7, -0.8], [15, 0.9, -4.5]],
      [[0, 1.6, -8.2], [0, 1.1, -11.2]], [[-10.5, 4.9, -6.8], [-13.5, 3.8, -9.5]], [[0, 2.5, -1], [-3.4, 3.8, -7]],
    ];
    return {
      update(lt, t) {
        const v = views[Math.floor(lt) % views.length];
        W.gab.resetPose(); W.gab.idle(lt); W.gab.root.position.set(v[0][0] + 0.3, (v[0][1] > 3 ? 3.3 : 0), v[0][2] - 0.6);
        W.gab.root.rotation.y = Math.atan2(v[1][0] - v[0][0], v[1][2] - v[0][2]);
        W.gab.attachPhone('front');
        W.flashAim = new THREE.Vector3(...v[1]);
        updateInterior(W, 1000);
        placeCamera(cam, v[0], v[1], { fov: 50 });
        return { scene: W.scene, camera: cam, bloom: 0.5, grade: { letterbox: 0, exposure: 1.6 } };
      },
    };
  },
};
