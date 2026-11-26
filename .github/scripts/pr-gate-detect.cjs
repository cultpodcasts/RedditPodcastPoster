'use strict';
// pr-gate-detect (cultpodcasts/RedditPodcastPoster#1555): decide whether PR gate can skip the
// heavy `build` job (dotnet.yml). It can only when the PR tip is a chain of PURE merge-from-base
// commits (see merge-only-tip.cjs) on top of a first-parent commit whose required check-run
// `gate` (PR gate workflow, GitHub Actions app) already concluded `success`. That earlier gate
// built and tested the PR's own code; the merge only added base-branch content that main's own
// CI covers. actionlint and bicep still run on every tip.
// `github.event.before` is NOT used: it is unreliable on `pull_request`. The first-parent
// chain is walked instead, at most MAX_HOPS commits.
// Fails closed: any other shape, an unverifiable listing or an API error yields
// skip_heavy=false (full build). Outputs: skip_heavy ('true' | 'false'), reason.
// Tests: pr-gate-detect.test.cjs.

const { walkMergeOnlyChain } = require('./merge-only-tip.cjs');

const GATE_CHECK_NAME = 'gate';
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
    return {
      skip: true,
      reason: `merge-from-base only (${n} commit${n === 1 ? '' : 's'}) above ${String(result.anchor).slice(0, 7)} with a green gate`,
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

module.exports = { GATE_CHECK_NAME, GATE_APP_SLUG, hasGreenGate, decide, run };
