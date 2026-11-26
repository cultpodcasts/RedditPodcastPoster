'use strict';
// review-gate (cultpodcasts/RedditPodcastPoster#1507, #1563): publish a commit status
// `review-gate` on a PR's head SHA. It passes when the PR has the `review-acceptable`
// label AND any of:
//
//   1. Approve path (#1563): latest trusted (OWNER / MEMBER / COLLABORATOR) PR review
//      for the current head SHA is APPROVED (workflow auto-adds the label). A trusted
//      CHANGES_REQUESTED review for that SHA fails the gate.
//   2. Marker path: latest trusted issue-comment marker for the head SHA says acceptable:
//        <!-- review-gate: acceptable sha=<full 40-char head sha> -->
//        <!-- review-gate: changes-requested sha=<full 40-char head sha> -->
//   3. Carry path (#1555): merge-from-base-only tip above an acceptable anchor + label.
//
// Markers in review bodies are ignored as verdict text (Approve/Changes Requested use
// the review *state*, not the body). Markers on quoted (`>`) lines, inside fenced or
// indented code blocks, or inside inline code spans are ignored. Comments are ordered
// by created_at, so editing an old comment does not make its marker the latest.
// The label is removed on a new push (`synchronize`, unless carried: see CARRY below) and on
// ANY run where it is present but the head has no acceptable Approve/marker and nothing
// is carried (so a dropped push run self-heals; the reviewer Approves or posts the marker
// first, then the label is present). Removing a PR label needs `pull-requests: write`.
// A non-404 failure (e.g. 403) is logged as a warning and is not fatal: the status is still
// published, and is never success when the label is stale or the verdict is missing
// (cultpodcasts/RedditPodcastPoster#1519).
// CARRY (cultpodcasts/RedditPodcastPoster#1555): a head with no marker/Approve of its own
// still passes when it is a chain of PURE merge-from-base commits (see merge-only-tip.cjs)
// on top of a commit whose latest trusted marker is `acceptable`. The walk follows first
// parents, at most MAX_HOPS hops, and stops at the nearest commit with a trusted marker:
// `acceptable` carries, `changes-requested` blocks. The label is kept in that case (no
// human re-mark). Rebases, feature commits, overlapping or hand-edited merges, an
// unverifiable listing or an API error do not carry: the label is removed on a push as
// before (an API error keeps the label but fails the status, so a re-run can heal it).
// The chain is recomputed from live PR state on every run; no extra marker is written.
// After publishing the commit status, a non-success state fails the Actions job
// (`core.setFailed`) so the check run is red when the status is red (#1563).
// Comment bodies are only matched against MARKER_RE here; they are never passed to a shell
// or a workflow expression. This is a process interlock, not a security boundary: every
// agent and Jon post as the same OWNER account.
// Mirrored across RedditPodcastPoster, Api and website. Tests: review-gate.test.cjs.

const { walkMergeOnlyChain } = require('./merge-only-tip.cjs');

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

// Latest trusted PR review for headSha: APPROVED → 'acceptable', CHANGES_REQUESTED →
// 'changes-requested'. Ignores COMMENTED / PENDING / DISMISSED and reviews whose commit_id
// is not the current head. Ordered by submitted_at.
function latestReviewVerdict(reviews, headSha) {
  const sha = String(headSha || '').toLowerCase();
  const trusted = (reviews || [])
    .filter((r) => TRUSTED_ASSOCIATIONS.has(String(r.author_association || '').toUpperCase()))
    .filter((r) => {
      const state = String(r.state || '').toUpperCase();
      return state === 'APPROVED' || state === 'CHANGES_REQUESTED';
    })
    .filter((r) => String(r.commit_id || '').toLowerCase() === sha)
    .map((r, i) => ({ ...r, i, t: Date.parse(r.submitted_at) || 0 }))
    .sort((a, b) => a.t - b.t || a.i - b.i);
  if (trusted.length === 0) return null;
  const state = String(trusted[trusted.length - 1].state || '').toUpperCase();
  return state === 'APPROVED' ? 'acceptable' : 'changes-requested';
}

// Latest trusted verdict per SHA (same ordering and parsing as latestVerdict), as a Map.
function latestVerdictsBySha(entries) {
  const trusted = (entries || [])
    .filter((e) => TRUSTED_ASSOCIATIONS.has(String(e.association || '').toUpperCase()))
    .map((e, i) => ({ ...e, i, t: Date.parse(e.at) || 0 }))
    .sort((a, b) => a.t - b.t || a.i - b.i);
  const bySha = new Map();
  for (const e of trusted) {
    for (const m of parseMarkers(e.body)) bySha.set(m.sha, m.verdict);
  }
  return bySha;
}

function truncate(s) {
  return s.length <= MAX_DESCRIPTION ? s : `${s.slice(0, MAX_DESCRIPTION - 1)}…`;
}

// Combined head verdict: Approve path wins over markers when present for this SHA
// (Approve clears a prior changes-requested marker; Changes Requested blocks a marker).
function headVerdict({ entries, headSha, reviewVerdict }) {
  if (reviewVerdict === 'changes-requested' || reviewVerdict === 'acceptable') {
    return { verdict: reviewVerdict, staleMarker: false, fromReview: true };
  }
  const m = latestVerdict(entries, headSha);
  return { verdict: m.verdict, staleMarker: m.staleMarker, fromReview: false };
}

// Pure decision. labels: string[]; pushed: true when this run is for a new push.
// carry: result of the merge-from-base chain walk (see resolveCarry), or null when not attempted.
// reviewVerdict: from latestReviewVerdict, or null.
function evaluate({ labels, headSha, entries, pushed, carry, reviewVerdict }) {
  const short = String(headSha || '').slice(0, 7);
  const hasLabel = (labels || []).includes(LABEL);
  if (carry && carry.ok) {
    const anchor = String(carry.anchor || '').slice(0, 7);
    const n = (carry.hops || []).length;
    if (hasLabel) {
      return {
        state: 'success',
        description: truncate(`Carried acceptable from ${anchor} across ${n} merge-from-base commit${n === 1 ? '' : 's'}`),
      };
    }
    return { state: 'failure', description: truncate(`${short} carries acceptable from ${anchor} but label ${LABEL} is missing`) };
  }
  if (carry && carry.apiError) {
    return { state: 'failure', description: truncate(`Could not verify merge-from-base carry for ${short} (API error); re-run`) };
  }
  if (pushed) {
    if (carry && carry.reason) {
      return {
        state: 'failure',
        description: truncate(`New commits (${short}) not carried: ${carry.reason}; reviewer must re-mark and re-add ${LABEL}`),
      };
    }
    return {
      state: 'failure',
      description: truncate(`New commits (${short}): review needed; reviewer must re-mark this SHA acceptable and re-add ${LABEL}`),
    };
  }
  const { verdict, staleMarker, fromReview } = headVerdict({ entries, headSha, reviewVerdict });
  if (verdict === 'acceptable' && hasLabel) {
    return {
      state: 'success',
      description: truncate(fromReview
        ? `Reviewer approved ${short}`
        : `Reviewer marked ${short} acceptable`),
    };
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

// Remove the label on a new push, or whenever it is present without an acceptable
// Approve/marker for the current head SHA. State-based, so a push run replaced in the
// concurrency queue self-heals. A carried acceptance keeps the label; so does an
// unverifiable carry (API error), which fails the status instead so a re-run can heal it.
function shouldRemoveLabel({ labels, headSha, entries, pushed, carry, reviewVerdict }) {
  if (!(labels || []).includes(LABEL)) return false;
  if (carry && (carry.ok || carry.apiError)) return false;
  if (pushed) return true;
  return headVerdict({ entries, headSha, reviewVerdict }).verdict !== 'acceptable';
}

// Walk merge-from-base hops from the head to the nearest commit with a trusted marker.
// Only attempted when the head has no Approve/marker of its own and the label is present
// (without the label there is nothing to carry). Resolves the walk result, or null when
// not attempted.
async function resolveCarry({ github, owner, repo, baseRef, headSha, labels, entries, reviewVerdict, walk = walkMergeOnlyChain }) {
  if (!(labels || []).includes(LABEL)) return null;
  if (headVerdict({ entries, headSha, reviewVerdict }).verdict !== null) return null;
  const bySha = latestVerdictsBySha(entries);
  const classify = (sha) => {
    const v = bySha.get(String(sha).toLowerCase());
    if (v === 'acceptable') return 'stop';
    if (v === 'changes-requested') return 'block';
    return null;
  };
  return walk({ github, owner, repo, baseRef, headSha, classify });
}

function prNumberFromContext(context) {
  const p = context.payload || {};
  if (context.eventName === 'issue_comment') {
    return p.issue && p.issue.pull_request ? p.issue.number : null;
  }
  if (context.eventName === 'pull_request_target' || context.eventName === 'pull_request_review') {
    return p.pull_request ? p.pull_request.number : null;
  }
  return null;
}

async function run({ github, context, core, walk }) {
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

  const reviews = await github.paginate(github.rest.pulls.listReviews, {
    owner, repo, pull_number: number, per_page: 100,
  });
  const reviewVerdict = latestReviewVerdict(reviews, headSha);

  // Approve path: ensure the label exists before evaluate so Approve alone clears the gate.
  if (reviewVerdict === 'acceptable' && !labels.includes(LABEL)) {
    try {
      await github.rest.issues.addLabels({ owner, repo, issue_number: number, labels: [LABEL] });
      labels = [...labels, LABEL];
      core.info(`Added ${LABEL}: trusted Approve for ${headSha}.`);
    } catch (err) {
      const reason = err.message || String(err);
      const msg = `Failed to add ${LABEL} (${err.status || 'error'}): ${reason}. Publishing review-gate status anyway.`;
      if (typeof core.warning === 'function') core.warning(msg);
      else core.info(msg);
    }
  }

  const carry = await resolveCarry({
    github, owner, repo, baseRef: pr.base && pr.base.ref, headSha, labels, entries, reviewVerdict, walk,
  });
  if (carry) {
    core.info(carry.ok
      ? `Carry: ${headSha} is ${carry.hops.length} merge-from-base commit(s) above acceptable ${carry.anchor}.`
      : `Carry: not carried (${carry.reason}).`);
  }

  let removalFailed = false;
  if (shouldRemoveLabel({ labels, headSha, entries, pushed, carry, reviewVerdict })) {
    try {
      await github.rest.issues.removeLabel({ owner, repo, issue_number: number, name: LABEL });
      core.info(`Removed ${LABEL}: ${pushed ? 'new push' : 'no acceptable verdict'} for ${headSha}.`);
      labels = labels.filter((l) => l !== LABEL);
    } catch (err) {
      if (err.status === 404) {
        labels = labels.filter((l) => l !== LABEL);
      } else {
        removalFailed = true;
        const reason = err.message || String(err);
        const msg = `Failed to remove ${LABEL} (${err.status || 'error'}): ${reason}. Publishing review-gate status anyway.`;
        if (typeof core.warning === 'function') core.warning(msg);
        else core.info(msg);
      }
    }
  }

  let result = evaluate({ labels, headSha, entries, pushed, carry, reviewVerdict });
  // A failed removal must never pass the gate: the label is still stale, or the
  // marker is missing. evaluate() already fails in those cases; this is a guard.
  if (removalFailed && result.state === 'success') {
    result = {
      state: 'failure',
      description: truncate(`Failed to remove stale ${LABEL}; ${String(headSha || '').slice(0, 7)} not cleared`),
    };
  }
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
  if (result.state !== 'success' && typeof core.setFailed === 'function') {
    core.setFailed(result.description);
  }
  return { headSha, ...result };
}

module.exports = {
  LABEL, STATUS_CONTEXT, MARKER_RE, stripQuotedAndFenced, parseMarkers, latestVerdict, latestVerdictsBySha,
  latestReviewVerdict, headVerdict, evaluate, commentEntries, shouldRemoveLabel, resolveCarry,
  prNumberFromContext, run,
};
