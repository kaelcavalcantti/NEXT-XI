const E = require('../src/engine.js');
const m = new E.Match({ seed: 7, halfMinutes: 3, userTeam: -1 });
let cur = null; let shown = 0;
const oa = m.applyKick.bind(m);
m.applyKick = (p, k) => {
  const snap = m.players.map(q => ({ id: q.id, team: q.team, x: q.p.x, z: q.p.z }));
  const from = { x: m.ball.p.x, z: m.ball.p.z };
  oa(p, k);
  cur = (k.kind === 'pass' && k.recv) ? { p, k, snap, from, t: m.time, v: { ...m.ball.v }, risk: m.passRisk(from, k.target, p.team, 10), recvPos: { x: k.recv.p.x, z: k.recv.p.z } } : null;
};
const og = m.giveBall.bind(m);
m.giveBall = q => {
  if (cur && q.team !== cur.p.team && shown < 8) {
    shown++;
    const s = cur.snap.find(o => o.id === q.id);
    const dx = cur.k.target.x - cur.from.x, dz = cur.k.target.z - cur.from.z, d = Math.hypot(dx, dz);
    const rx = s.x - cur.from.x, rz = s.z - cur.from.z;
    const along = (rx * dx + rz * dz) / d, perp = Math.abs(rx * dz - rz * dx) / d;
    console.log(`pass d=${d.toFixed(1)} risk=${cur.risk.toFixed(2)} spd=${Math.hypot(cur.v.x, cur.v.z).toFixed(1)} | interceptor along=${along.toFixed(1)} perp=${perp.toFixed(1)} after ${(m.time - cur.t).toFixed(2)}s at ball ${m.ball.p.x.toFixed(1)},${m.ball.p.z.toFixed(1)} | recv now ${cur.k.recv.p.x.toFixed(1)},${cur.k.recv.p.z.toFixed(1)} target ${cur.k.target.x.toFixed(1)},${cur.k.target.z.toFixed(1)}`);
  }
  cur = null; og(q);
};
while (m.phase !== 'fulltime') { if (m.phase === 'halftime') m.continueSecondHalf(); m.step(1 / 60); }
