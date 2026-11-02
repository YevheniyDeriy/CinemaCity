const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage(); const errors = []; const apiRequests = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('request', req => { if (new URL(req.url()).pathname.startsWith('/api/')) { assert.equal(new URL(req.url()).port, '5000', 'API request bypasses gateway'); apiRequests.push(req.url()); } });
  try {
    await page.goto('http://127.0.0.1:5173/auth');
    await page.getByRole('heading', { name: 'З поверненням у кіно' }).waitFor();
    await page.getByLabel('Email', { exact: true }).fill('original.owner@cinemacity.tests.local');
    await page.getByLabel('Пароль', { exact: true }).fill('OriginalCinemaCity123!');
    await page.getByRole('button', { name: 'Увійти', exact: true }).click();
    await page.getByRole('heading', { name: 'Знайдіть свій наступний фільм' }).waitFor();
    await page.getByRole('heading', { name: 'Original CinemaCity film', exact: true }).waitFor();
    await page.screenshot({ path: '../.logs/lab4-browser/catalog.png', fullPage: true });
    await page.getByRole('searchbox', { name: 'Пошук фільму' }).fill('not-in-this-catalog');
    await page.getByRole('heading', { name: 'Нічого не знайдено' }).waitFor();
    await page.getByRole('button', { name: 'Очистити пошук' }).click();
    await page.getByRole('heading', { name: 'Original CinemaCity film', exact: true }).waitFor();
    const before = apiRequests.length;
    await page.getByRole('heading', { name: 'Original CinemaCity film', exact: true }).click();
    await page.getByText('Ви ще не оцінили цей фільм', { exact: true }).waitFor();
    const requests = apiRequests.slice(before); assert.equal(requests.length, 1, 'details must use exactly one composed request');
    assert.ok(requests[0].includes('/api/v1/screens/movies/'));
    await page.getByRole('link', { name: 'Оцінити фільм', exact: true }).click();
    await page.getByRole('spinbutton').fill('8');
    await page.getByRole('button', { name: 'Зберегти оцінку' }).dblclick();
    await page.getByRole('heading', { name: 'Мої оцінки', exact: true }).waitFor();
    await page.getByText(/8 \/ 10/).waitFor();
    await page.screenshot({ path: '../.logs/lab4-browser/ratings.png', fullPage: true });
    await page.getByRole('button', { name: 'Скасувати оцінку' }).click();
    await page.getByRole('heading', { name: 'Оцінок ще немає' }).waitFor();
    // Reload restores access through the HttpOnly refresh cookie, without localStorage tokens.
    await page.reload(); await page.getByRole('heading', { name: 'Оцінок ще немає' }).waitFor();
    assert.equal(await page.evaluate(() => Object.keys(localStorage).some(key => /token/i.test(key))), false);
    assert.deepEqual(errors, []);
    console.log(JSON.stringify({ result: 'passed', screens: ['login', 'catalog', 'empty search', 'composed details', 'create rating', 'my ratings', 'cancel', 'cookie restore'], apiRequests: apiRequests.length, consoleErrors: errors }, null, 2));
  } catch (error) { await page.screenshot({ path: '../.logs/lab4-browser/failure.png', fullPage: true }); console.error('Page:', await page.locator('body').innerText()); throw error; }
  finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
