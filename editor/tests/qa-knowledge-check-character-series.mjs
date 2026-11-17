// Run against the loopback harness with NOTEBOOK_BROWSER_QA_CHARACTER_SERIES=1.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const origin = 'http://localhost:9098';
const fixture = 'http://127.0.0.1:8088';
const health = await (await fetch(fixture + '/_qa/health')).json();
assert.equal(health.fixture, 'fictional-notebook-browser-qa');
const { drafts } = JSON.parse(await readFile(new URL('../../api/KnowledgeChecks/CharacterTemplates.json', import.meta.url)));
const hosts = Map.groupBy(drafts, item => item.characterSlug);
const raymond = drafts.find(item => item.key === 'raymond-manglende-raekke');
const raymondJoin = drafts.find(item => item.key === 'raymond-forbind-tabeller');
assert.ok(raymond && raymondJoin);
assert.equal(hosts.get('raymond').length, 2);

const publicationIds = new Map();
for (const [characterSlug, templates] of hosts) {
  const response = await fetch(fixture + '/api/knowledge-checks/characters/' + characterSlug);
  assert.ok(response.ok, characterSlug + ' returned ' + response.status);
  const summaries = await response.json();
  assert.equal(summaries.length, templates.length, characterSlug);
  for (const { key, draft, difficulty } of templates) {
    const matching = summaries.filter(quiz => quiz.title === draft.title);
    assert.equal(matching.length, 1, draft.title);
    const [quiz] = matching;
    assert.deepEqual(quiz, {
      id: quiz.id,
      title: draft.title,
      topic: draft.topic,
      difficulty,
      questionCount: draft.questions.length,
      estimatedMinutes: draft.estimatedMinutes
    });
    publicationIds.set(key, quiz.id);
  }
}
assert.equal(new Set(publicationIds.values()).size, drafts.length);
const rayId = publicationIds.get(raymond.key);
assert.equal((await fetch(fixture + '/api/knowledge-checks/publications/' + rayId)).status, 401);
assert.deepEqual(await (await fetch(fixture + '/api/knowledge-checks/characters/not-a-character')).json(), []);

const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch();
try {
  const guest = await openPage({ width: 375, height: 800 });
  await guest.goto(origin + '/characters/raymond');
  const quizzes = guest.getByRole('region', { name: 'Videnstjek med Raymond' });
  const loginLink = quizCard(guest, raymond.draft.title, quizzes).getByRole('link', { name: /^Log ind og åbn quiz/ });
  const login = new URL(await loginLink.getAttribute('href'), origin);
  assert.equal(login.pathname, '/login');
  assert.equal(login.searchParams.get('returnUrl'), '/videnstjek/' + rayId + '?karakter=raymond');
  const joinLoginLink = quizCard(guest, raymondJoin.draft.title, quizzes).getByRole('link', { name: /^Log ind og åbn quiz/ });
  const joinLogin = new URL(await joinLoginLink.getAttribute('href'), origin);
  assert.equal(joinLogin.searchParams.get('returnUrl'), '/videnstjek/' + publicationIds.get(raymondJoin.key) + '?karakter=raymond');
  assert.ok(await quizzes.evaluate(el => el.getBoundingClientRect().right <= window.innerWidth));

  const learner = await openPage({ width: 1280, height: 800 });
  await learner.goto(origin + '/_qa/login?user=pupil');
  await learner.waitForURL(url => !url.pathname.startsWith('/_qa/'));
  await learner.goto(origin + login.searchParams.get('returnUrl'));
  const host = learner.getByRole('complementary', { name: 'Quizvært' });
  const portrait = host.locator('img');
  await host.getByRole('link', { name: 'Vært: Raymond', exact: true }).waitFor();
  assert.equal(await host.getByRole('link').getAttribute('href'), '/characters/raymond');
  await host.getByText(raymond.intro, { exact: true }).waitFor();
  assert.match(await portrait.getAttribute('src'), /^\/images\/characters\/raymond\//);

  await learner.goto(origin + '/characters/raymond');
  const hostedQuizzes = learner.getByRole('region', { name: 'Videnstjek med Raymond' });
  const openQuiz = quizCard(learner, raymond.draft.title, hostedQuizzes).getByRole('link', { name: /^Åbn quiz/ });
  await openQuiz.waitFor();
  assert.equal(await openQuiz.getAttribute('href'), login.searchParams.get('returnUrl'));
  await learner.goto(origin + '/videnstjek');
  await learner.getByText(`${drafts.length + 26} quizzer`, { exact: true }).waitFor();
  const hostFilter = learner.getByRole('combobox', { name: 'Vært' });
  assert.equal(await hostFilter.locator('option').count(), hosts.size + 1);
  await hostFilter.selectOption('raymond');
  await learner.waitForURL(url => url.searchParams.get('karakter') === 'raymond');
  await learner.getByText('2 quizzer', { exact: true }).waitFor();
  await learner.getByRole('heading', { name: raymond.draft.title, exact: true }).waitFor();
  await learner.getByRole('heading', { name: raymondJoin.draft.title, exact: true }).waitFor();
  const topics = learner.getByRole('navigation', { name: 'Emne', exact: true });
  await topics.getByRole('link', { name: raymond.draft.topic, exact: true }).click();
  await learner.waitForURL(url => url.searchParams.get('emne') === raymond.draft.topic);
  await learner.getByRole('combobox', { name: 'Niveau' }).selectOption(raymond.difficulty);
  await learner.waitForURL(url => url.searchParams.get('niveau') === raymond.difficulty);
  await learner.getByLabel('Søg i øvelser').fill('manglende række');
  await learner.getByText('1 quiz', { exact: true }).waitFor();
  const card = quizCard(learner, raymond.draft.title);
  await card.getByRole('link', { name: /Med Raymond/ }).waitFor();
  await card.getByRole('button', { name: /^Start quiz/ }).click();
  await learner.waitForURL(url => url.pathname === '/videnstjek/' + rayId && url.searchParams.has('forsog'));
  const url = new URL(learner.url());
  assert.equal(url.searchParams.get('karakter'), 'raymond');
  assert.equal(url.searchParams.get('emne'), raymond.draft.topic);
  assert.equal(url.searchParams.get('niveau'), raymond.difficulty);
  assert.equal(url.searchParams.get('q'), 'manglende række');

  const question = raymond.draft.questions[0];
  const choices = learner.getByRole(question.kind === 'multiple' ? 'checkbox' : 'radio');
  for (const correct of question.correctOptions) await choices.nth(correct).check();
  await learner.getByRole('button', { name: 'Tjek svar', exact: true }).click();
  await learner.getByRole('heading', { name: 'Korrekt', exact: true }).waitFor();
  if (question.learnMoreTitle) await learner.getByRole('link', { name: question.learnMoreTitle, exact: true }).waitFor();
  await host.getByRole('link', { name: 'Vært: Raymond', exact: true }).waitFor();
  await learner.getByRole('link', { name: 'Alle videnstjek', exact: true }).click();
  await card.getByRole('link', { name: /^Fortsæt/ }).waitFor();
  assert.equal(await hostFilter.inputValue(), 'raymond');
  assert.equal(await learner.getByLabel('Søg i øvelser').inputValue(), 'manglende række');
  console.log(`Alle ${drafts.length} offentlige quizopslag, gæstens loginretur, værtsfilter og feedback bestod i browserprøven.`);
} finally {
  await browser.close();
}

function quizCard(page, title, container = page) {
  return container.getByRole('article').filter({ has: page.getByRole('heading', { name: title, exact: true }) });
}

async function openPage(viewport) {
  const context = await browser.newContext({ viewport, serviceWorkers: 'block' });
  await context.route('**/*', route => {
    const target = new URL(route.request().url());
    return target.origin === origin || target.protocol === 'data:' ? route.continue() : route.abort();
  });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  return page;
}
