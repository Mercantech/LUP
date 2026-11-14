// Run against the isolated notebook fixture (see tests/Notebook.BrowserQa/README.md).
import assert from 'node:assert/strict';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const base = 'http://localhost:9098';
const health = await fetch('http://127.0.0.1:8088/_qa/health');
assert.equal((await health.json()).fixture, 'fictional-notebook-browser-qa');
const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch();
const errors = [];

async function client(options = {}) {
  const context = await browser.newContext({ serviceWorkers: 'block', ...options });
  await context.route('**/*', route => {
    const url = new URL(route.request().url());
    return ['blob:', 'data:'].includes(url.protocol) || ['localhost', '127.0.0.1'].includes(url.hostname)
      ? route.continue() : route.abort('blockedbyclient');
  });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  page.on('pageerror', error => errors.push(error.message));
  return page;
}

async function signIn(page, user, game) {
  await page.goto(base + '/_qa/login?user=' + user);
  await page.waitForURL(url => !url.pathname.startsWith('/_qa/'));
  await page.goto(base + '/games/' + game);
}

function nextMove(page, game, action) {
  return page.waitForResponse(response => response.url().endsWith('/api/games/daily/' + game + '/' + action)
    && response.request().method() === 'POST');
}

try {
  const guest = await client();
  await guest.goto(base + '/games');
  for (const [name, game] of [['Dagens ord', 'dagens-ord'], ['Taljagt', 'taljagt']]) {
    await guest.getByRole('link', { name: 'Spil ' + name, exact: true }).click();
    const login = guest.getByRole('link', { name: 'Log ind og spil' });
    await login.waitFor();
    assert.equal(new URL(await login.getAttribute('href'), base).searchParams.get('returnUrl'), '/games/' + game);
    await guest.getByRole('link', { name: '← Alle spil' }).click();
  }
  assert.equal(await guest.getByRole('link', { name: 'Spil dagens Wordle' }).count(), 1);

  const desktop = await client({ viewport: { width: 1440, height: 950 } });
  await signIn(desktop, 'pupil', 'dagens-ord');
  await desktop.getByRole('textbox', { name: 'Dit gæt' }).waitFor();
  for (const number of [1, 2]) {
    const hint = nextMove(desktop, 'dagens-ord', 'hint');
    await desktop.getByRole('button', { name: 'Vis ekstra ledetråd ' + number }).click();
    const response = await hint;
    assert.equal(response.status(), 200);
    const round = await response.json();
    assert.equal(round.hints.length, number);
    assert.equal(round.answer ?? null, null);
    const hints = desktop.getByRole('list', { name: 'Åbnede ledetråde' });
    await hints.getByText(round.hints.at(-1), { exact: false }).waitFor();
    if (number === 1) {
      await desktop.reload();
      await hints.getByText(round.hints[0], { exact: false }).waitFor();
    }
  }
  let finished;
  for (const word of ['banan', 'aftensmad', 'kanin']) {
    const input = desktop.getByRole('textbox', { name: 'Dit gæt' });
    await input.fill(word);
    const guess = nextMove(desktop, 'dagens-ord', 'guess');
    await input.press('Enter');
    const response = await guess;
    assert.equal(response.status(), 200);
    finished = await response.json();
    if (finished.status !== 0) break;
  }
  assert.equal(finished.streak, 1);
  assert.ok(finished.answer && finished.explanation && finished.example);
  await desktop.getByText(finished.explanation, { exact: true }).waitFor();
  await desktop.reload();
  await desktop.getByText(finished.explanation, { exact: true }).waitFor();
  await desktop.goto(base + '/games/taljagt');
  await desktop.getByRole('button', { name: /brik 1/ }).waitFor();

  const mobile = await client({ viewport: { width: 320, height: 844 }, isMobile: true, hasTouch: true });
  await signIn(mobile, 'peer', 'dagens-ord');
  await mobile.getByRole('textbox', { name: 'Dit gæt' }).waitFor();
  assert.equal(await mobile.getByRole('list', { name: 'Åbnede ledetråde' }).count(), 0);
  assert.equal(await mobile.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  await signIn(mobile, 'peer', 'taljagt');
  const firstTile = mobile.getByRole('button', { name: /brik 1/ });
  await firstTile.waitFor();
  assert.equal(await mobile.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  await firstTile.tap();
  await mobile.getByRole('button', { name: 'Plus', exact: true }).tap();
  await mobile.getByRole('button', { name: /brik 2/ }).tap();
  assert.equal(await firstTile.isDisabled(), true);
  const math = nextMove(mobile, 'taljagt', 'submit');
  await mobile.getByRole('button', { name: /Aflevér/ }).tap();
  const response = await math;
  assert.equal(response.status(), 200);
  const round = await response.json();
  assert.equal(round.value, round.day.tiles[0] + round.day.tiles[1]);
  assert.equal(round.distance, Math.abs(round.value - round.day.target));
  assert.equal(round.score, Math.max(0, 100 - round.distance));
  assert.equal(round.streak, 1);
  assert.ok(round.example);
  await mobile.getByText('En løsning', { exact: false }).waitFor();
  await mobile.reload();
  await mobile.getByText('En løsning', { exact: false }).waitFor();

  assert.deepEqual(errors, []);
  console.log('Daglige spil: gæstevisning, loginretur, hints, gemte runder, mobil og kontoadskillelse bestod.');
} finally {
  await browser.close();
}
