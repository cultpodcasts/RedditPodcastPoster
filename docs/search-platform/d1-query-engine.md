# D1 search query engine (technical)

Prototype: `cultpodcasts/Api` → `search-demo/`. Target: production search Worker that the
api Worker reaches over a **service binding** (not a public hostname — zone Bot Fight Mode
challenges server POSTs).

## Contract (drop-in for Azure AI Search)

`POST /indexes/{index}/docs/search?api-version=2024-07-01`

Body fields the website / api already send:

| Field | Behaviour |
|-------|-----------|
| `search` | Azure **simple** syntax (not Lucene `full`) |
| `searchMode` | `any` (OR) / `all` (AND) |
| `queryType` | `simple` only in production traffic |
| `filter` | OData subset: `eq`/`ne`/`and`/`or`, `search.in`, `subjects/any`, grouping |
| `orderby` | `release asc\|desc`, or rank when query present |
| `top` / `skip` / `count` | pagination + `@odata.count` |
| `facets` | `seriesName`/`podcastName`, `subjects`, `lang`, `contentKind` |
| `select` | subset projection |

Auth: shared `api-key` header (same pattern as today’s Azure key), planned for preview+prod
secrets parity.

## Simple-syntax translator (`query.ts`)

| Input | Output |
|-------|--------|
| `"a b"` | phrase |
| `+` / `\|` / `-term` | AND / OR / NOT-at-boundary |
| `term*` | prefix |
| `AND` / `OR` / `NOT` | **plain terms, not operators** (Azure simple has no keywords; then stopword-dropped) |
| empty / stopwords-only / lone `-` | match-none (`1=0`) |
| `*` | browse / match-all |

### en.lucene query-side mirroring

Production Azure fields use analyzer **`en.lucene`**. D1 FTS is rebuilt with
`tokenize='porter unicode61'`. Query side additionally:

- Drop Lucene English stopwords from **bare** terms (keep inside phrases)
- Split bare terms on hyphens/slashes (`ex-mormon` → mode-joined atoms)
- Possessive expansion: `heaven's` → `("heaven's" OR "heaven")`

Measured miss rate (Azure hits absent from D1) on full corpus free-text set:
**17.87% → 11.93% (porter alone) → 0.01%** (porter + query mirroring). See
[parity-evidence.md](./parity-evidence.md).

## Analyzer / index notes

- External-content FTS5 table + AI/AD/AU triggers (column contract must stay identical
  across rebuilds).
- **Diacritics:** D1 folds by default; Azure does not. Prefer keeping fold (better UX);
  document as intentional enhancement.
- Live Azure index (2026-11-08): **no** custom analyzers, suggesters, scoring profiles,
  synonym maps, or semantic config — stock `en.lucene` + default BM25 only. Advanced Azure
  features are unused; fuzzy (`~`) is the main unused capability (website never sends
  `queryType=full`).

## Response shape deltas (fix in SP-0.2)

| Field | Azure | D1 today |
|-------|-------|----------|
| Document keys | compact search record | same + extra `podcastName` |
| `release` | `…Z` | `….000Z` milliseconds |
| `@odata.context` | stripped to null by api Worker | null |
| `@search.highlights` | optional | not yet Azure-shaped (FTS5 `snippet`/`highlight` available) |

## Ranking

- Default free-text: FTS5 `bm25()` / `rank`. Top-10 overlap with Azure is weak (0–7/10) —
  tune per-column weights (SP-1.8).
- Filter browse: `ORDER BY release DESC` — **exact id sequence** vs Azure on podcast/subject
  pages (verified). Add `id` as secondary key for same-timestamp ties (SP-1.7).
- Post-cutover enhancements: recency blend, `NEAR` proximity boost
  ([../search-platform-epic.md](../search-platform-epic.md) §6A).

## Errors

| Condition | HTTP |
|-----------|------|
| Bad filter / body syntax | **400** + Azure-like `{ error: { code, message } }` |
| D1 overload / timeout / runtime | **503** (retryable) — do **not** collapse to 400 |

The api Worker proxy must forward upstream status + body text (SP-1.5); today it returns
`c.json(response.body, 400)` which yields empty `{}` for clients.

## Field map quirks

- `podcastName` → `series_name` in D1 FIELD_MAP (tolerates stale clients Azure 400s).
- English language: Azure stores `lang` null and omits the facet bucket; D1 may materialise
  `en` — intentional; align if drop-in facet shape is required.
