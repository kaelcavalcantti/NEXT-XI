// Penalty-box shooting gallery: AI keeper vs shots from a given distance.
const E = require('../src/engine.js');
const V = E.V;
for (const [dist, power, h] of [[20, 0.85, 0.3], [20, 0.85, 1.4], [12, 0.7, 0.3], [25, 0.9, 1.0]]) {
  const out = {};
  for (let i = 0; i < 60; i++) {
    const m = new E.Match({ seed: 50 + i, userTeam: -1 });
    m.phase = 'play'; m.restart = null;
    // clear the pitch: everyone except shooter (team 0 #9) and keeper (team 1 GK) far away
    for (const p of m.players) { p.p = V(-40, 0, 30); p.target = p.p; }
    const sh = m.teams[0].players[9], gk = m.teams[1].players[0];
    const goalX = m.attackDir(0) * 52.5;
    sh.p = V(goalX - Math.sign(goalX) * dist, 0, (i % 5 - 2) * 3); sh.facing = goalX > 0 ? 0 : Math.PI;
    gk.p = V(goalX - Math.sign(goalX) * 1.5, 0, 0); gk.target = gk.p;
    for (const p of m.players) if (p !== sh && p !== gk) { p.think = () => {}; }
    m.think = function (p, dt) { if (p === gk) this.keeperThink(p, dt); else p.target = p.p; };
    m.giveBall(sh);
    const z = (i % 2 ? 1 : -1) * (3.66 - 0.8);
    m.startKick(sh, { kind: 'shot', target: V(goalX, h, z), power });
    let res = 'none';
    const origGoal = m.goal.bind(m); m.goal = () => { res = 'goal'; m.phase = 'x'; };
    const oc = m.catchBall.bind(m); m.catchBall = g => { res = 'catch'; oc(g); };
    m.beginRestart = () => { res = res === 'none' ? (m.teams[1].stats.saves ? 'parried-out' : 'miss') : res; m.phase = 'x'; };
    for (let s = 0; s < 240 && m.phase === 'play'; s++) m.step(1 / 60);
    if (res === 'none' && m.teams[1].stats.saves) res = 'parry';
    out[res] = (out[res] || 0) + 1;
  }
  console.log(dist + 'm power ' + power + ' h ' + h, out);
}
// reach diagnostics
{
  const E2 = require('../src/engine.js');
  let reached = 0, fail = 0, tot = 0, dives = 0;
  for (let i = 0; i < 60; i++) {
    const m = new E2.Match({ seed: 50 + i, userTeam: -1 });
    m.phase = 'play'; m.restart = null;
    for (const p of m.players) { p.p = V(-40, 0, 30); p.target = p.p; }
    const sh = m.teams[0].players[9], gk = m.teams[1].players[0];
    const goalX = 52.5;
    sh.p = V(goalX - 16, 0, (i % 5 - 2) * 3); sh.facing = 0;
    gk.p = V(goalX - 1.5, 0, 0);
    m.think = function (p, dt) { if (p === gk) this.keeperThink(p, dt); else p.target = p.p; };
    m.giveBall(sh);
    m.startKick(sh, { kind: 'shot', target: V(goalX, 0.5, (i % 2 ? 1 : -1) * 2.8), power: 0.8 });
    const kt = m.keeperTouch.bind(m); let touched = false; m.keeperTouch = g => { touched = true; const sv = m.teams[1].stats.saves; kt(g); if (m.teams[1].stats.saves === sv) fail++; };
    const sd = m.startDive.bind(m); m.startDive = (...a) => { dives++; sd(...a); };
    let on = false;
    m.goal = () => { m.phase = 'x'; }; m.beginRestart = () => { m.phase = 'x'; };
    for (let s = 0; s < 14; s++) m.step(1 / 60);
    on = m.shotOnTarget(0);
    for (let s = 0; s < 200 && m.phase === 'play'; s++) m.step(1 / 60);
    if (on) { tot++; if (touched) reached++; } if (on && !touched && i < 8) console.log("miss reach: gk", gk.p.x.toFixed(1), gk.p.z.toFixed(1), "ball", m.ball.p.z.toFixed(1), m.ball.p.y.toFixed(1));
  }
  console.log(`16m on target ${tot}, keeper reached ${reached}, failed saves ${fail}, dives ${dives}`);
}
