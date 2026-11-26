import assert from 'node:assert/strict';
import test from 'node:test';
import { searchSlashCommands } from '../src/slash-command-search.mjs';

const commands = [
  { title: 'Afstemning fra holdet', group: 'Liveaktiviteter' },
  { title: 'Læringsmål', group: 'Hold' },
  { title: 'Ordsky', group: 'Liveaktiviteter', aliases: ['word-cloud'] },
  { title: 'Overskrift 1', group: 'Tekst' },
  { title: 'Præsenter', group: 'Præsentation' },
  { title: 'Afstemning', group: 'Liveaktiviteter' },
];

const titles = (query, category) => searchSlashCommands(commands, query, category).map(item => item.title);

test('common commands come first, and browsing a category keeps its commands together', () => {
  assert.deepEqual(titles(''), ['Overskrift 1', 'Læringsmål', 'Afstemning fra holdet', 'Ordsky', 'Afstemning', 'Præsenter']);
  assert.deepEqual(titles('', 'Liveaktiviteter'), ['Afstemning fra holdet', 'Ordsky', 'Afstemning']);
});

test('search crosses categories and ranks an exact command above a partial match', () => {
  assert.deepEqual(titles('afstemning', 'Tekst'), ['Afstemning', 'Afstemning fra holdet']);
  assert.deepEqual(titles('live ordsky'), ['Ordsky']);
  assert.deepEqual(titles('word cloud'), ['Ordsky']);
  assert.deepEqual(titles('findes ikke'), []);
});

test('Danish letters match common ASCII spellings', () => {
  assert.deepEqual(titles('praesenter'), ['Præsenter']);
  assert.deepEqual(titles('laeringsmaal'), ['Læringsmål']);
});
