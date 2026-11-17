/** @typedef {import('./live-course-api').LiveSnapshot} LiveSnapshot */
/** @typedef {import('./live-course-api').LiveRound} LiveRound */
/** @typedef {import('./live-course-api').LiveEntry} LiveEntry */

export const livePromptLimit = 200;
export const liveOptionLimit = 100;
export const liveMaxOptions = 6;

/** @param {string[]} options */
export function validLiveChoices(options) {
  const normalized = options.map(option => option.trim());
  return normalized.length >= 2 && normalized.length <= liveMaxOptions
    && normalized.every(option => option.length >= 1 && option.length <= liveOptionLimit)
    && new Set(normalized.map(option => option.toLowerCase())).size === normalized.length;
}

/** @param {LiveSnapshot|null} session @param {string} blockId @param {string} moduleId */
export function activeRoundForBlock(session, blockId, moduleId) {
  const round = session?.round;
  return round && round.blockId === blockId && round.moduleId === moduleId ? round : null;
}

/** Pending text must never be rendered in the shared presentation or results. @param {LiveRound} round */
export function visibleEntries(round) {
  return round.showResults ? round.entries.filter(entry => entry.approved) : [];
}

/** @param {LiveEntry[]} entries */
export function wordCloud(entries) {
  const words = new Map();
  for (const entry of entries) {
    if (!entry.approved) continue;
    const text = entry.text.trim();
    if (!text) continue;
    const normalized = text.normalize('NFC').toLocaleLowerCase('da-DK');
    const current = words.get(normalized);
    const frequency = Math.max(1, entry.votes || 1);
    if (current) current.count += frequency;
    else words.set(normalized, { id: entry.id, text, count: frequency });
  }
  return [...words.values()].sort((first, second) => second.count - first.count || first.text.localeCompare(second.text, 'da-DK'));
}
