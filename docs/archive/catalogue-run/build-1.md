# Build 1 — Schema & persistence

**Status:** ready to merge — rewrite tool + #988 code done; land on `main` to finish Build 1  
**PR:** [#988](https://github.com/cultpodcasts/RedditPodcastPoster/pull/988)

Ignore GATE 1 and everything after until this file is done.

---

## Ready?

| | |
|--|--|
| Build work | **Done** (models, dual-key, rewrite CLI + tests — `films-refactor/` since retired in #1406) |
| **Next action** | **Merge to `main`** (historical: included `films-refactor/`) |
| After that | Open [gate-1.md](./gate-1.md) only — that is when you deploy / run the tool |

### When is the rewrite app used?

| When | What you do with it |
|------|---------------------|
| **Build 1 (now)** | **Ship it** — do **not** run against production yet |
| **GATE 1 step 4** | Run **dry-run** (journal + affected ids; no writes) |
| **GATE 1 step 6** | Run **`--apply`** (explicit approval; after backup + pause) |
| **If apply goes wrong** | Run **`--rollback --journal … --apply`** |

So: **have it now · run it in GATE 1 only.**

---

## Done when

1. Code is **merged to `main`** and CI is green, **and**
2. `films-refactor/EpisodeWireRewrite` was on `main` (not in `.slnx`; folder retired in #1406 after GATE 1).

An open PR is not done.

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
- [ ] Merge to `main`

Parked (not required for Build 1 done): [#989](https://github.com/cultpodcasts/RedditPodcastPoster/issues/989) DRY Cosmos helper.

---

## When Build 1 is done

1. Merge complete.
2. Open **only** [gate-1.md](./gate-1.md).
3. Do not open build-2 until GATE 1 says you may.

---

## Dig deeper (still Build 1 only)

| Topic | Where |
|-------|--------|
| **Rewrite console requirements** | [build-1-rewrite-console-requirements.md](./build-1-rewrite-console-requirements.md) |
| Tool README | Historical: lived at `films-refactor/README.md` (retired #1406) |
| **Repair if rollback fails** | Historical: lived at `films-refactor/REPAIR.md` (retired #1406) |
| Why dual-key / field renames | Epic § Episode JSON cutover |
| What ships in the PR | PR #988 description |
| Deploy scripts (used in GATE 1, not now) | [deployment.md](../../deployment.md) |
