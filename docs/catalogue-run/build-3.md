# Build 3 — Submit / lookup v2

**Status:** open — [gate-2.md](./gate-2.md) is done.

## Done when

Classify / lookup / flag code is **merged to `main`**.

## Checklist (when unlocked)

- [x] Scraper classification validated — film extractors that already detect `IsMovie` set `MadeAsFilm`; series pages keep a show name; rules covered by `SubmitContentClassifierRules` and Netflix fixture extract
- [x] Lookup across containers + feature flag — `submitContentTypes:Enabled` (default false). On: Film, TvShowEpisode, and NewsReport membership plus `contentKind` / `parentName`
- [x] Flagged submit to Film / TV / News — `CatalogueKindSubmitter` when the flag is on. Off: Podcast + Episode
- [x] Merged to `main`

## When Build 3 is done

Open **only** [gate-3.md](./gate-3.md).
