# Remaining Migration Work - Audit

> Last verified against codebase: feature/my-diversion-then-ai-bringing-back-in

---

## ✅ Confirmed complete (verified in codebase)

- Detached episode persistence model is live in production.
- `PodcastUpdater` (V2-based) is the registered default `IPodcastUpdater`.
- `IPodcastRepositoryV2` + `IEpisodeRepository` used across all production paths.
- `IPodcastRepository` (legacy) confined to `LegacyPodcastToV2Migration` only, registered
  via isolated `AddLegacyPodcastRepository()` — not reachable by any other app.
- `ToLegacyPodcast`, `ToLegacyEpisode`, `.ToLegacy()` helpers removed from codebase.
- `FindSpotifyEpisodeRequestFactory` / `FindAppleEpisodeRequestFactory` fully on V2 model
  aliases (`using Episode = RedditPodcastPoster.Models.V2.Episode`) — no legacy overloads remain.
- `PodcastEpisodeV2` dissolved into canonical `PodcastEpisode` record (detached pair).
- `IPodcastEpisodeProvider` / `IPodcastEpisodePoster` **evolved** into canonical detached-model
  interfaces; they now work with the new `PodcastEpisode` pair and are the active implementations.
  They were NOT retired — they are the primary interfaces.
- `EpisodeSearchIndexerService` uses `IEpisodeRepository` + `IPodcastRepositoryV2` + detached
  `PodcastEpisode`.
- Search datasource uses `FROM episodes e` (no `JOIN e IN p.episodes`).
- `ICosmosDbContainerFactory` exposes explicit factory methods for all V2 containers.
- Social/shortener boundaries (`ITweetPoster`, `IBlueskyPoster`, `IShortnerService`) migrated
  to `PodcastEpisode` contracts.
- `CosmosDbDownloader --use-v2` implemented — downloads from all V2 containers to local files.
- `CosmosDbUploader --use-v2` implemented — uploads V2 local files to all V2 containers.
- `PublicDatabasePublisher --use-v2` implemented — reads from V2 Podcasts + Episodes containers.
- `IPodcastFilterV2` / `PodcastFilterV2` deleted — had no consumers; V1 `IPodcastFilter` already uses V2 models and is the correct design for its callers (sync, pre-loaded objects).
- `IPodcastEpisodeFilterV2` / `PodcastEpisodeFilterV2` deleted — `PodcastEpisodeProvider` consolidated onto `IPodcastEpisodeFilter`; V1 logic is battle-tested.
- `PodcastEpisodeProvider` migrated from `IPodcastEpisodeFilterV2` → `IPodcastEpisodeFilter`.
- `EpisodeDriftDetector` (`Console-Apps/EpisodeDriftDetector`) implemented — scans all episodes
  against their podcasts, reports metadata drift (`podcastMetadataVersion`, `podcastName`,
  `podcastSearchTerms`, `podcastLanguage`, `podcastRemoved`) and missing IDs derivable from
  service URLs (`spotifyId`, `youTubeId`, `appleId`). `--correct` applies all fixes.

---

## ⚠️ Inaccuracy in earlier doc versions

The previous version of this file claimed "Target Group D: Duplicate service variants — retired
`IPodcastEpisodeProvider` / `IPodcastEpisodePoster`". **This is incorrect.** These were not
retired — they were evolved. The interfaces remain active and are the canonical detached-episode
interfaces. Consumers use them via DI. The old doc was written against a plan that was not
followed exactly.

---

## ℹ️ `PublicDatabasePublisher` legacy mode

The legacy path reads from the legacy `CultPodcasts` container via `ICosmosDbRepository`
and writes output to local files via `ISafeFileEntityWriter`. It does not write to Cosmos.
Use `--use-v2` to read from the V2 Podcasts + Episodes containers instead.

---

## ❌ Not yet started

### Tests (all phases)
Zero tests cover any V2/detached-episode code path. Existing test projects predate the
migration. No coverage for `PodcastUpdater`, `EpisodeRepository`, `PodcastEpisodeProvider`,
`PodcastEpisodePoster`, `EpisodeSearchIndexerService`, `HomepagePublisher`, or any V2 service.

---

## ⏳ Phase 7 verification gates (unrun)

- [ ] Match per-podcast episode counts between legacy and V2 containers.
- [ ] Match search index document totals and sampled field values.
- [ ] Validate publish / delete / unremove / index / tweet flows end-to-end.
- [ ] Validate podcast retrieval and rename fan-out side effects.
- [ ] Validate RU and latency profile on `Episodes` container queries.
- [ ] Validate RU and latency of fan-out metadata updates.
- [ ] Confirm zero writes reach legacy `CultPodcasts` container in production.
- [ ] RU/latency comparison for homepage publish (noted in `next-steps.md`).

---

## 🟡 Low priority / decide fate

- `ModelTransformProcessor` — old model-shape transformer operating on file-based split
  repo. Likely a one-shot tool that predates V2. Candidate for retirement.
- `JsonSplitCosmosDbUploadProcessor` — reads embedded `podcast.Episodes` from JSON source
  files to split-upload. Intentionally pre-V2 bulk-import utility. Assess whether it is
  still needed or can be replaced by `CosmosDbUploader --use-v2`.
