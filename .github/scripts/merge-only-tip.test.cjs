'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const m = require('./merge-only-tip.cjs');

const sha1 = (s) => crypto.createHash('sha1').update(String(s)).digest('hex');

// In-memory git graph + the slice of octokit the module uses (git.getCommit and
// repos.compareCommitsWithBasehead), with GitHub's three-dot compare semantics.
function createRepo() {
  const commits = new Map();
  const branches = {};
  let seq = 0;
  const add = (parents, tree) => {
    const id = sha1(`commit-${++seq}`);
    commits.set(id, { parents, tree, seq });
    return id;
  };
  const ancestors = (id) => {
    const out = new Set();
    const stack = [id];
    while (stack.length) {
      const cur = stack.pop();
      if (out.has(cur)) continue;
      out.add(cur);
      stack.push(...commits.get(cur).parents);
    }
    return out;
  };
  const mergeBase = (a, b) => {
    const bs = ancestors(b);
    const common = [...ancestors(a)].filter((x) => bs.has(x));
    return common.sort((x, y) => commits.get(y).seq - commits.get(x).seq)[0];
  };
  const diff = (from, to) => {
    const files = [];
    for (const p of new Set([...Object.keys(from), ...Object.keys(to)]).values()) {
      if (from[p] === to[p]) continue;
      if (to[p] === undefined) files.push({ filename: p, status: 'removed', sha: sha1(from[p]) });
      else if (from[p] === undefined) files.push({ filename: p, status: 'added', sha: sha1(to[p]) });
      else files.push({ filename: p, status: 'modified', sha: sha1(to[p]) });
    }
    return files.sort((a, b) => a.filename.localeCompare(b.filename));
  };
  const resolve = (name) => branches[name] || name;

  const repo = {
    commits, branches, calls: [],
    root: (tree) => add([], { ...tree }),
    // A single-parent commit applying path -> content (null deletes).
    commit(parent, changes) {
      const tree = { ...commits.get(parent).tree };
      for (const [p, c] of Object.entries(changes)) { if (c === null) delete tree[p]; else tree[p] = c; }
      return add([parent], tree);
    },
    // A clean three-way merge. `tamper` edits the resulting tree (an evil merge).
    merge(first, second, tamper = {}) {
      const baseTree = commits.get(mergeBase(first, second)).tree;
      const a = commits.get(first).tree;
      const b = commits.get(second).tree;
      const tree = {};
      for (const p of new Set([...Object.keys(baseTree), ...Object.keys(a), ...Object.keys(b)])) {
        let v;
        if (a[p] === b[p]) v = a[p];
        else if (a[p] === baseTree[p]) v = b[p];
        else if (b[p] === baseTree[p]) v = a[p];
        else v = `conflict:${p}`;
        if (v !== undefined) tree[p] = v;
      }
      for (const [p, c] of Object.entries(tamper)) { if (c === null) delete tree[p]; else tree[p] = c; }
      return add([first, second], tree);
    },
    octopus(first, second, third) {
      return add([first, second, third], { ...commits.get(first).tree });
    },
  };
  repo.github = {
    rest: {
      git: {
        getCommit: async ({ commit_sha }) => {
          repo.calls.push(['getCommit', commit_sha]);
          if (repo.failGetCommit) throw new Error('502 Bad Gateway');
          if (!commits.has(commit_sha)) throw new Error('404');
          return { data: { parents: commits.get(commit_sha).parents.map((sha) => ({ sha })) } };
        },
      },
      repos: {
        compareCommitsWithBasehead: async ({ basehead, per_page = 30, page = 1 }) => {
          repo.calls.push(['compare', basehead, page]);
          const [b, h] = basehead.split('...').map(resolve);
          const bAnc = ancestors(b);
          const hAnc = ancestors(h);
          let status = 'diverged';
          if (b === h) status = 'identical';
          else if (bAnc.has(h)) status = 'behind';
          else if (hAnc.has(b)) status = 'ahead';
          const files = diff(commits.get(mergeBase(b, h)).tree, commits.get(h).tree);
          const start = (page - 1) * per_page;
          return { data: { status, files: files.slice(start, start + per_page) } };
        },
      },
    },
  };
  return repo;
}

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

test('listCompareFiles: collects every page and reports a short list as complete', async () => {
  const files = Array.from({ length: 250 }, (_, i) => ({ filename: `f${i}` }));
  const github = { rest: { repos: { compareCommitsWithBasehead: async ({ page, per_page }) => ({ data: { files: files.slice((page - 1) * per_page, page * per_page) } }) } } };
  const r = await m.listCompareFiles(github, 'o', 'r', 'a...b');
  assert.equal(r.files.length, 250);
  assert.equal(r.truncated, false);
});

test('listCompareFiles: reaching the 300 file cap is treated as truncated', async () => {
  const files = Array.from({ length: 400 }, (_, i) => ({ filename: `f${i}` }));
  const github = { rest: { repos: { compareCommitsWithBasehead: async ({ page, per_page }) => ({ data: { files: files.slice((page - 1) * per_page, page * per_page) } }) } } };
  const r = await m.listCompareFiles(github, 'o', 'r', 'a...b');
  assert.equal(r.truncated, true);
});

test('listCompareFiles: an API that ignores paging (repeated entries) is treated as truncated', async () => {
  const same = Array.from({ length: 100 }, (_, i) => ({ filename: `f${i}` }));
  const github = { rest: { repos: { compareCommitsWithBasehead: async () => ({ data: { files: same } }) } } };
  const r = await m.listCompareFiles(github, 'o', 'r', 'a...b');
  assert.equal(r.truncated, true);
});

test('listCompareFiles: a missing files array is an empty, complete listing', async () => {
  const github = { rest: { repos: { compareCommitsWithBasehead: async () => ({ data: {} }) } } };
  assert.deepEqual(await m.listCompareFiles(github, 'o', 'r', 'a...b'), { files: [], truncated: false });
});

test('assessMergeOnlyHop: a clean merge of main into the PR branch is merge-only', async () => {
  const { repo, feature, main1 } = scenario();
  const merge = repo.merge(feature, main1);
  const r = await m.assessMergeOnlyHop(hopArgs(repo, merge));
  assert.deepEqual(r, { ok: true, firstParent: feature, secondParent: main1, files: 1 });
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

test('assessMergeOnlyHop: a truncated base-side listing fails closed', async () => {
  const { repo, feature } = scenario();
  const bulk = {};
  for (let i = 0; i < 350; i++) bulk[`gen/f${i}.txt`] = `v${i}`;
  const main = repo.commit(repo.branches.main, bulk);
  repo.branches.main = main;
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(feature, main)));
  assert.equal(r.ok, false);
  assert.match(r.reason, /truncated/);
});

test('assessMergeOnlyHop: a large but complete listing (under the cap) is verified', async () => {
  const { repo, feature } = scenario();
  const bulk = {};
  for (let i = 0; i < 150; i++) bulk[`gen/f${i}.txt`] = `v${i}`;
  const main = repo.commit(repo.branches.main, bulk);
  repo.branches.main = main;
  const r = await m.assessMergeOnlyHop(hopArgs(repo, repo.merge(feature, main)));
  assert.equal(r.ok, true);
  assert.equal(r.files, 151);
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
