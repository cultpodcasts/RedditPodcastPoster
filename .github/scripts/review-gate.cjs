'use strict';
// review-gate (cultpodcasts/RedditPodcastPoster#1507): publish a commit status `review-gate`
// on a PR's head SHA. It passes only when the PR has the `review-acceptable` label AND the
// latest review-gate marker for the CURRENT head SHA, posted by a trusted author
// (OWNER / MEMBER / COLLABORATOR) in a PR **issue comment**, says `acceptable`:
//
//   <!-- review-gate: acceptable sha=<full 40-char head sha> -->
//   <!-- review-gate: changes-requested sha=<full 40-char head sha> -->   (overrides an earlier acceptable)
//
// Markers in review bodies are ignored (pull_request_review would run the PR's own workflow
// YAML), as are markers on quoted (`>`) lines, inside fenced or indented code blocks, or
// inside inline code spans. Comments are ordered by created_at, so editing an old comment
// does not make its marker the latest.
// The label is removed on a new push (`synchronize`) and on ANY run where it is present but
// the latest marker for the head SHA is not `acceptable` (so a dropped push run self-heals;
// the reviewer posts the marker first, then adds the label).
// Comment bodies are only matched against MARKER_RE here; they are never passed to a shell
// or a workflow expression. This is a process interlock, not a security boundary: every
// agent and Jon post as the same OWNER account.
// Mirrored across RedditPodcastPoster, Api and website. Tests: review-gate.test.cjs.

const LABEL = 'review-acceptable';
const STATUS_CONTEXT = 'review-gate';
const TRUSTED_ASSOCIATIONS = new Set(['OWNER', 'MEMBER', 'COLLABORATOR']);
const MARKER_RE = /<!--\s*review-gate:\s*(acceptable|changes-requested)\s+sha=([0-9a-fA-F]{40})\s*-->/gi;
const MAX_DESCRIPTION = 140;

// Drop quoted lines (`> ...`), fenced code blocks (``` or ~~~), 4-space/tab indented code
// blocks (start of text or after a blank line), and inline code spans, so a reply that
// quotes or shows an old marker does not count as a new verdict.
function stripIndentedCodeBlocks(body) {
  const kept = [];
  let inBlock = false;
  let prevBlank = true;
  for (const line of body.split(/\r?\n/)) {
    const isBlank = line.trim() === '';
    const isIndented = /^(?: {4}|\t)/.test(line);
    if (inBlock) {
      if (isBlank || isIndented) continue;
      inBlock = false;
    } else if (!isBlank && isIndented && prevBlank) {
      inBlock = true;
      continue;
    }
    kept.push(line);
    prevBlank = isBlank;
  }
  return kept.join('\n');
}

function stripInlineCodeSpans(body) {
  // CommonMark-style: a run of backticks closed by a run of the same length (so
  // `` `inner` `` is one span). Unclosed ticks are left alone.
  return body.replace(/(`+)(?:(?!\1)[\s\S])*?\1/g, '');
}

function stripQuotedAndFenced(body) {
  const kept = [];
  let fence = null;
  for (const line of body.split(/\r?\n/)) {
    const f = line.match(/^\s{0,3}(`{3,}|~{3,})/);
    if (fence) {
      if (f && f[1][0] === fence[0] && f[1].length >= fence.length) fence = null;
      continue;
    }
    if (f) { fence = f[1]; continue; }
    if (/^\s{0,3}>/.test(line)) continue;
    kept.push(line);
  }
  return stripInlineCodeSpans(stripIndentedCodeBlocks(kept.join('\n')));
}

function parseMarkers(body) {
  if (typeof body !== 'string' || body.length === 0) return [];
  const out = [];
  for (const m of stripQuotedAndFenced(body).matchAll(MARKER_RE)) {
    out.push({ verdict: m[1].toLowerCase(), sha: m[2].toLowerCase() });
  }
  return out;
}

// entries: [{ at: created_at ISO string, association, body }]. Returns 'acceptable' | 'changes-requested' | null
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

// Issue comments only; ordered by created_at (an edit changes content, not conversation order).
function commentEntries(comments) {
  return (comments || []).map((c) => ({ at: c.created_at, association: c.author_association, body: c.body }));
}

// Remove the label on a new push, or whenever it is present without an `acceptable` marker for
// the current head SHA. State-based, so a push run replaced in the concurrency queue self-heals.
function shouldRemoveLabel({ labels, headSha, entries, pushed }) {
  if (!(labels || []).includes(LABEL)) return false;
  if (pushed) return true;
  return latestVerdict(entries, headSha).verdict !== 'acceptable';
}

function prNumberFromContext(context) {
  const p = context.payload || {};
  if (context.eventName === 'issue_comment') {
    return p.issue && p.issue.pull_request ? p.issue.number : null;
  }
  if (context.eventName === 'pull_request_target') {
    return p.pull_request ? p.pull_request.number : null;
  }
  return null;
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

  const comments = await github.paginate(github.rest.issues.listComments, {
    owner, repo, issue_number: number, per_page: 100,
  });
  const entries = commentEntries(comments);

  if (shouldRemoveLabel({ labels, headSha, entries, pushed })) {
    try {
      await github.rest.issues.removeLabel({ owner, repo, issue_number: number, name: LABEL });
      core.info(`Removed ${LABEL}: ${pushed ? 'new push' : 'no acceptable marker'} for ${headSha}.`);
    } catch (err) {
      if (err.status !== 404) throw err;
    }
    labels = labels.filter((l) => l !== LABEL);
  }

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
  LABEL, STATUS_CONTEXT, MARKER_RE, stripQuotedAndFenced, parseMarkers, latestVerdict, evaluate,
  commentEntries, shouldRemoveLabel, prNumberFromContext, run,
};
