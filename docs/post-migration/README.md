# Post-Migration Status

> **Status (2026):** The detached-episode migration is complete. Legacy `Persistence.Legacy`,
> the embedded `CultPodcasts` container path, and one-shot migration tooling have been removed.
> Production uses split Cosmos containers (`Podcasts`, `Episodes`, etc.) via
> `RedditPodcastPoster.Persistence`.

---

## Completed

- Detached episode persistence is the only runtime path.
- Repository interfaces use primary names (`IPodcastRepository`, `IEpisodeRepository`, `ISubjectRepository`, etc.).
- `PodcastEpisode` is the canonical podcast+episode pair type.
- `PodcastUpdater` is the default `IPodcastUpdater`.
- Social, shortener, and search-index paths use detached models end-to-end.
- Legacy migration console apps (`LegacyPodcastToV2Migration`, etc.) removed from the solution.

---

## Ongoing / optional follow-ups

These are not blocking production:

- [ ] Add unit/integration tests for detached-episode code paths (see historical checklist in `docs/migration/remaining-work-audit.md`).
- [ ] Retire or reassess pre-migration console tools (`ModelTransformer`, `JsonSplitCosmosDbUploader`) if no longer needed.

---

## Related docs

| Doc | Purpose |
|-----|---------|
| [`docs/migration/README.md`](../migration/README.md) | Migration entry point (historical + completion summary) |
| [`docs/post-migration/cost-analysis.md`](./cost-analysis.md) | Cosmos/Functions cost investigation |
| [`docs/migration/remaining-work-audit.md`](../migration/remaining-work-audit.md) | Detailed audit (partially historical) |

Historical stage docs under `docs/migration/stages/` and `docs/migration/v2-*.md` describe the migration as it happened; they are not current architecture references.
