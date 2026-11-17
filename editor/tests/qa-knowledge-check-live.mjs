// Run against the explicit Notebook.BrowserQa loopback fixture and a published Web build.
import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const origin = 'http://127.0.0.1:' + (process.env.NOTEBOOK_BROWSER_QA_WEB_PORT ?? '9098');
const fixture = 'http://127.0.0.1:' + (process.env.NOTEBOOK_BROWSER_QA_API_PORT ?? '8088');
const name = 'QA Klassespil';
const live = '/api/knowledge-checks/live';
const artifacts = process.env.LIVE_QUIZ_QA_IMAGES;
const health = await (await fetch(fixture + '/_qa/health')).json();
assert.equal(health.fixture, 'fictional-notebook-browser-qa');
const identities = await (await fetch(fixture + '/_qa/identities')).json();
const teacher = identities.find(identity => identity.key === 'teacher');
const pupil = identities.find(identity => identity.key === 'pupil');
assert.ok(teacher && pupil);
const questions = [
  question('single', 'Hvilket tal er størst?', ['Tre', 'Fem', 'To', 'Et'], [1]),
  question('multiple', 'Hvilke to tal er lige?', ['To', 'Fire', 'Tre', 'Fem'], [0, 1]),
  question('short', 'Skriv tallet efter 41.', [], [], ['42'])
];

function question(kind, prompt, options, correctOptions, acceptedAnswers = []) {
  return { id: crypto.randomUUID(), kind, prompt, options, correctOptions, acceptedAnswers,
    manualGrading: false, explanation: 'Læs spørgsmålet igen og tjek tallet.' };
}

async function request(path, { method = 'GET', body, identity, playerToken } = {}) {
  return fetch(fixture + path, {
    method,
    headers: {
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...(identity ? { Authorization: 'Bearer ' + identity.token } : {}),
      ...(playerToken ? { 'X-Lup-Live-Player': playerToken } : {})
    },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
}

const draftResponse = await request('/api/knowledge-checks', {
  method: 'POST', identity: teacher, body: { title: name, description: '', topic: 'QA', questions }
});
if (!draftResponse.ok) throw new Error('Fixture draft could not be created: ' + await draftResponse.text());
const draft = await draftResponse.json();
assert.ok(draft?.id);
assert.equal((await request(live, { method: 'POST', identity: pupil,
  body: { sourceId: draft.id, fromDraft: true, questionSeconds: 20 } })).status, 403);

const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch();
if (artifacts) await mkdir(artifacts, { recursive: true });

try {
  const host = await open({ width: 1366, height: 900 });
  const alice = await open({ width: 390, height: 844 });
  const bo = await open({ width: 360, height: 800 });
  await host.goto(origin + '/_qa/login?user=teacher&screen=videnstjek');
  await host.getByRole('link', { name: 'Start klassespil', exact: true }).click();
  await host.getByLabel('Videnstjek', {exact: true}).selectOption({ label: name });
  await host.getByLabel('Tid pr. spørgsmål').selectOption('60');
  await host.getByRole('button', { name: 'Åbn lobby' }).click();
  await host.waitForURL(url => /^\/videnstjek\/live\/\d{6}\/vaert$/.test(url.pathname));
  const code = new URL(host.url()).pathname.split('/')[3];
  const statePath = live + '/' + code;
  const qr = host.getByRole('img', { name: 'Scan QR-koden for at åbne spillet' });
  await qr.waitFor();
  assert.match(await qr.getAttribute('src'), /^data:image\/png;base64,/);
  assert.equal((await request(statePath)).status, 404);
  assert.equal((await request(statePath + '/advance', { method: 'POST', identity: pupil,
    body: { phase: 'lobby', questionIndex: -1 } })).status, 403);

  await alice.goto(origin + '/spil');
  await alice.getByLabel('PIN-kode').fill(code);
  await alice.getByRole('button', { name: 'Fortsæt', exact: true }).click();
  await bo.goto(origin + '/spil/' + code);
  await join(alice, 'Ada');
  await join(bo, 'Bo');

  await host.getByRole('button', { name: 'Fjern Bo', exact: true }).waitFor();
  await host.getByRole('button', { name: 'Luk for nye deltagere' }).click();
  await host.getByRole('button', { name: 'Åbn for deltagere' }).waitFor();
  assert.equal((await request(statePath + '/join', {
    method: 'POST', body: { nickname: 'Tredje' }
  })).status, 409);
  await capture(host, '01-lobby');

  await host.getByRole('button', { name: 'Start spil' }).click();
  await alice.getByRole('heading', { name: questions[0].prompt, exact: true }).waitFor();
  await capture(alice, '02-player-question');
  await choices(alice).getByRole('button', { name: 'Fem', exact: true }).click();
  await alice.getByRole('heading', { name: 'Svaret er låst' }).waitFor();
  const playerToken = await alice.evaluate(code => window.lupLive.getPlayerToken(code), code);
  const locked = await (await request(statePath, { playerToken })).json();
  assert.equal(locked.phase, 'question');
  assert.equal(locked.reveal, null);
  assert.equal(locked.personal.correct, null);
  assert.deepEqual(locked.personal.selectedOptions, [1]);
  await alice.reload();
  await alice.getByRole('heading', { name: 'Svaret er låst' }).waitFor();
  await choices(bo).getByRole('button', { name: 'To', exact: true }).click();
  await host.getByRole('heading', { name: 'Facit', exact: true }).waitFor();
  await alice.getByRole('heading', { name: 'Helt rigtigt!' }).waitFor();
  await bo.getByRole('heading', { name: 'Forkert svar' }).waitFor();
  assert.equal((await request(statePath + '/advance', { method: 'POST', identity: teacher,
    body: { phase: 'question', questionIndex: 0 } })).status, 409);
  await host.getByRole('button', { name: 'Vis pointtavlen' }).click();
  await alice.getByRole('heading', { name: 'Din placering' }).waitFor();
  await capture(host, '03-pointtavle');

  await host.getByRole('button', { name: 'Næste spørgsmål' }).click();
  await choices(alice).getByRole('button', { name: 'To', exact: true }).click();
  await choices(alice).getByRole('button', { name: 'Fire', exact: true }).click();
  await alice.getByRole('button', { name: 'Lås svar', exact: true }).click();
  await alice.getByRole('heading', { name: 'Svaret er låst' }).waitFor();
  await host.getByRole('button', { name: 'Vis facit' }).click();
  await bo.getByRole('heading', { name: 'Tiden er gået' }).waitFor();
  await host.getByRole('button', { name: 'Vis pointtavlen' }).click();
  await host.getByRole('button', { name: 'Næste spørgsmål' }).click();
  await alice.getByLabel('Dit svar', {exact: true}).fill('42');
  await alice.getByRole('button', { name: 'Lås svar' }).click();
  await bo.getByLabel('Dit svar', {exact: true}).fill('42');
  await bo.getByRole('button', { name: 'Lås svar' }).click();
  await host.getByRole('button', { name: 'Vis pointtavlen' }).click();
  await host.getByRole('button', { name: 'Vis podiet' }).click();
  await host.getByRole('heading', { name: 'Podiet', exact: true }).waitFor();
  await alice.getByRole('heading', { name: 'Spillet er slut' }).waitFor();
  await capture(host, '04-podie');
  const final = await (await request(statePath, { identity: teacher })).json();
  assert.equal(final.phase, 'finished');
  assert.deepEqual(final.standings.map(row => row.nickname), ['Ada', 'Bo']);
  assert.ok(final.standings[0].score > final.standings[1].score);
} finally {
  await browser.close();
}

console.log('Klassespil bestod med en underviser, to gæster, gemt svar efter genindlæsning, alle svartyper og podie.');

async function open(viewport) {
  const context = await browser.newContext({ viewport, serviceWorkers: 'block' });
  await context.route('**/*', route => {
    const target = new URL(route.request().url());
    return target.origin === origin || target.protocol === 'data:' ? route.continue() : route.abort();
  });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  return page;
}

function choices(page) { return page.getByRole('group', { name: 'Svarmuligheder' }); }

async function join(page, nickname) {
  await page.getByLabel('Kaldenavn', { exact: true }).fill(nickname);
  await page.getByRole('button', { name: 'Deltag', exact: true }).click();
  await page.getByRole('heading', { name: 'Du er med!' }).waitFor();
}

async function capture(page, file) {
  if (artifacts) await page.screenshot({ path: resolve(artifacts, file + '.png'), animations: 'disabled' });
}
