// Update open PR branches from main (mirrored across RedditPodcastPoster, website, Api).
//
// Branch protection "Require branches to be up to date before merging" blocks squash/merge
// while a PR tip is behind main. Agents must not manually merge main into every open PR.
// On every push to main (typically a merged PR), this calls GitHub's Update Branch API for
// each open PR targeting main — the same action as the green "Update branch" button.
//
// Skips: draft PRs; PRs that are already up to date / mergeable without needing an update
// (API may no-op or return 422); conflicts (409 — leave for a human/agent to resolve).
// Failures on one PR do not block others; the job fails only if every attempted update fails
// when at least one PR needed updating.
//
// Review-gate: a clean update-branch merge is a merge-only tip (#1555), so an acceptable
// marker on the previous head carries without re-review.
//
// Tests: update-open-prs-from-main.test.cjs

'use strict';

/**
 * @param {object} opts
 * @param {{ paginate: Function, rest: { pulls: { updateBranch: Function } } }} opts.github
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
    try {
      await github.rest.pulls.updateBranch({
        owner,
        repo,
        pull_number: pr.number,
        expected_head_sha: pr.head.sha,
      });
      core.info(`Updated ${label} from ${base}.`);
      updated.push(pr.number);
    } catch (err) {
      const status = err.status ?? err.response?.status;
      const message = String(err.message ?? err);
      // 422: already up to date / not behind, or unmergeable. Treat conflict wording as failed.
      if (status === 422) {
        if (/conflict/i.test(message)) {
          core.warning(`Conflict updating ${label}: ${message}`);
          failed.push({ number: pr.number, reason: message });
        } else {
          core.info(`Skipped ${label}: ${message}`);
          skipped.push({ number: pr.number, reason: message });
        }
        continue;
      }
      if (status === 409) {
        core.warning(`Conflict updating ${label}: ${message}`);
        failed.push({ number: pr.number, reason: message });
        continue;
      }
      core.warning(`Failed updating ${label}: ${message}`);
      failed.push({ number: pr.number, reason: message });
    }
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

async function writeSummary(core, markdown) {
  if (core.summary?.addRaw && core.summary?.write) {
    await core.summary.addRaw(markdown, true).write();
  } else {
    core.info(markdown);
  }
}

module.exports = { run };
