---
title: "ADR-0002: Separate Cosmos containers per catalogue content family"
status: "Proposed"
date: "2026-09-03"
authors: "Catalogue platform (planning)"
tags: ["architecture", "cosmos", "catalogue", "content-types"]
supersedes: ""
superseded_by: ""
---

# ADR-0002: Separate Cosmos containers per catalogue content family

## Status

**Proposed** — Phase 0 planning. No production Cosmos writes until explicitly approved.

## Context

**CTX-001**: Today, ingestible URLs that are not Spotify / Apple / YouTube podcast-service episodes are stored as **Podcast + Episode** rows. Streaming TV, films, and news clips inherit podcast semantics (`NonPodcastShowNameResolver`, `PodcastNameAttachLookup`).

**CTX-002**: Product needs distinct parent/playable relationships: podcast show → episode; TV series → episode; standalone movie; news outlet → report. YouTube **entertainment** channels remain **podcasts**; **news-station** YouTube channels already stored as Podcasts must migrate to **NewsOrganisation**.

**CTX-003**: Azure AI Search uses a single episode-shaped index today (`EpisodeSearchRecord`). A future unified index must facet by **`contentKind`** without breaking podcast clients ([ADR-0003](./0003-unified-playable-search-document.md)).

**CTX-004**: Rollout is **phased** — new streaming/news submits behind flags **plus** incremental **migration tools** for the existing mis-filed corpus (news / movies / TV already in Podcasts+Episodes). Dry-run default; production Cosmos writes require explicit `--apply`. No big-bang unattended migration.

**CTX-005**: Partition patterns exist: **Podcasts** `/id`, **Episodes** `/podcastId` ([`cosmos-db.bicep`](../../Infrastructure/cosmos-db.bicep)).

## Decision

**DEC-001**: Use **separate Cosmos containers per content family**, not a type discriminator on Podcast/Episode.

| Container | Partition key (proposed) | Parent → child |
|-----------|--------------------------|----------------|
| **Podcasts** (existing) | `/id` | Podcast → **Episodes** `/podcastId` |
| **TvShows** (new) | `/id` | TvShow → **TvShowEpisodes** `/tvShowId` |
| **Movies** (new) | `/id` | — (playable = movie document) |
| **NewsOrganisations** (new) | `/id` | NewsOrganisation → **NewsReports** `/newsOrganisationId` |

**DEC-002**: **TvShowEpisodes** and **NewsReports** are **own containers** — never stored in Episodes.

**DEC-003**: **NewsReports** always belong to a **NewsOrganisation** parent. Flat news without an outlet is out of scope.

**DEC-004**: Reuse the **Episode `services` map** pattern for TvShowEpisode, Movie, and NewsReport platform URLs ([episode-services.md](../episode-services.md)).

**DEC-005**: **YouTube entertainment / show channels stay Podcast** — not TvShow. **News-station YouTube channels** (already in Podcasts) are **NewsOrganisation** migration candidates.

**DEC-006**: BBC `/news/` remains **non-submit** until an explicit news matcher ships; distinct from iPlayer/Sounds submit URLs. Migration of existing YouTube news Podcasts does **not** wait on BBC `/news/` submit.

**DEC-007**: **Migrate existing mis-filed rows** with console tools built as we go (identify dry-run → curated apply). Families: NewsOrganisation/NewsReports, Movies, TvShows/TvShowEpisodes. Prefer **preserving playable GUIDs** on move for search/URL continuity (confirm in epic open questions).

## Consequences

### Positive

- **POS-001**: Clear domain boundaries — independent repositories, indexers, and migrations per family.
- **POS-002**: TvShow/Movie/News lifecycle rules do not complicate Episode CRUD or podcast URLs.
- **POS-003**: Phased rollout: new containers + flags for new submits; migration tools reclaim the existing corpus without a big-bang rewrite.
- **POS-004**: News organisation attach mirrors proven Podcast name-attach semantics.
- **POS-005**: Dry-run identify tools let curators review news-station / movie / TV candidates before any `--apply`.

### Negative

- **NEG-001**: More containers, repositories, and API surface area (CRUD + submit per family).
- **NEG-002**: Lookup must query multiple playable containers for URL membership.
- **NEG-003**: Migration tooling is **required** work (Phase 5, interleaved) — identify heuristics for news YouTube vs true podcasts are imperfect; curator review needed.
- **NEG-004**: Curator UI and public routes multiply (`/tv/`, `/movie/`, `/news/` — see open questions).
- **NEG-005**: During migration, dual presence (old Podcast row + new target) must be avoided or carefully windowed to prevent URL/search duplicates.

## Alternatives Considered

### Type discriminator on Podcast/Episode only

- **ALT-001**: **Description**: `podcastType` / `contentKind` on existing containers.
- **ALT-002**: **Rejection Reason**: TvShowEpisode and NewsReport must not live in Episodes; movies do not fit episode lifecycle; couples unrelated indexing and social-posting rules.

### Hybrid — new containers for new submits only; never migrate legacy

- **ALT-003**: **Description**: Divert new submits only; leave all existing streaming/news rows in Podcast forever.
- **ALT-004**: **Rejection Reason**: Corpus already contains news stations, movies, and TV as Podcasts — product semantics stay wrong; migration tools are required (incremental, not big-bang).

### Separate search indexes per content kind

- **ALT-005**: **Description**: Four Azure Search indexes, federated query in Worker.
- **ALT-006**: **Rejection Reason**: Multiplies **50 MB Free-tier quota** consumption ([search storage impact](../catalogue-content-types-search-storage-impact.md)); duplicates facet plumbing; rejected in favour of unified index (ADR-0003).

## Implementation Notes

- **IMP-001**: Phase 1 adds bicep containers + models + repositories — **no production writes**.
- **IMP-002**: Submit v2 classifies URL → kind before persist; feature flag routes streaming to new handlers ([catalogue-content-types-epic.md](../catalogue-content-types-epic.md) Phase 3).
- **IMP-003**: Auth: same **`submit`** / **`curate`** permissions initially ([auth0-roles-and-permissions.md](../../../website/cultpodcasts/docs/auth0-roles-and-permissions.md)); split roles deferred.
- **IMP-004**: Sign off partition keys and container names in Phase 0 before bicep change (unique keys for outlet/show names TBD in scraper ADR outline).
- **IMP-005**: Migration tools (Phase 5): dry-run identify → curated `--apply`; build per family as containers land; search index **swap** on move ([storage impact](../catalogue-content-types-search-storage-impact.md)).

## References

- **REF-001**: [catalogue-content-types-epic.md](../catalogue-content-types-epic.md)
- **REF-002**: [ADR-0003](./0003-unified-playable-search-document.md)
- **REF-003**: [website submit-url-flows.md](../../../website/cultpodcasts/docs/submit-url-flows.md)
- **REF-004**: [catalogue-content-types-search-storage-impact.md](../catalogue-content-types-search-storage-impact.md)
