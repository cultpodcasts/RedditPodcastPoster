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
// Review-gate: a clean update-branch merge is a merge-only tip (#1555), so an acceptable
// marker on the previous head carries without re-review.
//
// Tests: update-open-prs-from-main.test.cjs

'use strict';

/** Known "already current" Validation Failed bodies from Update Branch (422). */
const ALREADY_CURRENT_RE = /already up to date|no new commits on the base branch/i;

/** expected_head_sha did not match the tip at request time (racing push). */
const HEAD_SHA_MISMATCH_RE = /expected.?head.?sha|head sha.*(did not|doesn't|does not) match|sha did not match/i;

/**
 * @param {object} opts
 * @param {{ paginate: Function, rest: { pulls: { list: Function, get: Function, updateBranch: Function } } }} opts.github
 * @param {{ repo: { owner: string, repo: string }, ref?: string }} opts.context
 * @param {{ info: Function, warning: Function, setFailed: Function, summary?: { addRaw: Function, write: Function } }} opts.core
 * @param {string} [opts.baseBranch='main']
 */
async function run({ github, context, core, baseBranch = 'main' }) {
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
    return { updated: [], skipped: [], failed: [] };
  }

  const updated = [];
  const skipped = [];
  const failed = [];

  for (const pr of candidates) {
    const label = `#${pr.number} (${pr.head.ref})`;
    const outcome = await updateOnePr({
      github,
      core,
      owner,
      repo,
      base,
      label,
      pullNumber: pr.number,
      headSha: pr.head.sha,
    });
    if (outcome.kind === 'updated') updated.push(pr.number);
    else if (outcome.kind === 'skipped') skipped.push({ number: pr.number, reason: outcome.reason });
    else failed.push({ number: pr.number, reason: outcome.reason });
  }

  const lines = [
    `### Update open PRs from \`${base}\``,
    `- Updated: ${updated.length ? updated.map((n) => `#${n}`).join(', ') : '(none)'}`,
    `- Skipped: ${skipped.length ? skipped.map((s) => `#${s.number}`).join(', ') : '(none)'}`,
    `- Failed: ${failed.length ? failed.map((f) => `#${f.number} (${f.reason})`).join(', ') : '(none)'}`,
  ];
  await writeSummary(core, lines.join('\n'));

  // Only fail the job when we tried to update and every candidate that was behind failed.
  // Skips (already current) are success. Partial success (some updated, some conflicts) is OK.
  if (updated.length === 0 && failed.length > 0 && skipped.length === 0) {
    core.setFailed(`Could not update any of ${failed.length} open PR(s) from ${base}.`);
  }

  return { updated, skipped, failed };
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

async function writeSummary(core, markdown) {
  if (core.summary?.addRaw && core.summary?.write) {
    await core.summary.addRaw(markdown, true).write();
  } else {
    core.info(markdown);
  }
}

module.exports = { run, ALREADY_CURRENT_RE, HEAD_SHA_MISMATCH_RE };
