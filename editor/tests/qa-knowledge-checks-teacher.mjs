// Run against qa-notebook-harness.mjs with the real API and published web build.
import assert from 'node:assert/strict';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const origin = 'http://localhost:9098';
const fixture = 'http://127.0.0.1:8088';
const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const identities = await (await fetch(fixture + '/_qa/identities')).json();
const teacher = identities.find(identity => identity.key === 'teacher');
const index = await fetch(fixture + '/api/knowledge-checks', { headers: { Authorization: 'Bearer ' + teacher.token } });
assert.equal(index.status, 200);
const [hold] = (await index.json()).holds;
assert.ok(hold);
const label = Date.now().toString(36);
const freeTitle = 'GF2 · Control Flow ' + label;
const holdTitle = 'QA · Forklar en betingelse ' + label;

const browser = await chromium.launch();
let desktop, mobile;
try {
  desktop = await open('teacher');
  mobile = await open('pupil', { viewport: { width: 390, height: 844 }, isMobile: true, deviceScaleFactor: 2, hasTouch: true });

  await desktop.goto(origin + '/videnstjek?vis=administrer');
  await desktop.getByRole('button', { name: 'Opret fra GF2-eksempel' }).click();
  await desktop.getByRole('heading', { name: 'Rediger videnstjek' }).waitFor();
  assert.equal(await desktop.getByLabel('Titel', { exact: true }).inputValue(), 'GF2 · Control Flow');
  assert.equal(await desktop.getByRole('heading', { name: /^Spørgsmål \d+$/ }).count(), 10);
  await desktop.getByRole('button', { name: 'Forhåndsvis' }).click();
  await desktop.getByRole('region', { name: 'Elevvisning' }).getByRole('button', { name: 'Næste' }).click();
  await desktop.getByText('Forhåndsvisning · Spørgsmål 2 af 10').waitFor();
  await desktop.getByLabel('Titel', { exact: true }).fill(freeTitle);
  await desktop.getByRole('button', { name: 'Udgiv', exact: true }).click();
  await desktop.getByText('Videnstjekket er udgivet.', { exact: true }).waitFor();

  await mobile.goto(origin + '/videnstjek?vis=oev');
  await mobile.getByLabel('Søg i øvelser').fill('ukendt-fagligt-emne');
  await mobile.getByText('Ingen øvelser matcher din søgning.').waitFor();
  await mobile.getByLabel('Søg i øvelser').fill(freeTitle);
  await mobile.getByRole('link', { name: 'GF2', exact: true }).click();
  await mobile.getByRole('heading', { name: freeTitle, exact: true }).waitFor();
  assert.ok(await mobile.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth));

  await desktop.goto(origin + '/videnstjek/rediger/ny?hold=' + hold.id);
  await desktop.getByLabel('Titel', { exact: true }).fill(holdTitle);
  assert.equal(await desktop.getByLabel('Målgruppe').inputValue(), hold.id);
  await desktop.getByLabel('Spørgsmålstype').selectOption('short');
  await desktop.getByLabel('Spørgsmål', { exact: true }).fill('Hvornår evaluerer && udtrykket til højre?');
  await desktop.getByLabel('Vurder svaret manuelt').check();
  await desktop.getByLabel('Facit vises').selectOption('manual');
  await desktop.getByRole('button', { name: 'Udgiv', exact: true }).click();
  await desktop.getByText('Videnstjekket er udgivet.', { exact: true }).waitFor();
  const holdResults = await desktop.getByRole('link', { name: 'Holdresultater' }).getAttribute('href');
  assert.ok(holdResults);
  const exercise = holdResults.replace('/resultater', '');

  await mobile.goto(origin + '/videnstjek?vis=tildelinger');
  await mobile.getByRole('heading', { name: holdTitle, exact: true }).waitFor();
  await mobile.getByRole('article').filter({ has: mobile.getByRole('heading', { name: holdTitle, exact: true }) })
    .getByRole('link', { name: /^Start/ }).click();
  await mobile.getByRole('button', { name: 'Start videnstjek' }).click();
  await mobile.getByLabel('Dit svar').fill('Når udtrykket til venstre er sandt.');
  await mobile.getByRole('button', { name: 'Gå til aflevering' }).click();
  await mobile.getByRole('button', { name: 'Aflever', exact: true }).click();
  await mobile.getByText('Venter på underviserens vurdering.').waitFor();

  await desktop.goto(origin + holdResults);
  await desktop.getByText('Når udtrykket til venstre er sandt.', { exact: true }).waitFor();
  await desktop.getByLabel('Kommentar til eleven').fill('Korrekt forklaring.');
  await desktop.getByRole('button', { name: 'Giv 1 point' }).click();
  await desktop.getByText('Vurderingen er gemt.', { exact: true }).waitFor();
  await mobile.reload();
  await mobile.getByText('Facit og forklaringer vises, når underviseren frigiver dem.').waitFor();

  await desktop.getByRole('button', { name: 'Frigiv facit', exact: true }).click();
  await desktop.getByRole('button', { name: 'Bekræft frigivelse' }).click();
  await desktop.getByText('Facit er frigivet.', { exact: true }).waitFor();
  await mobile.reload();
  await mobile.getByText('Underviserens kommentar. Korrekt forklaring.', { exact: true }).waitFor();
  await mobile.getByRole('heading', { name: '1 af 1 point', exact: true }).waitFor();
  assert.equal(new URL(mobile.url()).pathname, exercise);
  await mobile.goto(origin + '/videnstjek?vis=historik');
  await mobile.getByRole('article').filter({ has: mobile.getByRole('heading', { name: holdTitle, exact: true }) })
    .getByText('1 af 1 point', { exact: false }).waitFor();
  console.log('Underviser oprettede eksemplet, udgav fri øvelse og holdtjek, vurderede et kortsvar og frigav facit.');
} finally {
  await browser.close();
}

async function open(account, options = {}) {
  const context = await browser.newContext({ serviceWorkers: 'block', ...options });
  await context.route('**/*', route => route.request().url().startsWith(origin + '/') ? route.continue() : route.abort());
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  await page.goto(origin + '/_qa/login?user=' + account);
  await page.waitForURL(url => !url.pathname.startsWith('/_qa/'));
  return page;
}
