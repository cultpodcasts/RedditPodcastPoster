# Post-Migration Cost Analysis

## Summary

After the Episode Detachment Migration (completed ~March 11), daily Azure costs have approximately **doubled** compared to the pre-migration baseline. Two root causes were identified:

1. The detached-episode model has increased the number and cost of Cosmos DB queries executed by the Azure Functions, inflating **Function compute time** (GB-s billing).
2. **Historically, the old Cosmos DB account (`cultpodcasts-ukdb`) was being connected to** by Function app startup, generating background health-check calls before `P0` remediation.

## Latest Review: April 2026 spike (new)

### Evidence from user export (`C:\Users\jonbr\Downloads\cost-analysis.csv`)

- Source file columns: `UsageDate`, `ResourceGroupName`, `CostUSD`, `Cost`, `Currency`.
- Daily totals (USD) show a clear inflection on **2026-04-16** and **2026-04-17**:
  - `2026-04-15`: `~$0.07`
  - `2026-04-16`: `~$0.24`
  - `2026-04-17`: `~$0.30`
- The increase is concentrated in **`automatedinfra`**:
  - `2026-04-15`: `~$0.01`
  - `2026-04-16`: `~$0.19`
  - `2026-04-17`: `~$0.24`
- `automateddata` remained relatively steady in the same window (`~$0.05–$0.06/day`).
- Conclusion: this spike is infrastructure/execution-path driven, not primarily Cosmos data-plane growth.

### Live telemetry check for overlap and duration drift (Cultpodcasts subscription)

- Validated against subscription `a6b8f1a2-6163-41bc-aa6d-e33928939a6e` using the exact Application Insights resource `ai-infra`.
- Important correction: earlier workspace-only query appeared truncated because the resource query defaulted to a short lookback; rerunning with explicit `hours` retrieved full windows for both pre-spike and spike analysis.
- Windows analyzed:
  - Pre-spike: `2026-04-09..2026-04-15`
  - Spike: `2026-04-16..2026-04-18`

- Concurrent execution comparison (indexer/discovery orchestration + activities):
  - **Pre**: `MaxConcurrent=10`, `AvgConcurrent=4.009`, `MinutesWithOverlap=739`, `TotalActiveMinutes=894`
  - **Spike**: `MaxConcurrent=10`, `AvgConcurrent=4.128`, `MinutesWithOverlap=231`, `TotalActiveMinutes=296`

- Duration deltas (Pre → Spike):
  - `orchestration:HourlyOrchestration`: `63.0s` → `62.6s`
  - `activity:Indexer`: `16.53s` → `17.02s`
  - `orchestration:HalfHourlyOrchestration`: `14.76s` → `13.45s`
  - `activity:Poster`: `2.97s` → `2.71s`
  - `activity:Publisher`: `7.67s` → `7.21s`
  - `activity:Tweet`: `13.00s` → `12.91s`
  - `activity:Bluesky`: `2.08s` → `1.80s`
  - `activity:Categoriser`: `4.23s` → `3.45s`
  - Discovery path: `orchestration:Orchestration` `139.08s` → `48.47s`, `activity:Discover` `138.74s` → `48.14s`

- Per-function cost proxy (normalized execution-time/day, Pre → Spike):
  - `orchestration:HourlyOrchestration`: `1,512,537 ms/day` → `1,230,746 ms/day` (`-18.6%`)
  - `activity:Indexer`: `793,282 ms/day` → `669,305 ms/day` (`-15.6%`)
  - `orchestration:HalfHourlyOrchestration`: `354,288 ms/day` → `263,962 ms/day` (`-25.5%`)
  - `activity:Publisher`: `368,307 ms/day` → `283,769 ms/day` (`-23.0%`)
  - All measured indexer/discovery functions are flat/down on this execution-time proxy; no single function shows a spike-period blow-up.

- Cost concentration signal (share of request execution time):
  - **Pre top shares**: `orchestration:HourlyOrchestration 30.88%`, `activity:Indexer 16.19%`, Discovery pair combined `22.69%`.
  - **Spike top shares**: `orchestration:HourlyOrchestration 36.70%`, `activity:Indexer 19.96%`, Discovery pair combined `9.60%`.
  - Top two (`HourlyOrchestration` + `Indexer`) rose from ~`47.07%` to ~`56.66%` share, so elevated-period cost proxy is concentrated in these two functions, not broad across all functions.

- Interpretation: durable overlap exists both before and during spike; measured request-time by function does not indicate one runaway function. Elevated cost is likely meter/rate/scale behavior outside simple request-duration growth.

### Explicit next steps

1. Deploy the infrastructure and trigger changes to production.
2. Verify `Infrastructure/function.bicep` uses valid Flex Consumption bounds (`maxInstanceCount` floor `40`) and confirm no sub-40 overrides remain in `Infrastructure/functions.bicep`.
3. Track 48h daily totals; expected result is `automatedinfra` returning toward pre-spike run-rate.
4. Pull meter-level Cost Details for `automatedinfra` for Apr 15–18 and isolate top charged meters (`On Demand Execution Time`, `Total Executions`, and any storage-related meters) with day-level deltas.
5. If costs remain elevated after deployment, correlate meter spikes with hourly function invocation counts and drain/restart events from `requests` (`/admin/host/drain*`, `Hourly`, `HalfHourly`, `DiscoveryTrigger`).

## Latest Review: Cosmos Query Tuning (last 2 days)

### Changes now in place

- Episode API now supports podcast-qualified routes (`episode/{podcastIdentifier}/{episodeId}` and `episode/{podcastId}/{episodeId}`), reducing reliance on episode-only lookups.
- Recent candidate discovery (`Poster` / `Tweet` / `Bluesky`) now scopes candidate podcasts by `podcast.latestReleased` and then reads episodes with partition-scoped `GetByPodcastId(...)` queries.
- Homepage recent-episode loading now uses the same pattern: select recent podcasts first, then partition-scoped episode reads per podcast.
- `EpisodeRepository` maintains podcast `latestReleased` metadata and incrementally updates homepage active-episode count, reducing repeated broad scans.

### Current daily cost status

- Fresh capture completed on **2026-03-26 (UTC)** using Cost Management `generateCostDetailsReport` (`ActualCost`) at subscription scope.
- **24h window** (`2026-03-25` → `2026-03-26`): total **`£0.405650`**, normalized **`£0.405650/day`**.
- **48h window** (`2026-03-24` → `2026-03-26`): total **`£0.658763`**, normalized **`£0.329381/day`**.
- 48h resource-group split: `automatedinfra £0.51`, `AutomatedData £0.13`, `Cultpodcasts £0.01`, `Management ~£0.00`.
- User-provided daily export (`C:\Users\jonbr\Downloads\cost-analysis(1).csv`) aligns with a sharp inflection between **`2026-03-10`** and **`2026-03-11`**:
  - Pre baseline (`Mar 1–7`): **`~£0.005988/day`**
  - Transition window (`Mar 8–12`, includes two spikes): **`~£0.335932/day`**
  - Post steady window (`Mar 13–25`): **`~£0.216968/day`**
  - Peak day in CSV: **`2026-03-12`** at **`£0.953667`**
- Additional user-provided daily export (`C:\Users\jonbr\Downloads\cost-analysis(2).csv`) for **`Mar 20–26`** shows:
  - 7-day average: **`~£0.189650/day`** (`~$0.256682/day`)
  - Daily range: **`£0.114144`** (Mar 26) to **`£0.234267`** (Mar 20)
- Resource-scoped daily export for `indexer-infra` (`C:\Users\jonbr\Downloads\cost-analysis(3).csv`) for **`Mar 20–26`** shows:
  - 7-day average: **`~£0.146940/day`** (`~$0.198876/day`)
  - Daily range: **`£0.072220`** (Mar 26) to **`£0.187486`** (Mar 20)
  - Share of the same-window total daily average (`cost-analysis(2).csv`): **`~77.48%`**
- Cost remains materially above the pre-migration target level (historically documented as `~$0.26/day`), with `automatedinfra` still the dominant spend source.

### 24h instrumentation capture check (2026-03-27 UTC)

- Queried `loganalytics-infra` tables (`AppTraces`, `AppEvents`, `FunctionAppLogs`) over the last 72h for `indexer-infra` and found **no** `*CostProbe*` records.
- `AppTraces` severity distribution for `indexer-infra` over the last 24h is heavily skewed to warning/error (`SeverityLevel 2: 4756`, `SeverityLevel 3: 143`) with only `3` informational rows.
- Informational rows observed are telemetry-channel warnings (`TelemetryChannel found a telemetry item without an InstrumentationKey`), indicating dropped telemetry items are occurring.
- Local daily cost exports currently available (`cost-analysis(2).csv`, `cost-analysis(3).csv`) end at `2026-03-26`, so the full post-probe day (`2026-03-27`) is not yet available in local cost files.
- Remediation applied in code: all Indexer orchestration probe events (`IndexIdProvider`/`Indexer`/`Categoriser`/`Poster`/`Publisher`/`Tweet`/`Bluesky`) now emit `*.CostProbe.Start` and `*.CostProbe.Complete` at **Warning** level so they remain visible when Information traces are filtered.
- Conclusion: prior 24h run is not usable for attribution; rerun a clean 24h window after deploying the warning-level probe change.

### Objective check

## Appendix: KQL and PowerShell checks for cost-probe visibility

Short checklist to confirm whether cost-instrumented data is present in Application Insights and what to extract for correlation with cost exports.

- Ensure probes are emitted at Warning level or above in production (Information is filtered out).

- KQL: search traces/messages for probe markers (last 72h)
  - AppTraces (preferred):
    AppTraces
    | where TimeGenerated > ago(72h)
    | where AppRoleName == "indexer-infra"
    | where Message contains "CostProbe" or tostring(Properties) contains "CostProbe"
    | project TimeGenerated, AppRoleName, Message, Properties
    | order by TimeGenerated desc

  - If `Properties` is not present in your table schema, rely on `Message` or other text fields:
    AppTraces
    | where TimeGenerated > ago(72h)
    | where AppRoleName == "indexer-infra"
    | where Message contains "CostProbe"
    | project TimeGenerated, Message
    | order by TimeGenerated desc

- KQL: confirm ingestion and severity distribution (last 24h)
  AppTraces
  | where TimeGenerated > ago(24h) and AppRoleName == "indexer-infra"
  | summarize count() by SeverityLevel

- KQL: inspect informational rows (if allowed) for TelemetryChannel warnings
  AppTraces
  | where TimeGenerated > ago(24h) and AppRoleName == "indexer-infra" and SeverityLevel == 0
  | project TimeGenerated, Message, Properties

- KQL: correlate probe durations / fields (once probes appear)
  AppTraces
  | where TimeGenerated between (datetime(2026-03-25) .. datetime(2026-03-26))
  | where Message contains "IndexerCostProbe.Complete" or Message contains "PosterCostProbe.Complete"
  | extend fields = parse_json(tostring(Properties))
  | project TimeGenerated, Message, fields

- KQL: function request/duration summary (useful for cost allocation)
  AppRequests
  | where TimeGenerated > ago(48h) and AppRoleName == "indexer-infra"
  | summarize calls = count(), avgMs = avg(DurationMs), p95 = percentile(DurationMs,95) by Name

- PowerShell: inspect local cost CSV exports
  - Get last rows quickly:
    Import-Csv 'C:\Users\jonbr\Downloads\cost-analysis(3).csv' | Select-Object -Last 10
  - Convert to JSON for downstream tooling:
    Import-Csv 'C:\Users\jonbr\Downloads\cost-analysis(3).csv' | ConvertTo-Json -Compress

- Quick validation steps after deploying warning-level probes
  1. Wait for the next scheduled Indexer orchestration run (hourly) to produce probe rows.
  2. Run the AppTraces KQL above and confirm at least one `*.CostProbe.Start` and `*.CostProbe.Complete` row per activity.
  3. Export the same 24h UTC cost window from Cost Management and verify the probe window aligns with the CSV rows.

Notes
- If `Properties` is not JSON-parsable in your workspace rows, prefer extracting fields from the `Message` text or adjust your logger to emit structured properties under a consistent field (for example `Properties` or `CustomDimensions`).
- Use the `AppRequests` / `AppDependencies` / `AppTraces` tables together to build a per-activity estimate of time distribution before allocating cost by GB-s.

- Target objective: return to **`<= $0.26/day`** total Azure cost.
- Current confirmed state: **not yet met**.
- Latest measured run-rate is still elevated (`~£0.33–£0.41/day` across 48h/24h windows), so additional reductions are required.

### Billing mechanics evidence (new)

- Azure Monitor `Transactions` on Durable storage account `cultpodcastsstg` stayed broadly flat between sampled windows (`~195k–206k/day` pre and `~195k–199k/day` post), with no large post-migration spike.
- Cost Details exports show `automatedinfra` is now dominated by **`Flex Consumption - UK South` → `On Demand Execution Time`** charges.
- `automatedinfra` run-rate moved from **`£0.006647/day`** (Mar 1–7) to **`£0.237324/day`** (Mar 13–23).
- Flex execution-time rows were present in pre data but zero-priced there; first charged row appears in the post window:
  - Last zero-charge execution-time row: **`2026-03-08`**, `unitPrice=0`
  - First charged execution-time row: **`2026-03-13`**, `unitPrice=0.000037`
- Aggregate on-demand execution units/day across apps in sampled windows were not higher overall (`~6% lower` post vs pre), indicating the main step-change is chargeability/rate behavior on Flex execution time rather than a pure execution-volume increase.

## Implementation Status (feature/cost-reduction)

- ✅ **P0 complete**: old Cosmos DB connection removed from active function execution path.
- ✅ **P2 complete**: `PostNewEpisodes` over-fetch reduced.
- ✅ **P3 (interim) complete**: `ActiveEpisodeCount` now refreshes weekly in the same Monday window as `TotalDuration` (and still initializes when cache is empty).
- ✅ **P1 complete (deployed 2026-06-15):** orchestration-level recent-candidate preload — `LoadRecentCandidates` activity loads once per run; `IndexerContext.RecentEpisodeCandidates` passed to Categoriser, Poster, Tweet, and Bluesky. Eliminates duplicate `GetRecentActiveEpisodes` calls across Durable activity boundaries (Hourly: 4 loads → 1; HalfHourly: 2 loads → 1). **Awaiting 24h Episodes/Query RU validation** vs Phase 2 baseline.
- ✅ **P4 out (deployed), under observation**: social recent-candidate reads, `HomepagePublisher` recent episodes, and `RecentPodcastEpisodeCategoriser` now use podcast-level `latestReleased` metadata plus partition-scoped episode reads to avoid broad cross-partition scans. Treated as complete implementation, pending stability confirmation.
- ✅ **P5 complete (deployed 2026-06-15, with P1):** shared `postingCriteria.MaxDays` lookback loaded once in orchestration; consumers (`EpisodeProcessor`, `PodcastEpisodeProvider`, `RecentPodcastEpisodeCategoriser`, `Tweeter`, `BlueskyPostManager`) filter preloaded candidates by service-specific day settings (`RedditDays`, `TweetDays`, `BlueSkyDays`, `CategoriserDays`) instead of re-querying Cosmos. Fallback to `IRecentEpisodeCandidatesProvider` when preloaded set is absent (tests, console apps).
- 🔄 **P7 instrumentation remediation in progress**: probe logs were previously emitted at Information level (filtered in production). Code now emits all `*.CostProbe.*` events at Warning level across Indexer activities; deployment and fresh 24h capture are pending.
- ✅ **`latestReleased` 4-week backfill run locally (ThrowawayConsole)**: `RecentEpisodes=2285`, `PodcastsWithRecentEpisodes=691`, `UpdatedPodcasts=691`, `MissingPodcasts=0`.
- ⏳ **Remaining: P6** (HalfHourly scope reduction) plus **P1/P5 post-deploy RU validation** (24h window vs Phase 2 baseline).

## Daily Cost Trends (USD)

### Pre-Migration Baseline (Mar 1–7)

| Resource Group | Avg $/day | Contents |
|----------------|-----------|----------|
| **cultpodcasts** | ~$0.25 | CosmosDB (embedded episodes) |
| **automatedinfra** | ~$0.008 | Azure Functions (Indexer, Discovery, API) |
| **Total** | **~$0.26** | |

### Post-Migration Steady State (Mar 13–23)

| Resource Group | Avg $/day | Contents |
|----------------|-----------|----------|
| **automateddata** | ~$0.19 | CosmosDB (V2 Podcasts + Episodes containers) |
| **automatedinfra** | ~$0.30 | Azure Functions (same apps, more expensive queries) |
| **cultpodcasts** | ~$0.006 | Residual (old CosmosDB still receiving background pings) |
| **Total** | **~$0.50** | |

### Key Observations

1. **CosmosDB costs shifted** from `cultpodcasts` ($0.25/day) to `automateddata` ($0.19/day) — roughly comparable, slightly lower.
2. **Functions compute cost exploded**: `automatedinfra` went from **$0.008/day → $0.30/day** (a **~37× increase**).
3. The total daily cost has gone from ~$0.26 to ~$0.50 — an increase of **~$0.24/day**.
4. The overwhelming driver of the increase is **Function execution time** (Consumption Plan GB-s billing), caused by more and slower Cosmos DB queries per orchestration run.

## Application Insights Evidence

### Pre vs Post-Migration Function Duration Comparison

| Activity | Pre (Mar 1–7) avg/run | Post (Mar 22–24) avg/run | Change |
|----------|----------------------|-------------------------|--------|
| `HourlyOrchestration` | 98s | 107s | **+9%** |
| `Indexer` (×4 passes) | 19.4s | 18.6s | -4% |
| `HalfHourlyOrchestration` | 9.6s | 12.2s | **+27%** |
| `Bluesky` | 3.9s | 4.8s | **+23%** |
| `Publisher` | 3.6s | 3.6s | ~same |
| `Poster` | 2.7s | 2.8s | ~same |
| `Categoriser` | 3.6s | 4.9s | **+36%** |
| `Tweet` | 4.2s | 2.4s | -43% |
| `Discovery` | 211s | 20s | -90% |
| `IndexIdProvider` | 0.7s | 0.6s | ~same |

**Key insight**: Per-activity durations have only modestly increased. Categoriser (+36%), Bluesky (+23%), and HalfHourlyOrchestration (+27%) show the largest increases, but these are seconds not minutes. The Indexer, Publisher, and Poster are essentially unchanged. **Function compute time (GB-s) alone cannot explain a 37× cost increase.**

### Pre vs Post-Migration Cosmos DB Call Volume

| Period | Targets | Calls/hour | Change |
|--------|---------|-----------|--------|
| **Pre-migration** | cultpodcasts-ukdb only | **32.9/hr** | — |
| **Post-migration** | V2 new + old leaked | **70.9/hr** | **+115%** |

Cosmos DB calls per hour roughly doubled, but this translates to modest additional Function execution time (~37s additional Cosmos time per 48h). The calls themselves are faster on average (26ms V2 vs 59ms old regional).

### Revised Root Cause: Azure Storage Transactions

The `automatedinfra` cost increase ($0.008 → $0.30/day = **37×**) cannot be explained by the modest increase in Function execution time or Cosmos calls. The Consumption Plan's free tier (400,000 GB-s/month) likely covers the compute for both pre and post-migration workloads.

**The likely primary cost driver is Azure Storage transactions** from the Durable Functions task hub. Every orchestration activity invocation generates:
- Queue messages (dispatch + completion)
- Table entity writes (orchestration history)
- Blob operations (large message payloads)

The migration didn't change the number of activities per orchestration, but the overall system may be generating more storage operations due to larger payloads or different serialisation patterns with the detached-episode model.

### Post-Migration Function Execution Times (48h detail)

| Activity | Executions (48h) | Avg Duration | Total Duration | Runs in |
|----------|-----------------|-------------|----------------|---------|
| `HourlyOrchestration` | 24 | **107s** | 2,570s | Hourly |
| `Indexer` (×4 passes) | 95 | 18.6s | 1,771s | Hourly |
| `HalfHourlyOrchestration` | 24 | 12.2s | 293s | HalfHourly |
| `Bluesky` | 48 | 4.8s | 230s | Both |
| `Publisher` | 48 | 3.6s | 172s | Both |
| `Poster` | 48 | 2.8s | 136s | Both |
| `Categoriser` | 24 | 4.9s | 116s | Hourly |
| `Discovery` | 4 | 20.0s | 80s | 4×/day |
| `Tweet` | 24 | 2.4s | 57s | Hourly |
| `IndexIdProvider` | 23 | 0.6s | 14s | Hourly |

### Dependency Call Breakdown (Post-migration 48h)

| Target | Calls | Avg ms | Total Time | Purpose |
|--------|-------|--------|-----------|---------|
| Apple Podcasts API | 3,817 | 186ms | 711s | Episode enrichment |
| Spotify API | 3,038 | 169ms | 512s | Episode enrichment |
| YouTube API | 1,198 | 119ms | 143s | Episode enrichment |
| **cultpodcasts-db (V2, new)** | **3,034** | **26ms** | **79s** | Active V2 queries |
| Bluesky | 183 | 526ms | 96s | Social posting |
| **cultpodcasts-ukdb (OLD!)** | **255** | **125ms** | **32s** | Leaked V1 connection |
| Cloudflare | 109 | 588ms | 64s | R2/KV |
| Reddit | 217 | 278ms | 60s | Posting |
| **cultpodcasts-ukdb-uksouth (OLD!)** | **112** | **43ms** | **5s** | Leaked V1 regional |

Pre-migration Cosmos DB baseline (Mar 1–7): **only** the old DB was used — 4,733 calls in 7 days (676/day, 22+11 calls/hour). Post-migration the old DB still receives ~180 leaked calls/day.

### Critical Finding: Old Cosmos DB Still Connected

Two Cosmos DB accounts exist:

| Account | Resource Group | Status |
|---------|---------------|--------|
| `cultpodcasts-db` | **automateddata** | ✅ Active V2 DB — correct |
| `cultpodcasts-ukdb` | **cultpodcasts** | ⚠️ OLD DB — **still receiving 367 calls per 48h** |

**Root cause**: `CosmosDbContainerFactory` takes `[FromKeyedServices("v1")] CosmosClient` as a constructor dependency. Even though only V2 container methods are used at runtime, the V1 `CosmosClient` is instantiated by DI, which:
- Connects to `cultpodcasts-ukdb` on every Function cold-start
- Runs SDK background health-checks every ~5 minutes (`GET https://cultpodcasts-ukdb.documents.azure.com:443/`)
- Generates ~180+ wasted calls/day at ~125ms each

The old `cosmosdb` connection settings are deployed to all Function apps via `coreSettings` in `Infrastructure/functions.bicep`.

## Why Functions Cost More

With embedded episodes, reading a podcast + all its episodes was a **single point-read** (one Cosmos document). Now:

- Reading a podcast is one query, and reading its episodes is a **second query** to a separate container.
- Many operations need episodes from **multiple podcasts** and use **cross-partition queries** on the Episodes container (partitioned by `podcastId`). These fan out to every physical partition.
- Some operations fetch **all episodes** for a podcast when they only need a filtered subset.
- The result: each orchestration run makes **many more network round-trips**, takes **much longer**, and the Consumption Plan bills for that extra GB-seconds.

## Hot Path Inventory

The following analysis catalogs every significant Cosmos DB query made by the scheduled Functions, noting whether it's cross-partition and how frequently it runs.

### Schedules

| Trigger | Schedule | Frequency |
|---------|----------|-----------|
| **Hourly** | `0 */1 * * *` | Every hour, 24/day |
| **HalfHourly** | `30 */1 * * *` | Every hour at :30, 24/day |
| **Discovery** | `30 3/6 * * *` | 4 times/day |

### Hourly Orchestration Activities

| # | Activity | Data Access | Cross-Partition? | Calls/hour |
|---|----------|-------------|-----------------|------------|
| 1 | `IndexIdProvider` | `PodcastRepositoryV2.GetAllBy(filter, projection→Id)` on **Podcasts** | Yes (Podcasts) | 1 |
| 2 | `Indexer` (×4 passes) | `EpisodeRepository.GetByPodcastId(id, releaseSinceFilter)` per podcast | No (partition-scoped) ✓ | N podcasts × 4 |
| 2b | `Indexer` | `EpisodeRepository.Save(episodes)` per podcast | No (partition-scoped) ✓ | varies |
| 3 | `Categoriser` | `EpisodeRepository.GetAllBy(x => x.Release > since && !x.Subjects.Any())` on **Episodes** | **YES — cross-partition** ⚠️ | 1 |
| 3b | `Categoriser` | `PodcastRepositoryV2.GetPodcast(id)` per distinct podcast | No (point-read) ✓ | varies |
| 4 | `Poster` → `EpisodeProcessor` | `EpisodeRepository.GetAllBy(x => x.Release >= threshold && !x.Posted && ..., projection→PodcastId)` on **Episodes** | **YES — cross-partition** ⚠️ | 1 |
| 4b | `Poster` → `PodcastEpisodesPoster` | `EpisodeRepository.GetByPodcastId(id).ToArrayAsync()` **fetching ALL episodes** per candidate podcast | No (partition-scoped) but **over-fetches** ⚠️ | N podcasts |
| 5 | `Publisher` → `HomepagePublisher` | `EpisodeRepository.GetAllBy(x => !x.Removed && ..., projection→Id)` count query on **Episodes** | **YES — cross-partition** ⚠️ (weekly refresh window only) | ~0/hr steady-state |
| 5b | `Publisher` → `HomepagePublisher` | `EpisodeRepository.GetAllBy(x => !x.Removed && !x.Ignored && x.Release >= 7daysAgo && ..., projection→{fields})` recent episodes on **Episodes** | **YES — cross-partition** ⚠️ | 1 |
| 5c | `Publisher` → `HomepagePublisher` | (Monday 00:00–00:20 only) `EpisodeRepository.GetAllBy(x => !x.Removed && !x.Ignored && ..., projection→Length)` duration scan on **Episodes** | **YES — cross-partition** ⚠️ | rare |
| 6 | `Tweet` → `PodcastEpisodeProvider` | `EpisodeRepository.GetAllBy(x => x.Release >= since && !x.Tweeted && ...)` on **Episodes** | **YES — cross-partition** ⚠️ | 1 |
| 6b | `Tweet` → `PodcastEpisodeProvider` | `PodcastRepositoryV2.GetPodcast(id)` per distinct podcast with candidates | No (point-read) ✓ | varies |
| 7 | `Bluesky` → `PodcastEpisodeProvider` | `EpisodeRepository.GetAllBy(x => x.Release >= since && !x.Ignored && !x.Removed)` on **Episodes** | **YES — cross-partition** ⚠️ | 1 |
| 7b | `Bluesky` → `PodcastEpisodeProvider` | `PodcastRepositoryV2.GetPodcast(id)` per distinct podcast with candidates | No (point-read) ✓ | varies |

### HalfHourly Orchestration Activities

| # | Activity | Same cross-partition queries as | Calls/hour |
|---|----------|---------------------------------|------------|
| 8 | `Poster` | Same as #4, #4b above | 1 |
| 9 | `Publisher` | Same as #5, #5b above | 1 |
| 10 | `Bluesky` | Same as #7, #7b above | 1 |

### Total Cross-Partition Episode Queries Per Hour

| Source | Hourly | HalfHourly | Total/hour |
|--------|--------|------------|------------|
| Categoriser | 1 | — | 1 |
| Poster (find unposted) | 1 | 1 | 2 |
| HomepagePublisher (count) | ~0 (steady-state) | ~0 (steady-state) | ~0 |
| HomepagePublisher (recent) | 1 | 1 | 2 |
| Tweet (find untweeted) | 1 | — | 1 |
| Bluesky (find unposted) | 1 | 1 | 2 |
| **Total cross-partition Episode queries (steady-state)** | **5** | **3** | **8/hour = 192/day** |

Plus cross-partition Podcast queries:
- `IndexIdProvider`: 1/hour = 24/day

**Conservatively, the system runs ~192 steady-state cross-partition queries per day against the Episodes container**, plus ~24 against the Podcasts container (with additional rare weekly refresh scans).

### Over-Fetch Problem: `PodcastEpisodesPoster.PostNewEpisodes()`

// Fetches ALL episodes for each podcast with an unposted episode
var episodes = await episodeRepository.GetByPodcastId(podcastId).ToArrayAsync();

This loads every historical episode for each candidate podcast when only unposted recent episodes are needed. If a podcast has 500 episodes, all 500 are read from Cosmos (partition-scoped but wasteful RU + network).

## Optimization Recommendations

### Priority 0 (Completed): Remove old Cosmos DB connection (quick win, immediate savings)

**Problem**: `CosmosDbContainerFactory` requires `[FromKeyedServices("v1")] CosmosClient` even though only V2 container methods are used by the Function apps. The V1 `CosmosClient` keeps a background connection alive to the old `cultpodcasts-ukdb` DB.

**What changed**:
1. Removed active function dependency on the old V1 Cosmos client path.
2. Removed leaked old DB connection behavior from active execution path.

**Files**:
- `Class-Libraries/RedditPodcastPoster.Persistence/CosmosDbContainerFactory.cs`
- `Class-Libraries/RedditPodcastPoster.Persistence/Extensions/ServiceCollectionExtensions.cs`
- `Infrastructure/functions.bicep`

**Impact**: Eliminates old DB leak traffic, reduces cold-start overhead, and supports retirement of `cultpodcasts-ukdb`.

### Priority 1: Eliminate redundant cross-partition queries (biggest single impact)

Several activities query the Episodes container for nearly the same thing:
- **Poster**: unposted episodes released in last 7 days
- **Tweet**: untweeted episodes released recently
- **Bluesky**: bluesky-unposted episodes released recently

**Recommendation**: Query once at orchestration start and pass the candidate episode set through the orchestration context, or introduce a shared cache within a single Function execution.

### Priority 2 (Completed): Fix `PostNewEpisodes` over-fetch

Changed from fetching all episodes in a partition to a filtered subset for candidate posting windows.

### Priority 3 (Interim Completed): Weekly refresh for cached `ActiveEpisodeCount`

`HomepagePublisher` now refreshes `activeEpisodeCount` in the same weekly Monday refresh window used for duration, while still refreshing when the cache value is missing. This keeps the cache fresh without per-run scans.

**Remaining improvement opportunity**: Maintain the count incrementally on write paths (add/remove/ignore transitions) to avoid periodic full scans entirely.

### Priority 4: Replace cross-partition "recent episodes" query with partition-scoped queries

The `HomepagePublisher` recent-episodes query and the social-posting queries could:
1. First query the **Podcasts** container for active podcast IDs (smaller container, already done by `IndexIdProvider`).
2. Then issue **partition-scoped** queries per podcast for recent episodes.

At current scale (**5,000+ podcast entities**), this should be treated as a **bounded** fan-out strategy, not a full fan-out strategy. P4 should only fan out over a tightly filtered active subset (for example, podcasts with candidate activity in the target time window) and should keep strict caps/continuation handling to prevent replacing one expensive cross-partition read with thousands of partition reads.

**Current status**:
- ✅ Shared social recent-candidate path (`Poster`/`Tweet`/`Bluesky`) now uses `podcast.latestReleased >= releasedSince` to scope partition reads.
- ✅ `HomepagePublisher` recent-episodes path now uses `latestReleased`-scoped podcast selection + partition-scoped episode reads.
- ✅ `RecentPodcastEpisodeCategoriser` now uses `latestReleased`-scoped podcast selection + partition-scoped episode reads.
- ✅ Podcast metadata now stores `latestReleased`, and episode save/delete paths maintain it.
- ⏳ `latestReleased` backfill/coverage still needs telemetry validation.

### Priority 5: Consolidate Poster + Tweet + Bluesky episode discovery

All three currently do independent cross-partition scans for "recent unposted/untweeted episodes." Since they run sequentially in the same orchestration, the results overlap heavily.

**Recommendation**: Perform a single cross-partition query for recent episodes at orchestration start (with a broad filter), then filter in-memory for posted/tweeted/bluesky states. Saves 4–6 cross-partition queries per hour.

### Priority 6: Evaluate HalfHourly necessity

The HalfHourly orchestration runs `Poster` + `Publisher` + `Bluesky`. This doubles the cross-partition query load. Consider:
- Is half-hourly posting frequency actually needed?
- Could the HalfHourly orchestration be reduced to only `Poster` (skip `Publisher` which is expensive)?
- Could Bluesky posting be limited to hourly only?

## Estimated Impact

| Priority | Optimization | Est. query reduction | Est. daily savings |
|----------|-------------|---------------------|-------------------|
| **P0** | **Remove old DB connection** | **~180 calls/day to old DB** | **Completed** |
| P1 | Consolidate Poster/Tweet/Bluesky episode queries | ~120 cross-partition queries/day | ~$0.05–0.08 |
| **P2** | **Fix PostNewEpisodes over-fetch** | Varies by podcast count | **Completed** |
| **P3 (interim)** | **Weekly cached count refresh** | Eliminates steady-state per-run count scan | **Completed** |
| P4 | Replace cross-partition recent queries with bounded active-subset fan-out | Varies (sensitive to active-podcast cardinality) | ~$0.00–$0.04 |
| P5 | Consolidate episode discovery | ~120 cross-partition queries/day | ~$0.05–$0.08 |
| P6 | Reduce HalfHourly scope | ~96 cross-partition queries/day | ~$0.05–$0.08 |
| | **Remaining estimated** | | **~$0.10–$0.20/day** |

Target: bring total daily cost from ~$0.50 back to ≤$0.26 (pre-migration level).

## Cosmos DB diagnostics (TEMPORARY — TURN OFF after RU tuning)

> **⚠️ REMOVE after investigation.** Cosmos diagnostic export adds Log Analytics ingestion cost on top of existing Azure Monitor spend. **TURN OFF completed 2026-06-17 11:47 UTC** via `scripts/disable-cosmos-diagnostics.ps1` (removed `cosmos-to-loganalytics-infra`). Expect `AzureDiagnostics` ingestion in `loganalytics-infra` to fall over the **next 24–48h** (billing may lag similarly). P6 work remains; diagnostics stay off unless re-enabled for a new investigation.

**Enabled:** `2026-06-12` on `cultpodcasts-db` → `loganalytics-infra` (`cosmos-to-loganalytics-infra`).

**Disabled:** **2026-06-17 11:47 UTC** — diagnostic settings list empty on `cultpodcasts-db`; `AzureDiagnostics` billable MB should decline over **24–48h**.

| Mechanism | Location |
|-----------|----------|
| Bicep (GH Actions) | `Infrastructure/cosmos-db-diagnostics.bicep` (`enableDiagnostics=true`) |
| Apply now (no bicep deploy) | `scripts/enable-cosmos-diagnostics.ps1` |
| **Disable** | `scripts/disable-cosmos-diagnostics.ps1` or redeploy bicep with `enableDiagnostics=false` |

**Categories exported:** `DataPlaneRequests`, `QueryRuntimeStatistics`, metric `Requests`. Query full text remains off (`enableFullTextQuery: 'None'` in `cosmos-db.bicep`).

**Sample KQL** (workspace `loganalytics-infra`, allow ~15–30 min after enable for first rows):

```kusto
// RU by container + operation (data plane)
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.DOCUMENTDB"
| where Category == "DataPlaneRequests"
| where TimeGenerated > ago(24h)
| summarize TotalRU = sum(todouble(requestCharge_s)), Calls = count() by collectionName_s, operationName_s
| order by TotalRU desc

// Slowest / highest-RU queries (hash only unless full-text enabled)
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.DOCUMENTDB"
| where Category == "QueryRuntimeStatistics"
| where TimeGenerated > ago(24h)
| summarize TotalRU = sum(todouble(requestCharge_s)), Calls = count() by collectionName_s, queryText_s
| order by TotalRU desc
```

**June 2026 RU split (7-day Azure Metrics, pre-diagnostics):** ~68% `Episodes` / `Query`; ~15% `Subjects` / `ReadFeed`.

### Query consolidation — where to change code (P1 / P4 / P6)

P4 partition-scoped reads are largely in place via `RecentEpisodeCandidatesProvider`. **P1/P5 orchestration preload deployed 2026-06-15** (see section below). Remaining savings are mostly **P6** (HalfHourly duplication).

| Priority | Problem | Code / status |
|----------|---------|---------------|
| **P1 / P5 (done — 2026-06-15)** | Poster, Tweet, Bluesky, Categoriser each called `GetRecentActiveEpisodes` in separate Durable activities → static cache did **not** survive activity boundaries | **Implemented:** `Cloud/Indexer/LoadRecentCandidates.cs`; `IndexerContext.RecentEpisodeCandidates`; `HourlyOrchestration` + `HalfHourlyOrchestration` call load once before downstream activities. **Consumers:** `RecentPodcastEpisodeCategoriser.Categorise`, `EpisodeProcessor.PostEpisodesSinceReleaseDate`, `PodcastEpisodeProvider.GetReadyPodcastEpisodes`, `Tweeter.Tweet`, `BlueskyPostManager.Post` — optional preloaded set with provider fallback. **Loader:** `RecentEpisodeCandidatesProvider.LoadRecentPodcastEpisodes` (still used once per orchestration). |
| **P4 (remaining)** | Weekly Monday cross-partition scans for homepage totals | `Class-Libraries/RedditPodcastPoster.ContentPublisher/HomepagePublisher.cs` — `ResolveHomePageCache` lines ~172–189 (`episodeRepository.GetAllBy` for duration + active count). P3 interim limits this to Monday 00:00–00:20; incremental maintenance on write paths would remove it entirely. |
| **P4 (done)** | Recent episodes for homepage / social | `RecentEpisodeCandidatesProvider.LoadRecentPodcastEpisodes`, `HomepagePublisher.GetRecentEpisodes` / `LoadRecentEpisodes` (partition-scoped via `GetByPodcastId`). |
| **P6 (pending)** | HalfHourly re-runs Poster + Publisher + Bluesky (doubles Cosmos load) | `Cloud/Indexer/HalfHourlyOrchestration.cs` — trim activities (e.g. Poster-only, or drop Publisher/Bluesky from half-hourly). Trigger: `Cloud/Indexer/OrchestrationTrigger.cs` `RunHalfHourly`. |

**Activity entry points (Indexer):**

| Activity | File | Cosmos path |
|----------|------|-------------|
| LoadRecentCandidates | `Cloud/Indexer/LoadRecentCandidates.cs` | → `RecentEpisodeCandidatesProvider` (once per orchestration) |
| Categoriser | `Cloud/Indexer/Categoriser.cs` | → `RecentPodcastEpisodeCategoriser` (uses preloaded candidates) |
| Poster | `Cloud/Indexer/Poster.cs` | → `EpisodeProcessor` (uses preloaded candidates) |
| Tweet | `Cloud/Indexer/Tweet.cs` | → `Tweeter` → `PodcastEpisodeProvider` (uses preloaded candidates) |
| Bluesky | `Cloud/Indexer/Bluesky.cs` | → `BlueskyPostManager` → `PodcastEpisodeProvider` (uses preloaded candidates) |
| Publisher | `Cloud/Indexer/Publisher.cs` | → `HomepagePublisher` |

**Cross-partition primitive:** `Class-Libraries/RedditPodcastPoster.Persistence/EpisodeRepository.cs` — `GetAllBy` / `GetAllBy<TProjection>` (no partition key → fan-out). Partition-scoped alternative: `GetByPodcastId`.

### Where the biggest Function compute time is spent (App Insights evidence)

From the App Insights data, the **Indexer activity (4 passes, hourly)** dominates at 1,771s total over 48h — but this is largely driven by **external API calls** (Apple 711s, Spotify 512s, YouTube 143s), not Cosmos. The Cosmos-heavy activities are:

| Activity | 48h total | Cosmos-driven? | Optimization target? |
|----------|----------|---------------|---------------------|
| Indexer | 1,771s | Partially (also external APIs) | P2 (over-fetch) |
| HalfHourlyOrchestration | 293s | Yes | P6 (reduce scope) |
| Bluesky | 230s | Yes | P1/P5 (consolidate queries) |
| Publisher | 172s | Yes (cross-partition recent + periodic refresh scans) | P4 |
| Poster | 136s | Yes | P2 (over-fetch), P5 |
| Categoriser | 116s | Yes (1 cross-partition query) | P1 |
| Tweet | 57s | Yes | P5 (consolidate) |

## Next Steps

1. Deploy warning-level probe logging change to `indexer-infra` and verify new `*CostProbe*` events appear in `AppTraces` for the expected activity names.
2. Keep `indexer.EnableCostInstrumentation=true` only for the corrected capture window, then run a fresh 24h UTC interval with no unrelated behavior changes.
3. Re-export daily cost data to include the full probe day (`cost-analysis(3).csv` pattern) and align it to the exact probe interval.
4. Query App Insights for all activity probes (`IndexIdProviderCostProbe.*`, `IndexerCostProbe.*`, `CategoriserCostProbe.*`, `PosterCostProbe.*`, `PublisherCostProbe.*`, `TweetCostProbe.*`, `BlueskyCostProbe.*`) and summarize p50/p95 by activity, pass (where present), and hour-of-day.
5. Correlate the corrected probe output with Cosmos telemetry (RU, cross-partition query count, query latency) for `Poster`, `Tweet`, `Bluesky`, `Publisher`, and `Categoriser`.
6. Finalize P5 keep/rollback decision using measured RU + duration deltas against the pre-tuning baseline.
7. Implement and test P6 (HalfHourly scope reduction), then capture a new 48h cost window immediately after deployment.
8. Capture a 7-day post-change cost window and compare against both pre-migration baseline and this snapshot.
9. Re-check objective attainment (`<= $0.26/day`); if still above target, prioritize further orchestration/storage transaction reductions.

### Commands and query evidence (2026-03-27 session)

Commands used during validation and what they showed:

1. `subscription_list`
   - Result: default subscription is `Cultpodcasts` (`a6b8f1a2-6163-41bc-aa6d-e33928939a6e`).
2. `monitor_workspace_list` (subscription: `Cultpodcasts`)
   - Result: workspace `loganalytics-infra` identified for queries.
3. `monitor_table_list` (workspace: `loganalytics-infra`, table-type: `Microsoft`)
   - Result: `AppTraces`, `AppEvents`, and `FunctionAppLogs` available.
4. KQL (`AppTraces`): `where TimeGenerated > ago(72h)` + `contains "CostProbe"`
   - Result: `0` rows for `indexer-infra`.
5. KQL (`AppEvents`): `Name/Properties contains "CostProbe"`
   - Result: `0` rows.
6. KQL (`AppTraces`): `summarize count() by SeverityLevel` for `indexer-infra` over 24h
   - Result: `SeverityLevel 0: 3`, `SeverityLevel 2: 4756`, `SeverityLevel 3: 143`.
7. KQL (`AppTraces`): inspect `SeverityLevel == 0`
   - Result: telemetry-channel warnings (not cost probes), including `TelemetryChannel found a telemetry item without an InstrumentationKey`.
8. PowerShell: `Import-Csv C:\Users\jonbr\Downloads\cost-analysis(2).csv | Select-Object -Last 5`
   - Result: local total-cost export ends at `2026-03-26`.
9. PowerShell: `Import-Csv C:\Users\jonbr\Downloads\cost-analysis(3).csv | Select-Object -Last 10`
   - Result: local `indexer-infra` export ends at `2026-03-26`.

Discovery from command evidence: probe logs were not queryable because they were emitted at Information level while production filtering excludes Information traces. Remediation changed all Indexer `*.CostProbe.*` events to Warning level before next capture.

### 24h probe attribution update (2026-03-28 UTC)

- Probe visibility confirmed using `AppTraces` query with `Message contains "CostProbe"` (previous `has` operator produced false negatives for dotted probe tokens).
- Parsed `*.CostProbe.Complete` `total-ms` over last 24h (`indexer-infra`) shows activity-time concentration:
  - `Indexer`: `~74.05%` of probe total runtime (`96` completes, `~1,772.5s`)
  - `Publisher`: `~13.09%`
  - `Poster`: `~4.29%`
  - `Bluesky`: `~3.80%`
  - `Categoriser`: `~3.03%`
  - `Tweet`: `~1.23%`
  - `IndexIdProvider`: `~0.50%`
- Within `IndexerCostProbe.Complete`, `update-ms` is the dominant component:
  - `update-ms` share of Indexer `total-ms`: `~99.51%`
  - `initiate-ms + complete-ms` combined: `<0.5%`
- Dependency-time distribution in same 24h window indicates external enrichment pressure dominates:
  - Apple API + Spotify API account for a large share of dependency time;
  - Cosmos V2 (`cultpodcasts-db-uksouth.documents.azure.com`) remains active but is a small slice of total dependency time (`~1.10%` in this slice).
- Storage transactions (`cultpodcastsstg`) remain in prior band (`195,357/day`), with no new spike detected in this window.

#### Immediate next steps (P6 refinement path)

1. Implement pass-level workload shaping in `Indexer` so expensive Apple/Spotify enrichment runs on one primary pass/hour; keep remaining passes lightweight.
2. Reduce IDs per pass (or cap per-run podcast count) and carry continuation to next run to flatten p95 `Indexer` `update-ms`.
3. Re-run 24h probe capture and compare `IndexerCostProbe` `sum(total-ms)` and `avg(update-ms)` against this baseline before additional behavior changes.
4. When billing rows finalize, correlate reduced probe runtime with `Flex Consumption - On Demand Execution Time` daily rows to confirm actual cost reduction.

### P6 rotation refinement status (latest)
- ✅ Implemented: primary pass now rotates by hour using `primaryPass = (currentHour % totalPasses) + 1`.
- ✅ Exactly one primary pass remains active per run; non-primary passes continue to skip expensive Spotify/YouTube queries.
- Expected effect: reduce fixed pass hotspotting while preserving bounded expensive-query frequency.

### P6 rotation validation (next 24h capture)
1. Compare pass-level `avg(update-ms)` for passes `1..4` vs previous fixed-pass baseline.
2. Confirm reduced pass variance (flatter distribution across passes).
3. Confirm `IndexerCostProbe` `sum(total-ms)` and `avg(update-ms)` are stable or lower.
4. Correlate with finalized daily `Flex Consumption - On Demand Execution Time` rows.

### Latest update: probe orchestration simplification (2026-04-25)

- Removed `IndexerOptions.EnableCostInstrumentation` and `indexer__EnableCostInstrumentation` wiring.
- Probe lifecycle now delegates to injected `IMemoryProbeOrchestrator` with call-site pattern:
  - `Start(nameof(ActivityOrFunction))`
  - `End()` / `End(false, errorType)`.
- Effective on/off switch is now only `memoryProbe__Enabled` (via `MemoryProbeOptions`).
- Added handover summary document:
  - `docs/migration/handover-2026-04-25-memory-probe-and-alerting.md`

### Updated explicit next steps

1. Deploy and verify probe logs continue to emit at Warning level with orchestrator-based lifecycle.
2. Confirm alert resources remain healthy after configuration simplification (budget + query alerts).
3. Capture 24–72h p50/p95/max memory and duration by function to guide any per-app memory-size experiments.
4. Keep `Indexer` timing focus on `update-ms` in probe analysis.

### Latest update: CostProbe warning removal (2026-04-25)

- Removed Indexer activity `*CostProbe*` warning logs and associated stopwatch timing fields.
- Retained memory telemetry via `IMemoryProbeOrchestrator` and `MemoryProbe` warning events.
- Rationale: free-allowance exhaustion is already understood; retain memory diagnostics while reducing warning-log noise/volume.

### Updated next steps after CostProbe removal

1. Verify `MemoryProbe.Start`/`MemoryProbe.Complete` warnings continue across Indexer activity executions.
2. Confirm App Insights warning volume reduction after removing CostProbe warnings.
3. Continue memory footprint analysis (p50/p95/max private bytes) for memory-tier decisions.
4. Keep duration analysis where needed using targeted logs/queries, not broad CostProbe warning spam.

### Latest update: budget deployment decoupled from functions infra step (2026-04-25)

- Subscription-scope budget provisioning was decoupled from `Infrastructure/functions.bicep`.
- Budget now deploys via dedicated GitHub Actions provision step using:
  - `Infrastructure/functions-budget-subscription.bicep`
  - deployment `scope: subscription`
- Resource-group functions infra deploy now remains focused on RG resources only.

### Updated explicit next steps

1. Verify new workflow execution order: budget step succeeds independently before/alongside RG functions infra deployment.
2. Confirm budget resource updates continue to apply when `BUDGET_ALERT_EMAIL`/monthly amount changes.
3. If budget step fails, rerun only provision job path and inspect that step without conflating RG template errors.

---

## Cost-saving programme — telemetry reduction (started 2026-06-10)

### Context

Daily costs remain ~2× pre-migration baseline. Two stacked drivers:

1. **Functions (Flex Consumption)** — ~£0.18–0.22/day after monthly free-grant exhaustion (~day 13).
2. **Azure Monitor (Log Analytics ingestion)** — ~£0.107/day flat baseline since 2026-05-01, driven by OpenTelemetry `AppMetrics` (~57%) and `AppTraces` (~31%).

During the free-grant window (days 1–12 of each month), Azure Monitor appears as the dominant cost line because Functions show £0.00.

### Change applied 2026-06-10

**MemoryProbe disabled in production** (direct app-setting update; bicep also set to `false` for when GH Actions deploy resumes):

| App | Setting | Value |
|-----|---------|-------|
| `indexer-infra` | `memoryProbe__Enabled` | `false` |
| `discover-infra` | `memoryProbe__Enabled` | `false` |
| `api-infra` | `memoryProbe__Enabled` | `false` |

Workspace: `loganalytics-infra` (customer ID `2b1c62ee-689f-422a-816b-be1605ae88fa`).

### Pre-change baseline (use 2026-06-09 — full day before disable)

| Metric | Value |
|--------|-------|
| **Total billable ingestion** | **~40.3 MB/day** |
| AppMetrics | 21.78 MB |
| AppTraces | 13.07 MB |
| AppPerformanceCounters | 2.57 MB |
| AppDependencies | 1.52 MB |
| AppRequests | 1.08 MB |
| **MemoryProbe trace events** | **1,298/day** (indexer 480, api 802, discover 16) |
| **Azure Monitor daily cost** | **~£0.107/day** |
| **Subscription / RG** | `a6b8f1a2-6163-41bc-aa6d-e33928939a6e` / `automatedinfra` |

> 2026-06-10 is a partial day (disable applied mid-day); do not use it as the primary baseline.

### Code changes implemented and deployed 2026-06-10

| Change | Files |
|--------|-------|
| Drop noisy OTel runtime metrics | `Cloud/Azure/OpenTelemetryConfiguration.cs` — drops `process.runtime.dotnet.*`, `process.cpu.*`, `kestrel.*`, `http.server.active_requests`, `azure.functions.health_check.reports`, `_APPRESOURCEPREVIEW_*` |
| Application log levels | `Cloud/Azure/HostFactory.cs` — `Indexer`/`Api`/`Discovery` at **Information**; `RedditPodcastPoster` at **Warning** (since 2026-06-12); host/framework at **Warning** |
| host.json alignment | `Cloud/Indexer/host.json`, `Cloud/Api/host.json`, `Cloud/Discovery/host.json` — added `Azure: Warning` |
| Bicep app settings | `Infrastructure/functions.bicep` — OTel trace sampling env vars; log levels as above |

> **Deployed 2026-06-10 (UTC)** via `scripts/deploy-function-local.ps1` (Flex blob → `cultpodcastsstg/*/released-package.zip`):
> - `indexer-infra` → `indexer-deployment`
> - `discover-infra` → `discovery-deployment`
> - `api-infra` → `api-deployment`
>
> Log-level app settings applied directly on all three apps (bicep infra deploy still pending for GH Actions).

---

## Tomorrow review checklist (run 2026-06-11)

Run after a **full 24h UTC window** following the MemoryProbe disable (compare **2026-06-11** against baseline **2026-06-09**).

### 1. Confirm MemoryProbe is off in production

```powershell
foreach ($app in @('indexer-infra','discover-infra','api-infra')) {
  az functionapp config appsettings list `
    --resource-group automatedinfra --name $app `
    --query "[?name=='memoryProbe__Enabled'].{app:'$app',value:value}" -o table
}
```

Expected: all three show `false`.

### 2. Confirm no new MemoryProbe traces (Log Analytics)

Run in Azure Portal → `loganalytics-infra` → Logs, or:

```powershell
az monitor log-analytics query `
  --workspace "2b1c62ee-689f-422a-816b-be1605ae88fa" `
  --analytics-query @"
AppTraces
| where TimeGenerated between (datetime(2026-06-11) .. datetime(2026-06-12))
| where Message contains 'MemoryProbe'
| summarize Events=count() by AppRoleName
"@ -o table
```

Expected: **0 rows** (or negligible stragglers from pre-restart invocations on 2026-06-10).

Side-by-side with baseline:

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-09) .. datetime(2026-06-12))
| extend Day = startofday(TimeGenerated)
| summarize Traces=count(), MemoryProbe=countif(Message contains "MemoryProbe"), Warnings=countif(SeverityLevel >= 2) by Day, AppRoleName
| order by Day desc, AppRoleName asc
```

Expected on 2026-06-11: MemoryProbe ≈ 0; total Warnings on `indexer-infra` and `api-infra` drop materially vs 2026-06-09.

### 3. Measure ingestion reduction (primary success metric)

```kusto
Usage
| where TimeGenerated between (datetime(2026-06-09) .. datetime(2026-06-12))
| where IsBillable == true
| summarize DailyMB=sum(Quantity) by Day=startofday(TimeGenerated), DataType
| order by Day desc, DailyMB desc
```

Focus on **AppTraces** daily MB:

| Day | AppTraces MB (baseline) | Target after disable |
|-----|---------------------------|----------------------|
| 2026-06-09 | 13.07 | — |
| 2026-06-11 | — | materially lower (MemoryProbe warnings were large messages) |

Total daily MB target: below ~40 MB/day; AppTraces share should shrink most.

Top metrics unchanged until OTel trim is deployed:

```kusto
AppMetrics
| where TimeGenerated > ago(24h)
| summarize Count=count() by Name
| order by Count desc
| take 15
```

### 4. Check Azure Monitor cost row (billing lags 24–48h)

```powershell
@'
{
  "type": "ActualCost",
  "timeframe": "Custom",
  "timePeriod": { "from": "2026-06-09", "to": "2026-06-12" },
  "dataset": {
    "granularity": "Daily",
    "aggregation": { "totalCost": { "name": "Cost", "function": "Sum" } },
    "grouping": [{ "type": "Dimension", "name": "ServiceName" }]
  }
}
'@ | Set-Content -Path "$env:TEMP\cost-daily.json" -Encoding utf8

az rest --method post `
  --url "https://management.azure.com/subscriptions/a6b8f1a2-6163-41bc-aa6d-e33928939a6e/providers/Microsoft.CostManagement/query?api-version=2023-11-01" `
  --body "@$env:TEMP\cost-daily.json" -o json
```

Filter output for `Azure Monitor` and `Functions` rows. Cost rows for 2026-06-11 may not be final until 2026-06-12 or 2026-06-13; ingestion (step 3) is the early indicator.

### 5. Sanity-check function health (no regressions)

```kusto
AppRequests
| where TimeGenerated > ago(24h)
| summarize Executions=count(), Failed=countif(Success == false), AvgMs=avg(DurationMs) by AppRoleName, Name
| where Failed > 0 or Executions > 0
| order by Executions desc
```

Expected: execution counts and failure rates unchanged vs prior days.

### 6. Record results and decide next step

| Outcome | Next action |
|---------|-------------|
| MemoryProbe = 0, AppTraces MB down | Proceed with OTel metric trim + log-level PR; re-measure after deploy |
| MemoryProbe still appearing | Re-check app settings; confirm host restarted; inspect `AppRoleName` for stale instances |
| AppTraces down but Azure Monitor cost flat | Billing lag — re-check on 2026-06-13; OTel `AppMetrics` still dominant (~22 MB/day) |
| Function failures increased | Roll back `memoryProbe__Enabled=true` on affected app only; investigate separately |

### Pass criteria for this phase

- `MemoryProbe` trace count = 0 on a full post-change day.
- `AppTraces` ingestion down vs 2026-06-09 baseline (13.07 MB/day).
- No increase in failed `AppRequests`.
- Azure Monitor daily cost trending below ~£0.107/day once billing finalizes (secondary; may take 48h).

---

## Phase 1 review results (2026-06-11 vs baseline 2026-06-09)

| Metric | 06-09 | 06-11 | Outcome |
|--------|-------|-------|---------|
| MemoryProbe events | 1,298 | **0** | Pass |
| AppMetrics MB | 21.78 | 13.14 | **-40%** (OTel metric trim) |
| AppTraces MB | 13.07 | 24.87 | **+90%** (Information logs exported) |
| Total billable MB | 40.3 | 42.2 | Flat/slightly up |
| Azure Monitor £/day | 0.107 | 0.108 | Flat |

**Root cause of AppTraces increase:** raising `RedditPodcastPoster` to Information exported high-volume pagination logs (`PaginateEpisodes`, etc.) — ~20k Information traces/day vs ~1.4k baseline.

**Why `Logging__ApplicationInsights__SamplingSettings__*` had no effect:** production uses `telemetryMode: "OpenTelemetry"` in `host.json`. The legacy `Logging__ApplicationInsights__SamplingSettings__IsEnabled=true` setting only applies to the classic Application Insights ILogger provider, not the OpenTelemetry exporter path that writes to `AppTraces`.

---

## Phase 2 — logging + trace sampling (from 2026-06-12)

### Changes applied

| Change | Production |
|--------|------------|
| Full telemetry app-settings bundle | `scripts/apply-telemetry-app-settings.ps1` (run when bicep deploy is unavailable) |
| `Logging__LogLevel__RedditPodcastPoster=Warning` | App settings (all 3 apps) |
| `OTEL_TRACES_SAMPLER=microsoft.fixed_percentage` | App settings |
| `OTEL_TRACES_SAMPLER_ARG=0.25` | App settings |
| `APPLICATIONINSIGHTS_SAMPLING_PERCENTAGE=25` | App settings |
| `memoryProbe__Enabled=false` | App settings |
| `EnableTraceBasedLogsSampler=true` + `SamplingRatio=0.25` | Code (`HostFactory.cs`) — requires function deploy |
| `Indexer`/`Api`/`Discovery` remain **Information** | App settings + `HostFactory.cs` |

```powershell
# Re-apply all telemetry/cost app settings without bicep:
powershell -File scripts/apply-telemetry-app-settings.ps1
```

### Sunday review checklist (2026-06-15 — full UTC day 2026-06-14)

Compare **2026-06-14** against **2026-06-11** (pre-fix) and **2026-06-09** (original baseline).

```kusto
Usage
| where TimeGenerated between (datetime(2026-06-09) .. datetime(2026-06-15))
| where IsBillable == true
| summarize DailyMB=sum(Quantity) by Day=startofday(TimeGenerated), DataType
| order by Day desc, DailyMB desc
```

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-14) .. datetime(2026-06-15))
| summarize Traces=count() by SeverityLevel, AppRoleName
| order by Traces desc
```

**Pass criteria for phase 2:**

- `AppTraces` daily MB below **13 MB** (back to 06-09 baseline or lower).
- Information traces on `indexer-infra` materially below 06-11 (~17k/day).
- Azure Monitor daily cost below **£0.09/day** (target ~15% reduction from £0.107).
- No indexer/discovery execution regressions.

**If still elevated:** keep `RedditPodcastPoster` at Warning; consider raising `Indexer` to Warning or adding logger-specific filters for `PaginateEpisodes` only.

---

## Phase 2 review results (2026-06-15)

Review window: compare **2026-06-14** (first full post-Phase-2 UTC day after deploy ~12:29 UTC 2026-06-12) against **2026-06-11** (pre-fix) and **2026-06-09** (original telemetry baseline). Cosmos diagnostics enabled **2026-06-12** on `cultpodcasts-db` → `loganalytics-infra`.

### Subscription daily cost (Cost Management API, GBP)

| Day | Total | Azure Monitor | Cosmos DB | Functions | Storage |
|-----|-------|---------------|-----------|-----------|---------|
| 2026-06-09 (baseline) | £0.154 | £0.107 | £0.039 | £0.000 | £0.007 |
| 2026-06-11 (pre Phase 2) | £0.148 | £0.108 | £0.034 | £0.000 | £0.006 |
| 2026-06-12 (Phase 2 + Cosmos diag) | £0.160 | £0.107 | £0.046 | £0.000 | £0.007 |
| 2026-06-13 | £0.187 | £0.107 | **£0.073** | £0.000 | £0.007 |
| 2026-06-14 (Phase 2 full day) | £0.156 | £0.107 | £0.041 | £0.000 | £0.007 |
| 2026-06-15 (partial, billing lag) | £0.091 | £0.067 | £0.020 | £0.000 | £0.004 |

- **MTD (1–15 Jun):** £2.29 total, **~£0.153/day** average (includes partial 06-15).
- **Functions:** £0.00 all days (Flex free-grant window; expect execution-time charges from ~day 13 onward in prior months).
- **Azure Monitor billing row flat at ~£0.107/day** through 06-14 despite AppTraces ingestion drop — see ingestion table below (Cosmos diagnostic export offset savings; billing also lags 24–48h).

### Log Analytics billable ingestion (Usage table, MB/day)

| Day | Total MB | AppTraces | AppMetrics | AzureDiagnostics | Notes |
|-----|----------|-----------|------------|------------------|-------|
| 2026-06-09 | 40.26 | 13.07 | 21.78 | — | Baseline |
| 2026-06-11 | 42.16 | 24.87 | 13.14 | — | Information `RedditPodcastPoster` logs |
| 2026-06-12 | 78.39 | 26.56 | 20.30 | **23.89** | Phase 2 deploy + Cosmos diag enabled |
| 2026-06-13 | 103.86 | **5.21** | 18.32 | **72.13** | First full Phase 2 day; Episodes RU spike |
| 2026-06-14 | 79.64 | **5.16** | 18.37 | **47.88** | Phase 2 pass on AppTraces |
| 2026-06-15 (partial) | 63.13 | 3.46 | 15.81 | 36.83 | In progress |

**Phase 2 telemetry impact (measurable):**

| Metric | 06-09 | 06-11 | 06-14 | Outcome |
|--------|-------|-------|-------|---------|
| MemoryProbe events | 1,298 | 0 | 0 | Pass |
| `AppTraces` MB | 13.07 | 24.87 | **5.16** | **Pass** (below 13 MB target) |
| `indexer-infra` trace count | 10,440 | 24,923 | **4,025** | **−84% vs 06-11** |
| `PaginateEpisodes` traces | 0 | 984 | **0** | Pass (`RedditPodcastPoster=Warning`) |
| Total billable MB | 40.3 | 42.2 | 79.6 | **Fail** — `AzureDiagnostics` from Cosmos export |
| Azure Monitor £/day | 0.107 | 0.108 | 0.107 | Flat (diag offset + billing lag) |

Function health (06-14–15): no failed indexer/discovery orchestration activities; one API GET failure in sample window — unchanged from prior patterns.

### Cosmos DB diagnostics — RU attribution (AzureDiagnostics, from 2026-06-12)

**Is the 24h window sufficient?** **Yes.** Diagnostics have been flowing for **>72h** (full UTC days 06-13, 06-14, plus partial 06-12 from ~12:29 UTC and partial 06-15). Safe to use for P1/P5 design decisions; disable diagnostics soon to stop added Monitor ingestion.

**Diagnostic row volume:**

| Day | DataPlaneRequests rows | QueryRuntimeStatistics rows |
|-----|------------------------|----------------------------|
| 06-12 (partial) | 24,132 | 17,959 |
| 06-13 | 73,606 | 44,763 |
| 06-14 | 45,263 | 32,655 |
| 06-15 (partial) | 35,879 | 25,886 |

**Data-plane RU by day** (sum of `requestCharge_s`, all containers):

| Day | Total RU | Calls |
|-----|----------|-------|
| 06-12 (partial) | 90,257 | 24,132 |
| 06-13 | **289,977** | 73,606 |
| 06-14 | 147,773 | 45,263 |
| 06-15 (partial) | 126,127 | 35,879 |

**RU by container + operation (06-12 → 06-15 cumulative):**

| Container | Operation | Total RU | Calls | Share |
|-----------|-----------|----------|-------|-------|
| **Episodes** | **Query** | **412,051** | 110,389 | **~63%** |
| Episodes | Upsert | 81,892 | 6,119 | ~13% |
| Subjects | ReadFeed | 53,792 | 10,432 | ~8% |
| Discovery | Query | 23,985 | 450 | ~4% |
| Subjects | Query | 20,110 | 7,100 | ~3% |
| Podcasts | Read | 19,941 | 20,203 | ~3% |
| Podcasts | Query | 9,740 | 2,327 | ~1% |
| Episodes | Read | 7,180 | 7,172 | ~1% |

**QueryRuntimeStatistics** (top containers by call count): Episodes **110,473**, Subjects 7,100, Podcasts 2,327. Parameterized query text is present (full text off); top Episodes shapes are partition-scoped `SELECT VALUE` filters — consistent with P4 partition reads, but **Episodes/Query volume remains the dominant RU line**.

**06-13 anomaly:** Episodes container **231,945 RU** in one day (vs ~112k on 06-14). Cosmos **billing row** also peaked (£0.073). No urgent regression in function failures; treat as investigation item before sizing P1/P5 savings (possible batch/backfill or discovery-heavy day).

### Conclusions

1. **Phase 2 succeeded** on its primary telemetry goals: MemoryProbe off, `PaginateEpisodes` eliminated, `AppTraces` back to **~5 MB/day** (well under 13 MB baseline).
2. **Net Monitor cost not yet down** because **temporary Cosmos diagnostics** add **~48–72 MB/day** of `AzureDiagnostics` ingestion — likely **~£0.03–0.05/day** once fully billed. **~~Turn off diagnostics~~ Done 2026-06-17 11:47 UTC** (`scripts/disable-cosmos-diagnostics.ps1`; verify `az monitor diagnostic-settings list` → `[]`).
3. **Cosmos attribution confirmed P1/P5 as the right target:** **Episodes/Query ~63% of measured RU** — **implemented and deployed 2026-06-15** to `indexer-infra`. **~~Turn off Cosmos diagnostics~~ Done 2026-06-17 11:47 UTC** so Monitor savings are not offset by `AzureDiagnostics` ingestion (ingestion tail 24–48h).
4. **No urgent production issue** requiring immediate further query-consolidation deploy; 06-13 RU spike warrants a one-line check (discovery/indexer pass mix) before sizing net savings.

### Recommended next actions

| Priority | Action | Target date |
|----------|--------|-------------|
| 1 | **Validate P1/P5:** compare Episodes/Query RU **2026-06-16** (first full post-deploy UTC day) vs **2026-06-14** Phase 2 baseline (`AzureDiagnostics`, sum RU where container=Episodes and operation=Query) | **2026-06-16** |
| 2 | ~~**Disable Cosmos diagnostics**~~ **Done** (2026-06-17) after post-P1 snapshot | **2026-06-17** ✓ |
| 3 | Re-run Usage + Cost Management for **2026-06-17..18** to confirm Monitor row drops once `AzureDiagnostics` ingestion stops | 2026-06-18 |
| 4 | Optional KQL: compare 06-13 vs 06-14 Episodes/Query calls by hour to explain RU spike | 2026-06-17 |
| 5 | **Implement P6** (HalfHourly scope reduction), then capture 48h cost window | 2026-06-22 |
| 6 | Re-check total daily cost vs **≤ $0.26/day** objective after Functions grant exhaustion + diag removal + P1 savings | 2026-06-22 |

### Commands used (2026-06-15 session)

```powershell
# Daily cost by service (06-09..16)
az rest --method post `
  --url "https://management.azure.com/subscriptions/a6b8f1a2-6163-41bc-aa6d-e33928939a6e/providers/Microsoft.CostManagement/query?api-version=2023-11-01" `
  --body "@$env:TEMP\cost-daily-services.json"

# Per-day Usage / AppTraces (workspace 2b1c62ee-689f-422a-816b-be1605ae88fa)
az monitor log-analytics query --workspace "2b1c62ee-689f-422a-816b-be1605ae88fa" --analytics-query "<KQL>" -o json

# Cosmos RU — use single-quoted provider filter in PowerShell:
# ResourceProvider == 'MICROSOFT.DOCUMENTDB'
# extend Container=coalesce(collectionName_s, collectionname_s)
# OperationName (not operationName_s) on DataPlaneRequests
```

---

## P1/P5 orchestration preload — deployed 2026-06-15

**Scope:** `indexer-infra` only (`scripts/deploy-indexer.ps1`). Branch: `cursor/align-apple-spotify-enrichment-youtube-delay`.

### What shipped

| Component | Role |
|-----------|------|
| `LoadRecentCandidates` activity | Calls `IRecentEpisodeCandidatesProvider.GetRecentActiveEpisodes(MaxDays)` once per orchestration |
| `IndexerContext.RecentEpisodeCandidates` | `PodcastEpisode[]` passed through durable orchestration state |
| `HourlyOrchestration` | Index passes → **LoadRecentCandidates** → Categoriser → Poster → Publisher → Tweet → Bluesky |
| `HalfHourlyOrchestration` | **LoadRecentCandidates** → Poster → Publisher → Bluesky (no Categoriser/Tweet; preload still shared by Poster + Bluesky) |
| Consumers | Filter preloaded set by service day window; fall back to provider when null |

### Expected Cosmos impact

Phase 2 diagnostics attributed **~63% of measured RU** to **Episodes/Query**, dominated by `RecentEpisodeCandidatesProvider.LoadRecentPodcastEpisodes` (Podcasts cross-partition + N× partition-scoped episode reads).

| Orchestration | Before P1 | After P1 |
|---------------|-----------|----------|
| Hourly | 4 candidate loads (Categoriser, Poster, Tweet, Bluesky) | **1** (`LoadRecentCandidates`) |
| HalfHourly | 2 candidate loads (Poster, Bluesky) | **1** |

**Expected reduction:** ~**75% fewer candidate-load query batches on Hourly** (4→1) and ~**50% on HalfHourly** (2→1), translating to a material drop in Episodes/Query RU on orchestration hours — directionally toward eliminating the duplicate-load portion of the **~63% Episodes/Query share** (exact net % depends on non-candidate Episodes queries still in flight).

> **Diagnostics off (2026-06-17 11:47 UTC).** Cosmos export was temporary; **TURN OFF completed** via `scripts/disable-cosmos-diagnostics.ps1`. Expect `AzureDiagnostics` ingestion drop over **24–48h**. Re-enable only if a new RU investigation is needed.

### Validation plan (24h post-deploy)

1. **Baseline:** Phase 2 review **2026-06-14** — Episodes Query **~112k RU/day**, **~32,655** QueryRuntimeStatistics rows (full UTC day, diagnostics enabled).
2. **Post-deploy:** First full UTC day **2026-06-16** (deploy **2026-06-15**) — same KQL as Phase 2 (`AzureDiagnostics`, `ResourceProvider == 'MICROSOFT.DOCUMENTDB'`, sum `requestCharge_s` / count by container + operation).
3. **Pass criteria:** Episodes/Query RU and call count drop materially on hourly/half-hourly hours; no indexer orchestration failures; Poster/Tweet/Bluesky/Categoriser activity durations flat or down.
4. **Cost row:** Re-export Cost Management daily totals after **2026-06-17** (billing lag) to confirm Cosmos DB £/day trend.


### P1 validation (2026-06-17 UTC)

**Deploy:** `indexer-infra` **2026-06-15T21:59:41Z** (`released-package.zip`). **Verdict: FAIL** on pass criteria (no material Episodes/Query RU drop on first full post-deploy UTC day). **Code confirmed live** via `AppRequests` for `LoadRecentCandidates` (58 executions on **2026-06-16**, avg **~2.4s**; 5 on partial **2026-06-15** after deploy; none before deploy).

**Episodes / Query RU** (`AzureDiagnostics`, `DataPlaneRequests`, `collectionName_s=='Episodes'`, `OperationName=='Query'`):

| UTC day | Episodes Query RU | Query calls | Notes |
|---------|-------------------|-------------|--------|
| 2026-06-13 | 151,750 | 41,073 | Pre-baseline spike day |
| 2026-06-14 | **107,534** | 29,553 | **Phase 2 baseline (~112k RU)** |
| 2026-06-15 | 103,988 | 26,937 | Deploy day (partial post-21:59) |
| 2026-06-16 | **119,727** | **31,738** | First full post-P1 day (**+11% RU**, **+7% calls** vs 06-14) |
| 2026-06-17 | 55,077 (partial) | 14,418 | Diagnostics off mid-day |

**Account `TotalRequestUnits` (Azure Metrics, daily):** 06-14 **129,008** → 06-15 **117,396** → 06-16 **129,628** (flat vs baseline; not a material P1 win at account level).

**Orchestration-heavy hours** (combined Episodes+Podcasts `Query`, hours with **>1,500** calls/hour): 06-14 **38,902 RU / 11,051 calls** (5h) vs 06-16 **53,321 RU / 14,861 calls** (6h) — **higher** post-deploy, opposite of expected P1 direction.

**2026-06-15 deploy window** (21:00–24:00 UTC only): Episodes Query **6,390 RU** pre-21:59 vs **12,095 RU** post-21:59 (not isolated — includes normal hourly orchestration at 22:00/23:00).

**Podcasts Query RU/day:** ~2,618 (06-14) → ~2,729 (06-16) — unchanged.

**Interpretation:** P1 removed duplicate activity-boundary candidate loads, but **daily Episodes/Query RU and peak-hour orchestration RU did not fall** on 06-16. Remaining volume is likely dominated by **per-partition episode reads** (P4 path) and other Episodes queries (Indexer/Categoriser/HomepagePublisher), plus normal day-to-day variance (06-13 spike). P1 may still reduce **function wall-clock** without showing as large RU delta if duplicate loads were a smaller share than the 63% Episodes/Query attribution implied.

**Next steps:** (1) Proceed **P6** (HalfHourly scope) as planned. (2) Optional: compare **Indexer activity duration** / GB-s (Consumption) 06-14 vs 06-16 — P1 savings may appear there before Cosmos RU. (3) Cosmos diagnostics **already disabled 2026-06-17** — no further `AzureDiagnostics` RU snapshots unless re-enabled for P6 validation.
### Still pending

- **P6:** HalfHourly still runs Poster + Publisher + Bluesky every 30 minutes — trim scope to cut remaining duplicate paths and HomepagePublisher invocations.
- **P4 (remaining):** Weekly homepage cross-partition scans.
- **Cosmos diagnostics:** **Disabled 2026-06-17 11:47 UTC** (was enabled through P1 validation). Monitor `AzureDiagnostics` MB 2026-06-18..19 to confirm drop.

---

Diagnostics window: **2026-06-12 → 2026-06-15** (`AzureDiagnostics` in workspace `loganalytics-infra`). Production function traffic identified by user agent `cosmos-netstandard-sdk/…Ubuntu 24.04…NET 10.0.7`.

### Diagnostic summary (7-day window)

| Rank | Container | Operation | Total RU | Calls | ~Share |
|------|-----------|-----------|----------|-------|--------|
| 1 | Episodes | Query | 412,051 | 110,389 | **63%** |
| 2 | Episodes | Upsert | 81,892 | 6,119 | 13% |
| 3 | Subjects | ReadFeed | 53,792 | 10,432 | 8% |
| 4 | Discovery | Query | 23,985 | 450 | 4% |
| 5 | Subjects | Query | 20,110 | 7,100 | 3% |
| 6 | Podcasts | Read | 19,941 | 20,203 | 3% |

**Episodes query shapes (QueryRuntimeStatistics):** dominant patterns are partition-scoped `SELECT VALUE … WHERE podcastId = @pk AND release >= @date` (52,707 + 26,355 calls); **12,672** partition-only scans (no release filter); **90** cross-partition `WHERE true` (Episodes). **Hourly correlation:** Episodes Query steady **~6–11k RU/hour** on orchestration hours; **06-13 16:00** outlier (Upsert 71k + Query 28k RU). Subjects ReadFeed peaks **~400–1,088 calls/hour** aligned with hourly/half-hourly runs.

### Ranked candidates

| # | Label | Evidence | Current behavior | Proposed fix | Impact | Risk |
|---|-------|----------|------------------|--------------|--------|------|
| 1 | **Duplicate recent-candidate loads (P1/P5)** — **✅ deployed 2026-06-15** | 63% RU on Episodes/Query; prod UA 97k query calls; top query shapes match `RecentEpisodeCandidatesProvider.LoadRecentPodcastEpisodes` | Was: 4× Hourly + 2× HalfHourly separate activity loads | **Done:** `LoadRecentCandidates` + `IndexerContext.RecentEpisodeCandidates`; consumers use preloaded set | **High** (validate 24h) | **Medium** (payload size — deployed) |
| 2 | **HalfHourly activity duplication (P6)** | HalfHourly runs Poster + Publisher + Bluesky again (~48 extra activity executions/day); doubles paths in row 1 plus HomepagePublisher | `HalfHourlyOrchestration.cs` re-invokes Poster, Publisher, Bluesky every 30 min | Trim to Poster-only, or drop Publisher/Bluesky from half-hourly | **High** | **Low–Medium** (product: posting cadence) |
| 3 | **HomepagePublisher weekly cross-partition scans (P4)** | 90 Episodes `WHERE true` calls; projection queries for count/duration; `ResolveHomePageCache` `GetAllBy` | Monday 00:00–00:20 UTC: cross-partition `episodeRepository.GetAllBy` for **all** active episodes (duration + count) | Incremental count on write paths (`IncrementHomePageActiveEpisodeCount` exists); remove periodic full scans | **Medium** | **Medium** (cache correctness) |
| 4 | **Repeated Podcasts cross-partition filter (P4/new)** | Podcasts Query 9,740 RU; every candidate load calls `podcastRepository.GetAllBy(latestReleased >= …)` | Cross-partition Podcasts query before partition fan-out | Cache recent-podcast ID list in orchestration pass (row 1) or maintain lightweight index doc | **Medium** | **Low–Medium** |
| 5 | **Subjects full-container ReadFeed** | 8% RU; 10,432 ReadFeed calls; hourly peaks | `SubjectRepository.GetAll()` cross-partition ReadFeed; wired into `CachedSubjectProvider`, `PostModelFactory`, `HomepagePublisher`, `SubjectService` | Verify single cached instance per worker; avoid repeated `GetAll()` per request; point-read by name where possible | **Medium** | **Low** |
| 6 | **Bundle posting partition full-scan** | 12,672 partition-only Episodes queries (`WHERE podcastId = @pk` only) | `PodcastEpisodePoster.GetOrderedBundleEpisodes` → `GetByPodcastId(id)` **without release filter** for bundle collating | Add 7-day release window (matches `BundledEpisodeReleaseThreshold`) to Cosmos query | **Low–Medium** | **Low** |
| 7 | **Indexer episode Upsert volume** | 13% RU; 06-13 16:00 Upsert spike 71k RU | `Indexer` saves enriched episodes; `EpisodeRepository.Save` does read-before-write + optional `latestReleased` recompute | Skip upsert when unchanged; batch writes; continue pass-rotation (P6 indexing) | **Medium** (writes) | **Medium–High** |
| 8 | **FlareManager Subjects query** | Subjects Query 20k RU; 7,100 calls | `FlareManager.SetFlare` → `subjectRepository.GetAllBy(IN names)` per Reddit post | Point-read/`GetByName` per subject or in-memory flair map from cache | **Low–Medium** | **Low** |
| 9 | **IndexIdProvider Podcasts scan** | Podcasts Query ~1% RU; 24/day | `IndexablePodcastIdProvider.GetAllBy(IndexAllEpisodes…)` cross-partition hourly | Cached ID list or persisted index document | **Low** | **Low** |
| 10 | **Discovery container bursts** | 4% RU; cross-partition `WHERE true` on Discovery; spike 06-13 17:00 14k RU | `discover-infra` / `DiscoveryResultsRepository` | Review discovery query scope; correlate with 06-13 anomaly before changes | **Low–Medium** | **Medium** |

**Scheduled-function `GetAllBy` inventory (production paths only):**

| Location | Container | Cross-partition? | Schedule |
|----------|-----------|------------------|----------|
| `RecentEpisodeCandidatesProvider.LoadRecentPodcastEpisodes` | Podcasts | Yes | Hourly + HalfHourly **×1 per orchestration** (was ×4 / ×2) |
| `HomepagePublisher.ResolveHomePageCache` | Episodes | Yes | Weekly + hourly/half-hourly Publisher |
| `IndexablePodcastIdProvider` | Podcasts | Yes | Hourly |
| `FlareManager.SetFlare` | Subjects | Yes | Per Reddit post |
| `SubjectRepository.GetAll` | Subjects | Yes (ReadFeed) | Worker startup / cache refresh |
| `DiscoveryResultsService` | Episodes/Podcasts | Yes | API (not indexer schedule) |

**Recommended implementation order:** (1) validate P1/P5 RU drop → (2) ~~disable Cosmos diagnostics~~ **done 2026-06-17** → (3) **P6** HalfHourly trim → (4) P4 weekly scan removal → (5) Subjects/bundle polish.

