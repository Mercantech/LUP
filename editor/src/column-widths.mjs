export const COLUMN_TOTAL = 10000;
export const COLUMN_MIN = 1000;

/** Widths are stored in hundredths of one percent. */
export function savedColumnWidths(value, count) {
  if (typeof value !== 'string' || (count !== 2 && count !== 3)) return null;
  const parts = value.split(',');
  if (parts.length !== count || !parts.every(part => /^[1-9]\d{3}$/.test(part))) return null;
  const widths = parts.map(Number);
  return widths.reduce((sum, width) => sum + width, 0) === COLUMN_TOTAL ? widths : null;
}

export function columnWidths(value, count, layout) {
  const saved = savedColumnWidths(value, count);
  if (saved) return saved;
  if (count === 3) {
    if (layout === 'wideLeft') return [5000, 2500, 2500];
    if (layout === 'wideRight') return [2500, 2500, 5000];
    return [3333, 3334, 3333];
  }
  if (layout === 'wideLeft') return [6667, 3333];
  if (layout === 'wideRight') return [3333, 6667];
  return [5000, 5000];
}

/** Shift only the two tracks that meet at this divider. */
export function moveColumnDivider(widths, index, requestedWidth) {
  const pair = widths[index] + widths[index + 1];
  const next = widths.slice();
  next[index] = Math.max(COLUMN_MIN, Math.min(pair - COLUMN_MIN, Math.round(requestedWidth)));
  next[index + 1] = pair - next[index];
  return next;
}

/** Content from the last column joins the second column on a reduction. */
export function widthsAfterCountChange(stored, previousCount, nextCount) {
  const widths = savedColumnWidths(stored, previousCount);
  if (!widths) return '';
  if (previousCount === 3 && nextCount === 2) return [widths[0], widths[1] + widths[2]].join(',');
  if (previousCount === 2 && nextCount === 3) {
    const first = Math.min(widths[0], COLUMN_TOTAL - 2 * COLUMN_MIN);
    const second = Math.floor((COLUMN_TOTAL - first) / 2);
    return [first, second, COLUMN_TOTAL - first - second].join(',');
  }
  return stored;
}
