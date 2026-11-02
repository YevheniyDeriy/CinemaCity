const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright');
const assert = require('node:assert/strict');
(async () => {
 const browser = await chromium.launch({ headless: true, channel: 'msedge' }); const context = await browser.newContext(); const page = await context.newPage(); const ids = []; let token;
 try {
  const login = await context.request.post('http://127.0.0.1:5000/api/auth/login', { data: { email: 'original.admin@cinemacity.tests.local', password: 'OriginalCinemaCity123!' } }); assert.equal(login.status(), 200); token = (await login.json()).accessToken;
  for (let i=0;i<11;i++) {
   const response = await context.request.post('http://127.0.0.1:5000/api/v1/catalog/items', { headers: { Authorization: `Bearer ${token}` }, data: { title: `ZZZ browser pagination ${i}`, originalTitle: null, contentType: 'Movie', releaseYear: 2026, duration: 90, description: 'Disposable browser check', genreIds: [1], countryIds: [], languageIds: [], cast: [] } });
   assert.equal(response.status(), 201); ids.push((await response.json()).id);
  }
  await page.goto('http://127.0.0.1:5173/catalog'); await page.getByText('Сторінка 1 з 2').waitFor();
  await page.getByRole('button', { name: 'Далі', exact: true }).click(); await page.getByText('Сторінка 2 з 2').waitFor(); await page.waitForFunction(() => document.querySelectorAll('.client-card').length === 1); assert.equal(await page.locator('.client-card').count(), 1);
  const queries = []; page.on('request', req => { if (new URL(req.url()).pathname === '/api/v1/catalog/items') queries.push(req.url()); });
  await page.getByRole('searchbox', { name: 'Пошук фільму' }).pressSequentially('Original', { delay: 25 });
  await page.getByText('Сторінка 1 з 1').waitFor(); await page.getByRole('heading', { name: 'Original CinemaCity film', exact: true }).waitFor();
  assert.equal(queries.length, 1, 'Typing must debounce into one request'); assert.equal(await page.locator('.client-card').count(), 2);
  await page.setViewportSize({ width: 390, height: 844 }); await page.screenshot({ path: '../.logs/lab4-browser/mobile.png', fullPage: true });
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Mobile layout overflows horizontally');
  await page.setViewportSize({ width: 1440, height: 1000 }); await page.locator('.user-menu-trigger').click(); await page.getByRole('button', { name: 'Вийти', exact: true }).click(); await page.getByRole('link', { name: 'Увійти', exact: true }).waitFor();
  await page.goto('http://127.0.0.1:5173/my-ratings'); await page.getByRole('heading', { name: 'З поверненням у кіно' }).waitFor();
  console.log(JSON.stringify({ result: 'passed', scenarios: ['real pagination', 'debounced typing', 'reset to page 1', 'mobile no overflow', 'logout'] }, null, 2));
 } finally { for (const id of ids) await context.request.delete(`http://127.0.0.1:5000/api/v1/catalog/items/${id}`, { headers: { Authorization: `Bearer ${token}` } }); await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
