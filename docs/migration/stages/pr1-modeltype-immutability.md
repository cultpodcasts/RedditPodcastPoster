# PR1 Stage Note: Episode `ModelType` Immutability

## Change
- `Episode.ModelType` is now getter-only:
  - `public ModelType ModelType { get; } = ModelType.Episode;`

## Reason
- `ModelType` is invariant for `Episode` entities and should not be mutable after construction.
- Prevents accidental reassignment and keeps persisted entity semantics stable.

## Code updates in this stage
- Updated `Class-Libraries/RedditPodcastPoster.Models/Episode.cs`.
- Removed explicit assignment in `Class-Libraries/RedditPodcastPoster.Persistence/PodcastRepository.cs` where `episodeToMerge.ModelType` was being set.

## Validation
- Solution build passes after the change.

## Follow-up
- Continue PR1 work by wiring `IEpisodeRepository` implementation and migration-safe read paths.
