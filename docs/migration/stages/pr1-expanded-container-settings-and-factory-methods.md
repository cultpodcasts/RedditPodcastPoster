# PR1 Stage Note: Expanded Cosmos container settings and explicit factory methods

## Change
- Expanded `CosmosDbSettings` with explicit container-name members:
  - `PodcastsContainer`
  - `EpisodesContainer`
  - `SubjectsContainer`
  - `ActivitiesContainer`
  - `DiscoveryContainer`
  - `LookupContainer`
- Extended `ICosmosDbContainerFactory` and implementation with explicit methods:
  - `CreatePodcastsContainer()`
  - `CreateEpisodesContainer()`
  - `CreateSubjectsContainer()`
  - `CreateActivitiesContainer()`
  - `CreateDiscoveryContainer()`
  - `CreateLookupContainer()`
- Retained `Create()` for legacy/original container path.

## DI alignment
- Kept repository DI registration container-aware and explicit for V2 repositories.
- `IPodcastRepository` registration remains explicit factory style for consistency.

## Purpose
- Prepare configuration/factory surface for parallel infrastructure where legacy and new containers coexist.
- Support migration and post-migration routing without implicit container-name assumptions.
