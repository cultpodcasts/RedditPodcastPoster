Migration next steps - generated

Summary:
- Reviewed `docs/migration/README.md` and scanned repository for related artifacts.

Findings:
- Aligned/implemented:
  - Search index data-source is using detached `episodes` (file: `Console-Apps/CreateSearchIndex/CreateSearchIndexProcessor.cs`).
  - `PodcastUpdater` exists and uses V2 episode/podcast flows (`Class-Libraries/RedditPodcastPoster.PodcastServices/PodcastUpdater.cs`).
  - `KnownTerms` seeder and `PushSubscription` model/handler exist (partial infra for `LookUps` and `PushSubscriptions`).
  - `EpisodeSearchRecord` exists for Azure Search index schema.

- Partial/unclear:
  - Persistence of `KnownTerms` and `EliminationTerms` into a `LookUps` container not clearly discovered; implementations referencing repository abstractions exist but concrete container wiring needs verification.

- Missing / gaps vs README completion gates:
  - No explicit `CreatePodcastsContainer()` / `CreateEpisodesContainer()` factory methods found.
  - No `CompactSearchRecord` type or reduced-key search payload implementation discovered; `EpisodeSearchRecord` is present but not the compact contract.
  - No explicit evidence that `LookUps` container name is in use by the concrete persistence implementation.

Recommended next concrete task (highest priority):
1. Implement explicit container factory methods `CreatePodcastsContainer()` and `CreateEpisodesContainer()` in the Cosmos container factory and wire them into DI; this is a required completion gate in the README.

Follow-ups (after priority task):
2. Verify and, if missing, implement persistence to `LookUps` container for `KnownTerms` and `EliminationTerms`.
3. Design and implement `CompactSearchRecord` model and a plan for rolling it out to search consumers and indexers (update `CreateSearchIndexProcessor` and indexing pipeline as needed).
4. Update `docs/migration/README.md` to mark items that are already implemented (e.g., CreateDataSource for episodes, PodcastUpdater) and clarify remaining gaps.

Suggested immediate PR scope (small, safe):
- Add `CreatePodcastsContainer()` and `CreateEpisodesContainer()` methods in the existing Cosmos container factory class.
- Add unit-tests or integration smoke check to ensure the factory returns containers with expected names/config.

If you want, I can implement the priority task now (add the two factory methods and wire DI).