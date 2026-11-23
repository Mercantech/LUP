import React, { useCallback, useLayoutEffect, useRef, useState } from 'react';
import { COLUMN_MIN, COLUMN_TOTAL, moveColumnDivider } from './column-widths.mjs';

export function columnTracks(widths: number[]) {
  return widths.map(width => `minmax(0, ${width}fr)`).join(' ');
}

type Drag = {
  handle: HTMLDivElement;
  grid: HTMLElement;
  rule: CSSStyleRule;
  pointerId: number;
  startX: number;
  availableWidth: number;
};

export function ColumnResizer({ index, widths, revision, commit, onHistory }: {
  index: number;
  widths: number[];
  revision: string;
  commit: (widths: number[]) => void;
  onHistory: (redo: boolean) => void;
}) {
  const drag = useRef<Drag | null>(null);
  const [preview, setPreview] = useState<number[] | null>(null);
  const cancel = useCallback(() => {
    const active = drag.current;
    if (!active) return;
    drag.current = null;
    active.rule.style.removeProperty('--notion-column-preview');
    if (active.handle.hasPointerCapture(active.pointerId)) active.handle.releasePointerCapture(active.pointerId);
    setPreview(null);
  }, []);

  // If a peer changes this row, discard a preview based on its old tracks.
  useLayoutEffect(() => cancel, [revision, cancel]);

  const live = preview ?? widths;
  const max = widths[index] + widths[index + 1] - COLUMN_MIN;
  const name = `Skillelinje mellem kolonne ${index + 1} og ${index + 2}`;

  function pointerWidths(event: React.PointerEvent) {
    const active = drag.current;
    if (!active || event.pointerId !== active.pointerId) return;
    if (getComputedStyle(active.grid).display !== 'grid') {
      cancel();
      return;
    }
    const delta = (event.clientX - active.startX) * COLUMN_TOTAL / active.availableWidth;
    return moveColumnDivider(widths, index, widths[index] + delta);
  }

  return <div role="separator" aria-orientation="vertical" aria-label={name} title={name}
    aria-valuemin={COLUMN_MIN / 100} aria-valuemax={max / 100} aria-valuenow={live[index] / 100}
    aria-valuetext={`Kolonne ${index + 1} fylder ${(live[index] / 100).toLocaleString('da-DK')} %`}
    tabIndex={0} contentEditable={false} className="notion-column-resizer" data-dragging={preview ? 'true' : undefined}
    onPointerDown={event => {
      if (!event.isPrimary || event.button !== 0 || drag.current) return;
      const handle = event.currentTarget;
      const grid = handle.closest('.bn-block-outer')?.parentElement;
      if (!grid || getComputedStyle(grid).display !== 'grid') return;
      const style = grid.parentElement?.querySelector<HTMLStyleElement>(
        ':scope > :is(.bn-react-node-view-renderer, [data-content-type="columnLayout"]) style[data-column-tracks]',
      );
      const rule = style?.sheet?.cssRules[0];
      if (!(rule instanceof CSSStyleRule)) return;
      const availableWidth = Array.from(grid.children).reduce((total, child) => total + child.getBoundingClientRect().width, 0);
      if (availableWidth <= 0) return;
      event.preventDefault();
      event.stopPropagation();
      handle.focus({ preventScroll: true });
      handle.setPointerCapture(event.pointerId);
      drag.current = { handle, grid, rule, pointerId: event.pointerId, startX: event.clientX, availableWidth };
    }}
    onPointerMove={event => {
      const next = pointerWidths(event);
      if (!next || !drag.current) return;
      drag.current.rule.style.setProperty('--notion-column-preview', columnTracks(next));
      setPreview(next);
    }}
    onPointerUp={event => {
      const next = pointerWidths(event);
      if (!next) return;
      cancel();
      if (next[index] !== widths[index]) commit(next);
    }}
    onPointerCancel={cancel} onLostPointerCapture={cancel}
    onMouseDown={event => event.stopPropagation()}
    onKeyDown={event => {
      event.stopPropagation();
      const key = event.key.toLowerCase();
      if ((event.ctrlKey || event.metaKey) && !event.altKey && (key === 'z' || key === 'y')) {
        event.preventDefault();
        cancel();
        onHistory(key === 'y' || event.shiftKey);
        return;
      }
      if (event.key === 'Escape') { cancel(); return; }
      const step = event.shiftKey ? 1000 : 200;
      const targets: Record<string, number> = {
        ArrowLeft: widths[index] - step, ArrowRight: widths[index] + step, Home: COLUMN_MIN, End: max,
      };
      if (!(event.key in targets)) return;
      event.preventDefault();
      if (drag.current) return;
      const next = moveColumnDivider(widths, index, targets[event.key]);
      if (next[index] !== widths[index]) commit(next);
    }} />;
}
