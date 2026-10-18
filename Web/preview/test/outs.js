const E = require('../src/engine.js');
const tally = {};
for (let i = 0; i < 4; i++) {
  const m = new E.Match({ seed: 300 + i, halfMinutes: 3, userTeam: -1 });
  const ob = m.beginRestart.bind(m);
  m.beginRestart = (type, team, spot) => { const k = type + ' last=' + (m.lastTouch ? (m.lastTouch.isGK ? 'GK' : m.lastTouch.role) + (m.lastTouch.team === team ? '-same' : '-opp') : 'none'); tally[k] = (tally[k] || 0) + 1; ob(type, team, spot); };
  while (m.phase !== 'fulltime') { if (m.phase === 'halftime') m.continueSecondHalf(); m.step(1 / 60); }
}
console.log(tally);
