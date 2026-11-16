const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');

function playwright() {
    try { return require('playwright'); }
    catch {
        return require(path.join(os.homedir(), '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'));
    }
}

async function verify() {
    const folder = path.resolve(process.argv[2] || path.join(__dirname, '../../audio'));
    const manifest = JSON.parse(await fs.readFile(path.join(folder, 'audio-manifest.json'), 'utf8'));
    const signals = JSON.parse(await fs.readFile(path.join(folder, 'audio-validation.json'), 'utf8'));
    assert.equal(manifest.avatarId, 'bilbo');
    assert.equal(manifest.sfx.length, 28);
    assert.equal(manifest.phrases.length, 5);
    assert.equal(signals.passed, true);
    const expected = new Map(signals.items.map(item => [item.file, item.sha256]));
    const browser = await playwright().chromium.launch({
        executablePath: process.env.LUP_QA_CHROME || 'C:/Program Files/Google/Chrome/Application/chrome.exe',
        headless: true,
    });
    const results = [];
    try {
        const page = await browser.newPage();
        await page.setContent('<!doctype html><meta charset="utf-8"><title>Bilbo audio QA</title><button>Afspil aktuelt klip</button>');
        await page.evaluate(() => {
            const decoder = new AudioContext({ sampleRate: 44100 });
            window.loadClip = async encoded => {
                window.result = null;
                const data = Uint8Array.from(atob(encoded), char => char.charCodeAt(0));
                const decoded = await decoder.decodeAudioData(data.buffer.slice(0));
                const samples = decoded.getChannelData(0);
                let peak = 0;
                for (const sample of samples) peak = Math.max(peak, Math.abs(sample));
                const url = URL.createObjectURL(new Blob([data], {type: 'audio/wav'}));
                const player = new Audio(url);
                player.volume = .65;
                document.querySelector('button').onclick = async event => {
                    try {
                        await player.play();
                        window.result = {
                            trustedClick: event.isTrusted,
                            playback: player.paused ? 'failed' : 'started',
                            sampleRate: decoded.sampleRate,
                            channels: decoded.numberOfChannels,
                            durationSeconds: decoded.duration,
                            peak,
                        };
                    } catch (error) { window.result = { error: String(error) }; }
                    finally {
                        player.pause();
                        player.removeAttribute('src');
                        player.load();
                        URL.revokeObjectURL(url);
                    }
                };
            };
        });
        for (const entry of [...manifest.sfx, ...manifest.phrases]) {
            const bytes = await fs.readFile(path.join(folder, entry.file));
            const sha256 = createHash('sha256').update(bytes).digest('hex');
            assert.equal(sha256, expected.get(entry.file), entry.file);
            await page.evaluate(async encoded => window.loadClip(encoded), bytes.toString('base64'));
            await page.getByRole('button').click();
            await page.waitForFunction(() => window.result !== null);
            const result = await page.evaluate(() => window.result);
            assert.equal(result.error, undefined, entry.file);
            assert.equal(result.trustedClick, true, entry.file);
            assert.equal(result.playback, 'started', entry.file);
            assert.equal(result.sampleRate, 44100, entry.file);
            assert.equal(result.channels, 1, entry.file);
            assert.ok(result.peak > .025 && result.peak <= .77, entry.file);
            assert.ok(Math.abs(result.durationSeconds - entry.durationSeconds) < .001, entry.file);
            results.push({id: entry.id, file: entry.file, sha256, ...result});
        }
    } finally { await browser.close(); }
    const report = {
        passed: true, decoded: results.length, playbackStarted: results.length,
        spokenPlaybackStarted: results.filter(item => item.id.startsWith('phrase_')).length,
        scope: 'Real Chromium decode, PCM peak and native playback start after trusted clicks. Local asset fixture.',
        results,
    };
    await fs.writeFile(path.join(folder, 'browser-audio-validation.json'), JSON.stringify(report, null, 2) + '\n');
    console.log(`Decoded and played ${report.decoded} clips, including ${report.spokenPlaybackStarted} Danish phrases.`);
}

verify().catch(error => { console.error(error); process.exitCode = 1; });
