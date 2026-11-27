'use strict';

const { describe, it } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { run } = require('./update-open-prs-from-main.cjs');

const GITHUB_SCRIPT_PIN = 'actions/github-script@3a2844b7e9c422d3c10d287c895573f7108da1b3';

function coreStub() {
  const logs = { info: [], warning: [], failed: null, summary: [] };
  return {
    logs,
    info: (m) => logs.info.push(m),
    warning: (m) => logs.warning.push(m),
    setFailed: (m) => { logs.failed = m; },
    summary: {
      addRaw(md) { logs.summary.push(md); return this; },
      async write() {},
    },
  };
}

/**
 * @param {object} opts
 * @param {Array} opts.pulls
 * @param {Map<number, { error?: { status: number, message: string }, errors?: Array<{ status: number, message: string }> }>} opts.updateResults
 *   Per pull_number: single `error`, or `errors` queue consumed per updateBranch call (for retry).
 * @param {Map<number, object>} [opts.getResults] — pulls.get responses by number (default: head from list)
 */
function githubStub({ pulls, updateResults, getResults }) {
  const listCalls = [];
  const updateCalls = [];
  const getCalls = [];
  const errorQueues = new Map();
  for (const [n, result] of updateResults) {
    if (result?.errors) errorQueues.set(n, [...result.errors]);
  }
  return {
    listCalls,
    updateCalls,
    getCalls,
    paginate: async (fn, params) => {
      listCalls.push(params);
      return typeof fn === 'function' ? pulls : pulls;
    },
    rest: {
      pulls: {
        list: async () => ({ data: pulls }),
        get: async (args) => {
          getCalls.push(args);
          if (getResults?.has(args.pull_number)) {
            return { data: getResults.get(args.pull_number) };
          }
          const fromList = pulls.find((p) => p.number === args.pull_number);
          if (!fromList) {
            const err = new Error('Not Found');
            err.status = 404;
            throw err;
          }
          return { data: fromList };
        },
        updateBranch: async (args) => {
          updateCalls.push(args);
          const queue = errorQueues.get(args.pull_number);
          if (queue && queue.length > 0) {
            const next = queue.shift();
            const err = new Error(next.message);
            err.status = next.status;
            throw err;
          }
          const result = updateResults.get(args.pull_number);
          if (result?.error) {
            const err = new Error(result.error.message);
            err.status = result.error.status;
            throw err;
          }
          return { data: { message: 'Updating pull request branch.', url: 'https://example.test' } };
        },
      },
    },
  };
}

const context = { repo: { owner: 'cultpodcasts', repo: 'RedditPodcastPoster' } };

describe('update-open-prs-from-main', () => {
  it('updates each open non-draft PR targeting main', async () => {
    const pulls = [
      { number: 10, draft: false, head: { ref: 'feat/a', sha: 'a'.repeat(40) } },
      { number: 11, draft: true, head: { ref: 'feat/draft', sha: 'b'.repeat(40) } },
      { number: 12, draft: false, head: { ref: 'feat/b', sha: 'c'.repeat(40) } },
    ];
    const github = githubStub({ pulls, updateResults: new Map() });
    const core = coreStub();

    const result = await run({ github, context, core });

    assert.deepEqual(result.updated, [10, 12]);
    assert.equal(github.updateCalls.length, 2);
    assert.equal(github.updateCalls[0].pull_number, 10);
    assert.equal(github.updateCalls[0].expected_head_sha, 'a'.repeat(40));
    assert.equal(core.logs.failed, null);
  });

  it('skips when update-branch says the PR is already current (422 already up to date)', async () => {
    const pulls = [
      { number: 20, draft: false, head: { ref: 'feat/c', sha: 'd'.repeat(40) } },
    ];
    const github = githubStub({
      pulls,
      updateResults: new Map([
        [20, { error: { status: 422, message: 'Pull request is not updatable because it is already up to date.' } }],
      ]),
    });
    const core = coreStub();

    const result = await run({ github, context, core });

    assert.deepEqual(result.updated, []);
    assert.equal(result.skipped.length, 1);
    assert.equal(result.failed.length, 0);
    assert.equal(core.logs.failed, null);
  });

  it('skips when update-branch says no new commits on the base branch (422)', async () => {
    const pulls = [
      { number: 21, draft: false, head: { ref: 'feat/d', sha: 'e'.repeat(40) } },
    ];
    const github = githubStub({
      pulls,
      updateResults: new Map([
        [21, {
          error: {
            status: 422,
            message: 'There are no new commits on the base branch to merge into this pull request\'s head.',
          },
        }],
      ]),
    });
    const core = coreStub();

    const result = await run({ github, context, core });

    assert.equal(result.skipped.length, 1);
    assert.equal(result.failed.length, 0);
    assert.equal(core.logs.failed, null);
  });

  it('records 422 merge conflict as failed but does not fail the job when another PR updates', async () => {
    const pulls = [
      { number: 30, draft: false, head: { ref: 'feat/ok', sha: 'e'.repeat(40) } },
      { number: 31, draft: false, head: { ref: 'feat/conflict', sha: 'f'.repeat(40) } },
    ];
    const github = githubStub({
      pulls,
      updateResults: new Map([
        [31, { error: { status: 422, message: 'merge conflict between base and head' } }],
      ]),
    });
    const core = coreStub();

    const result = await run({ github, context, core });

    assert.deepEqual(result.updated, [30]);
    assert.equal(result.failed.length, 1);
    assert.equal(result.failed[0].number, 31);
    assert.equal(core.logs.failed, null);
  });

  it('records undocumented 409 conflict as failed (belt-and-suspenders)', async () => {
    const pulls = [
      { number: 32, draft: false, head: { ref: 'feat/legacy-409', sha: 'a'.repeat(40) } },
    ];
    const github = githubStub({
      pulls,
      updateResults: new Map([
        [32, { error: { status: 409, message: 'merge conflict' } }],
      ]),
    });
    const core = coreStub();

    const result = await run({ github, context, core });

    assert.equal(result.failed.length, 1);
    assert.match(core.logs.failed, /Could not update/);
  });

  it('retries once on expected_head_sha mismatch then updates with fresh tip', async () => {
    const stale = '1'.repeat(40);
    const fresh = '2'.repeat(40);
    const pulls = [
      { number: 60, draft: false, head: { ref: 'feat/race', sha: stale } },
    ];
    const github = githubStub({
      pulls,
      updateResults: new Map([
        [60, {
          errors: [
            { status: 422, message: 'Expected head sha didn\'t match current head ref.' },
          ],
        }],
      ]),
      getResults: new Map([
        [60, { number: 60, draft: false, head: { ref: 'feat/race', sha: fresh } }],
      ]),
    });
    const core = coreStub();

    const result = await run({ github, context, core });

    assert.deepEqual(result.updated, [60]);
    assert.equal(result.failed.length, 0);
    assert.equal(github.getCalls.length, 1);
    assert.equal(github.updateCalls.length, 2);
    assert.equal(github.updateCalls[0].expected_head_sha, stale);
    assert.equal(github.updateCalls[1].expected_head_sha, fresh);
    assert.equal(core.logs.failed, null);
  });

  it('fails when expected_head_sha mismatch persists after one retry', async () => {
    const pulls = [
      { number: 61, draft: false, head: { ref: 'feat/race2', sha: '3'.repeat(40) } },
    ];
    const github = githubStub({
      pulls,
      updateResults: new Map([
        [61, {
          errors: [
            { status: 422, message: 'Expected head sha didn\'t match current head ref.' },
            { status: 422, message: 'Expected head sha didn\'t match current head ref.' },
          ],
        }],
      ]),
      getResults: new Map([
        [61, { number: 61, draft: false, head: { ref: 'feat/race2', sha: '4'.repeat(40) } }],
      ]),
    });
    const core = coreStub();

    const result = await run({ github, context, core });

    assert.equal(result.updated.length, 0);
    assert.equal(result.failed.length, 1);
    assert.equal(github.updateCalls.length, 2);
    assert.match(core.logs.failed, /Could not update/);
  });

  it('fails unknown 422 validation instead of skipping', async () => {
    const pulls = [
      { number: 70, draft: false, head: { ref: 'feat/mystery', sha: '5'.repeat(40) } },
    ];
    const github = githubStub({
      pulls,
      updateResults: new Map([
        [70, { error: { status: 422, message: 'Validation Failed: some other reason' } }],
      ]),
    });
    const core = coreStub();

    const result = await run({ github, context, core });

    assert.equal(result.skipped.length, 0);
    assert.equal(result.failed.length, 1);
    assert.match(core.logs.failed, /Could not update/);
  });

  it('fails the job when every open PR fails to update', async () => {
    const pulls = [
      { number: 40, draft: false, head: { ref: 'feat/x', sha: '1'.repeat(40) } },
    ];
    const github = githubStub({
      pulls,
      updateResults: new Map([
        [40, { error: { status: 422, message: 'merge conflict between base and head' } }],
      ]),
    });
    const core = coreStub();

    await run({ github, context, core });

    assert.match(core.logs.failed, /Could not update/);
  });

  it('does nothing when there are no open non-draft PRs', async () => {
    const github = githubStub({
      pulls: [{ number: 50, draft: true, head: { ref: 'draft', sha: '2'.repeat(40) } }],
      updateResults: new Map(),
    });
    const core = coreStub();

    const result = await run({ github, context, core });

    assert.deepEqual(result.updated, []);
    assert.equal(github.updateCalls.length, 0);
    assert.equal(core.logs.failed, null);
  });
});

const workflow = fs.readFileSync(
  path.join(__dirname, '..', 'workflows', 'update-open-prs-from-main.yml'),
  'utf8',
).replace(/\r\n/g, '\n');
const workflowCode = workflow.split('\n').filter((l) => !/^\s*#/.test(l)).join('\n');

describe('update-open-prs-from-main workflow YAML', () => {
  it('grants contents: write and pull-requests: write', () => {
    assert.match(workflowCode, /^  contents: write$/m);
    assert.match(workflowCode, /^  pull-requests: write$/m);
  });

  it('serialises runs per repository with cancel-in-progress', () => {
    assert.match(
      workflowCode,
      /concurrency:\s*\n\s*group:\s*update-open-prs-from-main-\$\{\{\s*github\.repository\s*\}\}/,
    );
    assert.match(workflowCode, /cancel-in-progress:\s*true/);
  });

  it('SHA-pins github-script to the same commit as review-gate / pr-gate', () => {
    assert.match(workflowCode, new RegExp(`uses:\\s*${GITHUB_SCRIPT_PIN.replace(/[.@]/g, '\\$&')}`));
    assert.doesNotMatch(workflowCode, /uses:\s*actions\/github-script@v\d+\s*$/m);
  });
});
