# GATE 5 — Corpus migrate + close

**Status:** locked — tooling is on `main` (#1001). **Do not `--apply` CatalogueMigrate / CatalogueMigrateIdentify without explicit human consent in the current conversation.** Agents must not pass `--apply`. Dry-run is allowed.

## Steps (when unlocked)

| # | Do this |
|---|---------|
| 1 | **DEPLOY** Indexer → Discover → Api + CLIs **only when the human names that deploy** |
| 2 | Full backup |
| 3 | Migrators **dry-run**. `--apply` per kind (News → Film → TV) **only if the human names `--apply` and consents to Cosmos writes**. News needs an allowlist (S-008). Agents never run `--apply`. |
| 4 | Reindex Search after each kind batch |
| 5 | Only now: drop Episode dual-key/bridges if audit still green → redeploy Functions |
| 6 | Worker / Site if needed **and named** |
| 7 | Spot-check; deprecate old metrics |

Epic close when step 7 is green.
