# PR1 Stage Note: Explicit Cosmos container factory methods

## Change
- Replaced generic container factory API with explicit methods:
  - `CreatePodcastsContainer()`
  - `CreateEpisodesContainer()`

## Files
- `Class-Libraries/RedditPodcastPoster.Persistence.Abstractions/ICosmosDbContainerFactory.cs`
- `Class-Libraries/RedditPodcastPoster.Persistence/CosmosDbContainerFactory.cs`
- `Class-Libraries/RedditPodcastPoster.Persistence/Extensions/ServiceCollectionExtensions.cs`

## Why
- Aligns factory API with intended container semantics.
- Improves readability and reduces accidental container selection mistakes.

## Behavior
- Podcasts path uses configured default container (`cosmosdb:Container`).
- Episodes path uses required `cosmosdb:EpisodesContainer`; missing/whitespace throws `InvalidOperationException`.
