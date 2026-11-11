'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const g = require('./issue-close-sibling-guard.cjs');

const OPTIONS = [{ id: 'todo', name: 'Todo' }, { id: 'rfr', name: 'Ready For Review' }, { id: 'done', name: 'Done' }];
function item(status, { owner = 'cultpodcasts', number = 1 } = {}) {
  return {
    id: 'ITEM',
    project: { id: 'PROJ', number, owner: { login: owner }, field: { id: 'FIELD', options: OPTIONS } },
    fieldValueByName: status ? { name: status } : null,
  };
}
// Fake board: `status` is the card's Status; `automationDoneBeforeRead` lists read
// indices before which the board's "Item closed" automation sets Done (late landing).
function fakeBoard(initial, automationDoneBeforeRead = []) {
  let status = initial;
  let reads = 0;
  const writes = [];
  const graphql = async (q, vars) => {
    if (q.includes('updateProjectV2ItemFieldValue')) {
      writes.push(vars);
      status = 'Ready For Review';
      return {};
    }
    if (automationDoneBeforeRead.includes(reads)) status = 'Done';
    reads++;
    return { repository: { issue: { projectItems: { nodes: [item(status)] } } } };
  };
  return { graphql, writes };
}
const noSleep = async () => {};

test('openCompanions keeps only OPEN PRs, cross-repo', () => {
  const out = g.openCompanions([
    { number: 649, state: 'MERGED', url: 'u1', title: 't1', repository: { nameWithOwner: 'cultpodcasts/website' } },
    { number: 247, state: 'OPEN', url: 'u2', title: 't2', repository: { nameWithOwner: 'cultpodcasts/Api' } },
    null,
  ]);
  assert.deepEqual(out, [{ repo: 'cultpodcasts/Api', number: 247, url: 'u2', title: 't2' }]);
});

test('pickBoardItem ignores other projects', () => {
  const other = item('Done', { owner: 'someone', number: 1 });
  const mine = item('Done');
  assert.equal(g.pickBoardItem([other, mine]), mine);
  assert.equal(g.pickBoardItem([item('Done', { number: 2 })]), undefined);
});

test('restore: Done already set by automation -> writes RFR', async () => {
  const b = fakeBoard('Done');
  const r = await g.restoreBoardStatus({ graphql: b.graphql, owner: 'cultpodcasts', repo: 'Api', number: 244, sleep: noSleep });
  assert.equal(r.restored, true);
  assert.equal(b.writes.length, 1);
  assert.deepEqual(b.writes[0], { project: 'PROJ', item: 'ITEM', field: 'FIELD', option: 'rfr' });
});

test('restore: automation lands after first write -> re-asserts RFR', async () => {
  const b = fakeBoard('Ready For Review', [1]);
  // read0 RFR (stable 1), automation sets Done before read1 -> write, then RFR stable
  const r = await g.restoreBoardStatus({ graphql: b.graphql, owner: 'cultpodcasts', repo: 'Api', number: 244, sleep: noSleep });
  assert.equal(r.restored, true);
  assert.equal(b.writes.length, 1);
});

test('restore: not on board -> not restored, no write', async () => {
  const graphql = async () => ({ repository: { issue: { projectItems: { nodes: [] } } } });
  const r = await g.restoreBoardStatus({ graphql, owner: 'cultpodcasts', repo: 'Api', number: 1, sleep: noSleep });
  assert.equal(r.restored, false);
  assert.match(r.reason, /not on cultpodcasts project 1/);
});

function harness({ nodes, projectToken, board, omitGetOctokit = false }) {
  const calls = { update: [], comment: [], warn: [] };
  const github = {
    graphql: async () => ({ repository: { issue: { closedByPullRequestsReferences: { nodes } } } }),
    rest: {
      issues: {
        update: async (a) => calls.update.push(a),
        createComment: async (a) => calls.comment.push(a),
      },
    },
  };
  const core = { info: () => {}, warning: (m) => calls.warn.push(m) };
  const context = { repo: { owner: 'cultpodcasts', repo: 'Api' }, payload: { issue: { number: 244 } } };
  const getOctokit = (tok) => {
    assert.equal(tok, projectToken);
    return { graphql: board.graphql };
  };
  const args = { github, context, core, getOctokit, projectToken, sleep: noSleep };
  if (omitGetOctokit) delete args.getOctokit;
  return { calls, args };
}
const OPEN_247 = [
  { number: 649, state: 'MERGED', url: 'u1', title: 'site', repository: { nameWithOwner: 'cultpodcasts/website' } },
  { number: 247, state: 'OPEN', url: 'u2', title: 'api', repository: { nameWithOwner: 'cultpodcasts/Api' } },
];

test('run: Api#244 scenario reopens and restores board via PROJECT_TOKEN', async () => {
  const board = fakeBoard('Done');
  const h = harness({ nodes: OPEN_247, projectToken: 'pat', board });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.equal(h.calls.update[0].state, 'open');
  assert.equal(board.writes.length, 1);
  assert.match(h.calls.comment[0].body, /cultpodcasts\/Api#247/);
  assert.match(h.calls.comment[0].body, /restored to \*\*Ready For Review\*\*/);
  assert.equal(h.calls.warn.length, 0);
});

test('run: missing PROJECT_TOKEN reopens but warns loudly in job and comment', async () => {
  const board = fakeBoard('Done');
  const h = harness({ nodes: OPEN_247, projectToken: '', board });
  await g.run(h.args);
  assert.equal(h.calls.update.length, 1);
  assert.equal(board.writes.length, 0);
  assert.match(h.calls.warn[0], /PROJECT_TOKEN secret is not set/);
  assert.match(h.calls.comment[0].body, /NOT restored/);
});

test('run: no open companions -> leaves closed', async () => {
  const board = fakeBoard('Done');
  const h = harness({ nodes: [OPEN_247[0]], projectToken: 'pat', board });
  const r = await g.run(h.args);
  assert.equal(r.reopened, false);
  assert.equal(h.calls.update.length, 0);
  assert.equal(h.calls.comment.length, 0);
});

test('run: getOctokit missing (github-script < v9) still reopens and comments, warns about board', async () => {
  const board = fakeBoard('Done');
  const h = harness({ nodes: OPEN_247, projectToken: 'pat', board, omitGetOctokit: true });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.equal(h.calls.update.length, 1);
  assert.equal(h.calls.update[0].state, 'open');
  assert.equal(board.writes.length, 0);
  assert.match(h.calls.warn[0], /getOctokit is not available/);
  assert.match(h.calls.comment[0].body, /NOT restored \(getOctokit is not available/);
});

// cultpodcasts/RedditPodcastPoster#1394: on actions/github-script v7/v8 `getOctokit`
// is not injected, so the inline script threw "ReferenceError: getOctokit is not defined" before run() executed.
test('workflow: github-script major injects getOctokit when the script uses it', () => {
  const yml = fs.readFileSync(
    path.join(__dirname, '..', 'workflows', 'issue-close-sibling-guard.yml'),
    'utf8'
  );
  const m = yml.match(/uses:\s*actions\/github-script@v(\d+)/);
  assert.ok(m, 'guard workflow must use actions/github-script@vN');
  const script = yml.slice(yml.indexOf('script: |'));
  assert.match(script, /\bgetOctokit\b/, 'guard script passes getOctokit to run()');
  assert.ok(Number(m[1]) >= 9, `actions/github-script@v${m[1]} does not inject getOctokit; need v9+`);
  assert.match(script, /run\(\{[^}]*\bgetOctokit\b[^}]*\}\)/);
});
