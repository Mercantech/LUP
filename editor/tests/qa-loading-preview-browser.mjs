// Run against qa-notebook-harness.mjs with the published web build.
import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const origin = 'http://localhost:9098';
const toolsUrl = origin + '/teacher/tools';
const previewUrl = toolsUrl + '/indlaesning';
const output = process.env.LUP_LOADING_PREVIEW_QA_OUTPUT;
const raymondQuote = '»Jeg laver lige en JOIN. En elefant glemmer aldrig den primære nøgle.«';
const characters = ['LUPP', 'Raymond', 'Gummianden', 'Bilbo Backend', 'Kamæleonen', 'Næsebladet', 'Cyberbien', 'Docker'];

const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
if (output) await mkdir(output, { recursive: true });
const browser = await chromium.launch();

try {
  const teacher = await open('teacher', { viewport: { width: 1366, height: 900 } });
  await teacher.clock.install();
  await teacher.goto(toolsUrl);
  await teacher.getByRole('region', { name: 'Opsætning' }).getByRole('link', { name: /Indlæsningsskærmen/ }).click();
  await teacher.waitForURL(previewUrl);
  await teacher.getByRole('heading', { name: 'Indlæsningsskærmen', exact: true }).waitFor();

  const preview = teacher.locator('[data-loading-preview]');
  const gallery = teacher.locator('[data-loading-characters]');
  await gallery.getByRole('button').first().waitFor();
  assert.equal(await gallery.getByRole('button').count(), characters.length);
  for (const character of characters) {
    const card = gallery.getByRole('button', { name: new RegExp(character) });
    assert.match(await card.innerText(), /».+«/s);
  }

  const raymond = gallery.getByRole('button', { name: /Raymond/ });
  await raymond.click();
  await preview.getByText(raymondQuote, { exact: true }).waitFor();
  const portrait = preview.locator('[data-startup-portrait]');
  assert.match(await portrait.getAttribute('src'), /images\/characters\/raymond\/reactions\/join\.webp$/);
  await teacher.waitForFunction(() => {
    const image = document.querySelector('[data-loading-preview] [data-startup-portrait]');
    return image?.complete && image.naturalWidth > 0;
  });
  assert.equal(await raymond.getAttribute('aria-pressed'), 'true');
  assert.equal(await gallery.locator('[aria-pressed="true"]').count(), 1);
  if (output) await teacher.screenshot({ path: resolve(output, 'teacher-desktop.png') });

  const play = teacher.getByRole('button', { name: 'Afspil rotation', exact: true });
  const pause = teacher.getByRole('button', { name: 'Sæt rotation på pause', exact: true });
  await play.click();
  await pause.waitFor();
  await teacher.clock.fastForward(7_500);
  await preview.locator('[data-startup-character]').getByText('Gummianden', { exact: true }).waitFor();
  await raymond.click();
  await play.waitFor();
  await teacher.clock.fastForward(7_500);
  assert.equal(await preview.locator('[data-startup-character]').innerText(), 'Raymond');

  const mobile = await open('teacher', {
    viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, reducedMotion: 'reduce'
  });
  await mobile.goto(previewUrl);
  await mobile.locator('[data-loading-characters]').getByRole('button', { name: /Raymond/ }).click();
  const mobilePreview = mobile.locator('[data-loading-preview]');
  await mobilePreview.getByText(raymondQuote, { exact: true }).waitFor();
  const mobilePlay = mobile.getByRole('button', { name: 'Afspil rotation', exact: true });
  assert.ok(await mobilePlay.isDisabled());
  assert.ok(await mobile.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  if (output) {
    await mobilePreview.scrollIntoViewIfNeeded();
    await mobile.screenshot({ path: resolve(output, 'teacher-mobile.png') });
  }

  const pupil = await open('pupil');
  await pupil.goto(previewUrl);
  await pupil.getByText(/side er (kun )?for undervisere/i).waitFor();
  assert.equal(await pupil.locator('[data-loading-characters]').count(), 0);

  const guest = await open();
  await guest.goto(previewUrl);
  await guest.getByRole('heading', { name: 'Log ind', exact: true }).waitFor();
  assert.equal(await guest.locator('[data-loading-characters]').count(), 0);

  console.log('Indlæsningsskærmen viste otte figurer, Raymonds replik og korrekt underviseradgang på desktop og mobil.');
} finally {
  await browser.close();
}

async function open(account, options) {
  const context = await browser.newContext({ serviceWorkers: 'block', ...options });
  await context.route('**/*', route => route.request().url().startsWith(origin + '/') ? route.continue() : route.abort());
  const page = await context.newPage();
  page.setDefaultTimeout(20_000);
  if (account) {
    await page.goto(origin + '/_qa/login?user=' + account);
    await page.waitForURL(url => !url.pathname.startsWith('/_qa/'));
  }
  return page;
}
