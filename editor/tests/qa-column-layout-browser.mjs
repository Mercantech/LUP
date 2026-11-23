// Run against a fresh loopback fixture started with node editor/tests/qa-publishing-harness.mjs.
import assert from 'node:assert/strict';
import { chromium } from '../../release-news/node_modules/playwright/index.mjs';

const origin = 'http://127.0.0.1:9098';
const words = ['Vejledning', 'Eksempel', 'Opgave'];
const rowSelector = '[data-content-type="columnLayout"]';
const columnSelector = '.bn-block:has(> [data-content-type="layoutColumn"], > .bn-react-node-view-renderer > [data-content-type="layoutColumn"])';
const countControl = page => page.getByRole('combobox', { name: 'Antal kolonner' });
const widthControl = page => page.getByRole('combobox', { name: 'Kolonnebredde' });
const paragraphs = page => page.locator('.ProseMirror [data-content-type="paragraph"]');

async function fillColumn(page, index) {
  const field = page.locator(columnSelector).nth(index).locator('.bn-inline-content').first();
  await field.click();
  await page.keyboard.type(words[index]);
}

async function activeColumn(page) {
  return page.evaluate(selector => {
    const anchor = window.getSelection()?.anchorNode;
    const element = anchor?.nodeType === Node.TEXT_NODE ? anchor.parentElement : anchor;
    const column = element?.closest(selector);
    return Array.from(document.querySelectorAll(selector)).indexOf(column);
  }, columnSelector);
}

async function checkGeometry(page, count, stacked) {
  const columns = page.locator(columnSelector);
  await columns.nth(count - 1).waitFor();
  const bounds = await columns.evaluateAll(nodes => nodes.map(column => column.getBoundingClientRect().toJSON()));
  assert.equal(bounds.length, count);
  for (const [index, rect] of bounds.slice(1).entries()) {
    const previous = bounds[index];
    const fits = stacked
      ? rect.top >= previous.bottom - 1 && Math.abs(rect.left - previous.left) < 2
      : rect.left >= previous.right - 1 && Math.abs(rect.top - previous.top) < 2;
    assert.ok(fits, 'Expected ' + (stacked ? 'stacked' : 'side-by-side') + ' columns: ' + JSON.stringify(bounds));
  }
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), 'Page should fit the viewport');
}

async function contentIds(page) {
  const ids = [];
  for (const word of words) {
    const text = paragraphs(page).getByText(word, { exact: true });
    await text.waitFor();
    ids.push(await text.evaluate(node => node.closest('[data-node-type="blockContainer"]').dataset.id));
  }
  return ids;
}

async function blockAction(page, blockText, action) {
  await paragraphs(page).getByText(blockText, { exact: true }).hover();
  await page.locator('[data-test="dragHandle"]').click();
  await page.getByRole('menuitem', { name: action }).click();
}

const browser = await chromium.launch();
try {
  const context = await browser.newContext({ viewport: { width: 1360, height: 900 } });
  const page = await context.newPage();
  const errors = [];
  context.on('weberror', event => errors.push(event.error().message));
  await page.goto(origin + '/notion');
  const editor = page.locator('.ProseMirror[contenteditable="true"]');
  await editor.waitFor();
  assert.equal((await editor.innerText()).trim(), '', 'Start the publishing fixture with a fresh document');
  await editor.click();
  await page.keyboard.type('/kolonner');
  await page.getByRole('option', { name: /2 kolonner/i }).click();
  await checkGeometry(page, 2, false);
  await fillColumn(page, 0);
  await page.keyboard.press('Tab');
  assert.equal(await activeColumn(page), 1);
  await page.keyboard.press('Backspace');
  assert.equal(await activeColumn(page), 1);
  await page.keyboard.type(words[1]);
  await page.keyboard.press('Shift+Tab');
  assert.equal(await activeColumn(page), 0);
  await widthControl(page).selectOption('wideLeft');
  const columns = page.locator(columnSelector);
  await page.waitForFunction(selector => {
    const [left, right] = document.querySelectorAll(selector);
    return left.getBoundingClientRect().width > right.getBoundingClientRect().width * 1.2;
  }, columnSelector);
  await countControl(page).selectOption('3');
  await checkGeometry(page, 3, false);
  await fillColumn(page, 2);
  const ids = await contentIds(page);
  await page.waitForFunction(() => document.querySelector('#status').textContent === 'Gemt i skyen');
  await page.reload();
  await editor.waitFor();
  assert.deepEqual(await contentIds(page), ids);
  await checkGeometry(page, 3, false);
  assert.equal(await widthControl(page).inputValue(), 'wideLeft');

  await page.getByRole('button', { name: 'Udgiv til eleverne' }).click();
  await page.locator('#publish-status').getByText('Udgivet til eleverne').waitFor();
  const reader = await context.newPage();
  await reader.goto(origin + '/student');
  assert.deepEqual(await contentIds(reader), ids);
  assert.equal(await countControl(reader).count(), 0);
  assert.equal(await reader.locator('.ProseMirror[contenteditable="true"]').count(), 0);
  await checkGeometry(reader, 3, false);
  await reader.setViewportSize({ width: 390, height: 844 });
  await checkGeometry(reader, 3, true);
  await page.setViewportSize({ width: 390, height: 844 });
  await checkGeometry(page, 3, true);

  await countControl(page).selectOption('2');
  await checkGeometry(page, 2, true);
  assert.match(await columns.nth(1).innerText(), /Eksempel\s+Opgave/);
  await countControl(page).selectOption('1');
  await page.locator(rowSelector).waitFor({ state: 'detached' });
  assert.deepEqual(await contentIds(page), ids);
  assert.deepEqual((await paragraphs(page).allTextContents()).filter(Boolean), words);
  await page.waitForFunction(() => document.querySelector('#status').textContent === 'Gemt i skyen');
  await page.reload();
  await editor.waitFor();
  assert.equal(await page.locator(rowSelector).count(), 0);
  assert.deepEqual(await contentIds(page), ids);
  await page.setViewportSize({ width: 1360, height: 900 });
  await blockAction(page, words[0], 'Sæt i to kolonner');
  await checkGeometry(page, 2, false);
  await blockAction(page, words[0], 'Flyt til højre kolonne');
  assert.equal((await columns.first().locator('.bn-inline-content').innerText()).trim(), '');
  assert.equal((await columns.nth(1).locator('.bn-inline-content').innerText()).trim(), words[0]);
  assert.deepEqual(await contentIds(page), ids);

  const peer = await context.newPage();
  await peer.goto(origin + '/notion');
  await peer.locator('.ProseMirror[contenteditable=true]').waitFor();
  await page.waitForFunction(() => document.querySelector('[aria-label="Antal kolonner"]').disabled);
  await paragraphs(page).getByText(words[0], { exact: true }).click();
  await page.keyboard.press('End');
  await page.keyboard.press('Enter');
  await page.keyboard.type('/kolonner');
  await page.getByRole('option', { name: /2 kolonner/i }).click();
  await peer.locator(rowSelector).nth(1).waitFor();
  for (const tab of [page, peer]) {
    assert.equal(await tab.locator(rowSelector).count(), 2);
    assert.deepEqual(await contentIds(tab), ids);
  }
  await peer.close();
  await page.waitForFunction(() => !document.querySelector('[aria-label="Antal kolonner"]').disabled);
  assert.deepEqual(errors, []);
  console.log('PASS: keyboard navigation, widths, reload, publication, responsive layout, moving blocks and adding a row during collaboration.');
} finally {
  await browser.close();
}
