# GATE 4 — UI live

**Status:** done — 2026-09-28. Worker + Site from Build 4 are live. Empty Film/TV/News hubs are expected until GATE 5 migrate. Functions were not required for this gate (`ShortnerService` still writes unprefixed podcast keys).

## Steps (when unlocked)

| # | Do this |
|---|---------|
| 1 | **DEPLOY** Worker + Site (+ Functions only if APIs changed) |
| 2 | Redirects ready |
| 3 | Curator smoke Film / TV / News |

### Log

| Step | Result |
|------|--------|
| 1 Worker | Live. Cloudflare Workers Builds `api` commit `2e2e613` (#160), 2026-09-28T20:24Z, outcome success. |
| 1 Site | Live. `https://cultpodcasts.com/tv/Nightly` title `Nightly \| Cult Podcasts`, empty hub. `/news/Desk` same. Homepage and search work. Kind facet hidden until search has more than one `contentKind`. |
| 1 Functions | Skipped. Not needed to light empty kind routes. Say `deploy functions & clis` when Indexer/Api should encode prefixed shorts. |
| 2 Redirects | Site `redirectMovedKind` is on `/podcast/:name/:query`. `shortner` Worker last changed 2025-10-30; existing podcast shorts stay unprefixed. Prefixed KV keys appear when a Film/TV/News short is written (GATE 5). |
| 3 Curator smoke | Pre-corpus: hubs and `/film/` routes render; no Film/TV/News search hits. Kind smoke of real titles is GATE 5 after migrate. |

## When GATE 4 is done

Open **only** [build-5.md](./build-5.md).
