'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const g = require('./review-gate.cjs');
const { createRepo } = require('./git-graph-fake.cjs');

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

test('a new push fails, even with label and marker, unless a merge-from-base carry is proven', () => {
  const r = g.evaluate({ labels: ['review-acceptable'], headSha: SHA, entries: [e(ok())], pushed: true });
  assert.equal(r.state, 'failure');
  assert.match(r.description, /New commits/);
  const carried = g.evaluate({ labels: ['review-acceptable'], headSha: SHA, entries: [], pushed: true,
    carry: { ok: true, anchor: OLD, hops: [{ sha: SHA }] } });
  assert.equal(carried.state, 'success');
});

test('descriptions fit the 140 char status limit', () => {
  const longReason = 'x'.repeat(300);
  for (const args of [
    { labels: [], headSha: SHA, entries: [] },
    { labels: [], headSha: SHA, entries: [], pushed: true },
    { labels: [], headSha: SHA, entries: [e(ok(OLD))] },
    { labels: [], headSha: SHA, entries: [], pushed: true, carry: { ok: false, reason: longReason, hops: [] } },
    { labels: ['review-acceptable'], headSha: SHA, entries: [], carry: { ok: true, anchor: OLD, hops: new Array(10).fill({}) } },
    { labels: [], headSha: SHA, entries: [], carry: { ok: true, anchor: OLD, hops: [{}] } },
    { labels: [], headSha: SHA, entries: [], carry: { ok: false, apiError: true, reason: longReason, hops: [] } },
  ]) assert.ok(g.evaluate(args).description.length <= 140);
});

const LABELLED = ['review-acceptable'];
const carryOk = (hops = 1) => ({ ok: true, anchor: OLD, hops: new Array(hops).fill({ sha: SHA }) });

test('carry: acceptable carried across merge-from-base commits passes with the label', () => {
  const one = g.evaluate({ labels: LABELLED, headSha: SHA, entries: [e(ok(OLD))], carry: carryOk(1) });
  assert.equal(one.state, 'success');
  assert.match(one.description, /Carried acceptable from bbbbbbb across 1 merge-from-base commit$/);
  const three = g.evaluate({ labels: LABELLED, headSha: SHA, entries: [e(ok(OLD))], carry: carryOk(3) });
  assert.match(three.description, /across 3 merge-from-base commits$/);
});

test('carry: a carried acceptance still needs the review-acceptable label', () => {
  const r = g.evaluate({ labels: [], headSha: SHA, entries: [e(ok(OLD))], carry: carryOk() });
  assert.equal(r.state, 'failure');
  assert.match(r.description, /label/);
});

test('carry: an unverifiable carry (API error) fails the status', () => {
  const r = g.evaluate({ labels: LABELLED, headSha: SHA, entries: [e(ok(OLD))], pushed: true,
    carry: { ok: false, apiError: true, reason: 'boom', hops: [] } });
  assert.equal(r.state, 'failure');
  assert.match(r.description, /API error/);
});

test('carry: a failed carry on a push reports why, and never passes', () => {
  const r = g.evaluate({ labels: LABELLED, headSha: SHA, entries: [e(ok(OLD))], pushed: true,
    carry: { ok: false, reason: 'not a two-parent merge commit', hops: [] } });
  assert.equal(r.state, 'failure');
  assert.match(r.description, /not carried: not a two-parent merge commit/);
});

test('carry: a failed carry on a non-push run keeps the older-commit failure', () => {
  const r = g.evaluate({ labels: LABELLED, headSha: SHA, entries: [e(ok(OLD))],
    carry: { ok: false, reason: 'tip diverged from base on a main-touched file', hops: [] } });
  assert.equal(r.state, 'failure');
  assert.match(r.description, /older commit/);
});

test('latestVerdictsBySha: latest trusted verdict per sha; untrusted ignored', () => {
  const m = g.latestVerdictsBySha([
    e(ok(OLD), '2026-11-21T18:00:00Z'),
    e(no(OLD), '2026-11-21T19:00:00Z'),
    e(ok(SHA), '2026-11-21T18:00:00Z'),
    e(no(SHA), '2026-11-21T19:00:00Z', 'NONE'),
  ]);
  assert.equal(m.get(OLD), 'changes-requested');
  assert.equal(m.get(SHA), 'acceptable');
  assert.equal(m.size, 2);
});

test('resolveCarry: only attempted with the label and no marker of its own for the head', async () => {
  let calls = 0;
  const walk = async () => { calls++; return carryOk(); };
  const args = { github: {}, owner: 'o', repo: 'r', baseRef: 'main', headSha: SHA, walk };
  assert.equal(await g.resolveCarry({ ...args, labels: [], entries: [e(ok(OLD))] }), null);
  assert.equal(await g.resolveCarry({ ...args, labels: LABELLED, entries: [e(ok())] }), null);
  assert.equal(await g.resolveCarry({ ...args, labels: LABELLED, entries: [e(no())] }), null);
  assert.equal(calls, 0);
  assert.equal((await g.resolveCarry({ ...args, labels: LABELLED, entries: [e(ok(OLD))] })).ok, true);
  assert.equal(calls, 1);
});

test('resolveCarry: acceptable markers stop the walk, changes-requested markers block it', async () => {
  let classify;
  const walk = async (a) => { classify = a.classify; return carryOk(); };
  const entries = [e(ok(OLD), '2026-11-21T18:00:00Z'), e(no('c'.repeat(40)), '2026-11-21T18:00:00Z'),
    e(ok('d'.repeat(40)), '2026-11-21T18:00:00Z'), e(no('d'.repeat(40)), '2026-11-21T19:00:00Z')];
  await g.resolveCarry({ github: {}, owner: 'o', repo: 'r', baseRef: 'main', headSha: SHA, labels: LABELLED, entries, walk });
  assert.equal(classify(OLD), 'stop');
  assert.equal(classify(OLD.toUpperCase()), 'stop');
  assert.equal(classify('c'.repeat(40)), 'block');
  assert.equal(classify('d'.repeat(40)), 'block');
  assert.equal(classify('e'.repeat(40)), null);
});

test('resolveCarry: untrusted acceptable markers never anchor a carry', async () => {
  let classify;
  const walk = async (a) => { classify = a.classify; return { ok: false, reason: 'x', hops: [] }; };
  await g.resolveCarry({ github: {}, owner: 'o', repo: 'r', baseRef: 'main', headSha: SHA, labels: LABELLED,
    entries: [e(ok(OLD), undefined, 'CONTRIBUTOR')], walk });
  assert.equal(classify(OLD), null);
});

test('shouldRemoveLabel: a carried or unverifiable push keeps the label; a failed carry removes it', () => {
  const push = { labels: LABELLED, headSha: SHA, entries: [e(ok(OLD))], pushed: true };
  assert.equal(g.shouldRemoveLabel({ ...push, carry: carryOk() }), false);
  assert.equal(g.shouldRemoveLabel({ ...push, carry: { ok: false, apiError: true, reason: 'x', hops: [] } }), false);
  assert.equal(g.shouldRemoveLabel({ ...push, carry: { ok: false, reason: 'x', hops: [] } }), true);
  assert.equal(g.shouldRemoveLabel({ ...push, carry: null }), true);
});

test('prNumberFromContext: PR events, reviews, and PR comments only', () => {
  assert.equal(g.prNumberFromContext({ eventName: 'pull_request_target', payload: { pull_request: { number: 5 } } }), 5);
  assert.equal(g.prNumberFromContext({ eventName: 'pull_request_review', payload: { pull_request: { number: 6 } } }), 6);
  assert.equal(g.prNumberFromContext({ eventName: 'issue_comment', payload: { issue: { number: 7, pull_request: {} } } }), 7);
  assert.equal(g.prNumberFromContext({ eventName: 'issue_comment', payload: { issue: { number: 8 } } }), null);
});

function fakeGithub({ labels = [], comments = [], reviews = [], state = 'open', removeStatus, addLabelStatus, headSha = SHA, graph } = {}) {
  const calls = { statuses: [], removed: [], added: [] };
  const github = {
    rest: {
      pulls: {
        get: async () => ({ data: { state, head: { sha: headSha }, base: { ref: 'main' }, labels: labels.map((name) => ({ name })), html_url: 'u' } }),
        listReviews: 'listReviews',
      },
      // The head is a single-parent commit by default, so it is never a merge-only tip.
      git: { getCommit: async () => ({ data: { parents: [{ sha: OLD }] } }) },
      issues: {
        listComments: 'listComments',
        removeLabel: async (a) => {
          if (removeStatus) { const err = new Error('x'); err.status = removeStatus; throw err; }
          calls.removed.push(a.name);
        },
        addLabels: async (a) => {
          if (addLabelStatus) { const err = new Error('x'); err.status = addLabelStatus; throw err; }
          calls.added.push(...(a.labels || []));
        },
      },
      repos: { createCommitStatus: async (a) => { calls.statuses.push(a); } },
    },
    paginate: async (fn) => {
      if (fn === 'listComments') return comments;
      if (fn === 'listReviews') return reviews;
      assert.fail(`unexpected paginate target: ${fn}`);
    },
  };
  // A graph (git-graph-fake.cjs) replaces the single-parent default with a real commit graph, so the
  // real walker runs against GitHub-shaped getCommit / compare responses.
  if (graph) {
    github.rest.git = graph.github.rest.git;
    github.rest.repos.compareCommitsWithBasehead = graph.github.rest.repos.compareCommitsWithBasehead;
  }
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

const rev = (state, commit_id = SHA, submitted_at = '2026-11-21T18:00:00Z', author_association = 'OWNER') =>
  ({ state, commit_id, submitted_at, author_association });

test('latestReviewVerdict: Approve/Changes Requested for head only; ignores COMMENTED and other SHAs', () => {
  assert.equal(g.latestReviewVerdict([rev('APPROVED')], SHA), 'acceptable');
  assert.equal(g.latestReviewVerdict([rev('CHANGES_REQUESTED')], SHA), 'changes-requested');
  assert.equal(g.latestReviewVerdict([rev('COMMENTED')], SHA), null);
  assert.equal(g.latestReviewVerdict([rev('APPROVED', OLD)], SHA), null);
  assert.equal(g.latestReviewVerdict([
    rev('CHANGES_REQUESTED', SHA, '2026-11-21T18:00:00Z'),
    rev('APPROVED', SHA, '2026-11-21T19:00:00Z'),
  ], SHA), 'acceptable');
  assert.equal(g.latestReviewVerdict([rev('APPROVED', SHA, undefined, 'NONE')], SHA), null);
});

test('evaluate: trusted Approve for head + label passes', () => {
  const r = g.evaluate({ labels: LABELLED, headSha: SHA, entries: [], reviewVerdict: 'acceptable' });
  assert.equal(r.state, 'success');
  assert.match(r.description, /approved/);
});

test('evaluate: trusted Changes Requested for head fails even with an acceptable marker', () => {
  const r = g.evaluate({
    labels: LABELLED, headSha: SHA, entries: [e(ok())], reviewVerdict: 'changes-requested',
  });
  assert.equal(r.state, 'failure');
  assert.match(r.description, /requested changes/);
});

test('evaluate: Approve for head clears a prior changes-requested marker', () => {
  const r = g.evaluate({
    labels: LABELLED, headSha: SHA, entries: [e(no())], reviewVerdict: 'acceptable',
  });
  assert.equal(r.state, 'success');
});

test('evaluate: marker path still works when there is no review verdict', () => {
  const r = g.evaluate({ labels: LABELLED, headSha: SHA, entries: [e(ok())], reviewVerdict: null });
  assert.equal(r.state, 'success');
  assert.match(r.description, /marked/);
});

test('shouldRemoveLabel: Approve for head keeps the label; Changes Requested removes it', () => {
  assert.equal(g.shouldRemoveLabel({
    labels: LABELLED, headSha: SHA, entries: [], reviewVerdict: 'acceptable',
  }), false);
  assert.equal(g.shouldRemoveLabel({
    labels: LABELLED, headSha: SHA, entries: [e(ok())], reviewVerdict: 'changes-requested',
  }), true);
});

test('run: Approve for head auto-adds label and posts success', async () => {
  const { github, calls } = fakeGithub({
    labels: [], comments: [], reviews: [rev('APPROVED')],
  });
  const r = await g.run({
    github, core, context: ctx('pull_request_review', 'submitted', { pull_request: { number: 1 } }),
  });
  assert.deepEqual(calls.added, ['review-acceptable']);
  assert.equal(r.state, 'success');
  assert.equal(calls.statuses[0].state, 'success');
  assert.match(r.description, /approved/);
});

test('run: Approve for an older SHA is ignored; still awaiting', async () => {
  const { github, calls } = fakeGithub({
    labels: [], comments: [], reviews: [rev('APPROVED', OLD)],
  });
  const failures = [];
  const coreFail = { info() {}, setFailed(m) { failures.push(m); } };
  const r = await g.run({
    github, core: coreFail, context: ctx('pull_request_review', 'submitted', { pull_request: { number: 1 } }),
  });
  assert.deepEqual(calls.added, []);
  assert.equal(r.state, 'failure');
  assert.match(r.description, /Awaiting/);
  assert.equal(failures.length, 1);
});

test('run: Changes Requested for head fails and setFailed', async () => {
  const { github, calls } = fakeGithub({
    labels: LABELLED, comments: [], reviews: [rev('CHANGES_REQUESTED')],
  });
  const failures = [];
  const coreFail = { info() {}, setFailed(m) { failures.push(m); } };
  const r = await g.run({
    github, core: coreFail, context: ctx('pull_request_review', 'submitted', { pull_request: { number: 1 } }),
  });
  assert.deepEqual(calls.removed, ['review-acceptable']);
  assert.equal(r.state, 'failure');
  assert.equal(calls.statuses[0].state, 'failure');
  assert.equal(failures.length, 1);
  assert.match(failures[0], /requested changes/);
});

test('run: setFailed is not called on success', async () => {
  const { github } = fakeGithub({ labels: LABELLED, comments: [c(ok())] });
  const failures = [];
  const coreFail = { info() {}, setFailed(m) { failures.push(m); } };
  const r = await g.run({
    github, core: coreFail, context: ctx('issue_comment', 'created', { issue: { number: 1, pull_request: {} } }),
  });
  assert.equal(r.state, 'success');
  assert.deepEqual(failures, []);
});

test('run: setFailed is called when status is failure', async () => {
  const { github } = fakeGithub({ labels: [], comments: [] });
  const failures = [];
  const coreFail = { info() {}, setFailed(m) { failures.push(m); } };
  const r = await g.run({
    github, core: coreFail, context: ctx('issue_comment', 'created', { issue: { number: 1, pull_request: {} } }),
  });
  assert.equal(r.state, 'failure');
  assert.equal(failures.length, 1);
  assert.equal(failures[0], r.description);
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

const pushCtx = () => ctx('pull_request_target', 'synchronize', { pull_request: { number: 1 } });
const commentCtx = () => ctx('issue_comment', 'created', { issue: { number: 1, pull_request: {} } });
const walkOk = async () => carryOk(2);
const walkFail = async () => ({ ok: false, reason: 'tip diverged from base on a main-touched file', hops: [] });
const walkNever = async () => { throw new Error('walk must not run'); };

test('run: a merge-from-base push on an acceptable commit keeps the label and passes', async () => {
  const { github, calls } = fakeGithub({ labels: LABELLED, comments: [c(ok(OLD))] });
  const r = await g.run({ github, core, context: pushCtx(), walk: walkOk });
  assert.equal(r.state, 'success');
  assert.deepEqual(calls.removed, []);
  assert.equal(calls.statuses[0].state, 'success');
  assert.equal(calls.statuses[0].sha, SHA);
  assert.match(calls.statuses[0].description, /Carried acceptable from bbbbbbb across 2 merge-from-base commits/);
});

test('run: the carry walk gets the PR base ref, head sha and repo', async () => {
  const { github } = fakeGithub({ labels: LABELLED, comments: [c(ok(OLD))] });
  let seen;
  await g.run({ github, core, context: pushCtx(), walk: async (a) => { seen = a; return carryOk(); } });
  assert.equal(seen.baseRef, 'main');
  assert.equal(seen.headSha, SHA);
  assert.equal(seen.owner, 'o');
  assert.equal(seen.repo, 'r');
});

test('run: a later comment or label run on a carried head still passes (stateless)', async () => {
  const { github, calls } = fakeGithub({ labels: LABELLED, comments: [c(ok(OLD))] });
  const r = await g.run({ github, core, context: commentCtx(), walk: walkOk });
  assert.equal(r.state, 'success');
  assert.deepEqual(calls.removed, []);
});

test('run: a push that is not merge-from-base only removes the label and fails with the reason', async () => {
  const { github, calls } = fakeGithub({ labels: LABELLED, comments: [c(ok(OLD))] });
  const r = await g.run({ github, core, context: pushCtx(), walk: walkFail });
  assert.equal(r.state, 'failure');
  assert.deepEqual(calls.removed, ['review-acceptable']);
  assert.match(calls.statuses[0].description, /tip diverged from base on a main-touched file/);
});

test('run: a rebase or feature commit (single parent head, real walker) removes the label and fails', async () => {
  const { github, calls } = fakeGithub({ labels: LABELLED, comments: [c(ok(OLD))] });
  const r = await g.run({ github, core, context: pushCtx() });
  assert.equal(r.state, 'failure');
  assert.deepEqual(calls.removed, ['review-acceptable']);
  assert.match(r.description, /not a two-parent merge commit/);
});

test('run: an API error during the carry keeps the label but fails the status', async () => {
  const { github, calls } = fakeGithub({ labels: LABELLED, comments: [c(ok(OLD))] });
  github.rest.git.getCommit = async () => { throw new Error('502'); };
  const r = await g.run({ github, core, context: pushCtx() });
  assert.equal(r.state, 'failure');
  assert.deepEqual(calls.removed, []);
  assert.match(r.description, /API error/);
});

test('run: changes-requested on the head never carries', async () => {
  const { github, calls } = fakeGithub({ labels: LABELLED, comments: [c(ok(OLD)), c(no(), '2026-11-21T19:00:00Z')] });
  const r = await g.run({ github, core, context: commentCtx(), walk: walkNever });
  assert.equal(r.state, 'failure');
  assert.deepEqual(calls.removed, ['review-acceptable']);
});

test('run: no carry is attempted without the label', async () => {
  const { github } = fakeGithub({ labels: [], comments: [c(ok(OLD))] });
  const r = await g.run({ github, core, context: pushCtx(), walk: walkNever });
  assert.equal(r.state, 'failure');
});

test('run: a carried head never attempts a label removal', async () => {
  const { github, calls } = fakeGithub({ labels: LABELLED, comments: [c(ok(OLD))], removeStatus: 403 });
  const r = await g.run({ github, core, context: pushCtx(), walk: walkOk });
  assert.equal(r.state, 'success');
  assert.deepEqual(calls.removed, []);
});

// Wiring: review-gate.run + the REAL walkMergeOnlyChain + the label/status outcome, on an in-memory
// two-parent commit graph (no injected walker). Covers pr.base.ref plumbing and classify casing.
function carryGraph({ tamper } = {}) {
  const graph = createRepo();
  const root = graph.root({ 'src/a.txt': 'a0', 'docs/readme.md': 'r0' });
  const anchor = graph.commit(root, { 'src/feature.txt': 'f1' });
  const main1 = graph.commit(root, { 'src/a.txt': 'a1' });
  graph.branches.main = main1;
  const head = graph.merge(anchor, main1, tamper);
  return { graph, anchor, head };
}
const realCarry = ({ graph, anchor, head }, comments) => {
  const { github, calls } = fakeGithub({ labels: LABELLED, comments: comments || [c(ok(anchor.toUpperCase()))], headSha: head, graph });
  return { github, calls };
};

test('run (real walker): a clean merge-from-base head over an acceptable anchor keeps the label and passes', async () => {
  const scenario = carryGraph();
  const { github, calls } = realCarry(scenario);
  const r = await g.run({ github, core, context: pushCtx() });
  assert.equal(r.state, 'success');
  assert.deepEqual(calls.removed, []);
  assert.equal(calls.statuses[0].sha, scenario.head);
  assert.equal(calls.statuses[0].state, 'success');
  assert.match(calls.statuses[0].description, new RegExp(`Carried acceptable from ${scenario.anchor.slice(0, 7)} across 1 merge-from-base commit`));
});

test('run (real walker): an evil merge (extra hand-edited file) removes the label and fails', async () => {
  const scenario = carryGraph({ tamper: { 'src/sneaky.txt': 'x' } });
  const { github, calls } = realCarry(scenario);
  const r = await g.run({ github, core, context: pushCtx() });
  assert.equal(r.state, 'failure');
  assert.deepEqual(calls.removed, ['review-acceptable']);
  assert.equal(calls.statuses[0].state, 'failure');
  assert.match(calls.statuses[0].description, /differ from the base side/);
});

test('run (real walker): a changes-requested marker on the anchor blocks the carry', async () => {
  const scenario = carryGraph();
  const { github, calls } = realCarry(scenario, [c(ok(scenario.anchor)), c(no(scenario.anchor), '2026-11-21T19:00:00Z')]);
  const r = await g.run({ github, core, context: pushCtx() });
  assert.equal(r.state, 'failure');
  assert.deepEqual(calls.removed, ['review-acceptable']);
});
// Static guards on the workflow so the P1/P2 fixes can't regress.
const workflow = fs.readFileSync(path.join(__dirname, '..', 'workflows', 'review-gate.yml'), 'utf8');
const code = workflow.split('\n').filter((l) => !/^\s*#/.test(l)).join('\n');

test('workflow: pull_request_target + issue_comment + pull_request_review; no plain pull_request', () => {
  const on = code.slice(code.indexOf('\non:'), code.indexOf('\npermissions:'));
  assert.match(on, /^  pull_request_target:/m);
  assert.match(on, /^  issue_comment:/m);
  assert.match(on, /^  pull_request_review:/m);
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
