---
title: "ADR-0004: Scraper content classification rules (outline — not fully signed off)"
status: "Proposed"
date: "2026-09-03"
updated: "2026-09-23"
authors: "Catalogue platform (planning)"
tags: ["architecture", "submit", "scrapers", "content-types"]
supersedes: ""
superseded_by: ""
---

# ADR-0004: Scraper content classification rules (outline)

## Status

**Status:** Outline with **CLS-001…008 signed 2026-09-23**. Still validate against live scrapers before implementing Phase 3 handlers; do not treat sign-off as “ship without probes.”

| ID | Rule | Decision |
|----|------|----------|
| **CLS-001** | One-off / special vs series | **Signed** — single-episode → Film; miniseries / anthology / finite series → TvShow + TvShowEpisodes (epic OPEN-006) |
| **CLS-007** | Provider `Movie` / `IsMovie` → Film? | **Yes** when standalone made-as-film (epic OPEN-001); not when series |
| **CLS-002** | Netflix `/title/` catalogue page | **Reject** until episode/watch URL — no TvShow-only stub |
| **CLS-003** | Vimeo | Curator chooses when ambiguous; classifier suggests only |
| **CLS-004** | News outlet resolution | Scrape → name-attach; USA news-network name heuristics as assist; curator override |
| **CLS-005** | Multi-source series / ambiguous attach | Podcast-like parent name UI + 0/1/many attach rules (esp. TvShow from disparate channels) |
| **OPEN-001** | Film definition | Made-as-film one-off; cinema vs TV premiere **irrelevant** |

## Context

**CTX-001**: Submit v2 must classify URLs **before** persist ([ADR-0002](./0002-separate-catalogue-content-containers.md)).

**CTX-002**: Today streaming URLs create Podcast + Episode via scraped show metadata.

**CTX-003**: Canonical scraper cases live in `StreamingScraperCanonicalCases` / UrlSubmission.Tests. Many extractors expose **`IsMovie`** / JSON-LD `@type: Movie` — that is a **provider signal**, not the catalogue type name (**Film**).

**CTX-004**: Epic **OPEN-001 resolved** — Film = standalone work **made as a film**; where it premiered (cinema or one-off TV) does not matter. Examples: *Going Clear*, *My Scientology Movie*, *A Very British Cult*.

## Decision (draft — pending full acceptance)

**DEC-001 (draft)** classification order:

1. Podcast-service (Spotify / Apple / YouTube **episode**) → **Episode**.
2. News (new matcher, not `BBCUrlMatcher.IsSubmitUrl`) → **NewsOrganisation + NewsReport**.
3. Made-as-film one-off (provider film / one-off / special; OPEN-001) → **Film**.
4. Series episode (iPlayer programme, Netflix/Prime series watch URL) → **TvShow + TvShowEpisode**.
5. YouTube entertainment / show → **Podcast + Episode** (not TvShow).
6. YouTube news-station (migration / future submit) → **NewsOrganisation + NewsReport**.

## Open classification rules

| ID | Rule | Status |
|----|------|--------|
| **CLS-001** | Single-episode / one-off vs series | **Signed** — single-episode → Film; miniseries / anthology / finite or ongoing series → TvShow + episodes (OPEN-006) |
| **CLS-002** | Netflix `/title/` catalogue page | **Signed** — **reject** until episode/watch URL (no TvShow stub alone) |
| **CLS-003** | Vimeo — Film vs heuristic | **Signed** — curator chooses; classifier suggests when ambiguous; no auto-persist kind without confirm |
| **CLS-004** | News outlet resolution | **Signed** — scrape publisher/outlet → name-attach (podcast pattern); investigate USA news-network channel-name heuristics as assist; **curator can override** |
| **CLS-005** | Ambiguous / multi-source parent attach | **Signed** — same as podcast: 0→create, 1→attach, many→409/picker; **TvShow especially** needs podcast-like UI to see/choose **parent (series) name** when episodes arrive from different YouTube/Vimeo/BBC channels and build up one series |
| **CLS-006** | News-station YouTube Podcasts | Migrate → NewsOrganisation; entertainment stays Podcast — **signed** (epic S-008 allowlist for apply) |
| **CLS-007** | Provider `Movie` / `IsMovie` → catalogue Film? | **Signed with OPEN-001** — yes when standalone made-as-film; not when series |
| **CLS-008** | Miniseries / anthology / finite series | **Signed (OPEN-006)** — always TvShow + TvShowEpisodes, never Film |

## Implementation notes (future)

- Lookup response: `parentName` + `contentKind` hint; **Film** has title only (no parent).
- Feature flag gates classifier; fallback Podcast+Episode.
- Live probe tests inform the rule table — not production writes.
- Never persist product type **Movie**; map provider Movie → **Film** when made-as-film (OPEN-001).

## References

- [catalogue-content-types-epic.md](../catalogue-content-types-epic.md)
- [ADR-0002](./0002-separate-catalogue-content-containers.md)
- `Class-Libraries/RedditPodcastPoster.BBC/Matching/BBCUrlMatcher.cs`
