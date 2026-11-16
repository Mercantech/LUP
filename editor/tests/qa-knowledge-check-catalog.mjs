// Run against qa-notebook-harness.mjs. --seed-only prepares the catalog for Hallmark screenshots.
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const origin = 'http://localhost:9098';
const fixture = 'http://127.0.0.1:8088';
assert.equal((await (await fetch(fixture + '/_qa/health')).json()).fixture, 'fictional-notebook-browser-qa');
const identities = await (await fetch(fixture + '/_qa/identities')).json();
const teacher = identities.find(identity => identity.key === 'teacher');
const { drafts: legacy } = JSON.parse(await readFile(new URL('../../api/KnowledgeChecks/LegacyTemplates.json', import.meta.url)));
const { draft: quiz } = legacy.find(item => item.key === 'gf2-dag3-control-flow');

async function api(path = '', body) {
  const headers = { Authorization: 'Bearer ' + teacher.token };
  const response = await fetch(fixture + '/api/knowledge-checks' + path, body === undefined ? { headers } : {
    method: 'POST', headers: { ...headers, 'Content-Type': 'application/json' }, body: JSON.stringify(body),
  });
  assert.ok(response.ok, path + ' returned ' + response.status);
  return response.json();
}

await api('/legacy-import', {});
const { owned, publications, holds } = await api();
const draftsByTitle = new Map(owned.map(item => [item.title, item]));
for (const { draft } of legacy) {
  const own = draftsByTitle.get(draft.title);
  assert.ok(own, draft.title);
  if (!publications.some(item => item.quizId === own.id && item.isActive && item.holdId === null)) {
    await api('/' + own.id + '/publish', { feedbackMode: 'after-submit' });
  }
}
const ownQuiz = draftsByTitle.get(quiz.title);
if (!publications.some(item => item.quizId === ownQuiz.id && item.isActive && item.holdId === holds[0].id)) {
  await api('/' + ownQuiz.id + '/publish', { holdId: holds[0].id, feedbackMode: 'manual', maxAttempts: 1 });
}
const index = await api();
const controlFlow = index.publications.find(item => item.quizId === ownQuiz.id && item.holdId === null);
assert.ok(controlFlow);
if (process.argv.includes('--seed-only')) {
  console.log('Fiktivt QA-katalog forberedt med 26 frie øvelser og én holdtildeling.');
  process.exit(0);
}

const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch();
try {
  const context = await browser.newContext({
    viewport: { width: 375, height: 800 }, serviceWorkers: 'block', reducedMotion: 'reduce',
  });
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  let failFirstStart = true;
  let failFirstCheck = true;
  const requestIds = [];
  await context.route('**/*', async route => {
    const request = route.request();
    const url = new URL(request.url());
    if (url.origin !== origin) return route.abort('blockedbyclient');
    if (failFirstCheck && request.method() === 'POST' && url.pathname.endsWith('/check')) {
      failFirstCheck = false;
      return route.fulfill({ status: 503, json: {} });
    }
    if (request.method() !== 'POST' || url.pathname !== '/api/knowledge-checks/publications/' + controlFlow.id + '/attempts') {
      return route.continue();
    }
    requestIds.push(request.postDataJSON().requestId);
    if (!failFirstStart) return route.continue();
    failFirstStart = false;
    await route.fulfill({ status: 503, json: {} });
  });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  page.setDefaultTimeout(20000);
  await page.goto(origin + '/_qa/login?user=pupil&screen=videnstjek&theme=dark');
  await page.waitForURL(url => url.pathname === '/videnstjek');
  await page.goto(origin + '/videnstjek');
  await page.getByRole('heading', { name: 'Frie øvelser', exact: true }).waitFor();
  await page.getByText('26 quizzer', { exact: true }).waitFor();
  await page.getByRole('link', { name: /^Mine tildelinger.*1/ }).waitFor();
  assert.equal(await page.locator('.knowledge-card').count(), 26);
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth));
  assert.equal(await page.locator('html').getAttribute('data-theme'), 'dark');

  const topics = page.getByRole('navigation', { name: 'Emne', exact: true });
  for (const [topic, count] of [['Frontend', '3 quizzer'], ['Flutter', '1 quiz'], ['Backend', '17 quizzer'], ['GF2', '8 quizzer']]) {
    await topics.getByRole('link', { name: topic, exact: true }).click();
    await page.getByText(count, { exact: true }).waitFor();
    assert.equal(new URL(page.url()).searchParams.get('emne'), topic);
  }
  const search = page.getByLabel('Søg i øvelser');
  const level = page.getByRole('combobox', { name: 'Niveau' });
  await search.pressSequentially('Control Flow', { delay: 25 });
  assert.equal(await search.evaluate(element => element === document.activeElement), true);
  await level.selectOption(controlFlow.legacyDiscovery.difficulty);
  await page.getByText('1 quiz', { exact: true }).waitFor();
  const card = page.getByRole('article').filter({ has: page.getByRole('heading', { name: quiz.title, exact: true }) });
  await card.getByRole('button', { name: 'Start quiz ' + quiz.title, exact: true }).click();
  await card.getByRole('alert').waitFor();
  await card.getByRole('button', { name: 'Start quiz ' + quiz.title, exact: true }).click();
  await page.waitForURL(url => url.pathname === '/videnstjek/' + controlFlow.id && url.searchParams.has('forsog'));
  const firstAttempt = new URL(page.url()).searchParams.get('forsog');
  assert.equal(requestIds.length, 2);
  assert.match(requestIds[0], /^[a-f0-9-]{36}$/i);
  assert.equal(requestIds[0], requestIds[1]);
  const questionCount = quiz.questions.length;
  await page.getByText('Spørgsmål 1 af ' + questionCount, { exact: true }).waitFor();
  await page.getByRole('button', { name: 'Kopiér kode', exact: true }).click();
  assert.match(await page.evaluate(() => navigator.clipboard.readText()), /Console\.WriteLine/);
  await page.locator('.lup-knowledge-prompt').focus();
  await page.keyboard.press('a');
  assert.equal(await page.getByRole('radio').first().isChecked(), true);
  const firstSource = page.getByRole('link', { name: quiz.questions[0].learnMoreTitle, exact: true });
  assert.equal(await firstSource.count(), 0);
  const check = page.getByRole('button', { name: 'Tjek svar', exact: true });
  await check.click();
  await page.locator('.lup-knowledge-player').getByRole('alert').waitFor();
  await check.click();
  await page.getByRole('heading', { name: 'Forkert', exact: true }).waitFor();
  await firstSource.waitFor();
  assert.equal(await page.getByRole('radio').first().isDisabled(), true);
  await page.reload();
  await page.getByRole('heading', { name: 'Forkert', exact: true }).waitFor();
  assert.equal(await page.getByRole('radio').first().isDisabled(), true);
  await page.locator('.lup-knowledge-prompt').focus();
  await page.keyboard.press('Enter');
  await page.getByText('Spørgsmål 2 af ' + questionCount, { exact: true }).waitFor();
  await page.getByRole('progressbar', { name: '1 af ' + questionCount + ' tjekket' }).waitFor();
  await page.getByRole('status').filter({ hasText: /^Gemt$/ }).waitFor();

  await page.getByRole('link', { name: 'Alle videnstjek', exact: true }).click();
  await card.getByRole('link', { name: /^Fortsæt/ }).waitFor();
  assert.equal(await search.inputValue(), 'Control Flow');
  assert.equal(await level.inputValue(), controlFlow.legacyDiscovery.difficulty);
  await card.getByRole('link', { name: /^Fortsæt/ }).click();
  await page.getByText('Spørgsmål 2 af ' + questionCount, { exact: true }).waitFor();
  const navigator = page.getByRole('navigation', { name: 'Spørgsmålsoversigt' });
  await navigator.getByRole('button', { name: 'Spørgsmål 1, forkert', exact: true }).click();
  assert.equal(await page.getByRole('radio').first().isChecked(), true);
  await navigator.getByRole('button', { name: 'Spørgsmål 2, ubesvaret', exact: true }).click();
  for (let i = 1; i < questionCount; i++) {
    await page.getByText('Spørgsmål ' + (i + 1) + ' af ' + questionCount, { exact: true }).waitFor();
    await page.getByRole('radio').nth(quiz.questions[i].correctOptions[0]).check();
    await check.click();
    await page.getByRole('heading', { name: 'Korrekt', exact: true }).waitFor();
    await page.getByRole('button', { name: /^(Næste|Gå til aflevering)$/ }).click();
  }
  await page.getByRole('button', { name: 'Aflever', exact: true }).click();
  await page.getByRole('heading', { name: (questionCount - 1) + ' af ' + questionCount + ' point', exact: true }).waitFor();
  await page.getByRole('button', { name: /^Kun forkerte/ }).click();
  assert.equal(await page.locator('.lup-knowledge-review > li').count(), 1);
  await page.getByRole('link', { name: quiz.questions[0].learnMoreTitle, exact: true }).waitFor();

  await page.getByRole('link', { name: 'Alle videnstjek', exact: true }).click();
  await card.getByRole('button', { name: /^Prøv igen/ }).waitFor();
  await card.getByRole('link', { name: /^Se seneste forsøg/ }).click();
  await page.getByRole('button', { name: 'Nyt forsøg', exact: true }).click();
  await page.waitForURL(url => url.pathname === '/videnstjek/' + controlFlow.id
    && url.searchParams.has('forsog') && url.searchParams.get('forsog') !== firstAttempt);
  assert.equal(new URL(page.url()).searchParams.get('q'), 'Control Flow');
  await page.getByText('Spørgsmål 1 af ' + questionCount, { exact: true }).waitFor();
  await page.getByRole('link', { name: 'Alle videnstjek', exact: true }).click();
  await card.getByRole('link', { name: /^Fortsæt/ }).waitFor();
  await search.fill('ingen quiz med det navn');
  await page.getByText('Ingen øvelser matcher din søgning.').waitFor();
  await page.getByRole('button', { name: 'Ryd filtre', exact: true }).click();
  await page.getByText('26 quizzer', { exact: true }).waitFor();
  assert.equal(await search.evaluate(element => element === document.activeElement), true);

  const shortDraft = await api('', {
    title: 'Kortsvar med feedback', topic: 'QA', description: 'Et eksempel på feedback til et kortsvar.',
    questions: [{
      id: randomUUID(), kind: 'short', prompt: 'Hvilket nøgleord forlader en løkke?', options: [],
      correctOptions: [], acceptedAnswers: ['break'], explanation: 'break afslutter den aktuelle løkke.',
    }],
  });
  const shortQuiz = await api('/' + shortDraft.id + '/publish', { feedbackMode: 'after-submit' });
  await page.goto(origin + '/videnstjek/' + shortQuiz.id);
  await page.getByRole('button', { name: 'Start videnstjek', exact: true }).click();
  const shortAnswer = page.getByRole('textbox', { name: 'Dit svar' });
  await shortAnswer.fill('break');
  await shortAnswer.press('Enter');
  assert.equal(await page.getByRole('heading', { name: 'Korrekt', exact: true }).count(), 0);
  await check.click();
  await page.getByRole('heading', { name: 'Korrekt', exact: true }).waitFor();
  assert.equal(await shortAnswer.isDisabled(), true);

  assert.deepEqual(errors, []);
  console.log('Mobil: katalogets 26 quizzer, filtre, start, genforsøg og feedback med kilder efter hvert tjek bestod.');
} finally {
  await browser.close();
}
