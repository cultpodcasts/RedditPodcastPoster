# GATE 2 — Search cutover

**Status:** locked until [build-2.md](./build-2.md) is merged. Do not run this until then.

Keep Episode dual-key on (from GATE 1). Do not drop it here.

The rebuilt index uses `title`, `seriesName`, and `description`. It does not also store `episodeTitle`, `podcastName`, or `episodeDescription`. It does not store `seriesDescription`. Search can be down for this window.

The Cloudflare Api reads `podcastName` and `episodeTitle` from search (`getPageDetails`). That worker and the website have to switch to `seriesName` and `title` in the same window. A website deploy alone does not change those queries.

---

## Done when

The new index is live, search cards use the new field names, and a soak hour shows the indexer keeping up.

## Steps

| # | Do this |
|---|---------|
| 1 | Ship the website and Api that read `title`, `seriesName`, and `description`. Do not point them at production until step 4 is finished, or ship them and accept empty cards until the index is back. |
| 2 | Pause writers. Disable Indexer timers `Hourly` and `HourlyCatchUp` on `indexer-infra`. Pause Azure Search indexer `cultpodcasts-indexer` so it stops pulling. |
| 3 | Backup the current search definition and a handful of documents. |
| 4 | Tear down and recreate `cultpodcasts` with CreateSearchIndex so the index has only the new names. Run the indexer until document count is back (~86k). |
| 5 | Confirm the website and Api are the builds from step 1. |
| 6 | Turn `Hourly` and `HourlyCatchUp` back on. Resume `cultpodcasts-indexer`. |
| 7 | Soak. Watch one hourly slot in `AppRequests` (`orchestration:HourlyOrchestration`, `activity:Indexer`) and check search cards, a podcast page, and removed shows stay hidden. |

## When GATE 2 is done

Open **only** [build-3.md](./build-3.md).
