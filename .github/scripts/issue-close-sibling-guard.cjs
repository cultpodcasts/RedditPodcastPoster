// Issue close sibling guard (mirrored across RedditPodcastPoster, website, Api).
//
// GitHub auto-closes an issue when ANY linked PR with Closes/Fixes merges, and the
// features board's built-in "Item closed" workflow then sets Status = Done.
// When other PRs that still Closes/Fixes the issue are open, this reopens the issue
// and puts the board card back on In Progress (Ready For Review means the PRs are
// ready to merge).
//
// Board writes need PROJECT_TOKEN (classic PAT with `project` scope): the
// repo-scoped GITHUB_TOKEN cannot see user-owned Projects V2
// ("Could not resolve to a ProjectV2 with the number 1").

'use strict';

const BOARD_OWNER = 'cultpodcasts';
const BOARD_NUMBER = 1;
const STATUS_FIELD = 'Status';
const RESTORE_STATUS = 'In Progress';

function openCompanions(nodes) {
  return (nodes ?? [])
    .filter((p) => p && p.state === 'OPEN')
    .map((p) => ({
      repo: p.repository.nameWithOwner,
      number: p.number,
      url: p.url,
      title: p.title,
    }));
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
    `Reopened automatically: GitHub closed this when a \`Closes\`/\`Fixes\` PR merged, but **open companion PR(s)** still reference it:\n\n` +
    `${list}\n\n` +
    `GitHub cannot wait for every linked PR. This ticket stays open until those are merged (or unlinked). ` +
    `Keep \`Closes\` on open siblings so Development stays linked; this guard undoes premature closes.` +
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

async function run({ github, context, core, getOctokit, projectToken, sleep = (ms) => new Promise((r) => setTimeout(r, ms)) }) {
  const owner = context.repo.owner;
  const repo = context.repo.repo;
  const number = context.payload.issue.number;
  const full = `${owner}/${repo}#${number}`;

  // GraphQL closing-issue links (Development / Closes) — cross-repo aware, no search lag.
  const gql = await github.graphql(
    `query($owner: String!, $repo: String!, $number: Int!) {
      repository(owner: $owner, name: $repo) {
        issue(number: $number) {
          closedByPullRequestsReferences(first: 50, includeClosedPrs: true) {
            nodes { number state url title repository { nameWithOwner } }
          }
        }
      }
    }`,
    { owner, repo, number }
  );
  const companions = openCompanions(gql.repository?.issue?.closedByPullRequestsReferences?.nodes);
  if (companions.length === 0) {
    core.info(`No open companion Closes PRs for ${full}; leave closed.`);
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
  return { reopened: true, board };
}

module.exports = { run, openCompanions, pickBoardItem, buildComment, restoreBoardStatus, RESTORE_STATUS };
