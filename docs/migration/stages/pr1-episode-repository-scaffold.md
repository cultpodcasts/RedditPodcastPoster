# PR1 Stage Note: `IEpisodeRepository` Scaffold and DI Wiring

## Change
- Added `Class-Libraries/RedditPodcastPoster.Persistence/EpisodeRepository.cs` implementing `IEpisodeRepository`.
- Registered repository in `Class-Libraries/RedditPodcastPoster.Persistence/Extensions/ServiceCollectionExtensions.cs`:
  - `AddSingleton<IEpisodeRepository, EpisodeRepository>()`

## Scope of this stage
- Introduces repository contract implementation and registration only.
- Keeps existing podcast-embedded flows untouched for now.
- Enables subsequent slices to migrate handlers/processors incrementally.

## Technical notes
- Repository currently reads/writes episodes in the existing Cosmos container context.
- Methods implemented:
  - `GetEpisode(podcastId, episodeId)`
  - `GetByPodcastId(podcastId)`
  - `Save(episode)`
  - `Save(episodes)`
  - `Delete(podcastId, episodeId)`
  - `GetBy(selector)`
  - `GetAllBy(selector)`

## Follow-up
- Move selected read paths to `IEpisodeRepository`.
- Introduce container and partition-key migration for detached-episode model at cutover stage.
