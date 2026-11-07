# Search platform epic — leave Azure AI Search Free tier

**Status:** Proposal / Phase 0 planning. No production, index, or code changes.
**Date:** 2026-11-07 (all vendor limits and prices re-verified on this date).
**Related:**
[search-platform-alternatives.md](./search-platform-alternatives.md) (2026-07 survey — still the wide-net reference),
[search-index-slimming-plan.md](./search-index-slimming-plan.md) (tactical slimming, now executed),
[catalogue-content-types-search-storage-impact.md](./catalogue-content-types-search-storage-impact.md) (contentKind growth),
[catalogue-content-types-epic.md](./catalogue-content-types-epic.md).

---

## 1. Problem statement

Azure AI Search Free tier (50 MB hard cap) is exhausted as a growth platform. The slimming
plan has already been executed — compact service IDs, `svc` packing, `explicit` dropped,
YouTube image derivation — and the index still ran out of room:

| Date | Corpus | Description cap | Index size |
|------|--------|-----------------|------------|
| 2026-07-17 | 82,252 docs | 230 chars | 49.08 MB (~98% of cap) |
| 2026-10-22 | 88,279 docs | 100 chars | 52.06 MB — **over cap**, indexing broken |
| 2026-10-22 | — | cut to 50 | recovered |
| today | ~88k+ docs | **80 chars** (`Constants.DescriptionSize`) | ~41.5 MB (~8.5 MB headroom) |

Searchable episode description is now 80 characters. Every future need makes this worse:

- **Guests** — `Playable.Guests` exists on the model but is **not projected into search at
  all** (confirmed: no reference in `RedditPodcastPoster.Search` or
  `RedditPodcastPoster.EntitySearchIndexer`). Indexing ~88k × guest names has no room.
- **Catalogue content types epic** — TvShowEpisode / Film / NewsReport rows, `seriesDescription`,
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
   `lang eq null` (null = English), `id eq`, `contentKind`.
3. Sort by `release` asc/desc or relevance (BM25-ish score).
4. Facet counts: `podcastName` and `subjects` up to 1000 buckets; `lang`; `contentKind`. <!-- pragma: allowlist secret -->
5. Stable pagination (`skip`/`top`) + total count.
6. Public-site latency (Worker already edge-caches `POST /search` for 600 s).

New platform requirements from this epic:

7. **Restore description richness** — at least the old 230 chars; ideally no cap anxiety
   at all (500+ chars).
8. **Incremental sync without downtime** — add/update/remove individual documents while
   the index keeps serving. No teardown-rebuild for schema-compatible changes.
9. **Retire the hourly Cosmos → Search pull indexer.** Target model: **event-driven push
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

## 4. Recommendation

**Primary: Worker + D1 FTS5** — unchanged from the 2026-07 survey, now stronger:

- **£0 marginal cost, forever.** The account is already on Workers Paid for the Api
  Worker (Browser Rendering, Durable Objects, Analytics Engine, a production D1 database).
  Included D1 quota (10 GB per database, 25 billion row-reads/month) dwarfs this workload
  by orders of magnitude. Cloudflare commits to D1 keeping a free plan; FTS5 is a
  supported extension (verified 2026-11).
- **Kills the cap anxiety permanently.** Full-fidelity projection (500-char descriptions
  + guests + contentKind, ~88k rows ≈ 70 MB raw + FTS index) lands around 150–250 MB —
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

## 5. Target sync architecture (backend-agnostic)

Today there are two write paths into Azure Search:

| Path | Mechanism | Fate |
|------|-----------|------|
| Hourly pull | Azure Search indexer, Cosmos datasource, `_ts` high-watermark (`SearchIndexHandler`, `CreateSearchIndex`) | **Retired** — it is an Azure Search feature and does not exist elsewhere |
| Push on mutation | `SearchIndexerService.MergeOrUploadDocumentsAsync` / `DeleteDocumentsAsync` after episode/podcast mutations | **Kept** — becomes the primary path, retargeted at the new backend |

Target model (user requirement: in-week amends + weekly reconcile, no index downtime):

1. **In-week amends (primary, event-driven).** Every existing push call site keeps its
   semantics — upsert on save, delete-by-id on removal — with the transport swapped:
   - **D1:** authenticated Worker admin route (service-to-service; Auth0 M2M or Worker
     secret — plan the production secret alongside preview per the parity rule).
     `INSERT … ON CONFLICT DO UPDATE` / `DELETE`, with the FTS5 table kept in step via an
     external-content table and triggers. Row upserts are atomic; the index never stops
     serving.
   - **ES/OpenSearch/Meili:** direct REST from the .NET indexer (`_doc` PUT / DELETE).
2. **Weekly reconcile (safety net).** Scheduled Functions timer (Indexer app) scans
   Cosmos (full scan at this corpus size is cheap; `_ts`-windowed later if wanted), diffs
   against the backend's id inventory, upserts missing/stale documents and deletes
   orphans. Catches any missed push events. A full scan is ~tens of MB of egress —
   negligible, which is also why the backend does not need to live on Azure.
3. **Schema changes:** D1 — additive `ALTER TABLE`/new index online; FTS5 column changes
   rebuild a shadow table then swap in a transaction. ES — index alias swap
   (build v2, repoint alias, drop v1), zero downtime by design.
4. **Hourly indexer retired** once push + reconcile run in parallel with Azure Search and
   show no drift over a soak period.

## 6. Phase sketch (D1 path)

| Phase | Work | Gate |
|-------|------|------|
| P0 | Benchmark: bulk-load current corpus **at full fidelity** (500-char descriptions, guests, contentKind) into a D1 preview DB; measure size, query + facet latency under the Worker; validate the filter-grammar inventory against real search logs | Size & latency sign-off |
| P1 | Schema + bulk loader (Cosmos → D1), FTS5 external-content table + triggers | — |
| P2 | Worker query layer behind `POST /search`: OData-subset parser → SQL; response shaped as `value` / `@odata.count` / `@search.facets`; KV-cached facet snapshots | Angular runs unchanged against it |
| P3 | Write path: Worker admin upsert/delete route + retarget `SearchIndexerService`; weekly reconcile timer | Parity harness green |
| P4 | Dual-run against Azure Search (shadow queries, diff results); cutover `apihost`-equivalent internal routing; soak | Drift-free soak |
| P5 | Retire Azure Search index + hourly indexer; raise `DescriptionSize`; index guests; remove cap-driven slimming constraints that no longer pay rent | — |

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

## 7. Sources (verified 2026-11-07)

- Cloudflare AI Search: [limits & pricing](https://developers.cloudflare.com/ai-search/platform/limits-pricing/) (5 metadata fields, 64-byte filterable strings, query pricing), [metadata](https://developers.cloudflare.com/ai-search/configuration/indexing/metadata/), [filtering](https://developers.cloudflare.com/ai-search/configuration/retrieval/filtering/) (arrays not indexed), [billing start 2026-12-01](https://developers.cloudflare.com/changelog/post/2026-11-02-billing-postponed-default-sync-interval/)
- Cloudflare D1: [pricing](https://developers.cloudflare.com/d1/platform/pricing/), [limits](https://developers.cloudflare.com/d1/platform/limits/) (10 GB/db paid, 500 MB/db free, FTS5 in [supported statements](https://developers.cloudflare.com/d1/sql-api/sql-statements/))
- Elastic: [Cloud Hosted pricing](https://www.elastic.co/pricing/cloud-hosted) ($99 floor), [Azure Native ISV](https://www.elastic.co/docs/deploy-manage/deploy/elastic-cloud/azure-native-isv-service), [serverless pricing](https://www.elastic.co/pricing/serverless-search), [4 GB/node production minimum](https://www.elastic.co/docs/deploy-manage/deploy/elastic-cloud/ec-customize-deployment-components)
- Aiven OpenSearch: [free tier](https://aiven.io/docs/products/opensearch/concepts/opensearch-free-tier), [plan table incl. Developer $40](https://aiven.io/pricing/opensearch)
- Meilisearch: [pricing (no free tier; ~$23/mo minimum)](https://www.meilisearch.com/pricing), [serverless/free-tier still roadmap](https://www.meilisearch.com/blog/2026-march-roadmap)
- VPS reference prices: Hetzner CX23 €3.99/mo; Azure B2s from ~$31.57/mo ([vmprices.com](https://vmprices.com/azure/b2s/))
