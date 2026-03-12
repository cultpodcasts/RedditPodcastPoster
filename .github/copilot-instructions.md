# Copilot Instructions

## Project Guidelines
- User prefers minimizing search-index payload by storing compact identifiers (YouTube/Spotify/Apple IDs) and reduced key names, with UI reconstructing URLs.
- User prefers using the term `CompactSearchRecord` and avoiding `V2` terminology in migration planning and schema naming.
- User does not want additional Episode members for compact IDs; use existing Spotify/YouTube/Apple IDs and derive Apple episode slug from Apple URL via regex.
- In migration/infrastructure docs, use `LookUps` as the Cosmos container name instead of `knownTerms`, and store the single `KnownTerms` typed item in `LookUps`.
- EliminationTerms should be stored in the `LookUps` container, and infrastructure should include a dedicated `PushSubscriptions` container.
- Prefer using `PodcastEpisodeV2` and V2 models across the codebase, except in `PodcastRepository` and `LegacyPodcastToV2Migration`.
- Prefer migrating code to V2 podcast/episode models everywhere possible; keep legacy models only in `PodcastRepository` and `LegacyPodcastToV2Migration`.
- Use V2 repository implementations when both legacy and V2 repositories exist in this codebase.
- The project is not using soft-delete for Azure Search indexing.
- Episode ID is globally unique in this project.
- Documents under `docs/migration` must be kept up to date when making related changes.

## Container Creation
- User prefers explicit container factory methods `CreatePodcastsContainer()` and `CreateEpisodesContainer()` instead of `Create(string containerName)`.

## Tooling Preferences
- User does not want unrelated secret-management tooling invoked during code-edit/debug tasks; only use tools directly relevant to the requested change.

## Azure Search Indexing
- When automating Azure Search indexer reruns, wait for the newly triggered run to start (using status start-time correlation) before evaluating completion to avoid premature retriggers and 409 conflicts.

## Migration Verification
- When resetting the V2 database for migration verification, use the built-in teardown flags of the `LegacyPodcastToV2Migration` app rather than manual deletes.

## Debugging Preferences
- User prefers evidence and ongoing debugging notes to be added to `debug-session-summary.md` instead of being expanded in chat.

## Episode Management
- Use `podcast.Removed` as the canonical removal flag; do not trust episode-level `podcastRemoved` values when deciding whether an episode can be tweeted or posted.