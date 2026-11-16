const assert = require('node:assert/strict');
const { readFile } = require('node:fs/promises');
const path = require('node:path');
const test = require('node:test');
const { chromium } = require('../../release-news/node_modules/playwright');

const app = 'https://lup.example';
const tokenUrl = 'https://auth.example/oauth/token';
const html = '<!doctype html><script src="/js/auth-storage.js"></script><script>'
    + 'window.authReady = mercantecAuth.getTokens(() => {});'
    + '</script>';
const expires = () => Math.floor(Date.now() / 1000) + 3600;

function deferred() {
    let resolve;
    const promise = new Promise(done => { resolve = done; });
    return { promise, resolve };
}

test('Chromium shares live LUP sessions and safely rotates and clears their tokens', async () => {
    const source = await readFile(path.join(__dirname, '../../web/wwwroot/js/auth-storage.js'), 'utf8');
    const browser = await chromium.launch({ headless: true });
    try {
        const context = await browser.newContext();
        await context.route(/^https:\/\/(?:lup|other-lup)\.example\//, async route => {
            if (new URL(route.request().url()).pathname === '/js/auth-storage.js')
                return route.fulfill({ contentType: 'application/javascript', body: source });
            return route.fulfill({ contentType: 'text/html', body: html });
        });

        const requests = [];
        const lateRequest = deferred();
        const releaseLateResponse = deferred();
        await context.route(tokenUrl, async route => {
            const body = new URLSearchParams(route.request().postData());
            const refresh = body.get('refresh_token');
            requests.push({ grant: body.get('grant_type'), client: body.get('client_id'), refresh });
            if (refresh === 'rotated-refresh') {
                lateRequest.resolve();
                await releaseLateResponse.promise;
            }
            return route.fulfill({
                contentType: 'application/json',
                headers: { 'Access-Control-Allow-Origin': app },
                body: JSON.stringify({ access_token: 'rotated-access', refresh_token: 'rotated-refresh', expires_in: 3600 }),
            });
        });

        const first = await context.newPage();
        await first.addInitScript(expiry => {
            sessionStorage.setItem('mt_access_token', 'first-access');
            sessionStorage.setItem('mt_refresh_token', 'first-refresh');
            sessionStorage.setItem('mt_expires_at', String(expiry));
        }, expires());
        await first.goto(app + '/teacher');
        await first.evaluate(() => authReady);
        assert.equal(await first.evaluate(() => isSecureContext && !!navigator.locks), true);

        const deepLink = app + '/notebook/123?owner=a#latest';
        const second = await context.newPage();
        await second.goto(deepLink);
        assert.equal(await second.evaluate(() => window.opener), null);
        assert.equal((await second.evaluate(() => authReady)).access, 'first-access');
        assert.equal(second.url(), deepLink);
        assert.doesNotMatch(await second.evaluate(() => JSON.stringify(localStorage)), /first-access|first-refresh/);

        const otherOrigin = await context.newPage();
        await otherOrigin.goto('https://other-lup.example/');
        assert.equal((await otherOrigin.evaluate(() => authReady)).access, null);
        await otherOrigin.close();

        const refresh = (tab, rejected) => tab.evaluate(
            ({ endpoint, bearer }) => mercantecAuth.refreshTokens(endpoint, 'client', bearer, false),
            { endpoint: tokenUrl, bearer: rejected },
        );
        const results = await Promise.all([refresh(first, 'first-access'), refresh(second, 'first-access')]);
        assert.deepEqual(results.map(result => result.access), ['rotated-access', 'rotated-access']);
        assert.deepEqual(requests, [{ grant: 'refresh_token', client: 'client', refresh: 'first-refresh' }]);

        const oldRefresh = refresh(second, 'rotated-access');
        await lateRequest.promise;
        await first.evaluate(() => mercantecAuth.clearTokens(true));
        await second.waitForFunction(() => mercantecAuth.getTokens().access === null);
        releaseLateResponse.resolve();
        assert.equal((await oldRefresh).access, null);
        assert.equal(await first.evaluate(() => mercantecAuth.getTokens().access), null);
        assert.equal(await second.evaluate(() => mercantecAuth.getTokens().access), null);

        // Ending the final participating tab also ends sharing, even if the marker still exists.
        await first.evaluate(expiry => mercantecAuth.setTokens('last-access', 'last-refresh', expiry), expires());
        await Promise.all([first.close(), second.close()]);
        const afterClosing = await context.newPage();
        await afterClosing.goto(deepLink);
        assert.equal((await afterClosing.evaluate(() => authReady)).access, null);
        assert.equal(afterClosing.url(), deepLink);
        assert.equal(requests.length, 2);
    } finally {
        await browser.close();
    }
});
