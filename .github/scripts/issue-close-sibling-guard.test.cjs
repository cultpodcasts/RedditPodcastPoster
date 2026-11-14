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
// indices before which the board's "Item closed" automation sets Done (late landing);
// `closedBeforeRead` lists read indices before which a newer merge closes the issue
// again (the automation then also sets Done). `issueState` is what the board query
// sees for the issue (OPEN after the guard reopened it).
function fakeBoard(initial, automationDoneBeforeRead = [], { issueState = 'OPEN', closedBeforeRead = [] } = {}) {
  let status = initial;
  let state = issueState;
  let reads = 0;
  const writes = [];
  const graphql = async (q, vars) => {
    if (q.includes('updateProjectV2ItemFieldValue')) {
      writes.push(vars);
      status = OPTIONS.find((o) => o.id === vars.option).name;
      return {};
    }
    if (automationDoneBeforeRead.includes(reads)) status = 'Done';
    if (closedBeforeRead.includes(reads)) {
      state = 'CLOSED';
      status = 'Done';
    }
    reads++;
    return { repository: { issue: { state, projectItems: { nodes: [item(status)] } } } };
  };
  return { graphql, writes, get status() { return status; } };
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

function pr(repo, number, state, body = '', title = `t${number}`) {
  return { __typename: 'PullRequest', number, state, url: `https://github.com/${repo}/pull/${number}`, title, body, repository: { nameWithOwner: repo } };
}
// Fake GitHub. `nodes` = Development links; `timeline` = pages of PRs from cross-reference events;
// `search` = { [repoName]: [{ number }] } hits; `pulls` = live PR (state/title/body) for search hits.
// `issueStates`: successive live issue states from issues.get (last repeats).
// `pulls[repo#n]`: a live PR, or an array of successive live PRs (last repeats); a
// companion with no entry reads as open. `searchTotal`: override search total_count.
function harness({ nodes, projectToken, board, omitGetOctokit = false, timeline = [[]], search = {}, pulls = {}, graphqlError, searchError, searchTotal, repo = 'Api', number = 244, issueStates = ['closed'] }) {
  const calls = { update: [], comment: [], warn: [], graphql: [], search: [], summary: [], order: [], editComment: [], issueGets: 0 };
  const pullReads = {};
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
        get: async () => {
          const st = issueStates[Math.min(calls.issueGets, issueStates.length - 1)];
          calls.issueGets++;
          return { data: { state: st } };
        },
        update: async (a) => {
          calls.update.push(a);
          calls.order.push(a.state === 'open' ? 'reopen' : 'close');
        },
        createComment: async (a) => {
          calls.comment.push(a);
          calls.order.push('comment');
          return { data: { id: 900 + calls.comment.length } };
        },
        updateComment: async (a) => {
          calls.editComment.push(a);
          calls.order.push('editComment');
        },
      },
      search: {
        issuesAndPullRequests: async ({ q }) => {
          calls.search.push(q);
          const r = q.match(/^repo:cultpodcasts\/(\S+)/)[1];
          if (searchError && searchError.repo === r) throw new Error(searchError.message);
          const items = (search[r] ?? []).map((i) => ({ ...i, pull_request: {} }));
          return { data: { incomplete_results: false, total_count: searchTotal?.[r] ?? items.length, items } };
        },
      },
      pulls: {
        get: async ({ repo: r, pull_number }) => {
          const key = `${r}#${pull_number}`;
          const v = pulls[key];
          const n = (pullReads[key] = (pullReads[key] ?? 0) + 1) - 1;
          const live = Array.isArray(v) ? v[Math.min(n, v.length - 1)] : v;
          return { data: live ?? { number: pull_number, state: 'open', merged_at: null, html_url: `u${pull_number}`, title: `t${pull_number}`, body: '' } };
        },
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
  assert.equal(h.calls.comment.length, 1, 'no board warning comment when the card was restored');
  assert.equal(board.status, 'In Progress');
  assert.equal(h.calls.warn.length, 0);
});

test('run: missing PROJECT_TOKEN reopens but warns loudly in job and comment', async () => {
  const board = fakeBoard('Done');
  const h = harness({ nodes: OPEN_247, projectToken: '', board, issueStates: ['closed', 'closed', 'open'] });
  await g.run(h.args);
  assert.equal(h.calls.update.length, 1);
  assert.equal(board.writes.length, 0);
  assert.match(h.calls.warn[0], /PROJECT_TOKEN secret is not set/);
  assert.match(h.calls.comment[1].body, /NOT restored/);
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
  const h = harness({ nodes: OPEN_247, projectToken: 'pat', board, omitGetOctokit: true, issueStates: ['closed', 'closed', 'open'] });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.equal(h.calls.update.length, 1);
  assert.equal(h.calls.update[0].state, 'open');
  assert.equal(board.writes.length, 0);
  assert.match(h.calls.warn[0], /getOctokit is not available/);
  assert.match(h.calls.comment[1].body, /NOT restored \(getOctokit is not available/);
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

// ---- cultpodcasts/RedditPodcastPoster#1423: every open PR that CLOSES the issue keeps it open ----

const RPP = 'cultpodcasts/RedditPodcastPoster';
const RPP_1400 = { owner: 'cultpodcasts', repo: 'RedditPodcastPoster', number: 1400 };
const CLOSES_1400 = 'Closes cultpodcasts/RedditPodcastPoster#1400. RPP: cultpodcasts/RedditPodcastPoster#1420.';
const livePr = (number, body, state = 'open', merged_at = null) => ({ number, state, merged_at, html_url: `u${number}`, title: `t${number}`, body });

test('closesIssue: closing keyword + full ref / URL from any repo; bare #N only from the same repo', () => {
  const c = (text, prRepo) => g.closesIssue(text, { ...RPP_1400, prRepo });
  assert.equal(c('Closes cultpodcasts/RedditPodcastPoster#1400.', 'cultpodcasts/Api'), true);
  assert.equal(c('Fixes: #1400', RPP), true);
  assert.equal(c('resolves https://github.com/cultpodcasts/RedditPodcastPoster/issues/1400', 'cultpodcasts/website'), true);
  assert.equal(c('Closes #1400', 'cultpodcasts/Api'), false, 'bare #1400 in Api means Api#1400');
  assert.equal(c('Closes cultpodcasts/RedditPodcastPoster#14001', 'cultpodcasts/Api'), false);
  assert.equal(c('encloses #1400', RPP), false);
  assert.equal(c('', RPP), false);
});

test('closesIssue: Refs, prose, code spans and HTML comments are not companions', () => {
  const c = (text, prRepo = RPP) => g.closesIssue(text, { ...RPP_1400, prRepo });
  assert.equal(c('Refs cultpodcasts/RedditPodcastPoster#1400', 'cultpodcasts/Api'), false);
  assert.equal(c('Refs #1400'), false);
  assert.equal(c('see #1400; the #1400 toasts are separate'), false);
  assert.equal(c('Api#259 said `Closes cultpodcasts/RedditPodcastPoster#1400`', 'cultpodcasts/Api'), false);
  assert.equal(c('<!-- Closes #1400 -->'), false);
  assert.equal(c('```\nCloses #1400\n```'), false);
});

test('openCompanions dedupes across sources and ignores PRs outside the owner', () => {
  const out = g.openCompanions(
    [pr('cultpodcasts/Api', 259, 'OPEN'), pr('cultpodcasts/Api', 259, 'OPEN'), pr('someone/fork', 1, 'OPEN'), pr(RPP, 1420, 'MERGED')],
    'cultpodcasts'
  );
  assert.deepEqual(out.map((p) => `${p.repo}#${p.number}`), ['cultpodcasts/Api#259']);
});

const RPP_H = { repo: 'RedditPodcastPoster', number: 1400, projectToken: 'pat' };

test('run: #1400 scenario - #1420 merged, Api#259 + website#667 linked under Development -> reopens and lists both', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    ...RPP_H,
    board,
    nodes: [pr(RPP, 1420, 'MERGED'), pr('cultpodcasts/Api', 259, 'OPEN'), pr('cultpodcasts/website', 667, 'OPEN')],
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.equal(h.calls.update[0].issue_number, 1400);
  assert.deepEqual(r.companions.map((p) => `${p.repo}#${p.number}`), ['cultpodcasts/Api#259', 'cultpodcasts/website#667']);
  assert.match(h.calls.comment[0].body, /cultpodcasts\/Api#259/);
  assert.match(h.calls.comment[0].body, /ALL of its PRs are merged/);
  assert.match(h.calls.summary.join(''), /Reopened cultpodcasts\/RedditPodcastPoster#1400/);
});

test('run: timeline candidate with a Closes line (link not yet indexed) reopens', async () => {
  const board = fakeBoard('Done');
  const h = harness({ ...RPP_H, board, nodes: [pr(RPP, 1420, 'MERGED')], timeline: [[pr('cultpodcasts/Api', 259, 'OPEN', CLOSES_1400)]] });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.deepEqual(r.companions.map((p) => p.number), [259]);
});

test('run: Refs-only companion line does not hold the issue open (Closes is the convention)', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    ...RPP_H,
    board,
    nodes: [pr(RPP, 1420, 'MERGED')],
    timeline: [[pr('cultpodcasts/Api', 259, 'OPEN', 'Refs cultpodcasts/RedditPodcastPoster#1400.')]],
  });
  assert.equal((await g.run(h.args)).reopened, false);
});

test('run: prose-only mention ("see #N", "the #N") in an open PR body does not reopen', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    ...RPP_H,
    board,
    nodes: [pr(RPP, 1420, 'MERGED')],
    // e.g. the #1423 guard PRs, which explain the #1400 incident in prose
    timeline: [[pr(RPP, 1424, 'OPEN', 'Closes #1423\n\nsee #1400; the #1400 companions said `Refs cultpodcasts/RedditPodcastPoster#1400`')]],
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, false);
  assert.equal(h.calls.update.length, 0);
});

test('run: comment-only / commit-only cross-reference (body never names the issue) does not reopen', async () => {
  const board = fakeBoard('Done');
  // GitHub adds a CrossReferencedEvent when a PR comment, review comment or commit names the issue;
  // the PR's live title/body do not, so it is not a companion.
  const h = harness({ ...RPP_H, board, nodes: [], timeline: [[pr('cultpodcasts/website', 665, 'OPEN', 'Refs #664 (website). Attach as trailer tidy-up.')]] });
  const r = await g.run(h.args);
  assert.equal(r.reopened, false);
});

test('run: timeline is paginated so a Closes companion on a later page is still found', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    ...RPP_H,
    board,
    nodes: [pr(RPP, 1420, 'MERGED')],
    timeline: [[pr(RPP, 1420, 'MERGED')], [pr('cultpodcasts/website', 667, 'OPEN', CLOSES_1400)]],
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.equal(h.calls.graphql.length, 2);
  assert.deepEqual(r.companions.map((p) => p.number), [667]);
});

test('run: search safety net reopens only for a hit whose LIVE body closes the issue', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    ...RPP_H,
    board,
    nodes: [pr(RPP, 1420, 'MERGED')],
    search: { Api: [{ number: 259 }, { number: 260 }] },
    pulls: { 'Api#259': livePr(259, CLOSES_1400), 'Api#260': livePr(260, 'Closes cultpodcasts/RedditPodcastPoster#1423 (mentions issue 1400 in prose)') },
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.deepEqual(r.companions.map((p) => `${p.repo}#${p.number}`), ['cultpodcasts/Api#259']);
  assert.equal(h.calls.search.length, 3, 'searches RPP, Api and website');
});

test('run: stale search hit that is already merged does not reopen', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    ...RPP_H,
    board,
    nodes: [pr(RPP, 1420, 'MERGED')],
    search: { website: [{ number: 667 }] },
    pulls: { 'website#667': livePr(667, CLOSES_1400, 'closed', '2026-11-14T00:00:00Z') },
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, false);
  assert.match(h.calls.summary.join(''), /left closed/);
});

test('run: search hit whose body says bare "Fixes #1400" from another repo is not a companion', async () => {
  const board = fakeBoard('Done');
  const h = harness({ ...RPP_H, board, nodes: [], search: { Api: [{ number: 5 }] }, pulls: { 'Api#5': livePr(5, 'Fixes #1400') } });
  assert.equal((await g.run(h.args)).reopened, false);
});

test('run: a failed search rejects (job fails) instead of a green "no companions"', async () => {
  const board = fakeBoard('Done');
  const h = harness({ ...RPP_H, board, nodes: [pr(RPP, 1420, 'MERGED')], searchError: { repo: 'website', message: 'rate limited' } });
  await assert.rejects(() => g.run(h.args), /open-PR search incomplete.*cultpodcasts\/website: rate limited/);
  assert.equal(h.calls.update.length, 0);
  assert.equal(h.calls.comment.length, 0);
  assert.equal(h.calls.search.length, 3, 'still tries every repo, then fails with all errors');
});

test('run: GraphQL failure rejects (job fails visibly) instead of leaving the issue closed silently', async () => {
  const board = fakeBoard('Done');
  const h = harness({ nodes: [], graphqlError: 'Resource not accessible', projectToken: 'pat', board });
  await assert.rejects(() => g.run(h.args), /Resource not accessible/);
  assert.equal(h.calls.update.length, 0);
});

// ---- cultpodcasts/RedditPodcastPoster#1425: rapid multi-PR merges race ----

const ISSUE_1423 = { repo: 'RedditPodcastPoster', number: 1423, projectToken: 'pat' };
const COMPANIONS_1423 = [pr(RPP, 1424, 'MERGED'), pr('cultpodcasts/Api', 260, 'OPEN'), pr('cultpodcasts/website', 668, 'OPEN')];
const MERGED = (n) => ({ number: n, state: 'closed', merged_at: '2026-11-14T20:54:33Z', html_url: `u${n}`, title: `t${n}`, body: '' });

test('workflow: runs are serialised per issue without cancelling an in-progress run', () => {
  const yml = fs.readFileSync(path.join(__dirname, '..', 'workflows', 'issue-close-sibling-guard.yml'), 'utf8');
  assert.match(yml, /concurrency:\s*\n\s*group:\s*issue-close-sibling-guard-\$\{\{\s*github\.repository\s*\}\}-\$\{\{\s*github\.event\.issue\.number\s*\}\}/);
  assert.match(yml, /cancel-in-progress:\s*false/);
});

test('run: issue already reopened by an earlier (serialised) run -> no side effects', async () => {
  const board = fakeBoard('In Progress');
  const h = harness({ ...ISSUE_1423, board, nodes: COMPANIONS_1423, issueStates: ['open'] });
  const r = await g.run(h.args);
  assert.equal(r.skipped, 'issue-open');
  assert.deepEqual(h.calls.order, []);
  assert.equal(board.writes.length, 0);
});

test('run: comment is posted before the reopen and lists only live-open companions', async () => {
  const board = fakeBoard('Done');
  const h = harness({ ...ISSUE_1423, board, nodes: COMPANIONS_1423, pulls: { 'Api#260': MERGED(260) } });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.deepEqual(h.calls.order, ['comment', 'reopen']);
  assert.doesNotMatch(h.calls.comment[0].body, /Api#260/, 'Api#260 merged after the lookup; not listed');
  assert.match(h.calls.comment[0].body, /cultpodcasts\/website#668/);
});

test('run: every companion merged after the lookup -> no comment, no reopen (the newer close is final)', async () => {
  const board = fakeBoard('Done');
  const h = harness({ ...ISSUE_1423, board, nodes: COMPANIONS_1423, pulls: { 'Api#260': MERGED(260), 'website#668': MERGED(668) } });
  const r = await g.run(h.args);
  assert.equal(r.reopened, false);
  assert.deepEqual(h.calls.order, []);
  assert.equal(board.writes.length, 0);
});

test('run: issue reopened by someone else between lookup and side effects -> skip', async () => {
  const board = fakeBoard('Done');
  const h = harness({ ...ISSUE_1423, board, nodes: COMPANIONS_1423, issueStates: ['closed', 'open'] });
  const r = await g.run(h.args);
  assert.equal(r.skipped, 'issue-open');
  assert.deepEqual(h.calls.order, []);
});

test('run: companion merges between the comment and the reopen -> issue closed again, comment updated', async () => {
  const board = fakeBoard('Done');
  const h = harness({
    ...ISSUE_1423,
    board,
    nodes: [pr(RPP, 1424, 'MERGED'), pr('cultpodcasts/website', 668, 'OPEN')],
    // read 1: lookup-time recheck (open); read 2: after the reopen (merged)
    pulls: { 'website#668': [{ number: 668, state: 'open', merged_at: null, html_url: 'u', title: 't', body: '' }, MERGED(668)] },
  });
  const r = await g.run(h.args);
  assert.equal(r.reclosed, true);
  assert.deepEqual(h.calls.order, ['comment', 'reopen', 'close', 'editComment']);
  assert.equal(h.calls.update[1].state_reason, 'completed');
  assert.match(h.calls.editComment[0].body, /closed again/);
  assert.equal(board.writes.length, 0, 'card never moved off Done');
});

test('board: a newer merge closes the issue during the restore loop -> stop and put the card back on Done', async () => {
  // read0: OPEN/Done -> write In Progress; read1: newer merge closed it (automation set Done) -> stop, no write
  const board = fakeBoard('Done', [], { closedBeforeRead: [1] });
  const r = await g.restoreBoardStatus({ graphql: board.graphql, owner: 'cultpodcasts', repo: 'RedditPodcastPoster', number: 1423, sleep: noSleep });
  assert.equal(r.restored, false);
  assert.equal(r.closedAgain, true);
  assert.equal(board.status, 'Done');
  assert.equal(board.writes.length, 1, 'only the first In Progress write; never re-asserted on a closed issue');
});

test('board: closed issue whose card a late write left on In Progress is moved back to Done', async () => {
  const board = fakeBoard('In Progress', [], { issueState: 'CLOSED' });
  const r = await g.restoreBoardStatus({ graphql: board.graphql, owner: 'cultpodcasts', repo: 'RedditPodcastPoster', number: 1423, sleep: noSleep });
  assert.equal(r.closedAgain, true);
  assert.equal(board.status, 'Done');
  assert.deepEqual(board.writes.map((w) => w.option), ['done']);
});

test('board: closed issue on Done is never written', async () => {
  const board = fakeBoard('Done', [], { issueState: 'CLOSED' });
  await g.restoreBoardStatus({ graphql: board.graphql, owner: 'cultpodcasts', repo: 'RedditPodcastPoster', number: 1423, sleep: noSleep });
  assert.equal(board.writes.length, 0);
});

test('run: final close with no open companions settles a stray In Progress card to Done (the #1423 card)', async () => {
  const board = fakeBoard('In Progress', [], { issueState: 'CLOSED' });
  const h = harness({ ...ISSUE_1423, board, nodes: [pr(RPP, 1424, 'MERGED'), pr('cultpodcasts/Api', 260, 'MERGED'), pr('cultpodcasts/website', 668, 'MERGED')] });
  const r = await g.run(h.args);
  assert.equal(r.reopened, false);
  assert.equal(r.settled, true);
  assert.equal(board.status, 'Done');
  assert.deepEqual(h.calls.order, []);
});

test('run: final close leaves a Ready For Review / Todo card alone (only stray In Progress is corrected)', async () => {
  const board = fakeBoard('Ready For Review', [], { issueState: 'CLOSED' });
  const h = harness({ ...ISSUE_1423, board, nodes: [] });
  const r = await g.run(h.args);
  assert.equal(r.settled, false);
  assert.equal(board.writes.length, 0);
});

test('run: #1423 replay - three merges, each run sees live state; no stale comment after the final close', async () => {
  // Run for the LAST close (website#668 merged): every companion is merged live.
  const board = fakeBoard('In Progress', [], { issueState: 'CLOSED' });
  const h = harness({
    ...ISSUE_1423,
    board,
    // the lookup can still be stale (Development link says OPEN) ...
    nodes: [pr(RPP, 1424, 'MERGED'), pr('cultpodcasts/Api', 260, 'OPEN'), pr('cultpodcasts/website', 668, 'OPEN')],
    // ... but the live re-check right before side effects says merged
    pulls: { 'Api#260': MERGED(260), 'website#668': MERGED(668) },
  });
  const r = await g.run(h.args);
  assert.equal(r.reopened, false);
  assert.deepEqual(h.calls.order, [], 'no comment, no reopen');
  assert.equal(board.status, 'Done');
});

test('search: total_count above the results read fails loudly', async () => {
  const board = fakeBoard('Done');
  const h = harness({ ...RPP_H, board, nodes: [], search: { Api: [{ number: 1 }] }, searchTotal: { Api: 101 }, pulls: { 'Api#1': livePr(1, '') } });
  await assert.rejects(() => g.run(h.args), /cultpodcasts\/Api: search matched 101 PRs but only 1 were read/);
  assert.deepEqual(h.calls.order, []);
});

test('run: board warning comment is skipped when the issue was closed again meanwhile', async () => {
  const board = fakeBoard('Done');
  const h = harness({ nodes: OPEN_247, projectToken: '', board, issueStates: ['closed', 'closed', 'closed'] });
  const r = await g.run(h.args);
  assert.equal(r.reopened, true);
  assert.deepEqual(h.calls.order, ['comment', 'reopen'], 'no late "NOT restored" comment on a re-closed issue');
});
