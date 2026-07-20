# Search index slimming plan

Planning deliverable only. No production writes, deploys, or source-code changes beyond this document.

Related prior notes (descoped then revived): `docs/migration/concrete-migration-plan-and-cutover-strategy.md` (`CompactSearchRecord`), `docs/migration/implementation-checklist-mapped-to-repo.md` Phase 4, `docs/migration/sequenced-pr-plan.md` PR2/PR3.

---

## 1. Hard scope boundary

**In scope — only Azure Search document / search-result JSON shapes**

| Layer | Artifact |
|-------|----------|
| Index schema + push model | `Class-Libraries/RedditPodcastPoster.Search/EpisodeSearchRecord.cs` |
| Cosmos → Search projection | `Console-Apps/CreateSearchIndex/CreateSearchIndexProcessor.cs` (`CreateDataSource` SELECT aliases) |
| Incremental document upload | `Class-Libraries/RedditPodcastPoster.EntitySearchIndexer/Extensions/PodcastEpisodeExtensions.cs` (`ToEpisodeSearchRecord`) |
| Description length constant | `Class-Libraries/RedditPodcastPoster.Search/Constants.cs` (`DescriptionSize = 230`) |
| Public search HTTP | Cloudflare Worker `POST /search` → Azure Search docs API (`Api/src/search.ts`, `c.env.apihost`) |
| Worker search consumers | `Api/src/getPageDetails.ts` (OData filter + field reads on search hit), leech stub in `search.ts`, search-log field sniffing in `searchLogCollector.ts` |
| Angular search contracts | `SearchResult` / shared search-shaped fields on `HomepageEpisode` **only where fed by `/search`**, OData query builders, search templates |

**Explicitly out of scope — do not rename or reshape**

| Contract | Why |
|----------|-----|
| Cosmos `Episode` / `Podcast` documents | Canonical store; keep `urls.*`, `spotifyId`, `youTubeId`, `appleId`, etc. |
| Domain models / non-search API DTOs | Episode CRUD, podcast APIs, discovery, people, social posting |
| Homepage publish JSON (`HomepagePublisher` / R2 homepage) | Separate pipeline; keep human-readable keys |
| Discovery result JSON | Not Azure Search |
| Bookmarks / curator episode lists from Functions API | Not Azure Search documents |
| KV shortener metadata shape (`ShortnerRecord`) | Not the search index (Worker may still *read* search hits as a fallback in `getPageDetails`) |

Canonical platform URLs remain on Cosmos/domain/non-search APIs. URL omission and client/server reconstruction apply **only** to search documents and search-result JSON.

---

## 2. Current search document shape

Schema source: `EpisodeSearchRecord` (FieldBuilder + camelCase). Datasource SQL projects the same camelCase names. Push path: `ToEpisodeSearchRecord`.

| Field (JSON) | .NET type | Searchable | Filterable | Sortable | Facetable | Retrievable | Notes |
|--------------|-----------|------------|------------|----------|-----------|-------------|-------|
| `id` | string (key) | | ✓ | | | ✓ | Episode GUID |
| `episodeTitle` | string | ✓ (en.lucene) | | | | ✓ | |
| `podcastName` | string | ✓ | ✓ | | ✓ | ✓ | Facet + filter |
| `episodeDescription` | string | ✓ | | | | ✓ | Truncated to **230** chars |
| `release` | DateTimeOffset | | | ✓ | | ✓ | `orderby` |
| `duration` | string | | | | | ✓ | `TimeSpan.ToString()` |
| `explicit` | bool | | | | | ✓ | **Confirmed removal:** no search consumer reads it |
| `spotify` | string | | | | | ✓ | Full URL or `""` |
| `apple` | string | | | | | ✓ | Full URL or `""` |
| `youtube` | string | | | | | ✓ | Full URL or `""` |
| `bbc` | string | | | | | ✓ | Full URL or `""` — **not ID-derivable** |
| `internetArchive` | string | | | | | ✓ | Full URL or `""` — **not ID-derivable** |
| `subjects` | string[] | ✓ | ✓ | | ✓ | ✓ | Facet + filter |
| `podcastSearchTerms` | string | ✓ | | | | **hidden** | Inverted index only |
| `episodeSearchTerms` | string | ✓ | | | | **hidden** | Inverted index only |
| `image` | string | | | | | ✓ | Prefer YT → Spotify → Apple → other |
| `lang` | string? | | ✓ | | ✓ | ✓ | Subject page defaults to `lang eq null` (= English/unset; see §3D) |

No vector fields today.

**Description truncation:** `Constants.DescriptionSize = 230` in both datasource `SUBSTRING(..., 0, 230)` and `ToEpisodeSearchRecord`.

**Config:** `searchIndex:IndexName` = `cultpodcasts`, `IndexerName` = `cultpodcasts-indexer` (bicep `Infrastructure/functions.bicep`). Worker `apihost` points at the Azure Search **docs search URL for that index**.

---

## 3. What actually counts toward the 50 MB quota

### 3.1 Authoritative behavior (Azure AI Search)

Sources:

- [Search indexes — physical structure and size](https://learn.microsoft.com/azure/search/search-what-is-an-index#physical-structure-and-size)
- [Create an index — field attributes](https://learn.microsoft.com/azure/search/search-how-to-create-search-index#configure-field-definitions)
- [Estimate capacity](https://learn.microsoft.com/azure/search/search-capacity-planning)
- [Troubleshoot storage metrics](https://learn.microsoft.com/azure/search/troubleshoot-storage-metrics)
- [Serverless cost optimization — schema tips](https://learn.microsoft.com/azure/search/serverless-cost-optimization#reduce-compute-costs-through-optimization)
- [Performance tips — attributes and index size](https://learn.microsoft.com/azure/search/search-performance-tips#index-size-and-schema)

Findings:

1. **Reported `storageSize` is the on-disk footprint of internal index structures**, not the byte length of HTTP JSON upload/query payloads. Microsoft: physical structure is an internal implementation; size depends on document quantity/composition, field attributes, and index configuration (analyzers/suggesters/vectors).

2. **Pretty-printed JSON whitespace, indentation, braces, commas, and repeated property-name characters in the request body do not become persistent index storage.** They affect only network/request size and client/SDK serialization cost. After ingest, content is tokenized / copied into Lucene-like structures keyed by schema fields.

3. **Shortening JSON field names is not a primary lever for `storageSize`.** Field names live mainly in the **schema** (once per index), not as repeated JSON keys on disk per document. Microsoft never lists “shorter property names” as a capacity technique. Do **not** treat “chars saved in key names × document count” as quota savings.

4. **Value content and attributes drive storage:**
   - **Searchable** → inverted index / tokenized structures (extra space).
   - **Filterable / sortable / facetable** → additional storage for non-tokenized values (can multiply cost; MS: filtering/sorting/faceting can roughly quadruple storage vs minimal attribution).
   - **Retrievable / stored return values** → needed to return display fields; MS notes toggling retrievable alone is nuanced (`retrievable=true` “doesn’t cause increase” in create-index docs, while serverless guidance says `retrievable=false` on filter/sort-only fields can reduce on-disk storage). For URL-only display fields, **removing or shrinking the string value** is what frees quota.
   - **Hidden searchable fields** (`podcastSearchTerms`, `episodeSearchTerms`) still consume inverted-index space even though clients never see them.
   - **Vectors** — none in this index.
   - **Dual indexes** on one service **both** count toward the same service storage quota.

5. **Capacity planning guidance:** there are no solid heuristics; measure by building a sample index and extrapolating ([capacity planning](https://learn.microsoft.com/azure/search/search-capacity-planning)).

### 3.2 How this codebase serializes uploads

| Path | Serialization |
|------|----------------|
| `SearchClient.MergeOrUploadDocumentsAsync` (`EpisodeSearchIndexerService`) | Azure.Search.Documents SDK; default compact JSON (**no** `WriteIndented`) |
| Index FieldBuilder (`CreateSearchIndexProcessor.CreateIndex`) | `JsonObjectSerializer` with `PropertyNamingPolicy = CamelCase` only — **not** indented |
| Cosmos DB indexer | Service-side projection from Cosmos SQL → Search; not our pretty-printed JSON files |

Indentation is **not** enabled on search uploads. Even if it were, it would be transport-only relative to `storageSize`.

### 3.3 Corrected savings model (do not use wire-JSON bytes as quota)

| Change | Effect on **wire/JSON** | Effect on **reported index storage** | Priority for 50 MB |
|--------|-------------------------|--------------------------------------|--------------------|
| Remove/replace long URL **values** with short IDs | Large | **High** — fewer/shorter stored string values + less inverted content if ever searchable (they are not) | **Primary** |
| Keep/tighten description at 230 (or lower) | Medium | **High** — searchable field; tokens + stored text scale with length | **Primary** (already done) |
| Omit empty platform strings / unused fields | Small–medium | **Medium** — fewer stored values | Secondary |
| Shorten key names (`episodeTitle` → `t`) | Small per doc on wire | **Negligible / unproven for quota** | Optional DX/contract; not the quota fix |
| Disable unnecessary filterable/facetable/searchable | n/a | **High** if over-attributed | No removable query attributes found; `lang` facetability is required for planned subject-search facets |
| Dual live indexes during migration | n/a | **Critical risk** — sum of both `storageSize`s | See §8 |

**Rough value-side estimates (illustrative only — verify with index statistics after a sample rebuild):**

Assuming ~N documents and typical presence of Spotify/YouTube/Apple URLs:

| Value change | Approx. chars removed per doc (when present) | Nature |
|--------------|-----------------------------------------------|--------|
| Spotify URL → `spotifyId` value (~22 chars) | ~25–35 | Retrievable string shrink |
| YouTube URL → `youtubeId` value (~11 chars) | ~30–40 | Retrievable string shrink |
| Apple URL → `appleId` + `podcastAppleId` values | ~40–90 vs full URL | Retrievable string shrink |
| Empty `""` URLs omitted | few bytes each | Avoid storing empties |

These are **value** reductions. Multiplying by N gives an *upper-bound intuition* for stored content, not a guarantee equal to Δ`storageSize` (inverted indexes, attribute overhead, and merge lag make size nondeterministic).

**Key renaming is dropped.** Keep the existing descriptive field names; shorter keys have no meaningful persistent-storage benefit and would create avoidable consumer migration work.

---

## 3A. Measured savings for existing episodes (LIVE DATA)

Measured **2026-07-17** against the production Free-tier service `cultpodcasts` (uksouth, RG `Cultpodcasts`), index `cultpodcasts`, via read-only Search REST (`/stats`, `/docs/search`). No writes.

### Current index (authoritative, Get Index Statistics)

| Metric | Value |
|--------|-------|
| `documentCount` | **82,252** |
| `storageSize` | **51,462,953 bytes ≈ 49.08 MB** |
| `vectorIndexSize` | 0 |
| Tier | **Free (~50 MB hard cap)** |

**The index is essentially at the cap** (~98% of 50 MB). Slimming is not optional headroom work — it is required to keep indexing.

### URL field population + stored value size (FULL scan, all 82,252 docs)

| Field | Docs populated | % of docs | Total chars (≈bytes) | Avg len |
|-------|----------------|-----------|----------------------|---------|
| `spotify` (URL) | 29,536 | 35.9% | 1,624,810 | 55 |
| `apple` (URL) | 28,972 | 35.2% | 3,074,899 | 106.1 |
| `youtube` (URL) | 67,685 | 82.3% | 2,910,500 | 43 |
| `bbc` (keep) | 268 | 0.3% | 14,979 | 55.9 |
| `internetArchive` (keep) | 171 | 0.2% | 12,923 | 75.6 |

**Gross Spotify+Apple+YouTube URL value bytes = 7,610,209 ≈ 7.26 MB (~14.8% of storageSize).**

### ID components (do URLs need new fields? YES)

`spotifyId` / `youTubeId` / `appleId` are **not** in the search index today (only in Cosmos). Dropping URLs therefore requires **adding** compact id fields → net savings = URL bytes − new id bytes.

Component lengths (1,000-doc sample, highly consistent):

| Platform | Stored URL avg | Reconstruct from | New id chars/doc | Net save/doc |
|----------|----------------|------------------|------------------|--------------|
| Spotify | 55 | `spotifyId` (22) | 22 | **33** |
| YouTube | 43 | `youtubeId` (11) | 11 | **32** |
| Apple | 106.1 | `appleId`(13) + `podcastAppleId`(9.97≈10) | 23 | **~83** |

Apple URLs are 523/525 slugged, 2/525 slugless. Slug is **droppable** — `https://podcasts.apple.com/podcast/id{podcastAppleId}?i={appleId}` resolves via Apple redirect.

### Net storage savings (value-content, index-wide)

| Change | Gross bytes removed | New id bytes added | **Net bytes saved** | Net MB | % of storageSize |
|--------|--------------------|--------------------|---------------------|--------|------------------|
| Spotify URL → `spotifyId` | 1,624,810 | 649,792 | 975,018 | 0.93 | 1.9% |
| YouTube URL → `youtubeId` | 2,910,500 | 744,535 | 2,165,965 | 2.07 | 4.2% |
| Apple URL → `appleId`+`podcastAppleId` | 3,074,899 | 666,356 | 2,408,543 | 2.30 | 4.7% |
| **Total** | **7,610,209** | **2,060,683** | **5,549,526** | **≈5.29 MB** | **≈10.3%** |

Keep `bbc` (0.014 MB) and `internetArchive` (0.012 MB) — not id-derivable, negligible.

### Confidence & method

- **Value-byte measurement: HIGH.** Full scan of 100% of documents for population + total chars; id lengths from a consistent 1,000-doc sample (spotify=22, youtube=11, apple i=13/id≈10 with near-zero variance).
- **Translation to actual `storageSize` delta: MEDIUM.** These URL fields are **retrievable-only** (not searchable/filterable/facetable), so their footprint is ≈ stored value bytes plus modest per-field encoding overhead. Azure AI Search may compress stored values and reports size nondeterministically (merge lag 24–72h), so the realized `storageSize` drop could be **somewhat less** than the raw ~5.29 MB. Treat ~5.3 MB as a solid working estimate / near-upper-bound for the retrievable-value portion.
- **Recommended verification:** build a v2 sample/side index (see §8) and compare `storageSize` empirically before decommissioning the old index.

### Bottom line for existing episodes

- Removing the three derivable URL sets and adding compact ids nets **≈5.3 MB (~10% of the index)** — moving from ~49.1 MB to roughly **~43.8 MB**, buying real headroom under the 50 MB cap.
- **YouTube is the single biggest win** (82% populated → ~2.07 MB net), then Apple (~2.30 MB), then Spotify (~0.93 MB).
- **JSON indentation:** does **not** affect persistent index size — uploads here are compact anyway, and whitespace is transport-only ([evidence in §3.1](#31-authoritative-behavior-azure-ai-search)).
- **Key renaming:** dropped; it has negligible persistent-storage value compared with URL/value reduction.

---

## 3B. Unused / removable data audit (LIVE DATA, full scan)

Measured **2026-07-17**, full read-only scan of all **82,252** documents (same method as §3A). Consumer audit covers Angular webapp, Cloudflare Worker, Azure Functions API, and console tools that *query* the index.

### Stored-value inventory (context)

| Field | Docs populated | Total chars (≈bytes) | Avg len |
|-------|----------------|----------------------|---------|
| `episodeDescription` | 76,770 | **16,359,422 (~15.6 MB)** | 213.1 |
| `episodeTitle` | 82,252 | 4,951,811 (~4.7 MB) | 60.2 |
| `image` | 80,482 | 4,412,240 (~4.2 MB) | ~55 |
| `subjects` | 73,640 (96,580 elems) | 1,701,362 (~1.6 MB) | — |
| `podcastName` | 82,252 | 1,612,061 (~1.5 MB) | 19.6 |
| `duration` | 82,252 | 824,720 (~0.79 MB) | 10 |
| `lang` | 5,715 | 11,432 | 2 |
| `explicit` | true on 6,476 | bool | — |
| URLs (§3A) | — | 7,610,209 (~7.26 MB) | — |

Retrievable values ≈ 37.5 MB of the 49.1 MB `storageSize`; the rest is inverted-index / filter / facet structures and overhead.

### Per-field verdicts

| Field | Attributes | Consumers found | Verdict | Est. saved |
|-------|------------|-----------------|---------|-----------|
| `id` | key, filterable | Webapp routing/links; `episode.service.ts` + Worker `getPageDetails.ts` filters `id eq`; Functions `DeleteDocumentsAsync("id", …)` (`EpisodeHandler`, `PodcastHandler`) | **Keep** | — |
| `episodeTitle` | searchable | Search/podcast/subject templates; `episode-links.component.ts` share; Worker `getPageDetails`/`searchLogCollector`; console `AddSubjectToSearchMatches` select | **Keep** | — |
| `podcastName` | searchable+filterable+facetable | Facets/filters in all 3 list pages; `search.in(podcastName…)`; templates; Worker logs | **Keep** (all attributes used) | — |
| `episodeDescription` | searchable | Displayed on search/podcast/subject cards; console select | **Keep** — biggest single field (~15.6 MB, ~32% of index). Optional: cut `DescriptionSize` 230→180 ≈ **~2.4 MB** further (user decision; UX trade-off) | (optional ~2.4 MB) |
| `release` | sortable | `orderby: release asc/desc` everywhere; card dates; Worker page details | **Keep** | — |
| `duration` | retrievable-only | Cards display it — but every consumer strips the fractional tail (`duration.split(".")[0]`, `episode-links`, Worker `ShortnerRecord`) | **Trim value**: emit without `.0000000` suffix | **166,704 B ≈ 0.16 MB** |
| `explicit` | retrievable-only | **No search consumer.** Webapp reads `explicit` only via non-search episode API (edit/add dialogs). `SearchResult.explicit` declared but never read; Worker `ISearchResult.explicit` optional, only leech stub writes it | **Unused — drop from index** | ~small (bool ×82k, <0.1 MB) + simpler contract |
| `spotify`/`apple`/`youtube` | retrievable-only | Card link icons (via reconstruction post-change) | **Replace with ids** (§3A) | **≈5.29 MB net** |
| `bbc`, `internetArchive` | retrievable-only | `episode-links` / `episode-image` on search cards | **Keep** (not derivable; 0.027 MB combined) | — |
| `subjects` | searchable+filterable+facetable | Facets, `subjects/any(…)` filters, `app-subjects` display | **Keep** (all attributes used) | — |
| `podcastSearchTerms` | searchable, **hidden** | Search ranking only (by design) | **Keep** — already retrievable=false, cost is inverted-index only (not measurable via docs API) | — |
| `episodeSearchTerms` | searchable, **hidden** | Same | **Keep** | — |
| `image` | retrievable-only | `episode-image.component` | **Derive YouTube thumbnails** (below) | **≈3.18 MB net** |
| `lang` | filterable+facetable | Current filter: `subject-api` `lang eq null`; **faceting is confirmed planned/intended for subject searches**. Never displayed as a document property | **Keep filterable=true + facetable=true; set retrievable=false.** Facet values/counts come from `@search.facets`, so document retrieval is not required | Small stored-value saving only |

### Surprise find: `image` is mostly derivable

**There is exactly ONE image field in the index: `image`** (retrievable-only `Edm.String`), confirmed in the live schema, `EpisodeSearchRecord.Image`, and the Angular `HomepageEpisode.image`. It holds a single best-choice episode artwork URL selected at index time with preference YouTube → Spotify → Apple → other (`ToEpisodeSearchRecord` / datasource `e.images.youtube ?? e.images.spotify ?? e.images.apple ?? e.images.other`). There are **no** hidden/duplicate podcast-artwork, thumbnail, or social-image fields.

Full scan of `image` vs `youtube`:

- **64,412 / 80,482 images (80%)** are exactly `https://i.ytimg.com/vi/{videoId}/{variant}.jpg`
- **64,406 (99.99%)** of those have `{videoId}` equal to the doc's own YouTube id → derivable from `youtubeId` alone
- Variants: `maxresdefault` 53,882 · `sddefault` 7,098 · `hqdefault` 3,432
- YT-thumb chars stored: **3,307,304 (~3.15 MB)**; non-YT images: 16,070 docs, 1,104,936 chars (keep as URL)

Host/origin breakdown (rescanned later same day — live index drifts ~1% between scans as hourly indexing runs):

| Host | Docs | Chars | Origin |
|------|------|-------|--------|
| `i.ytimg.com` | 66,381 | 3,413,113 | YouTube thumbnail — **derivable from `youtubeId`** |
| `i.scdn.co` | 12,708 | 813,312 | Spotify CDN — opaque hash, **not derivable** from `spotifyId` |
| `is1-ssl.mzstatic.com` | 641 | 93,101 | Apple artwork — not derivable |
| `archive.org` | 101 | 18,154 | IA — keep |
| `ichef.bbci.co.uk` | 91 | 5,155 | BBC — keep |

Proposal: keep `image` only for non-YT images; for YT thumbs store a small `youtubeImageVariant` value (`maxresdefault` / `sddefault` / `hqdefault`, or a compact enum value) or adopt client-side fallback (`maxresdefault` → `hqdefault`). Net saving remains approximately **3.18 MB**. The webapp already inspects `i.ytimg.com` hosts for crop handling (`episode-image.component.ts`), so the reconstruction pattern is established.

### Audit totals (beyond §3A's ~5.29 MB URL savings)

| Item | Net saved |
|------|-----------|
| YT-thumbnail `image` derivation | **~3.18 MB** |
| `duration` fractional-tail trim | **~0.16 MB** |
| Drop `explicit` | <0.1 MB |
| `lang` retrievable=false (filterable+facetable retained) | Small; likely KB-scale |
| **Audit total** | **≈3.4 MB** |
| **Combined with URL removal (§3A)** | **≈8.7 MB ≈ 17–18% of the 49.1 MB index → ~40.4 MB** |
| Optional description 230→180 | +~2.4 MB more (~38 MB total) |

---

## 3C. Attribute-level audit (live schema × every consumer)

Live schema flags pulled read-only from `GET /indexes/cultpodcasts` (2026-07-17). Consumers cross-referenced: Angular (`search-api`, `podcast-api`, `subject-api`, `episode.service.ts`), Cloudflare Worker (`search.ts`, `getPageDetails.ts`, `searchLogCollector.ts`), Azure Functions API (`EpisodeHandler`/`PodcastHandler` — write/delete only, no queries), console tools (`AddSubjectToSearchMatches`).

Observed query surface (exhaustive):

- **$orderby:** `release asc` / `release desc` only.
- **Facets currently observed:** `podcastName,count:…` and `subjects,count:…`. **Planned/intended:** `lang` facets for subject searches.
- **$filter:** `podcastName eq` · `search.in(podcastName,…)` · `subjects/any(s: s eq …)` · `subjects/any(s: search.in(…))` · `id eq` · `lang eq null`.
- **search:** simple queryType, no `searchFields` restriction → all searchable fields participate.
- No suggesters, no scoring profiles, no vector fields (index config confirms).

| Field | searchable | filterable | sortable | facetable | retrievable | Verdict |
|-------|-----------|-----------|----------|-----------|-------------|---------|
| `id` | — | ✓ **used** (`id eq`: episode.service, Worker getPageDetails) | — | — | ✓ used (routing/links, delete-by-key) | Keep as is |
| `episodeTitle` | ✓ **used** (free-text) | — | — | — | ✓ used (cards, share, Worker) | Keep as is |
| `podcastName` | ✓ used | ✓ **used** (eq + search.in) | — | ✓ **used** (facet) | ✓ used | Keep as is — fully utilised |
| `episodeDescription` | ✓ used | — | — | — | ✓ used (cards) | Keep; only lever is shorter text (inverted index scales with the 15.6 MB of text, not with attributes) |
| `release` | — | — | ✓ **used** (every orderby) | — | ✓ used (card dates) | Keep as is |
| `duration` | — | — | — | — | ✓ used | Keep; trim value tail (§3B) |
| `explicit` | — | — | — | — | ✗ **never read** | **Drop field** (§3B) |
| `spotify`/`apple`/`youtube` | — | — | — | — | ✓ (via reconstruction) | **Replace with ids** (§3A) |
| `bbc`, `internetArchive` | — | — | — | — | ✓ used | Keep |
| `subjects` | ✓ used | ✓ **used** (`subjects/any`) | — | ✓ **used** (facet) | ✓ used (chips) | Keep as is — fully utilised |
| `podcastSearchTerms` | ✓ used (ranking) | — | — | — | already `false` | Keep as is |
| `episodeSearchTerms` | ✓ used (ranking) | — | — | — | already `false` | Keep as is |
| `image` | — | — | — | — | ✓ used | Derive YT thumbs (§3B) |
| `lang` | — | ✓ **used** (`lang eq null`) | — | ✓ **required** (planned subject-search facet) | ✗ **never displayed from a document** | **Keep filterable=true + facetable=true; set retrievable=false** |

### Attribute changes available

No filterable, sortable, facetable, or searchable flag should be removed. The only attribute-level change is **`lang.retrievable=false`**: it must remain filterable and facetable, but facet values/counts are returned in `@search.facets` independently of document retrieval. Field-level changes remain: drop `explicit`, replace derivable service URLs with IDs, derive YouTube thumbnails, and trim the duration tail.

Notable non-findings (schema is already tight):

- No field is sortable except `release`, which every page orderby uses.
- No facetable flags beyond `podcastName`, `subjects` (currently used) and `lang` (required for planned subject-search facets).
- All searchable fields are intended free-text targets (standard `en.lucene`, no ngram/edgeNgram analyzers, no suggesters — so no analyzer bloat to remove).
- The .NET SDK FieldBuilder defaults meant unset attributes are already `false`, so there is no accidental "everything filterable" REST-default bloat.

### Honest quantification caveat

Disabling `facetable`/`filterable` would remove internal forward/facet structures, but none can be disabled because all are current or confirmed intended requirements. Their size is **not observable via the docs API**. Setting `lang.retrievable=false` may save its small stored-value representation (2-char codes on 5,715 / 82,252 docs), likely only KB-scale; retain its facet structures. The material, quantified savings remain URLs (~5.29 MB net, §3A), YouTube-thumbnail derivation (~3.18 MB, §3B), duration tail (~0.16 MB), and optionally shorter descriptions (~2.4 MB per 50-char reduction). Key renaming is **dropped** (no storage benefit, §3.3).

---

## 3D. Subject-search language facet (implementation requirement)

**Scope:** Angular subject search OData query/UI only (`subject-api.component.ts` + its template). No Cosmos, API DTO, homepage, discovery, or non-search contract changes.

### Current behavior (code + live index, 2026-07-17)

Subject page (`website/cultpodcasts/src/app/subject-api/subject-api.component.ts`):

- Hard-coded `langFilter = ' and lang eq null'` always appended to the subject filter.
- Facets requested today: `podcastName` and `subjects` only — **no language selector**.
- Effect: subject results are restricted to documents with **no `lang` value**.

Live index distribution (`@search.facets` on `lang` + count filters):

| Bucket | Docs | Notes |
|--------|------|-------|
| `lang eq null` | **76,569** (~93%) | Dominant bucket |
| Non-null `lang` | **5,683** | ISO-ish codes only |
| `lang eq ''` | **0** | Empty string unused |
| `lang eq 'en'` / `en-GB` / `en-US` | **0** | Explicit English codes are **not stored** |

Top non-null values: `es` 3948, `pt` 581, `fr` 478, `cs` 324, `de` 140, `it` 58, … (no English variants).

**Semantics:** In this product, English/default is represented by **`lang` absent (`null`)**, not by `en`. Curator UI reinforces this: podcast language options exclude `en` (`PODCAST_DEFAULT_LANGUAGE_EXCLUDED_CODES`), and episode dialogs use `unset` → `"No Language"`. Datasource projects `e.lang ?? e.podcastLanguage as lang`. Push mapper (`ToEpisodeSearchRecord`) currently **omits `Lang` entirely** — fix that in the slim-index implementation so incremental uploads match the indexer (`episode.Language ?? podcast.Language`).

Therefore today's `lang eq null` clause is **already the English-only default**, by convention: it keeps unset/default-English docs and excludes explicit non-English codes. It does **not** mean “unknown language” as a separate concept in live data.

### Required default English-only filter

Exact OData fragment (matches current production behavior and live values):

```text
 and lang eq null
```

Full subject English-default example:

```text
subjects/any(s: s eq 'SUBJECT') and lang eq null
```

Do **not** use `lang eq 'en'` — that matches **zero** documents today.

### Null / empty / unknown treatment (recommended + open UX choice)

| Value | Live presence | Recommended treatment |
|-------|---------------|----------------------|
| `null` (missing) | 76,569 | Treat as **English (default)** — default selected facet |
| `''` empty | 0 | Treat same as null if it ever appears: include in English filter via `(lang eq null or lang eq '')` only if empty starts appearing |
| Explicit `en` / `en-*` | 0 | Not used today. **Open UX choice:** if curators later store explicit English codes, expand English filter to `(lang eq null or lang eq 'en' or search.in(lang, 'en-GB,en-US', ','))` — defer until needed |
| Other codes (`es`, `pt`, …) | 5,683 | Distinct language options in the selector |

Recommended label for the null bucket in the UI: **“English”** (not “Unknown”), matching product convention. Flag for product confirmation: whether null-only should forever mean English, or whether an eventual explicit-`en` migration is desired (out of scope for index slimming storage work).

### Facet request, selector, and `$filter` updates

Add `lang` to the subject page facet list (same style as podcast chips):

```text
facets: [
  "podcastName,count:1000,sort:count",
  "subjects,count:10,sort:count",
  "lang,count:50,sort:count"
]
```

**Important Azure Search nuance:** `@search.facets.lang` returns **only non-null values**. English (`null`) will **not** appear as a facet bucket. The UI must synthesize an English option:

1. Always show **English** first; selected by default; count = subject-scoped total with `lang eq null` (separate count, or `subjectTotal − Σ(non-null lang facet counts)` when no other lang filter is applied).
2. Show remaining options from `@search.facets.lang` (value + count), using display names from the existing `/languages` map where available.
3. Optional third control: **All languages** (clear `langFilter`).

When selection changes, rebuild `langFilter` only (subject + podcast filters unchanged):

| Selection | `langFilter` |
|-----------|--------------|
| English (default) | ` and lang eq null` |
| Single non-English code `es` | ` and lang eq 'es'` |
| Multiple non-English codes | ` and search.in(lang, 'es,fr', ',')` (pick a delimiter consistent with podcast chips) |
| English + one or more codes | ` and (lang eq null or search.in(lang, 'es,fr', ','))` |
| All languages | `` (empty) |

Reset page to 1 and re-run search (same pattern as `podcastsChange`).

### Retrievable?

**Keep `retrievable=false`.** Facet buckets supply codes and counts; the selector does not need per-document `lang` on each hit. Only flip to `retrievable=true` if a future UI shows language on each result card (not required now).

### Confined to search query/result behavior

| In scope | Out of scope |
|----------|--------------|
| Subject-page OData `$filter` / `facets` | Cosmos `Episode.lang` / `Podcast.lang` semantics |
| Search index field attributes for `lang` | Episode/podcast edit dialogs (already use API DTOs) |
| Search-result TypeScript shape only if needed for facet state | Homepage JSON, discovery, Worker non-search routes |

---

## 4. Proposed slim search schema (existing keys retained)

**No key renames.** Existing descriptive keys remain unchanged unless the value itself changes from a URL to an ID.

| Field | Type / attributes | Purpose / decision |
|-------|-------------------|--------------------|
| `id` | string key, filterable, retrievable | Unchanged |
| `episodeTitle` | searchable, retrievable | Unchanged |
| `podcastName` | searchable, filterable, facetable, retrievable | Unchanged |
| `episodeDescription` | searchable, retrievable | Keep 230 chars initially |
| `release` | sortable, retrievable | Unchanged |
| `duration` | retrievable string | Emit without fractional `.0000000` tail |
| `explicit` | — | **Drop (confirmed)** |
| `subjects` | searchable, filterable, facetable, retrievable | Unchanged |
| `lang` | filterable=true, facetable=true, retrievable=false | Keep filtering and planned subject-search facets; document value need not be returned |
| `spotifyId` | retrievable string | Replaces full `spotify` URL |
| `youtubeId` | retrievable string | Replaces full `youtube` URL |
| `appleId` + `podcastAppleId` | retrievable strings | Replace full `apple` URL; slug omitted |
| `bbc` | retrievable string | Keep full URL; not derivable |
| `internetArchive` | retrievable string | Keep full URL; not derivable |
| `image` | retrievable string? | Store only non-YouTube image URLs; omit for derivable YT thumbnails |
| `youtubeImageVariant` (optional) | retrievable string/enum | Preserve YT thumbnail variant only if fallback behavior is insufficient |
| `podcastSearchTerms` | searchable, retrievable=false | Unchanged |
| `episodeSearchTerms` | searchable, retrievable=false | Unchanged |

Prefer omitting null/empty IDs and `image` rather than writing `""`.

### 4.1 URL derivability (search reconstruction only)

| Platform | Cosmos IDs | Canonical reconstruction | Edge cases |
|----------|------------|--------------------------|------------|
| Spotify | `Episode.SpotifyId` | `https://open.spotify.com/episode/{spotifyId}` | Matches codebase (`SpotifySearcher`, fixtures). |
| YouTube | `Episode.YouTubeId` | `https://www.youtube.com/watch?v={youtubeId}` | Code uses watch URLs (`SearchResultExtensions.ToYouTubeUrl`). Shorts/`youtu.be` may exist in Cosmos URLs; watch URL is acceptable for links. |
| Apple | `Episode.AppleId` + podcast Apple id | `https://podcasts.apple.com/podcast/id{podcastAppleId}?i={appleId}` | Slug/`/us/` optional; Apple redirects. **Podcast Apple id is on `Podcast.AppleId`, not denormalized on Episode today.** Indexer push path can read podcast; Cosmos datasource SQL needs it extracted from `urls.apple` at index time (no Cosmos model change). |
| BBC / IA | No compact ids | Keep full URLs in search doc | Client uses them on search cards. |
| Image | — | Keep non-YT URL; derive YT thumbnail from `youtubeId` (plus optional variant) | Spotify/Apple CDN URLs are not derivable. |

Do **not** strip `urls` from Cosmos.

### 4.2 Datasource / push changes (search only)

- `CreateDataSource` SELECT: project `e.spotifyId as spotifyId`, `e.youTubeId as youtubeId`, `e.appleId as appleId`, plus `podcastAppleId`; stop projecting `e.urls.spotify|apple|youtube`.
- Keep `e.urls.bbc` / `e.urls.internetArchive` under their existing field names.
- `ToEpisodeSearchRecord`: map IDs from episode (+ podcast Apple id); do not copy Spotify/YouTube/Apple URL strings into the search document.

---

## 5. Client / query strategy (locked decision)

### 5.1 Recommendation: retain existing OData field names

**Do not rename keys and do not build an OData field-name translation layer.** Existing filters, orderings, facets, and displayed properties retain their descriptive names. Only the service-link representation changes from full URLs to ID properties, `explicit` disappears, and YouTube `image` values become derivable.

| Option | Verdict |
|--------|---------|
| **(A) Retain descriptive field names** | **Chosen.** No persistent-storage benefit justifies key renaming. |
| **(B) Rename keys and update consumers** | **Rejected.** Adds contract churn for negligible quota benefit. |
| **(C) API translates long ↔ short** | **Rejected.** Unnecessary and fragile across filters, lambdas, ordering, facets, Worker page details, and logs. |

Worker `POST /search` remains a **pass-through** to Azure Search (plus logging / leech stub). Existing OData field strings remain unchanged.

### 5.2 In-scope Angular / Worker touch list

**OData query sites (keep existing names; verify unchanged):**

| File | Today |
|------|--------|
| `website/cultpodcasts/src/app/search-api/search-api.component.ts` | `release`, `podcastName`, `subjects` facets/filters |
| `website/cultpodcasts/src/app/podcast-api/podcast-api.component.ts` | `podcastName eq`, `release`, `subjects` |
| `website/cultpodcasts/src/app/subject-api/subject-api.component.ts` | `subjects/any`, default `lang eq null`, add `lang` facet + selector (§3D), `podcastName` facet/filter, `release` |
| `website/cultpodcasts/src/app/episode.service.ts` | `podcastName` + `id` filter, `release desc` |

**Search-result models / templates (search path only):**

| File | Notes |
|------|--------|
| `search-result.interface.ts` | Drop `explicit`; replace URL properties with `spotifyId`, `youtubeId`, `appleId`, `podcastAppleId`; `image` becomes optional |
| Prefer **splitting** `SearchResult` from `HomepageEpisode` | Homepage JSON keeps full URLs; search results use IDs without changing unrelated contracts |
| `search-results-facets.interface.ts` | Keep existing `podcastName` / `subjects`; add `lang` support when the planned subject-search facet ships |
| `search-api` / `podcast-api` / `subject-api` `*.html` | Existing descriptive result keys remain unchanged |
| `episode-image` / `episode-links` when given **search** results | Reconstruct Spotify/YouTube/Apple URLs from IDs; derive YT thumbnail from `youtubeId`; keep `bbc`, `internetArchive`, and non-YT `image` |
| `podcast-episode` / `podcast` components that load via `/search` | Search-shaped fields only |

**Worker (search-shaped only):**

| File | Change |
|------|--------|
| `Api/src/getPageDetails.ts` | No field-name changes (`podcastName`, `id`, `episodeTitle`, `release`, `duration` stay) |
| `Api/src/search.ts` leech stub | Remove `explicit`; align any service-link fields with ID shape |
| `Api/src/searchLogCollector.ts` | No field-name renames; tolerate ID-shaped service links if logged |
| `Api/src/ISearchResult.ts` | Drop `explicit`; align service-link/image properties with the slim search JSON |

**Out of scope:** edit/add episode dialogs, `api-episode.interface`, discovery UI, homepage templates fed by homepage API, and curator episode APIs that are not Azure Search.

### 5.3 URL reconstruction location (search results)

Reconstruct in the **webapp**. Prefer a single helper, e.g. `searchResultLinks(spotifyId, youtubeId, appleId, podcastAppleId)`.

---

## 6. Backend implementation notes (search-only code)

1. Slim `EpisodeSearchRecord` while retaining descriptive names: drop `Explicit`, replace service URL properties with ID properties, make `Image` optional/non-YT only, trim `Duration`, and make `Lang` non-retrievable while retaining filtering+faceting.
2. Update `CreateDataSource` projection + `ToEpisodeSearchRecord`.
3. Populate `podcastAppleId` only in the search projection: parse it from `urls.apple` for the Cosmos datasource, and map it from the podcast in the push path. **Do not add it to the Episode/Cosmos model.**
4. Console tools selecting existing names (for example `episodeDescription`) require no rename changes; verify they tolerate the dropped/replaced fields.
5. `SearchDocument.cs` (older helper) — update or leave unused; do not ripple into non-search models.

Azure Functions **do not** proxy public search; they write the index (`EpisodeSearchIndexerService`) and run the indexer (`SearchIndexHandler`). Public queries: Worker → Azure Search.

---

## 7. Reindexing mechanics (this repo)

| Mechanism | Role |
|-----------|------|
| `Console-Apps/CreateSearchIndex` | Create/teardown index, datasource, indexer; run indexer with retries; Cosmos episode counts / duplicate fingerprint |
| Azure Search indexer + Cosmos datasource | High-watermark on `e._ts`; filter active episodes |
| `EpisodeSearchIndexerService` | `MergeOrUploadDocumentsAsync` after episode mutations |
| `searchIndex:*` app settings | `Url`, `Key`, `IndexName`, `IndexerName` |
| Worker `apihost` | Full Azure Search search URL including **index name** |

The field removals/additions and `lang.retrievable` change require a **new index** (several field definitions are immutable). Pattern: create new index + new datasource/indexer names → populate → flip config → delete old.

---

## 8. Deployment plan (minimal interruption)

### 8.1 Dual-index storage risk (critical)

Service `storageSize` is **summed across all indexes**. Free/Basic ~50 MB is shared.

If current index is already near the cap, **creating a second full copy will fail or force eviction**. Measure first:

- Portal: Search service → Usage / Indexes size  
- Or `GET .../servicestats` / index statistics (`storageSize`)

| Situation | Strategy |
|-----------|----------|
| `current + estimatedNew < quota` (with headroom for merge overhead) | Blue/green: build new alongside old |
| Not enough headroom | Temporary SKU bump for migration, **or** delete-old-then-rebuild (downtime = reindex duration) |
| Near cap even for one slim index | Value reduction is mandatory; key renaming will not save the tier |

Rebuilds temporarily inflate size (delete+insert until merges finish, often 24–72h) — [storage metrics](https://learn.microsoft.com/azure/search/troubleshoot-storage-metrics).

### 8.2 Dual-read / dual-write feasibility

| Pattern | Feasible today? |
|---------|-----------------|
| Dual-write to two indexes | **No** without new code (single `SearchClient` / `IndexName`) |
| Dual-read in webapp (old+new schemas) | Possible but unnecessary; keep descriptive field names and cut over atomically |
| Feature flag on index name | Yes via `searchIndex__IndexName` + Worker `apihost` |
| Index alias | Azure Search supports aliases; optional cutover aid — not used in repo today |

**Recommended:** blue/green with **atomic cutover** of writers + readers + webapp; no long dual-compat window.

### 8.3 Ordered rollout (preferred when dual-index fits)

1. **Measure** current `storageSize` and document count; estimate slim size (sample index on a non-prod service if possible).
2. **Ship code** that can build/populate the slim schema (console + libraries), without flipping prod `IndexName` yet.
3. **Create** `cultpodcasts-v2` (name TBD) + datasource + indexer **without** tearing down `cultpodcasts` (`CreateSearchIndex` without `--teardown-index`).
4. **Populate** via `--run-indexer` on the **new** indexer until doc counts match filter expectations.
5. **Prepare packages:** api-infra + indexer-infra with slim `EpisodeSearchRecord` (IDs not URLs, no `explicit`, trimmed duration, non-YT `image`, `lang` non-retrievable); webapp with URL/image reconstruction helpers + subject language facet (§3D); Worker search consumers updated for ID fields / dropped `explicit`.
6. **Cutover window (seconds–minutes):**
   1. Set function app `searchIndex__IndexName` (and `IndexerName`) to v2 (bicep/app settings — not local deploy scripts).
   2. Deploy api-infra + indexer-infra packages that emit slim docs.
   3. Point Worker `apihost` at the v2 index search URL.
   4. Deploy Angular PWA (`wrangler pages deploy`).
   5. Trigger indexer once on v2 to catch `_ts` delta since step 4.
7. **Verify:** search, podcast, subject (English-default `lang eq null` + language facet selector), episode deep link / `pagedetails`, podcast facets, platform link icons, BBC/IA when present. Confirm non-search APIs unchanged.
8. **Soak**, then **delete** old index + old indexer/datasource to reclaim quota.
9. **Rollback:** keep old index until soak passes; revert `apihost` + `IndexName` + previous function packages + previous Pages deploy.

**Outage estimate:** with dual-index headroom, user-visible cutover can be **near-zero to ~1–2 minutes** (config + deploys). Without headroom, outage ≈ **full reindex time** after deleting the old index (plus deploy), unless SKU is temporarily raised.

### 8.4 Deploy order across repos

| Order | Component | Action |
|-------|-----------|--------|
| 0 | Azure Search | Create+fill v2 (console), old still serving |
| 1 | `api-infra` + `indexer-infra` | Slim schema writers + IndexName=v2 |
| 2 | Cloudflare Worker `Api` | `apihost` → v2; align search hit fields (IDs, drop `explicit`) |
| 3 | `website/cultpodcasts` | URL/image reconstruction + subject language facet |
| 4 | Azure Search | Delete old index when stable |

Homepage / discovery / episode CRUD deploys are **unrelated** and must not change contracts for this work.

### 8.5 Compatibility shim?

**Not recommended.** Blue/green via **new index name + coordinated webapp/Worker** is enough. Field names stay descriptive; only value shapes change (URLs→IDs, drop `explicit`, derive YT images).

---

## 9. Phased implementation sequence

| Phase | Work |
|-------|------|
| P0 | Measure prod `storageSize`; decide dual-index vs SKU bump vs downtime rebuild |
| P1 | Slim `EpisodeSearchRecord` + datasource SQL + `ToEpisodeSearchRecord` (map `Lang`); URL→ID; drop `explicit`; trim duration; derive YT images; `lang` retrievable=false |
| P2 | Angular search-result models: ID reconstruction helpers; subject language facet/selector (§3D); keep existing OData field names |
| P3 | Worker search consumers (`getPageDetails`, logs, leech) — drop `explicit`, accept IDs |
| P4 | Create/populate v2 index; cutover; verify; decommission old |
| P5 | Optional: further description trim — measure again |

---

## 10. Open questions for the user

1. Current Azure Search SKU and **exact `storageSize` / quota** (dual-index possible?). *(Measured Free ~49.08 MB / 50 MB — still need dual-index headroom decision.)*
2. Approve temporary **SKU bump** for blue/green if under 50 MB headroom fails?
3. **`podcastAppleId`:** confirm parse-from-`urls.apple` at index time only (no Cosmos Episode field)?
4. Keep optional Apple slug in reconstruction, or omit (recommended: omit)?
5. ~~Keep `explicit`?~~ **Resolved: drop.**
6. New index name (`cultpodcasts-v2`?) and whether to use an **index alias** for cutover.
7. Target description length: stay at **230** or reduce further after URL value wins?
8. Confirm homepage / discovery / Cosmos URL fields remain untouched (assumed yes per scope).
9. **Language UX (§3D):** label null as **“English”** (recommended)? If explicit `en`/`en-*` codes are introduced later, expand the English filter accordingly?
10. Subject language selector: single-select only, or multi-select (English + other codes)?

---

## 11. Summary — final recommended changes (locked)

| Change | Decision |
|--------|----------|
| Drop `explicit` | **Yes (confirmed)** |
| Derive Spotify / YouTube / Apple URLs from IDs | **Yes** — store `spotifyId`, `youtubeId`, `appleId`, `podcastAppleId` |
| Derive YouTube-thumbnail `image` | **Yes** — keep non-YT image URLs only |
| Trim `duration` fractional `.0000000` tail | **Yes** |
| `lang` attributes | **filterable=true, facetable=true, retrievable=false** |
| Subject language facet | **Yes** — default English = `lang eq null` (§3D) |
| Key renaming / short keys | **No** — dropped (no storage benefit) |
| API OData translation layer | **No** |
| Non-search contracts | **Unchanged** |

- **Biggest quota wins:** platform URL→ID (~5.29 MB), YT image derivation (~3.18 MB), duration trim (~0.16 MB); `explicit` drop and `lang.retrievable=false` are small.
- **Queries:** keep descriptive OData field names; update webapp for ID reconstruction + subject language selector; Worker pass-through only.
- **Cutover:** new index → populate → flip `IndexName` + `apihost` + webapp together → delete old; **watch dual-index quota**.