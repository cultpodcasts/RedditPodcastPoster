'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const g = require('./review-gate.cjs');

const SHA = 'a'.repeat(40);
const OLD = 'b'.repeat(40);
const ok = (sha = SHA) => `Summary: acceptable\n<!-- review-gate: acceptable sha=${sha} -->`;
const no = (sha = SHA) => `<!-- review-gate: changes-requested sha=${sha} -->`;
const e = (body, at = '2026-11-21T18:00:00Z', association = 'OWNER') => ({ body, at, association });

test('parseMarkers: finds verdict and full sha, ignores short sha and junk', () => {
  assert.deepEqual(g.parseMarkers(ok()), [{ verdict: 'acceptable', sha: SHA }]);
  assert.deepEqual(g.parseMarkers('<!-- review-gate: acceptable sha=abc1234 -->'), []);
  assert.deepEqual(g.parseMarkers('review-gate: acceptable sha=' + SHA), []);
  assert.deepEqual(g.parseMarkers(null), []);
  assert.deepEqual(g.parseMarkers(`<!--review-gate:ACCEPTABLE sha=${SHA.toUpperCase()}-->`), [{ verdict: 'acceptable', sha: SHA }]);
});

test('passes with label + acceptable marker for head sha', () => {
  const r = g.evaluate({ labels: ['review-acceptable'], headSha: SHA, entries: [e(ok())] });
  assert.equal(r.state, 'success');
});

test('fails without label even when marker is present', () => {
  const r = g.evaluate({ labels: [], headSha: SHA, entries: [e(ok())] });
  assert.equal(r.state, 'failure');
  assert.match(r.description, /label/);
});

test('fails with label but no marker', () => {
  const r = g.evaluate({ labels: ['review-acceptable'], headSha: SHA, entries: [] });
  assert.equal(r.state, 'failure');
  assert.match(r.description, /Awaiting/);
});

test('marker for an older commit does not pass', () => {
  const r = g.evaluate({ labels: ['review-acceptable'], headSha: SHA, entries: [e(ok(OLD))] });
  assert.equal(r.state, 'failure');
  assert.match(r.description, /older commit/);
});

test('untrusted author markers are ignored', () => {
  for (const association of ['NONE', 'CONTRIBUTOR', 'FIRST_TIME_CONTRIBUTOR', null]) {
    const r = g.evaluate({ labels: ['review-acceptable'], headSha: SHA, entries: [e(ok(), undefined, association)] });
    assert.equal(r.state, 'failure', String(association));
  }
  const member = g.evaluate({ labels: ['review-acceptable'], headSha: SHA, entries: [e(ok(), undefined, 'COLLABORATOR')] });
  assert.equal(member.state, 'success');
});

test('latest marker for the head sha wins', () => {
  const later = g.evaluate({ labels: ['review-acceptable'], headSha: SHA,
    entries: [e(no(), '2026-11-21T19:00:00Z'), e(ok(), '2026-11-21T18:00:00Z')] });
  assert.equal(later.state, 'failure');
  assert.match(later.description, /requested changes/);
  const reAccepted = g.evaluate({ labels: ['review-acceptable'], headSha: SHA,
    entries: [e(no(), '2026-11-21T18:00:00Z'), e(ok(), '2026-11-21T19:00:00Z')] });
  assert.equal(reAccepted.state, 'success');
});

test('a new push always fails, even with label and marker', () => {
  const r = g.evaluate({ labels: ['review-acceptable'], headSha: SHA, entries: [e(ok())], pushed: true });
  assert.equal(r.state, 'failure');
  assert.match(r.description, /New commits/);
});

test('descriptions fit the 140 char status limit', () => {
  for (const args of [
    { labels: [], headSha: SHA, entries: [] },
    { labels: [], headSha: SHA, entries: [], pushed: true },
    { labels: [], headSha: SHA, entries: [e(ok(OLD))] },
  ]) assert.ok(g.evaluate(args).description.length <= 140);
});

test('prNumberFromContext: PR events and PR comments only', () => {
  assert.equal(g.prNumberFromContext({ eventName: 'pull_request_target', payload: { pull_request: { number: 5 } } }), 5);
  assert.equal(g.prNumberFromContext({ eventName: 'pull_request_review', payload: { pull_request: { number: 6 } } }), 6);
  assert.equal(g.prNumberFromContext({ eventName: 'issue_comment', payload: { issue: { number: 7, pull_request: {} } } }), 7);
  assert.equal(g.prNumberFromContext({ eventName: 'issue_comment', payload: { issue: { number: 8 } } }), null);
});

function fakeGithub({ labels = [], comments = [], reviews = [], state = 'open', removeStatus } = {}) {
  const calls = { statuses: [], removed: [] };
  const github = {
    rest: {
      pulls: {
        get: async () => ({ data: { state, head: { sha: SHA }, labels: labels.map((name) => ({ name })), html_url: 'u' } }),
        listReviews: 'listReviews',
      },
      issues: {
        listComments: 'listComments',
        removeLabel: async (a) => {
          if (removeStatus) { const err = new Error('x'); err.status = removeStatus; throw err; }
          calls.removed.push(a.name);
        },
      },
      repos: { createCommitStatus: async (a) => { calls.statuses.push(a); } },
    },
    paginate: async (fn) => (fn === 'listComments' ? comments : reviews),
  };
  return { github, calls };
}
const core = { info() {} };
const ctx = (eventName, action, payload) => ({ eventName, payload: { action, ...payload }, repo: { owner: 'o', repo: 'r' }, runId: 1 });

test('run: posts success status on head sha from a review body', async () => {
  const { github, calls } = fakeGithub({ labels: ['review-acceptable'],
    reviews: [{ state: 'COMMENTED', submitted_at: '2026-11-21T18:00:00Z', author_association: 'OWNER', body: ok() }] });
  const r = await g.run({ github, core, context: ctx('pull_request_review', 'submitted', { pull_request: { number: 1 } }) });
  assert.equal(r.state, 'success');
  assert.equal(calls.statuses[0].sha, SHA);
  assert.equal(calls.statuses[0].context, 'review-gate');
});

test('run: dismissed reviews do not count', async () => {
  const { github } = fakeGithub({ labels: ['review-acceptable'],
    reviews: [{ state: 'DISMISSED', submitted_at: '2026-11-21T18:00:00Z', author_association: 'OWNER', body: ok() }] });
  const r = await g.run({ github, core, context: ctx('pull_request_review', 'dismissed', { pull_request: { number: 1 } }) });
  assert.equal(r.state, 'failure');
});

test('run: synchronize removes the label and fails', async () => {
  const { github, calls } = fakeGithub({ labels: ['review-acceptable'],
    comments: [{ created_at: '2026-11-21T18:00:00Z', author_association: 'OWNER', body: ok() }] });
  const r = await g.run({ github, core, context: ctx('pull_request_target', 'synchronize', { pull_request: { number: 1 } }) });
  assert.deepEqual(calls.removed, ['review-acceptable']);
  assert.equal(r.state, 'failure');
  assert.equal(calls.statuses[0].state, 'failure');
});

test('run: label already gone (404) on synchronize is fine', async () => {
  const { github, calls } = fakeGithub({ labels: ['review-acceptable'], removeStatus: 404 });
  const r = await g.run({ github, core, context: ctx('pull_request_target', 'synchronize', { pull_request: { number: 1 } }) });
  assert.equal(r.state, 'failure');
  assert.equal(calls.statuses.length, 1);
});

test('run: non-PR issue comment and closed PR post nothing', async () => {
  const a = fakeGithub();
  assert.equal(await g.run({ github: a.github, core, context: ctx('issue_comment', 'created', { issue: { number: 3 } }) }), null);
  const b = fakeGithub({ state: 'closed' });
  assert.equal(await g.run({ github: b.github, core, context: ctx('issue_comment', 'created', { issue: { number: 3, pull_request: {} } }) }), null);
  assert.equal(a.calls.statuses.length + b.calls.statuses.length, 0);
});
