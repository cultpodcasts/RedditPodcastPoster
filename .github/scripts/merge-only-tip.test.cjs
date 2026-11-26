'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createRepo } = require('./git-graph-fake.cjs');
const m = require('./merge-only-tip.cjs');

// main: root -> m1; feature branch forks at root with one commit on a PR-only file.
function scenario() {
  const repo = createRepo();
  const root = repo.root({ 'src/a.txt': 'a0', 'src/b.txt': 'b0', 'docs/readme.md': 'r0' });
  const feature = repo.commit(root, { 'src/feature.txt': 'f1' });
  const main1 = repo.commit(root, { 'src/a.txt': 'a1' });
  repo.branches.main = main1;
  return { repo, root, feature, main1 };
}
const hopArgs = (repo, sha) => ({ github: repo.github, owner: 'o', repo: 'r', baseRef: 'main', sha });

test('touchedPaths / filesOverlap: renames count under both names; disjoint sets do not overlap', () => {
  const pr = [{ filename: 'a.txt' }, { filename: 'new.txt', previous_filename: 'old.txt' }];
  assert.deepEqual([...m.touchedPaths(pr)].sort(), ['a.txt', 'new.txt', 'old.txt']);
  assert.equal(m.filesOverlap(pr, [{ filename: 'b.txt' }]), false);
  assert.equal(m.filesOverlap(pr, [{ filename: 'a.txt' }]), true);
  assert.equal(m.filesOverlap(pr, [{ filename: 'x.txt', previous_filename: 'old.txt' }]), true);
  assert.equal(m.filesOverlap([], []), false);
  assert.equal(m.filesOverlap(null, undefined), false);
});

test('deltaMatchesBaseSide: order independent; any difference in file, status or blob fails', () => {
  const x = { filename: 'x', status: 'modified', sha: '1' };
  const y = { filename: 'y', status: 'added', sha: '2' };
  assert.equal(m.deltaMatchesBaseSide([x, y], [y, x]), true);
  assert.equal(m.deltaMatchesBaseSide([], []), true);
  assert.equal(m.deltaMatchesBaseSide([x], [x, y]), false);
  assert.equal(m.deltaMatchesBaseSide([x, y], [x]), false);
  assert.equal(m.deltaMatchesBaseSide([{ ...x, sha: '9' }], [x]), false);
  assert.equal(m.deltaMatchesBaseSide([{ ...x, status: 'removed' }], [x]), false);
  assert.equal(m.deltaMatchesBaseSide([{ ...x, previous_filename: 'w' }], [x]), false);
});

// A GitHub compare returns at most 300 files, on page 1 only (page 2+ come back empty), whatever
// per_page says. These doubles mirror that, so a clipped listing looks exactly like the real one.
const githubReturning = (files, extra = {}) => ({
  rest: { repos: { compareCommitsWithBasehead: async ({ page = 1 }) => ({ data: { files: page === 1 ? files.slice(0, 300) : [], ...extra } }) } },
});
const nFiles = (n) => Array.from({ length: n }, (_, i) => ({ filename: `f${i}` }));

test('listCompareFiles: a listing under the 300 file cap is complete (299 files)', async () => {
  const r = await m.listCompareFiles(githubReturning(nFiles(299)), 'o', 'r', 'a...b');
  assert.equal(r.files.length, 299);
  assert.equal(r.truncated, false);
});

test('listCompareFiles: a listing of exactly 300 files may be clipped, so it is truncated', async () => {
  const r = await m.listCompareFiles(githubReturning(nFiles(300)), 'o', 'r', 'a...b');
  assert.equal(r.files.length, 300);
  assert.equal(r.truncated, true);
});

test('listCompareFiles: a 390 file range clipped to 300 by GitHub is truncated', async () => {
  const r = await m.listCompareFiles(githubReturning(nFiles(390)), 'o', 'r', 'a...b');
  assert.equal(m.COMPARE_FILE_CAP, 300);
  assert.equal(r.files.length, 300);
  assert.equal(r.truncated, true);
});

test('listCompareFiles: makes one request and never pages files', async () => {
  const calls = [];
  const github = { rest: { repos: { compareCommitsWithBasehead: async (a) => { calls.push(a); return { data: { files: nFiles(300) } }; } } } };
  await m.listCompareFiles(github, 'o', 'r', 'a...b');
  assert.equal(calls.length, 1);
  assert.equal(calls[0].basehead, 'a...b');
});

test('listCompareFiles: no files array on a changed range fails closed (truncated)', async () => {
  const github = { rest: { repos: { compareCommitsWithBasehead: async () => ({ data: { status: 'ahead' } }) } } };
  assert.deepEqual(await m.listCompareFiles(github, 'o', 'r', 'a...b'), { files: [], truncated: true });
  const none = { rest: { repos: { compareCommitsWithBasehead: async () => ({ data: {} }) } } };
  assert.equal((await m.listCompareFiles(none, 'o', 'r', 'a...b')).truncated, true);
});

test('listCompareFiles: an identical range with no files array is an empty, complete listing', async () => {
  const github = { rest: { repos: { compareCommitsWithBasehead: async () => ({ data: { status: 'identical' } }) } } };
  assert.deepEqual(await m.listCompareFiles(github, 'o', 'r', 'a...b'), { files: [], truncated: false });
});

test('listCompareFiles: an empty files array on a changed range (e.g. an empty merge) is complete', async () => {
  const r = await m.listCompareFiles(githubReturning([], { status: 'ahead' }), 'o', 'r', 'a...b');
  assert.deepEqual(r, { files: [], truncated: false });
});

test('assessMergeOnlyHop: a clean merge of main into the PR branch is merge-only', async () => {
  const { repo, feature, main1 } = scenario();
  const merge = repo.merge(feature, main1);
  const r = await m.assessMergeOnlyHop(hopArgs(repo, merge));
  assert.deepEqual(r, { ok: true, firstParent: feature, secondParent: main1, files: 1, paths: ['src/a.txt'] });
});

test('assessMergeOnlyHop: a merge that brings in several main commits and deletions and additions is merge-only', async () => {
  const { repo, feature, main1 } = scenario();
  const main2 = repo.commit(main1, { 'src/b.txt': null, 'docs/new.md': 'n', 'src/a.txt': 'a2' });
  repo.branches.main = main2;
  const merge = repo.merge(feature, main2);
  const r = await m.assessMergeOnlyHop(hopArgs(repo, merge));
  assert.equal(r.ok, true);
  assert.equal(r.files, 3);
});

test('assessMergeOnlyHop: reports the merged-in paths (renames under both names), sorted; an empty merge has none', async () => {
  const { repo, feature, main1 } = scenario();
  const main2 = repo.commit(main1, { 'src/b.txt': null, 'docs/new.md': 'n' });
  repo.branches.main = main2;
  const merge = repo.merge(feature, main2);
  const r = await m.assessMergeOnlyHop(hopArgs(repo, merge));
  assert.deepEqual(r.paths, ['docs/new.md', 'src/a.txt', 'src/b.txt']);
  const again = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(merge, main2)));
  assert.deepEqual(again.paths, []);
});

test('walkMergeOnlyChain: every hop carries its merged-in paths', async () => {
  const { repo, feature, main1 } = scenario();
  const first = repo.merge(feature, main1);
  const main2 = repo.commit(main1, { 'docs/new.md': 'n' });
  repo.branches.main = main2;
  const second = repo.merge(first, main2);
  const r = await m.walkMergeOnlyChain({ ...hopArgs(repo, second), headSha: second, classify: (sha) => (sha === feature ? 'stop' : null) });
  assert.equal(r.ok, true);
  assert.deepEqual(r.hops.map((h) => h.paths), [['docs/new.md'], ['src/a.txt']]);
});
test('assessMergeOnlyHop: merging main when the PR already contains it (empty merge) is merge-only', async () => {
  const { repo, feature, main1 } = scenario();
  const first = repo.merge(feature, main1);
  const second = repo.merge(first, main1);
  const r = await m.assessMergeOnlyHop(hopArgs(repo, second));
  assert.equal(r.ok, true);
  assert.equal(r.files, 0);
});

test('assessMergeOnlyHop: a single-parent commit (rebase or feature commit) is not merge-only', async () => {
  const { repo, feature } = scenario();
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.commit(feature, { 'src/x.txt': 'x' })));
  assert.deepEqual(r, { ok: false, reason: 'not a two-parent merge commit' });
});

test('assessMergeOnlyHop: an octopus merge is not merge-only', async () => {
  const { repo, feature, main1, root } = scenario();
  const other = repo.commit(root, { 'src/o.txt': 'o' });
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.octopus(feature, main1, other)));
  assert.equal(r.ok, false);
  assert.match(r.reason, /two-parent/);
});

test('assessMergeOnlyHop: base merged into the wrong parent slot (PR second) is not merge-only', async () => {
  const { repo, feature, main1 } = scenario();
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(main1, feature)));
  assert.equal(r.ok, false);
  assert.match(r.reason, /second parent is not on main/);
});

test('assessMergeOnlyHop: merging another unmerged branch instead of the base is not merge-only', async () => {
  const { repo, feature, root } = scenario();
  const sibling = repo.commit(root, { 'src/sibling.txt': 's' });
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(feature, sibling)));
  assert.equal(r.ok, false);
  assert.match(r.reason, /second parent is not on main/);
});

test('assessMergeOnlyHop: an evil merge that edits an extra file is not merge-only', async () => {
  const { repo, feature, main1 } = scenario();
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(feature, main1, { 'src/sneaky.txt': 'payload' })));
  assert.equal(r.ok, false);
  assert.match(r.reason, /differ from the base side/);
});

test('assessMergeOnlyHop: an evil merge that alters a main file differently is not merge-only', async () => {
  const { repo, feature, main1 } = scenario();
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(feature, main1, { 'src/a.txt': 'tampered' })));
  assert.equal(r.ok, false);
  assert.match(r.reason, /differ from the base side/);
});

test('assessMergeOnlyHop: a merge that silently drops a main change is not merge-only', async () => {
  const { repo, feature, main1 } = scenario();
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(feature, main1, { 'src/a.txt': 'a0' })));
  assert.equal(r.ok, false);
});

test('assessMergeOnlyHop: a merge that reverts a PR-side change is not merge-only', async () => {
  const { repo, feature, main1 } = scenario();
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(feature, main1, { 'src/feature.txt': null })));
  assert.equal(r.ok, false);
});

test('assessMergeOnlyHop: PR and base touching the same file is not merge-only, even when merged cleanly', async () => {
  const { repo, root } = scenario();
  const pr = repo.commit(root, { 'src/a.txt': 'a-pr' });
  const main = repo.commit(repo.branches.main, { 'src/b.txt': 'b2' });
  repo.branches.main = main;
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(pr, main, { 'src/a.txt': 'a-resolved' })));
  assert.equal(r.ok, false);
  assert.match(r.reason, /same files/);
});

test('assessMergeOnlyHop: both sides making the identical change to a file still counts as overlap', async () => {
  const { repo, root } = scenario();
  const pr = repo.commit(root, { 'src/a.txt': 'a1' });
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(pr, repo.branches.main)));
  assert.equal(r.ok, false);
  assert.match(r.reason, /same files/);
});

// Main moves on by `extra` generated files on top of the scenario's one-file change (src/a.txt).
function staleBranch(extra) {
  const { repo, feature } = scenario();
  const bulk = {};
  for (let i = 0; i < extra; i++) bulk[`gen/f${String(i).padStart(4, '0')}.txt`] = `v${i}`;
  const main = repo.commit(repo.branches.main, bulk);
  repo.branches.main = main;
  return { repo, feature, main };
}

test('assessMergeOnlyHop: a truncated base-side listing fails closed (390 files, clipped to 300 by GitHub)', async () => {
  const { repo, feature, main } = staleBranch(389);
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(feature, main)));
  assert.equal(r.ok, false);
  assert.match(r.reason, /truncated/);
});

test('assessMergeOnlyHop: a base side of exactly 300 files (the cap) fails closed', async () => {
  const { repo, feature, main } = staleBranch(299);
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(feature, main)));
  assert.equal(r.ok, false);
  assert.match(r.reason, /truncated/);
});

test('assessMergeOnlyHop: a large but complete listing (299 files, under the cap) is verified', async () => {
  const { repo, feature, main } = staleBranch(298);
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(feature, main)));
  assert.equal(r.ok, true);
  assert.equal(r.files, 299);
});

test('assessMergeOnlyHop: an evil file hidden past the 300 file cut fails closed', async () => {
  // Base side and delta are both clipped to the same first 300 paths; the hand-added file sorts
  // after the cut, so only the truncation guard can catch it.
  const { repo, feature, main } = staleBranch(389);
  const merge = repo.merge(feature, main, { 'zzz/evil.txt': 'x' });
  const r = await m.assessMergeOnlyHop(hopArgs(repo, merge));
  assert.equal(r.ok, false);
  assert.match(r.reason, /truncated/);
});
test('assessMergeOnlyHop: invalid sha or missing base ref fails without calling the API', async () => {
  const { repo, feature, main1 } = scenario();
  const merge = repo.merge(feature, main1);
  assert.equal((await m.assessMergeOnlyHop(hopArgs(repo, 'abc'))).ok, false);
  assert.equal((await m.assessMergeOnlyHop(hopArgs(repo, undefined))).ok, false);
  assert.equal((await m.assessMergeOnlyHop({ ...hopArgs(repo, merge), baseRef: '' })).ok, false);
  assert.deepEqual(repo.calls, []);
});

test('assessMergeOnlyHop: an API error is thrown to the caller (never read as merge-only)', async () => {
  const { repo, feature, main1 } = scenario();
  const merge = repo.merge(feature, main1);
  repo.failGetCommit = true;
  await assert.rejects(m.assessMergeOnlyHop(hopArgs(repo, merge)), /502/);
});

const walkArgs = (repo, headSha, classify, extra = {}) => ({
  github: repo.github, owner: 'o', repo: 'r', baseRef: 'main', headSha, classify, ...extra,
});

test('walkMergeOnlyChain: one merge-from-base hop reaches the anchor', async () => {
  const { repo, feature, main1 } = scenario();
  const merge = repo.merge(feature, main1);
  const r = await m.walkMergeOnlyChain(walkArgs(repo, merge, (s) => (s === feature ? 'stop' : null)));
  assert.equal(r.ok, true);
  assert.equal(r.anchor, feature);
  assert.equal(r.hops.length, 1);
  assert.equal(r.hops[0].sha, merge);
});

test('walkMergeOnlyChain: several successive merges of main walk back to the nearest anchor', async () => {
  const { repo, feature, main1 } = scenario();
  const merge1 = repo.merge(feature, main1);
  const main2 = repo.commit(main1, { 'docs/readme.md': 'r2' });
  repo.branches.main = main2;
  const merge2 = repo.merge(merge1, main2);
  const main3 = repo.commit(main2, { 'src/b.txt': 'b3' });
  repo.branches.main = main3;
  const merge3 = repo.merge(merge2, main3);
  const seen = [];
  const r = await m.walkMergeOnlyChain(walkArgs(repo, merge3, (s) => { seen.push(s); return s === feature ? 'stop' : null; }));
  assert.equal(r.ok, true);
  assert.equal(r.anchor, feature);
  assert.equal(r.hops.length, 3);
  assert.deepEqual(seen, [merge2, merge1, feature]);
});

test('walkMergeOnlyChain: stops at the NEAREST anchor, not a farther one', async () => {
  const { repo, feature, main1 } = scenario();
  const merge1 = repo.merge(feature, main1);
  const main2 = repo.commit(main1, { 'docs/readme.md': 'r2' });
  repo.branches.main = main2;
  const merge2 = repo.merge(merge1, main2);
  const r = await m.walkMergeOnlyChain(walkArgs(repo, merge2, () => 'stop'));
  assert.equal(r.anchor, merge1);
  assert.equal(r.hops.length, 1);
});

test('walkMergeOnlyChain: a blocked anchor (changes-requested) does not carry', async () => {
  const { repo, feature, main1 } = scenario();
  const merge = repo.merge(feature, main1);
  const r = await m.walkMergeOnlyChain(walkArgs(repo, merge, () => 'block'));
  assert.equal(r.ok, false);
  assert.equal(r.blocked, true);
});

test('walkMergeOnlyChain: a feature commit on top of a merge breaks the chain', async () => {
  const { repo, feature, main1 } = scenario();
  const merge = repo.merge(feature, main1);
  const tip = repo.commit(merge, { 'src/new.txt': 'n' });
  const r = await m.walkMergeOnlyChain(walkArgs(repo, tip, () => 'stop'));
  assert.equal(r.ok, false);
  assert.match(r.reason, /two-parent/);
});

test('walkMergeOnlyChain: a feature commit between merges and the anchor breaks the chain', async () => {
  const { repo, feature, main1 } = scenario();
  const anchor = feature;
  const work = repo.commit(anchor, { 'src/more.txt': 'm' });
  const merge = repo.merge(work, main1);
  const r = await m.walkMergeOnlyChain(walkArgs(repo, merge, (s) => (s === anchor ? 'stop' : null)));
  assert.equal(r.ok, false);
  assert.match(r.reason, /two-parent/);
  assert.equal(r.hops.length, 1);
});

test('walkMergeOnlyChain: an evil merge anywhere in the chain breaks it', async () => {
  const { repo, feature, main1 } = scenario();
  const evil = repo.merge(feature, main1, { 'src/sneaky.txt': 'x' });
  const main2 = repo.commit(main1, { 'docs/readme.md': 'r2' });
  repo.branches.main = main2;
  const tip = repo.merge(evil, main2);
  const r = await m.walkMergeOnlyChain(walkArgs(repo, tip, (s) => (s === feature ? 'stop' : null)));
  assert.equal(r.ok, false);
  assert.match(r.reason, /differ from the base side/);
});

test('walkMergeOnlyChain: gives up after the maximum number of hops', async () => {
  const { repo, feature, main1 } = scenario();
  let tip = repo.merge(feature, main1);
  let main = main1;
  for (let i = 0; i < m.MAX_HOPS; i++) {
    main = repo.commit(main, { 'docs/readme.md': `r${i + 10}` });
    repo.branches.main = main;
    tip = repo.merge(tip, main);
  }
  // MAX_HOPS + 1 merges deep: the anchor (feature) is one hop beyond the limit.
  const r = await m.walkMergeOnlyChain(walkArgs(repo, tip, (s) => (s === feature ? 'stop' : null)));
  assert.equal(r.ok, false);
  assert.match(r.reason, /no anchor within 10/);
  assert.equal(r.hops.length, m.MAX_HOPS);
  const exactly = await m.walkMergeOnlyChain(walkArgs(repo, tip, (s) => (s === feature ? 'stop' : null), { maxHops: m.MAX_HOPS + 1 }));
  assert.equal(exactly.ok, true);
});

test('walkMergeOnlyChain: an API error fails closed and is flagged apiError', async () => {
  const { repo, feature, main1 } = scenario();
  const merge = repo.merge(feature, main1);
  repo.failGetCommit = true;
  const r = await m.walkMergeOnlyChain(walkArgs(repo, merge, () => 'stop'));
  assert.equal(r.ok, false);
  assert.equal(r.apiError, true);
});

test('walkMergeOnlyChain: a classify failure fails closed and is flagged apiError', async () => {
  const { repo, feature, main1 } = scenario();
  const merge = repo.merge(feature, main1);
  const r = await m.walkMergeOnlyChain(walkArgs(repo, merge, async () => { throw new Error('checks 500'); }));
  assert.equal(r.ok, false);
  assert.equal(r.apiError, true);
  assert.match(r.reason, /checks 500/);
});

test('walkMergeOnlyChain: classify is never asked about the head itself', async () => {
  const { repo, feature, main1 } = scenario();
  const merge = repo.merge(feature, main1);
  const seen = [];
  await m.walkMergeOnlyChain(walkArgs(repo, merge, (s) => { seen.push(s); return 'stop'; }));
  assert.ok(!seen.includes(merge));
});
