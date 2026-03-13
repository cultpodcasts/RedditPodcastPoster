# Post-Migration Task Carry-Over

> **Context:** The V2 detached-episode migration branch (`feature/my-diversion-then-ai-bringing-back-in`)
> has been merged to production and will soak-test for a few days before the project is formally
> closed. This document carries forward every action required to complete the decommission of
> legacy code and promote V2-named items to primary naming.
>
> Work here is **not blocking production** but must be completed before the legacy
> `CultPodcasts` container and legacy code paths are removed.

---

## 1. Soak-test monitoring (immediate — during production window)

Run these checks daily while the branch is in soak:

- [ ] Confirm all new episodes are appearing in the V2 `Episodes` container (not just legacy).
- [ ] Confirm zero write errors in Cosmos telemetry for V2 containers.
- [ ] Confirm Azure Search indexer is picking up new episodes from the `Episodes` datasource.
- [ ] Confirm Reddit posting, tweeting, and Bluesky posting continue to function.
- [ ] Run `EpisodeDriftDetector` (dry-run) to confirm no unexpected metadata drift accumulates.

---

## 2. Phase 7 verification gates (run once soak is stable)

- [ ] Match per-podcast episode counts between legacy `CultPodcasts` and V2 `Episodes` containers.
- [ ] Match Azure Search index document totals and sampled field values (`spotify`, `youtube`, `apple`).
- [ ] Validate publish / delete / unremove / index / tweet / Bluesky flows end-to-end.
- [ ] Validate podcast rename fan-out: rename a podcast and confirm all episode `podcastName` fields update.
- [ ] Validate RU and latency profile on `Episodes` container queries vs old embedded queries.
- [ ] Validate RU and latency of fan-out metadata updates.
- [ ] Confirm zero writes reach the legacy `CultPodcasts` container in production.

---

## 3. Retire legacy tools and apps

These console apps read from or write to legacy models/containers and should be decommissioned
once verification is complete.

### 3a. `LegacyPodcastToV2Migration`
- One-shot migration app; no longer needed once data integrity is confirmed.
- **Action:** Delete `Console-Apps/LegacyPodcastToV2Migration/` from the solution.
- **Unblocks:** retirement of `IPodcastRepository` / `PodcastRepository` (step 4a).

### 3b. `ModelTransformProcessor`
- Pre-V2 file-based model shape transformer; predates the detached architecture.
- **Action:** Delete `Console-Apps/ModelTransformer/` from the solution.

### 3c. `JsonSplitCosmosDbUploadProcessor`
- Pre-V2 bulk-import tool that reads `podcast.Episodes` (embedded model).
- **Action:** Assess whether any remaining use case exists; if not, delete
  `Console-Apps/JsonSplitCosmosDbUploader/`. `CosmosDbUploader --use-v2` is the replacement.

---

## 4. Retire legacy repositories and detach consumers

### 4a. `IPodcastRepository` / `PodcastRepository`
**Current consumers:** `LegacyPodcastToV2MigrationProcessor` only (via `AddLegacyPodcastRepository()`).
- **Prerequisite:** complete step 3a (retire migration app).
- **Action:** Delete `IPodcastRepository.cs`, `PodcastRepository.cs`, `AddLegacyPodcastRepository()`
  registration in `ServiceCollectionExtensions`.

### 4b. `ISubjectRepository` / `SubjectRepository` (legacy embedded-container version)
**Current consumers** (must migrate each to `ISubjectRepositoryV2` before deleting):

| File | Action |
|---|---|
| `Cloud/Discovery/DiscoveryProcessor.cs` | Switch to `ISubjectRepositoryV2` |
| `Class-Libraries/RedditPodcastPoster.AI/EpisodeClassifier.cs` | Switch to `ISubjectRepositoryV2` |
| `Console-Apps/EnrichSubjectRedditFlairs/RedditFlairsProcessor.cs` | Switch to `ISubjectRepositoryV2` |
| `Console-Apps/Sqllite3DatabasePublisher/Sqllite3DatabasePublisher.cs` | Switch to `ISubjectRepositoryV2` |
| `LegacyPodcastToV2MigrationProcessor.cs` | Intentionally legacy — covered by step 3a |

- **Action after migration:** Delete `ISubjectRepository.cs`, `SubjectRepository.cs` and their registrations.

### 4c. `IPushSubscriptionRepository` (legacy)
**Current consumers:** `LegacyPodcastToV2MigrationProcessor` only.
- **Prerequisite:** complete step 3a.
- **Action:** Delete `IPushSubscriptionRepository.cs` and its registration.

### 4d. `IDiscoveryResultsRepository` (legacy)
**Current consumers:**

| File | Action |
|---|---|
| `Cloud/Discovery/DiscoveryProcessor.cs` | Switch to `IDiscoveryResultsRepositoryV2` |
| `LegacyPodcastToV2MigrationProcessor.cs` | Intentionally legacy — covered by step 3a |

- **Action after migration:** Delete `IDiscoveryResultsRepository.cs` and its registration.

### 4e. `ICosmosDbRepository` / `CosmosDbRepository`
**Current consumers:**
- `PodcastRepository` — covered by step 4a.
- `SubjectRepository` (legacy) — covered by step 4b.
- `DiscoveryResultsRepository` (legacy) — covered by step 4d.
- `PushSubscriptionRepository` (legacy) — covered by step 4c.
- `PublicDatabasePublisher` legacy mode — legacy mode will remain (intentional); no action needed.
- `CosmosDbDownloader` legacy mode — legacy mode will remain; no action needed.
- `CosmosDbUploader` legacy mode — legacy mode will remain; no action needed.

> Once steps 4a–4d are complete, `ICosmosDbRepository` will only be used by the legacy
> `--no-v2` modes of the three tooling apps. It can be left in place indefinitely for those.

---

## 5. Promote V2-named interfaces to primary naming

These renames should happen as a single coordinated change once all legacy counterparts are
retired (steps 3–4 complete). Each rename is a solution-wide find-and-replace.

| Current name | Target name | Prerequisite |
|---|---|---|
| `IPodcastRepositoryV2` | `IPodcastRepository` | Step 4a complete |
| `PodcastRepositoryV2` | `PodcastRepository` | Step 4a complete |
| `ISubjectRepositoryV2` | `ISubjectRepository` | Step 4b complete |
| `SubjectRepositoryV2` | `SubjectRepository` | Step 4b complete |
| `IPushSubscriptionRepositoryV2` | `IPushSubscriptionRepository` | Step 4c complete |
| `PushSubscriptionRepositoryV2` | `PushSubscriptionRepository` | Step 4c complete |
| `IDiscoveryResultsRepositoryV2` | `IDiscoveryResultsRepository` | Step 4d complete |
| `DiscoveryResultsRepositoryV2` | `DiscoveryResultsRepository` | Step 4d complete |
| `ILookupRepositoryV2` | `ILookupRepository` | No legacy counterpart — can rename any time |
| `LookupRepositoryV2` | `LookupRepository` | No legacy counterpart — can rename any time |
| `ICosmosDbClientFactoryV2` | `ICosmosDbClientFactory` | After retiring legacy V1 client factory |
| `CosmosDbClientFactoryV2` | `CosmosDbClientFactory` | After retiring legacy V1 client factory |
| `CosmosDbSettingsV2` | `CosmosDbSettings` | Requires config key rename (`cosmosdbv2` → `cosmosdb`) across all appsettings and app registrations |

> **Note on `CosmosDbSettingsV2`:** renaming also requires updating the bound configuration
> key string `"cosmosdbv2"` to `"cosmosdb"` in every `BindConfiguration<CosmosDbSettingsV2>(...)` call
> and in every `appsettings.json` / environment variable prefix. Co-ordinate with any
> infrastructure/deployment config at the same time.

---

## 6. Tests

Zero tests currently cover V2/detached-episode code paths. These should be added after
stabilisation:

- [ ] Unit tests: `PodcastUpdater` (episode enrichment, merge, filter, persistence paths)
- [ ] Unit tests: `EpisodeRepository` (CRUD, partition-key scoped queries)
- [ ] Unit tests: `EpisodeSearchIndexerService` (document mapping, index write)
- [ ] Unit tests: `PodcastEpisodeProvider` (untweet, Bluesky filter, indexing-error elimination)
- [ ] Unit tests: `PodcastEpisodePoster` (post, delay, Apple grace period)
- [ ] Integration test: homepage publish flow (projection shapes, count/duration aggregations)
- [ ] Regression test: Cosmos LINQ expression projections (no constructor-based projections in
  expression trees — they fail at the Cosmos SDK serialisation layer)

---

## 7. Summary order of execution

```
Soak test (days 1–N)
  └─ Phase 7 verification gates
       └─ 3a. Retire LegacyPodcastToV2Migration
            ├─ 3b. Retire ModelTransformProcessor
            ├─ 3c. Retire JsonSplitCosmosDbUploadProcessor
            ├─ 4b. Migrate ISubjectRepository consumers → ISubjectRepositoryV2
            ├─ 4d. Migrate IDiscoveryResultsRepository consumers → IDiscoveryResultsRepositoryV2
            └─ 4a/4c. Retire IPodcastRepository, IPushSubscriptionRepository
                 └─ 5. Rename all V2-named interfaces to primary
                      └─ 6. Add tests
```
