import React, { useId } from 'react';
import { createExtension, type BlockNoteEditor } from '@blocknote/core';
import { createReactBlockSpec, useEditorState } from '@blocknote/react';
import { yUndoPluginKey } from 'y-prosemirror';
import { columnLayoutShared, useColumnLayoutShared } from './column-presence';
import { ColumnResizer, columnTracks } from './column-resizer';
import { columnWidths, widthsAfterCountChange } from './column-widths.mjs';

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
    if (columns.length !== count && row.props.widths) {
      const widths = widthsAfterCountChange(row.props.widths, columns.length, count);
      editor.updateBlock(rowId, { props: { widths } });
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
export function insertColumnLayout(editor: ColumnEditor, count: 2 | 3, blockId?: string, newRow = false) {
  if (!editor.isEditable) return;
  const target = blockId ? editor.getBlock(blockId) : editor.getTextCursorPosition().block;
  if (!target) return;
  const row = findRow(editor, target);
  if ((newRow && row) || columnLayoutShared(editor)) {
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

export function insertColumnRow(editor: ColumnEditor) {
  insertColumnLayout(editor, 2, undefined, true);
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
  propSchema: {
    layout: { default: 'equal' as ColumnWidth, values: ['equal', 'wideLeft', 'wideRight'] },
    widths: { default: '' },
  },
  content: 'none',
}, {
  experimental_keyboard: { childrenCanOutdent: false, emptyChildEnter: 'stay' },
  render: function ColumnLayout({ block, editor }) {
    const shared = useColumnLayoutShared(editor);
    const sharedNote = useId();
    const marker = useId();
    const state = useEditorState({ editor, selector: () => ({
      editable: editor.isEditable, count: editor.getBlock(block.id)?.children.length ?? 0,
    }) });
    const tracks = columnTracks(columnWidths(block.props.widths, state.count, block.props.layout));
    // Scope to this rendering of the row (the same document can be open twice).
    // Keeping the style in the node view avoids changing ProseMirror-owned DOM.
    const selector = `:is([data-content-type="columnLayout"],.bn-react-node-view-renderer):has(#${CSS.escape(marker)}) ~ .bn-block-group`;
    return <div id={marker} contentEditable={false}>
      <style data-column-tracks="">{`${selector}{--notion-column-tracks:${tracks}}`}</style>
      {state.editable && <div className="notion-column-toolbar" role="group" aria-label="Kolonnelayout"
        onKeyDown={event => event.stopPropagation()} onMouseDown={event => event.stopPropagation()}>
        <button type="button" disabled={shared} aria-describedby={shared ? sharedNote : undefined}
          onClick={() => resizeRow(editor, block.id, state.count === 2 ? 3 : 2)}>
          {state.count === 2 ? 'Tilføj tredje kolonne' : 'Saml til to kolonner'}
        </button>
        <button type="button" disabled={shared} aria-describedby={shared ? sharedNote : undefined}
          onClick={() => resizeRow(editor, block.id, 1)}>Saml til én kolonne</button>
        {shared && <small id={sharedNote}>Antallet kan ændres, når du redigerer alene.</small>}
      </div>}
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
    const fullEditor: ColumnEditor = editor;
    const state = useEditorState({ editor, selector: () => {
      if (!editor.isEditable) return null;
      const children = fullEditor.getBlock(block.id)?.children;
      if (!children) return null;
      const row = fullEditor.getParentBlock(block.id);
      if (row?.type !== 'columnLayout') return null;
      const [first] = children;
      const blank = children.length === 1 && first.type === 'paragraph' && !first.children.length
        && Array.isArray(first.content) && !first.content.length;
      return { empty: children.length === 0, blank, rowId: row.id,
        index: row.children.findIndex(column => column.id === block.id), count: row.children.length,
        stored: String(row.props.widths ?? ''), layout: String(row.props.layout ?? 'equal') };
    } });
    if (!state) return null;
    const { rowId, index, count, layout, stored } = state;
    function commit(widths: number[]) {
      const row = fullEditor.getBlock(rowId);
      if (!editor.isEditable || row?.type !== 'columnLayout' || row.children.length !== count
        || row.children[index]?.id !== block.id || row.props.layout !== layout || row.props.widths !== stored) return;
      applyColumnChange(fullEditor, () => fullEditor.updateBlock(rowId, { props: { widths: widths.join(',') } }));
    }
    return <>
      {index >= 0 && index < count - 1 && (count === 2 || count === 3) &&
        <ColumnResizer index={index} widths={columnWidths(stored, count, layout)}
          revision={[rowId, index, count, layout, stored].join('/')} commit={commit}
          onHistory={redo => { if (redo) editor.redo(); else editor.undo(); }} />}
      {state.blank && <span className="notion-column-placeholder" aria-hidden="true"
        contentEditable={false}>Skriv i kolonnen…</span>}
      {state.empty && <button type="button" className="notion-column-empty" contentEditable={false}
      onMouseDown={event => event.preventDefault()} onClick={() => {
        if (!editor.isEditable || editor.getBlock(block.id)?.children.length !== 0) return;
        const [paragraph] = (editor as ColumnEditor).insertBlocks([{ type: 'paragraph' }], block.id, 'first-child');
        editor.setTextCursorPosition(paragraph);
        editor.focus();
      }}>Skriv i kolonnen</button>}
    </>;
  },
  toExternalHTML: ({ childrenRef }) => <div ref={childrenRef} />,
});
