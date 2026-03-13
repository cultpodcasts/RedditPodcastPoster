# PR1 Stage Note: Legacy-to-V2 migration service scaffold

## Change
- Added migration abstraction:
  - `ILegacyPodcastToV2MigrationService`
  - `LegacyPodcastToV2MigrationResult`
- Added implementation:
  - `LegacyPodcastToV2MigrationService`

## Behavior
- Reads legacy podcasts (embedded episodes) via `IPodcastRepository`.
- Writes V2 podcasts via `IPodcastRepositoryV2`.
- Writes V2 episodes via `IEpisodeRepository`.
- Performs field mapping from legacy models to `Models.V2`.
- Returns failure identity detail in result payload:
  - `FailedPodcastIds`
  - `FailedEpisodeIds`
- Failed counts are derived from collection sizes:
  - failed podcasts = `FailedPodcastIds.Count`
  - failed episodes = `FailedEpisodeIds.Count`

## DI
- Registered `ILegacyPodcastToV2MigrationService` in persistence `AddRepositories()`.

## Purpose
- Enables side-by-side migration from old container model to new `PodcastsContainer` + `EpisodesContainer` model while keeping legacy path intact.
