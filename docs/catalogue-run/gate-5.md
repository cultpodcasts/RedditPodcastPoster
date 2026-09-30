# GATE 5 — Corpus migrate + close

**Status:** locked — tooling is on `main` (#1001). Current binaries are **dry-run only**; `--apply` **always exits 2**. Do not implement or run writes from unlocking this gate. Agents must not pass `--apply`. Dry-run is allowed.

## Steps (when unlocked)

| # | Do this |
|---|---------|
| 1 | **DEPLOY** Indexer → Discover → Api + CLIs **only when the human names that deploy** |
| 2 | Full backup |
| 3 | Migrators **dry-run only**. Current binaries: `--apply` exits 2; no Cosmos/Search writes. Do not implement or run writes from this gate unlock. A future GATE 5 that really applies needs a later binary **plus** named `--apply` **plus** S-008 (News allowlist) — not this checkout. |
| 4 | Reindex Search after each kind batch |
| 5 | Only now: drop Episode dual-key/bridges if audit still green → redeploy Functions |
| 6 | Worker / Site if needed **and named** |
| 7 | Spot-check; deprecate old metrics |

Epic close when step 7 is green.
