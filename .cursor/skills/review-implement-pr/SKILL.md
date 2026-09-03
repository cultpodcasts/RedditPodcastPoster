---
name: review-implement-pr
description: >-
  Run a reviewer + implementor subagent pair that collaborates only through
  GitHub Pull Request conversations. The reviewer posts prioritized findings
  (diff and touched files, code smells, SOLID, tests, devops the repo uses);
  the implementor replies FIX / PARTIAL / WONTFIX / NEED DIRECTION and lands
  high-quality code. Cycles until threads resolve or leftover work becomes
  kanban tickets shown to the human. Use when the user asks to
  review-implement a PR, run reviewer and implementor on a pull request,
  leave GitHub review comments for an implementor, or wants a flagship
  5-star PR review cycle.
---

# Review–implement PR

Parent agent **orchestrates only**. Do not review or patch the PR yourself. Launch **exactly two** `generalPurpose` subagents: **reviewer** then **implementor**. They talk **only** via GitHub PR review conversations (`gh`). Do **not** merge unless the user explicitly asked to merge in the **current** conversation.

Read [reviewer.md](reviewer.md) and [implementor.md](implementor.md) before launching. Comment verbs: [comments.md](comments.md). This-org kanban defaults: [this-org.md](this-org.md).

## When to use

User names a PR (number, URL, or branch) and wants the review/implement pair, or says `review-implement-pr`.

If the PR is ambiguous, ask once. Then start.

## Setup (parent)

1. Resolve `owner/repo`, PR number, clone path, default branch.
2. `gh pr view <n> --repo <owner/repo> --json url,title,headRefName,baseRefName,files,state`
3. Infer **languages and systems from the PR files** (do not hard-code a stack). Point both agents at this repo’s `AGENTS.md`, `.cursor/rules/`, CI workflows, and deploy/docs the **repo actually uses**.
4. Copy freeze from this conversation (do not merge, do not deploy, same PRs, no episode writes, etc.).

## Pair loop

Repeat until the reviewer states a merge verdict **and** every thread is resolved **or** ticketed:

| Step | Who | Wait |
| --- | --- | --- |
| 1 | Launch **reviewer** (`run_in_background: true` unless user wants blocking) | Reviewer must **finish posting** inline + summary comments before step 2 |
| 2 | Launch **implementor** (or `resume` it) | Implementor replies on threads, then codes, commits, pushes to the **PR branch** |
| 3 | `resume` **reviewer** | Re-read each changed thread + new commits; comment on the implementor’s work; resolve or escalate |
| 4 | If unresolved **NEED DIRECTION** or reviewer **emphasises FIX** | `resume` **implementor** |

Do not start the implementor before the first review comments exist (`gh api repos/<owner>/<repo>/pulls/<n>/comments` non-empty, or reviewer reported none).

Cap **three** full cycles unless the user says continue. After the last cycle, the reviewer still gives a merge verdict.

## Launch shape

`subagent_type: generalPurpose`. Distinct `description` values (`PR <n> reviewer`, `PR <n> implementor`). Embed the matching prompt file **plus**:

```text
owner/repo: …
PR: https://github.com/<owner>/<repo>/pull/<n>
Clone: <absolute path>
Head branch: …
Languages/systems (from PR files): …
Repo gold-standard paths: AGENTS.md, .cursor/rules, CI, deploy docs actually in this repo
Kanban: follow this-org.md if present, else gh project list for the repo/org
Freeze (verbatim from parent conversation): …
Do not merge. Do not deploy unless the freeze names an exact deploy.
```

## Parent reports to the human

When the pair stops, list:

1. Reviewer merge verdict (acceptable / not)
2. Threads resolved vs left open
3. **Every new kanban ticket** (URL + title) — outstanding WONTFIX/PARTIAL with no consensus
4. Commits pushed to the PR branch (SHAs)

Never `gh pr merge`. Never treat “acceptable for merging” as permission to merge.
