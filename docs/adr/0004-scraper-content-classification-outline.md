---
title: "ADR-0004: Scraper content classification rules (outline — not signed off)"
status: "Proposed"
date: "2026-09-03"
authors: "Catalogue platform (planning)"
tags: ["architecture", "submit", "scrapers", "content-types"]
supersedes: ""
superseded_by: ""
---

# ADR-0004: Scraper content classification rules (outline)

## Status

**Outline only** — Phase 0 placeholder. **Do not implement** classification handlers until rules are validated against live scrapers and product sign-off.

> Full ADR to be completed before Phase 3 (submit/lookup v2). This document captures decision space and open rules only.

## Context

**CTX-001**: Submit v2 must classify URLs **before** persist to TvShow, Movie, or News containers ([ADR-0002](./0002-separate-catalogue-content-containers.md)).

**CTX-002**: Today, streaming URLs create **Podcast + Episode** via scraped show metadata (`NonPodcastShowNameResolver`).

**CTX-003**: Canonical scraper cases live in `StreamingScraperCanonicalCases` / UrlSubmission.Tests.

## Decision (pending)

**DEC-001 (draft)**: Classification order:

1. **Podcast-service** (Spotify / Apple / YouTube **episode**) → **Episode** (unchanged).
2. **News** (new matcher, **not** `BBCUrlMatcher.IsSubmitUrl`) → **NewsOrganisation + NewsReport**.
3. **Standalone feature** (Netflix/Prime/Vimeo film metadata) → **Movie**.
4. **Series episode** (BBC iPlayer programme, Netflix/Prime series watch URL) → **TvShow + TvShowEpisode**.
5. **YouTube entertainment / show** → **Podcast + Episode** (not TvShow).
6. **YouTube news-station** (migration / future submit) → **NewsOrganisation + NewsReport**.

## Open classification rules (must resolve before acceptance)

| ID | Rule | Options |
|----|------|---------|
| **CLS-001** | Single-episode iPlayer series | TvShow with one TvShowEpisode **vs** Movie |
| **CLS-002** | Netflix `/title/` catalogue page | TvShow stub **vs** reject until episode URL |
| **CLS-003** | Vimeo — always Movie **vs** heuristic by duration/series metadata | |
| **CLS-004** | News outlet resolution | Scrape publisher **vs** curator pick **vs** domain map |
| **CLS-005** | Ambiguous attach | Same 409 / picker pattern as podcast name attach, per parent kind |
| **CLS-006** | News-station **YouTube** Podcasts | Migrate → NewsOrganisation (carve-out); entertainment YouTube stays Podcast |

## Implementation Notes (future)

- **IMP-001**: Lookup response generalizes `podcastName` hint → `parentName` + `contentKind` hint ([submit-url-flows.md](../../../website/cultpodcasts/docs/submit-url-flows.md)).
- **IMP-002**: Feature flag gates classifier — fallback remains Podcast+Episode.
- **IMP-003**: Live probe tests (`StreamingScraperCanonicalUrlProbe`) inform rule table — not production writes.

## References

- **REF-001**: [catalogue-content-types-epic.md](../catalogue-content-types-epic.md) § Submit URL flows
- **REF-002**: [ADR-0002](./0002-separate-catalogue-content-containers.md)
- **REF-003**: `Class-Libraries/RedditPodcastPoster.BBC/Matching/BBCUrlMatcher.cs`
