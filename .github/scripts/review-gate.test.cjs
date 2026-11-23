'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const g = require('./review-gate.cjs');

const SHA = 'a'.repeat(40);
const OLD = 'b'.repeat(40);
const ok = (sha = SHA) => `Summary: acceptable\n<!-- review-gate: acceptable sha=${sha} -->`;
const no = (sha = SHA) => `<!-- review-gate: changes-requested sha=${sha} -->`;
const quote = (s) => s.split('\n').map((l) => `> ${l}`).join('\n');
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
  assert.equal(g.prNumberFromContext({ eventName: 'pull_request_review', payload: { pull_request: { number: 6 } } }), null);
  assert.equal(g.prNumberFromContext({ eventName: 'issue_comment', payload: { issue: { number: 7, pull_request: {} } } }), 7);
  assert.equal(g.prNumberFromContext({ eventName: 'issue_comment', payload: { issue: { number: 8 } } }), null);
});

function fakeGithub({ labels = [], comments = [], state = 'open', removeStatus } = {}) {
  const calls = { statuses: [], removed: [] };
  const github = {
    rest: {
      pulls: {
        get: async () => ({ data: { state, head: { sha: SHA }, labels: labels.map((name) => ({ name })), html_url: 'u' } }),
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
    paginate: async (fn) => {
      assert.equal(fn, 'listComments');
      return comments;
    },
  };
  return { github, calls };
}
const core = { info() {} };
const ctx = (eventName, action, payload) => ({ eventName, payload: { action, ...payload }, repo: { owner: 'o', repo: 'r' }, runId: 1 });

const c = (body, created_at = '2026-11-21T18:00:00Z', author_association = 'OWNER', updated_at = created_at) =>
  ({ body, created_at, updated_at, author_association });

test('run: posts success status on head sha from an issue comment', async () => {
  const { github, calls } = fakeGithub({ labels: ['review-acceptable'], comments: [c(ok())] });
  const r = await g.run({ github, core, context: ctx('issue_comment', 'created', { issue: { number: 1, pull_request: {} } }) });
  assert.equal(r.state, 'success');
  assert.equal(calls.statuses[0].sha, SHA);
  assert.equal(calls.statuses[0].context, 'review-gate');
  assert.deepEqual(calls.removed, []);
});

test('run: pull_request_review events are ignored (review bodies never count)', async () => {
  const { github, calls } = fakeGithub({ labels: ['review-acceptable'], comments: [c(ok())] });
  const r = await g.run({ github, core, context: ctx('pull_request_review', 'submitted', { pull_request: { number: 1 } }) });
  assert.equal(r, null);
  assert.equal(calls.statuses.length, 0);
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

test('run: 403 on label removal still publishes a non-success status (synchronize)', async () => {
  const { github, calls } = fakeGithub({
    labels: ['review-acceptable'], comments: [c(ok())], removeStatus: 403,
  });
  const warnings = [];
  const coreWarn = { info() {}, warning(m) { warnings.push(m); } };
  const r = await g.run({
    github, core: coreWarn,
    context: ctx('pull_request_target', 'synchronize', { pull_request: { number: 1 } }),
  });
  assert.notEqual(r.state, 'success');
  assert.equal(r.state, 'failure');
  assert.equal(calls.statuses.length, 1);
  assert.equal(calls.statuses[0].state, 'failure');
  assert.equal(calls.statuses[0].sha, SHA);
  assert.equal(calls.statuses[0].context, 'review-gate');
  assert.equal(warnings.length, 1);
  assert.match(warnings[0], /403/);
});

test('run: 403 on stale-label removal still publishes a non-success status', async () => {
  const { github, calls } = fakeGithub({
    labels: ['review-acceptable'], comments: [c(ok(OLD))], removeStatus: 403,
  });
  const warnings = [];
  const coreWarn = { info() {}, warning(m) { warnings.push(m); } };
  const r = await g.run({
    github, core: coreWarn,
    context: ctx('issue_comment', 'created', { issue: { number: 1, pull_request: {} } }),
  });
  assert.notEqual(r.state, 'success');
  assert.equal(r.state, 'failure');
  assert.equal(calls.statuses.length, 1);
  assert.equal(calls.statuses[0].state, 'failure');
  assert.equal(calls.statuses[0].sha, SHA);
  assert.match(r.description, /older commit/);
  assert.equal(warnings.length, 1);
  assert.match(warnings[0], /403/);
});

test('run: non-PR issue comment and closed PR post nothing', async () => {
  const a = fakeGithub();
  assert.equal(await g.run({ github: a.github, core, context: ctx('issue_comment', 'created', { issue: { number: 3 } }) }), null);
  const b = fakeGithub({ state: 'closed' });
  assert.equal(await g.run({ github: b.github, core, context: ctx('issue_comment', 'created', { issue: { number: 3, pull_request: {} } }) }), null);
  assert.equal(a.calls.statuses.length + b.calls.statuses.length, 0);
});

test('comments are ordered by created_at: editing an older acceptable does not override a newer changes-requested', async () => {
  const { github } = fakeGithub({ labels: ['review-acceptable'], comments: [
    c(ok(), '2026-11-21T18:00:00Z', 'OWNER', '2026-11-21T20:00:00Z'),
    c(no(), '2026-11-21T19:00:00Z'),
  ] });
  const r = await g.run({ github, core, context: ctx('issue_comment', 'edited', { issue: { number: 1, pull_request: {} } }) });
  assert.equal(r.state, 'failure');
  assert.match(r.description, /requested changes/);
  assert.deepEqual(g.commentEntries([c('x', 'C', 'OWNER', 'U')]).map((e) => e.at), ['C']);
});

test('multiple markers in one body: the last one wins', () => {
  const body = `${ok()}\n${no()}`;
  assert.equal(g.latestVerdict([e(body)], SHA).verdict, 'changes-requested');
  assert.equal(g.latestVerdict([e(`${no()}\n${ok()}`)], SHA).verdict, 'acceptable');
});

test('quoted and fenced markers are ignored', () => {
  assert.deepEqual(g.parseMarkers(quote(ok())), []);
  assert.deepEqual(g.parseMarkers(`Thanks\n>> nested\n   > <!-- review-gate: acceptable sha=${SHA} -->`), []);
  assert.deepEqual(g.parseMarkers('```\n' + ok() + '\n```'), []);
  assert.deepEqual(g.parseMarkers('~~~md\n' + ok() + '\n~~~'), []);
  assert.deepEqual(g.parseMarkers('```\nx\n```\n' + ok()), [{ verdict: 'acceptable', sha: SHA }]);
  // quoting an old acceptable after changes-requested must not flip the gate back
  const r = g.evaluate({ labels: ['review-acceptable'], headSha: SHA, entries: [
    e(ok(), '2026-11-21T18:00:00Z'), e(no(), '2026-11-21T19:00:00Z'), e(quote(ok()), '2026-11-21T20:00:00Z'),
  ] });
  assert.equal(r.state, 'failure');
});

test('inline code and indented code-block markers are ignored', () => {
  const marker = `<!-- review-gate: acceptable sha=${SHA} -->`;
  assert.deepEqual(g.parseMarkers(`the reviewer should post \`${marker}\``), []);
  assert.deepEqual(g.parseMarkers('see ``' + marker + '``'), []);
  assert.deepEqual(g.parseMarkers('see `` `' + marker + '` ``'), []);
  assert.deepEqual(g.parseMarkers(`example:\n\n    ${marker}`), []);
  assert.deepEqual(g.parseMarkers('example:\n\n\t' + marker), []);
  // a real marker after a code span still counts
  assert.deepEqual(g.parseMarkers('see `' + marker + '`\n' + ok()), [{ verdict: 'acceptable', sha: SHA }]);
  // mid-paragraph indent (no blank line) is not a code block, so it still counts
  assert.deepEqual(g.parseMarkers(`note:\n    ${marker}`), [{ verdict: 'acceptable', sha: SHA }]);
  // talking about a marker in backticks after changes-requested must not flip the gate
  const r = g.evaluate({ labels: ['review-acceptable'], headSha: SHA, entries: [
    e(ok(), '2026-11-21T18:00:00Z'), e(no(), '2026-11-21T19:00:00Z'),
    e(`the reviewer should post \`${marker}\``, '2026-11-21T20:00:00Z'),
  ] });
  assert.equal(r.state, 'failure');
  const indented = g.evaluate({ labels: ['review-acceptable'], headSha: SHA, entries: [
    e(ok(), '2026-11-21T18:00:00Z'), e(no(), '2026-11-21T19:00:00Z'),
    e(`example:\n\n    ${marker}`, '2026-11-21T20:00:00Z'),
  ] });
  assert.equal(indented.state, 'failure');
});

test('shouldRemoveLabel: push, or label without acceptable marker for head; never when absent', () => {
  const L = ['review-acceptable'];
  assert.equal(g.shouldRemoveLabel({ labels: [], headSha: SHA, entries: [], pushed: true }), false);
  assert.equal(g.shouldRemoveLabel({ labels: L, headSha: SHA, entries: [e(ok())], pushed: true }), true);
  assert.equal(g.shouldRemoveLabel({ labels: L, headSha: SHA, entries: [e(ok())] }), false);
  assert.equal(g.shouldRemoveLabel({ labels: L, headSha: SHA, entries: [e(ok(OLD))] }), true);
  assert.equal(g.shouldRemoveLabel({ labels: L, headSha: SHA, entries: [e(ok()), e(no(), '2026-11-21T19:00:00Z')] }), true);
  assert.equal(g.shouldRemoveLabel({ labels: L, headSha: SHA, entries: [] }), true);
});

test('run: a dropped push run self-heals: a later comment run removes the stale label', async () => {
  const { github, calls } = fakeGithub({ labels: ['review-acceptable'], comments: [c(ok(OLD))] });
  const r = await g.run({ github, core, context: ctx('issue_comment', 'edited', { issue: { number: 1, pull_request: {} } }) });
  assert.deepEqual(calls.removed, ['review-acceptable']);
  assert.equal(r.state, 'failure');
});

// Static guards on the workflow so the P1/P2 fixes can't regress.
const workflow = fs.readFileSync(path.join(__dirname, '..', 'workflows', 'review-gate.yml'), 'utf8');
const code = workflow.split('\n').filter((l) => !/^\s*#/.test(l)).join('\n');

test('workflow: only pull_request_target + issue_comment triggers; no pull_request_review / pull_request', () => {
  const on = code.slice(code.indexOf('\non:'), code.indexOf('\npermissions:'));
  assert.match(on, /^  pull_request_target:/m);
  assert.match(on, /^  issue_comment:/m);
  assert.doesNotMatch(on, /pull_request_review/);
  assert.doesNotMatch(on, /^  pull_request:/m);
});

test('workflow: job id and name differ from the review-gate status context', () => {
  const jobs = code.slice(code.indexOf('\njobs:'));
  const ids = [...jobs.matchAll(/^  ([A-Za-z0-9_-]+):\s*$/gm)].map((m) => m[1]);
  assert.ok(ids.length > 0);
  for (const id of ids) assert.notEqual(id, g.STATUS_CONTEXT);
  for (const m of jobs.matchAll(/^    name:\s*(.+)$/gm)) assert.notEqual(m[1].trim(), g.STATUS_CONTEXT);
});

test('workflow: least privilege and SHA-pinned actions', () => {
  assert.match(code, /^  pull-requests: write$/m);
  assert.match(code, /^  statuses: write$/m);
  const uses = [...code.matchAll(/^\s*(?:-\s+)?uses:\s*(\S+)/gm)].map((m) => m[1]);
  assert.ok(uses.length >= 2);
  for (const u of uses) assert.match(u, /@[0-9a-f]{40}$/, u);
});

test('workflow: push runs have their own concurrency group', () => {
  assert.match(code, /group:.*github\.event\.action == 'synchronize' && 'push' \|\| 'eval'/);
});
