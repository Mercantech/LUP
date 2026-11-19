import test from 'node:test';
import assert from 'node:assert/strict';
import { enterFullscreen, leaveFullscreen } from '../src/fullscreen.mjs';

test('unsupported or denied native fullscreen leaves the viewport fallback available', async () => {
  assert.equal(await enterFullscreen(null), false);
  assert.equal(await enterFullscreen({}), false);
  assert.equal(await enterFullscreen({ requestFullscreen: async () => { throw new Error('denied'); } }), false);
});

test('native fullscreen reports the target that actually entered fullscreen', async () => {
  const document = { fullscreenElement: null };
  const element = { ownerDocument: document, requestFullscreen: async () => { document.fullscreenElement = element; } };
  assert.equal(await enterFullscreen(element), true);
  assert.equal(document.fullscreenElement, element);
});

test('closing an activity exits its own fullscreen and leaves other displays alone', async () => {
  let exits = 0;
  const other = {};
  const document = { fullscreenElement: other, exitFullscreen: async () => { exits++; document.fullscreenElement = null; } };
  const element = { ownerDocument: document, contains: candidate => candidate === element };
  assert.equal(await leaveFullscreen(element), false);
  assert.equal(exits, 0);
  document.fullscreenElement = element;
  assert.equal(await leaveFullscreen(element), true);
  assert.equal(exits, 1);
  assert.equal(document.fullscreenElement, null);
});

test('denied native fullscreen exit is recoverable by the surrounding view', async () => {
  const document = { fullscreenElement: null, exitFullscreen: async () => { throw new Error('denied'); } };
  const element = { ownerDocument: document, contains: candidate => candidate === element };
  document.fullscreenElement = element;
  assert.equal(await leaveFullscreen(element), false);
});
