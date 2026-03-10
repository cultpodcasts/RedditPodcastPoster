# Copilot Instructions

## Project Guidelines
- User prefers minimizing search-index payload by storing compact identifiers (YouTube/Spotify/Apple IDs) and reduced key names, with UI reconstructing URLs.
- User prefers using the term `CompactSearchRecord` and avoiding `V2` terminology in migration planning and schema naming.
- User does not want additional Episode members for compact IDs; use existing Spotify/YouTube/Apple IDs and derive Apple episode slug from Apple URL via regex.
- In migration/infrastructure docs, use `LookUps` as the Cosmos container name instead of `knownTerms`, and store the single `KnownTerms` typed item in `LookUps`.
- EliminationTerms should be stored in the `LookUps` container, and infrastructure should include a dedicated `PushSubscriptions` container.
- Prefer using `PodcastEpisodeV2` and V2 models across the codebase, except in `PodcastRepository` and `LegacyPodcastToV2Migration`.
- Prefer migrating code to V2 podcast/episode models everywhere possible; keep legacy models only in `PodcastRepository` and `LegacyPodcastToV2Migration`.

## Container Creation
- User prefers explicit container factory methods `CreatePodcastsContainer()` and `CreateEpisodesContainer()` instead of `Create(string containerName)`.

## Tooling Preferences
- User does not want unrelated secret-management tooling invoked during code-edit/debug tasks; only use tools directly relevant to the requested change.