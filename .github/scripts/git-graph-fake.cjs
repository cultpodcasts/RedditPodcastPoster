'use strict';
// In-memory git graph + the slice of octokit that merge-only-tip.cjs uses (git.getCommit and
// repos.compareCommitsWithBasehead), with GitHub's three-dot compare semantics. Shared by
// merge-only-tip.test.cjs and review-gate.test.cjs. Not a test file (not matched by *.test.cjs).
//
// Like the real API, compare returns at most COMPARE_FILES_CAP (300) files, on page 1 only:
// per_page and page never page files, and page 2 onwards come back with no files.
const crypto = require('node:crypto');

const COMPARE_FILES_CAP = 300;
const sha1 = (s) => crypto.createHash('sha1').update(String(s)).digest('hex');

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
        compareCommitsWithBasehead: async ({ basehead, page = 1 }) => {
          repo.calls.push(['compare', basehead, page]);
          const [b, h] = basehead.split('...').map(resolve);
          const bAnc = ancestors(b);
          const hAnc = ancestors(h);
          let status = 'diverged';
          if (b === h) status = 'identical';
          else if (bAnc.has(h)) status = 'behind';
          else if (hAnc.has(b)) status = 'ahead';
          const files = diff(commits.get(mergeBase(b, h)).tree, commits.get(h).tree);
          return { data: { status, files: page === 1 ? files.slice(0, COMPARE_FILES_CAP) : [] } };
        },
      },
    },
  };
  return repo;
}

module.exports = { createRepo, sha1, COMPARE_FILES_CAP };
