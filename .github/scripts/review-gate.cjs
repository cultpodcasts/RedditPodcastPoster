'use strict';
// review-gate (cultpodcasts/RedditPodcastPoster#1507): publish a commit status `review-gate`
// on a PR's head SHA. It passes only when the PR has the `review-acceptable` label AND the
// latest review-gate marker for the CURRENT head SHA, posted by a trusted author
// (OWNER / MEMBER / COLLABORATOR) in an issue comment or a review body, says `acceptable`:
//
//   <!-- review-gate: acceptable sha=<full 40-char head sha> -->
//   <!-- review-gate: changes-requested sha=<full 40-char head sha> -->   (overrides an earlier acceptable)
//
// A new push (pull_request_target `synchronize`) removes the label and fails the status.
// Comment and review bodies are only matched against MARKER_RE here; they are never passed
// to a shell or a workflow expression.
// Mirrored across RedditPodcastPoster, Api and website. Tests: review-gate.test.cjs.

const LABEL = 'review-acceptable';
const STATUS_CONTEXT = 'review-gate';
const TRUSTED_ASSOCIATIONS = new Set(['OWNER', 'MEMBER', 'COLLABORATOR']);
const MARKER_RE = /<!--\s*review-gate:\s*(acceptable|changes-requested)\s+sha=([0-9a-fA-F]{40})\s*-->/gi;
const MAX_DESCRIPTION = 140;

function parseMarkers(body) {
  if (typeof body !== 'string' || body.length === 0) return [];
  const out = [];
  for (const m of body.matchAll(MARKER_RE)) {
    out.push({ verdict: m[1].toLowerCase(), sha: m[2].toLowerCase() });
  }
  return out;
}

// entries: [{ at: ISO string, association, body }]. Returns 'acceptable' | 'changes-requested' | null
// for the latest trusted marker naming headSha; also whether any trusted marker names another SHA.
function latestVerdict(entries, headSha) {
  const sha = String(headSha || '').toLowerCase();
  const trusted = (entries || [])
    .filter((e) => TRUSTED_ASSOCIATIONS.has(String(e.association || '').toUpperCase()))
    .map((e, i) => ({ ...e, i, t: Date.parse(e.at) || 0 }))
    .sort((a, b) => a.t - b.t || a.i - b.i);
  let verdict = null;
  let staleMarker = false;
  for (const e of trusted) {
    for (const m of parseMarkers(e.body)) {
      if (m.sha === sha) verdict = m.verdict;
      else staleMarker = true;
    }
  }
  return { verdict, staleMarker };
}

function truncate(s) {
  return s.length <= MAX_DESCRIPTION ? s : `${s.slice(0, MAX_DESCRIPTION - 1)}…`;
}

// Pure decision. labels: string[]; pushed: true when this run is for a new push.
function evaluate({ labels, headSha, entries, pushed }) {
  const short = String(headSha || '').slice(0, 7);
  if (pushed) {
    return {
      state: 'failure',
      description: truncate(`New commits (${short}): review needed; reviewer must re-mark this SHA acceptable and re-add ${LABEL}`),
    };
  }
  const hasLabel = (labels || []).includes(LABEL);
  const { verdict, staleMarker } = latestVerdict(entries, headSha);
  if (verdict === 'acceptable' && hasLabel) {
    return { state: 'success', description: truncate(`Reviewer marked ${short} acceptable`) };
  }
  if (verdict === 'changes-requested') {
    return { state: 'failure', description: truncate(`Reviewer requested changes on ${short}`) };
  }
  if (verdict === 'acceptable') {
    return { state: 'failure', description: truncate(`${short} marked acceptable but label ${LABEL} is missing`) };
  }
  if (staleMarker) {
    return { state: 'failure', description: truncate(`Review marker is for an older commit; ${short} not reviewed yet`) };
  }
  return {
    state: 'failure',
    description: truncate(`Awaiting review-implement-pr: no acceptable marker for ${short}${hasLabel ? '' : ` and no ${LABEL} label`}`),
  };
}

function prNumberFromContext(context) {
  const p = context.payload || {};
  if (context.eventName === 'issue_comment') {
    return p.issue && p.issue.pull_request ? p.issue.number : null;
  }
  return p.pull_request ? p.pull_request.number : null;
}

async function run({ github, context, core }) {
  const number = prNumberFromContext(context);
  if (!number) {
    core.info('Not a pull request event; nothing to do.');
    return null;
  }
  const { owner, repo } = context.repo;
  const { data: pr } = await github.rest.pulls.get({ owner, repo, pull_number: number });
  if (pr.state !== 'open') {
    core.info(`PR #${number} is ${pr.state}; status left unchanged.`);
    return null;
  }
  const headSha = pr.head.sha;
  const pushed = context.eventName === 'pull_request_target' && context.payload.action === 'synchronize';
  let labels = (pr.labels || []).map((l) => l.name);

  if (pushed && labels.includes(LABEL)) {
    try {
      await github.rest.issues.removeLabel({ owner, repo, issue_number: number, name: LABEL });
      core.info(`Removed ${LABEL} after new push ${headSha}.`);
    } catch (err) {
      if (err.status !== 404) throw err;
    }
    labels = labels.filter((l) => l !== LABEL);
  }

  const comments = await github.paginate(github.rest.issues.listComments, {
    owner, repo, issue_number: number, per_page: 100,
  });
  const reviews = await github.paginate(github.rest.pulls.listReviews, {
    owner, repo, pull_number: number, per_page: 100,
  });
  const entries = [
    ...comments.map((c) => ({ at: c.updated_at || c.created_at, association: c.author_association, body: c.body })),
    ...reviews
      .filter((r) => r.state !== 'DISMISSED')
      .map((r) => ({ at: r.submitted_at, association: r.author_association, body: r.body })),
  ];

  const result = evaluate({ labels, headSha, entries, pushed });
  const serverUrl = process.env.GITHUB_SERVER_URL || 'https://github.com';
  const targetUrl = context.runId
    ? `${serverUrl}/${owner}/${repo}/actions/runs/${context.runId}`
    : pr.html_url;
  await github.rest.repos.createCommitStatus({
    owner, repo, sha: headSha, state: result.state, context: STATUS_CONTEXT,
    description: result.description, target_url: targetUrl,
  });
  core.info(`review-gate ${result.state} on ${headSha}: ${result.description}`);
  if (core.summary) {
    await core.summary
      .addRaw(`review-gate **${result.state}** on PR #${number} @ \`${headSha}\`: ${result.description}\n`)
      .write();
  }
  return { headSha, ...result };
}

module.exports = {
  LABEL, STATUS_CONTEXT, MARKER_RE, parseMarkers, latestVerdict, evaluate, prNumberFromContext, run,
};
