# Post-Migration Cost Analysis

## Summary

After the Episode Detachment Migration (completed ~March 11), daily Azure costs have approximately **doubled** compared to the pre-migration baseline. Two root causes were identified:

1. The detached-episode model has increased the number and cost of Cosmos DB queries executed by the Azure Functions, inflating **Function compute time** (GB-s billing).
2. **Historically, the old Cosmos DB account (`cultpodcasts-ukdb`) was being connected to** by Function app startup, generating background health-check calls before `P0` remediation.

## Implementation Status (feature/cost-reduction)

- ✅ **P0 complete**: old Cosmos DB connection removed from active function execution path.
- ✅ **P2 complete**: `PostNewEpisodes` over-fetch reduced.
- ✅ **P3 (interim) complete**: `ActiveEpisodeCount` now refreshes weekly in the same Monday window as `TotalDuration` (and still initializes when cache is empty).
- 🔄 **P1 in progress**: shared recent-candidate query path introduced for `Poster`/`Tweet`/`Bluesky`; runtime hotfix applied for in-memory `podcastRemoved` filtering (`IsDefined()` removed from LINQ-to-Objects path); **hourly orchestration smoke run passed with no exceptions**; awaiting **24–48h telemetry validation**.
- ✅ **P4 out (deployed), under observation**: social recent-candidate reads, `HomepagePublisher` recent episodes, and `RecentPodcastEpisodeCategoriser` now use podcast-level `latestReleased` metadata plus partition-scoped episode reads to avoid broad cross-partition scans. Treated as complete implementation, pending stability confirmation.
- 🔄 **P5 in progress**: shared recent-candidate discovery is being consolidated further by using a common lookback threshold across `Poster`/`Tweet`/`Bluesky`/`Categoriser` and cache reuse for narrower follow-up requests. Reddit lookback is configurable via required `postingCriteria.RedditDays` (no hardcoded magic number), Bluesky has required `postingCriteria.BlueSkyDays`, Categoriser has required `postingCriteria.CategoriserDays`, service-specific methods use their own day settings, and shared candidate caching uses `postingCriteria.MaxDays` then reduces by requested `releasedSince` (older-than-window requests log error and return cache-window data).
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
| P4 | Replace cross-partition recent queries with bounded active-subset fan-out | Varies (sensitive to active-podcast cardinality) | ~$0.00–0.04 |
| P5 | Consolidate episode discovery | ~120 cross-partition queries/day | ~$0.05–0.08 |
| P6 | Reduce HalfHourly scope | ~96 cross-partition queries/day | ~$0.05–0.08 |
| | **Remaining estimated** | | **~$0.10–0.20/day** |

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

1. Validate post-change Azure cost and dependency telemetry for at least 48h to confirm P1/P4/P5 stability after deployment.
2. Measure full P5 RU/latency delta versus baseline and document stable-state keep/rollback decision criteria.
3. Evaluate Priority 6 (HalfHourly scope reduction).
