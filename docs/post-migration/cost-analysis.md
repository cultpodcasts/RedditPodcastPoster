# Post-Migration Cost Analysis

## Summary

After the Episode Detachment Migration (completed ~March 11), daily Azure costs have approximately **doubled** compared to the pre-migration baseline. Two root causes have been identified:

1. The detached-episode model has increased the number and cost of Cosmos DB queries executed by the Azure Functions, inflating **Function compute time** (GB-s billing).
2. **The old Cosmos DB account (`cultpodcasts-ukdb`) is still being connected to** by every Function app startup, generating hundreds of background health-check calls per day.

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
| 5 | `Publisher` → `HomepagePublisher` | `EpisodeRepository.GetAllBy(x => !x.Removed && ..., projection→Id)` count query on **Episodes** | **YES — cross-partition** ⚠️ | 1 |
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
| HomepagePublisher (count) | 1 | 1 | 2 |
| HomepagePublisher (recent) | 1 | 1 | 2 |
| Tweet (find untweeted) | 1 | — | 1 |
| Bluesky (find unposted) | 1 | 1 | 2 |
| **Total cross-partition Episode queries** | **6** | **4** | **10/hour = 240/day** |

Plus cross-partition Podcast queries:
- `IndexIdProvider`: 1/hour = 24/day

**Conservatively, the system runs ~240 cross-partition queries per day against the Episodes container**, plus ~24 against the Podcasts container.

### Over-Fetch Problem: `PodcastEpisodesPoster.PostNewEpisodes()`

```csharp
// Fetches ALL episodes for each podcast with an unposted episode
var episodes = await episodeRepository.GetByPodcastId(podcastId).ToArrayAsync();
```

This loads every historical episode for each candidate podcast when only unposted recent episodes are needed. If a podcast has 500 episodes, all 500 are read from Cosmos (partition-scoped but wasteful RU + network).

## Optimization Recommendations

### Priority 0: Remove old Cosmos DB connection (quick win, immediate savings)

**Problem**: `CosmosDbContainerFactory` requires `[FromKeyedServices("v1")] CosmosClient` even though only V2 container methods are used by the Function apps. The V1 `CosmosClient` keeps a background connection alive to the old `cultpodcasts-ukdb` DB.

**What to change**:
1. Remove the V1 `CosmosClient` dependency from `CosmosDbContainerFactory` — it only needs the V2 client.
2. Remove the `Create()` method (V1 container accessor) or move it to a separate legacy-only factory.
3. Remove the keyed `"v1"` `CosmosClient` singleton and non-keyed `CosmosClient`/`Container` registrations from `AddRepositories()`.
4. Remove `cosmosdb` (V1 settings) from `coreSettings` in `functions.bicep` — only include it for `LegacyPodcastToV2Migration`.
5. Remove `IDataRepository`/`ICosmosDbRepository`/`CosmosDbRepository` registrations from `AddRepositories()` (keep in a separate `AddLegacyRepositories()` for migration tool only).

**Files**:
- `Class-Libraries/RedditPodcastPoster.Persistence/CosmosDbContainerFactory.cs`
- `Class-Libraries/RedditPodcastPoster.Persistence/Extensions/ServiceCollectionExtensions.cs`
- `Infrastructure/functions.bicep`

**Impact**: Eliminates ~367 calls/48h to old DB, reduces cold-start time, saves on Function compute. Also allows eventual retirement of the old `cultpodcasts-ukdb` Cosmos account.

### Priority 1: Eliminate redundant cross-partition queries (biggest single impact)

Several activities query the Episodes container for nearly the same thing:
- **Poster**: unposted episodes released in last 7 days
- **Tweet**: untweeted episodes released recently
- **Bluesky**: bluesky-unposted episodes released recently

**Recommendation**: Query once at orchestration start and pass the candidate episode set through the orchestration context, or introduce a shared cache within a single Function execution.

### Priority 2: Fix `PostNewEpisodes` over-fetch

Change:
```csharp
var episodes = await episodeRepository.GetByPodcastId(podcastId).ToArrayAsync();
```
To:
```csharp
var episodes = await episodeRepository.GetByPodcastId(podcastId, 
    x => x.Release >= since && !x.Posted && !x.Ignored && !x.Removed)
    .ToArrayAsync();
```

This uses the existing partition-scoped filtered overload, dramatically reducing data read per podcast.

### Priority 3: Replace cross-partition episode count with cached/stored value

`HomepagePublisher` runs this every hour + half-hour (48/day):
```csharp
episodeRepository.GetAllBy(x => !x.Removed && ..., x => x.Id).ToListAsync()
```
This materializes **all non-removed episode IDs** just to count them.

**Recommendation**: Store `activeEpisodeCount` in the `LookUps` container (alongside `HomePageCache`) and increment/decrement it when episodes are added/removed. Read a single document instead of scanning the entire Episodes container.

### Priority 4: Replace cross-partition "recent episodes" query with partition-scoped queries

The `HomepagePublisher` recent-episodes query and the social-posting queries could:
1. First query the **Podcasts** container for active podcast IDs (smaller container, already done by `IndexIdProvider`).
2. Then issue **partition-scoped** queries per podcast for recent episodes.

This trades one cross-partition query for N small partition-scoped queries, which is cheaper when N is modest.

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
| **P0** | **Remove old DB connection** | **~180 calls/day to old DB** | **Quick win + enables old DB retirement** |
| P1 | Consolidate Poster/Tweet/Bluesky episode queries | ~120 cross-partition queries/day | ~$0.05–0.08 |
| P2 | Fix PostNewEpisodes over-fetch | Varies by podcast count | ~$0.03–0.05 |
| P3 | Cache episode count | ~48 cross-partition queries/day | ~$0.03–0.05 |
| P4 | Replace cross-partition recent queries | Varies | ~$0.02–0.04 |
| P5 | Consolidate episode discovery | ~120 cross-partition queries/day | ~$0.05–0.08 |
| P6 | Reduce HalfHourly scope | ~96 cross-partition queries/day | ~$0.05–0.08 |
| | **Total estimated** | | **~$0.23–0.38/day** |

Target: bring total daily cost from ~$0.50 back to ≤$0.26 (pre-migration level).

### Where the biggest Function compute time is spent (App Insights evidence)

From the App Insights data, the **Indexer activity (4 passes, hourly)** dominates at 1,771s total over 48h — but this is largely driven by **external API calls** (Apple 711s, Spotify 512s, YouTube 143s), not Cosmos. The Cosmos-heavy activities are:

| Activity | 48h total | Cosmos-driven? | Optimization target? |
|----------|----------|---------------|---------------------|
| Indexer | 1,771s | Partially (also external APIs) | P2 (over-fetch) |
| HalfHourlyOrchestration | 293s | Yes | P6 (reduce scope) |
| Bluesky | 230s | Yes | P1/P5 (consolidate queries) |
| Publisher | 172s | Yes (3 cross-partition queries) | P3 (cache count), P4 |
| Poster | 136s | Yes | P2 (over-fetch), P5 |
| Categoriser | 116s | Yes (1 cross-partition query) | P1 |
| Tweet | 57s | Yes | P5 (consolidate) |

## Next Steps

1. **Implement Priority 0** — Remove old DB connection from `CosmosDbContainerFactory` and `functions.bicep`. This is a clean code change with immediate benefit and no functional risk (old DB is not queried for data).
2. Implement Priority 2 (PostNewEpisodes fix) — smallest code change, immediate benefit.
3. Implement Priority 3 (cached episode count) — eliminates expensive recurring scan.
4. Implement Priority 5 (consolidated episode query) — biggest structural improvement.
4. Implement Priority 5 (consolidated episode query) — biggest structural improvement.
5. Evaluate Priority 6 (HalfHourly reduction) — schedule change, no code needed.
