---
title: "ADR-0003: Unified playable search document with contentKind facet"
status: "Accepted"
date: "2026-09-03"
accepted: "2026-09-23"
amended: "2026-09-23"
authors: "Catalogue platform (planning)"
tags: ["architecture", "search", "azure-search", "content-types"]
supersedes: ""
superseded_by: ""
---

# ADR-0003: Unified playable search document with contentKind facet

## Status

**Accepted** — 2026-09-23; **amended** same day for unified projection fields (`title` / `seriesName` / `description` / `seriesDescription`). No production index recreation from this ADR alone.

## Context

**CTX-001**: Index `cultpodcasts` holds episode-shaped documents on Azure AI Search Free tier. **Re-measure before Phase 2** (epic OPEN-007).

**CTX-002**: ADR-0002 adds playables **Episodes**, **TvShowEpisodes**, **Films**, **NewsReports**. Search must return mixed playables with facet **`contentKind`**: `Episode | TvShowEpisode | Film | NewsReport`.

**CTX-003**: Legacy clients use `podcastName`, `episodeTitle`, `episodeDescription`. Those names are podcast-centric and wrong for Film/TV/News.

**CTX-004**: Dual live indexes on Free tier typically exceed 50 MB. Prefer **in-place** field add, then retire legacy fields.

**CTX-005**: **Film has no series** — `seriesName` / `seriesDescription` omitted on Film docs.

## Decision

**DEC-001**: **One search index** fed from four playable Cosmos sources. Parents are not separate index rows.

**DEC-002**: Add **`contentKind`** — filterable + facetable. Backfill existing episode rows to **`Episode`**.

**DEC-003** (**amended**): **Target public search-item shape** (epic OPEN-004):

| Field | Role |
|-------|------|
| `title` | Playable title (episode / film / headline) |
| `description` | Playable blurb |
| `seriesName` | Parent name when present (podcast / tv show / news org); **omit for Film** |
| `seriesDescription` | Parent description join when present; **omit for Film** |

| `contentKind` | `title` | `seriesName` | `seriesDescription` |
|---------------|---------|--------------|---------------------|
| `Episode` | episode title | podcast name | podcast description |
| `TvShowEpisode` | episode title | tv show name | tv show description |
| `Film` | film title | omit | omit |
| `NewsReport` | headline | news organisation name | org description |

**DEC-004**: Do **not** use kind-specific parent fields (`tvShowName`, `newsOrganisationName`, `filmName`) on the long-term public model — **`seriesName`** is the shared parent label.

**DEC-005**: Reuse cross-kind fields: `id`, `release`, `duration`, `subjects`, `lang`, `image`, `svc`, platform IDs, hidden search-term fields, plus `contentKind`.

**DEC-006**: Schema rollout — build a **fresh index** combining remaining slimming with the new playable projection (epic OPEN-011). Plan **SKU bump** or **delete-rebuild downtime**; Free tier cannot hold two full copies. Measure live size in Phase 2 (OPEN-007).

**DEC-007**: When `contentKind` is **omitted**, return **all kinds**. Cards must branch by kind.

**DEC-008**: Migration **preserves GUID**; search **swap** updates `contentKind` + projected fields. Id fallback across kinds (epic S-006). Kind-prefixed `shortId` on Film/TV/News (epic OPEN-003).

## Consequences

### Positive

- One card/DTO model for all kinds.
- Film has no fake series fields.
- Clearer API than `episodeTitle` for non-episodes.

### Negative

- Client + OData breaking change when legacy fields drop (mitigate with overlap window).
- **`seriesDescription` is new indexed text** — quota cost; truncate; re-measure.
- Shared GUID keyspace across containers.

## Alternatives considered

### Keep `episodeTitle` / `podcastName` forever

Rejected 2026-09-23 — project into `title` / `seriesName` / `description` / `seriesDescription`.

### Kind-specific parent name fields only (`tvShowName`, …)

Rejected for the public model — prefer one `seriesName` (Film omits).

### Default filter `contentKind eq 'Podcast'` / podcast-only during rollout

Rejected — all kinds when omitted. Podcast playables use **`Episode`**, not `Podcast`, as `contentKind` (OPEN-012).

### Separate indexes per kind

Rejected — Free-tier storage.

### Product facet value `Movie`

Rejected — use **`Film`**.

## Implementation notes

- `EpisodeSearchRecord` → `PlayableSearchRecord` with new field names.
- Parent description must be available on Podcast / TvShow / NewsOrganisation models (or join source) for `seriesDescription`.
- Angular `SearchResult` uses the new shape; Film cards omit series line.
- Re-measure `storageSize` after adding `seriesDescription` and `contentKind`.

## References

- [catalogue-content-types-epic.md](../catalogue-content-types-epic.md)
- [ADR-0002](./0002-separate-catalogue-content-containers.md)
- [catalogue-content-types-search-storage-impact.md](../catalogue-content-types-search-storage-impact.md)
- [search-index-slimming-plan.md](../search-index-slimming-plan.md)
