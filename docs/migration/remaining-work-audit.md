# Remaining Migration Work — Audit

> Last verified: post-`Persistence.Legacy` removal (PR #870, branch `fix/members-first-platform-enrichment`).

---

## ✅ Confirmed complete

- Detached episode persistence is the only production path.
- `PodcastUpdater` is the registered default `IPodcastUpdater`.
- `IPodcastRepository` + `IEpisodeRepository` used across all production paths.
- Legacy `Persistence.Legacy`, embedded-container repositories, and `LegacyPodcastToV2Migration` removed.
- `PodcastEpisodeV2` dissolved into canonical `PodcastEpisode` record.
- `IPodcastEpisodeProvider` / `IPodcastEpisodePoster` are the active detached-model interfaces.
- `EpisodeSearchIndexerService` uses detached repositories and `PodcastEpisode`.
- Search datasource uses `FROM episodes e` (no `JOIN e IN p.episodes`).
- `ICosmosDbContainerFactory` exposes explicit factory methods for all containers.
- Social/shortener boundaries use `PodcastEpisode` contracts.
- `CosmosDbDownloader` / `CosmosDbUploader` / `PublicDatabasePublisher` read split containers.
- `EpisodeDriftDetector` implemented for metadata drift detection and optional correction.

---

## ❌ Not yet started

### Tests

No automated tests cover detached-episode code paths. Existing test projects predate the migration.

Suggested coverage: `PodcastUpdater`, `EpisodeRepository`, `PodcastEpisodeProvider`, `PodcastEpisodePoster`, `EpisodeSearchIndexerService`.

---

## ⏳ Optional follow-ups

- [ ] Retire `ModelTransformer` / `JsonSplitCosmosDbUploader` if no operational use remains.
- [ ] Phase 7 verification gates (RU/latency, end-to-end flow validation) — run if not already done in production soak.

---

## ℹ️ Historical note

Earlier versions of this file referenced `IPodcastRepositoryV2`, legacy dual-write, and `--use-v2` tooling flags. Those paths are removed. See `docs/migration/v2-*.md` for archived migration detail only.
