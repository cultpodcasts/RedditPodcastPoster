'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const g = require('./issue-close-sibling-guard.cjs');

const OPTIONS = [
  { id: 'todo', name: 'Todo' },
  { id: 'in-progress', name: 'In Progress' },
  { id: 'rfr', name: 'Ready For Review' },
  { id: 'done', name: 'Done' },
];
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
      status = 'In Progress';
      return {};
    }
    if (automationDoneBeforeRead.includes(reads)) status = 'Done';
    reads++;
    return { repository: { issue: { projectItems: { nodes: [item(status)] } } } };
  };
  return { graphql, writes };
}
const noSleep = async () => {};

test('RESTORE_STATUS is In Progress (Ready For Review means ready to merge)', () => {
  assert.equal(g.RESTORE_STATUS, 'In Progress');
});

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

test('restore: Done already set by automation -> writes In Progress', async () => {
  const b = fakeBoard('Done');
  const r = await g.restoreBoardStatus({ graphql: b.graphql, owner: 'cultpodcasts', repo: 'Api', number: 244, sleep: noSleep });
  assert.equal(r.restored, true);
  assert.equal(b.writes.length, 1);
  assert.deepEqual(b.writes[0], { project: 'PROJ', item: 'ITEM', field: 'FIELD', option: 'in-progress' });
});

test('restore: automation lands after first write -> re-asserts In Progress', async () => {
  const b = fakeBoard('In Progress', [1]);
  // read0 In Progress (stable 1), automation sets Done before read1 -> write, then In Progress stable
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

function pr(repo, number, state, title = `t${number}`) {
  return { __typename: 'PullRequest', number, state, url: `https://github.com/${repo}/pull/${number}`, title, repository: { nameWithOwner: repo } };
}
// Fake GitHub. `nodes` = Development links; `timeline` = pages of PRs from cross-reference events;
// `search` = { [repoName]: [{ number, title, body }] }; `pulls` = live PR state for search hits.
function harness({ nodes, projectToken, board, omitGetOctokit = false, timeline = [[]], search = {}, pulls = {}, graphqlError, searchError, repo = 'Api', number = 244 }) {
  const calls = { update: [], comment: [], warn: [], graphql: [], search: [], summary: [] };
  const github = {
    graphql: async (q, vars) => {
      calls.graphql.push(vars);
      if (graphqlError) throw new Error(graphqlError);
      const pageIndex = vars.after ? Number(vars.after) : 0;
      const page = timeline[pageIndex] ?? [];
      return {
        repository: {
          issue: {
            closedByPullRequestsReferences: { nodes },
            timelineItems: {
              pageInfo: { hasNextPage: pageIndex + 1 < timeline.length, endCursor: String(pageIndex + 1) },
              nodes: page.map((p) => ({ __typename: 'CrossReferencedEvent', source: p })),
            },
          },
        },
      };
    },
    rest: {
      issues: {
        update: async (a) => calls.update.push(a),
        createComment: async (a) => calls.comment.push(a),
      },
      search: {
        issuesAndPullRequests: async ({ q }) => {
          calls.search.push(q);
          if (searchError) throw new Error(searchError);
          const r = q.match(/^repo:cultpodcasts\/(\S+)/)[1];
          return { data: { items: (search[r] ?? []).map((i) => ({ ...i, pull_request: {} })) } };
        },
      },
      pulls: {
        get: async ({ repo: r, pull_number }) => ({ data: pulls[`${r}#${pull_number}`] }),
      },
    },
  };
  const core = {
    info: () => {},
    warning: (m) => calls.warn.push(m),
    summary: { addRaw(t) { calls.summary.push(t); return this; }, write: async () => {} },
  };
  const context = { repo: { owner: 'cultpodcasts', repo }, payload: { issue: { number } } };
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
  assert.match(h.calls.comment[0].body, /restored to \*\*In Progress\*\*/);
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
// is not injected, so a bare `getOctokit` identifier threw "ReferenceError: getOctokit is not defined"
// before run() executed. The workflow passes it via `typeof` so a downgrade still reaches the .cjs fail-safe.
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
  // typeof does not throw when getOctokit is undeclared, so an accidental @v7 downgrade
  // still reaches run()'s fail-safe instead of dying at argument evaluation.
  assert.match(
    script,
    /getOctokit:\s*typeof getOctokit === 'function' \? getOctokit : undefined/,
    'script must not evaluate a bare getOctokit identifier'
  );
});

// ---- cultpodcasts/RedditPodcastPoster#1423: every open PR for the issue keeps it open ----

const RPP = 'cultpodcasts/RedditPodcastPoster';
const RPP_1400 = { owner: 'cultpodcasts', repo: 'RedditPodcastPoster', number: 1400 };

test('referencesIssue: Refs/Closes full ref and URL from any repo; bare #N only in the same repo', () => {
  const ref = (text, prRepo) => g.referencesIssue(text, { ...RPP_1400, prRepo });
  assert.equal(ref('Refs cultpodcasts/RedditPodcastPoster#1400.', 'cultpodcasts/Api'), true);
  assert.equal(ref('Closes cultpodcasts/RedditPodcastPoster#1400', 'cultpodcasts/website'), true);
  assert.equal(ref('See https://github.com/cultpodcasts/RedditPodcastPoster/issues/1400', 'cultpodcasts/website'), true);
  assert.equal(ref('Refs #1400', RPP), true);
  assert.equal(ref('Refs #1400', 'cultpodcasts/Api'), false, 'bare #1400 in Api means Api#1400');
  assert.equal(ref('Refs cultpodcasts/RedditPodcastPoster#14001', 'cultpodcasts/Api'), false);
  assert.equal(ref('Refs #14000', RPP), false);
  assert.equal(ref('', RPP), false);
});

test('openCompanions dedupes across sources and ignores PRs outside the owner', () => {
  const out = g.openCompanions(
    [pr('cultpodcasts/Api', 259, 'OPEN'), pr('cultpodcasts/Api', 259, 'OPEN'), pr('someone/fork', 1, 'OPEN'), pr(RPP, 1420, 'MERGED')],
    'cultpodcasts'
  );
  assert.deepEqual(out.map((p) => `${p.repo}#${p.number}`), ['cultpodcasts/Api#259']);
});

test('run: #1400 scenario - #1420 merged, Refs companions Api#259 + website#667 open -> reopens and lists both', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    repo: 'RedditPodcastPoster',
    number: 1400,
    nodes: [pr(RPP, 1420, 'MERGED')], // only the Closes PR is a Development link
    timeline: [[pr(RPP, 1420, 'MERGED'), pr('cultpodcasts/Api', 259, 'OPEN'), pr('cultpodcasts/website', 667, 'OPEN')]],
    projectToken: 'pat',
    board,
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.equal(h.calls.update.length, 1);
  assert.equal(h.calls.update[0].issue_number, 1400);
  assert.deepEqual(r.companions.map((p) => `${p.repo}#${p.number}`), ['cultpodcasts/Api#259', 'cultpodcasts/website#667']);
  assert.match(h.calls.comment[0].body, /cultpodcasts\/Api#259/);
  assert.match(h.calls.comment[0].body, /cultpodcasts\/website#667/);
  assert.match(h.calls.comment[0].body, /ALL of its PRs are merged/);
  assert.match(h.calls.summary.join(''), /Reopened cultpodcasts\/RedditPodcastPoster#1400/);
});

test('run: timeline is paginated so a companion on a later page is still found', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    repo: 'RedditPodcastPoster',
    number: 1400,
    nodes: [pr(RPP, 1420, 'MERGED')],
    timeline: [[pr(RPP, 1420, 'MERGED')], [pr('cultpodcasts/website', 667, 'OPEN')]],
    projectToken: 'pat',
    board,
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.equal(h.calls.graphql.length, 2);
  assert.deepEqual(r.companions.map((p) => p.number), [667]);
});

test('run: search safety net finds an open Refs PR the timeline missed, confirmed by live state', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    repo: 'RedditPodcastPoster',
    number: 1400,
    nodes: [pr(RPP, 1420, 'MERGED')],
    search: { Api: [{ number: 259, title: 'api', body: 'Refs cultpodcasts/RedditPodcastPoster#1400.' }] },
    pulls: { 'Api#259': { number: 259, state: 'open', merged_at: null, html_url: 'u259', title: 'api' } },
    projectToken: 'pat',
    board,
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.deepEqual(r.companions.map((p) => `${p.repo}#${p.number}`), ['cultpodcasts/Api#259']);
  assert.equal(h.calls.search.length, 3, 'searches RPP, Api and website');
});

test('run: stale search hit that is already merged does not reopen', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    repo: 'RedditPodcastPoster',
    number: 1400,
    nodes: [pr(RPP, 1420, 'MERGED')],
    search: { website: [{ number: 667, title: 'web', body: 'Closes cultpodcasts/RedditPodcastPoster#1400' }] },
    pulls: { 'website#667': { number: 667, state: 'closed', merged_at: '2026-11-14T00:00:00Z', html_url: 'u', title: 'web' } },
    projectToken: 'pat',
    board,
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, false);
  assert.equal(h.calls.update.length, 0);
  assert.match(h.calls.summary.join(''), /left closed/);
});

test('run: search hit whose body only says bare #1400 from another repo is not a companion', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    repo: 'RedditPodcastPoster',
    number: 1400,
    nodes: [],
    search: { Api: [{ number: 5, title: 'x', body: 'Fixes #1400' }] },
    pulls: { 'Api#5': { number: 5, state: 'open', merged_at: null, html_url: 'u', title: 'x' } },
    projectToken: 'pat',
    board,
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, false);
});

test('run: search failure warns but Development links / cross-references still reopen', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    repo: 'RedditPodcastPoster',
    number: 1400,
    nodes: [pr(RPP, 1420, 'MERGED')],
    timeline: [[pr('cultpodcasts/Api', 259, 'OPEN')]],
    searchError: 'rate limited',
    projectToken: 'pat',
    board,
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.ok(h.calls.warn.some((m) => /Search for open PRs .* failed: rate limited/.test(m)));
});

test('run: GraphQL failure rejects (job fails visibly) instead of leaving the issue closed silently', async () => {
  const board = fakeBoard('Done');
  const h = harness({ nodes: [], graphqlError: 'Resource not accessible', projectToken: 'pat', board });
  await assert.rejects(() => g.run(h.args), /Resource not accessible/);
  assert.equal(h.calls.update.length, 0);
});
