# Debug Session Summary

## Cosmos LINQ projection failure in homepage publishing

- Exception observed: `Microsoft.Azure.Cosmos.Linq.DocumentQueryException: Constructor invocation is not supported`.
- Failing location: `EpisodeRepository.GetAllBy<TProjection>` during `.ToFeedIterator()` for projected queries.
- Root cause: constructor-based projection expressions (record primary constructors) cannot be translated by Cosmos LINQ provider.
- Fix applied: kept repository server-side `.Select(projection)` path and changed call-site projections to anonymous-type initializers (no constructor arguments in expression tree).
- Validation evidence: workspace build completed successfully after changes.

## Follow-up checks

- Execute homepage publish path to confirm runtime behavior and RU profile are stable with server-side projections.
- Keep future Cosmos query projections constructor-free in expression trees.
