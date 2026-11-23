import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as Y from 'yjs';
import { cloneNotebookDocument, defaultNotebookTemplate, notebookStateProjection, projectLivePresentation, projectNotebookCover, projectNotebookDocument, projectNotionContext } from '../src/notebook-documents.mjs';

const id = '11111111-2222-4333-8444-555555555555';
const targetId = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
const oldAsset = '22222222-2222-4222-8222-222222222222';
const newAsset = '33333333-3333-4333-8333-333333333333';
const encode = doc => Buffer.from(Y.encodeStateAsUpdate(doc)).toString('base64');
const decode = encoded => { const doc = new Y.Doc(); Y.applyUpdate(doc, Buffer.from(encoded, 'base64')); return doc; };
const element = (name, attrs, children = []) => {
  const node = new Y.XmlElement(name);
  for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, value);
  node.insert(0, children);
  return node;
};

test('live projection preserves correct slide indexes for multiple and nested activities', () => {
  const doc = new Y.Doc();
  const block = (blockId, type, attrs = {}, children = []) => element('blockContainer', { id: blockId }, [
    element(type, attrs), ...(children.length ? [element('blockGroup', {}, children)] : [])
  ]);
  doc.getXmlFragment('document-store').insert(0, [element('blockGroup', {}, [
    block('blank', 'paragraph'),
    block('split-1', 'slideBreak'),
    block('first', 'liveCourseActivity', { moduleId: id.toUpperCase(), kind: 'poll', prompt: 'Kladdeindhold' }),
    block('with-child', 'paragraph', {}, [block('second', 'liveCourseActivity', { moduleId: targetId, kind: 'brainstorm' })]),
    block('split-2', 'slideBreak'),
    block('hidden', 'classRoster', {}, [block('excluded', 'liveCourseActivity', { moduleId: targetId, kind: 'brainstorm' })]),
    block('split-3', 'slideBreak'),
    block('standalone', 'holdPoll', { pollId: oldAsset }),
    block('third', 'liveCourseActivity', { moduleId: newAsset, kind: 'quiz' })
  ])]);
  try {
    assert.deepEqual(projectLivePresentation({ stateBase64: encode(doc) }), {
      slideCount: 2,
      bindings: [
        { blockId: 'first', moduleId: id, slideIndex: 0 },
        { blockId: 'second', moduleId: targetId, slideIndex: 0 },
        { blockId: 'third', moduleId: newAsset, slideIndex: 1 }
      ]
    });
  } finally { doc.destroy(); }
});

test('live projection rejects ambiguous published bindings', () => {
  const doc = new Y.Doc();
  const activity = () => element('blockContainer', { id: 'duplicate' }, [
    element('liveCourseActivity', { kind: 'qa', moduleId: id })
  ]);
  doc.getXmlFragment('document-store').insert(0, [element('blockGroup', {}, [activity(), activity()])]);
  try {
    assert.throws(() => projectLivePresentation({ stateBase64: encode(doc) }), /Invalid live activity block/);
  } finally { doc.destroy(); }
});

test('knowledge check bindings derive only from the live teaching document and retain block IDs', () => {
  const doc = new Y.Doc({ gc: false });
  const group = element('blockGroup', {}, [
    element('blockContainer', { id: 'check-one' }, [element('knowledgeCheck', { publicationId: id })]),
    element('blockContainer', { id: 'check-two' }, [element('knowledgeCheck', { publicationId: id.toUpperCase() })]),
    element('blockContainer', { id: 'deleted' }, [element('knowledgeCheck', { publicationId: targetId })]),
    element('blockContainer', { id: 'unconfigured' }, [element('knowledgeCheck', {})]),
  ]);
  doc.getXmlFragment('document-store').insert(0, [group]);
  group.delete(2, 1);
  doc.getMap('fake-bindings').set('fake-block', targetId);
  const projection = projectNotebookDocument(doc, 'notion');
  assert.deepEqual(projection.knowledgeCheckBlocks, { 'check-one': id, 'check-two': id });
  assert.equal(projectNotebookDocument(doc, 'notebook').knowledgeCheckBlocks, undefined);
  group.push([element('blockContainer', { id: 'check-one' }, [element('knowledgeCheck', { publicationId: targetId })])]);
  assert.throws(() => projectNotebookDocument(doc, 'notion'), /Invalid knowledge check block/);
  doc.destroy();
});

test('AI projection preserves block boundaries, heading context and code without reading deleted history or side maps', () => {
  const doc = new Y.Doc({ gc: false });
  const nested = element('blockContainer', { id: 'nested' }, [element('codeBlock', { language: 'csharp' }, [new Y.XmlText('var x = 42;')])]);
  const group = element('blockGroup', {}, [
    element('blockContainer', { id: 'title' }, [element('heading', { level: 2 }, [new Y.XmlText('Relationer')])]),
    element('blockContainer', { id: 'parent' }, [element('paragraph', {}, [new Y.XmlText('Vigtig tekst')]), element('blockGroup', {}, [nested])]),
    element('blockContainer', { id: 'diagram' }, [element('mermaidDiagram', { source: 'graph TD\n A-->B' })]),
    element('blockContainer', { id: 'link' }, [element('pageLink', { pageId: id })]),
    element('blockContainer', { id: 'deleted' }, [element('paragraph', {}, [new Y.XmlText('SLETTET HEMMELIGHED')])]),
  ]);
  doc.getXmlFragment('document-store').insert(0, [group]);
  doc.getMap('ai-review-receipts').set('private', 'PRIVAT KLADDE');
  group.delete(4, 1);
  const projection = projectNotionContext({ stateBase64: encode(doc) });
  assert.deepEqual(projection.blocks.map(b => [b.id, b.type, b.text, b.heading]), [
    ['title', 'heading', 'Relationer', 'Relationer'], ['parent', 'paragraph', 'Vigtig tekst', 'Relationer'],
    ['nested', 'codeBlock', 'var x = 42;', 'Relationer'], ['diagram', 'mermaidDiagram', 'graph TD\n A-->B', 'Relationer'],
    ['link', 'pageLink', '', 'Relationer'],
  ]);
  assert.deepEqual(projection.references, [{ kind: 'notion-page', id }]);
  assert.doesNotMatch(JSON.stringify(projection), /SLETTET|HEMMELIGHED|PRIVAT/);
  assert.match(projection.plainText, /var x = 42/);
  assert.throws(() => projectNotionContext({ stateBase64: '%%%!' }), /Invalid notebook state/);
  doc.destroy();
});

test('notebook projection searches live content and references, excluding deleted history', () => {
  const doc = new Y.Doc({ gc: false });
  const group = element('blockGroup', {}, [
    element('blockContainer', { id: 'block-one' }, [element('paragraph', {}, [new Y.XmlText('Læring om C#')])]),
    element('blockContainer', { id: 'block-two' }, [element('pageLink', { pageId: id })]),
    element('blockContainer', { id: 'block-three' }, [element('pageMedia', { pageId: id, assetId: oldAsset })]),
    element('blockContainer', { id: 'removed' }, [element('paragraph', {}, [new Y.XmlText('SKJULT SLETTET')])]),
  ]);
  doc.getXmlFragment('document-store').insert(0, [group]);
  group.delete(3, 1);
  const projection = projectNotebookDocument(doc);
  assert.match(projection.plainText, /Læring om C#/);
  assert.doesNotMatch(projection.plainText, /SKJULT|SLETTET/);
  assert.deepEqual(projection.linkIds, [id]);
  assert.deepEqual(projection.assetIds, [oldAsset]);
  assert.ok(Buffer.from(encode(doc), 'base64').includes(Buffer.from('SKJULT SLETTET')), 'the deleted source really remains in CRDT history');
  doc.destroy();
});

test('cover projection reads ordered real inline and callout emojis plus shortcodes from live text', () => {
  const doc = new Y.Doc({ gc: false });
  const group = element('blockGroup', {}, [
    element('heading', { level: 2 }, [
      element('customEmoji', { emojiId: id.toUpperCase(), name: 'Raymond_Hello' }),
      new Y.XmlText(' SQL :live-text: and prefix:ignore-this: '),
      element('customEmoji', { emojiId: id, name: 'raymond_hello' })
    ]),
    element('callout', { emojiId: targetId, emojiName: 'test-callout' }, [new Y.XmlText('Think about the join.')]),
    element('codeBlock', { language: 'text' }, [new Y.XmlText(':code-example:')]),
    element('paragraph', {}, [element('customEmoji', { emojiId: oldAsset, name: 'removed' })])
  ]);
  try {
    doc.getXmlFragment('document-store').insert(0, [group]);
    group.delete(3, 1);
    doc.getMap('private').set('emojiId', newAsset);
    const result = projectNotebookCover({ stateBase64: encode(doc) });
    assert.match(result.plainText, /SQL/);
    assert.deepEqual(result.emojis, [
      { id, shortcode: 'raymond_hello' },
      { id: null, shortcode: 'live-text' },
      { id: targetId, shortcode: 'test-callout' }
    ]);
    assert.doesNotMatch(JSON.stringify(result.emojis), /removed|code-example|ignore-this/);
    assert.equal(projectNotebookDocument(doc).emojis, undefined);
  } finally { doc.destroy(); }
});

test('cover projection retains real emoji IDs when many raw shortcodes exhaust the context budget', () => {
  const doc = new Y.Doc();
  try {
    doc.getXmlFragment('document-store').insert(0, [
      new Y.XmlText(Array.from({ length: 30 }, (_, index) => `:text_${index}:`).join(' ')),
      element('customEmoji', { emojiId: id, name: 'first-real' })
    ]);
    const result = projectNotebookCover({ stateBase64: encode(doc) });
    assert.equal(result.emojis.length, 16);
    assert.deepEqual(result.emojis.at(-1), { id, shortcode: 'first-real' });
  } finally { doc.destroy(); }
});

test('template copies get fresh CRDT/block identities and local asset IDs without deleted source content', () => {
  const source = new Y.Doc({ gc: false });
  const group = element('blockGroup', {}, [
    element('blockContainer', { id: 'old-id' }, [element('paragraph', {}, [new Y.XmlText('Bevar denne tekst')])]),
    element('blockContainer', { id: 'old-media' }, [element('pageMedia', { pageId: id, assetId: oldAsset, kind: 'image' })]),
    element('blockContainer', { id: 'removed' }, [element('paragraph', {}, [new Y.XmlText('SLETTET FORTID')])]),
  ]);
  source.getXmlFragment('document-store').insert(0, [group]);
  group.delete(2, 1);
  const request = { stateBase64: encode(source), pageId: targetId, assetMap: { [oldAsset]: newAsset } };
  const first = cloneNotebookDocument(request), second = cloneNotebookDocument(request);
  const firstDoc = decode(first.stateBase64), secondDoc = decode(second.stateBase64);
  const firstGroup = firstDoc.getXmlFragment('document-store').get(0);
  const secondGroup = secondDoc.getXmlFragment('document-store').get(0);
  assert.notEqual(firstGroup.get(0).getAttribute('id'), 'old-id');
  assert.notEqual(firstGroup.get(0).getAttribute('id'), secondGroup.get(0).getAttribute('id'));
  assert.equal(firstGroup.get(1).get(0).getAttribute('pageId'), targetId);
  assert.equal(firstGroup.get(1).get(0).getAttribute('assetId'), newAsset);
  assert.deepEqual(first.assetIds, [newAsset]);
  assert.equal(first.plainText, 'Bevar denne tekst');
  assert.equal(Buffer.from(first.stateBase64, 'base64').includes(Buffer.from('SLETTET FORTID')), false);
  assert.deepEqual(notebookStateProjection(Y.encodeStateAsUpdate(firstDoc)), first);
  assert.throws(() => cloneNotebookDocument({ ...request, assetMap: {} }), /Missing notebook asset mapping/);
  source.destroy(); firstDoc.destroy(); secondDoc.destroy();
});

test('template copies retain column widths, reading order and page-local media inside columns', () => {
  const source = new Y.Doc();
  const block = (id, content, children = []) => element('blockContainer', { id }, [
    content, ...(children.length ? [element('blockGroup', {}, children)] : [])
  ]);
  const row = block('row', element('columnLayout', { layout: 'wideLeft' }), [
    block('left', element('layoutColumn', {}), [block('caption', element('paragraph', {}, [new Y.XmlText('Først')]))]),
    block('right', element('layoutColumn', {}), [
      block('photo', element('pageMedia', { pageId: id, assetId: oldAsset, kind: 'image' })),
      block('last', element('paragraph', {}, [new Y.XmlText('Sidst')]))
    ])
  ]);
  source.getXmlFragment('document-store').insert(0, [element('blockGroup', {}, [row])]);
  const copy = cloneNotebookDocument({ stateBase64: encode(source), pageId: targetId, assetMap: { [oldAsset]: newAsset } });
  const copied = decode(copy.stateBase64);
  try {
    const copiedRow = copied.getXmlFragment('document-store').get(0).get(0);
    assert.equal(copiedRow.get(0).getAttribute('layout'), 'wideLeft');
    assert.equal(copiedRow.get(1).length, 2);
    assert.deepEqual(copy.assetIds, [newAsset]);
    const contents = projectNotionContext({ stateBase64: copy.stateBase64 }).blocks;
    assert.deepEqual(contents.map(block => block.type), ['columnLayout', 'layoutColumn', 'paragraph', 'layoutColumn', 'pageMedia', 'paragraph']);
    assert.deepEqual(contents.filter(block => block.text).map(block => block.text), ['Først', 'Sidst']);
  } finally { source.destroy(); copied.destroy(); }
});

test('default template has three editable H2 prompts and empty answers; blank note starts empty', () => {
  const doc = defaultNotebookTemplate();
  const group = doc.getXmlFragment('document-store').get(0);
  assert.equal(group.length, 6);
  assert.equal(group.get(0).get(0).nodeName, 'heading');
  assert.equal(group.get(0).get(0).getAttribute('level'), 2);
  assert.equal(group.get(1).get(0).nodeName, 'paragraph');
  assert.match(projectNotebookDocument(doc).plainText, /Hvad arbejdede jeg med\?/);
  assert.match(cloneNotebookDocument({ pageId: id, defaultTemplate: true }).plainText, /Hvad er mit næste skridt\?/);
  assert.equal(cloneNotebookDocument({ pageId: id }).plainText, '');
  assert.throws(() => cloneNotebookDocument({ pageId: 'invalid', defaultTemplate: true }));
  assert.throws(() => cloneNotebookDocument({ pageId: id, stateBase64: 'invalid' }));
  const fixture = JSON.parse(readFileSync(new URL('./fixtures/default-notebook-template.json', import.meta.url)));
  assert.deepEqual(notebookStateProjection(Buffer.from(fixture.stateBase64, 'base64')), { ...fixture, drawingIds: [], drawingBlocks: {}, references: [] });
  doc.destroy();
});

test('nested notebook drawing blocks remap to independent drawings and fresh block IDs', () => {
  const source = new Y.Doc({ gc: false });
  const drawing = element('drawing', { drawingId: oldAsset, schemaVersion: 1 });
  const container = element('blockContainer', { id: 'old-drawing-block' }, [drawing]);
  source.getXmlFragment('document-store').insert(0, [element('blockGroup', {}, [element('blockContainer', { id: 'parent' }, [element('paragraph', {}, []), element('blockGroup', {}, [container])])])]);
  assert.deepEqual(projectNotebookDocument(source).drawingIds, [oldAsset]);
  const request = { stateBase64: encode(source), pageId: targetId, drawingMap: { [oldAsset]: newAsset } };
  const result = cloneNotebookDocument(request), copy = decode(result.stateBase64);
  assert.deepEqual(result.drawingIds, [newAsset]);
  const copied = copy.getXmlFragment('document-store').get(0).get(0).get(1).get(0);
  assert.notEqual(copied.getAttribute('id'), 'old-drawing-block');
  assert.equal(result.drawingBlocks[newAsset], copied.getAttribute('id'));
  assert.equal(copied.get(0).getAttribute('drawingId'), newAsset);
  assert.throws(() => cloneNotebookDocument({ ...request, drawingMap: {} }), /Missing notebook drawing/);
  assert.throws(() => cloneNotebookDocument({ ...request, drawingMap: { [oldAsset]: oldAsset } }), /Invalid notebook drawing/);
  source.destroy(); copy.destroy();
});

test('nullable C# drawingMap transport keeps legacy blank template creation working', () => {
  const projection = cloneNotebookDocument({ stateBase64: null, pageId: id, assetMap: {}, defaultTemplate: false, drawingMap: null });
  assert.equal(projection.plainText, '');
  assert.deepEqual(projection.drawingIds, []);
  assert.deepEqual(projection.drawingBlocks, {});
});

test('typed and legacy references project across domains without labels or deleted history', () => {
  const doc = new Y.Doc({ gc: false });
  const group = element('blockGroup', {}, [
    element('blockContainer', { id: 'legacy' }, [element('pageLink', { pageId: targetId })]),
    element('blockContainer', { id: 'typed' }, [element('paragraph', {}, [element('entityMention', { kind: 'person', id })])]),
    element('blockContainer', { id: 'slug' }, [element('entityLink', { kind: 'subject', id: 'database/joins' })]),
    element('blockContainer', { id: 'poll' }, [element('holdPoll', { holdId: id, pollId: oldAsset })]),
    element('blockContainer', { id: 'removed' }, [element('entityMention', { kind: 'person', id: newAsset })]),
  ]);
  doc.getXmlFragment('document-store').insert(0, [group]); group.delete(4, 1);
  assert.deepEqual(projectNotebookDocument(doc).references, [
    { kind: 'notebook-page', id: targetId }, { kind: 'person', id }, { kind: 'poll', id: oldAsset }, { kind: 'subject', id: 'database/joins' },
  ]);
  assert.equal(projectNotebookDocument(doc, 'notion').references[0].kind, 'notion-page');
  assert.deepEqual(projectNotebookDocument(doc).linkIds, [targetId]);
  assert.equal(JSON.stringify(projectNotebookDocument(doc).references).includes(newAsset), false);
  doc.destroy();
});
