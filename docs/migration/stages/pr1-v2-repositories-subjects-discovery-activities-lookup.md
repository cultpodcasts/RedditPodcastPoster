# PR1 Stage Note: Added V2 repositories for subjects, discovery, activities, and lookup

## Change
Added V2 repository abstractions:
- `ISubjectRepositoryV2`
- `IDiscoveryResultsRepositoryV2`
- `IActivityRepositoryV2`
- `ILookupRepositoryV2`

Added V2 repository implementations:
- `SubjectRepositoryV2`
- `DiscoveryResultsRepositoryV2`
- `ActivityRepositoryV2`
- `LookupRepositoryV2`

Added V2 activity model:
- `RedditPodcastPoster.Models.V2.Activity`

## DI
Registered the new repositories in persistence `AddRepositories()` using explicit container-factory methods:
- `CreateSubjectsContainer()`
- `CreateDiscoveryContainer()`
- `CreateActivitiesContainer()`
- `CreateLookupContainer()` (targets the `LookUps` container)

## Lookup details
`ILookupRepositoryV2` / `LookupRepositoryV2` is intentionally read-focused and exposes:
- `GetEliminationTerms()`
- `GetKnownTerms<TKnownTerms>()`

`GetEliminationTerms()` now defensively checks both:
- `Id == EliminationTerms._Id`
- `ModelType == ModelType.EliminationTerms`

Single-item lookup documents (including typed `KnownTerms` and `EliminationTerms`) are stored in the `LookUps` container.
