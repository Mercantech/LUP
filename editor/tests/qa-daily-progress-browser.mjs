// Start a fresh loopback notebook fixture before running this script.
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const base = 'http://localhost:9098';
const fixture = 'http://127.0.0.1:8088';
assert.equal((await (await fetch(fixture + '/_qa/health')).json()).fixture, 'fictional-notebook-browser-qa');
const accounts = Object.fromEntries((await (await fetch(fixture + '/_qa/identities')).json()).map(row => [row.key, row]));
const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch();
const errors = [];

function request(account, path, method = 'GET', body) {
  return fetch(fixture + '/api/' + path, {
    method,
    headers: {
      Authorization: 'Bearer ' + account.token,
      'X-LUP-Profile-Subject': account.sub,
      'X-LUP-Daily-Subject': account.sub,
      'Content-Type': 'application/json'
    },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
}

async function json(account, path, method, body) {
  const response = await request(account, path, method, body);
  assert.ok(response.ok, path + ': ' + response.status);
  return response.json();
}

async function client(options = {}) {
  const context = await browser.newContext({ serviceWorkers: 'block', ...options });
  await context.route('**/*', route => {
    const url = new URL(route.request().url());
    return ['localhost', '127.0.0.1'].includes(url.hostname) || ['blob:', 'data:'].includes(url.protocol)
      ? route.continue() : route.abort('blockedbyclient');
  });
  const page = await context.newPage();
  page.on('pageerror', error => errors.push(error.message));
  page.setDefaultTimeout(20000);
  return page;
}

async function completeNumbers(account, tokens) {
  const round = await json(account, 'games/daily/taljagt/round');
  assert.equal(round.status, 0);
  return json(account, 'games/daily/taljagt/submit', 'POST', {
    date: round.day.date, version: round.version, requestId: randomUUID(), tokens
  });
}

try {
  const guest = await client();
  await guest.goto(base + '/games');
  await guest.getByRole('link', { name: 'Historik og ranglister' }).click();
  await guest.getByRole('heading', { name: 'Dine daglige spil' }).waitFor();
  const login = guest.getByRole('link', { name: /Log ind/ }).last();
  assert.equal(new URL(await login.getAttribute('href'), base).searchParams.get('returnUrl'), '/games/fremskridt');

  const [pupil, peer, outsider] = [accounts.pupil, accounts.peer, accounts.outsider];
  const settingsPath = 'games/progress/settings';
  const boardPath = 'games/progress/taljagt/leaderboard';
  const settings = await json(pupil, settingsPath);
  assert.equal(settings.visible, false);
  assert.ok(settings.holds.length);
  const holdId = settings.holds[0].id;
  const otherBoard = await request(outsider, boardPath + '?holdId=' + holdId);
  assert.ok([403, 404].includes(otherBoard.status));

  for (const [account, name] of [[pupil, 'QA Taljæger'], [peer, 'QA Regnemester']])
    await json(account, 'me/profile', 'PUT', { displayName: name });
  const play = await completeNumbers(pupil, ['0', '+', '1']);
  await completeNumbers(peer, ['0', '+', '1']);
  await json(peer, settingsPath, 'PUT', { visible: true });
  const before = await json(pupil, boardPath + '?period=day');
  assert.deepEqual(before.entries.map(entry => entry.name), ['QA Regnemester']);
  assert.equal(before.own, null);

  const phone = await client({ viewport: { width: 340, height: 844 }, isMobile: true, hasTouch: true });
  await phone.goto(base + '/_qa/login?user=pupil');
  await phone.waitForURL(url => !url.pathname.startsWith('/_qa/'));
  await phone.goto(base + '/games/fremskridt?game=taljagt');
  await phone.getByRole('heading', { name: 'Din historik' }).waitFor();
  await phone.getByRole('heading', { name: 'Rangliste', exact: true }).waitFor();
  assert.equal(await phone.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  const history = await json(pupil, 'games/progress/taljagt/history');
  assert.equal(history.entries.length, 1);
  assert.equal(history.entries[0].score, play.score);
  assert.equal(history.entries[0].distance, play.distance);
  assert.equal(history.streak, 1);
  await phone.getByRole('button', { name: 'Vis mig på ranglister' }).tap();
  await phone.getByRole('button', { name: 'Skjul mig på ranglister' }).waitFor();
  await phone.getByLabel('Periode').selectOption('week');
  await phone.getByLabel('Vis rangliste for').selectOption(holdId);
  const ranking = phone.getByRole('table', { name: /Placeringer/ });
  await ranking.getByText('QA Regnemester', { exact: false }).waitFor();
  await ranking.getByText('QA Taljæger', { exact: false }).waitFor();

  for (const period of ['day', 'week', 'month']) {
    const board = await json(pupil, boardPath + '?period=' + period + '&holdId=' + holdId);
    assert.deepEqual(board.entries.map(entry => entry.rank), [1, 1]);
    assert.equal(board.own.name, 'QA Taljæger');
    assert.equal(board.own.score, play.score);
    assert.ok(board.entries.every(entry => !('userSub' in entry) && !('email' in entry)));
  }

  await phone.getByRole('button', { name: 'Skjul mig på ranglister' }).tap();
  await phone.getByRole('button', { name: 'Vis mig på ranglister' }).waitFor();
  const after = await json(peer, boardPath + '?period=day');
  assert.deepEqual(after.entries.map(entry => entry.name), ['QA Regnemester']);
  assert.equal((await json(pupil, 'games/progress/taljagt/history')).entries.length, 1);
  assert.deepEqual(errors, []);
  console.log('Historik, mobil, privat adgang, ranglisteperioder, delt placering og til-/fravalg bestod.');
} finally {
  await browser.close();
}
