const state = {
  document: null,
  filteredIndices: [],
  currentIndex: -1,
  refreshData: null,
  episodeUrls: {}
};

const BSKY_PLACEHOLDER = 'user.bsky.social';

const els = {
  seedMeta: document.getElementById('seed-meta'),
  search: document.getElementById('search'),
  filterAliases: document.getElementById('filter-aliases'),
  peopleList: document.getElementById('people-list'),
  editor: document.getElementById('editor'),
  position: document.getElementById('position'),
  status: document.getElementById('status'),
  name: document.getElementById('name'),
  twitter: document.getElementById('twitter'),
  bluesky: document.getElementById('bluesky'),
  notes: document.getElementById('notes'),
  aliases: document.getElementById('aliases'),
  newAlias: document.getElementById('new-alias'),
  episodes: document.getElementById('episodes'),
  episodeCount: document.getElementById('episode-count'),
  twitterProfileLink: document.getElementById('twitter-profile-link'),
  blueskyProfileLink: document.getElementById('bluesky-profile-link'),
  refreshPanel: document.getElementById('refresh-panel'),
  refreshResults: document.getElementById('refresh-results')
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

function applyFilters() {
  const query = els.search.value.trim().toLowerCase();
  const aliasesOnly = els.filterAliases.checked;
  state.filteredIndices = state.document.people
    .map((person, index) => ({ person, index }))
    .filter(({ person }) => {
      if (aliasesOnly && (!person.aliases || person.aliases.length === 0)) return false;
      if (!query) return true;
      const haystack = [
        person.name,
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
    button.innerHTML = `${escapeHtml(person.name)}<span class="meta">${escapeHtml(person.twitterHandle || person.blueskyHandle || 'no handle')}</span>`;
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

async function swapAliasWithCanonical(alias) {
  readEditorIntoPerson();
  const person = currentPerson();
  if (!person) return;

  setStatus('Swapping…');
  const response = await fetch('/api/swap-canonical', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      canonicalName: person.name,
      alias,
      aliases: person.aliases || [],
      twitterHandle: person.twitterHandle,
      blueskyHandle: person.blueskyHandle
    })
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => ({}));
    setStatus(payload.error || 'Swap failed', true);
    return;
  }

  const result = await response.json();
  person.name = result.name;
  person.aliases = result.aliases?.length ? result.aliases : [];
  els.name.value = person.name;
  renderAliases(person.aliases);
  renderList();
  setStatus('Swapped canonical (not saved yet)');
}

function renderEpisodes(episodeIds) {
  els.episodes.innerHTML = '';
  for (const episodeId of episodeIds) {
    const li = document.createElement('li');
    const url = state.episodeUrls[episodeId];
    if (url) {
      const link = document.createElement('a');
      link.href = url;
      link.target = '_blank';
      link.rel = 'noopener noreferrer';
      link.textContent = episodeId;
      li.appendChild(link);
    } else {
      const span = document.createElement('span');
      span.textContent = episodeId;
      span.className = 'episode-id-unlinked';
      li.appendChild(span);
    }
    els.episodes.appendChild(li);
  }
}

function renderEditor() {
  const person = currentPerson();
  if (!person) return;

  els.editor.hidden = false;
  const filteredPos = state.filteredIndices.indexOf(state.currentIndex);
  els.position.textContent = `${filteredPos + 1} / ${state.filteredIndices.length} (record #${state.currentIndex + 1})`;

  els.name.value = person.name || '';
  els.twitter.value = person.twitterHandle || '';
  els.bluesky.value = person.blueskyHandle || '';
  els.notes.value = person.notes || '';
  renderAliases(person.aliases || []);

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
  person.twitterHandle = els.twitter.value.trim() || null;
  person.blueskyHandle = els.bluesky.value.trim() || null;
  person.notes = els.notes.value.trim() || null;
}

function selectIndex(index) {
  state.currentIndex = index;
  state.refreshData = null;
  els.refreshPanel.hidden = true;
  renderEditor();
  setStatus('');
}

async function loadEpisodeUrls(people) {
  const ids = [...new Set((people || []).flatMap(person => person.sourceEpisodeIds || []))];
  if (ids.length === 0) {
    state.episodeUrls = {};
    return;
  }

  const response = await fetch('/api/episode-urls', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ids })
  });
  if (!response.ok) {
    throw new Error('Failed to load episode URLs');
  }
  state.episodeUrls = await response.json();
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
  await loadEpisodeUrls(state.document.people);
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

async function refreshProfile() {
  readEditorIntoPerson();
  setStatus('Fetching profile names…');
  const response = await fetch(`/api/people/${state.currentIndex}/refresh-profile`, { method: 'POST' });
  if (!response.ok) {
    setStatus('Profile refresh failed', true);
    return;
  }
  state.refreshData = await response.json();
  els.refreshResults.innerHTML = '';
  const entries = [
    ['Chosen', state.refreshData.chosenName, state.refreshData.chosenSource],
    ['X / Twitter', state.refreshData.twitterName, 'twitter'],
    ['Bluesky', state.refreshData.blueskyName, 'bluesky']
  ];
  for (const [label, value, source] of entries) {
    if (!value) continue;
    const li = document.createElement('li');
    li.textContent = `${label}: ${value}${source ? ` (${source})` : ''}`;
    els.refreshResults.appendChild(li);
  }
  els.refreshPanel.hidden = !state.refreshData.chosenName;
  setStatus(state.refreshData.chosenName ? 'Profile names loaded' : 'No display name found');
}

function applyRefreshAsCanonical() {
  if (!state.refreshData?.chosenName) return;
  els.name.value = state.refreshData.chosenName;
  readEditorIntoPerson();
  els.refreshPanel.hidden = true;
  setStatus('Applied as canonical (not saved yet)');
}

function applyRefreshAsAlias() {
  if (!state.refreshData?.chosenName) return;
  const person = currentPerson();
  const alias = state.refreshData.chosenName.trim();
  person.aliases = [...new Set([...(person.aliases || []), alias])];
  renderAliases(person.aliases);
  els.refreshPanel.hidden = true;
  setStatus('Added alias (not saved yet)');
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
document.getElementById('refresh-profile').addEventListener('click', refreshProfile);
document.getElementById('apply-canonical').addEventListener('click', applyRefreshAsCanonical);
document.getElementById('apply-alias').addEventListener('click', applyRefreshAsAlias);
document.getElementById('dismiss-refresh').addEventListener('click', () => {
  els.refreshPanel.hidden = true;
});

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

els.search.addEventListener('input', applyFilters);
els.filterAliases.addEventListener('change', applyFilters);

loadSeed().catch(err => {
  console.error(err);
  setStatus(err.message, true);
});
