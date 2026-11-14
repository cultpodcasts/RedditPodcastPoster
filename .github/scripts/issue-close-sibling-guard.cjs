// Issue close sibling guard (mirrored across RedditPodcastPoster, website, Api).
//
// GitHub auto-closes an issue when ANY linked PR with Closes/Fixes merges, and the
// features board's built-in "Item closed" workflow then sets Status = Done.
// Rule (Jon, cultpodcasts/RedditPodcastPoster#1423): a ticket with PRs in several
// repos stays open until ALL of its PRs are merged. When any other PR for the issue
// is still open, this reopens the issue and puts the board card back on In Progress
// (Ready For Review means the PRs are ready to merge).
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
// Fail loud: any lookup that cannot complete (GraphQL, search, PR fetch) throws, so
// the job fails instead of reporting a green "no companions".
//
// Board writes need PROJECT_TOKEN (classic PAT with `project` scope): the
// repo-scoped GITHUB_TOKEN cannot see user-owned Projects V2
// ("Could not resolve to a ProjectV2 with the number 1").

'use strict';

const BOARD_OWNER = 'cultpodcasts';
const BOARD_NUMBER = 1;
const STATUS_FIELD = 'Status';
const RESTORE_STATUS = 'In Progress';
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

function buildComment(companions, board) {
  const list = companions.map((p) => `- ${p.repo}#${p.number}: ${p.title} — ${p.url}`).join('\n');
  let boardLine;
  if (board.restored) {
    boardLine = `Board card restored to **${RESTORE_STATUS}**.`;
  } else if (board.reason) {
    boardLine = `⚠️ Board card NOT restored (${board.reason}); set Status back to ${RESTORE_STATUS} by hand.`;
  } else {
    boardLine = '';
  }
  return (
    `Reopened automatically: this was closed while **open PR(s)** for it remain:\n\n` +
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

// The board's "Item closed" automation can land before OR after this job, so keep
// re-asserting In Progress until it reads back stable twice in a row.
async function restoreBoardStatus({ graphql, owner, repo, number, sleep, attempts = 8, delayMs = 5000, log = () => {} }) {
  let stable = 0;
  let wrote = false;
  for (let i = 0; i < attempts; i++) {
    const res = await graphql(ITEMS_QUERY, { owner, repo, number });
    const item = pickBoardItem(res.repository?.issue?.projectItems?.nodes);
    if (!item) return { restored: false, reason: `issue is not on ${BOARD_OWNER} project ${BOARD_NUMBER}` };
    const field = item.project.field;
    const restoreOption = field?.options?.find((o) => o.name === RESTORE_STATUS);
    if (!field?.id || !restoreOption) return { restored: false, reason: `no "${RESTORE_STATUS}" option on ${STATUS_FIELD}` };

    const current = item.fieldValueByName?.name ?? null;
    if (current === RESTORE_STATUS) {
      stable++;
      if (stable >= 2) return { restored: true, wrote };
    } else {
      stable = 0;
      log(`Board Status is ${current ?? '(empty)'}; setting ${RESTORE_STATUS}.`);
      await graphql(SET_STATUS, { project: item.project.id, item: item.id, field: field.id, option: restoreOption.id });
      wrote = true;
    }
    if (i < attempts - 1) await sleep(delayMs);
  }
  return stable >= 1 ? { restored: true, wrote } : { restored: false, reason: 'status kept reverting' };
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

async function run({ github, context, core, getOctokit, projectToken, sleep = (ms) => new Promise((r) => setTimeout(r, ms)) }) {
  const owner = context.repo.owner;
  const repo = context.repo.repo;
  const number = context.payload.issue.number;
  const full = `${owner}/${repo}#${number}`;

  const companions = await findOpenCompanions({ github, owner, repo, number });
  if (companions.length === 0) {
    core.info(`No open PR closes ${full} (Development links, confirmed cross-references, trio search); leave closed.`);
    await writeSummary(core, `No open PR closes ${full}; left closed.`);
    return { reopened: false };
  }

  await github.rest.issues.update({ owner, repo, issue_number: number, state: 'open', state_reason: 'reopened' });
  core.info(`Reopened ${full}; ${companions.length} open companion PR(s).`);

  let board;
  if (!projectToken) {
    board = { restored: false, reason: 'PROJECT_TOKEN secret is not set' };
  } else if (typeof getOctokit !== 'function') {
    // actions/github-script injects getOctokit only from v9
    // (cultpodcasts/RedditPodcastPoster#1394). Never let a missing factory stop
    // the reopen + comment; just report the board gap.
    board = {
      restored: false,
      reason: 'getOctokit is not available in the github-script context (needs actions/github-script v9+)',
    };
  } else {
    try {
      const projectOctokit = getOctokit(projectToken);
      board = await restoreBoardStatus({
        graphql: projectOctokit.graphql,
        owner,
        repo,
        number,
        sleep,
        log: (m) => core.info(m),
      });
    } catch (e) {
      board = { restored: false, reason: `board update failed: ${e.message || e}` };
    }
  }
  if (board.restored) core.info(`Board Status is ${RESTORE_STATUS}.`);
  else core.warning(`Board Status not restored for ${full}: ${board.reason}`);

  await github.rest.issues.createComment({ owner, repo, issue_number: number, body: buildComment(companions, board) });
  await writeSummary(
    core,
    `Reopened ${full}: ${companions.map((p) => `${p.repo}#${p.number}`).join(', ')} still open. ` +
      (board.restored ? `Board ${RESTORE_STATUS}.` : `Board NOT restored: ${board.reason}.`)
  );
  return { reopened: true, board, companions };
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
  RESTORE_STATUS,
  TRIO,
};
