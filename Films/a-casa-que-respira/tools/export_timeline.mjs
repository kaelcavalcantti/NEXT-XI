// Exporta a linha do tempo para JSON (lida pelo motor de áudio em Python).
import fs from 'node:fs';
import * as TL from '../src/timeline.js';
import { buildChoreo } from '../src/choreo.js';
const choreo = buildChoreo();
const out = {
  fps: TL.FPS, total: TL.TOTAL, shots: TL.SHOTS, lines: TL.LINES, events: TL.EVENTS,
  steps: choreo.steps, cues: choreo.cues,
};
fs.mkdirSync(new URL('../build/', import.meta.url), { recursive: true });
fs.writeFileSync(new URL('../build/timeline.json', import.meta.url), JSON.stringify(out, null, 1));
console.log('timeline.json: total', TL.TOTAL.toFixed(1), 's,', TL.LINES.length, 'falas,', choreo.steps.length, 'passos');
