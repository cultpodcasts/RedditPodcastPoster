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
//   3. the merge delta (P1..M) is a subset of the base side (merge-base..P2): every newly
//      introduced file key (status, path, blob) appears on the base side. Extras or tampered
//      blobs fail (evil merge). A subset (not exact equality) allows stacked / cherry-pick
//      tips that already held some of main's blobs before the merge;
//   4. the tip honours main on every base-touched path: compare P2..M must not touch any path
//      the base side changed (auto-merged divergent hunks and dropped main changes fail);
//   5. no compare listing was truncated (GitHub returns at most 300 files per compare, on
//      page 1 only, and does not page them), so a hidden file can never ride along.
// Filename overlap between PR and base sides is allowed when (3) and (4) hold — that is how
// stacked PRs sync main without a human re-mark.
// Known residual: compare entries carry no file mode or type, so a merge that only flips a mode
// (100644 -> 100755) or swaps a file for a symlink with the SAME blob, on a file the base side also
// changed, has an identical key and passes. Blob content is still verified. Accepted: closing it
// needs a tree read per hop (git.getTree, comparing mode as well as sha) and would complicate the mirror.
// A chain is a run of such hops following FIRST parents, at most MAX_HOPS long, ending at a
// commit the caller classifies as an anchor (a reviewed or already-green commit).
// Everything fails closed: an unexpected shape or an API error is "not merge-only".
// Pure helpers are exported for tests; the API calls are injected via `github` (octokit).
// Mirrored byte-identical across RedditPodcastPoster, Api and website. Tests: merge-only-tip.test.cjs.

const MAX_HOPS = 10;
// GitHub returns at most 300 files for a compare, on page 1 only: `per_page` and `page` page the
// commits list, never `files` (page 2 onwards come back with no files). A listing of 300 may
// therefore be clipped, so it counts as truncated.
const COMPARE_FILE_CAP = 300;
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

// True when every delta file key appears on the base side (evil extras / tampered blobs fail;
// a proper subset is allowed when the PR tip already held some of main's content).
function deltaSubsetOfBaseSide(deltaFiles, baseFiles) {
  const base = new Set(fileKeys(baseFiles));
  return fileKeys(deltaFiles).every((k) => base.has(k));
}

// All files of a compare, or `truncated: true` when the listing may be incomplete: it reached
// GitHub's 300 file cap (a single request, since files are not paged), or it has no files array
// although the compare is not `identical`. Fails closed: an unknown listing is never complete.
async function listCompareFiles(github, owner, repo, basehead) {
  const { data } = await github.rest.repos.compareCommitsWithBasehead({ owner, repo, basehead });
  if (!data || !Array.isArray(data.files)) {
    return { files: [], truncated: !(data && data.status === 'identical') };
  }
  return { files: data.files, truncated: data.files.length >= COMPARE_FILE_CAP };
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
  const delta = await listCompareFiles(github, owner, repo, `${first}...${sha}`);
  const tipVsBase = await listCompareFiles(github, owner, repo, `${second}...${sha}`);
  if (baseSide.truncated || delta.truncated || tipVsBase.truncated) {
    return notMergeOnly('file listing truncated; cannot verify the merge');
  }
  if (!deltaSubsetOfBaseSide(delta.files, baseSide.files)) {
    return notMergeOnly('merge changes differ from the base side');
  }
  if (filesOverlap(tipVsBase.files, baseSide.files)) {
    return notMergeOnly('tip diverged from base on a main-touched file');
  }
  return {
    ok: true, firstParent: first, secondParent: second, files: delta.files.length,
    paths: [...touchedPaths(delta.files)].sort(),
  };
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
    hops.push({ sha: current, firstParent: hop.firstParent, secondParent: hop.secondParent, paths: hop.paths });
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
  MAX_HOPS, COMPARE_FILE_CAP, touchedPaths, filesOverlap, fileKeys,
  deltaMatchesBaseSide, deltaSubsetOfBaseSide, listCompareFiles, assessMergeOnlyHop, walkMergeOnlyChain,
};
