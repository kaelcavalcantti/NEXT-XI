// Loads the built page in headless Chromium (three.js served from the npm package), plays a
// little with simulated input and saves screenshots. Usage: node test/browser.js <outDir>
const { chromium } = require('/opt/node-tools/node_modules/playwright');
const fs = require('fs'), path = require('path');
const out = process.argv[2] || '.';
const three = fs.readFileSync(process.env.THREE_JS);
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  for (const vp of [{ name: 'phone', width: 844, height: 390, mobile: true }, { name: 'desktop', width: 1280, height: 720, mobile: false }]) {
    const ctx = await browser.newContext({ viewport: { width: vp.width, height: vp.height }, isMobile: vp.mobile, hasTouch: vp.mobile, deviceScaleFactor: 1 });
    const page = await ctx.newPage();
    const errors = [];
    page.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') errors.push(m.type() + ': ' + m.text()); });
    page.on('pageerror', e => errors.push('pageerror: ' + e.message));
    await page.route('https://cdnjs.cloudflare.com/**', r => r.fulfill({ body: three, contentType: 'application/javascript' }));
    await page.route('https://fonts.googleapis.com/**', r => r.fulfill({ body: '', contentType: 'text/css' }));
    await page.route('https://fonts.gstatic.com/**', r => r.abort());
    await page.goto('file://' + path.resolve(__dirname, '../dist/standalone.html'));
    await page.waitForTimeout(2500);
    await page.screenshot({ path: path.join(out, vp.name + '_menu.png') });
    await page.click('#playBtn');
    await page.waitForTimeout(800);
    await page.screenshot({ path: path.join(out, vp.name + '_kickoff.png') });
    // Kick off and play: hold right, pass, sprint, shoot.
    await page.keyboard.press('KeyJ');
    await page.waitForTimeout(1500);
    await page.keyboard.down('KeyD');
    await page.keyboard.down('ShiftLeft');
    await page.waitForTimeout(1500);
    await page.screenshot({ path: path.join(out, vp.name + '_play.png') });
    await page.keyboard.press('KeyJ');
    await page.waitForTimeout(1200);
    await page.keyboard.down('KeyK'); await page.waitForTimeout(500); await page.keyboard.up('KeyK');
    await page.waitForTimeout(3000);
    await page.keyboard.up('KeyD'); await page.keyboard.up('ShiftLeft');
    const state = await page.evaluate(() => { const m = window.__nxi.match; return { phase: m.phase, clock: m.clockText(), score: m.teams[0].stats.goals + '-' + m.teams[1].stats.goals, passes: m.teams[0].stats.passes, user: m.user && m.user.name, feed: m.feed.slice(-5).map(f => f.text) }; });
    await page.screenshot({ path: path.join(out, vp.name + '_later.png') });
    // Fast-forward to the end screen.
    await page.evaluate(() => { const m = window.__nxi.match; m.userAutopilot = true; let n = 0; while (m.phase !== 'fulltime' && n++ < 400000) { if (m.phase === 'halftime') m.continueSecondHalf(); m.step(1 / 60); } });
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(out, vp.name + '_end.png') });
    const fps = await page.evaluate(() => new Promise(res => { let n = 0; const t0 = performance.now(); const f = () => { n++; if (performance.now() - t0 < 2000) requestAnimationFrame(f); else res(n / 2); }; requestAnimationFrame(f); }));
    console.log(vp.name, JSON.stringify(state), 'fps(swiftshader)', fps.toFixed(0));
    console.log(errors.length ? errors.join('\n') : 'no console errors');
    await ctx.close();
  }
  await browser.close();
})();
