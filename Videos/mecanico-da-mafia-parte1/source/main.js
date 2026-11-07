import * as THREE from 'three';
import { makeSky, clamp, sm, eo } from './lib.js';
import { setAvenue, setWorkshop, setStreet, setGarage } from './sets.js';

const W = 1080, H = 1920;
async function init() {
  await Promise.all(['Anton', '"Bebas Neue"'].map(f => document.fonts.load(`40px ${f}`, 'ÁÉÍÓÚÃÕÇáéíóúãõç ABC')));
  const TLraw = await (await fetch('timeline.json')).json();
  const sc = {}; for (const s of TLraw.scenes) sc[s.id] = s;
  const TL = { dur: {}, env: {}, raw: TLraw };
  for (const s of TLraw.scenes) TL.dur[s.id] = s.end - s.start;
  for (const l of TLraw.lines) if (l.env) TL.env[l.k] = l.env;
  TL.h3 = TLraw.lines.find(l => l.k === 'h3').start - sc.s3.start;

  const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
  renderer.setPixelRatio(1); renderer.setSize(W, H);
  renderer.shadowMap.enabled = true; renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.toneMapping = THREE.NeutralToneMapping; renderer.toneMappingExposure = 1.12;
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  // environment from the painted sky
  const pm = new THREE.PMREMGenerator(renderer);
  const envScene = new THREE.Scene(); envScene.add(makeSky([-.4, .2, -1], true));
  const env = pm.fromScene(envScene, 0.02, 1, 1000).texture;

  const cam = new THREE.PerspectiveCamera(45, W / H, .05, 1500);
  const sets = { av: setAvenue(env, TL), ws: setWorkshop(env, TL), st: setStreet(env, TL), gr: setGarage(env, TL) };
  const map = {
    s1: [sets.av, (t) => sets.av.update(t, cam)],
    s2: [sets.ws, (t) => sets.ws.update(t, cam, 's2')],
    s3: [sets.st, (t) => sets.st.update(t, cam, TL)],
    s4: [sets.ws, (t) => sets.ws.update(t, cam, 's4')],
    s5: [sets.gr, (t) => sets.gr.update(t, cam, 's5', TL)],
    s6: [sets.gr, (t) => sets.gr.update(t, cam, 's6', TL)],
  };
  // ---- 2D compositor ----
  const comp = document.createElement('canvas'); comp.width = W; comp.height = H; const cx = comp.getContext('2d');
  document.body.appendChild(comp);
  const vig = document.createElement('canvas'); vig.width = W; vig.height = H; { const v = vig.getContext('2d'); const g = v.createRadialGradient(W / 2, H * .48, H * .25, W / 2, H / 2, H * .75); g.addColorStop(0, 'rgba(20,6,2,0)'); g.addColorStop(1, 'rgba(20,6,2,.55)'); v.fillStyle = g; v.fillRect(0, 0, W, H); }
  // captions: chunk words
  const chunks = [];
  for (const l of TLraw.lines) {
    let cur = [];
    const flush = () => { if (cur.length) { chunks.push({ line: l, words: cur }); cur = []; } };
    for (const w of l.words) { cur.push(w); const len = cur.map(x => x.w).join(' ').length; if (/[.,?!]$/.test(w.w) || cur.length >= 3 || len > 15) flush(); }
    flush();
  }
  chunks.forEach(c => c.t0 = c.words[0].t);
  chunks.forEach((c, i) => { const nx = chunks[i + 1]; const lend = c.line.start + c.line.dur; c.t1 = nx && nx.line === c.line ? nx.t0 : lend + .15; });
  let lastId = null, lastCut = -10;
  const cuts = [];
  window.dbgChunks = chunks;
  function caption(t) {
    const c = chunks.find(c => t >= c.t0 - .02 && t < c.t1); if (!c) return;
    const sp = c.line.speaker;
    const age = t - c.t0; const s = 1 + .14 * (1 - eo(clamp(age / .12)));
    const y = H * .69;
    cx.save(); cx.translate(W / 2, y); cx.scale(s, s);
    cx.font = '96px Anton'; cx.textAlign = 'center'; cx.textBaseline = 'middle'; cx.lineJoin = 'round';
    const words = c.words.map(w => w.w.toUpperCase());
    const space = 26; const widths = words.map(w => cx.measureText(w).width); let total = widths.reduce((a, b) => a + b, 0) + space * (words.length - 1);
    let sc2 = 1; if (total > W * .88) { sc2 = W * .88 / total; cx.scale(sc2, sc2); }
    let x = -total / 2;
    let cur = 0; c.words.forEach((w, i) => { if (t >= w.t - .02) cur = i; });
    words.forEach((w, i) => {
      const wx = x + widths[i] / 2;
      cx.lineWidth = 18; cx.strokeStyle = '#120806'; cx.strokeText(w, wx, 6);
      cx.fillStyle = i === cur ? (sp === 'n' ? '#FFD23F' : '#FF8A5C') : '#FFFFFF';
      cx.fillText(w, wx, 0);
      x += widths[i] + space;
    });
    cx.restore();
    if (sp !== 'n') { // speaker tag
      const tag = sp === 'h' ? 'HOMEM' : '📞 VOZ NO TELEFONE';
      cx.font = '44px Anton'; const tw = cx.measureText(tag).width;
      cx.fillStyle = '#D97757'; cx.strokeStyle = '#120806'; cx.lineWidth = 6;
      const bx = W / 2 - tw / 2 - 22, by = y - 130;
      cx.beginPath(); cx.roundRect(bx, by, tw + 44, 66, 14); cx.fill(); cx.stroke();
      cx.fillStyle = '#120806'; cx.textAlign = 'center'; cx.textBaseline = 'middle'; cx.fillText(tag, W / 2, by + 35);
    }
  }
  function titleCards(t) {
    cx.textAlign = 'center'; cx.textBaseline = 'middle'; cx.lineJoin = 'round';
    // hook
    if (t < 3.6) {
      const a = clamp(t / .25) * (1 - sm(3.2, 3.6, t)); const s = 1 + .2 * (1 - eo(clamp(t / .3)));
      cx.save(); cx.globalAlpha = a; cx.translate(W / 2, 300); cx.scale(s, s);
      cx.font = '104px Anton'; cx.lineWidth = 20; cx.strokeStyle = '#120806';
      for (const [txt, yy, col] of [['E SE VOCÊ FOSSE O', -70, '#FFFFFF'], ['MECÂNICO DA MÁFIA?', 50, '#FFD23F']]) { cx.strokeText(txt, 0, yy); cx.fillStyle = col; cx.fillText(txt, 0, yy); }
      cx.restore();
    }
    // location
    if (t > 3.4 && t < 7.0) {
      const a = sm(3.4, 3.7, t) * (1 - sm(6.6, 7.0, t));
      cx.save(); cx.globalAlpha = a; cx.font = '64px "Bebas Neue"';
      const txt = 'GUADALAJARA, MÉXICO'; const n = Math.floor(clamp((t - 3.5) / 1.0) * txt.length);
      const shown = txt.slice(0, n); const tw = cx.measureText(txt).width;
      cx.fillStyle = 'rgba(18,8,6,.72)'; cx.fillRect(W / 2 - tw / 2 - 30, 228, tw + 60, 84);
      cx.fillStyle = '#D97757'; cx.fillRect(W / 2 - tw / 2 - 30, 228, 10, 84);
      cx.fillStyle = '#F7E9C9'; cx.textAlign = 'left'; cx.fillText(shown, W / 2 - tw / 2, 272);
      cx.restore();
    }
    // part badge
    if (t < TL.raw.scenes.find(s => s.id === 'end').start) {
      cx.save(); cx.font = '40px "Bebas Neue"'; cx.textAlign = 'left'; const txt = 'PARTE 1 · O ACORDO'; const tw = cx.measureText(txt).width;
      cx.globalAlpha = .9; cx.fillStyle = '#D97757'; cx.beginPath(); cx.roundRect(48, 110, tw + 36, 56, 10); cx.fill();
      cx.fillStyle = '#120806'; cx.fillText(txt, 66, 140); cx.restore();
    }
  }
  function endCard(t, te) {
    const u = t - te;
    const g = cx.createRadialGradient(W / 2, H / 2, 50, W / 2, H / 2, H * .7); g.addColorStop(0, '#3a0d06'); g.addColorStop(1, '#070202'); cx.fillStyle = g; cx.fillRect(0, 0, W, H);
    const s = 1 + .25 * (1 - eo(clamp(u / .35)));
    cx.save(); cx.translate(W / 2, H * .46); cx.scale(s, s); cx.textAlign = 'center'; cx.textBaseline = 'middle'; cx.lineJoin = 'round';
    cx.font = '150px Anton'; cx.lineWidth = 22; cx.strokeStyle = '#000';
    cx.strokeText('CONTINUA', 0, -90); cx.fillStyle = '#F7E9C9'; cx.fillText('CONTINUA', 0, -90);
    const dots = '.'.repeat(1 + Math.floor(u * 3) % 3);
    cx.strokeText('NA PARTE 2' + '…', 0, 80); cx.fillStyle = '#D97757'; cx.fillText('NA PARTE 2…', 0, 80);
    cx.restore();
    cx.globalAlpha = sm(.6, 1.0, u); cx.font = '54px "Bebas Neue"'; cx.fillStyle = '#F7E9C9'; cx.textAlign = 'center'; cx.fillText('SEGUE PRA NÃO PERDER O QUE ACONTECE COM ALEJANDRO', W / 2, H * .62); cx.globalAlpha = 1;
  }
  window.renderAt = (t) => {
    const end = TL.raw.scenes.find(s => s.id === 'end');
    if (t >= end.start) { endCard(t, end.start); return comp.toDataURL('image/jpeg', .93); }
    const s = TLraw.scenes.find(s => t >= s.start && t < s.end) || TLraw.scenes[0];
    const [set, fn] = map[s.id];
    const lt = t - s.start;
    const id = s.id + fn(lt);
    if (id !== lastId) { lastCut = t; lastId = id; }
    set.sky.position.copy(cam.position);
    renderer.render(set.scene, cam);
    // punch-in on cuts + flash
    const since = t - lastCut; const punch = 1 + .05 * (1 - eo(clamp(since / .22)));
    cx.save(); cx.translate(W / 2, H / 2); cx.scale(punch, punch); cx.drawImage(renderer.domElement, -W / 2, -H / 2); cx.restore();
    cx.drawImage(vig, 0, 0);
    if (since < .08 && t > .1) { cx.fillStyle = `rgba(255,230,200,${.25 * (1 - since / .08)})`; cx.fillRect(0, 0, W, H); }
    // fade from/to black around the final dolly
    const s6 = TLraw.scenes.find(x => x.id === 's6');
    if (t > s6.end - .35) { cx.fillStyle = `rgba(0,0,0,${sm(s6.end - .35, s6.end, t)})`; cx.fillRect(0, 0, W, H); }
    if (t < .25) { cx.fillStyle = `rgba(0,0,0,${1 - t / .25})`; cx.fillRect(0, 0, W, H); }
    titleCards(t); caption(t);
    return comp.toDataURL('image/jpeg', .93);
  };
  window.total = TLraw.total; window.dbgCam = () => [cam.position.toArray(), cam.fov, sets.st.bru.root.position.toArray(), sets.st.bru.root.visible, sets.st.bru.root.parent && sets.st.bru.root.parent.type];
  window.ready = true;
}
init().catch(e => { window.err = String(e.stack || e); });
