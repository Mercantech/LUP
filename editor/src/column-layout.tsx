import React, { useId } from 'react';
import { createExtension, type BlockNoteEditor } from '@blocknote/core';
import { createReactBlockSpec, useEditorState } from '@blocknote/react';
import { yUndoPluginKey } from 'y-prosemirror';
import { columnLayoutShared, useColumnLayoutShared } from './column-presence';

type ColumnEditor = BlockNoteEditor<any, any, any>;
type Block = ReturnType<ColumnEditor['getTextCursorPosition']>['block'];
type NewBlock = Parameters<ColumnEditor['insertBlocks']>[0][number];
type ColumnCount = 1 | 2 | 3;
type ColumnWidth = 'equal' | 'wideLeft' | 'wideRight';

/** Keep a layout command separate from the Yjs undo group for adjacent typing. */
export function applyColumnChange(editor: ColumnEditor, change: () => void) {
  const history = yUndoPluginKey.getState(editor.prosemirrorState)?.undoManager;
  history?.stopCapturing();
  try { editor.transact(change); } finally { history?.stopCapturing(); }
}

function newColumn(children: NewBlock[] = [{ type: 'paragraph' }]): NewBlock {
  return { type: 'layoutColumn', children };
}

function findRow(editor: ColumnEditor, block: Block | undefined) {
  for (let ancestor = block; ancestor; ancestor = editor.getParentBlock(ancestor.id)) {
    if (ancestor.type === 'columnLayout') return ancestor;
  }
}

function columnContent(block: Block): Block[] {
  return block.type === 'layoutColumn' ? block.children : [block];
}

function resizeRow(editor: ColumnEditor, rowId: string, count: ColumnCount) {
  if (!editor.isEditable || columnLayoutShared(editor)) return;
  applyColumnChange(editor, () => {
    const row = editor.getBlock(rowId);
    if (!row || row.type !== 'columnLayout') return;
    const columns = row.children;
    if (count === 1) {
      const contents = columns.flatMap(columnContent);
      const { insertedBlocks } = editor.replaceBlocks([row], contents.length ? contents : [{ type: 'paragraph' }]);
      editor.setTextCursorPosition(insertedBlocks[0]);
      return;
    }
    if (columns.length < count) {
      const added = editor.insertBlocks(
        Array.from({ length: count - columns.length }, () => newColumn()), row, 'last-child',
      );
      editor.setTextCursorPosition(added[0].children[0]);
    } else if (columns.length > count) {
      const retained = columns[count - 1];
      const surplus = columns.slice(count);
      const contents = surplus.flatMap(columnContent);
      editor.removeBlocks(surplus);
      if (contents.length) editor.insertBlocks(contents, retained, 'last-child');
      const focus = contents[0] ?? retained.children[0] ?? retained;
      editor.setTextCursorPosition(focus);
    }
  });
  editor.focus();
}

/** Keep real block IDs, formatting, attachments and nested blocks when wrapping the current block. */
export function insertColumnLayout(editor: ColumnEditor, count: 2 | 3, blockId?: string) {
  if (!editor.isEditable) return;
  const target = blockId ? editor.getBlock(blockId) : editor.getTextCursorPosition().block;
  if (!target) return;
  const row = findRow(editor, target);
  if (columnLayoutShared(editor)) {
    // Moving a Yjs subtree recreates its text nodes. While peers are editing,
    // add independent nodes so their concurrent text updates keep their target.
    applyColumnChange(editor, () => {
      const children = Array.from({ length: count }, () => newColumn());
      const [added] = editor.insertBlocks([{ type: 'columnLayout', children }], row ?? target, 'after');
      editor.setTextCursorPosition(added.children[0].children[0]);
    });
    editor.focus();
    return;
  }
  if (row) {
    resizeRow(editor, row.id, count);
    return;
  }
  applyColumnChange(editor, () => {
    const columns = [newColumn([target]), ...Array.from({ length: count - 1 }, () => newColumn())];
    editor.replaceBlocks([target], [{ type: 'columnLayout', children: columns }]);
    editor.setTextCursorPosition(target.id, 'end');
  });
  editor.focus();
}

function navigateColumn(editor: ColumnEditor, forward: boolean) {
  if (!editor.isEditable || !editor.prosemirrorState.selection.empty) return false;
  const { block, parentBlock: column } = editor.getTextCursorPosition();
  if (column?.type !== 'layoutColumn' || !['paragraph', 'heading', 'quote'].includes(block.type)) return false;
  const row = editor.getParentBlock(column);
  if (row?.type !== 'columnLayout') return false;
  const offset = row.children.findIndex(child => child.id === column.id);
  const neighbour = row.children[offset + (forward ? 1 : -1)];
  editor.transact(() => {
    const target = neighbour
      ? neighbour.children[0] ?? editor.insertBlocks([{ type: 'paragraph' }], neighbour, 'first-child')[0]
      : (forward ? editor.getNextBlock(row) : editor.getPrevBlock(row))
        ?? editor.insertBlocks([{ type: 'paragraph' }], row, forward ? 'after' : 'before')[0];
    editor.setTextCursorPosition(target, neighbour || forward ? 'start' : 'end');
  });
  return true;
}

// BlockNote normally merges across the edge of an indented group. Keep the
// first/last paragraph in a column independent of the adjacent column.
function atColumnEdge(editor: ColumnEditor, edge: 'start' | 'end') {
  const { empty, $from } = editor.prosemirrorState.selection;
  if (!empty || !$from.parent.isTextblock) return false;
  const start = edge === 'start';
  if ($from.parentOffset !== (start ? 0 : $from.parent.content.size)) return false;
  const { block, parentBlock, prevBlock, nextBlock } = editor.getTextCursorPosition();
  if (start) return prevBlock?.type === 'columnLayout' || (!prevBlock && parentBlock?.type === 'layoutColumn');
  if (block.children.length) return false;
  if (nextBlock) return nextBlock.type === 'columnLayout';
  for (let parent = parentBlock; parent; parent = editor.getParentBlock(parent)) {
    if (parent.type === 'layoutColumn') return true;
    const next = editor.getNextBlock(parent);
    if (next) return next.type === 'columnLayout';
  }
  return false;
}

const columnKeys = createExtension({
  key: 'notion-column-navigation',
  keyboardShortcuts: {
    Tab: ({ editor }) => navigateColumn(editor, true),
    'Shift-Tab': ({ editor }) => navigateColumn(editor, false),
    Backspace: ({ editor }) => atColumnEdge(editor, 'start'),
    Delete: ({ editor }) => atColumnEdge(editor, 'end'),
  },
});

export const createColumnLayout = createReactBlockSpec({
  type: 'columnLayout',
  propSchema: { layout: { default: 'equal' as ColumnWidth, values: ['equal', 'wideLeft', 'wideRight'] } },
  content: 'none',
}, {
  experimental_keyboard: { childrenCanOutdent: false, emptyChildEnter: 'stay' },
  render: function ColumnLayout({ block, editor }) {
    const shared = useColumnLayoutShared(editor);
    const sharedNote = useId();
    const state = useEditorState({ editor, selector: () => ({
      editable: editor.isEditable, count: editor.getBlock(block.id)?.children.length ?? 0,
    }) });
    if (!state.editable) return null;
    return <div className="notion-column-toolbar" role="group" aria-label="Kolonnelayout" contentEditable={false}
      onKeyDown={event => event.stopPropagation()} onMouseDown={event => event.stopPropagation()}>
      <select aria-label="Antal kolonner" title="Antal kolonner" value={state.count} disabled={shared}
        aria-describedby={shared ? sharedNote : undefined}
        onChange={event => resizeRow(editor, block.id, Number(event.target.value) as ColumnCount)}>
        <option value="1">Saml til én kolonne</option>
        <option value="2">2 kolonner</option>
        <option value="3">3 kolonner</option>
      </select>
      <select aria-label="Kolonnebredde" title="Kolonnebredde" value={block.props.layout}
        onChange={event => {
          if (editor.isEditable && editor.getBlock(block.id)?.type === 'columnLayout') {
            const layout = event.target.value as ColumnWidth;
            applyColumnChange(editor, () => editor.updateBlock(block.id, { props: { layout } }));
          }
        }}>
        <option value="equal">Lige brede</option>
        <option value="wideLeft">Bred venstre</option>
        <option value="wideRight">Bred højre</option>
      </select>
      {shared && <small id={sharedNote}>Antallet kan ændres, når du redigerer alene.</small>}
    </div>;
  },
  toExternalHTML: ({ childrenRef }) => <div ref={childrenRef} />,
}, [columnKeys]);

export const createLayoutColumn = createReactBlockSpec({
  type: 'layoutColumn', propSchema: {}, content: 'none',
}, {
  meta: { draggable: false, dropsIntoChildren: () => true },
  experimental_keyboard: { childrenCanOutdent: false, emptyChildEnter: 'stay' },
  render: function LayoutColumn({ block, editor }) {
    const prompt = useEditorState({ editor, selector: () => {
      if (!editor.isEditable) return null;
      const children = (editor as ColumnEditor).getBlock(block.id)?.children;
      if (!children) return null;
      if (children.length === 0) return 'add';
      const [first] = children;
      return children.length === 1 && first.type === 'paragraph' && !first.children.length
        && Array.isArray(first.content) && !first.content.length ? 'write' : null;
    } });
    if (!prompt) return null;
    if (prompt === 'write') return <span className="notion-column-placeholder" aria-hidden="true"
      contentEditable={false}>Skriv i kolonnen…</span>;
    return <button type="button" className="notion-column-empty" contentEditable={false}
      onMouseDown={event => event.preventDefault()} onClick={() => {
        if (!editor.isEditable || editor.getBlock(block.id)?.children.length !== 0) return;
        const [paragraph] = (editor as ColumnEditor).insertBlocks([{ type: 'paragraph' }], block.id, 'first-child');
        editor.setTextCursorPosition(paragraph);
        editor.focus();
      }}>Skriv i kolonnen</button>;
  },
  toExternalHTML: ({ childrenRef }) => <div ref={childrenRef} />,
});
