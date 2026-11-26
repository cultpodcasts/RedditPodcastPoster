'use strict';
// pr-gate-verdict (cultpodcasts/RedditPodcastPoster#1555): the fail-closed arbiter behind the
// single required check "PR gate / gate". Given the `needs` context of the gate job, it lists the
// jobs that make the gate fail.
//
//   - Every job in REQUIRED_JOBS must be present in `needs`; a missing one fails (a skipped or
//     renamed dependency must not leave the gate green by omission).
//   - `detect` is not a failure source: when it fails, `build` runs in full (workflow `!cancelled()`
//     clause) and is judged on its own result.
//   - A job passes only on `success`, with ONE exception: `build` may be `skipped` when detect
//     succeeded AND said skip_heavy=true. Anything else (a failed detect with a stale output, an
//     empty or `false` skip_heavy, a cancelled build) means a skipped build fails the gate.
// Runs from the PR's merge ref like the rest of PR gate; see docs/deployment.md § PR gate.
// Tests: pr-gate-verdict.test.cjs.

const REQUIRED_JOBS = ['detect', 'build', 'actionlint', 'bicep'];

// True when a skipped `build` is acceptable.
function skipAccepted({ skipHeavy, detectResult }) {
  return skipHeavy === 'true' && detectResult === 'success';
}

// Names of the failing jobs, in `needs` order (missing required jobs last). Empty = gate passes.
function failedJobs({ needs, skipHeavy, detectResult }) {
  const entries = needs && typeof needs === 'object' && !Array.isArray(needs) ? Object.entries(needs) : [];
  const skipOk = skipAccepted({ skipHeavy, detectResult });
  const failed = entries
    .filter(([name]) => name !== 'detect')
    .filter(([, job]) => !job || job.result !== 'success')
    .filter(([name, job]) => !(name === 'build' && job && job.result === 'skipped' && skipOk))
    .map(([name]) => name);
  for (const name of REQUIRED_JOBS) {
    if (!entries.some(([n]) => n === name) && !failed.includes(name)) failed.push(name);
  }
  return failed;
}

// Parse the `needs` JSON from the workflow. Anything unparseable fails the gate.
function parseNeeds(json) {
  try {
    const parsed = JSON.parse(json);
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

// CLI: reads RESULTS (needs JSON), SKIP_HEAVY, DETECT_RESULT, DETECT_REASON from the environment.
// Returns the process exit code (0 = gate passes).
function main(env, log = console) {
  const needs = parseNeeds(env.RESULTS);
  if (!needs) {
    log.log('::error::PR gate failed: could not read the needs context');
    return 1;
  }
  for (const [name, job] of Object.entries(needs)) log.log(`${name}: ${job && job.result}`);
  log.log(`detect: skip_heavy=${env.SKIP_HEAVY || '<unset>'} (${env.DETECT_REASON || 'no reason'})`);
  const failed = failedJobs({ needs, skipHeavy: env.SKIP_HEAVY, detectResult: env.DETECT_RESULT });
  if (failed.length) {
    log.log(`::error::PR gate failed: ${failed.join(', ')}`);
    return 1;
  }
  return 0;
}

module.exports = { REQUIRED_JOBS, skipAccepted, failedJobs, parseNeeds, main };

if (require.main === module) process.exit(main(process.env));
