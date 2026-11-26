'use strict';
// merge-only-tip (cultpodcasts/RedditPodcastPoster#1555): decide whether a PR tip is a PURE
// merge-from-base commit, i.e. someone merged the base branch (main) into the PR branch and
// added nothing else. Used by review-gate (carry an acceptable review across the merge) and
// by RPP's PR gate (skip the heavy build when the previous tip was already green).
//
// A hop is a commit M that passes ALL of:
//   1. exactly two parents: P1 (the PR side) and P2 (the base side), in that order (GitHub's
//      "Update branch" button and `git merge origin/main` both put the PR branch first);
//   2. P2 is on the base branch (compare base...P2 is `behind` or `identical`);
//   3. the PR side (merge-base..P1) and the base side (merge-base..P2) touch no file in
//      common, so the merge was textually clean and needed no hand resolution;
//   4. the merge delta (P1..M) is exactly the base side (merge-base..P2): same files, same
//      status, same resulting blob SHAs. Anything extra (an "evil merge") or missing fails;
//   5. no compare listing was truncated (GitHub caps compare file lists), so a hidden file
//      can never ride along.
// A chain is a run of such hops following FIRST parents, at most MAX_HOPS long, ending at a
// commit the caller classifies as an anchor (a reviewed or already-green commit).
// Everything fails closed: an unexpected shape or an API error is "not merge-only".
// Pure helpers are exported for tests; the API calls are injected via `github` (octokit).
// Mirrored byte-identical across RedditPodcastPoster, Api and website. Tests: merge-only-tip.test.cjs.

const MAX_HOPS = 10;
const COMPARE_PAGE_SIZE = 100;
// GitHub caps a compare file list at 300; reaching the cap means the list may be truncated.
const COMPARE_MAX_PAGES = 3;
const SHA_RE = /^[0-9a-f]{40}$/i;
const BASE_ANCESTOR_STATUSES = new Set(['behind', 'identical']);

function notMergeOnly(reason) {
  return { ok: false, reason };
}

// Every path a compare entry touches (a rename touches both names).
function touchedPaths(files) {
  const out = new Set();
  for (const f of files || []) {
    out.add(f.filename);
    if (f.previous_filename) out.add(f.previous_filename);
  }
  return out;
}

function filesOverlap(prFiles, baseFiles) {
  const pr = touchedPaths(prFiles);
  for (const p of touchedPaths(baseFiles)) {
    if (pr.has(p)) return true;
  }
  return false;
}

// Canonical, order-independent description of what a diff does to each file.
function fileKeys(files) {
  return (files || [])
    .map((f) => `${f.status}|${f.filename}|${f.previous_filename || ''}|${f.sha || ''}`)
    .sort();
}

// True when the merge delta is exactly the base side: nothing added, dropped or altered.
function deltaMatchesBaseSide(deltaFiles, baseFiles) {
  const a = fileKeys(deltaFiles);
  const b = fileKeys(baseFiles);
  return a.length === b.length && a.every((k, i) => k === b[i]);
}

// All files of a compare, or `truncated: true` when the listing may be incomplete (page cap
// reached, or the API ignored paging and repeated entries).
async function listCompareFiles(github, owner, repo, basehead) {
  const files = [];
  const seen = new Set();
  for (let page = 1; page <= COMPARE_MAX_PAGES; page++) {
    const { data } = await github.rest.repos.compareCommitsWithBasehead({
      owner, repo, basehead, per_page: COMPARE_PAGE_SIZE, page,
    });
    const batch = (data && data.files) || [];
    for (const f of batch) {
      if (seen.has(f.filename)) return { files, truncated: true };
      seen.add(f.filename);
      files.push(f);
    }
    if (batch.length < COMPARE_PAGE_SIZE) return { files, truncated: false };
  }
  return { files, truncated: true };
}

// Assess one merge commit. Resolves { ok: true, firstParent, secondParent, files } or
// { ok: false, reason }. Throws on API errors (callers fail closed).
async function assessMergeOnlyHop({ github, owner, repo, baseRef, sha }) {
  if (!SHA_RE.test(String(sha || ''))) return notMergeOnly('invalid commit SHA');
  if (!baseRef) return notMergeOnly('base ref unknown');
  const { data: commit } = await github.rest.git.getCommit({ owner, repo, commit_sha: sha });
  const parents = ((commit && commit.parents) || []).map((p) => p.sha);
  if (parents.length !== 2 || !parents.every((p) => SHA_RE.test(String(p || '')))) {
    return notMergeOnly('not a two-parent merge commit');
  }
  const [first, second] = parents;

  const { data: onBase } = await github.rest.repos.compareCommitsWithBasehead({
    owner, repo, basehead: `${baseRef}...${second}`, per_page: 1,
  });
  if (!onBase || !BASE_ANCESTOR_STATUSES.has(onBase.status)) {
    return notMergeOnly(`second parent is not on ${baseRef}`);
  }

  const baseSide = await listCompareFiles(github, owner, repo, `${first}...${second}`);
  const prSide = await listCompareFiles(github, owner, repo, `${second}...${first}`);
  const delta = await listCompareFiles(github, owner, repo, `${first}...${sha}`);
  if (baseSide.truncated || prSide.truncated || delta.truncated) {
    return notMergeOnly('file listing truncated; cannot verify the merge');
  }
  if (filesOverlap(prSide.files, baseSide.files)) {
    return notMergeOnly('PR and base changed the same files');
  }
  if (!deltaMatchesBaseSide(delta.files, baseSide.files)) {
    return notMergeOnly('merge changes differ from the base side');
  }
  return { ok: true, firstParent: first, secondParent: second, files: delta.files.length };
}

// Walk first-parent merge-only hops from headSha until `classify(sha)` returns 'stop'
// (an anchor to carry from) or 'block' (an anchor that must not be carried). classify may be
// sync or async and is only called for commits after at least one hop. Resolves
// { ok: true, anchor, hops } or { ok: false, reason, hops, apiError?, blocked? }.
async function walkMergeOnlyChain({ github, owner, repo, baseRef, headSha, classify, maxHops = MAX_HOPS }) {
  const hops = [];
  let current = headSha;
  for (let i = 0; i < maxHops; i++) {
    let hop;
    try {
      hop = await assessMergeOnlyHop({ github, owner, repo, baseRef, sha: current });
    } catch (err) {
      return { ok: false, apiError: true, reason: `API error while checking merge-only tip: ${err.message || err}`, hops };
    }
    if (!hop.ok) return { ok: false, reason: hop.reason, hops };
    hops.push({ sha: current, firstParent: hop.firstParent, secondParent: hop.secondParent });
    current = hop.firstParent;
    let verdict;
    try {
      verdict = await classify(current);
    } catch (err) {
      return { ok: false, apiError: true, reason: `API error while classifying ${current.slice(0, 7)}: ${err.message || err}`, hops };
    }
    if (verdict === 'stop') return { ok: true, anchor: current, hops };
    if (verdict === 'block') return { ok: false, blocked: true, anchor: current, reason: `blocked at ${current.slice(0, 7)}`, hops };
  }
  return { ok: false, reason: `no anchor within ${maxHops} merge-from-base commits`, hops };
}

module.exports = {
  MAX_HOPS, COMPARE_PAGE_SIZE, COMPARE_MAX_PAGES, touchedPaths, filesOverlap, fileKeys,
  deltaMatchesBaseSide, listCompareFiles, assessMergeOnlyHop, walkMergeOnlyChain,
};
