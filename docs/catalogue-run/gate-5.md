# GATE 5 — Corpus migrate + close

**Status:** locked. The writer is in code (PR #1049): `CatalogueMigrate --apply` writes **only** with the exact human consent phrase. Without it, `--apply` exits 2. Agents must never pass `--consent` or `--apply` (see [`.cursor/rules/catalogue-migrate-cli-dry-run.mdc`](../../.cursor/rules/catalogue-migrate-cli-dry-run.mdc)). Dry-run is allowed.

**Checklist with exact commands:** [definition-of-done.md](./definition-of-done.md)

## Writer design (`Console-Apps/CatalogueMigrate`)

| Rule | How |
|------|-----|
| Order News → Film → TV (S-007) | One `--kind` per run, run in that order. Identify gives each podcast exactly one kind. |
| Keep ids (S-006) | NewsOrganisation/TvShow id = podcast id. Every playable keeps its episode id. Film id = episode id. |
| Search swap | `MergeOrUpload` of same-id documents with `contentKind` (active episodes only). An extra news channel uses the canonical organisation name. |
| Move, don't copy | Destination upsert → verify count → search swap → delete episodes → delete podcast. Removed episodes move too and stay removed. |
| S-008 | News apply only for `CatalogueNewsOrganisationAllowlist` publishers, and only when every active episode is news. |
| Auto-indexed podcasts | Blocked (`IndexAllEpisodes` / `EpisodeIncludeTitleRegex`), because Film/TV/News parents are not indexed. |
| Conflicts | A podcast id that already has the other parent kind is skipped and logged as a `conflict`. |
| Dry-run = apply plan | Both modes log the same `Catalogue migrate plan … apply= block=` lines from one planner. |
| Evidence | Each batch writes one JSONL file in `catalogue-migrate-evidence\`. A `begin` record holds the full podcast and every episode **before** any write, then `destWritten`, `verified`, `searchSwapped`, `sourceDeleted`, `completed` (or `failed`/`conflict`). |
| Idempotent / resumable | Every write is an upsert or an idempotent delete. On failure the run stops (exit 3) with the source rows kept. Re-running the same command resumes. `--limit` and `--batch-size` bound the run. |
| Rollback | `--rollback <file>` (dry-run) or with `--apply` plus the rollback phrase. It restores the podcast and episodes from `begin`, swaps search back to Episode, then deletes the destination rows. It refuses when the parent now holds playables the batch did not write. |
| Consent | `--consent "I approve CatalogueMigrate --kind <Kind> production writes"`, or for rollback `"I approve CatalogueMigrate rollback of <file name> in production"`. Exact, case-sensitive. The kind or file name is inside the phrase, so consent can't be replayed for another run. |

Note: snapshots are typed `Podcast`/`Episode`. Cosmos fields that are not on the models (for example leftover `urls.bbc`) are not in the evidence. The full backup (step 2) is the authoritative restore.

## Steps (when unlocked)

| # | Do this |
|---|---------|
| 1 | **DEPLOY** Indexer → Discover → Api + CLIs **only when the human names that deploy** |
| 2 | Full backup (`CosmosDbDownloader`) |
| 3 | Dry-run identify and plans. Review the `block=` lines. |
| 4 | Apply News, then Film, then TV, **only with the human's own consent phrase**. Start with `--limit 5` and spot-check. Search is swapped per podcast. |
| 5 | Only now: drop Episode dual-key/bridges if the audit is still green → redeploy Functions |
| 6 | Worker / Site follow-ups if needed **and named** |
| 7 | Spot-check; deprecate old metrics |

Epic close when step 7 is green.
