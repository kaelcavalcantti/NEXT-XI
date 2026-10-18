const E = require('../src/engine.js');
const m = new E.Match({ seed: 7, halfMinutes: 3, userTeam: -1 });
const passes = [];
const origApply = m.applyKick.bind(m);
m.applyKick = (p, k) => { origApply(p, k); if (m.pass) passes.push({ kind: k.kind, d: k.target ? Math.hypot(k.target.x - p.p.x, k.target.z - p.p.z) : 0, team: p.team, t: m.time, recvId: k.recv ? k.recv.id : -1, res: null }); if (k.kind === 'shot' || k.isShot) shots.push({ t: m.time, team: p.team, d: Math.hypot(m.goalOf(p.team).x - p.p.x, p.p.z), v: Math.hypot(m.ball.v.x, m.ball.v.y, m.ball.v.z), res: null }); };
const shots = [];
const origGive = m.giveBall.bind(m);
m.giveBall = (p) => { const last = passes[passes.length - 1]; if (last && !last.res) last.res = p.team === last.team ? (p.id === last.recvId ? 'recv' : 'mate') : 'opp'; const s = shots[shots.length-1]; if (s && !s.res) s.res = 'gained:' + (p.team===s.team?'att':'def'); origGive(p); };
const origCatch = m.catchBall.bind(m);
m.catchBall = (gk) => { const last = passes[passes.length - 1]; if (last && !last.res) last.res = gk.team === last.team ? 'mate' : 'gk'; const s = shots[shots.length-1]; if (s && !s.res) s.res = 'caught'; origCatch(gk); };
const origRestart = m.beginRestart.bind(m);
m.beginRestart = (type, team, spot) => { const last = passes[passes.length - 1]; if (last && !last.res) last.res = 'out'; const s = shots[shots.length-1]; if (s && !s.res) s.res = 'out'; origRestart(type, team, spot); };
const origGoal = m.goal.bind(m);
m.goal = (side) => { const s = shots[shots.length-1]; if (s && !s.res) s.res = 'GOAL'; origGoal(side); };
while (m.phase !== 'fulltime') { if (m.phase === 'halftime') m.continueSecondHalf(); m.step(1/60); }
const by = {};
for (const p of passes) { const k = p.kind + ' ' + (p.d < 15 ? '<15' : p.d < 30 ? '<30' : '30+'); by[k] = by[k] || {}; by[k][p.res] = (by[k][p.res] || 0) + 1; }
console.log(by);
console.log(shots.map(s => `${s.d.toFixed(0)}m ${s.v.toFixed(0)}m/s ${s.res}`).join('\n'));
