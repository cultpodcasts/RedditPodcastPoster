# PR1 Stage Note: Split container configuration for legacy + Podcasts + Episodes

## Change
- Added `PodcastsContainer` to `CosmosDbSettings`.
- Reintroduced `Create()` on `ICosmosDbContainerFactory` / `CosmosDbContainerFactory` for legacy/original container access.
- Kept explicit methods:
  - `CreatePodcastsContainer()`
  - `CreateEpisodesContainer()`

## Purpose
- Keep legacy/original data isolated (`Container`) while enabling migration to dedicated new containers (`PodcastsContainer`, `EpisodesContainer`).
- Enable migration code to read from old container and write to new podcast/episode containers.

## DI behavior
- Default `Container` registration path now uses `Create()` (legacy/original container).
- `EpisodeRepository` uses `CreateEpisodesContainer()`.

## Next
- Introduce a dedicated `PodcastRepository` path that uses `CreatePodcastsContainer()` during cutover stage.
