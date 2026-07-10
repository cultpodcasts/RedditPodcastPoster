const state = {
  document: null,
  filteredIndices: [],
  currentIndex: -1,
  sortNameManuallyEdited: false,
  syncingSort: false
};

const BSKY_PLACEHOLDER = 'user.bsky.social';

const els = {
  seedMeta: document.getElementById('seed-meta'),
  search: document.getElementById('search'),
  filterAliases: document.getElementById('filter-aliases'),
  filterMissingSort: document.getElementById('filter-missing-sort'),
  peopleList: document.getElementById('people-list'),
  editor: document.getElementById('editor'),
  position: document.getElementById('position'),
  status: document.getElementById('status'),
  name: document.getElementById('name'),
  sortName: document.getElementById('sort-name'),
  sortsAsHint: document.getElementById('sorts-as-hint'),
  useFullName: document.getElementById('use-full-name'),
  twitter: document.getElementById('twitter'),
  bluesky: document.getElementById('bluesky'),
  notes: document.getElementById('notes'),
  aliases: document.getElementById('aliases'),
  newAlias: document.getElementById('new-alias'),
  episodes: document.getElementById('episodes'),
  episodeCount: document.getElementById('episode-count'),
  twitterProfileLink: document.getElementById('twitter-profile-link'),
  blueskyProfileLink: document.getElementById('bluesky-profile-link')
};

function normalizeHandle(value) {
  return (value || '').trim().replace(/^@+/, '');
}

function isRealHandle(platform, value) {
  const token = normalizeHandle(value);
  if (!token) return false;
  if (platform === 'bsky' && token.toLowerCase() === BSKY_PLACEHOLDER) return false;
  return true;
}

function profileUrl(platform, handle) {
  const token = normalizeHandle(handle);
  if (!token) return null;
  if (platform === 'x') return `https://x.com/${encodeURIComponent(token)}`;
  const actor = token.includes('.') ? token : `${token}.bsky.social`;
  return `https://bsky.app/profile/${encodeURIComponent(actor)}`;
}

function updateInlineProfileLink(anchor, platform, value) {
  const url = isRealHandle(platform, value) ? profileUrl(platform, value) : null;
  if (url) {
    anchor.href = url;
    anchor.hidden = false;
  } else {
    anchor.removeAttribute('href');
    anchor.hidden = true;
  }
}

function setStatus(message, isError = false) {
  els.status.textContent = message;
  els.status.style.color = isError ? 'var(--danger)' : 'var(--muted)';
}

function currentPerson() {
  if (state.currentIndex < 0 || !state.document) return null;
  return state.document.people[state.currentIndex];
}

function updateSortsAsHint() {
  const name = els.name.value;
  const sortName = els.sortName.value;
  const key = getEffectiveSortKey({ name, sortName }) || guessSortName(name) || '—';
  els.sortsAsHint.textContent = `Sorts as: ${key}`;
}

function hasCustomSort(person) {
  const stored = person.sortName?.trim() ?? '';
  if (!stored) return false;
  return stored !== deriveSortKeyFromName(person.name);
}

function applyFilters() {
  const query = els.search.value.trim().toLowerCase();
  const aliasesOnly = els.filterAliases.checked;
  const missingSortOnly = els.filterMissingSort.checked;
  state.filteredIndices = state.document.people
    .map((person, index) => ({ person, index }))
    .filter(({ person }) => {
      if (aliasesOnly && (!person.aliases || person.aliases.length === 0)) return false;
      if (missingSortOnly && hasCustomSort(person)) return false;
      if (!query) return true;
      const haystack = [
        person.name,
        person.sortName,
        person.twitterHandle,
        person.blueskyHandle,
        person.notes,
        ...(person.aliases || [])
      ].join(' ').toLowerCase();
      return haystack.includes(query);
    })
    .map(x => x.index);

  renderList();
  if (state.filteredIndices.length === 0) {
    els.editor.hidden = true;
    setStatus('No matching records');
    return;
  }

  if (!state.filteredIndices.includes(state.currentIndex)) {
    selectIndex(state.filteredIndices[0]);
  } else {
    renderEditor();
  }
}

function renderList() {
  els.peopleList.innerHTML = '';
  for (const index of state.filteredIndices) {
    const person = state.document.people[index];
    const li = document.createElement('li');
    const button = document.createElement('button');
    button.type = 'button';
    button.className = index === state.currentIndex ? 'active' : '';
    const sortHint = getEffectiveSortKey(person) || guessSortName(person.name);
    button.innerHTML = `${escapeHtml(person.name)}<span class="meta">${escapeHtml(sortHint)} · ${escapeHtml(person.twitterHandle || person.blueskyHandle || 'no handle')}</span>`;
    button.addEventListener('click', () => selectIndex(index));
    li.appendChild(button);
    els.peopleList.appendChild(li);
  }
}

function renderAliases(aliases) {
  els.aliases.innerHTML = '';
  for (const alias of aliases || []) {
    const li = document.createElement('li');
    const span = document.createElement('span');
    span.className = 'alias-text';
    span.textContent = alias;

    const actions = document.createElement('span');
    actions.className = 'alias-actions';

    const swap = document.createElement('button');
    swap.type = 'button';
    swap.className = 'swap';
    swap.textContent = 'Swap';
    swap.title = 'Make this alias the canonical name';
    swap.addEventListener('click', () => swapAliasWithCanonical(alias));

    const remove = document.createElement('button');
    remove.type = 'button';
    remove.className = 'danger';
    remove.textContent = 'Remove';
    remove.addEventListener('click', () => {
      const person = currentPerson();
      person.aliases = (person.aliases || []).filter(x => x !== alias);
      renderAliases(person.aliases);
    });

    actions.appendChild(swap);
    actions.appendChild(remove);
    li.appendChild(span);
    li.appendChild(actions);
    els.aliases.appendChild(li);
  }
}

function sameName(a, b) {
  return (a || '').trim().toLowerCase() === (b || '').trim().toLowerCase();
}

function swapAliasWithCanonical(alias) {
  readEditorIntoPerson();
  const person = currentPerson();
  if (!person) return;

  const trimmedAlias = (alias || '').trim();
  if (!trimmedAlias) return;

  const aliasList = (person.aliases || [])
    .filter(x => x && x.trim())
    .map(x => x.trim());

  if (!aliasList.some(a => sameName(a, trimmedAlias))) {
    setStatus(`Alias not found: ${trimmedAlias}`, true);
    return;
  }

  const previousCanonical = (person.name || '').trim();
  const remaining = aliasList.filter(a => !sameName(a, trimmedAlias));
  if (previousCanonical && !remaining.some(a => sameName(a, previousCanonical))) {
    remaining.unshift(previousCanonical);
  }

  person.name = trimmedAlias;
  person.aliases = remaining;
  state.sortNameManuallyEdited = false;
  els.name.value = person.name;
  const guess = guessSortName(person.name);
  els.sortName.value = guess;
  els.useFullName.checked = looksLikeOrganization(person.name) && guess === person.name.trim();
  person.sortName = sortNameForPersist(person.name, guess, els.useFullName.checked);
  renderAliases(person.aliases);
  updateSortsAsHint();
  renderList();
  setStatus('Swapped canonical (not saved yet)');
}

function renderEpisodes(episodeIds) {
  els.episodes.innerHTML = '';
  for (const episodeId of episodeIds || []) {
    const li = document.createElement('li');
    const span = document.createElement('span');
    span.textContent = episodeId;
    span.className = 'episode-id-unlinked';
    li.appendChild(span);
    els.episodes.appendChild(li);
  }
}

function renderEditor() {
  const person = currentPerson();
  if (!person) return;

  els.editor.hidden = false;
  const filteredPos = state.filteredIndices.indexOf(state.currentIndex);
  els.position.textContent = `${filteredPos + 1} / ${state.filteredIndices.length} (record #${state.currentIndex + 1})`;

  state.syncingSort = true;
  const name = person.name || '';
  const storedSort = person.sortName?.trim() ?? '';
  const guess = guessSortName(name);
  const displaySort = storedSort || guess;
  els.name.value = name;
  els.sortName.value = displaySort;
  els.useFullName.checked = !!displaySort && displaySort === name.trim();
  state.sortNameManuallyEdited =
    !!storedSort && storedSort !== guess && storedSort !== deriveSortKeyFromName(name);
  state.syncingSort = false;

  els.twitter.value = person.twitterHandle || '';
  els.bluesky.value = person.blueskyHandle || '';
  els.notes.value = person.notes || '';
  renderAliases(person.aliases || []);
  updateSortsAsHint();

  updateInlineProfileLink(els.twitterProfileLink, 'x', els.twitter.value);
  updateInlineProfileLink(els.blueskyProfileLink, 'bsky', els.bluesky.value);

  const episodeIds = person.sourceEpisodeIds || [];
  els.episodeCount.textContent = String(episodeIds.length);
  renderEpisodes(episodeIds);

  document.getElementById('prev').disabled = filteredPos <= 0;
  document.getElementById('next').disabled = filteredPos >= state.filteredIndices.length - 1;

  renderList();
}

function readEditorIntoPerson() {
  const person = currentPerson();
  if (!person) return;
  person.name = els.name.value.trim();
  person.sortName = sortNameForPersist(person.name, els.sortName.value, els.useFullName.checked);
  person.twitterHandle = els.twitter.value.trim() || null;
  person.blueskyHandle = els.bluesky.value.trim() || null;
  person.notes = els.notes.value.trim() || null;
}

function selectIndex(index) {
  state.currentIndex = index;
  renderEditor();
  setStatus('');
}

async function loadSeed() {
  const response = await fetch('/api/seed');
  if (!response.ok) throw new Error('Failed to load seed');
  state.document = await response.json();
  const generated = state.document.generatedAt
    ? new Date(state.document.generatedAt).toLocaleString()
    : 'unknown';
  els.seedMeta.textContent = `${state.document.people.length} people · generated ${generated}`;
  state.currentIndex = state.document.people.length > 0 ? 0 : -1;
  applyFilters();
}

async function saveSeed() {
  readEditorIntoPerson();
  setStatus('Saving…');
  const response = await fetch('/api/seed', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(state.document)
  });
  if (!response.ok) {
    setStatus('Save failed', true);
    return;
  }
  const payload = await response.json();
  setStatus(`Saved to ${payload.path}`);
  applyFilters();
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

document.getElementById('prev').addEventListener('click', () => {
  const pos = state.filteredIndices.indexOf(state.currentIndex);
  if (pos > 0) selectIndex(state.filteredIndices[pos - 1]);
});

document.getElementById('next').addEventListener('click', () => {
  const pos = state.filteredIndices.indexOf(state.currentIndex);
  if (pos >= 0 && pos < state.filteredIndices.length - 1) selectIndex(state.filteredIndices[pos + 1]);
});

document.getElementById('save').addEventListener('click', saveSeed);

document.getElementById('add-alias').addEventListener('click', () => {
  const value = els.newAlias.value.trim();
  if (!value) return;
  const person = currentPerson();
  person.aliases = [...new Set([...(person.aliases || []), value])];
  els.newAlias.value = '';
  renderAliases(person.aliases);
});

els.twitter.addEventListener('input', () => {
  updateInlineProfileLink(els.twitterProfileLink, 'x', els.twitter.value);
});

els.bluesky.addEventListener('input', () => {
  updateInlineProfileLink(els.blueskyProfileLink, 'bsky', els.bluesky.value);
});

els.name.addEventListener('input', () => {
  if (state.syncingSort) return;
  const name = els.name.value;
  if (els.useFullName.checked) {
    state.syncingSort = true;
    els.sortName.value = name;
    state.syncingSort = false;
  } else if (!state.sortNameManuallyEdited) {
    state.syncingSort = true;
    const guess = guessSortName(name);
    els.sortName.value = guess;
    const orgGuess = !!guess && looksLikeOrganization(name) && guess === name.trim();
    if (els.useFullName.checked !== orgGuess) {
      els.useFullName.checked = orgGuess;
    }
    state.syncingSort = false;
  }
  updateSortsAsHint();
});

els.sortName.addEventListener('input', () => {
  if (state.syncingSort) return;
  state.sortNameManuallyEdited = true;
  const name = els.name.value.trim();
  const sortName = els.sortName.value.trim();
  const isOrg = !!sortName && sortName === name;
  if (els.useFullName.checked !== isOrg) {
    state.syncingSort = true;
    els.useFullName.checked = isOrg;
    state.syncingSort = false;
  }
  updateSortsAsHint();
});

els.useFullName.addEventListener('change', () => {
  if (state.syncingSort) return;
  state.syncingSort = true;
  state.sortNameManuallyEdited = false;
  if (els.useFullName.checked) {
    els.sortName.value = els.name.value;
  } else {
    els.sortName.value = guessSortName(els.name.value);
  }
  state.syncingSort = false;
  updateSortsAsHint();
});

els.search.addEventListener('input', applyFilters);
els.filterAliases.addEventListener('change', applyFilters);
els.filterMissingSort.addEventListener('change', applyFilters);

loadSeed().catch(err => {
  console.error(err);
  setStatus(err.message, true);
});
