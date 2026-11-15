// Start the loopback notebook fixture with a freshly published web build.
import assert from 'node:assert/strict';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const base = 'http://localhost:9098';
const fixture = 'http://127.0.0.1:8088';
assert.equal((await (await fetch(fixture + '/_qa/health')).json()).fixture, 'fictional-notebook-browser-qa');
const identities = await (await fetch(fixture + '/_qa/identities')).json();
const pupil = identities.find(identity => identity.key === 'pupil');
assert.ok(pupil);

const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch();
const oldRefresh = 'qa-refresh-before-deploy';
const newRefresh = 'qa-refresh-after-deploy';
const nextRefresh = 'qa-refresh-after-concurrent-calls';
const refreshRequests = [];
const pageErrors = [];
const providerRecovered = Promise.withResolvers();
const concurrentRequests = Promise.withResolvers();
const oauthHeaders = { 'access-control-allow-origin': base };
const studentRequests = new Set(['/api/lectio/me', '/api/me/memberships']);
const rejectedRequests = new Set();
const retriedRequests = new Set();
const rejectedAccess = pupil.token.split('.').slice(0, 2).concat('c3RhbGU').join('.');
let checkingConcurrency = false;

try {
  const context = await browser.newContext({ serviceWorkers: 'block' });
  await context.route('**/*', async route => {
    const request = route.request();
    const url = new URL(request.url());
    if (checkingConcurrency && url.origin === base && studentRequests.has(url.pathname)) {
      if (request.headers().authorization === 'Bearer ' + rejectedAccess) {
        rejectedRequests.add(url.pathname);
        await route.fulfill({ status: 401, json: {} });
        if (rejectedRequests.size === studentRequests.size) concurrentRequests.resolve();
        return;
      }
      assert.equal(request.headers().authorization, 'Bearer ' + pupil.token);
      retriedRequests.add(url.pathname);
      await route.fulfill({ json: url.pathname === '/api/lectio/me' ? {} : [] });
      return;
    }
    if (url.origin === base) return route.continue();
    if (url.href !== 'https://auth.mercantec.tech/oauth/token') return route.abort('blockedbyclient');

    assert.equal(request.method(), 'POST');
    const body = new URLSearchParams(request.postData());
    assert.equal(body.get('grant_type'), 'refresh_token');
    refreshRequests.push(body.get('refresh_token'));
    if (refreshRequests.length === 1) {
      await route.fulfill({ status: 503, headers: oauthHeaders, json: { error: 'temporarily_unavailable' } });
      return;
    }

    if (checkingConcurrency) {
      await concurrentRequests.promise;
      if (refreshRequests.length > 3) {
        await route.fulfill({ status: 400, headers: oauthHeaders, json: { error: 'invalid_grant' } });
        return;
      }
    } else {
      await providerRecovered.promise;
    }
    await route.fulfill({
      headers: oauthHeaders,
      json: { access_token: pupil.token, refresh_token: checkingConcurrency ? nextRefresh : newRefresh, expires_in: 900 }
    });
  });

  const page = await context.newPage();
  page.on('pageerror', error => pageErrors.push(error.message));
  page.setDefaultTimeout(20000);
  await page.goto(base + '/_qa/login?user=pupil');
  await page.waitForURL(url => url.pathname === pupil.home);
  const newNote = page.getByRole('button', { name: 'Ny note', exact: true });
  await newNote.waitFor();
  const signedInPath = page.url();

  // The next document starts a new Blazor runtime in the same tab, as after a deploy.
  await page.evaluate(refresh => {
    sessionStorage.setItem('mt_refresh_token', refresh);
    sessionStorage.setItem('mt_expires_at', '1');
  }, oldRefresh);
  const failedRefresh = page.waitForResponse(response =>
    response.url() === 'https://auth.mercantec.tech/oauth/token' && response.status() === 503);
  await page.reload({ waitUntil: 'domcontentloaded' });
  await failedRefresh;
  const recovery = page.getByRole('status').filter({ hasText: 'Genopretter login' });
  const loginPrompt = page.getByRole('heading', { name: 'Log ind', exact: true });
  await recovery.or(loginPrompt).waitFor();
  assert.equal(page.url(), signedInPath);
  assert.deepEqual(await page.evaluate(() => [
    Boolean(sessionStorage.getItem('mt_access_token')), sessionStorage.getItem('mt_refresh_token')
  ]), [true, oldRefresh]);
  assert.equal(await loginPrompt.count(), 0);

  providerRecovered.resolve();
  await newNote.waitFor();
  assert.equal(await page.evaluate(() => sessionStorage.getItem('mt_refresh_token')), newRefresh);
  assert.deepEqual(refreshRequests, [oldRefresh, oldRefresh]);

  await page.reload({ waitUntil: 'domcontentloaded' });
  await newNote.waitFor();
  assert.equal(page.url(), signedInPath);
  assert.equal(await page.evaluate(() => sessionStorage.getItem('mt_refresh_token')), newRefresh);
  assert.deepEqual(refreshRequests, [oldRefresh, oldRefresh]);
  console.log('Login overlevede en midlertidig refresh-fejl ved genindlæsning og en ny genindlæsning.');

  checkingConcurrency = true;
  await page.evaluate(access => sessionStorage.setItem('mt_access_token', access), rejectedAccess);
  const retriedResponses = Promise.all([...studentRequests].map(path =>
    page.waitForResponse(response => new URL(response.url()).pathname === path && response.ok())));
  await page.goto(base + '/student', { waitUntil: 'domcontentloaded' });
  await retriedResponses;
  assert.deepEqual(rejectedRequests, studentRequests);
  assert.deepEqual(retriedRequests, studentRequests);
  assert.deepEqual(refreshRequests, [oldRefresh, oldRefresh, newRefresh]);
  assert.equal(await page.evaluate(() => sessionStorage.getItem('mt_refresh_token')), nextRefresh);
  assert.deepEqual(pageErrors, []);
  console.log('To samtidige 401-svar genbrugte samme fornyede adgangstoken.');
} finally {
  providerRecovered.resolve();
  concurrentRequests.resolve();
  await browser.close();
}
