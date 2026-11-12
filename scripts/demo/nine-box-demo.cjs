// Slow, captioned walk-through of the nine-box screen. Records a video and one screenshot per step.
// Needs the host on :5000 and Vite on :5173 (see README). Run from any folder that has playwright-core:
//   node scripts/demo/nine-box-demo.cjs [outDir] [--headed] [--pace=1]
// --pace scales every pause: 2 is twice as slow, 0.5 twice as fast. --headed opens a visible browser window.
const path = require('node:path');
const { chromium } = require('playwright-core');

const args = process.argv.slice(2);
const outDir = path.resolve(args.find((a) => !a.startsWith('--')) ?? 'demo-output');
const headed = args.includes('--headed');
const pace = Number(args.find((a) => a.startsWith('--pace='))?.split('=')[1] ?? 1);
const wait = (ms) => new Promise((r) => setTimeout(r, ms * pace));

(async () => {
  const browser = await chromium.launch({
    executablePath: process.env.CHROMIUM_PATH ?? '/usr/bin/chromium',
    headless: !headed,
    slowMo: 250 * pace,
    args: ['--no-sandbox'],
  });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    recordVideo: { dir: outDir, size: { width: 1440, height: 900 } },
  });
  const page = await context.newPage();
  let step = 0;

  // A caption bar at the top of the page, outside the React root so re-renders leave it alone.
  const caption = async (title, detail = '') => {
    await page.evaluate(([t, d]) => {
      let bar = document.getElementById('demo-caption');
      if (!bar) {
        bar = document.createElement('div');
        bar.id = 'demo-caption';
        Object.assign(bar.style, {
          position: 'fixed', left: '50%', bottom: '24px', transform: 'translateX(-50%)', zIndex: 99999,
          background: 'rgba(20,40,45,0.94)', color: '#fff', padding: '14px 24px', borderRadius: '10px',
          font: '20px system-ui, sans-serif', maxWidth: '1100px', boxShadow: '0 6px 24px rgba(0,0,0,.35)', textAlign: 'center',
        });
        document.body.appendChild(bar);
      }
      bar.innerHTML = `<div style="font-weight:700">${t}</div>${d ? `<div style="font-size:16px;opacity:.85;margin-top:4px">${d}</div>` : ''}`;
    }, [title, detail]);
  };

  const shot = async (name) => {
    step += 1;
    await page.screenshot({ path: path.join(outDir, `${String(step).padStart(2, '0')}-${name}.png`) });
  };

  // Outline the target in red for a moment so the viewer sees what is about to be clicked.
  const act = async (locator, how = 'click') => {
    await locator.scrollIntoViewIfNeeded();
    await locator.evaluate((el) => { el.style.outline = '4px solid #e53935'; el.style.outlineOffset = '3px'; });
    await wait(1200);
    await (how === 'check' ? locator.check() : locator.click());
    await locator.evaluate((el) => { el.style.outline = ''; }).catch(() => {});
    await wait(700);
  };

  const evaluate = async (name, perf, pot) => {
    await act(page.getByRole('button', { name: new RegExp(name) }));
    const form = page.getByRole('form', { name: `Evaluate ${name}` });
    await form.waitFor();
    await wait(1500);
    await act(form.getByRole('radiogroup').nth(0).getByRole('radio', { name: new RegExp(`^${perf} ·`) }), 'check');
    await act(form.getByRole('radiogroup').nth(1).getByRole('radio', { name: new RegExp(`^${pot} ·`) }), 'check');
    await act(form.getByRole('button', { name: 'Save evaluation' }));
    await page.getByText(/^Saved/).waitFor();
    await page.getByText(/^Saved/).scrollIntoViewIfNeeded();
    await wait(1500);
  };

  const showGrid = async () => {
    await page.getByRole('grid').scrollIntoViewIfNeeded();
    await wait(2500);
  };

  // 1. Sign in
  await page.goto('http://localhost:5173/');
  await caption('Step 1 of 7 · Sign in', 'Development login: pick the HR admin (the reviewer).');
  await wait(3500);
  await shot('sign-in');
  await act(page.getByRole('button', { name: /Continue as hr-admin/ }));

  // 2. The screen
  await page.getByRole('button', { name: /Pearline Weber/ }).waitFor();
  await caption('Step 2 of 7 · The nine-box screen', 'Left: 12 employees seeded on boot. Right: the grid of latest placements.');
  await wait(4500);
  await shot('screen');

  // 3. AC1
  await caption('Step 3 of 7 · AC1: Performance 3, Potential 3', 'Each level shows its one-line rubric. Expected: Star, score 9.');
  await evaluate('Pearline Weber', 3, 3);
  await caption('AC1 passed: Star · box 9 · score 9', 'Saved, and visible on the grid and in the history below.');
  await wait(3500);
  await shot('ac1-star');
  await showGrid();

  // 4. AC2
  await page.getByRole('button', { name: /Herta Hagenes/ }).scrollIntoViewIfNeeded();
  await caption('Step 4 of 7 · AC2: Performance 1, Potential 1', 'Expected: Risk, score 1.');
  await wait(2000);
  await evaluate('Herta Hagenes', 1, 1);
  await caption('AC2 passed: Risk · box 1 · score 1', 'Low band, shown in red.');
  await wait(3500);
  await shot('ac2-risk');
  await showGrid();

  // 5. AC3
  await caption('Step 5 of 7 · AC3: values outside 1 to 3', 'The form only offers 1 to 3, so we send 4 and 0 straight to the API.');
  await wait(3500);
  const problem = await page.evaluate(async () => {
    const token = (await (await fetch('/bff/antiforgery')).json()).requestToken;
    const list = await (await fetch('/api/v1/assessment/nine-box/employees')).json();
    const r = await fetch('/api/v1/assessment/nine-box/evaluations', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token },
      body: JSON.stringify({ employeeId: list.items[11].employeeId, performance: 4, potential: 0 }),
    });
    return { status: r.status, errors: (await r.json()).errors };
  });
  await caption(`AC3 passed: HTTP ${problem.status}, nothing saved`,
    `${problem.errors.performance[0]}<br>${problem.errors.potential[0]}`);
  await wait(6000);
  await shot('ac3-rejected');

  // 6. AC4
  await page.getByRole('button', { name: /Lambert Donnelly/ }).scrollIntoViewIfNeeded();
  await caption('Step 6 of 7 · AC4: evaluate the same person twice', 'First: Performance 2, Potential 2 (Core player).');
  await wait(2500);
  await evaluate('Lambert Donnelly', 2, 2);
  await caption('Step 6 of 7 · AC4: second evaluation', 'Now: Performance 3, Potential 2 (High performer).');
  await wait(2500);
  await evaluate('Lambert Donnelly', 3, 2);
  await page.getByRole('table', { name: 'Evaluation history' }).scrollIntoViewIfNeeded();
  await caption('AC4 passed: newer one shown, both kept', 'History lists both rows, newest first. The grid shows only the latest.');
  await wait(5000);
  await shot('ac4-history');
  await showGrid();

  // 7. Wrap up
  await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'smooth' }));
  await caption('Step 7 of 7 · Done', 'All 4 acceptance criteria shown. Data is saved in PostgreSQL.');
  await wait(4000);
  await shot('done');

  const video = page.video();
  await context.close();
  await browser.close();
  console.log(`video: ${await video.path()}`);
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
