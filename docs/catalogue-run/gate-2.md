# GATE 2 — Search cutover

**Status:** locked — open only after [build-2.md](./build-2.md) is done.

Keep Episode dual-key on (from GATE 1). Do not drop it here.

---

## Done when

New search index is live; facets and “all kinds” behaviour verified.

## Steps (when unlocked)

| # | Do this |
|---|---------|
| 1 | **DEPLOY** — Indexer → Discover → Api + CLIs + Search; then Worker; then Site (prefer offline index first) |
| 2 | Backup search definition + sample docs |
| 3 | Populate index if needed |
| 4 | Cutover readers/writers |
| 5 | Verify facets |

## When GATE 2 is done

Open **only** [build-3.md](./build-3.md).
