// Headless check of the browser preview engine: AI vs AI matches plus a random "user".
const E = require('../src/engine.js');
const n = +(process.argv[2] || 6), halfMin = +(process.argv[3] || 3), withUser = process.argv[4] === 'user';
let goals = 0, t0 = Date.now(), problems = 0;
for (let i = 0; i < n; i++) {
  const m = new E.Match({ seed: 100 + i, halfMinutes: halfMin, userTeam: withUser ? 0 : -1, home: E.CLUBS[i % 8].id, away: E.CLUBS[(i + 3) % 8].id });
  const rng = E.makeRng(9 + i);
  let steps = 0, still = 0, lastP = null, maxStill = 0;
  while (m.phase !== 'fulltime' && steps < 200000) {
    if (m.phase === 'halftime') m.continueSecondHalf();
    if (withUser && steps % 20 === 0) {
      m.input.mx = rng.range(-1, 1); m.input.mz = rng.range(-1, 1); m.input.sprint = rng.chance(0.5);
      if (rng.chance(0.15)) m.input.presses.push({ kind: rng.pick(['pass', 'through', 'shoot', 'lob', 'switch']), power: rng.range(0.3, 1) });
    }
    m.step(1 / 60); steps++;
    const b = m.ball.p;
    if (!isFinite(b.x + b.y + b.z)) { console.log('NaN ball'); problems++; break; }
    if (lastP && Math.hypot(b.x - lastP.x, b.z - lastP.z) < 0.002 && m.phase === 'play' && !m.holder && !m.owner) still++; else still = 0;
    maxStill = Math.max(maxStill, still);
    lastP = { x: b.x, z: b.z };
    for (const p of m.players) if (!isFinite(p.p.x + p.p.z)) { console.log('NaN player'); problems++; }
  }
  const s0 = m.teams[0].stats, s1 = m.teams[1].stats;
  const poss = s0.poss / Math.max(1, s0.poss + s1.poss);
  const kinds = {};
  for (const f of m.feed) kinds[f.kind] = (kinds[f.kind] || 0) + 1;
  console.log(`M${i} ${m.teams[0].club.id} ${s0.goals}-${s1.goals} ${m.teams[1].club.id} | shots ${s0.shots}(${s0.onTarget})-${s1.shots}(${s1.onTarget}) xg ${s0.xg.toFixed(1)}-${s1.xg.toFixed(1)} | poss ${(poss * 100).toFixed(0)}% | pass ${s0.completed}/${s0.passes} ${s1.completed}/${s1.passes} | saves ${s0.saves}-${s1.saves} | corners ${s0.corners}-${s1.corners} | tackles ${s0.tackles}-${s1.tackles} | stillMax ${(maxStill / 60).toFixed(1)}s | ${JSON.stringify(kinds)}`);
  goals += s0.goals + s1.goals;
}
console.log(`avg goals ${(goals / n).toFixed(2)} | ${((Date.now() - t0) / 1000).toFixed(1)}s | problems ${problems}`);
