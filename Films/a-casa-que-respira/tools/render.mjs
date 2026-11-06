// Renderiza os quadros da animação em Chromium headless (WebGL) e salva JPEGs em build/frames/.
// Uso: node tools/render.mjs [--from 0] [--to N] [--step 1] [--scale 1] [--workers 2] [--out build/frames] [--force]
//      node tools/render.mjs --stills 10,55.5,120  (tempos em segundos -> build/stills/)
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const args = Object.fromEntries(process.argv.slice(2).reduce((acc, a, i, arr) => {
  if (a.startsWith('--')) acc.push([a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true]);
  return acc;
}, []));
const scale = parseFloat(args.scale || '1');
const W = Math.round(1920 * scale), H = Math.round(1080 * scale);
const workers = parseInt(args.workers || '1');
const step = parseInt(args.step || '1');
const out = path.resolve(ROOT, args.out || (args.stills ? 'build/stills' : 'build/frames'));
fs.mkdirSync(out, { recursive: true });

const MIME = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.json': 'application/json', '.woff2': 'font/woff2', '.png': 'image/png', '.jpg': 'image/jpeg' };
const server = http.createServer((req, res) => {
  const p = path.join(ROOT, decodeURIComponent(req.url.split('?')[0]));
  if (!p.startsWith(ROOT) || !fs.existsSync(p) || fs.statSync(p).isDirectory()) { res.writeHead(404); return res.end(); }
  res.writeHead(200, { 'Content-Type': MIME[path.extname(p)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
  fs.createReadStream(p).pipe(res);
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const port = server.address().port;

async function openPage() {
  const browser = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-vsync', '--js-flags=--max-old-space-size=4096'] });
  const page = await browser.newPage({ viewport: { width: W, height: H } });
  page.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') console.log('[page]', m.text().slice(0, 300)); });
  page.on('pageerror', e => console.log('[pageerror]', e.message));
  await page.goto(`http://127.0.0.1:${port}/src/index.html?scale=${scale}${args.test ? '&test=' + args.test : ''}`);
  await page.waitForFunction(() => window.READY === true, null, { timeout: 120000 });
  return { browser, page };
}

async function renderList(frames, label) {
  const { browser, page } = await openPage();
  const t0 = Date.now();
  let n = 0;
  for (const [f, name] of frames) {
    const file = path.join(out, name);
    if (!args.force && !args.stills && fs.existsSync(file)) continue;
    const shot = await page.evaluate(f => window.renderFrame(f), f);
    const data = await page.evaluate(() => window.grab(0.94));
    fs.writeFileSync(file, Buffer.from(data.split(',')[1], 'base64'));
    n++;
    if (n % 24 === 0 || args.stills) {
      const el = (Date.now() - t0) / 1000;
      console.log(`${label} quadro ${f} (${shot}) — ${n} feitos, ${(el / n).toFixed(2)} s/quadro`);
    }
  }
  await browser.close();
}

if (args.stills) {
  const list = String(args.stills).split(',').map(s => { const t = parseFloat(s); return [Math.round(t * 24), `still_${t.toFixed(2).padStart(7, '0')}.jpg`]; });
  await renderList(list, 'still');
} else {
  const { page, browser } = await openPage();
  const total = await page.evaluate(() => window.TOTAL_FRAMES);
  await browser.close();
  const from = parseInt(args.from || '0'), to = Math.min(total, parseInt(args.to || String(total)));
  const all = [];
  for (let f = from; f < to; f += step) all.push([f, `f${String(f).padStart(5, '0')}.jpg`]);
  const chunk = Math.ceil(all.length / workers);
  await Promise.all(Array.from({ length: workers }, (_, i) => renderList(all.slice(i * chunk, (i + 1) * chunk), `[w${i}]`)));
}
server.close();
console.log('ok');
