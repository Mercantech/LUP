// Run against qa-notebook-harness.mjs with the real API and published web build.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const origin = 'http://localhost:9098';
const templates = JSON.parse(await readFile(new URL('../../api/KnowledgeChecks/LegacyTemplates.json', import.meta.url)));
const { draft } = templates.drafts.find(entry => entry.draft.questions.some(question => question.learnMoreUrl));
const source = draft.questions.find(question => question.learnMoreTitle && question.learnMoreUrl);
assert.ok(source?.optionExplanations?.some(Boolean));

const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch();
try {
  const teacher = await open('teacher');
  await teacher.goto(origin + '/videnstjek?vis=administrer');
  await teacher.getByText('Importér fra det gamle Videnstjek', { exact: true }).click();
  const importer = teacher.getByRole('button', { name: 'Importér som kladder', exact: true });
  await importer.click();
  await teacher.getByRole('status').filter({ hasText: /er tilføjet som kladder|allerede importeret alle/ }).waitFor();
  for (const entry of templates.drafts) {
    await teacher.getByRole('heading', { name: entry.draft.title, exact: true }).waitFor();
  }
  await importer.click();
  await teacher.getByText('Du har allerede importeret alle videnstjek i udvalget.', { exact: true }).waitFor();
  assert.equal(await teacher.getByRole('heading', { name: draft.title, exact: true }).count(), 1);

  await teacher.getByRole('article').filter({ has: teacher.getByRole('heading', { name: draft.title, exact: true }) })
    .getByRole('link', { name: /^Rediger/ }).click();
  const exerciseTitle = draft.title + ' · QA ' + Date.now().toString(36);
  await teacher.getByLabel('Titel', { exact: true }).fill(exerciseTitle);
  await teacher.getByRole('button', { name: 'Udgiv', exact: true }).click();
  await teacher.getByText('Videnstjekket er udgivet.', { exact: true }).waitFor();
  const exercise = await teacher.getByRole('link', { name: 'Åbn', exact: true }).getAttribute('href');
  assert.ok(exercise);

  const learner = await open('pupil');
  await learner.goto(origin + exercise);
  await learner.getByRole('button', { name: 'Start videnstjek', exact: true }).click();
  for (let index = 0; index < draft.questions.length; index++) {
    const question = draft.questions[index];
    await learner.getByText(`Spørgsmål ${index + 1} af ${draft.questions.length}`, { exact: true }).waitFor();
    assert.equal(await learner.getByRole('link', { name: source.learnMoreTitle, exact: false }).count(), 0);
    const choices = learner.getByRole(question.kind === 'multiple' ? 'checkbox' : 'radio');
    for (const selected of question.correctOptions) await choices.nth(selected).check();
    await learner.getByRole('button', { name: /^(Næste|Gå til aflevering)$/ }).click();
  }
  await learner.getByRole('button', { name: 'Aflever', exact: true }).click();
  await learner.getByRole('heading', { name: `${draft.questions.length} af ${draft.questions.length} point`, exact: true }).waitFor();
  const review = learner.locator('.lup-knowledge-review').getByRole('listitem')
    .filter({ has: learner.getByRole('link', { name: source.learnMoreTitle, exact: false }) }).first();
  const href = await review.getByRole('link', { name: source.learnMoreTitle, exact: false }).first().getAttribute('href');
  assert.equal(href, new URL(source.learnMoreUrl).href);
  const explanation = source.optionExplanations.find(line => line && !line.includes('`'));
  assert.ok(explanation);
  await review.getByText(explanation, { exact: false }).first().waitFor();
  console.log(`Importen gav ${templates.drafts.length} kladder. Forklaringer og kildelink blev bevaret efter redigering, udgivelse og aflevering.`);
} finally {
  await browser.close();
}

async function open(account) {
  const context = await browser.newContext({ serviceWorkers: 'block' });
  await context.route('**/*', route => route.request().url().startsWith(origin + '/') ? route.continue() : route.abort());
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  await page.goto(origin + '/_qa/login?user=' + account);
  await page.waitForURL(url => !url.pathname.startsWith('/_qa/'));
  return page;
}
