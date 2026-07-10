/** Mirrors website/cultpodcasts/src/app/person-sort.ts */

const ORG_SORT_KEYWORDS = [
  'podcast', 'news', 'morning', 'cnn', 'channel', 'fm', 'am', 'tv', 'radio',
  'network', 'show', 'official', 'bbc', 'nbc', 'abc', 'cbs', 'msnbc', 'fox',
  'sky', 'media', 'press', 'times', 'post', 'journal', 'gazette', 'tribune',
  'herald', 'daily', 'weekly', 'magazine', 'inc', 'llc', 'ltd', 'corp',
  'company', 'foundation', 'institute', 'ministry', 'church', 'temple',
  'university', 'college', 'school', 'association', 'society', 'committee',
  'commission', 'agency', 'bureau', 'department', 'office', 'group',
  'collective', 'productions', 'entertainment', 'studios', 'records'
];

const ORG_KEYWORD_PATTERN = new RegExp(
  `\\b(?:${ORG_SORT_KEYWORDS.map(escapeRegExp).join('|')})\\b`,
  'i'
);

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function deriveSortKeyFromName(name) {
  if (!name?.trim()) return '';
  const parts = name.trim().split(/\s+/);
  return parts[parts.length - 1] ?? '';
}

function looksLikeOrganization(name) {
  const trimmed = name?.trim();
  if (!trimmed) return false;
  if (ORG_KEYWORD_PATTERN.test(trimmed)) return true;
  const parts = trimmed.split(/\s+/);
  if (parts.length >= 5) return true;
  if (parts.length >= 2 && trimmed === trimmed.toUpperCase() && /[A-Z]/.test(trimmed)) {
    return true;
  }
  return false;
}

function guessSortName(name) {
  const trimmed = name?.trim() ?? '';
  if (!trimmed) return '';
  if (looksLikeOrganization(trimmed)) return trimmed;
  return deriveSortKeyFromName(trimmed);
}

function getEffectiveSortKey(person) {
  if (person.sortName?.trim()) return person.sortName.trim();
  return deriveSortKeyFromName(person.name);
}

/** Persist null when sort equals last-token default (unless org/full-name). */
function sortNameForPersist(name, sortName, useFullName) {
  const trimmed = sortName?.trim() ?? '';
  if (!trimmed) return null;
  if (useFullName) return trimmed;
  if (trimmed === deriveSortKeyFromName(name)) return null;
  return trimmed;
}
