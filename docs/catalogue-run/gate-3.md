# GATE 3 — Submit flag on

**Status:** open — [build-3.md](./build-3.md) is done. `submitContentTypes__Enabled=true` on `api-infra` (no staging slot; new submits only). Dry-run samples classified a BBC news URL as NewsReport, a Netflix title link as rejected, and Netflix watch `80057281` as TvShowEpisode. Nothing was persisted.

## Steps (when unlocked)

| # | Do this |
|---|---------|
| 1 | **DEPLOY** Indexer → Discover → Api + Worker (+ Site only if submit client needs it) |
| 2 | Backup Podcasts + Episodes |
| 3 | Flag on staging; classify samples |
| 4 | Flag on prod for **new** submits only |
| 5 | Confirm nothing mis-filed into Podcast |

## When GATE 3 is done

Open **only** [build-4.md](./build-4.md).
