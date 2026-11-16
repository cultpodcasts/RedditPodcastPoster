# Azure ↔ D1 parity evidence

Sources: Api `search-demo/harness/` (synthetic), Workers Logs production replay
(2026-10-31 → 2026-11-07), live ordered-page checks, live diacritics A/B. Narrative also
in epic §4C / §4C.1 / §4C.2.

## Analyzer A/B (full corpus free-text set)

| Configuration | Azure hits missed by D1 |
|---------------|-------------------------|
| `unicode61` FTS, literal query terms | 15,738 / 88,080 (**17.87%**) |
| `porter unicode61` FTS | 10,508 / 88,080 (**11.93%**) |
| porter + en.lucene query mirroring (**shipped**) | **8 / 88,080 (0.01%)** |

The one flagged harness case (`l. ron hubbard`) is an **Azure artefact**: ~100-char
description truncation mints single-letter `l` tokens mid-URL; D1 indexes full text and
correctly does not match. Confirmed via Azure highlights API.

## Production log replay

| Window | Distinct shapes | In-corpus misses |
|--------|----------------:|-----------------:|
| Day 2026-11-06→07 | 4,027 | **0** |
| Week 2026-10-31→07 | 13,070 (from 24,680 requests) | **0** |

Week filter-family Azure-higher deltas: **3,442** cases, all exactly **+1** (2,953) or
**+2** (489). All 97 Azure-only ids checked → **absent from D1 corpus snapshot**
(freshness). Match-all `*`: Azure 89,255 vs D1 89,245 (10 docs).

### Traffic mix (week)

| Mode | Requests | Share |
|------|---------:|------:|
| Subject filter (browse, rails, lang) | 23,140 | 93.7% |
| Shortener fallback (`id eq`) | 1,170 | 4.7% |
| Free-text search | 370 | 1.5% |

### Family summary (week)

| Family | Cases | Counts agree | Notes |
|--------|------:|-------------:|-------|
| Related rail | 10,661 | 7,238 | all deltas freshness |
| Episode page (series+id) | 1,736 | 1,733 | freshness |
| Podcast browse | 122 | **122** | exact |
| Shortener / kind browse | 23 | **23** | exact |
| Free text | 36 | 7 | D1 superset; 1 Azure hit missing all week (not in corpus) |
| Query+filter pairs | 20 | 16 | 1 Azure 400 (`podcastName` bug) |

## Release sort

Live ordered comparison (identical POST bodies, `select: id,release`):

| Scenario | Result |
|----------|--------|
| Podcast browse release desc page 1 & 2 | identical id order |
| release asc | identical |
| Subject browse (Peoples Temple) | identical; counts 832=832 |
| Same-second tie series (worst) | same membership; within-tie shuffle — fix with secondary `id` sort |

## Diacritics (live)

| Query | Azure | D1 |
|-------|------:|---:|
| `ágora` / `agora` | 407 / 23 | 421 / 421 |
| `josé` / `jose` | 18 / 14 | 145 / 145 |
| `café` / `cafe` | 4 / 8 | 356 / 356 |

Azure does not fold; D1 does.

## Intentional divergences (accept or tune later)

| Topic | Azure | D1 | Disposition |
|-------|-------|-----|-------------|
| Free-text recall | Truncated descriptions | Full text (+ guests/terms) | **Decided 16 Nov 2026 (SP-0.3):** keep as benefit from soak day one; no strict-content soak. Guests also from day one (pulled forward from G5). |
| Lone `-phrase` under searchMode=any | Everything-except | Empty / AND-NOT | Keep documented; Azure is footgun |
| Stopwords inside phrases | Dropped | Kept | Low impact |
| Rank / `@search.score` | Lucene BM25 | FTS5 bm25 | Approximate via column weights; never bit-identical |
| `lang` facet English | Omitted (null) | May show `en` | Align if needed for drop-in |
| Diacritics | No fold | Fold | Prefer D1 |

## JSON shape

Envelope and document keys match for apihost swap modulo SP-0.2 nits (`podcastName`
extra, release `.000Z`, highlights). Forwarding D1 JSON through the api Worker works
today for browse/filter paths.

## Decisions recorded 16 November 2026 (10:14 GMT, Europe/London)

See [README.md](./README.md#decisions-16-november-2026-1014-gmt-europelondon). Short form:

- SP-0.3 closed: full descriptions **and guests** from soak day one (option A + guests from G5).
- Unquoted exact title / AKA phrase-first ranking is required before cutover (SP-1.8 / SP-G4). Quoted searches stay phrases.

## Post-epic gaps (after this 8 Nov 2026 evidence)

The harness and week replay did **not** cover later Azure fields or the phrase-first requirement:

- Film `alternativeTitles` (RPP #1431, Azure `standard.lucene`) — not in the November D1 schema.
- Retrievable identity ids (RPP #1437).
- Unquoted exact title / AKA ranked first (required before G4; add harness cases on SP-1.8).
- Guests were in the demo corpus as a measured benefit, but were originally a G5 enhancement; they are now required from soak day one.

Do not treat top-10 overlap with Azure as sufficient for cutover. Phrase-first ranking is a separate acceptance item.
