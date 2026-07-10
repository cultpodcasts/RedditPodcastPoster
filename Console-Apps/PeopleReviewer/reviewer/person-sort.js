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

/** Strip leading "The " for corp/entity sort keys (display Name unchanged). */
function stripLeadingThe(value) {
  const trimmed = value?.trim() ?? '';
  if (trimmed.length >= 4 && /^the\s+/i.test(trimmed)) {
    return trimmed.replace(/^the\s+/i, '').trimStart();
  }
  return trimmed;
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
  if (looksLikeOrganization(trimmed)) return stripLeadingThe(trimmed);
  return deriveSortKeyFromName(trimmed);
}

function getEffectiveSortKey(person) {
  if (person.sortName?.trim()) return person.sortName.trim();
  return deriveSortKeyFromName(person.name);
}

/**
 * Persist null ONLY when effective key equals last-token surname default.
 * Org path: StripLeadingThe(Name). Keep other overrides.
 */
function sortNameForPersist(name, sortName, useFullName) {
  const trimmedName = name?.trim() ?? '';
  const lastToken = deriveSortKeyFromName(trimmedName);
  const isOrg = useFullName || looksLikeOrganization(trimmedName);
  const orgKey = isOrg ? stripLeadingThe(trimmedName) : '';

  let effective = sortName?.trim() ?? '';
  if (effective) {
    if (isOrg && orgKey) {
      const stripped = stripLeadingThe(effective);
      if (
        effective.toLowerCase() === trimmedName.toLowerCase() ||
        stripped.toLowerCase() === orgKey.toLowerCase() ||
        /^the\s+/i.test(effective)
      ) {
        effective = orgKey;
      }
    }
  } else if (isOrg && orgKey) {
    effective = orgKey;
  } else {
    effective = lastToken;
  }

  if (!effective || effective === lastToken) return null;
  return effective;
}
