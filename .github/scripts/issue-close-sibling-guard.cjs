// Issue close sibling guard (mirrored across RedditPodcastPoster, website, Api).
//
// GitHub auto-closes an issue when ANY linked PR with Closes/Fixes merges, and the
// features board's built-in "Item closed" workflow then sets Status = Done.
// Rule (Jon, cultpodcasts/RedditPodcastPoster#1423): a ticket with PRs in several
// repos stays open until ALL of its PRs are merged. When any other PR for the issue
// is still open, this reopens the issue and puts the board card back in the column it
// was in BEFORE the close (cultpodcasts/RedditPodcastPoster#1514, Jon: "it should stay
// in the column it is in until I merge the final PR"; then it goes to Done). That
// column is read from the issue's ProjectV2ItemStatusChangedEvent history; In
// Progress is only the fallback when the card has no status history.
//
// "PR for the issue" = an OPEN pull request in a cultpodcasts repo that is either
//   1. a Development link (closedByPullRequestsReferences: a closing keyword in the
//      PR body, or linked by hand), or
//   2. a candidate from the issue timeline (cross-reference) or a search across the
//      app trio whose LIVE title/body has an explicit closing keyword for the issue:
//      `Closes|Fixes|Resolves #N` from the same repo, or
//      `Closes|Fixes|Resolves owner/repo#N` (or the issue URL) from any repo.
//      Prose mentions ("see #N", `Refs #N`) and comment- or commit-only mentions do
//      NOT count, so a PR that merely talks about an issue cannot hold it open.
// #1423: since 6d890dda only (1) was checked, and #1400's companions said `Refs`, so
// they were neither linked nor counted. Convention now: every companion says
// `Closes ...#N` (so it is linked under Development); (2) only covers link lag.
//
// Fail loud: any lookup that cannot complete (GraphQL, search, PR fetch, a search
// with more hits than were read) throws, so the job fails instead of reporting a
// green "no companions".
//
// Races (cultpodcasts/RedditPodcastPoster#1425: three merges ~35 s apart left stale
// reopen comments after the final close and a closed issue's card on In Progress):
// - the workflow serialises runs per issue (concurrency group, no cancel-in-progress);
// - every side effect is preceded by a LIVE re-check of the issue state and of each
//   companion PR's state; the run skips when the issue is already open again, or
//   when every companion has merged since (a newer close is then final);
// - the comment is posted BEFORE the reopen, so it never lists a PR that had merged
//   by the time the issue reopened; if a companion merges in the gap the issue is
//   re-closed straight away;
// - the board card is only ever moved off Done while the issue is open, and a closed
//   issue's card is never moved off Done (a stray restored column is put back).
// Only issues closed as completed (or with no reason) are guarded: `not_planned` and
// `duplicate` closes are skipped by the job condition AND by a live state_reason check
// before any side effect, so a curator's reason is never overwritten.
//
// Board writes need PROJECT_TOKEN (classic PAT with `project` scope): the
// repo-scoped GITHUB_TOKEN cannot see user-owned Projects V2
// ("Could not resolve to a ProjectV2 with the number 1").

'use strict';

const BOARD_OWNER = 'cultpodcasts';
const BOARD_NUMBER = 1;
const STATUS_FIELD = 'Status';
// Fallback only: used when the card has no status history to restore from (#1514).
const RESTORE_STATUS = 'In Progress';
const DONE_STATUS = 'Done';
const TRIO = ['RedditPodcastPoster', 'Api', 'website'];

function escapeRegExp(t) {
  return t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

// Remove text GitHub does not treat as closing keywords: HTML comments (PR template
// guidance), fenced code blocks and inline code spans.
function stripNonProse(text) {
  return String(text)
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/`[^`\n]*`/g, ' ');
}

const CLOSING = '(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)';

// Does `text` (a PR's live title + body) carry an explicit closing keyword for
// owner/repo#number? `prRepo` is the PR's own "owner/name": a bare `#N` only means
// this issue inside the same repo. Refs / prose mentions are not companions.
function closesIssue(text, { owner, repo, number, prRepo }) {
  if (!text) return false;
  const t = stripNonProse(text);
  const full = escapeRegExp(`${owner}/${repo}`);
  const n = String(number);
  const lead = `(?:^|[^\\w])${CLOSING}:?\\s+`;
  const refs = [`${full}#${n}`, `https?://github\\.com/${full}/issues/${n}`];
  if (prRepo && prRepo.toLowerCase() === `${owner}/${repo}`.toLowerCase()) refs.push(`#${n}`);
  return new RegExp(`${lead}(?:${refs.join('|')})(?![\\w/])`, 'i').test(t);
}

function openCompanions(nodes, owner) {
  const seen = new Set();
  const out = [];
  for (const p of nodes ?? []) {
    if (!p || p.state !== 'OPEN' || !p.repository?.nameWithOwner) continue;
    const repoName = p.repository.nameWithOwner;
    if (owner && repoName.split('/')[0].toLowerCase() !== owner.toLowerCase()) continue;
    const key = `${repoName}#${p.number}`.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    out.push({ repo: repoName, number: p.number, url: p.url, title: p.title });
  }
  return out;
}

function pickBoardItem(projectItems, owner = BOARD_OWNER, number = BOARD_NUMBER) {
  return (projectItems ?? []).find(
    (n) => n && n.project && n.project.number === number && n.project.owner?.login === owner
  );
}

const MANUAL_RESTORE = 'set Status back to the column it was in before the close (e.g. Ready For Review) by hand';

function buildComment(companions, board = {}) {
  const list = companions.map((p) => `- ${p.repo}#${p.number}: ${p.title} — ${p.url}`).join('\n');
  let boardLine;
  if (board.restored) {
    boardLine = `Board card restored to **${board.status ?? RESTORE_STATUS}**.`;
  } else if (board.reason) {
    boardLine = `⚠️ Board card NOT restored (${board.reason}); ${MANUAL_RESTORE}.`;
  } else {
    boardLine = '';
  }
  return (
    `Reopening automatically: this was closed while **open PR(s)** for it remain:\n\n` +
    `${list}\n\n` +
    `A ticket with PRs in several repos stays open until ALL of its PRs are merged (or unlinked). ` +
    `Every PR for a ticket says \`Closes #N\` (same repo) or \`Closes <owner>/<repo>#N\` (other repo) so it shows under Development; this guard undoes premature closes.` +
    (boardLine ? `\n\n${boardLine}` : '')
  );
}

const ITEMS_QUERY = `
  query($owner: String!, $repo: String!, $number: Int!) {
    repository(owner: $owner, name: $repo) {
      issue(number: $number) {
        state
        projectItems(first: 20, includeArchived: true) {
          nodes {
            id
            project {
              id
              number
              owner { ... on User { login } ... on Organization { login } }
              field(name: "${STATUS_FIELD}") {
                ... on ProjectV2SingleSelectField { id options { id name } }
              }
            }
            fieldValueByName(name: "${STATUS_FIELD}") {
              ... on ProjectV2ItemFieldSingleSelectValue { name optionId }
            }
          }
        }
        timelineItems(last: 100, itemTypes: [PROJECT_V2_ITEM_STATUS_CHANGED_EVENT]) {
          nodes {
            ... on ProjectV2ItemStatusChangedEvent {
              createdAt
              previousStatus
              status
              project { number owner { ... on User { login } ... on Organization { login } } }
            }
          }
        }
      }
    }
  }
`;

const SET_STATUS = `
  mutation($project: ID!, $item: ID!, $field: ID!, $option: String!) {
    updateProjectV2ItemFieldValue(input: {
      projectId: $project, itemId: $item, fieldId: $field,
      value: { singleSelectOptionId: $option }
    }) { projectV2Item { id } }
  }
`;

// The column the card was in before the close (#1514). Scans this board's
// ProjectV2ItemStatusChangedEvent history newest-first and returns the newest
// status that is not Done: the "Item closed" automation's Done event carries the
// pre-close column as previousStatus, and if that event has not landed yet the
// newest event's status IS the pre-close column. Repeated closes (Done -> Done
// chains) are skipped the same way. null when there is no usable history.
function columnBeforeClose(events, owner = BOARD_OWNER, number = BOARD_NUMBER) {
  const mine = (events ?? []).filter(
    (e) => e && e.project && e.project.number === number && e.project.owner?.login === owner
  );
  for (let i = mine.length - 1; i >= 0; i--) {
    for (const s of [mine[i].status, mine[i].previousStatus]) {
      if (s && s !== DONE_STATUS) return s;
    }
  }
  return null;
}

function boardContext(res) {
  const issue = res.repository?.issue;
  const item = pickBoardItem(issue?.projectItems?.nodes);
  if (!item) return { error: `issue is not on ${BOARD_OWNER} project ${BOARD_NUMBER}` };
  const field = item.project.field;
  const option = (name) => field?.options?.find((o) => o.name === name);
  if (!field?.id) return { error: `no ${STATUS_FIELD} field on ${BOARD_OWNER} project ${BOARD_NUMBER}` };
  const current = item.fieldValueByName?.name ?? null;
  // Column to restore: pre-close history, else the live column if it is not Done
  // (automation not landed and no history), else the In Progress fallback.
  const target =
    columnBeforeClose(issue?.timelineItems?.nodes) ?? (current && current !== DONE_STATUS ? current : RESTORE_STATUS);
  return { issueState: issue.state, item, field, option, current, target };
}

// The board's "Item closed" automation can land before OR after this job, so keep
// re-asserting the pre-close column until it reads back stable twice in a row. The
// column is fixed from the FIRST read (later reads would also see this job's own
// writes). Only Done / empty is ever overwritten: a card a human moved to another
// column meanwhile is left where it is. Each pass re-reads the LIVE issue state:
// once the issue is closed again (a newer merge), stop and never leave the card
// off Done (#1425).
async function restoreBoardStatus({ graphql, owner, repo, number, sleep, attempts = 8, delayMs = 5000, log = () => {} }) {
  let stable = 0;
  let wrote = false;
  let target = null;
  for (let i = 0; i < attempts; i++) {
    const ctx = boardContext(await graphql(ITEMS_QUERY, { owner, repo, number }));
    if (ctx.error) return { restored: false, reason: ctx.error };
    const { issueState, item, field, option, current } = ctx;
    if (target === null) target = ctx.target;
    if (issueState !== 'OPEN') {
      const done = option(DONE_STATUS);
      if (current !== DONE_STATUS && done && (wrote || current === target)) {
        log(`Issue is closed again; putting the card back on ${DONE_STATUS}.`);
        await graphql(SET_STATUS, { project: item.project.id, item: item.id, field: field.id, option: done.id });
      }
      return { restored: false, closedAgain: true, reason: `issue was closed again; card left on ${DONE_STATUS}` };
    }
    if (current === target) {
      stable++;
      if (stable >= 2) return { restored: true, wrote, status: target };
    } else if (current && current !== DONE_STATUS) {
      log(`Board Status is ${current} (moved meanwhile, not ${target}); leaving it there.`);
      return { restored: true, wrote, status: current };
    } else {
      const opt = option(target);
      if (!opt) return { restored: false, reason: `no "${target}" option on ${STATUS_FIELD}` };
      stable = 0;
      log(`Board Status is ${current ?? '(empty)'}; restoring ${target}.`);
      await graphql(SET_STATUS, { project: item.project.id, item: item.id, field: field.id, option: opt.id });
      wrote = true;
    }
    if (i < attempts - 1) await sleep(delayMs);
  }
  return stable >= 1 ? { restored: true, wrote, status: target } : { restored: false, reason: 'status kept reverting' };
}

// Closed issue with no open companions: the final PR merged (or the ticket is
// otherwise complete), so its card belongs on Done (Jon, #1514). Any column a guard
// run may have restored (In Progress, Ready For Review, Blocked, ...) is moved to
// Done, the same as the board's own "Item closed" automation does.
async function settleClosedCard({ graphql, owner, repo, number, log = () => {} }) {
  const ctx = boardContext(await graphql(ITEMS_QUERY, { owner, repo, number }));
  if (ctx.error || ctx.issueState === 'OPEN') return { settled: false };
  const done = ctx.option(DONE_STATUS);
  if (!ctx.current || ctx.current === DONE_STATUS || !done) return { settled: false };
  log(`Closed issue's card is ${ctx.current}; moving it to ${DONE_STATUS}.`);
  await graphql(SET_STATUS, { project: ctx.item.project.id, item: ctx.item.id, field: ctx.field.id, option: done.id });
  return { settled: true };
}

const LINKS_QUERY = `
  query($owner: String!, $repo: String!, $number: Int!, $after: String) {
    repository(owner: $owner, name: $repo) {
      issue(number: $number) {
        closedByPullRequestsReferences(first: 50, includeClosedPrs: true) {
          nodes { number state url title repository { nameWithOwner } }
        }
        timelineItems(first: 100, after: $after, itemTypes: [CROSS_REFERENCED_EVENT]) {
          pageInfo { hasNextPage endCursor }
          nodes {
            __typename
            ... on CrossReferencedEvent {
              source { __typename ... on PullRequest { number state url title body repository { nameWithOwner } } }
            }
          }
        }
      }
    }
  }
`;

// Development links (trusted) + timeline cross-reference candidates (paginated;
// manual links already appear in closedByPullRequestsReferences, so CONNECTED /
// DISCONNECTED events are not needed). GraphQL errors throw (job fails visibly).
async function linkedPullRequests(graphql, { owner, repo, number }) {
  const linked = [];
  const candidates = [];
  let after = null;
  let complete = false;
  for (let page = 0; page < 20; page++) {
    const res = await graphql(LINKS_QUERY, { owner, repo, number, after });
    const issue = res?.repository?.issue;
    if (!issue) throw new Error(`issue ${owner}/${repo}#${number} not found via GraphQL`);
    if (page === 0) linked.push(...(issue.closedByPullRequestsReferences?.nodes ?? []));
    const tl = issue.timelineItems;
    for (const n of tl?.nodes ?? []) {
      const pr = n?.source;
      if (pr && pr.__typename === 'PullRequest') candidates.push(pr);
    }
    if (!tl?.pageInfo?.hasNextPage) {
      complete = true;
      break;
    }
    after = tl.pageInfo.endCursor;
  }
  if (!complete) throw new Error(`timeline for ${owner}/${repo}#${number} has more than 2000 cross-references; refusing to guess`);
  return { linked, candidates };
}

// Link-lag safety net: search the app trio for open PRs naming the issue. Every hit
// is re-read live (pulls.get) and kept only if its title/body closes the issue.
// Any search or fetch failure throws after all repos are tried (incomplete lookup
// must never look like "no companions").
async function searchTrio(github, { owner, repo, number }) {
  const full = `${owner}/${repo}#${number}`;
  const found = [];
  const errors = [];
  for (const r of TRIO) {
    const terms = [`"${full}"`, `"github.com/${owner}/${repo}/issues/${number}"`];
    if (r.toLowerCase() === repo.toLowerCase()) terms.push(`"#${number}"`);
    const q = `repo:${owner}/${r} is:pr is:open (${terms.join(' OR ')})`;
    try {
      const data = (await github.rest.search.issuesAndPullRequests({ q, per_page: 100 })).data;
      if (data.incomplete_results) throw new Error('search returned incomplete_results');
      const read = (data.items ?? []).length;
      if (typeof data.total_count === 'number' && data.total_count > read) {
        throw new Error(`search matched ${data.total_count} PRs but only ${read} were read`);
      }
      for (const it of data.items ?? []) {
        if (!it.pull_request) continue;
        const pr = (await github.rest.pulls.get({ owner, repo: r, pull_number: it.number })).data;
        found.push({
          number: pr.number,
          state: pr.merged_at ? 'MERGED' : pr.state === 'open' ? 'OPEN' : 'CLOSED',
          url: pr.html_url,
          title: pr.title,
          body: pr.body,
          repository: { nameWithOwner: `${owner}/${r}` },
        });
      }
    } catch (e) {
      errors.push(`${owner}/${r}: ${e.message || e}`);
    }
  }
  if (errors.length) throw new Error(`open-PR search incomplete, refusing to leave the issue closed: ${errors.join('; ')}`);
  return found;
}

async function findOpenCompanions({ github, owner, repo, number }) {
  const { linked, candidates } = await linkedPullRequests(github.graphql, { owner, repo, number });
  const searched = await searchTrio(github, { owner, repo, number });
  const confirmed = [...candidates, ...searched].filter((p) =>
    closesIssue(`${p.title ?? ''}\n${p.body ?? ''}`, { owner, repo, number, prRepo: p.repository?.nameWithOwner })
  );
  return openCompanions([...linked, ...confirmed], owner);
}

async function writeSummary(core, line) {
  try {
    if (core.summary?.addRaw) await core.summary.addRaw(`${line}\n`).write();
  } catch {
    // summary is best-effort (no GITHUB_STEP_SUMMARY locally)
  }
}

const GUARDED_REASONS = new Set(['completed', null, undefined]);

// Live issue state: 'open', 'closed' (completed / no reason, guarded), or 'other'
// (closed as not_planned, duplicate or any future reason: never touched).
async function liveIssueState(github, { owner, repo, number }) {
  const data = (await github.rest.issues.get({ owner, repo, issue_number: number })).data;
  if (data.state !== 'closed') return 'open';
  return GUARDED_REASONS.has(data.state_reason) ? 'closed' : 'other';
}

// Re-read each companion PR's live state (the earlier lookup may be seconds old).
async function stillOpen(github, companions) {
  const out = [];
  for (const p of companions) {
    const [o, r] = p.repo.split('/');
    const pr = (await github.rest.pulls.get({ owner: o, repo: r, pull_number: p.number })).data;
    if (pr.state === 'open' && !pr.merged_at) out.push(p);
  }
  return out;
}

function boardClient({ getOctokit, projectToken }) {
  if (!projectToken) return { reason: 'PROJECT_TOKEN secret is not set' };
  // actions/github-script injects getOctokit only from v9
  // (cultpodcasts/RedditPodcastPoster#1394). Never let a missing factory stop
  // the reopen + comment; just report the board gap.
  if (typeof getOctokit !== 'function') {
    return { reason: 'getOctokit is not available in the github-script context (needs actions/github-script v9+)' };
  }
  return { graphql: getOctokit(projectToken).graphql };
}

async function run({ github, context, core, getOctokit, projectToken, sleep = (ms) => new Promise((r) => setTimeout(r, ms)) }) {
  const owner = context.repo.owner;
  const repo = context.repo.repo;
  const number = context.payload.issue.number;
  const full = `${owner}/${repo}#${number}`;
  const ids = { owner, repo, number };
  const board = boardClient({ getOctokit, projectToken });

  // Runs are serialised per issue, so an earlier run may already have reopened it;
  // a not_planned / duplicate close is never guarded.
  const initial = await liveIssueState(github, ids);
  if (initial === 'open') {
    core.info(`${full} is open again; nothing to do.`);
    await writeSummary(core, `${full} already open; skipped.`);
    return { reopened: false, skipped: 'issue-open' };
  }
  if (initial === 'other') {
    core.info(`${full} is closed as not completed (not planned / duplicate); not guarded.`);
    await writeSummary(core, `${full} closed as not completed; skipped.`);
    return { reopened: false, skipped: 'not-completed' };
  }

  const companions = await findOpenCompanions({ github, ...ids });
  const leaveClosed = async (why) => {
    core.info(`${why}; leave ${full} closed.`);
    let settled = { settled: false };
    if (board.graphql) {
      try {
        settled = await settleClosedCard({ graphql: board.graphql, ...ids, log: (m) => core.info(m) });
      } catch (e) {
        core.warning(`Could not check the board card for closed ${full}: ${e.message || e}`);
      }
    }
    await writeSummary(core, `${why}; ${full} left closed.${settled.settled ? ` Card moved to ${DONE_STATUS}.` : ''}`);
    return { reopened: false, settled: settled.settled };
  };
  if (companions.length === 0) {
    return leaveClosed('No open PR closes the issue (Development links, confirmed cross-references, trio search)');
  }

  // Immediately before the side effects: live issue (state + reason) + live companion states.
  const before = await liveIssueState(github, ids);
  if (before === 'open') {
    core.info(`${full} was reopened meanwhile; nothing to do.`);
    return { reopened: false, skipped: 'issue-open' };
  }
  if (before === 'other') {
    core.info(`${full} was re-closed as not completed (not planned / duplicate) meanwhile; not guarded.`);
    return { reopened: false, skipped: 'not-completed' };
  }
  const open = await stillOpen(github, companions);
  if (open.length === 0) return leaveClosed('Every companion PR has merged or closed since the lookup');

  // Comment first, then reopen, so the comment never lists a PR that had already
  // merged when the issue reopened.
  const comment = (
    await github.rest.issues.createComment({ owner, repo, issue_number: number, body: buildComment(open) })
  ).data;
  try {
    await github.rest.issues.update({ owner, repo, issue_number: number, state: 'open', state_reason: 'reopened' });
  } catch (e) {
    // Never leave a "Reopening automatically" comment on an issue that stayed closed:
    // delete it (or, failing that, say the reopen failed), then still fail the job.
    if (comment?.id) {
      try {
        await github.rest.issues.deleteComment({ owner, repo, comment_id: comment.id });
      } catch {
        try {
          await github.rest.issues.updateComment({
            owner,
            repo,
            comment_id: comment.id,
            body: `⚠️ The guard tried to reopen this (open PR(s): ${open.map((p) => `${p.repo}#${p.number}`).join(', ')}) but the reopen failed; see the workflow run.`,
          });
        } catch {
          // best effort; the job failure below is the loud signal
        }
      }
    }
    throw new Error(`reopen of ${full} failed: ${e.message || e}`);
  }
  core.info(`Reopened ${full}; ${open.length} open companion PR(s).`);

  // A companion that merged between the check and the reopen could not close the
  // (then closed) issue, so close it again rather than leave it open with no PRs.
  const after = await stillOpen(github, open);
  if (after.length === 0) {
    await github.rest.issues.update({ owner, repo, issue_number: number, state: 'closed', state_reason: 'completed' });
    if (comment?.id) {
      await github.rest.issues.updateComment({
        owner,
        repo,
        comment_id: comment.id,
        body: `${buildComment(open)}\n\n**Update:** those PR(s) merged while this was being reopened, so the issue was closed again.`,
      });
    }
    core.info(`Companions merged during the reopen; ${full} closed again.`);
    await writeSummary(core, `${full}: companions merged during the reopen; closed again.`);
    return { reopened: false, reclosed: true, companions: open };
  }

  let result;
  if (!board.graphql) {
    result = { restored: false, reason: board.reason };
  } else {
    try {
      result = await restoreBoardStatus({ graphql: board.graphql, ...ids, sleep, log: (m) => core.info(m) });
    } catch (e) {
      result = { restored: false, reason: `board update failed: ${e.message || e}` };
    }
  }
  if (result.restored) {
    core.info(`Board Status is ${result.status}.`);
  } else if (result.closedAgain) {
    core.info(`${full} was closed again by a newer merge; card left on ${DONE_STATUS}.`);
  } else {
    core.warning(`Board Status not restored for ${full}: ${result.reason}`);
    // Only worth saying while the issue is still open.
    if ((await liveIssueState(github, ids)) === 'open') {
      await github.rest.issues.createComment({
        owner,
        repo,
        issue_number: number,
        body: `⚠️ Board card NOT restored (${result.reason}); ${MANUAL_RESTORE}.`,
      });
    }
  }
  await writeSummary(
    core,
    `Reopened ${full}: ${after.map((p) => `${p.repo}#${p.number}`).join(', ')} still open. ` +
      (result.restored ? `Board ${result.status}.` : `Board NOT restored: ${result.reason}.`)
  );
  return { reopened: true, board: result, companions: after };
}

module.exports = {
  run,
  openCompanions,
  closesIssue,
  stripNonProse,
  linkedPullRequests,
  findOpenCompanions,
  pickBoardItem,
  buildComment,
  restoreBoardStatus,
  settleClosedCard,
  columnBeforeClose,
  stillOpen,
  RESTORE_STATUS,
  DONE_STATUS,
  TRIO,
};
