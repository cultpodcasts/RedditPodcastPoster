# Search platform epic — leave Azure AI Search Free tier

**Status:** Proposal / Phase 0 planning. No production, index, or code changes.
**Date:** 2026-11-07 (all vendor limits and prices re-verified on this date).
**Related:**
[search-platform-alternatives.md](./search-platform-alternatives.md) (2026-07 survey — still the wide-net reference),
[search-index-slimming-plan.md](./search-index-slimming-plan.md) (tactical slimming, now executed),
[catalogue-content-types-search-storage-impact.md](./catalogue-content-types-search-storage-impact.md) (contentKind growth), <!-- pragma: allowlist secret -->
[catalogue-content-types-epic.md](./catalogue-content-types-epic.md). <!-- pragma: allowlist secret -->

---

## 1. Problem statement

Azure AI Search Free tier (50 MB hard cap) is exhausted as a growth platform. The slimming
plan has already been executed — compact service IDs, `svc` packing, `explicit` dropped,
YouTube image derivation — and the index still ran out of room: <!-- pragma: allowlist secret -->

| Date | Corpus | Description cap | Index size |
|------|--------|-----------------|------------|
| 2026-07-17 | 82,252 docs | 230 chars | 49.08 MB (~98% of cap) |
| 2026-10-22 | 88,279 docs | 100 chars | 52.06 MB — **over cap**, indexing broken |
| 2026-10-22 | — | cut to 50 | recovered |
| today | ~88k+ docs | **80 chars** (`Constants.DescriptionSize`) | ~41.5 MB (~8.5 MB headroom) |

Searchable episode description is now 80 characters. Every future need makes this worse:

- **Guests** — `Playable.Guests` exists on the model but is **not projected into search at
  all** (confirmed: no reference in `RedditPodcastPoster.Search` or
  `RedditPodcastPoster.EntitySearchIndexer`). Indexing ~88k × guest names has no room. <!-- pragma: allowlist secret -->
- **Catalogue content types epic** — TvShowEpisode / Film / NewsReport rows, `seriesDescription`, <!-- pragma: allowlist secret -->
  and news-at-scale all add storage (see storage-impact doc).
- **Corpus growth** — ~6k docs added Jul→Oct; the cap is re-hit on a timescale of months
  even with no new features.

The next Azure tier (Basic, ~£50+/month) is **rejected on cost**. This epic selects a
replacement platform and a Cosmos-sync architecture.

## 2. Requirements

Functional non-negotiables (unchanged from the 2026-07 survey, verified against
`Api/src/search.ts`, `getPageDetails.ts`, and the Angular query builders):

1. Keyword search over title / description / series name / hidden search-terms — **plus
   guests (new)**.
2. Filters: `podcastName` (eq + `search.in`), `subjects` (array `any` + `search.in`), <!-- pragma: allowlist secret -->
   `lang eq null` (null = English), `id eq`, `contentKind`. <!-- pragma: allowlist secret -->
3. Sort by `release` asc/desc or relevance (BM25-ish score).
4. Facet counts: `podcastName` and `subjects` up to 1000 buckets; `lang`; `contentKind`. <!-- pragma: allowlist secret -->
5. Stable pagination (`skip`/`top`) + total count.
6. Public-site latency (Worker already edge-caches `POST /search` for 600 s).

New platform requirements from this epic:

7. **Restore description richness** — at least the old 230 chars; ideally no cap anxiety
   at all (500+ chars).
8. **Incremental sync without downtime** — add/update/remove individual documents while
   the index keeps serving. No teardown-rebuild for schema-compatible changes.
9. **Retire the hourly Cosmos → Search pull indexer.** Target model: **event-driven push <!-- pragma: allowlist secret -->
   on mutation (in-week amends) + weekly full reconcile** as the drift safety net.
10. Cost: £0/month strongly preferred; hard ceiling well below £50/month.

## 3. Candidate verdicts (2026-11-07)

| Candidate | Monthly cost | Functional fit | Verdict |
|-----------|-------------|----------------|---------|
| **Cloudflare Worker + D1 (SQLite FTS5)** | **£0 marginal** (account already on Workers Paid: 10 GB/db, 25 B row-reads/mo included; prod `apiDB` D1 already bound to the Worker) | Full, but **you build it**: FTS5 + SQL filters + `GROUP BY` facets + `COUNT` | **Recommended** |
| **Cloudflare AI Search** | Cheap (billing starts 2026-12-01; 1k full-text queries/mo included then $0.10/1k) | **Fails hard**: max 5 custom metadata fields; string arrays not indexed/filterable (kills `subjects`); no facet counts; filterable strings truncated at 64 bytes; file/chunk RAG model, not per-record; no total-count paging | **Reject** for catalog search | <!-- pragma: allowlist secret -->
| **Elastic on Azure — managed** (Elastic Cloud Hosted via Azure Native ISV) | **$99/mo (~£75) floor**, Standard tier | Excellent | **Reject on cost** (≈1.5× the refused Azure Basic) |
| **Elastic on Azure — serverless** (Azure Native Serverless project) | Usage-based; search VCU $0.09/hr — an always-warm public search endpoint plausibly $25–65+/mo, unpredictable | Excellent | **Reject** — not reliably under budget, cost is open-ended |
| **Elastic on Azure — self-host VM** | Cheapest B2s (2 vCPU/4 GB, ES minimum) ≈ $31.57/mo in cheapest US region, more in uksouth + disk | Excellent | **Reject on cost** — Azure VM pricing ≈ Azure Search Basic, plus full ops burden |
| **Elastic off Azure — managed** (Elastic Cloud on AWS/GCP) | Same $99/mo floor | Excellent | **Reject on cost** |
| **Elastic off Azure — self-host VPS** (Hetzner CX23 2 vCPU/4 GB €3.99/mo ≈ £4; Netcup, IONOS similar) | **~£4–8/mo** | Excellent (query DSL, aggregations = facets, alias-swap rebuilds, partial updates, delete-by-id) | **Viable runner-up** — cheapest "real Elasticsearch"; single-node ops (patching, TLS, backups, uptime) are yours |
| **OpenSearch managed — Aiven Free** | £0 (4 GB RAM / 20 GB disk; now DigitalOcean-only; idle power-off; no SLA; no security plugin; fixed region) | Excellent while running | **Benchmark sandbox only** — still not a production promise (unchanged from 2026-07) |
| **OpenSearch managed — Aiven Developer** | **$40/mo (~£30)** — new since the 2026-07 survey; single node 2 vCPU/4 GB/30 GB, 1-day backups, DigitalOcean only | Excellent | **Managed fallback under budget** if self-managing is rejected |
| **Meilisearch** (for completeness) | Cloud: no free tier yet ($23/mo ≈ £18 minimum; promised serverless free tier not shipped as of 2026-11). Self-host: same ~£4 VPS as ES, lighter | Excellent, facets/typo-tolerance native | Self-host variant interchangeable with ES on the VPS path |

### Why Cloudflare AI Search is rejected (detail)

It is a RAG product over file corpora (R2 objects, websites), not a structured-record
search engine. Verified limits (2026-11): **5 custom metadata fields per instance** (we
need podcastName, subjects, lang, contentKind, release — and subjects is a string array, <!-- pragma: allowlist secret -->
which Vectorize **stores but does not index or filter**); filters match only the **first
64 UTF-8 bytes** of a string; there is **no facet-count aggregation** at all; `boost_by`
is a ranking nudge, not a deterministic `ORDER BY release`; results are chunks of files,
without the OData-style total count + skip/top contract the Angular infinite scroll uses.
Full-text query pricing ($0.10/1k after 1k/mo included) is not the problem — the product
shape is. It could later *complement* catalog search as an "ask the catalogue" RAG
feature, but it cannot replace it.

### Why Elastic everywhere fails the budget

Managed Elastic has a **$99/month floor** on every cloud, including the Azure Native ISV
service (same hosted product, billed through Azure). Serverless Elastic prices by VCU-hour
and storage; a public site keeps the search tier warm around the clock, so the bill is
both material and unpredictable. Self-hosting needs ES's practical minimum of a 4 GB-RAM
node: that node costs ~£25–35/month on Azure but **~£4/month off Azure** (Hetzner CX23
€3.99). "On Azure" buys nothing here — the sync path is HTTPS push either way (§5), and
the query path originates at the Cloudflare Worker, not inside Azure.

## 3A. Prior art: the 2023 D1 FTS5 experiment ("queries took ~30 s")

D1 FTS5 **was tried before** — it is the Api Worker's origin story. Commit `db4d221`
(2023-09-17) created `migrations/0000_full-text-search.sql` plus an `/api/episode_search`
route; a .NET-side process populated the database from a local SQLite dump
(`create-sql-dump.cmd`). It was removed in PR #7 (2024-02-17) after queries took ~30
seconds versus milliseconds on Azure Search. That experience does **not** transfer to the
design in this epic, for three compounding reasons:

1. **It ran on D1 alpha.** September 2023 predates D1's ground-up storage-backend rewrite
   and its April 2024 GA. Cloudflare replaced the alpha architecture precisely because of
   its per-query latency. 30 s is also exactly D1's max-query-duration ceiling — the
   queries were timing out, not merely slow.
2. **The FTS table was joined on an unindexed TEXT GUID.** The schema was a contentless <!-- pragma: allowlist secret -->
   `fts5(Guid, Title, Description, YouTube, Spotify, Apple, …)` joined <!-- pragma: allowlist secret -->
   `ON t.Guid = e.Guid` to a base table from a raw `sqlite3 .dump` with **no index on
   `Guid`** (and GUIDs + full URLs were tokenised into the FTS index as searchable text).
3. **The query had no `LIMIT`.** A common term matched tens of thousands of documents — <!-- pragma: allowlist secret -->
   every one joined twice and serialised into a single JSON response.

**Measured (2026-11-07):** rebuilding that exact schema + query on today's engine next to
the proposed design, same 90,000-document corpus (see §4A): the 2023 shape takes
**121 ms / 346 ms / 1,531 ms** (rare / medium / common term) where the proposed shape
takes **6.5 / 16.6 / 57.8 ms** — and today's SQLite is actively rescuing the 2023 schema
by building an automatic covering index for the missing `Guid` index on every query
(`EXPLAIN QUERY PLAN: SEARCH e USING AUTOMATIC COVERING INDEX (Guid=?)`), which is why
even a single-match query reads all 90k rows. On the 2023 alpha backend, without that
mitigation, returning ~60k-row JSON bodies per query, 30 s is the expected outcome.

## 4. Recommendation

**Primary: Worker + D1 FTS5** — unchanged from the 2026-07 survey, now stronger. The
2023 attempt's ~30 s queries (§3A) were an artefact of D1 alpha plus an unindexed-join,
no-LIMIT schema; the executed benchmark (§4A) shows the proposed design answers in
**5–60 ms** on the same corpus size at full fidelity:

- **£0 marginal cost, forever.** The account is already on Workers Paid for the Api
  Worker (Browser Rendering, Durable Objects, Analytics Engine, a production D1 database).
  Included D1 quota (10 GB per database, 25 billion row-reads/month) dwarfs this workload
  by orders of magnitude. Cloudflare commits to D1 keeping a free plan; FTS5 is a
  supported extension (verified 2026-11).
- **Kills the cap anxiety permanently.** Full-fidelity projection (500-char descriptions
  + guests + contentKind, ~88k rows ≈ 70 MB raw + FTS index) lands around 150–250 MB — <!-- pragma: allowlist secret -->
  2–3% of one paid-plan database.
- **No new vendor, no new server, no new bill, no idle-shutdown clause.** The search
  query already terminates at this Worker; D1 is in-platform (no extra origin hop,
  unlike a VPS or Aiven).
- **The cost is engineering, not money:** an OData-subset parser (the observed filter
  grammar is small and closed — §2 item 2), facet queries (`GROUP BY` + KV-cached
  snapshots), and the response shaping (`value`, `@odata.count`, `@search.facets`) so
  Angular ships unchanged. Typo tolerance is weaker than Lucene/Meilisearch unless added
  later — acceptable; Azure `simple` query mode today isn't typo-tolerant either.

**Runner-up (if a "real search engine" is preferred over building the query layer):**
self-hosted **Elasticsearch — off Azure** on a ~£4/month Hetzner-class VPS, or managed
**Aiven OpenSearch Developer at ~£30/month** if nobody wants to own a box. Use the
**Aiven Free tier as the zero-cost benchmark environment** during Phase 0 regardless.

**Rejected:** Cloudflare AI Search (product shape), all managed Elastic (cost), ES
self-host on Azure VMs (cost ≈ the refused Azure Basic, plus ops).

## 4A. Phase 0 benchmark — executed 2026-11-07

Method: 90,000 full-fidelity documents (**500-char** descriptions, guests, hidden search
terms, subjects junction table, zipf-distributed vocabulary) loaded into a **local D1** <!-- pragma: allowlist secret -->
via `wrangler getPlatformProxy` — the same SQLite engine and D1 API as production
(production adds network RTT of a few ms per query; the Worker's 600 s edge cache sits on
top). Schema: `search_docs` base table (integer rowid, indexed `release`,
`series_name`, `lang`), `doc_subjects` junction, **external-content FTS5** over <!-- pragma: allowlist secret -->
title/series/description/guests/search-terms, kept in step by triggers. Medians of 5 runs:

| Query (proposed design) | Median | rows_read |
|---|---|---|
| Relevance search, worst-case term (**matches 75,139 docs = 83% of corpus**), `ORDER BY rank LIMIT 20` | **57.8 ms** | 39 |
| Total count for that term (`@odata.count`) | 9.7 ms | 75,139 |
| That term, `ORDER BY release DESC LIMIT 20` | 32.8 ms | 225,417 |
| Podcast facet over those matches (`GROUP BY`, 1000 buckets) | 46.9 ms | 225,646 |
| Subject facet over those matches (junction join) | 56.9 ms | 419,840 |
| Relevance search, medium term (13,221 matches) | 16.6 ms | 39 |
| Relevance search, rare term | 6.5 ms | 2 |
| Multi-term AND | 7.8 ms | 4 |
| Guest-name search | 6.8 ms | 0 |
| Podcast page (series filter, no FTS) | 6.0 ms | 2 |
| Subject page (junction + `lang IS NULL`) | 8.7 ms | 4,947 |
| Full-corpus podcast facet (browse) | 9.3 ms | 90,255 |
| Deep page (`LIMIT 20 OFFSET 980`) | 40.1 ms | 225,417 |
| **Sync:** single-doc upsert (FTS triggers fire) | 5.8 ms | 1 |
| **Sync:** single-doc delete | 11.0 ms | 2 |

Read-quota economics: the most expensive query (subject facet over a worst-case term)
reads ~420k rows; Workers Paid includes 25 **billion** row-reads/month ≈ 59,000 such
worst-case queries **per day** before any charge — and the edge cache absorbs most of
them. Bulk load ran at ~210 docs/s through the proxy including FTS triggers (a one-shot
full rebuild of the corpus is minutes, and the production path would use batched D1
writes from the reconcile job).

Real searches are nothing like the worst-case term: user queries are selective by nature.
The practical envelope is **5–20 ms** per query with facet sets at **40–60 ms**, versus
~30 s in the 2023 experiment (§3A) — the difference is GA D1 plus schema, not luck.

## 4B. Real-data run — executed 2026-11-07

The §4A synthetic run was repeated with the **real catalogue**: a fresh read-only
`CosmosDbDownloader` export (101,796 playables) projected through the production search
mapping (`ToEpisodeSearchRecord` semantics: removed/parent-removed exclusion, compact <!-- pragma: allowlist secret -->
image tokens, `svc` packing, compact ids) into **89,245 indexable documents** — 80,868
episodes, 8,338 news reports, 37 TV episodes, 2 films; 12,551 excluded as removed. <!-- pragma: allowlist secret -->
Descriptions were loaded **in full** (no cap): 101.5 M chars total vs the 6.5 M chars the
80-char Azure cap retains today (median description 824 chars, p90 2,574, max 50,896).
Guests and curator search terms included.

**Size (the headline):** the complete database is **239 MB** — base table 149.5 MB
(all retrievable fields: full description, compact image/svc, ids, duration), FTS5
inverted index ≈ 66 MB, junction + btree indexes ≈ 23 MB. That is **2.3 % of D1's
10 GB per-database limit** — a 42× headroom over everything Azure Search could not fit
into 50 MB, at £0 marginal cost.

Real-term query medians (same local-D1 method as §4A; full corpus, full descriptions):

| Query (real data, real terms) | Median | rows_read |
|---|---|---|
| `scientology` (6,658 matches), `ORDER BY rank LIMIT 20` + snippets | 10.7 ms | 39 |
| `jehovah witnesses` (11,959 matches), rank | 14.5 ms | 39 |
| `nxivm` (1,145), rank | 5.3 ms | 39 |
| `chad daybell` phrase, rank | 8.2 ms | 39 |
| Guest-name search (`vicky ward`, searchable guests — impossible on Azure today) | 4.6 ms | 22 |
| Prefix `scien*`, rank | 13.5 ms | 39 |
| `scientology`, `ORDER BY release DESC LIMIT 20` | 13.4 ms | 19,974 |
| Count (`@odata.count`) for `cult` (32k matches) | 4.3 ms | 32,463 |
| Browse: subject chip filter + recency page | 9.8 ms | 18,007 |
| Browse: series page (`seriesName` filter) | 3.9 ms | 20 |
| Browse: newest 20 overall | 3.3 ms | 20 |
| Facet: subject counts for `scientology` matches | 7.6 ms | 30,547 |
| Facet: corpus-wide subject counts | 7.8 ms | 107,181 |
| Facet: kind + lang over `cult` matches (worst observed) | 31.5 ms | 97,389 |
| **Sync:** single upsert / single delete (FTS triggers) | 8.1 / 5.5 ms | — |

FTS5 full rebuild + optimize over the whole corpus: **4.3 s** — a from-scratch weekly
reconcile is trivially cheap.

**Demo Worker:** `search-demo/` in the Api repo (branch `cursor/search-d1-demo-0b84`,
local `wrangler dev` only) serves this database with Azure-shaped responses —
`GET /search?q=…&kind=…&subject=…&series=…&lang=…&orderby=…&skip/top&facets=true`
returning `{ "@odata.count", value[], "@search.facets" }` with `<b>…</b>` snippet
highlighting. Measured end-to-end (HTTP + batched count/page/facet statements +
subject-chip hydration): 2–29 ms per request on the full real corpus. The backup,
corpus JSONL, and loaded D1 state stay off-repo (hidden curator search-term fields;
both repos are public).

The demo also serves an **Auth0-locked web UI** at `GET /` (search box, facet
sidebar, snippet highlighting, paging): the page signs in against the production
tenant with the website's SPA client, and `GET /search` validates the access token
and authorises on the `permissions[]` claim (`REQUIRED_PERMISSIONS` var, default
`curate`) — the same `@cfworker/jwt` model as the production api Worker. Browsable
from outside the dev box via a `cloudflared` quick tunnel (the per-run tunnel URL
must be added to the Auth0 SPA app's allowed callback/web-origin lists).

## 4C. Query-contract parity — executed 2026-11-07

The demo Worker (Api `search-demo/`, deployed on D1 `search-demo-db`, WEUR) now exposes an
**Azure-shaped `POST …/docs/search` endpoint**: same request body the api Worker forwards
today (`search`, `filter`, `facets`, `top`/`skip`/`count`/`orderby`, `queryType: simple`,
`searchMode: any`), same `api-key` header auth, same response envelope
(`@odata.count`, `@search.facets`, `value[]` with the production index field names).
Swapping `env.apihost` on the api Worker at the D1 engine is a config change, not a code change.

Two translators make that work:

- **Simple-syntax → FTS5 MATCH** (`search-demo/src/query.ts`): phrases (`"hannah w."`),
  `+` AND, `|` OR, `-`/`NOT` negation, trailing `*` prefix, bare terms OR'd under
  `searchMode=any` (AND under `all`). Atoms are emitted as quoted FTS5 strings, so user
  text cannot inject FTS5 syntax.
- **OData filter subset → SQL** (`search-demo/src/odata.ts`): exactly the grammar the
  Angular app sends — `subjects/any(s: s eq '…')`, `subjects/any(s: search.in(s, '…', '£'))`, <!-- pragma: allowlist secret -->
  `seriesName`/`podcastName`/`contentKind`/`lang`/`id` with `eq` and `search.in`,
  `lang eq null` (null = English), `and`/`or`, parentheses, `''` quote-doubling.
  Unsupported syntax → 400 with an Azure-style error envelope.

Measured against the **production Azure index** (read-only, same POST body to both):

| Query / filter | Azure | D1 | Verdict |
|---|---|---|---|
| `subjects/any(s: s eq 'Scientology')` + facets | 6,003 | 6,002 | Filter parity (1 doc newer than the backup); facet keys and top bucket identical (`XENU TV`, 644 = 644) | <!-- pragma: allowlist secret -->
| subject + `search.in(seriesName…)` + `search.in(contentKind…)` | 549 | 549 | Exact |
| `scientology+"paulette cooper"` | 35 | 74 | D1 ⊇ Azure (containment verified: 0 Azure hits missing); extras from full descriptions |
| `"Hannah W."` | 0 | 26 | Guests are not in the Azure index at all; D1 indexes them |
| `nxivm` / `"twin flames"` | 1,050 / 281 | 1,145 / 311 | D1 ⊇ Azure (0 missing on both) |
| `cult* -scientology` | 84,458 | 37,447 | **Known divergence**: with `searchMode=any` Azure treats `-` as *OR NOT* (docs matching `cult*` **or** simply lacking `scientology`); D1 does the intuitive *AND NOT*. Azure's behaviour is a documented footgun — keep D1 semantics, note in release comms |

Containment checks (full id-set, not top-10): every Azure hit was present in the D1 result
set for all sampled queries. The D1 engine is a strict superset wherever full descriptions
and guests add matches — which is the point of the epic.

Ranking is **not** yet parity-tuned: top-10 overlap is 1–6/10 because FTS5 `bm25()`
currently weights all columns equally while Azure weights fields. FTS5 accepts per-column
weights (`bm25(search_fts, w_title, w_series, w_desc, …)`) — tune during the dual-run
phase against real search-log click data.

### 4C.1 Analyzer parity — porter tokenizer + query-side en.lucene mirroring (2026-11-07)

A committed harness (Api `search-demo/harness/`) now replays every website OData
use-case plus a 42-query free-text corpus against both engines and classifies every
id-level difference. First run exposed the real gap: the production Azure index runs the
**`en.lucene` analyzer** on every searchable field — Porter stemming, possessive
stripping, English stopwords, hyphen splitting — while the D1 FTS5 index used the plain
`unicode61` tokenizer. Confirmed via the Azure highlights API (`universe` →
`<em>Universal</em>`, `jeffs` → `<em>Jeff</em>`, `dei` → `<em>dey</em>`).

Fix, measured A/B on the full 89,245-doc corpus (free-text query set, Azure hits missing
from D1):

| Configuration | Azure hits missed by D1 |
|---|---|
| `unicode61` FTS + literal query terms | 15,738 / 88,080 (17.87%) |
| `porter unicode61` FTS, same queries | 10,508 / 88,080 (11.93%) |
| porter FTS **+ query-side en.lucene mirroring** | **8 / 88,080 (0.01%)** |

Query-side mirroring (in the simple-syntax translator): drop Lucene English stopwords
from bare terms (kept inside phrases), split bare terms on hyphens/slashes into
mode-joined atoms (`ex-mormon` → `ex OR mormon`), expand possessives
(`heaven's` → `("heaven's" OR "heaven")`). The remote FTS table was rebuilt with
`tokenize='porter unicode61'` (10.5 s for the full corpus, online).

The one case still flagged (`l. ron hubbard`) is an **Azure artefact, not a D1 miss**:
Azure's ~100-char description truncation cuts words mid-URL and mints spurious
single-letter `l` tokens (`vicnews.com/l…` from `local-news`), which Azure then matches.
D1 indexes the full description where the word is whole. Remaining divergences to accept
or tune later: stopwords inside phrases (D1 keeps them, Azure drops them) and the OR-NOT
footgun already noted.

Operational finding: the zone's bot challenge (`cf-mitigated: challenge`) blocks
server-to-server POSTs to `search-demo.cultpodcasts.com`. For the real swap the api
Worker should reach the D1 search Worker over a **service binding** (direct
Worker-to-Worker, no public edge, no WAF, lower latency), not a public hostname. The
demo keeps `workers.dev` enabled for harness access.

### 4C.2 Production log replay — real traffic, day then week (2026-11-07)

The api Worker's structured search logs (Workers Logs observability API, exact
day-by-day distinct counts) were replayed with identical POST bodies against both
engines: one day (4,027 distinct request shapes) then a full week, 2026-10-31 →
2026-11-07 (13,070 shapes from 24,680 requests).

Traffic mix for the week: subject-filter queries 23,140 (93.7%), shortener fallback
1,170 (4.7%), free text 370 (1.5%) — filters, not syntax, are the parity-critical path.

Week result: **zero in-corpus misses in any family.** All 3,442 cases where Azure
counted higher differ by exactly +1 (2,953) or +2 (489); all 97 Azure-only ids behind
them are absent from the D1 corpus snapshot (indexed after export — the match-all query
measures the drift: 89,255 vs 89,245). Podcast browse, episode pages, kind browse and
shortener fallback agree exactly. Free text is a D1 superset with one Azure hit missing
across the whole week, itself not in the corpus.

Findings from real traffic:

1. **Live website bug:** the website sends
   `(contentKind eq 'NewsReport') and (podcastName eq 'Associated Press')`; production
   Azure 400s it (no `podcastName` field). The only error in 13,070 cases. The D1
   translator tolerates it (`podcastName` → `seriesName`).
2. **`AND` is not an operator in Azure simple syntax** — a real user's
   `scientology AND "paulette cooper"` returns the OR result on Azure (keywords are
   plain terms, then stopword-dropped). The D1 translator was corrected to match.
3. **Lone-NOT divergence seen in the wild:** `-"Davi Vieira"` → Azure 88,628
   (everything-except), D1 0. Kept as the documented intentional divergence.
4. Replay concurrency hit `D1 DB is overloaded`; the demo Worker now returns 503 for D1
   runtime errors (retryable) and reserves 400 for filter/body syntax, mirroring Azure.

## 5. Target sync architecture (backend-agnostic)

Today there are two write paths into Azure Search:

| Path | Mechanism | Fate |
|------|-----------|------|
| Hourly pull | Azure Search indexer, Cosmos datasource, `_ts` high-watermark (`SearchIndexHandler`, `CreateSearchIndex`) | **Retired** — it is an Azure Search feature and does not exist elsewhere | <!-- pragma: allowlist secret -->
| Push on mutation | `SearchIndexerService.MergeOrUploadDocumentsAsync` / `DeleteDocumentsAsync` after episode/podcast mutations | **Kept** — becomes the primary path, retargeted at the new backend | <!-- pragma: allowlist secret -->

Target model (user requirement: in-week amends + weekly reconcile, no index downtime):

1. **In-week amends (primary, event-driven).** Every existing push call site keeps its
   semantics — upsert on save, delete-by-id on removal — with the transport swapped:
   - **D1:** authenticated Worker admin route (service-to-service; Auth0 M2M or Worker
     secret — plan the production secret alongside preview per the parity rule).
     `INSERT … ON CONFLICT DO UPDATE` / `DELETE`, with the FTS5 table kept in step via an
     external-content table and triggers. Row upserts are atomic; the index never stops <!-- pragma: allowlist secret -->
     serving.
   - **ES/OpenSearch/Meili:** direct REST from the .NET indexer (`_doc` PUT / DELETE). <!-- pragma: allowlist secret -->
2. **Weekly reconcile (safety net).** Scheduled Functions timer (Indexer app) scans <!-- pragma: allowlist secret -->
   Cosmos (full scan at this corpus size is cheap; `_ts`-windowed later if wanted), diffs
   against the backend's id inventory, upserts missing/stale documents and deletes
   orphans. Catches any missed push events. A full scan is ~tens of MB of egress —
   negligible, which is also why the backend does not need to live on Azure.
3. **Schema changes:** D1 — additive `ALTER TABLE`/new index online; FTS5 column changes
   rebuild a shadow table then swap in a transaction. ES — index alias swap
   (build v2, repoint alias, drop v1), zero downtime by design.
4. **Hourly indexer retired** once push + reconcile run in parallel with Azure Search and <!-- pragma: allowlist secret -->
   show no drift over a soak period.

### 5A. Does the write path need a Durable Object?

Short answer: **no — start without one.** The question is whether azure-api → D1 pushes
need a single-writer coordinator. What D1 already gives:

- D1 is a single SQLite primary; **writes are serialized by the platform**. There is no
  multi-writer race for a DO to referee.
- Upserts are idempotent (`INSERT … ON CONFLICT DO UPDATE`), deletes are by id, and the
  FTS index follows via the existing AI/AD/AU triggers — replaying or reordering a
  same-document pair is resolved by a `modified_at` guard column (ignore stale writes),
  which is cheaper than an ordering service.
- Expected write volume is tiny (tens of mutations/hour from the Functions apps), far
  below any batching threshold a DO would earn its keep at.

When a coordinator *would* pay rent, prefer **Cloudflare Queues** over a DO: azure-api
posts mutation events to a queue producer endpoint, a consumer Worker batches them into
`db.batch()` writes with built-in retries/DLQ and at-least-once delivery. That covers
burst absorption (weekly reconcile emitting thousands of upserts) without hand-rolled DO
alarm/buffer code. A DO adds value only if we later need per-document fencing tokens or
read-your-write coordination across colos — neither applies to a search index that
tolerates seconds of staleness.

**Does a DO help with the weekly swap (§5B)?** Also no. The in-place rename swap is
already atomic inside one `db.batch()`, so there is nothing to coordinate. For the
blue/green variant, a DO *could* hold the active-database pointer with strong
consistency, but that puts a single-object hop on every read of a read-heavy path to
solve a non-problem: during a KV pointer flip both databases are valid, so eventual
consistency costs nothing. Rebuild orchestration (export → load → verify → swap) is
better served by a cron Worker or Cloudflare Workflows than DO alarms. The honest DO
use-case remains the one above — fencing/ordering guarantees we do not need.

Transport for pushes: authenticated HTTPS from azure-api to the search Worker
(shared-secret header like today's `api-key`, or Auth0 M2M). Note the §4C finding: the
endpoint must be reachable without the zone's bot challenge — either a dedicated
unproxied hostname/route exempted from Bot Fight Mode, or (preferred once the engine
lives behind the api Worker) pushes land on the api Worker and reach the search engine
over a **service binding**.

### 5B. Drip updates and the weekly index reset (blue/green)

How the backend drips changes in, and how the index is swapped wholesale when drip has
failed — "just reset the index" as the recovery path (2026-11-08).

**Drip (in-week amends), concretely.** `SearchIndexerService` call-sites POST batches to
an authenticated admin route on the search Worker: `{ upserts: [doc…], deletes: [id…] }`.
The Worker applies them with one `db.batch()` — atomic, serialized by D1, FTS kept in
step by the external-content triggers, stale replays discarded by the `modified_at`
guard. No downtime: a row upsert is visible to the next query.

**Weekly reset (drip-failure recovery and schema changes).** Two workable designs:

1. **In-place shadow rebuild (preferred).** Bulk-load Cosmos into `search_docs_new` (+
   shadow FTS + triggers) while the live tables keep serving, verify, then swap with a
   single `db.batch()` of `ALTER TABLE … RENAME` statements — D1 batches are
   transactional, so readers see old tables or new tables, never a mix. Drop the old
   tables after. The §4B load took ~10 s for the full corpus, so the rebuild window is
   minutes including Cosmos export.
2. **Blue/green dual database.** Two D1 databases bound as `SEARCH_DB_A` / `SEARCH_DB_B`;
   a KV key names the active one. Weekly reset rebuilds the inactive database and flips
   the pointer; rollback is flipping it back. KV's ~60 s propagation is harmless — both
   sides are valid during the overlap. Buys hard isolation and instant rollback at the
   cost of a second database and the pointer read.

Start with (1); adopt (2) only if rebuild-while-serving ever interferes with live reads.

**When to reset instead of patch.** The weekly reconcile already diffs Cosmos against the
index id inventory. Give it a threshold: small drift → targeted upserts/deletes (patch);
drift beyond the threshold, a failed migration, or an FTS tokenizer change → full rebuild
+ swap. Gate the swap on the §4C.2-style check (doc count + a handful of family counts vs
Cosmos) so a bad build never goes live. Orchestrate with a cron-triggered Worker — or
Cloudflare Workflows if the export→load→verify→swap chain wants durable steps/retries.

**Read replication interplay (accepted direction).** D1 read replication (Sessions API)
serves the read-heavy browse path (93.7 % of traffic) from replicas. Replicas follow the
primary automatically, so drip writes and the rename swap propagate without extra work;
search tolerates the seconds of replica lag. Sequential consistency within a session is
free with `withSession(first-unconstrained)`; nothing in the search path needs
read-your-writes.

**Geography (2026-11-08).** The primary lives in one region — our demo DB is `WEUR`,
`read_replication.mode: disabled`, `jurisdiction: null`. Enabling replication
(`mode: "auto"`) makes Cloudflare place a read replica in **every** supported region
(ENAM, WNAM, WEUR, EEUR, APAC, OC) automatically; no per-region provisioning, no extra
storage/compute charge. Reads via Sessions API route to the nearest replica; writes
always go to the primary. A jurisdiction constraint (EU / FedRAMP) would pin replicas
inside that jurisdiction — we have none set, so global replication is available. Without
Sessions API the replicas exist but are unused (every query still hits the primary).

## 6. Phase sketch (D1 path)

| Phase | Work | Gate |
|-------|------|------|
| P0 | ~~Benchmark~~ **Done 2026-11-07 (§4A synthetic + §4B real data)** — real corpus (89,245 docs, full descriptions) is **239 MB = 2.3 % of the D1 limit**, 3–32 ms query envelope, 8/5.5 ms sync writes, demo Worker running (Api `search-demo/`); remaining P0 work: validate the filter-grammar inventory against real search logs, and a small real-D1 (preview) spot check to confirm production network overhead | Size & latency sign-off |
| P1 | Schema + bulk loader (Cosmos → D1), FTS5 external-content table + triggers | — | <!-- pragma: allowlist secret -->
| P2 | ~~Worker query layer~~ **Prototyped 2026-11-07 (§4C)** — Azure-shaped `POST …/docs/search` with simple-syntax + OData-subset translators live in the demo Worker; filter/facet parity measured exact against production Azure; analyzer parity (§4C.1) at 99.99% Azure⊆D1 via porter FTS + en.lucene query mirroring, verified by the committed harness; a week of real production traffic replayed with zero in-corpus misses (§4C.2). Remaining: ranking weights (bm25 per-column), KV-cached facet snapshots if latency wants them, service-binding wiring | Angular runs unchanged against it |
| P3 | Write path: Worker admin upsert/delete route + retarget `SearchIndexerService`; weekly reconcile timer | Parity harness green | <!-- pragma: allowlist secret -->
| P4 | Dual-run against Azure Search (shadow queries, diff results); cutover `apihost`-equivalent internal routing; soak | Drift-free soak |
| P5 | Retire Azure Search index + hourly indexer; raise `DescriptionSize`; index guests; remove cap-driven slimming constraints that no longer pay rent | — | <!-- pragma: allowlist secret -->

Open decisions for Phase 0:

1. Keep the OData-ish response contract (zero Angular change) vs introduce a cleaner
   structured query DTO (better long-term, more churn) — the 2026-07 survey leaned
   contract-preserving; confirm.
2. Guests: searchable-only (ranking) or also filterable/facetable ("episodes with X")? <!-- pragma: allowlist secret -->
   Facetable costs one more SQL aggregate, not storage anxiety.
3. Facet freshness: compute per-query vs KV snapshot refreshed on write — decide after
   P0 latency numbers.
4. Whether the existing `apiDB` D1 database hosts the search tables or a dedicated
   search database is created (isolation + independent Time Travel restore favour
   dedicated; binding count is a non-issue).

## 6A. Enhancements backlog (post-parity — keep collecting)

Ideas and suggestions gathered as the parity work progresses (started 2026-11-08).
Parity first; everything here ships after cutover unless marked otherwise.

**Relevance (search page, 1.5 % of traffic but the point of search):**

- **bm25 per-column weights** — the known P2 gap: top-10 overlap with Azure is 0–7/10 on
  free text. FTS5 `bm25()` takes per-column weights (title > seriesName > subjects >
  description > search terms); tune against the harness's top-10 overlap metric.
- **Recency blending** — blended `ORDER BY` of bm25 + release-date decay so fresh
  episodes surface. Azure needed a scoring profile for this; the live index never
  defined one.
- **Proximity boosting** — rank `NEAR(a b, n)` matches above bag-of-words matches.
  Azure simple syntax has no equivalent.
- **Zero-result rescue** — when all-terms matching returns nothing, relax to OR / drop
  the rarest term and tell the UI "showing results for…". Impossible in Azure's fixed
  pipeline; trivial when the translator owns the query.

**Net-new capabilities (Azure had these unused, or lacked them):**

- **Spell-correct / "did you mean"** — an `fts5vocab` table exposes the real corpus
  vocabulary; the Worker corrects query terms by edit distance against it. Replaces the
  one genuine capability loss (Azure full-Lucene fuzzy, which the website never used).
- **Autosuggest enrichment** — the existing pipeline is engine-independent and survives
  the swap unchanged: `SearchSuggestionsIndexBuilder` (Cosmos subjects, podcasts, TV
  shows, news organisations, films) → `PublishR2` → R2 object → api Worker
  `searchSuggestions` route (edge-cached, ETagged). Post-swap, enrich the same R2 object
  from D1 — corpus vocabulary, popular titles, query telemetry — without a new read
  path. The live Azure index defines no suggester, so this is additive either way.
- **Synonym expansion** — query-side, from a table versioned in the repo ("LDS" ↔
  "latter-day saints"). More testable than Azure synonym maps (never configured).
- **Highlights over full descriptions** — FTS5 `snippet()`/`highlight()` emitted in
  Azure `@search.highlights` shape (needed for drop-in anyway). Strictly better than
  Azure's, which could only highlight inside its ~100-char truncated descriptions.

**Already banked (verified):** full descriptions beyond the ~100-char cap, guests +
curator search-term fields, diacritics folding (2026-11-08 A/B: `agora`/`ágora` both 421
on D1 vs 23/407 disjoint on Azure — en.lucene has no ASCII folding), legacy
`podcastName` filter tolerance (stale clients get results instead of Azure's 400).

**Operational:**

- **Deterministic pagination** — add `id` as secondary sort key; fixes the measured
  same-timestamp tie shuffle (87/104 positions in the worst 43-doc tie series) and makes
  paging more stable than Azure, whose own within-tie order is undocumented index order.
- **Read replication (Sessions API)** — accepted direction; see §5B. Serves the
  read-heavy browse path (93.7 % of traffic) from replicas.
- **KV-cached facet/count snapshots** — browse counts and facet blocks are highly
  repetitive; short-TTL caching also guards the overload mode observed when the replay
  drove concurrent heavy `COUNT(*)` queries.
- **Service binding + smart placement** — §4C.1 operational finding; no public edge, no
  bot-challenge false positives.
- **Zero-result + slow-query telemetry** — log searches that return nothing (product
  signal: demand the catalogue lacks) through the api Worker's existing structured-log
  pattern.

## 7. Sources (verified 2026-11-07)

- Cloudflare AI Search: [limits & pricing](https://developers.cloudflare.com/ai-search/platform/limits-pricing/) (5 metadata fields, 64-byte filterable strings, query pricing), [metadata](https://developers.cloudflare.com/ai-search/configuration/indexing/metadata/), [filtering](https://developers.cloudflare.com/ai-search/configuration/retrieval/filtering/) (arrays not indexed), [billing start 2026-12-01](https://developers.cloudflare.com/changelog/post/2026-11-02-billing-postponed-default-sync-interval/)
- Cloudflare D1: [pricing](https://developers.cloudflare.com/d1/platform/pricing/), [limits](https://developers.cloudflare.com/d1/platform/limits/) (10 GB/db paid, 500 MB/db free, FTS5 in [supported statements](https://developers.cloudflare.com/d1/sql-api/sql-statements/))
- Elastic: [Cloud Hosted pricing](https://www.elastic.co/pricing/cloud-hosted) ($99 floor), [Azure Native ISV](https://www.elastic.co/docs/deploy-manage/deploy/elastic-cloud/azure-native-isv-service), [serverless pricing](https://www.elastic.co/pricing/serverless-search), [4 GB/node production minimum](https://www.elastic.co/docs/deploy-manage/deploy/elastic-cloud/ec-customize-deployment-components)
- Aiven OpenSearch: [free tier](https://aiven.io/docs/products/opensearch/concepts/opensearch-free-tier), [plan table incl. Developer $40](https://aiven.io/pricing/opensearch)
- Meilisearch: [pricing (no free tier; ~$23/mo minimum)](https://www.meilisearch.com/pricing), [serverless/free-tier still roadmap](https://www.meilisearch.com/blog/2026-march-roadmap)
- VPS reference prices: Hetzner CX23 €3.99/mo; Azure B2s from ~$31.57/mo ([vmprices.com](https://vmprices.com/azure/b2s/))
