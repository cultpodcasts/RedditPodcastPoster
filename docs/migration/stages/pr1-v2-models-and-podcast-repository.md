# PR1 Stage Note: Introduced V2 models and V2 podcast repository

## Change
- Added new model namespace `RedditPodcastPoster.Models.V2`:
  - `Podcast`
  - `Episode`
- Added new repository abstraction:
  - `IPodcastRepositoryV2`
- Added implementation:
  - `PodcastRepositoryV2`

## Why
- Keep legacy model/repository paths intact for migration reads.
- Introduce isolated V2 types and repository path targeting new containers.

## DI
- Registered `IPodcastRepositoryV2` in persistence DI using:
  - `CreatePodcastsContainer()`

## Note
- Existing `PodcastRepository` and legacy models remain unchanged for old-container migration flow.
