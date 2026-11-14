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
//   1. a Development link (closedByPullRequestsReferences: `Closes owner/repo#N`
//      or linked by hand), or
//   2. a timeline cross-reference (body/title/commit mentions: `Refs owner/repo#N`,
//      same-repo `#N`, full issue URL), or
//   3. found by search across the app trio and confirmed by body text + live state.
// #1423: since 6d890dda only (1) was checked, so `Refs` companions (Api#259,
// website#667 on #1400) were missed and the issue stayed closed.
//
// Convention: cross-repo companions use `Closes cultpodcasts/RedditPodcastPoster#N`
// so they show under Development; (2) and (3) are the safety net for `Refs`.
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

// Does `text` (a PR's title + body) reference owner/repo#number? `prRepo` is the
// PR's own "owner/name": a bare `#N` only means this issue inside the same repo.
function referencesIssue(text, { owner, repo, number, prRepo }) {
  if (!text) return false;
  const full = `${owner}/${repo}`;
  const n = String(number);
  const fullRef = new RegExp(`(^|[^\\w/.-])${escapeRegExp(full)}#${n}(?!\\d)`, 'i');
  const url = new RegExp(`github\\.com/${escapeRegExp(full)}/issues/${n}(?!\\d)`, 'i');
  if (fullRef.test(text) || url.test(text)) return true;
  if (prRepo && prRepo.toLowerCase() === full.toLowerCase()) {
    return new RegExp(`(^|[^\\w/&])#${n}(?!\\d)`).test(text);
  }
  return false;
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
    `Cross-repo PRs should say \`Closes <owner>/<repo>#N\` so they show under Development; this guard undoes premature closes.` +
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
        timelineItems(first: 100, after: $after, itemTypes: [CROSS_REFERENCED_EVENT, CONNECTED_EVENT]) {
          pageInfo { hasNextPage endCursor }
          nodes {
            __typename
            ... on CrossReferencedEvent {
              source { __typename ... on PullRequest { number state url title repository { nameWithOwner } } }
            }
            ... on ConnectedEvent {
              subject { __typename ... on PullRequest { number state url title repository { nameWithOwner } } }
            }
          }
        }
      }
    }
  }
`;

// Every PR the issue knows about: Development links + timeline cross-references
// (paginated). GraphQL errors throw so the job fails visibly instead of leaving
// the issue closed as a silent "no companions".
async function linkedPullRequests(graphql, { owner, repo, number }) {
  const prs = [];
  let after = null;
  for (let page = 0; page < 10; page++) {
    const res = await graphql(LINKS_QUERY, { owner, repo, number, after });
    const issue = res?.repository?.issue;
    if (!issue) throw new Error(`issue ${owner}/${repo}#${number} not found via GraphQL`);
    if (page === 0) prs.push(...(issue.closedByPullRequestsReferences?.nodes ?? []));
    const tl = issue.timelineItems;
    for (const n of tl?.nodes ?? []) {
      const pr = n?.source ?? n?.subject;
      if (pr && pr.__typename === 'PullRequest') prs.push(pr);
    }
    if (!tl?.pageInfo?.hasNextPage) break;
    after = tl.pageInfo.endCursor;
  }
  return prs;
}

// Safety net: search the app trio for open PRs mentioning the issue (search index
// can lag; hits are confirmed by body text and the PR's live state).
async function searchTrio(github, { owner, repo, number, core }) {
  const full = `${owner}/${repo}#${number}`;
  const found = [];
  for (const r of TRIO) {
    const terms = [`"${full}"`, `"github.com/${owner}/${repo}/issues/${number}"`];
    if (r.toLowerCase() === repo.toLowerCase()) terms.push(`"#${number}"`);
    const q = `repo:${owner}/${r} is:pr is:open (${terms.join(' OR ')})`;
    let items;
    try {
      items = (await github.rest.search.issuesAndPullRequests({ q, per_page: 50 })).data.items ?? [];
    } catch (e) {
      core.warning(`Search for open PRs in ${owner}/${r} failed: ${e.message || e}`);
      continue;
    }
    for (const it of items) {
      if (!it.pull_request) continue;
      const prRepo = `${owner}/${r}`;
      if (!referencesIssue(`${it.title}\n${it.body || ''}`, { owner, repo, number, prRepo })) continue;
      const pr = (await github.rest.pulls.get({ owner, repo: r, pull_number: it.number })).data;
      found.push({
        number: pr.number,
        state: pr.merged_at ? 'MERGED' : pr.state === 'open' ? 'OPEN' : 'CLOSED',
        url: pr.html_url,
        title: pr.title,
        repository: { nameWithOwner: prRepo },
      });
    }
  }
  return found;
}

async function findOpenCompanions({ github, owner, repo, number, core }) {
  const linked = await linkedPullRequests(github.graphql, { owner, repo, number });
  const searched = github.rest?.search ? await searchTrio(github, { owner, repo, number, core }) : [];
  // GraphQL state is live; prefer it over search for the same PR.
  return openCompanions([...linked, ...searched], owner);
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

  const companions = await findOpenCompanions({ github, owner, repo, number, core });
  if (companions.length === 0) {
    core.info(`No open PRs for ${full} (Development links, cross-references, trio search); leave closed.`);
    await writeSummary(core, `No open PRs reference ${full}; left closed.`);
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
  referencesIssue,
  linkedPullRequests,
  findOpenCompanions,
  pickBoardItem,
  buildComment,
  restoreBoardStatus,
  RESTORE_STATUS,
  TRIO,
};
