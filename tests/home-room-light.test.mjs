import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const moduleSource = await readFile(new URL('../web/wwwroot/js/home-room-light.js', import.meta.url));
const { paintRoomLight } = await import('data:text/javascript;base64,' + moduleSource.toString('base64'));

function canvasFixture() {
    const context = {
        clears: 0, lines: [], pixels: null,
        clearRect() { this.clears++; this.lines = []; this.pixels = null; },
        createImageData(w, h) { return { width: w, data: new Uint8ClampedArray(w * h * 4) }; },
        putImageData(image) { this.pixels = image; },
        beginPath() {}, moveTo() {}, lineTo(x, y) { this.lines.push([x, y]); }, stroke() {}
    };
    return { canvas: { getContext: () => context }, context };
}

test('light follows measured tile values, with contours only in measurement mode', () => {
    const { canvas, context } = canvasFixture();
    const input = [40, 220, 455, 40, 220, 455];
    paintRoomLight(canvas, input, 3, 2, 40, true);
    const alpha = x => context.pixels.data[(10 * canvas.width + x) * 4 + 3];
    assert.ok(alpha(10) < alpha(30) && alpha(30) < alpha(50));
    assert.ok(context.lines.length > 0);
    paintRoomLight(canvas, [...input], 3, 2, 40, true);
    assert.equal(context.clears, 1);
    paintRoomLight(canvas, input, 3, 2, 40, false);
    assert.equal(alpha(0), 0);
    assert.equal(context.lines.length, 0);
    paintRoomLight(canvas, [], 3, 2, 40, false);
    assert.equal(context.pixels, null);
});
