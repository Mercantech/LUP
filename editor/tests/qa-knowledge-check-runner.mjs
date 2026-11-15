// Run against the loopback notebook QA harness and a published web build.
import assert from 'node:assert/strict';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const base = 'http://localhost:9098';
const api = 'http://127.0.0.1:8088';
assert.equal((await (await fetch(api + '/_qa/health')).json()).fixture, 'fictional-notebook-browser-qa');
const pupil = (await (await fetch(api + '/_qa/identities')).json()).find(item => item.key === 'pupil');
assert.ok(pupil);

const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch();
const publicationId = '11111111-1111-4111-8111-111111111111';
const attemptId = '22222222-2222-4222-8222-222222222222';
const path = '/api/knowledge-checks/publications/' + publicationId;
const attemptPath = path + '/attempts/' + attemptId;
const questions = [
  { id: 'a', kind: 'single', prompt: 'Hvad skriver løkken?\n```csharp\nConsole.WriteLine(2);\n```', options: ['1', '2'] },
  { id: 'b', kind: 'multiple', prompt: 'Hvilke værdier er lige?', options: ['2', '3', '4'] },
  { id: 'c', kind: 'boolean', prompt: 'Er 8 et lige tal?', options: ['Sandt', 'Falsk'] },
  { id: 'd', kind: 'short', prompt: 'Hvilket nøgleord afslutter en løkke?', options: [] },
];
const publication = {
  id: publicationId, title: 'Control Flow', description: 'Øv de fire spørgsmålstyper.', topic: 'GF2', estimatedMinutes: 4,
  questionCount: questions.length, holdName: 'Testhold', maxAttempts: 1, isActive: true, attemptsUsed: 0,
  activeAttemptId: null, latestAttemptId: null,
};
const attempt = {
  id: attemptId, publicationId, status: 'started', revision: 1, questionIndex: 0, questions, answers: [], score: null, results: null,
};
const pageErrors = [];
let failNextLoad = true;
let submitted = 0;

try {
  const context = await browser.newContext({ viewport: { width: 390, height: 760 }, serviceWorkers: 'block' });
  await context.route('**/*', async route => {
    const request = route.request();
    const url = new URL(request.url());
    if (url.origin !== base) return route.abort('blockedbyclient');
    if (!url.pathname.startsWith(path)) return route.continue();
    assert.equal(request.headers().authorization, 'Bearer ' + pupil.token);

    if (url.pathname === path && request.method() === 'GET') {
      if (failNextLoad) {
        failNextLoad = false;
        return route.fulfill({ status: 503, json: {} });
      }
      return route.fulfill({ json: publication });
    }
    if (url.pathname === path + '/attempts') {
      assert.equal(request.method(), 'POST');
      assert.match(request.postDataJSON().requestId, /^[a-f0-9-]{36}$/i);
      publication.attemptsUsed = 1;
      publication.activeAttemptId = attemptId;
      return route.fulfill({ json: attempt });
    }
    if (url.pathname === attemptPath && request.method() === 'GET') return route.fulfill({ json: attempt });
    if (url.pathname === attemptPath && request.method() === 'PUT') {
      assert.equal(attempt.status, 'started');
      const body = request.postDataJSON();
      assert.equal(body.revision, attempt.revision);
      attempt.revision += 1;
      attempt.questionIndex = body.questionIndex;
      attempt.answers = body.answers;
      return route.fulfill({ json: attempt });
    }
    if (url.pathname === attemptPath + '/submit') {
      assert.equal(request.method(), 'POST');
      assert.equal(request.postDataJSON().revision, attempt.revision);
      assert.deepEqual(Object.fromEntries(attempt.answers.map(answer =>
        [answer.questionId, answer.text ?? answer.selectedOptions])), { a: [1], b: [0, 2], c: [0], d: 'break' });
      submitted += 1;
      publication.activeAttemptId = null;
      publication.latestAttemptId = attemptId;
      attempt.status = 'submitted';
      return route.fulfill({ json: attempt });
    }
    return route.fulfill({ status: 404, json: {} });
  });

  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  page.on('pageerror', error => pageErrors.push(error.message));
  await page.goto(base + '/_qa/login?user=pupil');
  await page.waitForURL(url => url.pathname === pupil.home);
  const pageUrl = base + '/videnstjek/' + publicationId;
  await page.goto(pageUrl);

  await page.getByRole('alert').filter({ hasText: 'Videnstjekket kunne ikke hentes.' }).waitFor();
  await page.getByRole('button', { name: 'Prøv igen', exact: true }).click();
  await page.getByRole('button', { name: 'Start videnstjek', exact: true }).click();
  await page.locator('.lup-knowledge-code').filter({ hasText: 'Console.WriteLine(2);' }).waitFor();
  await page.getByRole('radio', { name: '2', exact: true }).check();
  await page.getByRole('button', { name: 'Næste', exact: true }).click();
  await page.getByRole('checkbox', { name: '2', exact: true }).check();
  await page.getByRole('checkbox', { name: '4', exact: true }).check();
  await page.getByRole('status').filter({ hasText: /^Gemt$/ }).waitFor();
  await page.reload();
  await page.getByText('Spørgsmål 2 af 4', { exact: true }).waitFor();
  assert.equal(await page.getByRole('checkbox', { name: '2', exact: true }).isChecked(), true);
  assert.equal(await page.getByRole('checkbox', { name: '4', exact: true }).isChecked(), true);
  await page.getByRole('button', { name: 'Næste', exact: true }).click();
  await page.getByRole('radio', { name: 'Sandt', exact: true }).check();
  await page.getByRole('button', { name: 'Næste', exact: true }).click();
  await page.getByRole('textbox', { name: 'Dit svar', exact: true }).fill('break');
  await page.getByRole('button', { name: 'Gå til aflevering', exact: true }).click();
  await page.getByRole('button', { name: 'Aflever', exact: true }).click();
  await page.getByText('Venter på underviserens vurdering.', { exact: true }).waitFor();
  assert.equal(await page.getByRole('heading', { name: /point/ }).count(), 0);

  attempt.status = 'graded';
  await page.getByRole('button', { name: 'Opdatér resultat', exact: true }).click();
  await page.getByText('Facit og forklaringer vises, når underviseren frigiver dem.', { exact: true }).waitFor();
  assert.equal(await page.getByRole('heading', { name: /point/ }).count(), 0);

  attempt.score = 4;
  attempt.results = questions.map((question, index) => ({
    questionId: question.id, correct: true, correctOptions: [[1], [0, 2], [0], []][index],
    acceptedAnswers: question.kind === 'short' ? ['break'] : [], explanation: 'En faglig forklaring.',
    learnMoreUrl: index === 0 ? '/notion/33333333-3333-4333-8333-333333333333#loops' : null,
    comment: question.kind === 'short' ? 'Godt forklaret.' : null,
  }));
  await page.getByRole('button', { name: 'Opdatér resultat', exact: true }).click();
  await page.getByRole('heading', { name: '4 af 4 point', exact: true }).waitFor();
  assert.match(await page.getByRole('link', { name: 'Læs mere', exact: true }).getAttribute('href'), /#loops$/);
  assert.match(await page.locator('.lup-knowledge-feedback').innerText(), /Godt forklaret/);
  assert.equal(await page.getByRole('button', { name: 'Nyt forsøg', exact: true }).count(), 0);

  await page.goto(pageUrl + '?forsog=' + attemptId);
  await page.getByRole('heading', { name: '4 af 4 point', exact: true }).waitFor();
  assert.equal(submitted, 1);
  assert.deepEqual(pageErrors, []);
  console.log('Videnstjek på mobil bestod de fire spørgsmålstyper, genindlæsning, aflevering og frigivet feedback.');
} finally {
  await browser.close();
}
