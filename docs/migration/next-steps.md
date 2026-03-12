Migration next steps - updated

Summary:
- Applied a production-affecting fix for Cosmos LINQ projection translation in homepage publishing.

Completed in this update:
- Restored server-side projection path in `Class-Libraries/RedditPodcastPoster.Persistence/EpisodeRepository.cs` (`.Where(...).Select(projection)` before iterator materialization).
- Updated constructor-based projection call sites in `Class-Libraries/RedditPodcastPoster.ContentPublisher/HomepagePublisher.cs` to anonymous-type projections that Cosmos can translate.
- Added debugging evidence in `docs/migration/debug-session-summary.md`.

Why this was required:
- Cosmos LINQ provider throws `Constructor invocation is not supported` for constructor-based projection expressions (for example, record primary-constructor projections) during query translation.

Recommended immediate follow-ups:
1. Run `Poster` publish flow end-to-end and verify homepage generation succeeds without `DocumentQueryException`.
2. Capture RU and latency comparison for homepage publish before/after to confirm expected server-side projection efficiency.
3. Add a small regression test around projection-query patterns to prevent reintroducing constructor-based Cosmos projections.
4. Continue migration gates from `docs/migration/README.md` (container factory and `LookUps` verification items remain relevant).
