export const slashCategories = [
  'Tekst', 'Layout', 'Medier og links', 'Hold', 'Liveaktiviteter', 'Præsentation', 'AI',
];

function normalize(text = '') {
  return text.toLowerCase()
    .replaceAll('æ', 'ae').replaceAll('ø', 'oe').replaceAll('å', 'aa')
    .normalize('NFD').replace(/\p{M}/gu, '')
    .replace(/[^a-z0-9]+/g, ' ').trim();
}

function matchRank(item, query, words) {
  const title = normalize(item.title);
  if (title === query) return 0;
  if (title.startsWith(query)) return 1;
  if (words.every(word => title.includes(word))) return 2;

  const aliases = (item.aliases ?? []).map(normalize);
  if (words.every(word => [title, ...aliases].some(field => field.includes(word)))) return 3;

  const context = [normalize(item.group), normalize(item.subtext)];
  return words.every(word => [title, ...aliases, ...context].some(field => field.includes(word))) ? 4 : Infinity;
}

export function searchSlashCommands(items, query, category) {
  const text = normalize(query);
  const words = text.split(' ');
  const sections = new Map(slashCategories.map(group => [group, []]));

  for (const item of items) {
    const group = sections.has(item.group) ? item.group : 'Tekst';
    if (!text && category && category !== group) continue;
    const rank = text ? matchRank(item, text, words) : 0;
    if (rank !== Infinity) sections.get(group).push({ item, rank });
  }

  return [...sections.values()]
    .filter(section => section.length)
    .map(section => section.sort((a, b) => a.rank - b.rank))
    .sort((a, b) => a[0].rank - b[0].rank)
    .flatMap(section => section.map(({ item }) => item));
}
