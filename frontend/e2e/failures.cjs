const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright');
const fs = require('node:fs'); const assert = require('node:assert/strict');
const { movieId } = JSON.parse(fs.readFileSync('../.local/defense-demo.json', 'utf8'));
const headers = { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': 'http://127.0.0.1:5173', 'Access-Control-Allow-Credentials': 'true', 'Access-Control-Expose-Headers': 'Retry-After' };
(async () => {
 const browser = await chromium.launch({ headless: true, channel: 'msedge' }); const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
 const page = await context.newPage(); const errors = []; page.on('pageerror', error => errors.push(error.message));
 try {
  const login = await context.request.post('http://127.0.0.1:5000/api/auth/login', { data: { email: 'original.owner@cinemacity.tests.local', password: 'OriginalCinemaCity123!' } }); assert.equal(login.status(), 200);
  const composed = `**/api/v1/screens/movies/${movieId}`;
  await page.route(composed, route => route.fulfill({ status: 200, headers, body: JSON.stringify({ movie: { id: movieId, title: 'Доступний опис', releaseYear: 2026, posterUrl: null }, myState: null, partial: true, communityStatus: 'unavailable' }) }));
  await page.goto(`http://127.0.0.1:5173/movies/${movieId}`); await page.getByText('Особисті дані тимчасово недоступні').waitFor();
  await page.screenshot({ path: '../.logs/lab4-browser/partial.png', fullPage: true }); await page.unroute(composed);
  let calls = 0;
  const catalog = '**/api/v1/catalog/items?*';
  await page.route(catalog, route => { calls++; return calls === 1 ? route.fulfill({ status: 429, headers: { ...headers, 'Retry-After': '2' }, body: '{"status":429,"title":"limit"}' }) : route.continue(); });
  await page.goto('http://127.0.0.1:5173/catalog');
  const waiting = page.getByRole('button', { name: /Повторити через/ }); await waiting.waitFor(); assert.equal(await waiting.isDisabled(), true);
  await page.waitForTimeout(700); assert.equal(calls, 1, 'No network calls during Retry-After');
  await page.getByRole('button', { name: 'Повторити', exact: true }).click(); await page.getByRole('heading', { name: 'Original CinemaCity film', exact: true }).waitFor();
  assert.equal(calls, 2); await page.unroute(catalog);
  // A lost/unavailable response keeps the exact same operation key on a manual retry.
  const keys = []; let posts = 0;
  await page.route('**/api/ratings', async route => {
   if (route.request().method() !== 'POST') return route.continue();
   posts++; keys.push(route.request().headers()['idempotency-key']);
   if (posts === 1) return route.fulfill({ status: 503, headers, body: '{"status":503,"title":"unavailable"}' });
   return route.continue();
  });
  await page.goto(`http://127.0.0.1:5173/movies/${movieId}/rate`); const input = page.getByRole('spinbutton'); await input.waitFor();
  await input.fill('0'); await page.getByRole('button', { name: 'Зберегти оцінку' }).click(); await page.getByText('Вкажіть оцінку від 1 до 10.').waitFor(); assert.equal(posts, 0);
  await input.fill('7'); await page.getByRole('button', { name: 'Зберегти оцінку' }).click(); await page.getByText('Сервіс тимчасово недоступний. Спробуйте ще раз.', { exact: true }).waitFor();
  await page.getByRole('button', { name: 'Зберегти оцінку' }).click(); await page.getByRole('heading', { name: 'Мої оцінки', exact: true }).waitFor(); assert.equal(keys[0], keys[1]); assert.ok(keys[0]);
  await page.getByRole('button', { name: 'Скасувати оцінку' }).click(); await page.getByRole('heading', { name: 'Оцінок ще немає' }).waitFor();
  // Expired access + failed refresh must return to login, without a loop.
  let refreshes = 0;
  await page.route('**/api/auth/refresh', route => { refreshes++; return route.fulfill({ status: 401, headers, body: '{"status":401}' }); });
  await page.route('**/api/ratings', route => route.fulfill({ status: 401, headers, body: '{"status":401}' }));
  await page.getByRole('link', { name: 'Обрати фільм →' }).click(); await page.goto('http://127.0.0.1:5173/my-ratings');
  await page.getByRole('heading', { name: 'З поверненням у кіно' }).waitFor(); assert.equal(refreshes, 1);
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ result: 'passed', scenarios: ['partial', '429 countdown', 'no traffic during cooldown', 'client field validation', 'unavailable/retry', 'same idempotency key', '401 redirect, one refresh'], consoleErrors: errors }, null, 2));
 } catch (error) { await page.screenshot({ path: '../.logs/lab4-browser/failures-error.png', fullPage: true }); console.error(await page.locator('body').innerText()); throw error; }
 finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
