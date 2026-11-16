import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createContext, runInContext } from 'node:vm';

const source = await readFile(new URL('../../web/wwwroot/js/auth-storage.js', import.meta.url), 'utf8');
const tokenUrl = 'https://auth.example/oauth/token';
const expiresAt = () => Math.floor(Date.now() / 1000) + 3600;
const tick = () => new Promise(resolve => setImmediate(resolve));
const reply = (access, refresh) => ({
    ok: true, status: 200,
    async json() {
        return { access_token: access, refresh_token: refresh, expires_in: 900 };
    },
});

function browser() {
    const shared = new Map();
    const tabs = new Set();
    const locks = new Map();
    let fetch = () => assert.fail('Unexpected OAuth request');

    function open(values = new Map()) {
        const tab = { values, listeners: new Map() };
        tabs.add(tab);
        const localStorage = {
            getItem: key => shared.get(key) ?? null,
            setItem(key, value) {
                const oldValue = shared.get(key) ?? null;
                shared.set(key, value);
                for (const other of tabs) {
                    if (other !== tab) setImmediate(() => other.listeners.get('storage')?.({ key, oldValue }));
                }
            },
            removeItem(key) {
                shared.delete(key);
            },
        };
        class BroadcastChannel {
            constructor() {
                tab.channel = this;
            }
            postMessage(data) {
                for (const other of tabs) {
                    if (other !== tab && !other.suspended) {
                        setImmediate(() => other.channel?.onmessage?.({ data: structuredClone(data) }));
                    }
                }
            }
        }
        const window = { localStorage, addEventListener: (event, handler) => tab.listeners.set(event, handler) };
        const context = createContext({
            window, BroadcastChannel, crypto, setTimeout, clearTimeout, AbortController, URLSearchParams,
            navigator: {
                locks: {
                    request(name, action) {
                        const operation = (locks.get(name) ?? Promise.resolve()).then(action);
                        locks.set(name, operation.catch(() => {}));
                        return operation;
                    },
                },
            },
            fetch: (...args) => fetch(...args),
            sessionStorage: {
                getItem: key => values.get(key) ?? null,
                setItem: (key, value) => values.set(key, String(value)),
                removeItem: key => values.delete(key),
            },
        });
        runInContext(source, context, { filename: 'auth-storage.js' });
        return Object.assign(tab, {
            auth: window.mercantecAuth,
            close: () => tabs.delete(tab),
        });
    }

    return { open, shared, setFetch: mock => { fetch = mock; } };
}

test('a new tab adopts a live session without copying PKCE or a pending return destination', async () => {
    const app = browser();
    const first = app.open(new Map([
        ['mt_access_token', 'legacy-access'],
        ['mt_refresh_token', 'legacy-refresh'],
        ['mt_expires_at', String(expiresAt())],
    ]));
    await first.auth.getTokens(() => {});
    first.auth.setPkce('verifier', 'state');
    first.auth.setLoginReturnPath('/notebook?owner=a#latest');

    const second = app.open();
    const tokens = await second.auth.getTokens(() => {});
    assert.equal(tokens.access, 'legacy-access');
    assert.equal(tokens.refresh, 'legacy-refresh');
    assert.equal(second.auth.getPkce().verifier, null);
    assert.equal(second.auth.consumeLoginReturnPath(), null);
    assert.equal(first.auth.consumeLoginReturnPath(), '/notebook?owner=a#latest');

    assert.doesNotMatch(JSON.stringify([...app.shared]), /legacy-access|legacy-refresh|verifier|notebook/);
    first.close();
    second.close();
    const third = app.open();
    assert.equal((await third.auth.getTokens(() => {})).access, null);
});

test('simultaneous 401s in two tabs rotate the refresh token once', async () => {
    const app = browser();
    const first = app.open();
    const second = app.open();
    await first.auth.setTokens('rejected-access', 'old-refresh', expiresAt());
    await second.auth.getTokens(() => {});
    const submitted = [];
    app.setFetch(async (url, request) => {
        assert.equal(url, tokenUrl);
        assert.equal(request.body.get('grant_type'), 'refresh_token');
        submitted.push(request.body.get('refresh_token'));
        await tick();
        return reply('new-access', 'new-refresh');
    });

    const results = await Promise.all([
        first.auth.refreshTokens(tokenUrl, 'client', 'rejected-access', false),
        second.auth.refreshTokens(tokenUrl, 'client', 'rejected-access', false),
    ]);
    assert.deepEqual(submitted, ['old-refresh']);
    assert.deepEqual(results.map(result => result.access), ['new-access', 'new-access']);
    assert.equal(second.auth.getTokens().refresh, 'new-refresh');
});

test('a resumed tab never sends a refresh token another tab already rotated', async () => {
    const app = browser();
    const first = app.open();
    const second = app.open();
    await first.auth.setTokens('first-access', 'first-refresh', expiresAt());
    await second.auth.getTokens(() => {});
    second.suspended = true;
    let requests = 0;
    app.setFetch(async () => {
        requests++;
        return reply('second-access', 'second-refresh');
    });
    await first.auth.refreshTokens(tokenUrl, 'client', 'first-access', false);
    await tick();
    second.suspended = false;
    assert.equal(second.auth.getTokens().refresh, 'first-refresh');

    const result = await second.auth.refreshTokens(tokenUrl, 'client', 'first-access', false);
    assert.equal(result.access, 'second-access');
    assert.equal(second.auth.getTokens().refresh, 'second-refresh');
    assert.equal(requests, 1);
});

test('a missed rotation with no live peer falls back to login once the old access token expires', async () => {
    const app = browser();
    const first = app.open();
    const second = app.open();
    await first.auth.setTokens('first-access', 'first-refresh', 1);
    await second.auth.getTokens(() => {});
    second.suspended = true;
    let requests = 0;
    app.setFetch(async () => {
        requests++;
        return reply('second-access', 'second-refresh');
    });
    await first.auth.refreshTokens(tokenUrl, 'client', null, false);
    first.close();
    second.suspended = false;

    const result = await second.auth.refreshTokens(tokenUrl, 'client', null, true);
    assert.equal(result.sessionEnded, true);
    assert.equal(second.auth.getTokens().access, null);
    assert.equal(requests, 1);
});

test('logout across tabs discards both a late refresh and a pending OAuth completion', async () => {
    const app = browser();
    const first = app.open();
    const second = app.open();
    await first.auth.setTokens('first-access', 'first-refresh', expiresAt());
    let changes = 0;
    await second.auth.getTokens(() => { changes++; });
    second.auth.setPkce('verifier', 'state');
    const epoch = second.auth.getPkce().loginEpoch;
    let releaseToken;
    app.setFetch(() => new Promise(resolve => { releaseToken = resolve; }));
    const refreshing = second.auth.refreshTokens(tokenUrl, 'client', 'first-access', false);
    await tick();
    assert.equal(typeof releaseToken, 'function');

    await first.auth.clearTokens(true);
    releaseToken(reply('late-access', 'late-refresh'));
    assert.equal((await refreshing).access, null);
    await tick();
    assert.equal(changes, 1);
    assert.equal(second.auth.getTokens().access, null);
    assert.equal(await second.auth.setTokens('late-login', 'late-refresh', expiresAt(), epoch), false);
    assert.doesNotMatch(JSON.stringify([...app.shared]), /first-access|late-refresh|verifier/);
});

test('a revoked refresh clears the shared session when the token server returns invalid_grant', async () => {
    const app = browser();
    const first = app.open();
    const second = app.open();
    await first.auth.setTokens('first-access', 'first-refresh', expiresAt());
    let changes = 0;
    await second.auth.getTokens(() => { changes++; });
    app.setFetch(async () => ({ status: 400, async json() { return { error: 'invalid_grant' }; } }));

    const result = await first.auth.refreshTokens(tokenUrl, 'client', 'first-access', false);
    await tick();
    assert.equal(result.sessionEnded, true);
    assert.equal(first.auth.getTokens().access, null);
    assert.equal(second.auth.getTokens().access, null);
    assert.equal(changes, 1);
});
