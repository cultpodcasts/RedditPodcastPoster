'use strict';
// pr-gate-detect (cultpodcasts/RedditPodcastPoster#1555): decide whether PR gate can skip the
// heavy `build` job (dotnet.yml). It can only when ALL of these hold:
//   1. the PR tip is a chain of PURE merge-from-base commits (see merge-only-tip.cjs);
//   2. the chain sits on top of a first-parent commit whose required check-run `gate` (PR gate
//      workflow, GitHub Actions app) already concluded `success` (that gate built and tested the
//      PR's own code); and
//   3. every file the merged-in base side touched is on the NON-BUILD allowlist below (docs,
//      markdown, .cursor, workflow scripts). Textual non-overlap does not imply the combination
//      compiles (main can rename an API in file B while the PR calls it from file A), and main's
//      own CI covers main's content alone, not PR + main together. So any base-side file that
//      could affect the build or tests means the full build runs.
// actionlint and bicep still run on every tip.
// Trust: this runs on `pull_request` from the PR's own merge ref, so a PR author controls these
// scripts and pr-gate.yml. The skip decision is only as strong as "PR gate is required on main"
// (docs/deployment.md § PR gate); it is not a security boundary.
// `github.event.before` is NOT used: it is unreliable on `pull_request`. The first-parent
// chain is walked instead, at most MAX_HOPS commits.
// Fails closed: any other shape, an unverifiable listing or an API error yields
// skip_heavy=false (full build). Outputs: skip_heavy ('true' | 'false'), reason.
// Tests: pr-gate-detect.test.cjs.

const { walkMergeOnlyChain } = require('./merge-only-tip.cjs');

const GATE_CHECK_NAME = 'gate';

// Paths that cannot change what the build, tests, coverage gate or unit-test guardrails see.
// Deny first: docs/contracts/** is compared against a generated copy by the build.
const DENY_PATTERNS = [/^docs\/contracts\//];
const NON_BUILD_PATTERNS = [
  /^docs\//,
  /^\.cursor\//,
  /^\.github\/scripts\//,
  /^\.github\/ISSUE_TEMPLATE\//,
  /\.md$/i,
];

// True when changing `path` cannot affect the heavy build.
function isNonBuildPath(path) {
  const p = String(path || '');
  if (!p || DENY_PATTERNS.some((re) => re.test(p))) return false;
  return NON_BUILD_PATTERNS.some((re) => re.test(p));
}

// Paths from the walked hops that are NOT on the allowlist. A hop with no `paths` array is
// unknown, so it counts as build-affecting (fail closed).
function buildAffectingPaths(hops) {
  const out = new Set();
  for (const hop of hops || []) {
    if (!Array.isArray(hop && hop.paths)) { out.add('<unknown paths>'); continue; }
    for (const p of hop.paths) if (!isNonBuildPath(p)) out.add(p);
  }
  return [...out].sort();
}
const GATE_APP_SLUG = 'github-actions';

// True when `sha` has a completed, successful `gate` check-run posted by GitHub Actions.
async function hasGreenGate({ github, owner, repo, sha }) {
  const { data } = await github.rest.checks.listForRef({
    owner, repo, ref: sha, check_name: GATE_CHECK_NAME, filter: 'latest', per_page: 100,
  });
  return ((data && data.check_runs) || []).some((cr) => cr.name === GATE_CHECK_NAME
    && cr.status === 'completed'
    && cr.conclusion === 'success'
    && cr.app && cr.app.slug === GATE_APP_SLUG);
}

// Pure-ish decision; resolves { skip: boolean, reason: string }.
async function decide({ github, owner, repo, baseRef, headSha, walk = walkMergeOnlyChain, green = hasGreenGate }) {
  if (!headSha || !baseRef) return { skip: false, reason: 'pull request head or base unknown' };
  let result;
  try {
    result = await walk({
      github, owner, repo, baseRef, headSha,
      classify: async (sha) => ((await green({ github, owner, repo, sha })) ? 'stop' : null),
    });
  } catch (err) {
    return { skip: false, reason: `error while checking the tip: ${err.message || err}` };
  }
  if (result && result.ok) {
    const n = result.hops.length;
    const affecting = buildAffectingPaths(result.hops);
    if (affecting.length) {
      const shown = affecting.slice(0, 3).join(', ');
      const more = affecting.length > 3 ? ` and ${affecting.length - 3} more` : '';
      return { skip: false, reason: `merged-in main changes build-affecting files (${shown}${more}); full build runs` };
    }
    return {
      skip: true,
      reason: `merge-from-base only (${n} commit${n === 1 ? '' : 's'}, non-build files only) above ${String(result.anchor).slice(0, 7)} with a green gate`,
    };
  }
  return { skip: false, reason: (result && result.reason) || 'tip is not merge-from-base only' };
}

async function run({ github, context, core, walk, green }) {
  const pr = context.payload && context.payload.pull_request;
  const { owner, repo } = context.repo;
  const { skip, reason } = pr
    ? await decide({ github, owner, repo, baseRef: pr.base && pr.base.ref, headSha: pr.head && pr.head.sha, walk, green })
    : { skip: false, reason: 'not a pull_request event' };
  core.setOutput('skip_heavy', skip ? 'true' : 'false');
  core.setOutput('reason', reason);
  core.info(`PR gate detect: skip_heavy=${skip} (${reason})`);
  if (core.summary) {
    await core.summary
      .addRaw(`PR gate detect: heavy build **${skip ? 'skipped' : 'runs'}** (${reason})\n`)
      .write();
  }
  return { skip, reason };
}

module.exports = {
  GATE_CHECK_NAME, GATE_APP_SLUG, isNonBuildPath, buildAffectingPaths, hasGreenGate, decide, run,
};
