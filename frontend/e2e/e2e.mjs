import { chromium } from 'playwright';
import fs from 'node:fs';

// End-to-end UI check in headless Chromium against the REAL containers (see README: "Frontend end-to-end test").
// The browser runs inside Docker, where the page's configured API_URL (http://localhost:8080) is not the backend,
// so API requests are redirected to API_TO: a dedicated backend started with a SHORT hold time so expiry can be tested.
const APP = process.env.APP_URL || 'http://frontend:80';
const API_FROM = process.env.API_FROM || 'http://localhost:8080';
const API_TO = process.env.API_TO || 'http://backend-e2e:8080';
const RUN = String(Date.now()).slice(-9); const mk = (n) => '5' + RUN + '000000' + n; const A = mk(1), B = mk(2), C = mk(3), D = mk(4);
const NAME = 'E2E Class ' + Date.now();
fs.mkdirSync('shots', { recursive: true });

const results = [];
const check = (name, ok, detail = '') => { results.push({ name, ok, detail }); console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? '  -> ' + detail : ''}`); };

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1000, height: 900 } });
const page = await ctx.newPage();
const consoleErrors = [];
page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });
page.on('pageerror', (e) => consoleErrors.push('pageerror: ' + e.message));

// The browser lives inside Docker, where "localhost:8080" is not the backend. Redirect it (config.js still says localhost).
let abortApi = false;
let staleClasses = false;
await page.route(`${API_FROM}/**`, async (route) => {
  if (abortApi) return route.abort('connectionrefused');
  const url = route.request().url().replace(API_FROM, API_TO);
  if (staleClasses && route.request().method() === 'GET' && /\/classes$/.test(url)) {
    const res = await route.fetch({ url });
    const data = await res.json();
    for (const c of data) if (c.name === NAME) { c.availableSeats = 1; }
    return route.fulfill({ response: res, json: data });
  }
  return route.continue({ url });
});

const shot = (n) => page.screenshot({ path: `shots/${n}.png`, fullPage: true });
const setStudent = async (id) => { await page.fill('#student-id', id); };
const tab = (n) => page.getByRole('tab', { name: n }).click();
const card = () => page.locator('article.class-card').filter({ hasText: NAME });

for (let i = 1; ; i++) { // the containers may still be starting
  try { await page.goto(APP, { timeout: 5000 }); break; } catch (e) { if (i >= 30) throw e; await new Promise((r) => setTimeout(r, 2000)); }
}
await page.waitForSelector('h1');
check('page renders (not blank)', (await page.locator('h1').innerText()) === 'School Admission');
check('config.js loaded', (await page.evaluate(() => window.__CONFIG__?.API_URL)) === 'http://localhost:8080');
await shot('01-home');

// ---- 1. student id validation + persistence
await setStudent('123');
check('short ID shows validation message', (await page.locator('#student-id-help').innerText()).includes('exactly 17 digits'));
await setStudent('abc');
check('non-digits rejected with message', (await page.locator('#student-id-help').innerText()).includes('digits only'));

// ---- 2. create class (validation, then success)
await tab('Create class (admin)');
await page.getByRole('button', { name: 'Create class' }).click();
check('empty create form shows field errors', (await page.locator('.help.error').count()) >= 2);
await page.fill('#class-name', NAME);
await page.fill('#class-seats', '0');
await page.getByRole('button', { name: 'Create class' }).click();
check('seats=0 rejected in plain language', (await page.locator('.form').innerText()).includes('whole number of at least 1'));
await page.fill('#class-seats', '2');
await page.getByRole('button', { name: 'Create class' }).click();
await card().waitFor();
check('class created with 2 seats and listed', (await card().textContent()).replace(/\s+/g, '').includes('Total2'));
check('success notice shown', (await page.locator('.notice.success').innerText()).includes('created'));
await shot('02-class-created');

// ---- 3. apply disabled without valid ID
await setStudent('');
check('Apply disabled without valid student ID', await card().getByRole('button', { name: 'Apply' }).isDisabled());
check('hint explains why', (await card().innerText()).includes('17-digit student ID'));

// ---- 4. student A applies
await setStudent(A);
await card().getByRole('button', { name: 'Apply' }).click();
await page.locator('article.app-card').first().waitFor();
check('apply moves to My applications with HELD + countdown',
  (await page.locator('.badge.held').count()) === 1 && /\d+:\d\d/.test(await page.locator('.countdown').innerText()));
const t1 = await page.locator('.countdown strong').innerText();
await page.waitForTimeout(2200);
const t2 = await page.locator('.countdown strong').innerText();
check('countdown ticks live', t1 !== t2, `${t1} -> ${t2}`);
await shot('03-held');

// persistence across reload
await page.reload();
await page.waitForSelector('#student-id');
check('student ID survives reload', (await page.inputValue('#student-id')) === A);
await tab('My applications');
await page.locator('article.app-card').first().waitFor();
check('my applications survive reload', (await page.locator('article.app-card').count()) === 1);

// ---- 5. student B applies
await setStudent(B);
await tab('Classes');
await card().waitFor();
await page.waitForFunction((n) => [...document.querySelectorAll('article.class-card')].find(a => a.textContent.includes(n))?.textContent.replace(/\s+/g,' ').includes('Available1'), NAME, { timeout: 9000 });
check('seat counts updated without reload (A holds 1 -> available 1)', true);
await card().getByRole('button', { name: 'Apply' }).click();
await page.locator('.badge.held').first().waitFor();
check('student B apply OK', (await page.locator('article.app-card').count()) === 1);

// ---- 6. student C: no seats
await setStudent(C);
await tab('Classes');
await card().waitFor();
await page.waitForFunction((n) => [...document.querySelectorAll('article.class-card')].find(a => a.textContent.includes(n))?.textContent.replace(/\s+/g,' ').includes('Available0'), NAME, { timeout: 9000 });
check('full class: Apply disabled for C', await card().getByRole('button', { name: 'Apply' }).isDisabled());
check('full class: hint says no seats left', (await card().innerText()).includes('No seats left'));
await shot('04-full');
// force the race: stale list says 1 seat, server says 0 -> real 409 path
staleClasses = true;
await page.waitForFunction((n) => !([...document.querySelectorAll('article.class-card')].find(a => a.textContent.includes(n))?.querySelector('button')?.disabled), NAME, { timeout: 9000 });
await card().getByRole('button', { name: 'Apply' }).click();
await page.locator('.notice.error').waitFor();
check('409 NO_SEATS shown in plain language', (await page.locator('.notice.error').innerText()).includes('no seats left'), (await page.locator('.notice.error').innerText()).trim().slice(0, 70));
await shot('05-no-seats-error');
staleClasses = false;
await page.locator('.notice-close').click();

// ---- 7. B pays
await setStudent(B);
await tab('My applications');
await page.locator('article.app-card').first().waitFor();
await page.getByRole('button', { name: 'Pay fee' }).click();
await page.locator('.badge.confirmed').waitFor();
check('B: Pay fee -> CONFIRMED', true);
check('B: Pay and Cancel disabled after confirm', (await page.getByRole('button', { name: 'Pay fee' }).isDisabled()) && (await page.getByRole('button', { name: 'Cancel' }).isDisabled()));
await shot('06-confirmed');

// ---- 8. A cancels
await setStudent(A);
await page.locator('article.app-card').first().waitFor();
await page.getByRole('button', { name: 'Cancel' }).click();
await page.locator('.badge.cancelled').waitFor();
check('A: Cancel -> CANCELLED', true);
await tab('Classes');
await page.waitForFunction((n) => [...document.querySelectorAll('article.class-card')].find(a => a.textContent.includes(n))?.textContent.replace(/\s+/g,' ').includes('Available1'), NAME, { timeout: 9000 });
check('cancel freed the seat (available 1)', true);

// ---- 9. D applies and lets it expire (HOLD_DURATION_SECONDS=30), no reload, no clicks
await setStudent(D);
await card().getByRole('button', { name: 'Apply' }).click();
await page.locator('.badge.held').waitFor();
const startedAt = Date.now();
await shot('07-d-held');
await page.locator('.badge.expired').waitFor({ timeout: 45000 });
const waited = ((Date.now() - startedAt) / 1000).toFixed(1);
check('countdown flipped to EXPIRED without reload', true, `after ${waited}s`);
check('Pay disabled once expired', await page.getByRole('button', { name: 'Pay fee' }).isDisabled());
await shot('08-expired');
await page.waitForTimeout(1500);
check('expired status confirmed by server (still EXPIRED after re-check)', (await page.locator('.badge.expired').count()) === 1);
await tab('Classes');
await page.waitForFunction((n) => [...document.querySelectorAll('article.class-card')].find(a => a.textContent.includes(n))?.textContent.replace(/\s+/g,' ').includes('Available1'), NAME, { timeout: 9000 });
check('expired hold released its seat (available 1)', true);

// ---- 10. server unreachable
abortApi = true;
await page.reload();
await page.waitForSelector('.notice.error', { timeout: 10000 });
const down = await page.locator('.notice.error').first().innerText();
check('server down: plain-language message, page not blank', down.includes('cannot reach'), down.trim().slice(0, 80));
check('server down: page still has content', (await page.locator('h1').count()) === 1);
await shot('09-server-down');
abortApi = false;

check('no uncaught page errors', consoleErrors.filter(e => e.startsWith('pageerror')).length === 0, consoleErrors.filter(e => e.startsWith('pageerror')).join(' | '));
console.log('console errors (info):', consoleErrors.length, consoleErrors.slice(0, 3));

await browser.close();
const failed = results.filter((r) => !r.ok);
console.log(`\n${results.length - failed.length}/${results.length} checks passed`);
process.exit(failed.length ? 1 : 0);
