# Build 1 — Schema & persistence

**Status (historical):** Build 1 and GATE 1 completed; `films-refactor/` was retired in #1406. Do **not** restore that folder to re-run cutover. This file is the archived Build 1 checklist.

**PR:** [#988](https://github.com/cultpodcasts/RedditPodcastPoster/pull/988)

GATE 1 and later steps were out of scope until this Build finished — that sequence is complete history now.

---

## Ready?

| | |
|--|--|
| Build work | **Done** (models, dual-key, rewrite CLI + tests — `films-refactor/` since retired in #1406) |
| **Outcome** | **Merged to `main`** (historical: included `films-refactor/`) |
| After that | Opened [gate-1.md](./gate-1.md) — deploy / run the tool happened there (completed; do not re-run) |

### When was the rewrite app used?

| When | What you do with it |
|------|---------------------|
| **Build 1 (then)** | **Shipped it** — did **not** run against production yet |
| **GATE 1 step 4** | Ran **dry-run** (journal + affected ids; no writes) |
| **GATE 1 step 6** | Ran **`--apply`** (explicit approval; after backup + pause) |
| **If apply went wrong** | Would run **`--rollback --journal … --apply`** |

So: **had it for Build 1 · ran it in GATE 1 only** (tooling since retired in #1406).

---

## Done when

1. Code was **merged to `main`** and CI was green, **and**
2. `films-refactor/EpisodeWireRewrite` was on `main` (not in `.slnx`; folder retired in #1406 after GATE 1).

An open PR was not done — that bar was met.

---

## Goal (this Build only)

Ship models + repos for the new catalogue types, **plus** the rewrite tool GATE 1 will run.  
Production keeps serving today’s Episodes.  
No public Film / TV / News UI yet.

---

## Checklist

- [~] Models (Publisher, Playable, Film, TV, News, releaseSort)
- [~] Old Episode field names still readable (bridges)
- [~] Queries understand old **and** new names (dual-key)
- [~] Repos + DI for five new Cosmos containers
- [~] Tests
- [x] Rewrite console app in `films-refactor/` (not in `.slnx`; progress + journal + rollback; retired #1406)
- [x] Merge to `main`

Parked (not required for Build 1 done): [#989](https://github.com/cultpodcasts/RedditPodcastPoster/issues/989) DRY Cosmos helper.

---

## When Build 1 is done

1. Merge complete.
2. Opened **only** [gate-1.md](./gate-1.md).
3. Did not open build-2 until GATE 1 said you may.

---

## Dig deeper (still Build 1 only)

| Topic | Where |
|-------|--------|
| **Rewrite console requirements** | [build-1-rewrite-console-requirements.md](./build-1-rewrite-console-requirements.md) |
| Tool README | Historical: lived at `films-refactor/README.md` (retired #1406) |
| **Repair if rollback fails** | [repair.md](./repair.md) (archived from `films-refactor/REPAIR.md`; retired #1406) |
| Why dual-key / field renames | Epic § Episode JSON cutover |
| What ships in the PR | PR #988 description |
| Deploy scripts (used in GATE 1, not now) | [deployment.md](../../deployment.md) |
