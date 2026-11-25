// Usage: node smoke.mjs <url> <out-dir> [shot1,shot2,...]
// 1. "boot": loads the WebGL build, waits for Unity to finish loading, saves screenshot.png.
// 2. Each named shot: loads <url>?shot=<name>. The game's photo mode stages a scene, logs
//    "[shot] start", then "[shot] state {json}" lines, then "[shot] done". Frames are taken at fixed
//    times after "[shot] start". Every "[shot]" console line is kept as telemetry.
// Writes smoke.json and summary.md. Exits non-zero only when the boot check fails.
import { chromium } from 'playwright';
import { writeFileSync } from 'node:fs';

const [baseUrl, outDir, shotList = ''] = process.argv.slice(2);
const shots = shotList.split(',').map(s => s.trim()).filter(s => /^[a-z0-9-]+$/.test(s));
const FRAME_TIMES = [0.25, 0.75, 1.5, 3.0];        // seconds after "[shot] start"

const browser = await chromium.launch({
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'],
});

async function capture(name) {
  const url = new URL(baseUrl);
  if (name !== 'boot') url.searchParams.set('shot', name);
  const r = { name, url: url.href, loaded: false, loadSeconds: null, shotStarted: false, shotDone: false,
              frames: [], telemetry: [], consoleErrors: [], pageErrors: [], failedRequests: [] };
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const t0 = Date.now();
  let startAt = null;
  page.on('console', m => {
    const text = m.text();
    if (text.startsWith('[shot]')) {
      if (r.telemetry.length < 400) r.telemetry.push({ t: (Date.now() - t0) / 1000, text: text.slice(0, 2000) });
      if (text.startsWith('[shot] start') && startAt === null) startAt = Date.now();
      if (text.startsWith('[shot] done')) r.shotDone = true;
    } else if (m.type() === 'error') {
      r.consoleErrors.push(text.slice(0, 500));
    }
  });
  page.on('pageerror', e => r.pageErrors.push(String(e).slice(0, 500)));
  page.on('requestfailed', q => r.failedRequests.push(`${q.url()} ${q.failure()?.errorText}`));
  page.on('response', q => { if (q.status() >= 400) r.failedRequests.push(`${q.url()} HTTP ${q.status()}`); });

  try {
    await page.goto(url.href, { waitUntil: 'domcontentloaded', timeout: 30_000 });
    await page.waitForSelector('#unity-canvas', { timeout: 30_000 });
    // Unity's Default template hides the loading bar when the player has started.
    await page.waitForFunction(() => {
      const bar = document.querySelector('#unity-loading-bar');
      return bar && getComputedStyle(bar).display === 'none';
    }, { timeout: 180_000 });
    r.loaded = true;
    r.loadSeconds = (Date.now() - t0) / 1000;

    if (name === 'boot') {
      await page.waitForTimeout(5_000);              // let a few frames render
      await page.screenshot({ path: `${outDir}/screenshot.png` });
      r.frames.push('screenshot.png');
    } else {
      const deadline = Date.now() + 15_000;
      while (startAt === null && Date.now() < deadline) await page.waitForTimeout(100);
      r.shotStarted = startAt !== null;
      if (r.shotStarted) {
        for (const [i, t] of FRAME_TIMES.entries()) {
          const wait = startAt + t * 1000 - Date.now();
          if (wait > 0) await page.waitForTimeout(wait);
          const file = `${name}-${i + 1}.png`;
          await page.screenshot({ path: `${outDir}/${file}` });
          r.frames.push(file);
        }
        const doneBy = Date.now() + 5_000;
        while (!r.shotDone && Date.now() < doneBy) await page.waitForTimeout(100);
      }
    }
  } catch (e) {
    r.pageErrors.push(`capture: ${String(e).slice(0, 500)}`);
    await page.screenshot({ path: `${outDir}/${name}-failed.png` }).catch(() => {});
  }
  await page.close();
  return r;
}

const runs = [await capture('boot')];
if (runs[0].loaded) for (const name of shots) runs.push(await capture(name));
await browser.close();

const boot = runs[0];
const result = { url: baseUrl, loaded: boot.loaded, loadSeconds: boot.loadSeconds, runs };
writeFileSync(`${outDir}/smoke.json`, JSON.stringify(result, null, 2));

const rows = runs.map(r => `| ${r.name} | ${r.loaded ? 'yes' : 'NO'} | ${r.loadSeconds ?? '-'} | ` +
  `${r.name === 'boot' ? '-' : (r.shotStarted ? (r.shotDone ? 'done' : 'started') : 'not started')} | ` +
  `${r.frames.length} | ${r.consoleErrors.length + r.pageErrors.length} |`);
const errs = runs.flatMap(r => [...r.pageErrors, ...r.consoleErrors].slice(0, 5).map(e => `- ${r.name}: ${e}`));
writeFileSync(`${outDir}/summary.md`,
  `## Browser check\n\n| Run | Loaded | Load s | Shot | Frames | Errors |\n|---|---|---|---|---|---|\n` +
  rows.join('\n') + `\n\n${errs.length ? '**First errors**\n' + errs.join('\n') : 'No errors.'}\n\n` +
  `Frames and telemetry are in the run's screenshot artifact.\n`);

console.log(JSON.stringify(result, null, 2));
process.exit(boot.loaded && boot.pageErrors.length === 0 ? 0 : 1);
