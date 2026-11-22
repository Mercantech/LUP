import { randomUUID } from 'node:crypto';
import * as Y from 'yjs';
import { fingerprintHash } from './fingerprint.mjs';

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const emptyGuid = '00000000-0000-0000-0000-000000000000';
const MAX_BYTES = 16 * 1024 * 1024;
const MAX_COVER_EMOJIS = 16;
const MAX_COVER_TEXT = 4096;

function decode(encoded) {
  if (typeof encoded !== 'string' || !encoded || encoded.length > Math.ceil(MAX_BYTES * 4 / 3) + 4
      || !/^[A-Za-z0-9+/]+={0,2}$/.test(encoded)) throw new Error('Invalid notebook state');
  const bytes = new Uint8Array(Buffer.from(encoded, 'base64'));
  if (!bytes.length || bytes.length > MAX_BYTES) throw new Error('Invalid notebook state');
  const doc = new Y.Doc();
  try { Y.applyUpdate(doc, bytes); return doc; }
  catch { doc.destroy(); throw new Error('Invalid notebook state'); }
}

/** Only integrated, live XML children are visited. Deleted CRDT history is never searchable. */
export function projectNotebookDocument(doc, domain = 'notebook', includeCoverEmojis = false) {
  const text = [], links = new Set(), assets = new Set(), drawings = new Set(), drawingBlocks = {}, references = new Map();
  const knowledgeCheckBlocks = new Map();
  const emojis = includeCoverEmojis ? new Map() : null;
  const addEmoji = (value, name) => {
    const id = typeof value === 'string' && guid.test(value) && value !== emptyGuid ? value.toLowerCase() : null;
    const shortcode = typeof name === 'string' && /^[a-z0-9_-]{2,32}$/i.test(name) ? name.toLowerCase() : null;
    if (!emojis || (!id && (value != null || !shortcode))) return;
    const key = id ?? ':' + shortcode;
    if (emojis.has(key)) return;
    if (emojis.size >= MAX_COVER_EMOJIS) {
      if (!id) return;
      // Preserve actual emoji elements ahead of low-confidence text aliases when the sample is full.
      const textAlias = [...emojis.keys()].reverse().find(item => item.startsWith(':'));
      if (!textAlias) return;
      emojis.delete(textAlias);
    }
    emojis.set(key, { id, shortcode });
  };
  const reference = (kind, id) => {
    if (typeof kind !== 'string' || !/^[a-z][a-z0-9-]{0,47}$/.test(kind) || typeof id !== 'string' || !id || id.length > 240 || /[\u0000-\u001f\u007f]/.test(id)) return;
    if (guid.test(id)) id = id.toLowerCase();
    references.set(kind + ':' + id, { kind, id });
  };
  let visited = 0;
  const visit = (node, depth = 0, blockId, inCode = false) => {
    if (++visited > 250000 || depth > 256) throw new Error('Notebook document is too complex');
    if (node instanceof Y.XmlText) {
      const parts = node.toDelta();
      text.push(parts.map(item => typeof item.insert === 'string' ? item.insert : '').join(''));
      if (includeCoverEmojis && !inCode) {
        const readable = parts.map(item => typeof item.insert === 'string' && !item.attributes?.code ? item.insert : ' ').join('');
        for (const match of readable.matchAll(/(?<![\p{L}\p{N}_]):([a-zA-Z0-9_-]{2,32}):/gu)) addEmoji(null, match[1]);
      }
      return;
    }
    if (node instanceof Y.XmlElement) {
      const attrs = node.getAttributes();
      if (includeCoverEmojis && node.nodeName === 'customEmoji') addEmoji(attrs.emojiId, attrs.name);
      if (includeCoverEmojis && node.nodeName === 'callout' && attrs.emojiId) addEmoji(attrs.emojiId, attrs.emojiName);
      if (node.nodeName === 'blockContainer' && typeof attrs.id === 'string') blockId = attrs.id;
      if (node.nodeName === 'pageLink' || node.nodeName === 'pageMention') {
        if (typeof attrs.pageId === 'string' && guid.test(attrs.pageId)) { links.add(attrs.pageId.toLowerCase()); reference(domain === 'notion' ? 'notion-page' : 'notebook-page', attrs.pageId); }
      }
      if (node.nodeName === 'entityMention' || node.nodeName === 'entityLink') reference(attrs.kind, attrs.id);
      if (node.nodeName === 'studentMention') reference('member', attrs.membershipId);
      if (node.nodeName === 'holdPoll') reference('poll', attrs.pollId);
      if (domain === 'notion' && node.nodeName === 'knowledgeCheck' && attrs.publicationId) {
        if (typeof attrs.publicationId !== 'string' || !guid.test(attrs.publicationId)
          || typeof blockId !== 'string' || !/^[a-z0-9_-]{1,128}$/i.test(blockId) || knowledgeCheckBlocks.has(blockId))
          throw new Error('Invalid knowledge check block');
        knowledgeCheckBlocks.set(blockId, attrs.publicationId.toLowerCase());
      }
      if (node.nodeName === 'holdGroup') reference('group', attrs.groupId);
      if (node.nodeName === 'classRoster') {
        reference('hold', attrs.holdId);
        for (const id of String(attrs.membershipIds || '').split(',')) if (guid.test(id)) reference('member', id);
      }
      if (node.nodeName === 'pageMedia' && typeof attrs.assetId === 'string' && guid.test(attrs.assetId)) { assets.add(attrs.assetId.toLowerCase()); reference(domain === 'notion' ? 'notion-asset' : 'notebook-asset', attrs.assetId); }
      if (node.nodeName === 'drawing' && typeof attrs.drawingId === 'string' && guid.test(attrs.drawingId)) {
        const id = attrs.drawingId.toLowerCase();
        drawings.add(id);
        reference('drawing', id);
        if (blockId) {
          if (drawingBlocks[id] && drawingBlocks[id] !== blockId) throw new Error('A drawing cannot belong to multiple blocks');
          drawingBlocks[id] = blockId;
        }
      }
      if (node.nodeName === 'mermaidDiagram' && typeof attrs.source === 'string') text.push(attrs.source);
    }
    if (node instanceof Y.XmlElement || node instanceof Y.XmlFragment) {
      const childInCode = inCode || node.nodeName === 'codeBlock';
      for (const child of node.toArray()) visit(child, depth + 1, blockId, childInCode);
      if (node instanceof Y.XmlElement && !['pageMention', 'studentMention', 'entityMention', 'customEmoji'].includes(node.nodeName)) text.push('\n');
    }
  };
  visit(doc.getXmlFragment('document-store'));
  return { plainText: text.join('').replace(/\n{3,}/g, '\n\n').trim(), linkIds: [...links].sort(), references: [...references].sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0).map(([, value]) => value), assetIds: [...assets].sort(), drawingIds: [...drawings].sort(), drawingBlocks: Object.fromEntries(Object.entries(drawingBlocks).sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0)), ...(domain === 'notion' && { knowledgeCheckBlocks: Object.fromEntries([...knowledgeCheckBlocks].sort()) }), ...(emojis && { emojis: [...emojis.values()] }) };
}

export function notebookStateProjection(state, domain = 'notebook') {
  const doc = new Y.Doc();
  try {
    Y.applyUpdate(doc, state);
    return { stateBase64: Buffer.from(state).toString('base64'), fingerprint: fingerprintHash(state, Y.encodeStateVector(doc)), ...projectNotebookDocument(doc, domain) };
  } finally { doc.destroy(); }
}

/** Cover context needs a single live walk of teaching or personal notebook content. */
export function projectNotebookCover({ stateBase64 } = {}) {
  const doc = decode(stateBase64);
  try {
    const { plainText, emojis } = projectNotebookDocument(doc, 'notebook', true);
    // Bound the companion text response while still finding emoji nodes anywhere in the saved sample.
    const text = plainText.slice(0, MAX_COVER_TEXT).replace(/[\uD800-\uDBFF]$/, '');
    return { plainText: text, emojis };
  } finally { doc.destroy(); }
}

/** Internal AI projection: only live Notion blocks, never CRDT tombstones or private side maps. */
export function projectNotionContext({ stateBase64 } = {}) {
  const doc = decode(stateBase64);
  try {
    const projection = projectNotebookDocument(doc, 'notion');
    const blocks = [];
    let visited = 0, heading = null;
    const ownText = (node, depth = 0) => {
      if (++visited > 250000 || depth > 256) throw new Error('Notion document is too complex');
      if (node instanceof Y.XmlText) return node.toDelta().map(part => typeof part.insert === 'string' ? part.insert : '').join('');
      if (!(node instanceof Y.XmlElement) || node.nodeName === 'blockGroup' || node.nodeName === 'blockContainer') return '';
      if (node.nodeName === 'mermaidDiagram') return String(node.getAttribute('source') || '');
      const text = node.toArray().map(child => ownText(child, depth + 1)).join(node.nodeName === 'tableRow' ? '\t' : '');
      return node.nodeName === 'tableRow' ? text + '\n' : text;
    };
    const visit = (node, depth = 0) => {
      if (++visited > 250000 || depth > 256) throw new Error('Notion document is too complex');
      if (!(node instanceof Y.XmlElement || node instanceof Y.XmlFragment)) return;
      if (node instanceof Y.XmlElement && node.nodeName === 'blockContainer') {
        const content = node.toArray().find(child => child instanceof Y.XmlElement && child.nodeName !== 'blockGroup');
        const id = node.getAttribute('id');
        if (content && typeof id === 'string' && id) {
          const text = ownText(content).trim();
          if (content.nodeName === 'heading') heading = text || heading;
          blocks.push({ id, type: content.nodeName, text, heading });
        }
      }
      for (const child of node.toArray()) visit(child, depth + 1);
    };
    visit(doc.getXmlFragment('document-store'));
    return { plainText: projection.plainText, references: projection.references, blocks };
  } finally { doc.destroy(); }
}

/** Only stable interactive block IDs and slide indexes leave the published Yjs state. */
export function projectLivePresentation({ stateBase64 } = {}) {
  const doc = decode(stateBase64);
  try {
    // Match the editor's splitSlides rules: top-level breaks split, rosters and empty slides disappear.
    const slides = [[]];
    const kinds = new Set(['poll', 'brainstorm', 'word-cloud', 'qa', 'quiz']);
    const seen = new Set();
    let visited = 0;

    function readBlock(container, depth = 0) {
      if (++visited > 250000 || depth > 256) throw new Error('Notion document is too complex');
      if (!(container instanceof Y.XmlElement) || container.nodeName !== 'blockContainer') return null;
      const nodes = container.toArray().filter(node => node instanceof Y.XmlElement);
      const content = nodes.find(node => node.nodeName !== 'blockGroup');
      if (!content || content.nodeName === 'classRoster') return null;

      const type = content.nodeName;
      const children = nodes.filter(node => node.nodeName === 'blockGroup')
        .flatMap(group => group.toArray().map(node => readBlock(node, depth + 1)).filter(Boolean));
      const blockId = container.getAttribute('id');
      const moduleId = type === 'liveCourseActivity' ? content.getAttribute('moduleId') : undefined;
      let binding;
      if (moduleId) {
        if (typeof moduleId !== 'string' || !guid.test(moduleId) || !kinds.has(content.getAttribute('kind'))
          || typeof blockId !== 'string' || !/^[a-z0-9_-]{1,128}$/i.test(blockId) || seen.has(blockId))
          throw new Error('Invalid live activity block');
        seen.add(blockId);
        binding = { blockId, moduleId: moduleId.toLowerCase() };
      }

      const hasContent = type !== 'paragraph' || content.toArray().some(child =>
        child instanceof Y.XmlText ? child.toDelta().some(part => typeof part.insert === 'string' && part.insert.trim()) : true);
      return {
        type,
        visible: hasContent || children.some(child => child.visible),
        activities: [...(binding ? [binding] : []), ...children.flatMap(child => child.activities)]
      };
    }

    for (const node of doc.getXmlFragment('document-store').toArray()) {
      const containers = node instanceof Y.XmlElement && node.nodeName === 'blockGroup' ? node.toArray() : [node];
      for (const container of containers) {
        const block = readBlock(container);
        if (block?.type === 'slideBreak') slides.push([]);
        else if (block) slides.at(-1).push(block);
      }
    }
    const visible = slides.filter(slide => slide.some(block => block.visible));
    return {
      slideCount: Math.max(1, visible.length),
      bindings: visible.flatMap((slide, slideIndex) =>
        slide.flatMap(block => block.activities.map(activity => ({ ...activity, slideIndex }))))
    };
  } finally { doc.destroy(); }
}

function appendDefaultBlock(group, type, content = '', level) {
  const container = new Y.XmlElement('blockContainer');
  container.setAttribute('id', randomUUID());
  const block = new Y.XmlElement(type);
  if (level) block.setAttribute('level', level);
  if (content) block.insert(0, [new Y.XmlText(content)]);
  container.insert(0, [block]);
  group.push([container]);
}

export function defaultNotebookTemplate() {
  const doc = new Y.Doc();
  const group = new Y.XmlElement('blockGroup');
  doc.getXmlFragment('document-store').insert(0, [group]);
  for (const question of ['Hvad arbejdede jeg med?', 'Hvad lærte jeg?', 'Hvad er mit næste skridt?']) {
    appendDefaultBlock(group, 'heading', question, 2);
    appendDefaultBlock(group, 'paragraph');
  }
  return doc;
}

/** Fresh CRDT identities, fresh block IDs and page-local asset grants for an independent student copy. */
export function cloneNotebookDocument({ stateBase64, pageId, assetMap = {}, drawingMap = {}, defaultTemplate = false } = {}) {
  drawingMap ??= {};
  if (typeof pageId !== 'string' || !guid.test(pageId) || !assetMap || typeof assetMap !== 'object' || Array.isArray(assetMap) || !drawingMap || typeof drawingMap !== 'object' || Array.isArray(drawingMap)) throw new Error('Invalid notebook clone request');
  const mappings = new Map();
  for (const [from, to] of Object.entries(assetMap)) {
    if (!guid.test(from) || typeof to !== 'string' || !guid.test(to)) throw new Error('Invalid notebook asset mapping');
    mappings.set(from.toLowerCase(), to.toLowerCase());
  }
  const drawingMappings = new Map();
  for (const [from, to] of Object.entries(drawingMap)) {
    if (!guid.test(from) || typeof to !== 'string' || !guid.test(to) || from.toLowerCase() === to.toLowerCase()) throw new Error('Invalid notebook drawing mapping');
    drawingMappings.set(from.toLowerCase(), to.toLowerCase());
  }
  const source = defaultTemplate ? defaultNotebookTemplate() : stateBase64 ? decode(stateBase64) : new Y.Doc();
  const target = new Y.Doc();
  try {
    const sourceRoot = source.getXmlFragment('document-store');
    let count = 0;
    const copy = (node, depth = 0) => {
      if (++count > 250000 || depth > 256) throw new Error('Notebook document is too complex');
      if (node instanceof Y.XmlText) return node.clone();
      if (!(node instanceof Y.XmlElement)) throw new Error('Unsupported notebook node');
      const result = new Y.XmlElement(node.nodeName);
      const attrs = { ...node.getAttributes() };
      if (node.nodeName === 'blockContainer') attrs.id = randomUUID();
      if (node.nodeName === 'pageMedia') {
        const oldId = attrs.assetId;
        if (oldId) {
          const newId = mappings.get(String(oldId).toLowerCase());
          if (!newId) throw new Error('Missing notebook asset mapping');
          attrs.assetId = newId;
        }
        attrs.pageId = pageId.toLowerCase();
      }
      if (node.nodeName === 'drawing') {
        if (attrs.drawingId) {
          const newId = drawingMappings.get(String(attrs.drawingId).toLowerCase());
          if (!newId) throw new Error('Missing notebook drawing mapping');
          attrs.drawingId = newId;
        }
      }
      for (const [key, value] of Object.entries(attrs)) result.setAttribute(key, value);
      result.insert(0, node.toArray().map(child => copy(child, depth + 1)));
      return result;
    };
    target.getXmlFragment('document-store').insert(0, sourceRoot.toArray().map(node => copy(node)));
    return notebookStateProjection(Y.encodeStateAsUpdate(target));
  } finally { source.destroy(); target.destroy(); }
}
