'use strict';

const { describe, it } = require('node:test');
const assert = require('node:assert/strict');
const { run } = require('./update-open-prs-from-main.cjs');

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

function githubStub({ pulls, updateResults }) {
  const listCalls = [];
  const updateCalls = [];
  return {
    listCalls,
    updateCalls,
    paginate: async (fn, params) => {
      listCalls.push(params);
      return typeof fn === 'function' ? pulls : pulls;
    },
    rest: {
      pulls: {
        list: async () => ({ data: pulls }),
        updateBranch: async (args) => {
          updateCalls.push(args);
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

  it('skips when update-branch says the PR is already current (422 without conflict)', async () => {
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

  it('records conflicts as failed but does not fail the job when another PR updates', async () => {
    const pulls = [
      { number: 30, draft: false, head: { ref: 'feat/ok', sha: 'e'.repeat(40) } },
      { number: 31, draft: false, head: { ref: 'feat/conflict', sha: 'f'.repeat(40) } },
    ];
    const github = githubStub({
      pulls,
      updateResults: new Map([
        [31, { error: { status: 409, message: 'merge conflict between base and head' } }],
      ]),
    });
    const core = coreStub();

    const result = await run({ github, context, core });

    assert.deepEqual(result.updated, [30]);
    assert.equal(result.failed.length, 1);
    assert.equal(result.failed[0].number, 31);
    assert.equal(core.logs.failed, null);
  });

  it('fails the job when every open PR fails to update', async () => {
    const pulls = [
      { number: 40, draft: false, head: { ref: 'feat/x', sha: '1'.repeat(40) } },
    ];
    const github = githubStub({
      pulls,
      updateResults: new Map([
        [40, { error: { status: 409, message: 'merge conflict' } }],
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
