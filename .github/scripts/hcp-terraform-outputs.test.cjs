'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const h = require('./hcp-terraform-outputs.cjs');

const SECRET = 'not-a-real-secret-value';
const WANTED = [
  { name: 'client_id', env: 'SEARCH_INDEX_WRITER_CLIENT_ID' },
  { name: 'client_secret', env: 'SEARCH_INDEX_WRITER_CLIENT_SECRET' },
];

// Fake HCP API: list hides sensitive values (as HCP does); the single-output endpoint returns them.
function fakeHcp({ outputs = null, status = {} } = {}) {
  const calls = [];
  const list = outputs ?? [
    { id: 'wsout-id', attributes: { name: 'client_id', sensitive: false, value: 'writer-client-id' } },
    { id: 'wsout-secret', attributes: { name: 'client_secret', sensitive: true, value: null } },
  ];
  const fetchImpl = async (url, init) => {
    calls.push({ url, auth: init.headers.Authorization });
    const path = new URL(url).pathname;
    const body = (data) => ({ ok: !(path in status), status: status[path] ?? 200, json: async () => data });
    if (path === '/api/v2/organizations/cultpodcasts/workspaces/cultpodcasts-auth0') return body({ data: { id: 'ws-1' } });
    if (path === '/api/v2/workspaces/ws-1/current-state-version-outputs') return body({ data: list });
    if (path === '/api/v2/state-version-outputs/wsout-secret') {
      return body({ data: { attributes: { name: 'client_secret', sensitive: true, value: SECRET } } });
    }
    return { ok: false, status: 404, json: async () => ({}) };
  };
  return { fetchImpl, calls };
}

test('reads a plain output from the list and a sensitive one from its own endpoint', async () => {
  const { fetchImpl, calls } = fakeHcp();
  const values = await h.readOutputs({ org: 'cultpodcasts', workspace: 'cultpodcasts-auth0', outputs: WANTED, token: 't', fetchImpl });
  assert.deepEqual(values.map((v) => [v.env, v.value, v.sensitive]), [
    ['SEARCH_INDEX_WRITER_CLIENT_ID', 'writer-client-id', false],
    ['SEARCH_INDEX_WRITER_CLIENT_SECRET', SECRET, true],
  ]);
  assert.ok(calls.every((c) => c.auth === 'Bearer t'));
});

test('masks every sensitive value before it reaches GITHUB_ENV, and never logs a value', async () => {
  const { fetchImpl } = fakeHcp();
  const logs = [];
  let written = '';
  await h.main(
    ['--org', 'cultpodcasts', '--workspace', 'cultpodcasts-auth0',
      '--output', 'client_id=SEARCH_INDEX_WRITER_CLIENT_ID', '--output', 'client_secret=SEARCH_INDEX_WRITER_CLIENT_SECRET'],
    { TF_TOKEN: 't', GITHUB_ENV: '/tmp/env' },
    { fetchImpl, log: (l) => logs.push(l), write: (_f, text) => { written += text; } });
  assert.equal(logs[0], `::add-mask::${SECRET}`);
  assert.ok(logs.slice(1).every((l) => !l.includes(SECRET) && !l.includes('writer-client-id')));
  assert.equal(written, `SEARCH_INDEX_WRITER_CLIENT_ID=writer-client-id\nSEARCH_INDEX_WRITER_CLIENT_SECRET=${SECRET}\n`);
});

test('with no token, warns, exports nothing and succeeds so provision still runs', async () => {
  const logs = [];
  let wrote = false;
  const code = await h.main(['--org', 'o', '--workspace', 'w', '--output', 'client_id=X_ID'], {},
    { fetchImpl: async () => { throw new Error('must not call HCP'); }, log: (l) => logs.push(l), write: () => { wrote = true; } });
  assert.equal(code, 0);
  assert.equal(wrote, false);
  assert.match(logs[0], /^::warning::TF_TOKEN is not set/);
});

test('fails when a requested output is missing (stack never applied)', async () => {
  const { fetchImpl } = fakeHcp({ outputs: [{ id: 'a', attributes: { name: 'client_id', sensitive: false, value: 'x' } }] });
  await assert.rejects(
    h.readOutputs({ org: 'cultpodcasts', workspace: 'cultpodcasts-auth0', outputs: WANTED, token: 't', fetchImpl }),
    /has no output 'client_secret'/);
});

test('fails on an HCP error status without echoing the response body', async () => {
  const { fetchImpl } = fakeHcp({ status: { '/api/v2/state-version-outputs/wsout-secret': 403 } });
  await assert.rejects(
    h.readOutputs({ org: 'cultpodcasts', workspace: 'cultpodcasts-auth0', outputs: WANTED, token: 't', fetchImpl }),
    /state-version-outputs\/wsout-secret answered 403/);
});

test('rejects a multi-line value, which would inject extra GITHUB_ENV lines', async () => {
  const { fetchImpl } = fakeHcp({ outputs: [{ id: 'a', attributes: { name: 'client_id', sensitive: false, value: 'x\nEVIL=1' } }] });
  await assert.rejects(
    h.readOutputs({ org: 'cultpodcasts', workspace: 'cultpodcasts-auth0', outputs: [WANTED[0]], token: 't', fetchImpl }),
    /single-line/);
});

test('rejects an --output without a valid ENV name', () => {
  assert.throws(() => h.parseArgs(['--org', 'o', '--workspace', 'w', '--output', 'client_id=lower']), /--output must be/);
});
