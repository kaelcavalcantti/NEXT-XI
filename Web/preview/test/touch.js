// Drives the on-screen joystick and buttons with real pointer events (mouse) and checks the engine sees them.
const { chromium } = require('/opt/node-tools/node_modules/playwright');
const fs = require('fs'), path = require('path');
const three = fs.readFileSync(process.env.THREE_JS);
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const page = await (await browser.newContext({ viewport: { width: 844, height: 390 } })).newPage();
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  await page.route('https://cdnjs.cloudflare.com/**', r => r.fulfill({ body: three, contentType: 'application/javascript' }));
  await page.route('https://fonts.**/**', r => r.abort());
  await page.goto('file://' + path.resolve(__dirname, '../dist/standalone.html'));
  await page.waitForTimeout(800);
  await page.click('#playBtn');
  await page.waitForTimeout(700);
  const ev = () => page.evaluate(() => { const m = window.__nxi.match; return { phase: m.phase, ownerTeam: m.owner ? m.owner.team : null, user: m.user && m.user.id, passes: m.teams[0].stats.passes, shots: m.teams[0].stats.shots, ux: m.user && +m.user.p.x.toFixed(1), mx: +m.input.mx.toFixed(2) }; });
  console.log('start', await ev());
  await page.click('[data-act="pass"]');            // kick-off pass
  await page.waitForTimeout(1500);
  console.log('after kickoff pass', await ev());
  // Joystick: press in the left zone and drag right.
  await page.mouse.move(150, 250); await page.mouse.down(); await page.mouse.move(200, 250, { steps: 4 });
  await page.waitForTimeout(400);
  console.log('stick held', await ev());
  await page.waitForTimeout(1200);
  await page.mouse.up();
  console.log('stick released', await ev());
  // Hold the shoot button for 0.6 s.
  const b = await page.$('[data-act="shoot"]'); const box = await b.boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2); await page.mouse.down(); await page.waitForTimeout(600); await page.mouse.up();
  await page.waitForTimeout(800);
  console.log('after shoot button', await ev());
  await page.click('#pauseBtn'); await page.waitForTimeout(200);
  const pausedClock = await page.evaluate(() => window.__nxi.match.halfClock);
  await page.waitForTimeout(500);
  console.log('paused stable', pausedClock === await page.evaluate(() => window.__nxi.match.halfClock));
  await page.click('#pResume');
  console.log(errors.length ? errors : 'no page errors');
  await browser.close();
})();
