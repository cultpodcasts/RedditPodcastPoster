// Update open PR branches from main (mirrored across RedditPodcastPoster, website, Api).
//
// Branch protection "Require branches to be up to date before merging" blocks squash/merge
// while a PR tip is behind main. Agents must not manually merge main into every open PR.
// On every push to main (typically a merged PR), this calls GitHub's Update Branch API for
// each open PR targeting main — the same action as the green "Update branch" button.
//
// Skips: draft PRs; PRs already current (422 with "already up to date" / "no new commits on
// the base branch"). Conflicts arrive as 422 (documented Update Branch statuses are 202 /
// 403 / 422); leave those for a human/agent. A 409 arm is kept as undocumented belt-and-
// suspenders. expected_head_sha mismatch: re-GET once and retry; still mismatched or other
// unknown 422 → failed (not a silent skip). Failures on one PR do not block others; the job
// fails only if every attempted update fails when at least one PR needed updating.
//
// Token (HARD): Update Branch MUST use a PAT or GitHub App installation token
// (`OPEN_PR_UPDATE_TOKEN`), not `GITHUB_TOKEN`. Tips updated with `GITHUB_TOKEN` put
// `pull_request` workflow runs in approval-required state ("Approve workflows to run") and
// often leave `review-gate` unposted. See cultpodcasts/RedditPodcastPoster#1582 and
// https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow#triggering-a-workflow-from-a-workflow
//
// Review-gate: after each successful tip update, wait for the new head SHA then republish
// `review-gate` with synchronize semantics so merge-only carry (#1555) applies without a
// human re-mark (and without relying solely on pull_request_target firing).
//
// Tests: update-open-prs-from-main.test.cjs

'use strict';

const reviewGateDefault = require('./review-gate.cjs');

/** Known "already current" Validation Failed bodies from Update Branch (422). */
const ALREADY_CURRENT_RE = /already up to date|no new commits on the base branch/i;

/** expected_head_sha did not match the tip at request time (racing push). */
const HEAD_SHA_MISMATCH_RE = /expected.?head.?sha|head sha.*(did not|doesn't|does not) match|sha did not match/i;

const DEFAULT_TIP_WAIT_MS = 90_000;
const DEFAULT_TIP_POLL_MS = 2_000;

/**
 * @param {object} opts
 * @param {{ paginate: Function, rest: { pulls: { list: Function, get: Function, updateBranch: Function }, repos?: object, issues?: object } }} opts.github
 * @param {{ repo: { owner: string, repo: string }, ref?: string, runId?: number, eventName?: string, payload?: object }} opts.context
 * @param {{ info: Function, warning: Function, setFailed: Function, summary?: { addRaw: Function, write: Function } }} opts.core
 * @param {string} [opts.baseBranch='main']
 * @param {{ run: Function }} [opts.reviewGate] — injectable; defaults to review-gate.cjs
 * @param {(ms: number) => Promise<void>} [opts.sleep]
 * @param {number} [opts.tipWaitMs]
 * @param {number} [opts.tipPollMs]
 */
async function run({
  github,
  context,
  core,
  baseBranch = 'main',
  reviewGate = reviewGateDefault,
  sleep = defaultSleep,
  tipWaitMs = DEFAULT_TIP_WAIT_MS,
  tipPollMs = DEFAULT_TIP_POLL_MS,
}) {
  const owner = context.repo.owner;
  const repo = context.repo.repo;
  const base = baseBranch;

  const pulls = await github.paginate(github.rest.pulls.list, {
    owner,
    repo,
    state: 'open',
    base,
    per_page: 100,
  });

  const candidates = pulls.filter((p) => !p.draft);
  if (candidates.length === 0) {
    core.info(`No open non-draft PRs targeting ${base}; nothing to update.`);
    await writeSummary(core, `No open non-draft PRs targeting \`${base}\`.`);
    return { updated: [], skipped: [], failed: [], gated: [] };
  }

  const updated = [];
  const skipped = [];
  const failed = [];
  const gated = [];

  for (const pr of candidates) {
    const label = `#${pr.number} (${pr.head.ref})`;
    const previousSha = pr.head.sha;
    const outcome = await updateOnePr({
      github,
      core,
      owner,
      repo,
      base,
      label,
      pullNumber: pr.number,
      headSha: previousSha,
    });
    if (outcome.kind === 'updated') {
      updated.push(pr.number);
      const gate = await republishReviewGate({
        github,
        context,
        core,
        owner,
        repo,
        label,
        pullNumber: pr.number,
        previousSha,
        reviewGate,
        sleep,
        tipWaitMs,
        tipPollMs,
      });
      gated.push({ number: pr.number, ...gate });
    } else if (outcome.kind === 'skipped') {
      skipped.push({ number: pr.number, reason: outcome.reason });
    } else {
      failed.push({ number: pr.number, reason: outcome.reason });
    }
  }

  const lines = [
    `### Update open PRs from \`${base}\``,
    `- Updated: ${updated.length ? updated.map((n) => `#${n}`).join(', ') : '(none)'}`,
    `- Skipped: ${skipped.length ? skipped.map((s) => `#${s.number}`).join(', ') : '(none)'}`,
    `- Failed: ${failed.length ? failed.map((f) => `#${f.number} (${f.reason})`).join(', ') : '(none)'}`,
    `- Review-gate republished: ${gated.length ? gated.map((g) => `#${g.number}=${g.state || g.reason}`).join(', ') : '(none)'}`,
  ];
  await writeSummary(core, lines.join('\n'));

  // Only fail the job when we tried to update and every candidate that was behind failed.
  // Skips (already current) are success. Partial success (some updated, some conflicts) is OK.
  if (updated.length === 0 && failed.length > 0 && skipped.length === 0) {
    core.setFailed(`Could not update any of ${failed.length} open PR(s) from ${base}.`);
  }

  return { updated, skipped, failed, gated };
}

/**
 * @returns {Promise<{ kind: 'updated' } | { kind: 'skipped', reason: string } | { kind: 'failed', reason: string }>}
 */
async function updateOnePr({ github, core, owner, repo, base, label, pullNumber, headSha }) {
  try {
    await github.rest.pulls.updateBranch({
      owner,
      repo,
      pull_number: pullNumber,
      expected_head_sha: headSha,
    });
    core.info(`Updated ${label} from ${base}.`);
    return { kind: 'updated' };
  } catch (err) {
    return classifyUpdateError({
      err,
      github,
      core,
      owner,
      repo,
      base,
      label,
      pullNumber,
      retried: false,
    });
  }
}

/**
 * @param {object} opts
 * @param {boolean} opts.retried — true when this is the post-mismatch second attempt
 */
async function classifyUpdateError({
  err,
  github,
  core,
  owner,
  repo,
  base,
  label,
  pullNumber,
  retried,
}) {
  const status = err.status ?? err.response?.status;
  const message = String(err.message ?? err);

  if (status === 422) {
    if (/conflict/i.test(message)) {
      core.warning(`Conflict updating ${label}: ${message}`);
      return { kind: 'failed', reason: message };
    }
    if (ALREADY_CURRENT_RE.test(message)) {
      core.info(`Skipped ${label}: ${message}`);
      return { kind: 'skipped', reason: message };
    }
    if (!retried && HEAD_SHA_MISMATCH_RE.test(message)) {
      core.info(`Head SHA mismatch for ${label}; re-fetching tip and retrying once.`);
      try {
        const { data: fresh } = await github.rest.pulls.get({
          owner,
          repo,
          pull_number: pullNumber,
        });
        const freshSha = fresh.head.sha;
        try {
          await github.rest.pulls.updateBranch({
            owner,
            repo,
            pull_number: pullNumber,
            expected_head_sha: freshSha,
          });
          core.info(`Updated ${label} from ${base} (after SHA refresh).`);
          return { kind: 'updated' };
        } catch (retryErr) {
          return classifyUpdateError({
            err: retryErr,
            github,
            core,
            owner,
            repo,
            base,
            label,
            pullNumber,
            retried: true,
          });
        }
      } catch (getErr) {
        const getMessage = String(getErr.message ?? getErr);
        core.warning(`Failed re-fetching ${label} after SHA mismatch: ${getMessage}`);
        return { kind: 'failed', reason: `sha-mismatch re-fetch: ${getMessage}` };
      }
    }
    core.warning(`Failed updating ${label} (422): ${message}`);
    return { kind: 'failed', reason: message };
  }

  // Undocumented for update-branch (official docs: 202/403/422); keep as conflict-ish fail.
  if (status === 409) {
    core.warning(`Conflict updating ${label} (409, undocumented for update-branch): ${message}`);
    return { kind: 'failed', reason: message };
  }

  core.warning(`Failed updating ${label}: ${message}`);
  return { kind: 'failed', reason: message };
}

async function waitForNewHead({
  github, owner, repo, pullNumber, previousSha, label, core, sleep, tipWaitMs, tipPollMs,
}) {
  const deadline = Date.now() + tipWaitMs;
  while (Date.now() < deadline) {
    const { data: pr } = await github.rest.pulls.get({
      owner,
      repo,
      pull_number: pullNumber,
    });
    if (pr.head.sha !== previousSha) {
      core.info(`${label} tip advanced ${previousSha.slice(0, 7)} → ${pr.head.sha.slice(0, 7)}.`);
      return pr.head.sha;
    }
    await sleep(tipPollMs);
  }
  core.warning(`Timed out waiting for ${label} tip to advance after update-branch (still ${previousSha.slice(0, 7)}).`);
  return null;
}

async function republishReviewGate({
  github,
  context,
  core,
  owner,
  repo,
  label,
  pullNumber,
  previousSha,
  reviewGate,
  sleep,
  tipWaitMs,
  tipPollMs,
}) {
  const newSha = await waitForNewHead({
    github, owner, repo, pullNumber, previousSha, label, core, sleep, tipWaitMs, tipPollMs,
  });
  if (!newSha) {
    return { state: null, reason: 'tip-wait-timeout' };
  }

  // Do not fail the update job when a PR's gate is red (conflicts / needs re-review).
  const gateCore = {
    info: (m) => core.info(m),
    warning: (m) => core.warning(m),
    setFailed: (m) => core.warning(`review-gate after update of ${label}: ${m}`),
    summary: core.summary,
  };

  try {
    const result = await reviewGate.run({
      github,
      context,
      core: gateCore,
      pullNumber,
      pushed: true,
    });
    if (!result) {
      return { state: null, reason: 'no-result' };
    }
    return { state: result.state, reason: result.description };
  } catch (err) {
    const message = String(err.message ?? err);
    core.warning(`Failed republishing review-gate for ${label}: ${message}`);
    return { state: null, reason: message };
  }
}

function defaultSleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function writeSummary(core, markdown) {
  if (core.summary?.addRaw && core.summary?.write) {
    await core.summary.addRaw(markdown, true).write();
  } else {
    core.info(markdown);
  }
}

module.exports = {
  run,
  ALREADY_CURRENT_RE,
  HEAD_SHA_MISMATCH_RE,
  waitForNewHead,
  republishReviewGate,
};
