// Run against the fictional loopback notebook fixture (tests/Notebook.BrowserQa/README.md).
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const web = new URL('http://localhost:9098');
const apiUrl = 'http://127.0.0.1:8088';
const knowledge = '/api/knowledge-checks';
const health = await (await fetch(apiUrl + '/_qa/health')).json();
assert.equal(health.fixture, 'fictional-notebook-browser-qa');
const people = Object.fromEntries((await (await fetch(apiUrl + '/_qa/identities')).json())
  .map(identity => [identity.key, identity]));
const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch();
const errors = [];
const title = 'QA Control Flow ' + randomUUID().slice(0, 8);

function api(person, path, body) {
  return fetch(apiUrl + path, {
    method: body ? 'POST' : 'GET',
    headers: {
      Authorization: 'Bearer ' + people[person].token, ...(body && { 'Content-Type': 'application/json' }),
    },
    body: body && JSON.stringify(body),
  });
}

async function json(person, path, body) {
  const response = await api(person, path, body);
  assert.ok(response.ok, `${person} ${path}: ${response.status} ${response.ok ? '' : await response.text()}`);
  return response.json();
}

async function session(person, viewport, path) {
  const context = await browser.newContext({ serviceWorkers: 'block', viewport });
  await context.route('**/*', route => {
    const url = new URL(route.request().url());
    const allowed = ['blob:', 'data:'].includes(url.protocol) || ['localhost', '127.0.0.1'].includes(url.hostname);
    return allowed ? route.continue() : route.abort('blockedbyclient');
  });
  const page = await context.newPage();
  page.setDefaultTimeout(25000);
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(new URL('/_qa/login?user=' + person, web).href);
  await page.waitForURL(url => !url.pathname.startsWith('/_qa/'));
  await page.goto(new URL(path, web).href);
  return page;
}

function responseTo(page, path, method) {
  return page.waitForResponse(response =>
    new URL(response.url()).pathname === path && response.request().method() === method);
}

const question = (prompt, options) => ({
  id: randomUUID(), kind: 'single', prompt, options, correctOptions: [0],
  acceptedAnswers: [], manualGrading: false, explanation: 'Forklaring til ' + prompt, learnMoreUrl: null,
});

try {
  const hold = (await json('teacher', '/api/notion/holds')).find(item => item.canManage);
  assert.ok(hold);
  const draft = await json('teacher', knowledge, {
    title, description: 'En afprøvning af det fælles forsøg.', topic: 'GF2', estimatedMinutes: 2,
    questions: [question('Hvad starter en betingelse?', ['if', 'using']), question('Hvad gentager en blok?', ['while', 'return'])],
  });
  const publication = await json('teacher', knowledge + '/' + draft.id + '/publish', {
    holdId: hold.id, maxAttempts: 1, feedbackMode: 'after-submit', revision: draft.revision, requestId: randomUUID(),
  });
  const notebook = await json('teacher', '/api/notion/pages', { holdId: hold.id, title: 'QA notesbog ' + title });
  const draftPath = '/notion/' + notebook.id + '?hold=' + hold.id;
  const publishedPath = draftPath + '&published=true';
  const teacher = await session('teacher', { width: 1440, height: 960 }, draftPath);
  const editor = teacher.locator('.notion-blocknote-host .bn-editor[contenteditable=true]').first();
  await editor.click();
  await teacher.keyboard.press('Control+End');
  await teacher.keyboard.type('/videnstjek', { delay: 30 });
  await teacher.locator('.bn-suggestion-menu').getByText('Videnstjek', { exact: true }).click();
  const choices = teacher.locator('.notion-knowledge-check').getByLabel('Udgivelse');
  await choices.selectOption(publication.id);
  await teacher.getByText('Gemt i skyen', { exact: true }).waitFor();
  const published = responseTo(teacher, '/api/notion/pages/' + notebook.id + '/publish', 'POST');
  await teacher.getByRole('button', { name: 'Udgiv til holdet', exact: true }).click();
  const publishResponse = await published;
  assert.ok(publishResponse.ok(), await publishResponse.text());

  const learner = await session('pupil', { width: 390, height: 844 }, publishedPath);
  const block = learner.locator('.notion-knowledge-check');
  const location = knowledge + '/publications/' + publication.id;
  const begun = responseTo(learner, location + '/attempts', 'POST');
  await block.getByRole('button', { name: 'Start videnstjek', exact: true }).click();
  const attemptResponse = await begun;
  assert.ok(attemptResponse.ok());
  assert.equal(new URL(attemptResponse.url()).searchParams.get('pageId'), notebook.id);
  assert.ok(new URL(attemptResponse.url()).searchParams.get('blockId'));
  const attempt = await attemptResponse.json();
  assert.ok(attempt.questions.every(item => !Object.hasOwn(item, 'correctOptions')));
  await block.getByRole('radio', { name: 'if', exact: true }).check();
  await block.getByRole('button', { name: 'Næste', exact: true }).click();
  await block.getByRole('status').filter({ hasText: /^Gemt$/ }).waitFor();
  await learner.reload();
  await block.getByText('Hvad gentager en blok?', { exact: true }).waitFor();
  await block.getByRole('link', { name: 'Åbn på egen side', exact: true }).click();
  await learner.waitForURL(url => url.pathname === '/videnstjek/' + publication.id);
  const player = learner.locator('.lup-knowledge-player');
  await player.getByText('Hvad gentager en blok?', { exact: true }).waitFor();
  await player.getByRole('button', { name: 'Forrige', exact: true }).click();
  assert.equal(await player.getByRole('radio', { name: 'if', exact: true }).isChecked(), true);
  await player.getByRole('button', { name: 'Næste', exact: true }).click();
  await player.getByRole('radio', { name: 'while', exact: true }).check();
  await player.getByRole('button', { name: 'Gå til aflevering', exact: true }).click();
  const submitted = responseTo(learner, location + '/attempts/' + attempt.id + '/submit', 'POST');
  await player.getByRole('button', { name: 'Aflever', exact: true }).click();
  assert.ok((await submitted).ok());
  await player.getByRole('heading', { name: '2 af 2 point', exact: true }).waitFor();
  assert.equal(await player.getByRole('button', { name: 'Nyt forsøg', exact: true }).count(), 0);
  assert.equal((await api('outsider', location)).status, 404);
  await learner.goto(new URL(publishedPath, web).href);
  await block.getByRole('heading', { name: '2 af 2 point', exact: true }).waitFor();
  assert.deepEqual(errors, []);
  console.log('Videnstjek bestod browser-QA fra udgivelse i notesbogen til samme forsøg i selvstændig visning.');
} finally {
  await browser.close();
}
