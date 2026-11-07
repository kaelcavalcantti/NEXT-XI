import { createRequire } from 'module'; const require = createRequire('/usr/lib/'); const { chromium } = (()=>{ try { return require('playwright'); } catch(e) { return createRequire(process.execPath)('playwright'); } })();
import fs from 'fs';
const [,, mode, a, b, out] = process.argv;
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1080, height: 1920 } });
page.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') console.error('PAGE', m.text().slice(0, 300)); });
page.on('pageerror', e => console.error('PAGEERR', e.message));
await page.goto('http://127.0.0.1:8765/index.html');
await page.waitForFunction(() => window.ready || window.err, null, { timeout: 300000 });
const err = await page.evaluate(() => window.err); if (err) { console.error(err); process.exit(1); }
if (mode === 'stills') {
  const times = a.split(',').map(Number);
  for (const t of times) { const t0 = Date.now(); const d = await page.evaluate(t => window.renderAt(t), t); fs.writeFileSync(`${b}/still_${t.toFixed(2)}.jpg`, Buffer.from(d.split(',')[1], 'base64')); console.log('still', t, Date.now() - t0, 'ms'); }
} else {
  const fps = 30; const f0 = +a, f1 = +b; const fd = fs.openSync(out, 'w');
  const t0 = Date.now();
  for (let f = f0; f < f1; f++) {
    const d = await page.evaluate(t => window.renderAt(t), f / fps);
    fs.writeSync(fd, Buffer.from(d.split(',')[1], 'base64'));
    if ((f - f0) % 60 === 0) console.log(out, f, ((Date.now() - t0) / (f - f0 + 1)).toFixed(0), 'ms/frame');
  }
  fs.closeSync(fd);
}
await browser.close();
