// Samples are the authoritative lux values at tile centres. The texture
// interpolates those values for drawing only; readings stay on the exact tile.
const lastPaint = new WeakMap();
const resolution = 20;
const contours = [150, 300, 450, 600];

export function paintRoomLight(canvas, samples, columns, rows, ambient, measure) {
    const previous = lastPaint.get(canvas);
    if (previous && previous.columns === columns && previous.rows === rows &&
        previous.ambient === ambient && previous.measure === measure &&
        previous.samples.length === samples.length &&
        samples.every((value, i) => value === previous.samples[i])) return;

    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    lastPaint.set(canvas, { samples, columns, rows, ambient, measure });
    const width = columns * resolution;
    const height = rows * resolution;
    if (canvas.width !== width || canvas.height !== height) {
        canvas.width = width;
        canvas.height = height;
    }
    ctx.clearRect(0, 0, width, height);
    if (samples.length !== columns * rows) return;

    const sample = (x, y) => samples[Math.min(rows - 1, Math.max(0, y)) * columns +
        Math.min(columns - 1, Math.max(0, x))];
    const image = ctx.createImageData(width, height);
    for (let py = 0; py < height; py++) {
        const y = (py + .5) / resolution - .5;
        const y0 = Math.floor(y), fy = y - y0;
        for (let px = 0; px < width; px++) {
            const x = (px + .5) / resolution - .5;
            const nearest = sample(Math.round(x), Math.round(y));
            if (nearest < 0) continue;
            const x0 = Math.floor(x), fx = x - x0;
            // At the edge of the floor, extend the current reading into its
            // own half-cell instead of blending it with nonexistent tiles.
            const at = (col, row) => {
                const value = sample(col, row);
                return value < 0 ? nearest : value;
            };
            const top = at(x0, y0) * (1 - fx) + at(x0 + 1, y0) * fx;
            const bottom = at(x0, y0 + 1) * (1 - fx) + at(x0 + 1, y0 + 1) * fx;
            const lux = top * (1 - fy) + bottom * fy;
            const level = (measure ? lux : lux - ambient) / 600;
            const i = (py * width + px) * 4;
            image.data[i] = 255;
            image.data[i + 1] = 194;
            image.data[i + 2] = 72;
            image.data[i + 3] = Math.round(Math.max(0, Math.min(level, 1)) * (measure ? 156 : 112));
        }
    }
    ctx.putImageData(image, 0, 0);
    if (!measure) return;

    // March each rectangle between four real readings. Missing floor samples
    // break the contour, which keeps the view useful on irregular layouts.
    ctx.strokeStyle = "rgba(255, 251, 220, .95)";
    ctx.lineWidth = .8;
    for (const threshold of contours) {
        ctx.beginPath();
        for (let y = -1; y < rows; y++) {
            for (let x = -1; x < columns; x++) {
                const values = [sample(x, y), sample(x + 1, y), sample(x + 1, y + 1), sample(x, y + 1)];
                if (values.some(value => value < 0)) continue;
                const corners = [[x + .5, y + .5], [x + 1.5, y + .5],
                    [x + 1.5, y + 1.5], [x + .5, y + 1.5]];
                const crossings = [];
                for (let from = 0; from < 4; from++) {
                    const to = (from + 1) % 4;
                    if ((values[from] >= threshold) === (values[to] >= threshold)) continue;
                    const t = (threshold - values[from]) / (values[to] - values[from]);
                    crossings.push(corners[from].map((coordinate, axis) =>
                        (coordinate + (corners[to][axis] - coordinate) * t) * resolution));
                }
                if (crossings.length === 4 &&
                    ((values[0] >= threshold) === (values.reduce((a, b) => a + b) / 4 >= threshold)))
                    crossings.unshift(crossings.pop());
                for (let i = 0; i + 1 < crossings.length; i += 2) {
                    ctx.moveTo(...crossings[i]);
                    ctx.lineTo(...crossings[i + 1]);
                }
            }
        }
        ctx.stroke();
    }
}
