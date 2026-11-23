import assert from 'node:assert/strict';
import test from 'node:test';
import { columnWidths, moveColumnDivider, savedColumnWidths, widthsAfterCountChange } from '../src/column-widths.mjs';

test('uses existing widths or restores the legacy ratio on missing, malformed or mismatched values', () => {
  assert.deepEqual(columnWidths('4200,5800', 2, 'wideLeft'), [4200,5800]);
  for (const raw of [null, '', '05000,5000', '20%,80%', '999,9001', '3000,6999', '3000,3000,4000'])
    assert.deepEqual(columnWidths(raw, 2, 'wideLeft'), [6667,3333]);
  assert.deepEqual(columnWidths('', 3, 'wideRight'), [2500,2500,5000]);
  assert.equal(columnWidths('', 3, 'equal').reduce((sum,w) => sum + w), 10000);
});

test('moving either divider preserves the outer track, minimum sizes and total', () => {
  assert.deepEqual(moveColumnDivider([3300,3400,3300], 1, 4528.4), [3300,4528,2172]);
  assert.deepEqual(moveColumnDivider([3300,3400,3300], 1, 9000), [3300,5700,1000]);
  assert.deepEqual(moveColumnDivider([3300,3400,3300], 0, -300), [1000,5700,3300]);
});

test('keeps custom widths on count changes and keeps legacy presets when unmodified', () => {
  assert.equal(widthsAfterCountChange('4300,5700', 2, 3), '4300,2850,2850');
  assert.deepEqual(savedColumnWidths(widthsAfterCountChange('9000,1000', 2, 3), 3), [8000,1000,1000]);
  assert.equal(widthsAfterCountChange('4151,2670,3179', 3, 2), '4151,5849');
  assert.equal(widthsAfterCountChange('', 2, 3), '');
});
