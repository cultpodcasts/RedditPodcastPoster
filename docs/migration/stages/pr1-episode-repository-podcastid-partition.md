# PR1 Stage Note: `EpisodeRepository` switched to `podcastId` partition usage

## Change
- Updated `Class-Libraries/RedditPodcastPoster.Persistence/EpisodeRepository.cs` to use `podcastId` as the Cosmos partition key value for writes, point reads, and deletes.
- Added optional `EpisodesContainer` config in `Class-Libraries/RedditPodcastPoster.Persistence/CosmosDbSettings.cs`.
- Extended container factory API with named-container overload:
  - `ICosmosDbContainerFactory.Create(string containerName)`
- Updated DI wiring in `Persistence.Extensions.ServiceCollectionExtensions` so `EpisodeRepository` can resolve a dedicated episodes container when configured.

## Why
- Aligns implementation with target detached-episode model partition strategy (`/podcastId`).

## Notes
- Query methods currently use container LINQ without explicit partition key in request options to support selector-based methods.
- Point operations (`ReadItemAsync`, `DeleteItemAsync`, `UpsertItemAsync`) use `podcastId` partition key directly.

## Follow-up
- Migrate first runtime path (`EpisodeHandler`) to consume `IEpisodeRepository` for episode retrieval/mutation operations.
