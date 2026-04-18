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
- 🔄 **P1 in progress**: shared recent-candidate query path introduced for `Poster`/`Tweet`/`Bluesky`; runtime hotfix applied for in-memory `podcastRemoved` filtering (`IsDefined()` removed from LINQ-to-Objects path); **hourly orchestration smoke run passed with no exceptions**; awaiting **24–48h telemetry validation**.
- ✅ **P4 out (deployed), under observation**: social recent-candidate reads, `HomepagePublisher` recent episodes, and `RecentPodcastEpisodeCategoriser` now use podcast-level `latestReleased` metadata plus partition-scoped episode reads to avoid broad cross-partition scans. Treated as complete implementation, pending stability confirmation.
- 🔄 **P5 in progress**: shared recent-candidate discovery is being consolidated further by using a common lookback threshold across `Poster`/`Tweet`/`Bluesky`/`Categoriser` and cache reuse for narrower follow-up requests. Reddit lookback is configurable via required `postingCriteria.RedditDays` (no hardcoded magic number), Bluesky has required `postingCriteria.BlueSkyDays`, Categoriser has required `postingCriteria.CategoriserDays`, service-specific methods use their own day settings, and shared candidate caching uses `postingCriteria.MaxDays` then reduces by requested `releasedSince` (older-than-window requests log error and return cache-window data).
- 🔄 **P7 instrumentation remediation in progress**: probe logs were previously emitted at Information level (filtered in production). Code now emits all `*.CostProbe.*` events at Warning level across Indexer activities; deployment and fresh 24h capture are pending.
- ✅ **`latestReleased` 4-week backfill run locally (ThrowawayConsole)**: `RecentEpisodes=2285`, `PodcastsWithRecentEpisodes=691`, `UpdatedPodcasts=691`, `MissingPodcasts=0`.
- ⏳ Remaining: P6 (plus **P1/P4/P5 telemetry and stability observation**).

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
