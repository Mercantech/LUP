// Run against a fresh loopback fixture started with node editor/tests/qa-publishing-harness.mjs.
import assert from 'node:assert/strict';
import { chromium } from '../../release-news/node_modules/playwright/index.mjs';

const origin = 'http://127.0.0.1:9098';
const words = ['Vejledning', 'Eksempel', 'Opgave'];
const rowSelector = '[data-content-type="columnLayout"]';
const columnSelector = '.bn-block:has(> [data-content-type="layoutColumn"], > .bn-react-node-view-renderer > [data-content-type="layoutColumn"])';
const addColumn = page => page.getByRole('button', { name: 'Tilføj tredje kolonne' });
const handles = page => page.getByRole('separator', { name: /Skillelinje mellem kolonne/ });
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

async function proportions(page) {
  const widths = await page.locator(columnSelector).evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect().width));
  const total = widths.reduce((sum, width) => sum + width, 0);
  return widths.map(width => width / total);
}

function sameProportions(actual, expected) {
  assert.equal(actual.length, expected.length);
  for (let i = 0; i < actual.length; i++) {
    assert.ok(Math.abs(actual[i] - expected[i]) < 0.012, 'Column ' + i + ': ' + actual[i] + ' vs ' + expected[i]);
  }
}

async function dragBoundary(page, index, delta, cancel = false) {
  const handle = handles(page).nth(index);
  await handle.scrollIntoViewIfNeeded();
  const box = await handle.boundingBox();
  assert.ok(box, 'Expected a visible column divider');
  const x = box.x + box.width / 2, y = box.y + box.height / 2;
  if (cancel) {
    await handle.evaluate(element => element.addEventListener('pointerdown', event => {
      element.dataset.qaPointer = String(event.pointerId);
    }, { once: true }));
  }
  await page.mouse.move(x, y);
  await page.mouse.down();
  try {
    await page.mouse.move(x + delta, y, { steps: 10 });
    if (cancel) {
      const pointerId = Number(await handle.getAttribute('data-qa-pointer'));
      assert.ok(pointerId > 0, 'The browser pressed the divider');
      await handle.dispatchEvent('pointercancel', { pointerId, pointerType: 'mouse', isPrimary: true });
    }
  } finally {
    await page.mouse.up();
  }
}

async function waitForWidths(page, expected) {
  await page.waitForFunction(({ selector, expected }) => {
    const widths = Array.from(document.querySelectorAll(selector), column => column.getBoundingClientRect().width);
    const total = widths.reduce((sum, width) => sum + width, 0);
    return widths.length === expected.length && widths.every((width, i) => Math.abs(width / total - expected[i]) < 0.012);
  }, { selector: columnSelector, expected });
}

async function resizeWithTouch(browser) {
  const tablet = await browser.newContext({ hasTouch: true, isMobile: true, viewport: { width: 1024, height: 768 } });
  try {
    const page = await tablet.newPage();
    await page.goto(origin + '/notion');
    const handle = handles(page).first();
    await handle.scrollIntoViewIfNeeded();
    const box = await handle.boundingBox();
    assert.ok(box && await page.evaluate(() => matchMedia('(pointer: coarse)').matches), 'Tablet should offer a touch divider');
    const x = box.x + box.width / 2, y = box.y + box.height / 2;
    const firstColumn = page.locator(columnSelector).first();
    const initial = (await firstColumn.boundingBox()).width;
    const cdp = await tablet.newCDPSession(page);
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y }] });
    for (const step of [10, 20, 35, 55]) {
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: x + step, y }] });
    }
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    assert.ok((await firstColumn.boundingBox()).width > initial + 30, 'Touch dragging should make the column wider');
  } finally {
    await tablet.close();
  }
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
  await page.getByRole('listbox').getByRole('option', { name: /2 kolonner/i }).click();
  await checkGeometry(page, 2, false);
  await fillColumn(page, 0);
  await page.keyboard.press('Tab');
  assert.equal(await activeColumn(page), 1);
  await page.keyboard.press('Backspace');
  assert.equal(await activeColumn(page), 1);
  await page.keyboard.type(words[1]);
  await page.keyboard.press('Shift+Tab');
  assert.equal(await activeColumn(page), 0);
  const columns = page.locator(columnSelector);
  const initial = await proportions(page);
  await dragBoundary(page, 0, -82);
  const two = await proportions(page);
  assert.ok(two[0] < initial[0] - 0.04 && two[1] > initial[1] + 0.04, 'Dragging changes two column widths');
  await handles(page).first().press('Control+z');
  await waitForWidths(page, initial);
  await handles(page).first().press('Control+Shift+z');
  await waitForWidths(page, two);
  await addColumn(page).click();
  await checkGeometry(page, 3, false);
  await fillColumn(page, 2);
  const ids = await contentIds(page);
  const original = await proportions(page);
  await dragBoundary(page, 0, -46);
  const left = await proportions(page);
  assert.ok(left[0] < original[0] - 0.024 && left[1] > original[1] + 0.024);
  assert.ok(Math.abs(left[2] - original[2]) < 0.012);
  await dragBoundary(page, 1, 48);
  const right = await proportions(page);
  assert.ok(right[1] > left[1] + 0.024 && right[2] < left[2] - 0.024);
  assert.ok(Math.abs(right[0] - left[0]) < 0.012);
  await dragBoundary(page, 1, -60, true);
  sameProportions(await proportions(page), right);
  const rightHandle = handles(page).nth(1);
  const previousAriaValue = Number(await rightHandle.getAttribute('aria-valuenow'));
  assert.equal(await rightHandle.getAttribute('aria-orientation'), 'vertical');
  await rightHandle.press('ArrowLeft');
  assert.ok(Number(await rightHandle.getAttribute('aria-valuenow')) < previousAriaValue);
  const savedWidths = await proportions(page);
  assert.ok(savedWidths[2] > right[2] && savedWidths[1] < right[1], 'ArrowLeft moves the second divider');
  await page.waitForFunction(() => document.querySelector('#status').textContent === 'Gemt i skyen');
  await page.reload();
  await editor.waitFor();
  assert.deepEqual(await contentIds(page), ids);
  await checkGeometry(page, 3, false);
  sameProportions(await proportions(page), savedWidths);

  await page.getByRole('button', { name: 'Udgiv til eleverne' }).click();
  await page.locator('#publish-status').getByText('Udgivet til eleverne').waitFor();
  const reader = await context.newPage();
  await reader.goto(origin + '/student');
  assert.deepEqual(await contentIds(reader), ids);
  assert.equal(await addColumn(reader).count(), 0);
  assert.equal(await handles(reader).count(), 0);
  assert.equal(await reader.locator('.ProseMirror[contenteditable="true"]').count(), 0);
  await checkGeometry(reader, 3, false);
  sameProportions(await proportions(reader), savedWidths);
  await reader.setViewportSize({ width: 390, height: 844 });
  await checkGeometry(reader, 3, true);
  await page.setViewportSize({ width: 390, height: 844 });
  await checkGeometry(page, 3, true);
  assert.equal(await handles(page).count(), 0);

  await page.getByRole('button', { name: 'Saml til to kolonner' }).click();
  await checkGeometry(page, 2, true);
  assert.match(await columns.nth(1).innerText(), /Eksempel\s+Opgave/);
  await page.getByRole('button', { name: 'Saml til én kolonne' }).click();
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
  await page.getByRole('button', { name: 'Tilføj tredje kolonne', disabled: true }).waitFor();
  await dragBoundary(page, 0, 55);
  await waitForWidths(peer, await proportions(page));
  const divider = await handles(page).first().boundingBox();
  const x = divider.x + divider.width / 2, y = divider.y + divider.height / 2;
  await page.mouse.move(x, y);
  await page.mouse.down();
  try {
    await page.mouse.move(x - 65, y, { steps: 7 });
    await handles(peer).first().press('Shift+ArrowRight');
    await waitForWidths(page, await proportions(peer));
  } finally {
    await page.mouse.up();
  }
  sameProportions(await proportions(page), await proportions(peer));
  await columns.first().locator('.bn-inline-content').click();
  await page.keyboard.type('/kolonner');
  await page.getByRole('listbox').getByRole('option', { name: /2 kolonner/i }).click();
  await peer.locator(rowSelector).nth(1).waitFor();
  for (const tab of [page, peer]) {
    assert.equal(await tab.locator(rowSelector).count(), 2);
    assert.deepEqual(await contentIds(tab), ids);
  }
  await peer.close();
  await page.getByRole('button', { name: 'Tilføj tredje kolonne', disabled: false }).first().waitFor();
  await page.waitForFunction(() => document.querySelector('#status').textContent === 'Gemt i skyen');
  await page.close();
  await resizeWithTouch(browser);
  assert.deepEqual(errors, []);
  console.log('PASS: pointer and touch dragging, keyboard resizing, cancellation, persistence, publication, mobile, moving blocks and collaboration.');
} finally {
  await browser.close();
}
