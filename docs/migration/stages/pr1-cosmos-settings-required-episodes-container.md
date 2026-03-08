# PR1 Stage Note: Required Cosmos settings and strict episodes container resolution

## Change
- Updated `Class-Libraries/RedditPodcastPoster.Persistence/CosmosDbSettings.cs`:
  - `Endpoint` -> required
  - `AuthKeyOrResourceToken` -> required
  - `DatabaseId` -> required
  - `Container` -> required
  - `EpisodesContainer` -> required
  - `UseGateway` remains optional

- Updated `Class-Libraries/RedditPodcastPoster.Persistence/CosmosDbContainerFactory.cs`:
  - `CreateEpisodesContainer()` now requires `EpisodesContainer` and throws `InvalidOperationException` if missing/whitespace.
  - Removed fallback to default `Container` for episodes path.

## Why
- Enforces explicit detached-episodes container configuration.
- Prevents accidental writes/reads against podcasts container for episode repository path.

## Operational note
- Environment/app settings must include `cosmosdb:EpisodesContainer` in all deployment targets.
