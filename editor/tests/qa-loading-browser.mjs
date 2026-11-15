// Publish with: dotnet publish web -c Release -o .tools/loading-smoke
// Then run: node editor/tests/qa-loading-browser.mjs
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { mkdir, readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { extname, resolve, sep } from 'node:path';
import { pathToFileURL } from 'node:url';

const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const webRoot = resolve(process.argv[2] ?? '.tools/loading-smoke/wwwroot');
const output = process.env.LUP_STARTUP_QA_OUTPUT;
const mimeTypes = {
  '.css': 'text/css;charset=utf-8', '.html': 'text/html;charset=utf-8', '.js': 'text/javascript;charset=utf-8',
  '.json': 'application/json', '.svg': 'image/svg+xml', '.wasm': 'application/wasm',
  '.webp': 'image/webp', '.png': 'image/png', '.woff2': 'font/woff2'
};

await readFile(resolve(webRoot, 'index.html'));
if (output) await mkdir(output, { recursive: true });
const server = createServer(async (request, response) => {
  try {
    const { pathname } = new URL(request.url, 'http://127.0.0.1');
    if (pathname.startsWith('/api/')) {
      response.writeHead(404).end();
      return;
    }
    const path = extname(pathname) ? resolve(webRoot, '.' + decodeURIComponent(pathname)) : resolve(webRoot, 'index.html');
    if (!path.startsWith(webRoot + sep)) {
      response.writeHead(404).end();
      return;
    }
    const body = await readFile(path);
    response.writeHead(200, {
      'Content-Type': mimeTypes[extname(path)] ?? 'application/octet-stream',
      'Blazor-Environment': 'Production'
    }).end(body);
  } catch {
    response.writeHead(404).end();
  }
});
await new Promise((accept, reject) => {
  server.once('error', reject);
  server.listen(0, '127.0.0.1', accept);
});
const origin = 'http://127.0.0.1:' + server.address().port;
let browser;
try {
  browser = await chromium.launch();
  const light = await check('desktop-light', 'light', 'LUPP', 0, { viewport: { width: 1366, height: 850 } });
  const dark = await check('mobile-dark', 'dark', 'Raymond', 0.15, {
    viewport: { width: 390, height: 844 }, isMobile: true, deviceScaleFactor: 2,
    hasTouch: true, colorScheme: 'dark', reducedMotion: 'reduce'
  });
  assert.notEqual(light.character, dark.character, 'Different launch choices should show different characters.');
  assert.notEqual(light.portrait, dark.portrait, 'Each character should show its own portrait.');
  assert.notEqual(light.quote, dark.quote, 'Each character should have its own launch quote.');
  assert.notDeepEqual(light.palette, dark.palette, 'The startup screen should respect the selected theme.');
  console.log('LUP-startskærmen viste forskellige karakterer og fungerede i lyst desktop- og mørkt mobiltema.');
} finally {
  await browser?.close();
  server.closeAllConnections();
  await new Promise(done => server.close(done));
}

async function check(name, theme, expectedCharacter, random, options) {
  const context = await browser.newContext({ serviceWorkers: 'block', ...options });
  const boot = Promise.withResolvers();
  try {
    await context.addInitScript(({ theme, random }) => {
      localStorage.setItem('lup-theme', theme);
      Math.random = () => random;
    }, { theme, random });
    await context.route('**/*', async route => {
      const target = new URL(route.request().url());
      if (target.origin !== origin) return route.abort();
      if (/^\/_framework\/dotnet\.native.*\.wasm$/.test(target.pathname)) await boot.promise;
      return route.continue();
    });

    const page = await context.newPage();
    page.setDefaultTimeout(20_000);
    await page.clock.install();
    await page.goto(origin + '/videnstjek', { waitUntil: 'commit' });
    const splash = page.locator('#app [data-lup-startup]');
    await splash.getByText('LUP starter…', { exact: true }).waitFor();
    const attribution = splash.locator('[data-startup-character]');
    await attribution.getByText(expectedCharacter, { exact: true }).waitFor();
    const logo = splash.locator('.lup-logo-assembly');
    assert.deepEqual(await logo.locator('use').evaluateAll(parts => parts.map(part => part.getAttribute('href'))), [
      'images/branding/pixel-lup.svg#letter-l',
      'images/branding/pixel-lup.svg#letter-u',
      'images/branding/pixel-lup.svg#letter-p',
    ]);
    const logoAnimations = await logo.evaluate(mark => mark.getAnimations({ subtree: true }).length);
    assert.equal(logoAnimations, options.reducedMotion === 'reduce' ? 0 : 3);
    assert.equal(await page.locator('.loading-progress, .loading-progress-text').count(), 0);
    assert.ok(!(await splash.innerText()).includes('100%'));

    const portrait = splash.locator('[data-startup-portrait]');
    await page.waitForFunction(() => {
      const image = document.querySelector('[data-startup-portrait]');
      return image?.complete && image.naturalWidth > 0;
    });
    const scene = await splash.evaluate(element => {
      const styles = getComputedStyle(element);
      const body = getComputedStyle(document.body);
      return {
        theme: document.documentElement.dataset.bsTheme,
        width: document.documentElement.scrollWidth,
        viewport: window.innerWidth,
        palette: [styles.color, styles.backgroundColor, styles.backgroundImage, body.backgroundColor],
        animations: element.getAnimations({ subtree: true }).length
      };
    });
    const character = (await attribution.textContent()).trim();
    const quote = (await splash.locator('[data-startup-quote]').textContent()).trim();
    assert.ok(character && quote, 'Both attribution and quote should be visible during boot.');
    assert.equal(scene.theme, theme);
    assert.ok(scene.width <= scene.viewport, 'The splash should fit without horizontal scrolling.');
    if (options.reducedMotion === 'reduce') assert.equal(scene.animations, 0);
    const portraitPath = await portrait.getAttribute('src');
    if (output) await page.screenshot({ path: resolve(output, name + '.png') });

    await page.clock.fastForward(7_500);
    if (options.reducedMotion === 'reduce') {
      assert.equal((await attribution.textContent()).trim(), expectedCharacter);
    } else {
      await attribution.getByText('Raymond', { exact: true }).waitFor();
    }

    boot.resolve();
    await splash.waitFor({ state: 'detached' });
    await page.getByRole('heading', { name: /log ind/i }).waitFor();
    return { character, quote, portrait: portraitPath, palette: scene.palette };
  } finally {
    boot.resolve();
    await context.close();
  }
}
