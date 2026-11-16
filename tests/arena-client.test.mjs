import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';

async function moduleUrl(file) {
    let source = await readFile(new URL('../web/wwwroot/js/' + file, import.meta.url), 'utf8');
    for (const [statement, dependency] of source.matchAll(/from '\.\/([\w.-]+\.js)(?:\?[^']+)?'/g)) {
        source = source.replace(statement, 'from ' + JSON.stringify(await moduleUrl(dependency)));
    }
    return 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
}

const arena = await import(await moduleUrl('arena-client.js'));

async function fixture(t) {
    let now = 0, socket;
    const intervals = new Map();
    const document = Object.assign(new EventTarget(), {
        hidden: false, activeElement: null, getElementById: () => null
    });
    const window = new EventTarget();
    const canvas = Object.assign(new EventTarget(), {
        tagName: 'CANVAS', offsetWidth: 1280, offsetHeight: 720,
        focus() { document.activeElement = this; },
        getContext: () => ({}), getBoundingClientRect: () => ({ width: 1280, height: 720 })
    });
    const globals = {
        window, document, location: { href: 'https://arena.example' }, devicePixelRatio: 1,
        performance: { now: () => now }, matchMedia: () => ({ matches: false }),
        requestAnimationFrame: () => 1, cancelAnimationFrame() {},
        ResizeObserver: class { observe() {} disconnect() {} },
        Image: class { set src(value) { queueMicrotask(() => this.onerror?.()); } },
        setInterval(handler, delay) { intervals.set(delay, handler); return delay; },
        clearInterval(id) { intervals.delete(id); },
        WebSocket: class {
            static OPEN = 1;
            readyState = 1;
            bufferedAmount = 0;
            sent = [];
            closes = [];
            constructor() { socket = this; }
            send(packet) { this.sent.push(JSON.parse(packet)); }
            close(code) { this.closes.push(code); }
            message(packet) { this.onmessage({ data: JSON.stringify(packet) }); }
        }
    };
    const original = new Map();
    for (const [name, value] of Object.entries(globals)) {
        original.set(name, Object.getOwnPropertyDescriptor(globalThis, name));
        Object.defineProperty(globalThis, name, { value, writable: true, configurable: true });
    }
    t.after(() => {
        arena.dispose();
        for (const [name, descriptor] of original) {
            if (descriptor) Object.defineProperty(globalThis, name, descriptor);
            else delete globalThis[name];
        }
    });
    await arena.mount(canvas, { invokeMethodAsync: async () => {} }, {
        stages: [{ id: 'workshop' }], selectedAvatarId: 'starter', soundEnabled: false
    });
    arena.connect('/api/arena/connect', 'local');
    socket.onopen();

    return {
        document, canvas, socket,
        clickSoundButton(detail) {
            const button = {
                tagName: 'BUTTON', getAttribute: () => 'true',
                closest(selector) { return selector === '[data-arena-sound-toggle]' ? this : null; }
            };
            document.activeElement = button;
            const click = Object.assign(new Event('click'), { detail });
            Object.defineProperty(click, 'target', { value: button });
            document.dispatchEvent(click);
            return button;
        },
        key(code) {
            window.dispatchEvent(Object.assign(new Event('keydown', { cancelable: true }), { code }));
        },
        room(phase) { socket.message({ type: 'room', room: { phase, players: [], stageId: 'workshop' } }); },
        snapshot(phase, tick) {
            socket.message({ type: 'state', state: { phase, tick, stageId: 'workshop', fighters: [] } });
        },
        heartbeat(time) {
            now = time;
            intervals.get(3000)();
            socket.message({ type: 'pong', sentAt: 0 });
        }
    };
}

test('a pointer click on sound restores play while keyboard activation keeps button focus', async t => {
    const client = await fixture(t);
    client.snapshot('playing', 120);
    const button = client.clickSoundButton(0);
    assert.equal(client.document.activeElement, button);
    client.key('Space');
    assert.ok(!client.socket.sent.some(packet => packet.type === 'input'));

    client.clickSoundButton(1);
    assert.equal(client.document.activeElement, client.canvas);
    client.key('Space');
    assert.equal(client.socket.sent.at(-1).jump, true);
});

test('pongs and repeated snapshots cannot mask a stalled match', async t => {
    const client = await fixture(t);
    client.snapshot('playing', 120);
    for (const time of [3000, 6000, 9000, 12000]) {
        client.heartbeat(time);
        client.snapshot('playing', 120);
    }
    assert.deepEqual(client.socket.closes, [4000]);
});

test('a countdown without the first snapshot is recovered as a stalled match', async t => {
    const client = await fixture(t);
    client.room('countdown');
    for (const time of [3000, 6000, 9000, 12000]) client.heartbeat(time);
    assert.deepEqual(client.socket.closes, [4000]);
});

test('lobby, advancing match and finished game allow a healthy socket to stay open', async t => {
    const client = await fixture(t);
    client.room('lobby');
    for (const time of [3000, 6000, 9000, 12000]) client.heartbeat(time);

    client.room('playing');
    for (const time of [15000, 18000, 21000, 24000]) {
        client.heartbeat(time);
        client.snapshot('playing', time / 50);
    }

    client.room('finished');
    client.snapshot('finished', 490);
    for (const time of [27000, 30000, 33000, 36000]) client.heartbeat(time);
    assert.deepEqual(client.socket.closes, []);
});
