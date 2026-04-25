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
- Keep `docs/post-migration/cost-analysis.md` up to date as cost-reduction work progresses and provide explicit next steps after each change.
- Avoid behavior-changing edits to Reddit posting and Twitter tweeting flows during cost-reduction work unless explicitly approved.
- When investigating Indexer app cost increases, instrumentation must include all activities in the orchestration, not only the Indexer activity, before drawing cost conclusions.
- In dedup verification mode, do not modify backup files; validate and enforce that canonical Cosmos episodes are not ignored/removed when any deleted duplicate had ignored/removed=false.

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

## Execution Path Validation
- When validating this execution path live, use the Poster console app with -r -t -b flags against production.

## Build Tooling
- When using the run_build tool, confirm the status of ReSharper Build, as it may not return results. Instead, use get_errors on individual files.

## Editing Guidelines
- When editing files, never include markdown fences, file labels, or tool artifacts in the edit content; apply plain code-only edits to avoid file corruption.
- Prioritize preventing top-of-file artifact insertion and immediately verify line 1 after each edit on touched files.

## Telemetry
- For production cost-probe telemetry in this repo, emit probe logs at Warning level or above because Information logs are disabled in Application Insights.
- For Indexer cost probe logging, only updateMs should be logged; initiate and complete timing fields are not needed.

## Azure Functions
- For Azure Functions Flex Consumption instanceMemoryMB, valid values are 512, 2048, and 4096; 1024 is not valid.

## Memory Probing
- Prefer memory-probe responsibility delegated to an injected `IMemoryProbeOrchestrator`, with call sites using `Start(nameof(Class))` and `End()`, and the orchestrator deciding via `MemoryProbeOptions` whether to create/log a session.