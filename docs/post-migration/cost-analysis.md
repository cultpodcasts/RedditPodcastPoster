# Post-migration cost analysis

Operational notes for hourly indexing cost and wall-clock after the Cosmos split-container migration. Keep the **next step** named at the bottom.

## Parallel Indexer passes (#1560)

App setting `indexer__ParallelPasses`: `Off` (default / absent), `OddHours`, `All`.

- **Absent = Off** — no bicep / `run_provision` required to ship dark.
- `IndexIdProvider` reads the setting and returns `ParallelPasses` on its response; `HourlyOrchestration` must not read configuration (Durable replay).
- When parallel is on, both of the hour's Indexer activities are scheduled before either is awaited; results merge via `IndexerContextMerge`.
- **YouTube-enabled hours** (`hourUtc % 3 == 0`) stay sequential with log `reason='youtube-hour'`: `YouTubeQuotaUsageTracker.FlushToCosmosAsync` writes an absolute total, so two workers would overwrite each other. Prefer this over a delta flush until a safer store exists.
- Log line: `HourlyOrchestration parallel-passes='…' mode='…' reason='…' hour-utc='…'`.
- Soak (Jon only — do not flip from agents): stage A `OddHours` 3 days, then B `All` 7 days. Queries and pass/fail criteria: GitHub issue #1560 and [indexing-app-insights-queries.md](../indexing-app-insights-queries.md) § Parallel-passes soak.

Baseline before #1560 (7 days to 26 Nov 2026): even-hour Indexer wall p50 289 s / p95 404 s; odd-hour p50 149 s / p95 270 s. Spotify 429s: zero.

## Next step

Soak `indexer__ParallelPasses` (OddHours → All) and decide whether to leave All on; do not tune Spotify/Apple rate limits in place if 429s appear — roll back to Off and report on #1560.
