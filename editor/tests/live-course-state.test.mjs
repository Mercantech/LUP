import { test } from 'node:test';
import assert from 'node:assert/strict';
import { activeRoundForBlock, validLiveChoices, visibleEntries, wordCloud } from '../src/live-course-state.mjs';

test('a round binds to a specific block and private module version', () => {
  const round = { id: 'runde', blockId: 'blok', moduleId: 'version-2' };
  const session = { round };
  assert.equal(activeRoundForBlock(session, 'blok', 'version-2'), round);
  assert.equal(activeRoundForBlock(session, 'blok', 'version-1'), null);
  assert.equal(activeRoundForBlock(session, 'kopi', 'version-2'), null);
  assert.equal(activeRoundForBlock(null, 'blok', 'version-2'), null);
});

test('only approved text appears when the teacher chooses to show results', () => {
  const approved = { id: '1', text: 'Godkendt', approved: true };
  const pending = { id: '2', text: 'Afventer', approved: false };
  assert.deepEqual(visibleEntries({ showResults: false, entries: [approved, pending] }), []);
  assert.deepEqual(visibleEntries({ showResults: true, entries: [approved, pending] }), [approved]);
});

test('word clouds sum individual or grouped frequencies while ignoring pending text', () => {
  const approved = { id: '1', text: 'Løkke', approved: true, votes: 1 };
  const pending = { id: '2', text: 'Hemmeligt', approved: false, votes: 25 };
  const expected = [{ id: '1', text: 'Løkke', count: 2 }];
  assert.deepEqual(wordCloud([approved, { ...approved, id: '3', text: 'løkke ' }, pending]), expected);
  assert.deepEqual(wordCloud([{ ...approved, votes: 2 }]), expected);
});

test('choice validation matches the server bounds and rejects case-insensitive duplicates', () => {
  assert.equal(validLiveChoices(['Et valg', 'Andet valg']), true);
  assert.equal(validLiveChoices(['Et valg', '  et VALG  ']), false);
  assert.equal(validLiveChoices(['Valg', '   ']), false);
  assert.equal(validLiveChoices(['Ét']), false);
  assert.equal(validLiveChoices(['a', 'b', 'c', 'd', 'e', 'f', 'g']), false);
  assert.equal(validLiveChoices(['a', 'x'.repeat(101)]), false);
});
