---
title: "ADR-0003: Unified playable search document with contentKind facet"
status: "Proposed"
date: "2026-09-03"
authors: "Catalogue platform (planning)"
tags: ["architecture", "search", "azure-search", "content-types"]
supersedes: ""
superseded_by: ""
---

# ADR-0003: Unified playable search document with contentKind facet

## Status

**Proposed** — Phase 0 planning. No production index recreation from this ADR alone ([episode-services.md](../episode-services.md) guardrail).

## Context

**CTX-001**: Azure AI Search index `cultpodcasts` holds **~82k** episode-shaped documents. Jul 2026: **~49 MB / 50 MB** Free-tier cap ([search-index-slimming-plan.md](../search-index-slimming-plan.md) §3A). Slimming (URL→ID, `svc`, drop `explicit`) targets **~40 MB**.

**CTX-002**: Epic [ADR-0002](./0002-separate-catalogue-content-containers.md) adds **TvShowEpisodes**, **Movies**, and **NewsReports** as playable sources. Search must return mixed playables with correct parent identity and a **`contentKind`** facet: `Podcast | TvShow | Movie | NewsReport`.

**CTX-003**: Existing clients (`SearchResult`, OData filters on `podcastName`, `subjects`, `lang`) must keep working for podcast rows during phased rollout.

**CTX-004**: Dual live indexes on Free tier **sum** storage — ~40 MB × 2 **exceeds** cap ([storage impact analysis](../catalogue-content-types-search-storage-impact.md)).

## Decision

**DEC-001**: **One search index** (`cultpodcasts` or successor name) fed from four playable Cosmos sources:

| Source container | `contentKind` | Parent join at index time |
|------------------|---------------|---------------------------|
| Episodes | `Podcast` | Podcast.name → `podcastName` |
| TvShowEpisodes | `TvShow` | TvShow.name → `tvShowName` |
| Movies | `Movie` | Movie.title → `movieName` |
| NewsReports | `NewsReport` | NewsOrganisation.name → `newsOrganisationName` |

**NewsOrganisations**, **TvShows**, and **Podcasts** are **not** separate index rows — only playables.

**DEC-002**: Add **`contentKind`** — **filterable + facetable** on every document. Existing podcast docs backfilled to `Podcast`.

**DEC-003**: **Backward compatibility** on podcast documents:

- Keep **`id`**, **`podcastName`**, **`episodeTitle`**, **`episodeDescription`**, and existing service fields unchanged for `contentKind=Podcast`.
- Do **not** populate `tvShowName` / `movieName` / `newsOrganisationName` on podcast rows.
- Do **not** duplicate `podcastName` into a generic `parentName` on podcast rows (storage — see impact doc).

**DEC-004**: Non-podcast playables use **kind-specific nullable parent name fields** (not `podcastName`):

| `contentKind` | Playable key | Title field (shared) | Parent name field |
|---------------|--------------|----------------------|-------------------|
| `Podcast` | `id` (episode GUID) | `episodeTitle` | `podcastName` |
| `TvShow` | `id` (tvShowEpisode GUID) | `episodeTitle` | `tvShowName` |
| `Movie` | `id` (movie GUID) | `episodeTitle` | `movieName` |
| `NewsReport` | `id` (newsReport GUID) | `episodeTitle` (headline) | `newsOrganisationName` |

Optional **`parentId`** (filterable) may be added per kind in implementation — not required for Phase 0 sign-off.

**DEC-005**: Reuse existing cross-kind fields where semantics align: `release`, `duration`, `subjects`, `lang`, `image`, `svc`, platform IDs, hidden search term fields (with kind-appropriate source mapping in indexer).

**DEC-006**: **Schema rollout on Free tier** — prefer **in-place field additions** + merge/upload backfill of `contentKind=Podcast`. Avoid two full indexes on Free tier; if a new index is required (immutable field change), follow slimming §8 (SKU bump or delete-rebuild), ideally **combining** any remaining slimming deltas with `contentKind` in **one** cutover.

**DEC-007**: **Default query behaviour during rollout** (website + API): when `contentKind` facet/filter is **omitted**, return **all kinds** once UI supports cards; until Phase 4, Worker may default filter `contentKind eq 'Podcast'` — **confirm at Phase 2** (open question in epic).

## Consequences

### Positive

- **POS-001**: Single quota pool and one OData facet pipeline for `contentKind`.
- **POS-002**: Podcast clients unchanged if they ignore unknown fields and omit `contentKind` filter (or filter `Podcast` during transition).
- **POS-003**: News outlet name available for display/filter without indexing organisation rows separately.
- **POS-004**: In-place schema extension avoids dual-index **80 MB** failure mode on Free tier.

### Negative

- **NEG-001**: Polymorphic `SearchResult` — website routes by `contentKind`; `podcastName` empty on non-podcast hits.
- **NEG-002**: Indexer projects four Cosmos sources — more failure modes; id GUID space is shared (keys must remain globally unique across containers).
- **NEG-003**: Large NewsReport catalogues add **row count** faster than field overhead — capacity risk ([storage impact](../catalogue-content-types-search-storage-impact.md)).
- **NEG-004**: Rename of `episodeTitle` → `title` deferred — would break OData and clients for negligible quota benefit.

## Alternatives Considered

### Generic `parentName` + `parentId` only (drop `podcastName`)

- **ALT-001**: **Description**: One parent label field for all kinds.
- **ALT-002**: **Rejection Reason**: Breaks existing `podcastName` filters/facets and Worker `getPageDetails` without a coordinated breaking change; rejected for phased compat.

### Duplicate `parentName` = `podcastName` on all podcast rows

- **ALT-003**: **Description**: Easier generic card template.
- **ALT-004**: **Rejection Reason**: ~1.5 MB redundant stored labels on 82k docs; rejected.

### Separate indexes per content kind

- **ALT-005**: **Description**: Federated search in Worker.
- **ALT-006**: **Rejection Reason**: Multiplies Free-tier storage; rejected (ADR-0002).

### Type discriminator inside Episode container only

- **ALT-007**: **Rejection Reason**: Conflicts with ADR-0002 container split.

## Implementation Notes

- **IMP-001**: Rename `EpisodeSearchRecord` → `PlayableSearchRecord` (or parallel type) in Phase 2 — **code change only after ADR accepted**.
- **IMP-002**: Extend `EntitySearchIndexer` + `CreateSearchIndex` datasource SQL — **do not** tear down production index from docs alone.
- **IMP-003**: Worker `POST /search` passthrough adds optional `contentKind` facet param; `getPageDetails` must resolve detail route by kind.
- **IMP-004**: Angular: `SearchResult.contentKind?`; cards branch on kind; podcast templates unchanged when kind absent or `Podcast`.
- **IMP-005**: Migration tools: **swap** search docs when moving Episode → TvShowEpisode / Movie / NewsReport (prefer **same `id`**, update `contentKind` + parent name fields) — keep Free-tier doc count flat. Never leave duplicate keys.
- **IMP-006**: Re-measure `storageSize` after adding `contentKind` and after first **1k** non-podcast sample docs.

## References

- **REF-001**: [catalogue-content-types-epic.md](../catalogue-content-types-epic.md)
- **REF-002**: [ADR-0002](./0002-separate-catalogue-content-containers.md)
- **REF-003**: [catalogue-content-types-search-storage-impact.md](../catalogue-content-types-search-storage-impact.md)
- **REF-004**: [search-index-slimming-plan.md](../search-index-slimming-plan.md)
- **REF-005**: `Class-Libraries/RedditPodcastPoster.Search/Models/EpisodeSearchRecord.cs`
