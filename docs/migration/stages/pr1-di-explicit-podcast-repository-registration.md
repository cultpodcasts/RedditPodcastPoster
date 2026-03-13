# PR1 Stage Note: Explicit `IPodcastRepository` DI registration pattern

## Change
- Updated `Class-Libraries/RedditPodcastPoster.Persistence/Extensions/ServiceCollectionExtensions.cs`:
  - Replaced `.AddSingleton<IPodcastRepository, PodcastRepository>()`
  - With explicit factory registration mirroring `IEpisodeRepository` style.

## Why
- Keeps registration style consistent across repositories.
- Makes dependency composition explicit for future container-split adjustments.

## Behavior
- No functional behavior change intended in this step.
- `PodcastRepository` still resolves the same dependencies (`IDataRepository`, `IEpisodeMatcher`, logger).
