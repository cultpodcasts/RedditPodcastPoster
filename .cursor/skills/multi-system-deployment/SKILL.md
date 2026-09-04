---
name: multi-system-deployment
description: >-
  Operator-led multi-system production rollout across website (Pages), Api
  Worker, and Azure Functions, with a watch-file Live status, in-step %, and
  human gates. Use when running a coordinated deploy, HITL backup, freeze,
  soak, “start 0a/0b”, completing PRs vs script-deploying Functions, or when
  the user says they are the operator and the agent must not skip Expect/check.
---

# Multi-system deployment (operator)

Human is the operator. Agent executes **only the named step**, then updates the **watch file** and **stops at HITL**. Do not treat “start the rollout” as permission to finish the train.

This skill is the **procedure**. Always-on safety still lives in repo rules (`no-api-website-deploys`, production blob/request truth, secrets parity, episode writes). If the **live watch file** or the user’s freeze in this conversation conflicts with a runbook, **follow the freeze**.

## When to use

- Coordinated ship across **website**, **Api Worker**, **Functions** (and optional CLIs / R2 / Cosmos tools)
- User is gating **Expect / check** (HITL)
- Long backups or deploys that need **% of the current step**
- User says start a numbered step (`start 0a`, `continue 0b`, `complete the website PR`)

## Roles

| Human | Agent |
| --- | --- |
| Inspect backups; name the next step; merge/complete PRs they name; any `apply: true`; feed/homepage publish | Run the named step; keep Live status honest; stop at HITL; never infer deploy/merge/publish |

## Watch file (required)

Pick one markdown file (this org: `docs/episode-services-deploy-plan.md` when that rollout is live). Edit **in place**. Tell the user to watch it.

### Live status (always at the top)

```markdown
## Live status

Updated: **<local datetime>**

### NOW: <step id> <short name> **<n>%**

One-paragraph: what is running, write targets, what is forbidden this step.

In-progress rows always show **% of that step** (not of the whole rollout).

| Step | Status | % of step | Notes |
| --- | --- | --- | --- |
| … | done / IN PROGRESS / WAITING / not started | n% or — | … |
```

Rules:

- **NOW** is a single line. Only one step is `IN PROGRESS`.
- **% of step** uses real signals (CLI bars, file counts vs a prior dump, blob timestamp after script). Never leave `IN PROGRESS` at **0%** while work is running.
- File-weight long dumps (episodes dominate Cosmos). Also quote container bars.
- Duplicate/stale table rows (e.g. still “blocked on 0pre” after 0pre is done) must be removed.
- After a HITL stop: `NOW` is the inspect gate, not the next automatable step.

### Heartbeat on long steps

While a dump/deploy is running, refresh Live status on a **bounded** ~45s one-shot wake (re-arm only if still running). Do **not** start an unbounded `while true` loop. Do **not** recursively count a huge existing dump folder during a live write (IO steal).

When the process **exits 0**: set that step **100%**, move NOW to HITL if the plan says stop, **do not** start the next step, **do not** re-arm.

## HITL

Hard-stop steps (typical): Cosmos dump inspect, freeze accept, post-site checks, post-Worker feed GET, post-Functions blob+R2, soak raw JSON, feed publish.

After a hard stop, wait for a phrase that **names** the next step (e.g. “Cosmos backup looks good — continue 0b”). Silence is not a go.

## Ship paths (this org)

Never run Wrangler/Pages deploy CLIs unless the user names that exact deploy in this conversation.

| System | How production ships | Agent must not |
| --- | --- | --- |
| **website** | **Complete** the website PR (Pages from git) | `npm run deploy` / `wrangler pages deploy` |
| **Api Worker** | **Complete** the Api PR. Live is top-level Worker **`api`** | `wrangler deploy` / `npm run deploy`; `--env production` (`api-production` is the wrong Worker) |
| **Functions** | `scripts/deploy-indexer.ps1` → `deploy-discover.ps1` → `deploy-api.ps1` with explicit Azure args + `-Confirm:$false` | Infer deploy from GitHub Actions; treat PR complete as Functions live |
| **CLIs** | `scripts/publish-console-apps.ps1` from the **freeze branch** → `artifacts/tools/` | PATH/`dotnet tool` exes from an older publish |

**Merge is a release step, not paperwork.** Do not complete all three PRs “because they are one feature.” Order and “leave this PR open” come from the **watch file / user freeze**.

If the freeze says **script-deploy Functions from the open branch** and **do not complete** the Functions PR through soak: do that even if an older runbook still says merge-then-deploy.

Phrase **deploy functions & clis** is full approval for the Functions+CLI script sequence (see `deploy-functions-and-clis` rule). It is **not** approval to complete PRs, publish feed, or run Wrangler.

## Production truth

| Claim | Authoritative | Forbidden as go/no-go |
| --- | --- | --- |
| Functions version on prod | Deployment blob `lastModified` + restart near that time | GitHub Actions, Kudu, “PR merged” |
| Orchestration ran | App Insights **`AppRequests`** | `AppTraces` alone |
| Feed/lookups live | R2 object / Worker GET bytes | CI green |
| Cosmos shape | Raw document | Typed dump omitting leftover JSON fields |

If blob was not checked this conversation: say **deploy time unknown — blob not checked**.

## Backups

- New dump = **new dated sibling folder** under `CultPodcasts-PrivateDatabase` (`YYYY-MM-DD`). Folder must **not** already exist.
- **Never** write into an existing dated folder. **Never** `--overwrite`.
- Cosmos downloader serializes **typed models** — leftover Cosmos fields not on the DTO **will not** appear in dump JSON. Say that at HITL.
- Build CLIs on the freeze branch **before** dump/publish so the exe matches the code under freeze.
- Do not `PublishR2 all` when the step is lookups-only. Lookups ≠ feed ≠ homepage.

## Forbidden unless explicitly named this conversation

- Complete/merge a PR the freeze says leave open
- `apply: true` / production Cosmos episode writes
- Feed or homepage publish
- Recreate search index / drop compact id fields
- “Fix” a known soak bug with a bulk write
- Commit secret values

New Worker/Pages secrets: document names in PR `## Config / secrets` for **preview and production**; live Worker secrets go on top-level **`api`**.

## Start of a named step

1. Confirm the watch file’s NOW is that step (or the user named it).
2. Confirm write targets (new folder, branch, which apps).
3. Start the work; immediately set Live status to **IN PROGRESS** with a real % as soon as a signal exists.
4. On success: Expect/check from the plan. If HITL, stop.
5. On failure: stop the train; do not “continue with 0b anyway.”

## Additional resources

- Per-system commands and this-org defaults: [systems.md](systems.md)
- Current episode-services instance: `docs/episode-services-deploy-plan.md` and `docs/episode-services-ops-runbook.md` in RedditPodcastPoster
