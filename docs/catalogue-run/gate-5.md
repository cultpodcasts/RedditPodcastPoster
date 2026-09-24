# GATE 5 — Corpus migrate + close

**Status:** locked — open only after [build-5.md](./build-5.md) is done.

## Steps (when unlocked)

| # | Do this |
|---|---------|
| 1 | **DEPLOY** Indexer → Discover → Api + CLIs |
| 2 | Full backup |
| 3 | Migrators dry-run then `--apply` per kind (News → Film → TV) |
| 4 | Reindex Search after each kind batch |
| 5 | Only now: drop Episode dual-key/bridges if audit still green → redeploy Functions |
| 6 | Worker / Site if needed |
| 7 | Spot-check; deprecate old metrics |

Epic close when step 7 is green.
