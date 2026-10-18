// NEXT XI — browser preview: three.js renderer, touch/keyboard input, HUD and synthesized audio
// on top of engine.js. Everything is procedural: no external models, textures or sounds.
(function () {
  'use strict';
  const E = window.NXEngine;
  const C = E.constants;
  const $ = id => document.getElementById(id);
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const lerp = (a, b, t) => a + (b - a) * t;

  // ---------------------------------------------------------------- settings / state
  const store = {
    get(k, d) { try { const v = localStorage.getItem('nxi.' + k); return v == null ? d : JSON.parse(v); } catch (e) { return d; } },
    set(k, v) { try { localStorage.setItem('nxi.' + k, JSON.stringify(v)); } catch (e) { /* storage blocked */ } }
  };
  const cfg = {
    home: store.get('home', 'APA'), away: store.get('away', 'UNS'),
    half: store.get('half', 3), diff: store.get('diff', 1), sound: store.get('sound', true), cam: store.get('cam', 1)
  };
  let match = null, view = null, paused = false, running = false, lastFeed = 0;

  // ---------------------------------------------------------------- textures
  function canvasTex(w, h, draw, repeat) {
    const c = document.createElement('canvas');
    c.width = w; c.height = h;
    draw(c.getContext('2d'), w, h);
    const t = new THREE.CanvasTexture(c);
    if (repeat) { t.wrapS = t.wrapT = THREE.RepeatWrapping; }
    t.anisotropy = 4;
    return t;
  }

  function pitchTexture() {
    const ppm = 16, W = 115, H = 78;
    return canvasTex(W * ppm, H * ppm, (g, w, h) => {
      const X = x => (x + W / 2) * ppm, Y = z => (z + H / 2) * ppm;
      g.fillStyle = '#2f7a32'; g.fillRect(0, 0, w, h);
      // Mown stripes, 12 across the length.
      for (let i = 0; i < 18; i++) {
        g.fillStyle = i % 2 ? 'rgba(255,255,255,0.045)' : 'rgba(0,0,0,0.06)';
        g.fillRect(X(-C.HL + i * (2 * C.HL / 18)), Y(-C.HW), (2 * C.HL / 18) * ppm + 1, 2 * C.HW * ppm);
      }
      // Subtle noise so the grass does not look flat.
      const img = g.getImageData(0, 0, w, h), d = img.data;
      for (let i = 0; i < d.length; i += 4) { const n = (Math.random() - 0.5) * 14; d[i] += n; d[i + 1] += n; d[i + 2] += n * 0.6; }
      g.putImageData(img, 0, 0);
      g.strokeStyle = 'rgba(245,245,240,0.92)'; g.lineWidth = 0.12 * ppm;
      const L = (x1, z1, x2, z2) => { g.beginPath(); g.moveTo(X(x1), Y(z1)); g.lineTo(X(x2), Y(z2)); g.stroke(); };
      g.strokeRect(X(-C.HL), Y(-C.HW), 2 * C.HL * ppm, 2 * C.HW * ppm);
      L(0, -C.HW, 0, C.HW);
      g.beginPath(); g.arc(X(0), Y(0), 9.15 * ppm, 0, Math.PI * 2); g.stroke();
      g.fillStyle = 'rgba(245,245,240,0.92)';
      g.beginPath(); g.arc(X(0), Y(0), 0.2 * ppm, 0, Math.PI * 2); g.fill();
      for (const s of [-1, 1]) {
        const gx = s * C.HL;
        g.strokeRect(Math.min(X(gx), X(gx - s * 16.5)), Y(-20.16), 16.5 * ppm, 40.32 * ppm);
        g.strokeRect(Math.min(X(gx), X(gx - s * 5.5)), Y(-9.16), 5.5 * ppm, 18.32 * ppm);
        g.beginPath(); g.arc(X(gx - s * 11), Y(0), 0.2 * ppm, 0, Math.PI * 2); g.fill();
        const a0 = Math.acos(5.5 / 9.15);
        g.beginPath();
        if (s > 0) g.arc(X(gx - 11), Y(0), 9.15 * ppm, Math.PI - a0, Math.PI + a0);
        else g.arc(X(gx + 11), Y(0), 9.15 * ppm, -a0, a0);
        g.stroke();
        for (const zs of [-1, 1]) {
          g.beginPath();
          g.arc(X(gx), Y(zs * C.HW), 1 * ppm, s > 0 ? (zs > 0 ? Math.PI : Math.PI / 2) : (zs > 0 ? -Math.PI / 2 : 0), s > 0 ? (zs > 0 ? 1.5 * Math.PI : Math.PI) : (zs > 0 ? 0 : Math.PI / 2));
          g.stroke();
        }
      }
    });
  }

  function crowdTexture() {
    return canvasTex(512, 256, (g, w, h) => {
      g.fillStyle = '#141a24'; g.fillRect(0, 0, w, h);
      const cols = ['#c8102e', '#f5f5f5', '#1e3a8a', '#facc15', '#334155', '#64748b', '#e2e8f0', '#0f172a', '#b45309'];
      for (let y = 4; y < h; y += 9) {
        for (let x = 3; x < w; x += 7) {
          if (Math.random() < 0.12) continue;
          g.fillStyle = cols[(Math.random() * cols.length) | 0];
          g.globalAlpha = 0.55 + Math.random() * 0.45;
          g.fillRect(x + Math.random() * 2, y + Math.random() * 2, 4, 6);
          g.fillStyle = '#d6a77a'; g.fillRect(x + 0.8 + Math.random(), y - 3, 2.5, 2.5);
        }
      }
      g.globalAlpha = 1;
    }, true);
  }

  function boardTexture(text, bg, fg) {
    return canvasTex(1024, 64, (g, w, h) => {
      g.fillStyle = bg; g.fillRect(0, 0, w, h);
      g.fillStyle = fg; g.font = '700 40px "Barlow Condensed", "Arial Narrow", sans-serif';
      g.textBaseline = 'middle';
      for (let x = 20; x < w; x += g.measureText(text).width + 70) g.fillText(text, x, h / 2 + 2);
    }, true);
  }

  function numberTexture(num, color, shirt) {
    return canvasTex(64, 64, (g) => {
      g.fillStyle = shirt; g.fillRect(0, 0, 64, 64);
      g.fillStyle = color; g.font = '700 46px "Barlow Condensed", "Arial Narrow", sans-serif';
      g.textAlign = 'center'; g.textBaseline = 'middle';
      g.fillText(String(num), 32, 35);
    });
  }

  function ballTexture() {
    return canvasTex(256, 128, (g, w, h) => {
      g.fillStyle = '#f8fafc'; g.fillRect(0, 0, w, h);
      g.fillStyle = '#111827';
      for (let i = 0; i < 12; i++) {
        const x = (i % 6) * (w / 6) + (i < 6 ? 0 : w / 12), y = i < 6 ? h * 0.3 : h * 0.72;
        g.beginPath();
        for (let k = 0; k < 5; k++) { const a = k / 5 * Math.PI * 2; g.lineTo(x + Math.cos(a) * 13, y + Math.sin(a) * 15); }
        g.fill();
      }
      g.fillStyle = '#c8102e'; g.fillRect(0, h * 0.5 - 3, w, 6);
    });
  }

  // ---------------------------------------------------------------- stadium
  function buildStadium(scene, homeName) {
    const lam = c => new THREE.MeshLambertMaterial({ color: c });
    const pitch = new THREE.Mesh(new THREE.PlaneGeometry(115, 78), new THREE.MeshLambertMaterial({ map: pitchTexture() }));
    pitch.rotation.x = -Math.PI / 2;
    scene.add(pitch);
    const apron = new THREE.Mesh(new THREE.PlaneGeometry(150, 110), lam('#24552a'));
    apron.rotation.x = -Math.PI / 2; apron.position.y = -0.02;
    scene.add(apron);

    // Advertising boards.
    const boards = [['NEXT XI', '#0b1220', '#f5d90a'], ['ARENA DAS MARÉS', '#c8102e', '#ffffff'], ['PRÉVIA JOGÁVEL', '#0b1220', '#e2e8f0'], ['PORTO ALTO', '#1e3a8a', '#ffffff']];
    const addBoard = (x, z, w, rotY, i) => {
      const b = boards[i % boards.length];
      const t = boardTexture(b[0], b[1], b[2]);
      t.repeat.set(w / 30, 1);
      const m = new THREE.Mesh(new THREE.BoxGeometry(w, 0.9, 0.12), [lam('#111'), lam('#111'), lam('#111'), lam('#111'), new THREE.MeshBasicMaterial({ map: t }), new THREE.MeshBasicMaterial({ map: t })]);
      m.position.set(x, 0.45, z); m.rotation.y = rotY;
      scene.add(m);
    };
    for (let i = 0; i < 4; i++) { addBoard(-45 + i * 30, -37.5, 29.5, 0, i); addBoard(-45 + i * 30, 37.5, 29.5, Math.PI, i + 2); }
    for (const s of [-1, 1]) { addBoard(s * 57, -18, 28, s * Math.PI / 2, 1); addBoard(s * 57, 18, 28, s * Math.PI / 2, 3); }

    // Stands: stepped tiers with a crowd texture.
    const crowd = crowdTexture();
    const tier = (w, depth, x, z, rotY) => {
      const g = new THREE.Group();
      for (let r = 0; r < 3; r++) {
        const tex = crowd.clone(); tex.needsUpdate = true; tex.repeat.set(w / 14, 1);
        const step = new THREE.Mesh(new THREE.BoxGeometry(w, 3.2, depth / 3), [lam('#1b2230'), lam('#1b2230'), lam('#232c3c'), lam('#1b2230'), new THREE.MeshLambertMaterial({ map: tex }), lam('#1b2230')]);
        step.position.set(0, 1.6 + r * 3.0, r * depth / 3);
        g.add(step);
      }
      const roof = new THREE.Mesh(new THREE.BoxGeometry(w + 2, 0.5, depth * 0.9), lam('#0f141d'));
      roof.position.set(0, 14.5, depth * 0.55);
      g.add(roof);
      g.position.set(x, 0, z); g.rotation.y = rotY;
      scene.add(g);
    };
    tier(124, 18, 0, -41, 0);
    // No stand on the camera side (the broadcast camera sits there).
    tier(80, 15, -61, 0, Math.PI / 2);
    tier(80, 15, 61, 0, -Math.PI / 2);

    // Floodlights.
    for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
      const mast = new THREE.Mesh(new THREE.CylinderGeometry(0.35, 0.5, 34, 6), lam('#3b4252'));
      mast.position.set(sx * 64, 17, sz * 48); scene.add(mast);
      const panel = new THREE.Mesh(new THREE.BoxGeometry(6, 3, 0.6), new THREE.MeshBasicMaterial({ color: '#fffbe6' }));
      panel.position.set(sx * 64, 34.5, sz * 48); panel.lookAt(0, 0, 0); scene.add(panel);
      const glow = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTexture(), color: '#fff3c4', transparent: true, opacity: 0.7, depthWrite: false }));
      glow.scale.set(22, 22, 1); glow.position.copy(panel.position); scene.add(glow);
    }

    // Goals with nets.
    const white = new THREE.MeshLambertMaterial({ color: '#f8fafc' });
    const netMat = new THREE.LineBasicMaterial({ color: '#e5e7eb', transparent: true, opacity: 0.45 });
    for (const s of [-1, 1]) {
      const gx = s * C.HL;
      const post = (z) => { const m = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, C.GH, 10), white); m.position.set(gx, C.GH / 2, z); scene.add(m); };
      post(-C.HGW); post(C.HGW);
      const bar = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, C.HGW * 2 + 0.12, 10), white);
      bar.rotation.x = Math.PI / 2; bar.position.set(gx, C.GH, 0); scene.add(bar);
      const pts = [];
      const back = gx + s * C.GD;
      const step = 0.25;
      for (let z = -C.HGW; z <= C.HGW + 1e-6; z += step) { pts.push(back, 0, z, back, C.GH, z); pts.push(gx, C.GH, z, back, C.GH, z); }
      for (let y = 0; y <= C.GH + 1e-6; y += step) { pts.push(back, y, -C.HGW, back, y, C.HGW); }
      for (let x = 0; x <= C.GD + 1e-6; x += step) for (const z of [-C.HGW, C.HGW]) pts.push(gx + s * x, 0, z, gx + s * x, C.GH, z);
      for (let y = 0; y <= C.GH + 1e-6; y += step) for (const z of [-C.HGW, C.HGW]) pts.push(gx, y, z, back, y, z);
      for (let x = 0; x <= C.GD + 1e-6; x += step) pts.push(gx + s * x, C.GH, -C.HGW, gx + s * x, C.GH, C.HGW);
      const geo = new THREE.BufferGeometry();
      geo.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
      scene.add(new THREE.LineSegments(geo, netMat));
      for (const z of [-1, 1]) {
        const flag = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 1.5, 6), white);
        flag.position.set(gx, 0.75, z * C.HW); scene.add(flag);
        const cloth = new THREE.Mesh(new THREE.PlaneGeometry(0.45, 0.32), new THREE.MeshBasicMaterial({ color: '#f5d90a', side: THREE.DoubleSide }));
        cloth.position.set(gx - s * 0.22, 1.35, z * C.HW); scene.add(cloth);
      }
    }
  }

  let _glow = null;
  function glowTexture() {
    if (_glow) return _glow;
    _glow = canvasTex(128, 128, (g) => {
      const r = g.createRadialGradient(64, 64, 0, 64, 64, 64);
      r.addColorStop(0, 'rgba(255,255,255,1)'); r.addColorStop(0.25, 'rgba(255,250,220,0.5)'); r.addColorStop(1, 'rgba(255,250,220,0)');
      g.fillStyle = r; g.fillRect(0, 0, 128, 128);
    });
    return _glow;
  }

  let _shadow = null;
  function shadowTexture() {
    if (_shadow) return _shadow;
    _shadow = canvasTex(64, 64, (g) => {
      const r = g.createRadialGradient(32, 32, 0, 32, 32, 32);
      r.addColorStop(0, 'rgba(0,0,0,0.55)'); r.addColorStop(1, 'rgba(0,0,0,0)');
      g.fillStyle = r; g.fillRect(0, 0, 64, 64);
    });
    return _shadow;
  }

  // ---------------------------------------------------------------- athlete model
  const SKINS = ['#f1c7a3', '#d9a27c', '#b97a52', '#8d5a3b', '#5e3b26'];
  const HAIRS = ['#1f1611', '#3b2416', '#0f0f0f', '#6b4a2b', '#c9a15b'];

  function box(w, h, d, mat, y) {
    const g = new THREE.BoxGeometry(w, h, d);
    g.translate(0, y, 0);
    return new THREE.Mesh(g, mat);
  }

  function buildAthlete(p, kit, rng) {
    const lam = c => new THREE.MeshLambertMaterial({ color: c });
    const shirtCol = p.isGK ? kit.keeper : kit.shirt;
    const shirt = lam(shirtCol), shorts = lam(p.isGK ? '#1f2937' : kit.shorts), socks = lam(p.isGK ? kit.keeper : kit.socks);
    const skin = lam(SKINS[Math.floor(rng.next() * SKINS.length)]);
    const hair = lam(HAIRS[Math.floor(rng.next() * HAIRS.length)]);
    const boots = lam(rng.chance(0.5) ? '#111827' : rng.pick(['#f97316', '#e5e7eb', '#22d3ee', '#a3e635']));
    const gloves = lam('#f1f5f9');
    const scale = 0.94 + rng.next() * 0.12;

    const root = new THREE.Group();
    const body = new THREE.Group(); root.add(body);          // tilts for slides and dives
    const pelvis = new THREE.Group(); pelvis.position.y = 0.95; body.add(pelvis);
    pelvis.add(box(0.36, 0.22, 0.22, shorts, -0.02));
    const torso = new THREE.Group(); torso.position.y = 0.08; pelvis.add(torso);
    torso.add(box(0.42, 0.5, 0.23, shirt, 0.25));
    const back = new THREE.Mesh(new THREE.PlaneGeometry(0.24, 0.24), new THREE.MeshBasicMaterial({ map: numberTexture(p.number, kit.number, shirtCol) }));
    back.position.set(0, 0.3, -0.118); back.rotation.y = Math.PI; torso.add(back);
    const head = new THREE.Group(); head.position.y = 0.52; torso.add(head);
    head.add(box(0.09, 0.08, 0.09, skin, 0.03));
    const skull = new THREE.Mesh(new THREE.SphereGeometry(0.115, 12, 10), skin); skull.position.y = 0.17; skull.scale.set(0.95, 1.08, 1); head.add(skull);
    const cap = new THREE.Mesh(new THREE.SphereGeometry(0.122, 12, 8, 0, Math.PI * 2, 0, Math.PI * 0.5), hair); cap.position.y = 0.19; cap.rotation.x = -0.25; head.add(cap);

    const limb = (parent, x, y, upLen, loLen, upW, loW, upMat, loMat, end) => {
      const hip = new THREE.Group(); hip.position.set(x, y, 0); parent.add(hip);
      hip.add(box(upW, upLen, upW, upMat, -upLen / 2));
      const knee = new THREE.Group(); knee.position.y = -upLen; hip.add(knee);
      knee.add(box(loW, loLen, loW, loMat, -loLen / 2));
      if (end) { const e = end(); e.position.y = -loLen; knee.add(e); }
      return { hip, knee };
    };
    const legs = [-1, 1].map(s => limb(pelvis, s * 0.1, -0.06, 0.43, 0.43, 0.15, 0.12, skin, socks, () => {
      const f = new THREE.Group(); f.add(box(0.11, 0.07, 0.26, boots, -0.02)); f.children[0].position.z = 0.06; return f;
    }));
    // Shorts cover the top of the thighs.
    for (const l of legs) l.hip.add(box(0.165, 0.18, 0.165, shorts, -0.07));
    const arms = [-1, 1].map(s => limb(torso, s * 0.27, 0.46, 0.3, 0.28, 0.1, 0.085, p.isGK ? shirt : skin, p.isGK ? shirt : skin, () => {
      const h = box(0.09, 0.09, 0.07, p.isGK ? gloves : skin, -0.03); const g = new THREE.Group(); g.add(h); return g;
    }));
    for (const a of arms) a.hip.add(box(0.11, 0.13, 0.11, shirt, -0.05));     // sleeves

    root.scale.setScalar(scale);
    const shadow = new THREE.Mesh(new THREE.PlaneGeometry(1.1, 1.1), new THREE.MeshBasicMaterial({ map: shadowTexture(), transparent: true, depthWrite: false }));
    shadow.rotation.x = -Math.PI / 2; shadow.position.y = 0.02;
    return { root, body, pelvis, torso, head, legs, arms, shadow, scale };
  }

  // Procedural pose for one frame (run cycle + action overlays).
  function poseAthlete(a, p, m, time) {
    const speed = p.speed;
    const stride = 2.1;
    const ph = p.anim / stride * Math.PI * 2;
    const run = clamp(speed / 7, 0, 1);
    const amp = 0.25 + run * 0.65;
    const [L, Rr] = a.legs, [AL, AR] = a.arms;
    let lean = run * 0.22;
    let bob = Math.abs(Math.sin(ph)) * 0.05 * run;
    // Base gait.
    L.hip.rotation.x = Math.sin(ph) * amp; Rr.hip.rotation.x = -Math.sin(ph) * amp;
    L.knee.rotation.x = Math.max(0, -Math.cos(ph)) * amp * 1.5 + 0.08; Rr.knee.rotation.x = Math.max(0, Math.cos(ph)) * amp * 1.5 + 0.08;
    AL.hip.rotation.x = -Math.sin(ph) * amp * 0.8; AR.hip.rotation.x = Math.sin(ph) * amp * 0.8;
    AL.knee.rotation.x = -0.4 - run * 0.7; AR.knee.rotation.x = -0.4 - run * 0.7;
    AL.hip.rotation.z = -0.12; AR.hip.rotation.z = 0.12;
    if (speed < 0.3) { const br = Math.sin(time * 2 + p.id) * 0.02; AL.hip.rotation.x = br; AR.hip.rotation.x = -br; L.hip.rotation.x = 0.05; Rr.hip.rotation.x = -0.05; }
    a.body.rotation.set(0, 0, 0); a.body.position.set(0, 0, 0);
    a.head.rotation.set(0, 0, 0);
    a.torso.rotation.set(0, 0, 0);

    const act = p.action;
    if (act && act.type === 'kick') {
      const k = act.t / act.contact;
      const kick = act.t < act.contact ? -1.1 * Math.sin(Math.min(1, k) * Math.PI / 2) : lerp(-1.1, 1.3, Math.min(1, (act.t - act.contact) / 0.12));
      const after = act.t > act.contact + 0.12 ? lerp(1.3, 0.2, Math.min(1, (act.t - act.contact - 0.12) / 0.25)) : null;
      Rr.hip.rotation.x = after != null ? after : kick;
      Rr.knee.rotation.x = act.t < act.contact ? 1.3 * Math.min(1, k) : 0.15;
      L.hip.rotation.x = -0.15; L.knee.rotation.x = 0.25;
      AL.hip.rotation.z = -0.9; AR.hip.rotation.z = 0.5; AL.hip.rotation.x = 0.3;
      lean = act.kick.kind === 'shot' ? 0.18 : 0.1;
    } else if (act && act.type === 'throw') {
      const k = clamp(act.t / act.contact, 0, 1);
      const arm = act.t < act.contact ? -2.9 - 0.4 * k : lerp(-3.3, -1.2, clamp((act.t - act.contact) / 0.25, 0, 1));
      AL.hip.rotation.x = arm; AR.hip.rotation.x = arm; AL.knee.rotation.x = -0.6; AR.knee.rotation.x = -0.6;
      AL.hip.rotation.z = 0; AR.hip.rotation.z = 0;
      lean = act.t < act.contact ? -0.25 * k : 0.2;
    } else if (act && act.type === 'tackle') {
      Rr.hip.rotation.x = 0.9; Rr.knee.rotation.x = 0.1; L.knee.rotation.x = 0.6; lean = 0.25;
    } else if (act && act.type === 'slide') {
      a.body.rotation.x = -1.15; a.body.position.y = -0.55;
      Rr.hip.rotation.x = 1.4; Rr.knee.rotation.x = 0; L.hip.rotation.x = 0.9; L.knee.rotation.x = 1.2;
      AL.hip.rotation.z = -1.2; AR.hip.rotation.z = 1.2;
      lean = 0;
    } else if (act && act.type === 'dive') {
      const k = clamp(act.t / 0.3, 0, 1);
      const side = act.side;
      a.body.rotation.z = side * 1.35 * k;
      a.body.position.y = p.jump * 0.9 + 0.1 * k;
      AL.hip.rotation.x = -3.0 * k; AR.hip.rotation.x = -3.0 * k; AL.knee.rotation.x = 0; AR.knee.rotation.x = 0;
      L.hip.rotation.x = 0.2; Rr.hip.rotation.x = -0.2;
      lean = 0;
    }
    if (p.jump > 0.05 && !(act && act.type === 'dive')) {
      a.body.position.y = p.jump; L.knee.rotation.x = 0.7; Rr.knee.rotation.x = 0.7; a.head.rotation.x = -0.3;
    }
    if (p.stun > 0.3 && !act) { lean = 0.35; }
    if (m.phase === 'goal' && p.team === m.goalTeam && !p.isGK) {
      AL.hip.rotation.x = -2.8; AR.hip.rotation.x = -2.8; AL.knee.rotation.x = -0.2; AR.knee.rotation.x = -0.2;
    }
    if (m.holder === p) { AL.hip.rotation.x = -1.2; AR.hip.rotation.x = -1.2; AL.hip.rotation.z = 0.25; AR.hip.rotation.z = -0.25; }
    if (p.isGK && !act && m.holder !== p && m.phase === 'play' && speed < 2.5) {
      AL.hip.rotation.z = -0.55; AR.hip.rotation.z = 0.55; AL.knee.rotation.x = -0.9; AR.knee.rotation.x = -0.9;
      L.knee.rotation.x = 0.35; Rr.knee.rotation.x = 0.35; L.hip.rotation.x = -0.2; Rr.hip.rotation.x = -0.2; bob = -0.06;
    }
    a.torso.rotation.x = lean;
    a.pelvis.position.y = 0.95 + bob - (act && act.type === 'tackle' ? 0.08 : 0);
  }

  // ---------------------------------------------------------------- view
  class View {
    constructor(canvas, m) {
      this.m = m;
      const mobile = matchMedia('(pointer: coarse)').matches;
      this.renderer = new THREE.WebGLRenderer({ canvas, antialias: !mobile || devicePixelRatio < 2, powerPreference: 'high-performance' });
      this.renderer.setPixelRatio(Math.min(devicePixelRatio || 1, mobile ? 1.6 : 2));
      this.scene = new THREE.Scene();
      this.scene.background = new THREE.Color('#0a0f1a');
      this.scene.fog = new THREE.Fog('#0a0f1a', 110, 200);
      this.camera = new THREE.PerspectiveCamera(32, 1, 0.5, 400);
      this.scene.add(new THREE.HemisphereLight('#dfe8ff', '#20301f', 0.75));
      const sun = new THREE.DirectionalLight('#fff6e0', 0.75); sun.position.set(-30, 60, 40); this.scene.add(sun);
      const fill = new THREE.DirectionalLight('#cfe0ff', 0.3); fill.position.set(40, 40, -30); this.scene.add(fill);
      buildStadium(this.scene);

      const rng = E.makeRng(4242);
      this.athletes = m.players.map(p => {
        const a = buildAthlete(p, m.teams[p.team].kit, rng);
        this.scene.add(a.root); this.scene.add(a.shadow);
        return a;
      });
      this.ball = new THREE.Mesh(new THREE.SphereGeometry(0.16, 18, 12), new THREE.MeshLambertMaterial({ map: ballTexture() }));
      this.scene.add(this.ball);
      this.ballShadow = new THREE.Mesh(new THREE.PlaneGeometry(0.6, 0.6), new THREE.MeshBasicMaterial({ map: shadowTexture(), transparent: true, depthWrite: false }));
      this.ballShadow.rotation.x = -Math.PI / 2; this.scene.add(this.ballShadow);

      // User marker: ring on the ground plus a chevron above the head.
      this.ring = new THREE.Mesh(new THREE.RingGeometry(0.55, 0.72, 28), new THREE.MeshBasicMaterial({ color: '#f5d90a', transparent: true, opacity: 0.9, depthWrite: false }));
      this.ring.rotation.x = -Math.PI / 2; this.scene.add(this.ring);
      const chev = new THREE.ConeGeometry(0.22, 0.4, 3); chev.rotateX(Math.PI);
      this.chevron = new THREE.Mesh(chev, new THREE.MeshBasicMaterial({ color: '#f5d90a' }));
      this.scene.add(this.chevron);
      this.target = new THREE.Mesh(new THREE.RingGeometry(0.3, 0.42, 20), new THREE.MeshBasicMaterial({ color: '#ffffff', transparent: true, opacity: 0.5, depthWrite: false }));
      this.target.rotation.x = -Math.PI / 2; this.scene.add(this.target);

      this.camPos = new THREE.Vector3(0, 30, 50); this.camLook = new THREE.Vector3();
      this.resize();
    }
    resize() {
      const w = innerWidth, h = innerHeight;
      this.renderer.setSize(w, h, false);
      this.camera.aspect = w / h;
      this.camera.fov = w < h ? 52 : 32;
      this.camera.updateProjectionMatrix();
      this.portrait = w < h;
    }
    render(time, dt) {
      const m = this.m;
      m.players.forEach((p, i) => {
        const a = this.athletes[i];
        a.root.position.set(p.p.x, 0, p.p.z);
        a.root.rotation.y = Math.PI / 2 - p.facing;
        a.root.visible = true;
        poseAthlete(a, p, m, time);
        a.shadow.position.set(p.p.x + 0.25, 0.02, p.p.z + 0.15);
      });
      const b = m.ball;
      this.ball.position.set(b.p.x, Math.max(0.16, b.p.y + 0.05), b.p.z);
      const sp = Math.hypot(b.v.x, b.v.z);
      if (sp > 0.05) { this.ball.rotation.z -= b.v.x * dt / 0.16; this.ball.rotation.x += b.v.z * dt / 0.16; }
      this.ballShadow.position.set(b.p.x + b.p.y * 0.25, 0.025, b.p.z + b.p.y * 0.12);
      const sh = clamp(1 - b.p.y / 8, 0.3, 1); this.ballShadow.scale.set(sh, sh, 1);

      const u = m.user;
      this.ring.visible = this.chevron.visible = !!u && m.phase !== 'fulltime';
      if (u) {
        this.ring.position.set(u.p.x, 0.03, u.p.z);
        this.chevron.position.set(u.p.x, 2.35 + Math.sin(time * 5) * 0.06, u.p.z);
      }
      this.target.visible = !!(m.pass && m.pass.recv && m.pass.team === m.userTeam && m.owner === null);
      if (this.target.visible) { const r = m.pass.recv; this.target.position.set(r.p.x, 0.03, r.p.z); }

      // Broadcast camera following the ball from the main stand.
      const focusX = clamp(b.p.x, -C.HL + 8, C.HL - 8), focusZ = clamp(b.p.z, -C.HW, C.HW);
      const near = cfg.cam === 0 ? 0.8 : cfg.cam === 2 ? 1.25 : 1;
      const dist = (this.portrait ? 40 : 29) * near;
      const want = new THREE.Vector3(focusX * 0.9, (this.portrait ? 27 : 16.5) * near, focusZ * 0.35 + dist);
      const look = new THREE.Vector3(focusX * 0.95, 0, focusZ * 0.6 - 2);
      const k = 1 - Math.exp(-dt * 3.2);
      this.camPos.lerp(want, k); this.camLook.lerp(look, k);
      this.camera.position.copy(this.camPos); this.camera.lookAt(this.camLook);
      this.renderer.render(this.scene, this.camera);
    }
    dispose() { this.renderer.dispose(); }
  }

  // ---------------------------------------------------------------- audio (synthesized)
  const Audio = {
    ctx: null, crowd: null, crowdGain: null, master: null,
    init() {
      if (this.ctx || !cfg.sound) return;
      const AC = window.AudioContext || window.webkitAudioContext;
      if (!AC) return;
      try { this.ctx = new AC(); } catch (e) { return; }
      const c = this.ctx;
      this.master = c.createGain(); this.master.gain.value = 0.8; this.master.connect(c.destination);
      const len = c.sampleRate * 2;
      const buf = c.createBuffer(1, len, c.sampleRate);
      const d = buf.getChannelData(0);
      let last = 0;
      for (let i = 0; i < len; i++) { last = last * 0.97 + (Math.random() * 2 - 1) * 0.03; d[i] = last * 6 + (Math.random() * 2 - 1) * 0.05; }
      this.noise = buf;
      const src = c.createBufferSource(); src.buffer = buf; src.loop = true;
      const bp = c.createBiquadFilter(); bp.type = 'bandpass'; bp.frequency.value = 700; bp.Q.value = 0.5;
      this.crowdGain = c.createGain(); this.crowdGain.gain.value = 0.18;
      src.connect(bp); bp.connect(this.crowdGain); this.crowdGain.connect(this.master);
      src.start();
    },
    setEnabled(on) {
      cfg.sound = on; store.set('sound', on);
      if (on) { this.init(); if (this.ctx) this.ctx.resume(); } else if (this.ctx) this.ctx.suspend();
    },
    env(g, t, a, peak, dcy) { g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(peak, t + a); g.gain.exponentialRampToValueAtTime(0.0001, t + a + dcy); },
    thump(v, f0 = 140, f1 = 45, dur = 0.12) {
      const c = this.ctx, t = c.currentTime;
      const o = c.createOscillator(), g = c.createGain();
      o.frequency.setValueAtTime(f0, t); o.frequency.exponentialRampToValueAtTime(f1, t + dur);
      this.env(g, t, 0.004, 0.9 * v, dur); o.connect(g); g.connect(this.master); o.start(t); o.stop(t + dur + 0.05);
      const n = c.createBufferSource(); n.buffer = this.noise; const hp = c.createBiquadFilter(); hp.type = 'highpass'; hp.frequency.value = 1800;
      const ng = c.createGain(); this.env(ng, t, 0.002, 0.25 * v, 0.04); n.connect(hp); hp.connect(ng); ng.connect(this.master); n.start(t, Math.random()); n.stop(t + 0.08);
    },
    whistle(long) {
      const c = this.ctx, t = c.currentTime;
      const pulses = long ? [0, 0.45, 0.9] : [0];
      for (const p of pulses) {
        const o = c.createOscillator(), g = c.createGain(), lfo = c.createOscillator(), lg = c.createGain();
        o.frequency.value = 2900; lfo.frequency.value = 38; lg.gain.value = 120;
        lfo.connect(lg); lg.connect(o.frequency);
        const dur = long && p === 0.9 ? 0.8 : 0.32;
        g.gain.setValueAtTime(0.0001, t + p); g.gain.exponentialRampToValueAtTime(0.18, t + p + 0.02); g.gain.setValueAtTime(0.18, t + p + dur - 0.05); g.gain.exponentialRampToValueAtTime(0.0001, t + p + dur);
        o.connect(g); g.connect(this.master); o.start(t + p); lfo.start(t + p); o.stop(t + p + dur + 0.02); lfo.stop(t + p + dur + 0.02);
      }
    },
    roar(peak, dur, freq = 900) {
      const c = this.ctx, t = c.currentTime;
      const n = c.createBufferSource(); n.buffer = this.noise; n.loop = true;
      const bp = c.createBiquadFilter(); bp.type = 'bandpass'; bp.frequency.value = freq; bp.Q.value = 0.6;
      const g = c.createGain();
      g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(peak, t + 0.35); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
      n.connect(bp); bp.connect(g); g.connect(this.master); n.start(t, Math.random()); n.stop(t + dur + 0.1);
    },
    clang() {
      const c = this.ctx, t = c.currentTime;
      for (const f of [620, 1170, 1840, 2650]) {
        const o = c.createOscillator(), g = c.createGain(); o.type = 'triangle'; o.frequency.value = f;
        this.env(g, t, 0.002, 0.12, 0.7); o.connect(g); g.connect(this.master); o.start(t); o.stop(t + 0.8);
      }
    },
    play(e) {
      if (!this.ctx || !cfg.sound) return;
      switch (e.name) {
        case 'kick': this.thump(0.35 + e.v * 0.4); break;
        case 'shot': this.thump(0.9, 160, 40, 0.16); this.roar(0.25, 1.4, 1000); break;
        case 'touch': this.thump(0.25, 110, 60, 0.07); break;
        case 'header': this.thump(0.5, 220, 90, 0.08); break;
        case 'throw': this.thump(0.2, 90, 60, 0.06); break;
        case 'tackle': this.thump(0.55, 90, 35, 0.14); break;
        case 'save': this.thump(0.6, 120, 50, 0.1); this.roar(0.35, 1.6, 600); break;
        case 'dive': break;
        case 'post': this.clang(); this.roar(0.4, 1.8, 650); break;
        case 'goal': this.roar(0.75, 4.5, 950); this.roar(0.4, 4.0, 1700); break;
        case 'whistle': this.whistle(false); break;
        case 'whistle_end': this.whistle(true); break;
      }
    },
    update(m) {
      if (!this.ctx || !this.crowdGain) return;
      const ax = Math.abs(m.ball.p.x) / C.HL;
      const target = 0.14 + Math.pow(ax, 3) * 0.22;
      this.crowdGain.gain.setTargetAtTime(target, this.ctx.currentTime, 0.5);
    }
  };

  // ---------------------------------------------------------------- input
  const input = { mx: 0, mz: 0, sprint: false, keys: new Set(), stickId: null, stickOrigin: null, shootStart: 0, shootId: null };

  function setupInput() {
    const zone = $('stickZone'), knob = $('stickKnob'), base = $('stickBase');
    const R = 56;
    zone.addEventListener('pointerdown', e => {
      if (input.stickId !== null) return;
      input.stickId = e.pointerId; zone.setPointerCapture(e.pointerId);
      input.stickOrigin = { x: e.clientX, y: e.clientY };
      base.style.left = (e.clientX - 64) + 'px'; base.style.top = (e.clientY - 64) + 'px';
      base.classList.add('on');
      knob.style.transform = 'translate(0px,0px)';
    });
    zone.addEventListener('pointermove', e => {
      if (e.pointerId !== input.stickId) return;
      let dx = e.clientX - input.stickOrigin.x, dy = e.clientY - input.stickOrigin.y;
      const d = Math.hypot(dx, dy);
      if (d > R) { dx *= R / d; dy *= R / d; }
      knob.style.transform = `translate(${dx}px,${dy}px)`;
      input.mx = dx / R; input.mz = dy / R;
    });
    const end = e => {
      if (e.pointerId !== input.stickId) return;
      input.stickId = null; input.mx = 0; input.mz = 0;
      base.classList.remove('on');
    };
    zone.addEventListener('pointerup', end); zone.addEventListener('pointercancel', end);

    const press = kind => { if (match && !paused) match.input.presses.push({ kind, power: 0.7 }); };
    document.querySelectorAll('[data-act]').forEach(btn => {
      const kind = btn.dataset.act;
      btn.addEventListener('pointerdown', e => {
        e.preventDefault();
        btn.classList.add('down');
        Audio.init();
        if (kind === 'sprint') { input.sprint = true; return; }
        if (kind === 'shoot' && context() === 'attack') { input.shootStart = performance.now(); input.shootId = e.pointerId; btn.setPointerCapture(e.pointerId); return; }
        press(kind);
      });
      const up = e => {
        btn.classList.remove('down');
        if (kind === 'sprint') { input.sprint = false; return; }
        if (kind === 'shoot' && input.shootId === e.pointerId) {
          const power = clamp((performance.now() - input.shootStart) / 900, 0.2, 1);
          input.shootId = null; input.shootStart = 0;
          if (match && !paused) match.input.presses.push({ kind: 'shoot', power });
        }
      };
      btn.addEventListener('pointerup', up); btn.addEventListener('pointercancel', up);
      btn.addEventListener('contextmenu', e => e.preventDefault());
    });

    const keyAct = { KeyJ: 'pass', KeyK: 'shoot', KeyL: 'through', KeyI: 'lob', Space: 'switch' };
    addEventListener('keydown', e => {
      if (e.repeat) return;
      if (e.code === 'Escape' || e.code === 'KeyP') { if (running) togglePause(); return; }
      input.keys.add(e.code);
      if (e.code === 'ShiftLeft' || e.code === 'ShiftRight') input.sprint = true;
      const k = keyAct[e.code];
      if (!k || !match || paused) return;
      e.preventDefault();
      Audio.init();
      if (k === 'shoot' && context() === 'attack') { input.shootStart = performance.now(); input.shootKey = true; return; }
      match.input.presses.push({ kind: k, power: 0.7 });
    });
    addEventListener('keyup', e => {
      input.keys.delete(e.code);
      if (e.code === 'ShiftLeft' || e.code === 'ShiftRight') input.sprint = false;
      if (e.code === 'KeyK' && input.shootKey) {
        input.shootKey = false;
        const power = clamp((performance.now() - input.shootStart) / 900, 0.2, 1);
        input.shootStart = 0;
        if (match && !paused) match.input.presses.push({ kind: 'shoot', power });
      }
    });
  }

  function readKeys() {
    const k = input.keys;
    let x = 0, z = 0;
    if (k.has('KeyA') || k.has('ArrowLeft')) x -= 1;
    if (k.has('KeyD') || k.has('ArrowRight')) x += 1;
    if (k.has('KeyW') || k.has('ArrowUp')) z -= 1;
    if (k.has('KeyS') || k.has('ArrowDown')) z += 1;
    return { x, z };
  }

  function context() {
    const m = match;
    if (!m || !m.user) return 'defend';
    if (m.phase === 'restart' && m.restart && m.restart.taker === m.user) return m.restart.type === 'corner' ? 'corner' : 'restart';
    if (m.owner === m.user) return 'attack';
    const pt = m.possessionTeam();
    if (pt === m.userTeam && (m.owner || m.holder || m.pass)) return 'support';
    return 'defend';
  }

  const LABELS = {
    attack: { shoot: 'CHUTE', pass: 'PASSE', through: 'ENFIADA', lob: 'LANÇAR' },
    support: { shoot: 'CHUTE', pass: 'PEDIR', through: 'INFILTRAR', lob: 'LANÇAR' },
    defend: { shoot: 'CARRINHO', pass: 'TROCAR', through: 'DESARME', lob: 'PRESSÃO' },
    restart: { shoot: 'LONGO', pass: 'PASSE', through: 'ENFIADA', lob: 'LANÇAR' },
    corner: { shoot: 'CRUZAR', pass: 'CURTO', through: 'CURTO', lob: 'CRUZAR' }
  };

  // ---------------------------------------------------------------- HUD
  function teamChip(t) { return `<i style="background:${t.kit.shirt};box-shadow:inset 0 -4px 0 ${t.kit.shorts}"></i>`; }

  let lastCtx = '';
  function updateHud() {
    const m = match;
    $('sbHome').innerHTML = teamChip(m.teams[0]) + m.teams[0].club.id;
    $('sbAway').innerHTML = m.teams[1].club.id + teamChip(m.teams[1]);
    $('sbScore').textContent = m.teams[0].stats.goals + ' – ' + m.teams[1].stats.goals;
    $('sbClock').textContent = m.clockText() + "'";
    const ctx = context();
    if (ctx !== lastCtx) {
      lastCtx = ctx;
      const l = LABELS[ctx];
      document.querySelectorAll('[data-act]').forEach(b => { if (l[b.dataset.act]) b.querySelector('span').textContent = l[b.dataset.act]; });
      $('controls').dataset.ctx = ctx;
    }
    const charging = input.shootStart > 0;
    const pw = charging ? clamp((performance.now() - input.shootStart) / 900, 0, 1) : 0;
    $('power').style.setProperty('--p', pw.toFixed(3));
    $('power').classList.toggle('on', charging);
    $('userName').textContent = m.user ? `#${m.user.number} ${m.user.name}` : '';
    while (lastFeed < m.feed.length) {
      const f = m.feed[lastFeed++];
      if (f.kind === 'goal') toast(f.text, 'goal', 3200, f.team);
      else if (f.kind === 'save' || f.kind === 'post') toast(f.text, '', 1600, f.team);
      else if (f.kind === 'corner' || f.kind === 'goalkick') toast(f.text, 'small', 1200, f.team);
    }
    if (m.phase === 'halftime' && !$('halfScreen').classList.contains('on')) showBreak(false);
    if (m.phase === 'fulltime' && !$('endScreen').classList.contains('on')) showBreak(true);
  }

  let toastTimer = 0;
  function toast(text, cls, ms, team) {
    const el = $('toast');
    el.className = 'toast on ' + (cls || '');
    const col = team != null && match ? match.teams[team].kit.shirt : null;
    el.style.setProperty('--team', col || '#f5d90a');
    el.textContent = text;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => el.classList.remove('on'), ms);
  }

  function statsTable(m) {
    const a = m.teams[0].stats, b = m.teams[1].stats;
    const poss = a.poss / Math.max(1, a.poss + b.poss);
    const pct = s => s.passes ? Math.round(s.completed / s.passes * 100) + '%' : '–';
    const rows = [['Posse de bola', Math.round(poss * 100) + '%', Math.round((1 - poss) * 100) + '%'], ['Finalizações', a.shots, b.shots], ['No alvo', a.onTarget, b.onTarget], ['xG', a.xg.toFixed(1), b.xg.toFixed(1)], ['Passes certos', pct(a), pct(b)], ['Desarmes', a.tackles, b.tackles], ['Defesas do goleiro', a.saves, b.saves], ['Escanteios', a.corners, b.corners]];
    const scorers = t => m.feed.filter(f => f.kind === 'goal' && f.team === t).map(f => `<li>${f.text.replace('GOL! ', '')} <span>${f.min}'</span></li>`).join('');
    return `<div class="final"><div>${teamChip(m.teams[0])}<b>${m.teams[0].club.name}</b></div><strong>${a.goals} – ${b.goals}</strong><div><b>${m.teams[1].club.name}</b>${teamChip(m.teams[1])}</div></div>
      <div class="scorers"><ul>${scorers(0)}</ul><ul>${scorers(1)}</ul></div>
      <table>${rows.map(r => `<tr><td>${r[1]}</td><th>${r[0]}</th><td>${r[2]}</td></tr>`).join('')}</table>`;
  }

  function showBreak(end) {
    const el = $(end ? 'endScreen' : 'halfScreen');
    el.querySelector('.statsBox').innerHTML = statsTable(match);
    el.classList.add('on');
    $('controls').classList.add('hidden');
  }

  // ---------------------------------------------------------------- flow
  function startMatch() {
    Audio.init();
    if (Audio.ctx && Audio.ctx.state === 'suspended') Audio.ctx.resume();
    if (view) { view.dispose(); view = null; }
    match = new E.Match({ home: cfg.home, away: cfg.away, halfMinutes: cfg.half, difficulty: cfg.diff, userTeam: 0, seed: (Date.now() & 0xffffff) | 1 });
    lastFeed = 0; lastCtx = '';
    view = new View($('gl'), match);
    $('menu').classList.remove('on');
    ['halfScreen', 'endScreen', 'pauseScreen'].forEach(id => $(id).classList.remove('on'));
    $('controls').classList.remove('hidden');
    $('hud').classList.remove('hidden');
    paused = false; running = true;
    toast('Toque em PASSE para dar a saída', 'small', 2500, 0);
  }

  function togglePause(force) {
    if (!running || match.phase === 'fulltime' || match.phase === 'halftime') return;
    paused = force != null ? force : !paused;
    $('pauseScreen').classList.toggle('on', paused);
    $('controls').classList.toggle('hidden', paused);
    if (Audio.ctx) paused ? Audio.ctx.suspend() : (cfg.sound && Audio.ctx.resume());
  }

  function toMenu() {
    running = false; paused = false;
    ['halfScreen', 'endScreen', 'pauseScreen'].forEach(id => $(id).classList.remove('on'));
    $('controls').classList.add('hidden'); $('hud').classList.add('hidden');
    $('menu').classList.add('on');
    renderMenu();
  }

  let acc = 0, prev = 0;
  function frame(now) {
    requestAnimationFrame(frame);
    const dt = Math.min(0.1, (now - (prev || now)) / 1000);
    prev = now;
    if (!view) return;
    if (running && !paused) {
      const k = readKeys();
      const usingKeys = k.x !== 0 || k.z !== 0;
      match.input.mx = usingKeys ? k.x : input.mx;
      match.input.mz = usingKeys ? k.z : input.mz;
      match.input.sprint = input.sprint;
      acc += dt;
      let n = 0;
      while (acc >= 1 / 60 && n < 6) { match.step(1 / 60); acc -= 1 / 60; n++; }
      if (n === 6) acc = 0;
      for (const s of match.sfx) Audio.play(s);
      match.sfx.length = 0;
      Audio.update(match);
      updateHud();
    }
    view.render(now / 1000, running && !paused ? dt : 0);
  }

  // ---------------------------------------------------------------- menu
  function renderMenu() {
    const club = id => E.CLUBS.find(c => c.id === id);
    const h = club(cfg.home), a = club(cfg.away);
    const chip = c => `<i style="background:${c.shirt};box-shadow:inset 0 -5px 0 ${c.shorts}"></i>`;
    $('pickHome').innerHTML = chip(h) + `<b>${h.name}</b><small>Força ${h.strength} · ${h.formation}</small>`;
    $('pickAway').innerHTML = chip(a) + `<b>${a.name}</b><small>Força ${a.strength} · ${a.formation}</small>`;
    document.querySelectorAll('[data-half]').forEach(b => b.classList.toggle('sel', +b.dataset.half === cfg.half));
    document.querySelectorAll('[data-diff]').forEach(b => b.classList.toggle('sel', +b.dataset.diff === cfg.diff));
    $('soundBtn').textContent = cfg.sound ? 'Som: ligado' : 'Som: desligado';
    $('pSound').textContent = cfg.sound ? 'Som: ligado' : 'Som: desligado';
    $('pCam').textContent = 'Câmera: ' + ['perto', 'padrão', 'longe'][cfg.cam];
  }
  function cycleClub(which, dir) {
    const ids = E.CLUBS.map(c => c.id);
    let i = ids.indexOf(cfg[which]);
    const other = which === 'home' ? cfg.away : cfg.home;
    do { i = (i + dir + ids.length) % ids.length; } while (ids[i] === other);
    cfg[which] = ids[i]; store.set(which, cfg[which]);
    renderMenu();
  }

  function setupMenu() {
    $('homePrev').onclick = () => cycleClub('home', -1);
    $('homeNext').onclick = () => cycleClub('home', 1);
    $('awayPrev').onclick = () => cycleClub('away', -1);
    $('awayNext').onclick = () => cycleClub('away', 1);
    document.querySelectorAll('[data-half]').forEach(b => b.onclick = () => { cfg.half = +b.dataset.half; store.set('half', cfg.half); renderMenu(); });
    document.querySelectorAll('[data-diff]').forEach(b => b.onclick = () => { cfg.diff = +b.dataset.diff; store.set('diff', cfg.diff); renderMenu(); });
    $('soundBtn').onclick = () => { Audio.setEnabled(!cfg.sound); renderMenu(); };
    $('playBtn').onclick = startMatch;
    $('pauseBtn').onclick = () => togglePause();
    $('pResume').onclick = () => togglePause(false);
    $('pRestart').onclick = startMatch;
    $('pMenu').onclick = toMenu;
    $('pSound').onclick = () => { Audio.setEnabled(!cfg.sound); renderMenu(); };
    $('pCam').onclick = () => { cfg.cam = (cfg.cam + 1) % 3; store.set('cam', cfg.cam); renderMenu(); };
    $('secondHalf').onclick = () => {
      $('halfScreen').classList.remove('on'); $('controls').classList.remove('hidden');
      match.continueSecondHalf();
    };
    $('again').onclick = startMatch;
    $('endMenu').onclick = toMenu;
    renderMenu();
  }

  function boot() {
    if (!window.THREE) { $('bootError').hidden = false; return; }
    setupMenu();
    setupInput();
    addEventListener('resize', () => view && view.resize());
    document.addEventListener('visibilitychange', () => { if (document.hidden && running && !paused) togglePause(true); });
    // Menu backdrop: an AI-vs-AI match running behind the title.
    match = new E.Match({ home: cfg.home, away: cfg.away, halfMinutes: 6, userTeam: -1, seed: 77 });
    view = new View($('gl'), match);
    const demo = () => { if (!running && match && match.userTeam === -1) { for (let i = 0; i < 2; i++) match.step(1 / 60); if (match.phase === 'halftime') match.continueSecondHalf(); match.sfx.length = 0; } };
    setInterval(demo, 1000 / 30);
    requestAnimationFrame(frame);
    window.__nxi = { get match() { return match; }, startMatch, input, cfg };
  }
  boot();
})();
