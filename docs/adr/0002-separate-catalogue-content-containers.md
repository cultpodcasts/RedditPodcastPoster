---
title: "ADR-0002: Separate Cosmos containers per catalogue content family"
status: "Accepted"
date: "2026-09-03"
accepted: "2026-09-23"
authors: "Catalogue platform (planning)"
tags: ["architecture", "cosmos", "catalogue", "content-types"]
supersedes: ""
superseded_by: ""
---

# ADR-0002: Separate Cosmos containers per catalogue content family

## Status

**Accepted** — 2026-09-23 sign-off quiz (epic S-001…S-010). No production Cosmos writes until Phase 1+ is explicitly scheduled and approved.

**Terminology:** product type is **Film** — a standalone work **made as a film** (cinema or one-off TV; premiere venue irrelevant). Do **not** use Movie / Movies / `/movie/` in catalogue APIs or routes. Provider scrapers may still detect JSON-LD `Movie` as a classification **input** that often maps to Film.

## Context

**CTX-001**: Ingestible URLs that are not Spotify / Apple / YouTube podcast-service episodes are stored as **Podcast + Episode**. Streaming TV, one-off films/documentaries, and news inherit podcast semantics.

**CTX-002**: Product needs distinct parent/playable relationships:

| Family | Parent | Playable |
|--------|--------|----------|
| Podcast | Podcast | Episode |
| TV series | TvShow | TvShowEpisode |
| One-off film (made as a film) | — | **Film** |
| News | NewsOrganisation | NewsReport |

YouTube **entertainment** channels remain **podcasts**. **News-station** YouTube channels already stored as Podcasts migrate to **NewsOrganisation**.

**CTX-003**: Search will facet by `contentKind` ([ADR-0003](./0003-unified-playable-search-document.md)).

**CTX-004**: Rollout is phased — flagged new submits **plus** migration tools for the mis-filed corpus. Dry-run default; `--apply` requires explicit approval. Order: **News → Film → TV**.

**CTX-005**: Existing partition patterns: Podcasts `/id`, Episodes `/podcastId`.

## Decision

**DEC-001**: **Separate Cosmos containers** per content family (not a discriminator on Podcast/Episode).

| Container | Partition key | Parent → child |
|-----------|---------------|----------------|
| **Podcasts** (existing) | `/id` | → **Episodes** `/podcastId` |
| **TvShows** (new) | `/id` | → **TvShowEpisodes** `/tvShowId` |
| **Films** (new) | `/id` | — (document is the playable) |
| **NewsOrganisations** (new) | `/id` | → **NewsReports** `/newsOrganisationId` |

**DEC-002**: **TvShowEpisodes** and **NewsReports** are **own containers** — never stored in Episodes.

**DEC-003**: **NewsReports** always belong to a **NewsOrganisation**. Flat news without an outlet is out of scope.

**DEC-004**: **Films have no parent.** No parent container and no parent-name field in search/API.

**DEC-005**: Reuse the Episode **`services` map** on TvShowEpisode, Film, and NewsReport ([episode-services.md](../episode-services.md)).

**DEC-006**: YouTube entertainment stays Podcast. News-station YouTube Podcasts are NewsOrganisation migration candidates.

**DEC-007**: BBC `/news/` remains non-submit until an explicit news matcher ships. Migration of existing YouTube news Podcasts does **not** wait on that matcher.

**DEC-008**: Migrate existing mis-filed rows with console tools (identify dry-run → curated apply). **Preserve playable GUIDs** on move. Prefer search doc **swap**.

**DEC-009** (routes — signed with epic): public paths use SEO **slug** + playable **`shortId`** (base64url of GUID). Parents: `/tv/{slug}`, `/news/{slug}`. Playables: `/film/{slug}/{shortId}`, `/tv/{slug}/{shortId}`, `/news/{slug}/{shortId}`. Podcast paths unchanged.

## Consequences

### Positive

- Clear domain boundaries; independent repos, indexers, migrations.
- Film lifecycle is a single document — no empty parent shells.
- Phased rollout + migration tools reclaim the corpus without big-bang rewrite.
- News organisation attach can mirror Podcast name-attach.

### Negative

- More containers and API surface.
- Lookup queries multiple playable containers.
- Migration heuristics imperfect — **allowlist** for news `--apply`; curator review required.
- Shortener / page-details must become **kind-aware** (epic OPEN-003).
- Classifier must distinguish made-as-film one-offs from series episodes (OPEN-001 resolved; CLS-* remain for edges).

## Alternatives considered

### Type discriminator on Podcast/Episode only

Rejected — TvShowEpisode / NewsReport / Film lifecycles do not fit Episode; couples unrelated rules.

### New containers for new submits only; never migrate

Rejected — corpus already contains news stations, one-offs, and series as Podcasts.

### Separate search indexes per kind

Rejected — Free-tier storage ([storage impact](../catalogue-content-types-search-storage-impact.md)); see ADR-0003.

### Product name Movie

Rejected 2026-09-23 — use **Film**.

## Implementation notes

- **IMP-001**: Phase 1 adds bicep containers + models + repositories — **no production writes**.
- **IMP-002**: Submit v2 classifies URL → kind behind a feature flag ([epic](../catalogue-content-types-epic.md) Phase 3); provider `IsMovie` / one-off signals → Film when made-as-film (OPEN-001); series → TvShow.
- **IMP-003**: Auth: same **`submit`** / **`curate`** for all kinds (OPEN-010).
- **IMP-004**: Migration tools: dry-run → allowlisted `--apply` for news; search swap; redirects from old episode URLs.
- **IMP-005**: `shortId` = base64url of GUID bytes for Podcast; for Film/TV/News prepend ASCII `f`/`t`/`n` before base64url. Same encoding on routes and shortener (epic OPEN-003). Match website `GuidService` byte order.

## References

- [catalogue-content-types-epic.md](../catalogue-content-types-epic.md)
- [ADR-0003](./0003-unified-playable-search-document.md)
- [catalogue-content-types-search-storage-impact.md](../catalogue-content-types-search-storage-impact.md)
- [website submit-url-flows.md](../../../website/cultpodcasts/docs/submit-url-flows.md)
