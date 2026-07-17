# Search platform alternatives (strategic)

**Status:** Proposal only — no production or code changes.  
**Date:** 2026-07-17  
**Related:** [search-index-slimming-plan.md](./search-index-slimming-plan.md) (tactical stay-on-Azure-Free), [migration/README.md](./migration/README.md) (Cosmos split; assumes Azure AI Search).

---

## Recommendation (read this first)

| Rank | Path | Verdict |
|------|------|---------|
| **1 — Best free long-term** | **Cloudflare Worker + D1 (SQLite FTS5)** | Best fit for “stay free” with the existing Worker edge. 5 GB free D1 storage easily holds an 82k+ episode projection; implement search/filter/facet/sort in the Worker. |
| **2 — Only plausible managed $0 candidate** | **Aiven OpenSearch Free** | Verified 2026-07-17 at **4 GB RAM / 20 GB storage** (not the conflicting 30 GB shown on Aiven's generic pricing page). It holds the corpus, but Aiven labels it learning/prototyping/evaluation, powers it off after 24 hours with no indexing or queries, and gives it no SLA. |
| **3 — Best low-cost self-managed** | **Self-hosted Meilisearch** (small VPS / container) | Excellent feature fit, but it is not a managed free tier: the software is free and the infrastructure/operations are yours. |
| **Bridge (not free)** | **Azure AI Search Basic (~$74/SU-month list)** | Lowest migration risk: keep OData, indexer, Angular contracts. Use if Free-tier storage pain returns after slimming and you will not invest in a Worker rewrite. |
| **Near-term** | **Stay on Azure Free + index slimming** | Correct tactical move while evaluating alternatives ([search-index-slimming-plan.md](./search-index-slimming-plan.md)). Does not solve long-term growth past ~50 MB. |

**Honest free ceiling:** A public podcast site with ~82k growing documents, multi-field keyword search, `search.in` / collection filters, date sort, relevance ranking, facet counts up to 1000, and pagination **can** stay free where storage is multi‑GB and you own the query layer (D1/FTS5 or self-hosted Meilisearch/OpenSearch). As of 2026-07-17, **there is no managed, ongoing $0 tier that is both a comfortable functional fit and a reliable production offering**. Aiven is the closest, but explicitly has no SLA and is positioned for evaluation; Algolia and Upstash impose low request ceilings; Atlas M0 is shared and storage-constrained.

**Migration docs alignment:** The multi-container Cosmos migration docs treat **Azure AI Search as the search projection** (datasource on `Episodes`, `EpisodeSearchIndexerService`). They do **not** discuss platform alternatives. This document is the strategic counterpart; Cosmo migration work stays valid whatever search backend is chosen (episodes remain source of truth).

---

## Current usage (verified from codebase)

### Architecture

```
Angular PWA  --POST /search-->  Cloudflare Worker (pass-through + cache)  -->  Azure AI Search
.NET indexer / CreateSearchIndex  --push/pull-->  Azure AI Search
Homepage  --GET /homepage-->  Worker / Functions  (NOT Azure Search)
```

- Index: Free tier `cultpodcasts` (uksouth), ~82k docs, ~49 MB / **50 MB hard cap**.
- Public reads: Worker `Api/src/search.ts` → `c.env.apihost` (Azure Search docs API).
- Writes: `EpisodeSearchIndexerService` + Cosmos pull indexer (`CreateSearchIndex`).
- Azure Functions API does **not** proxy public search.

### Features actually used

| Capability | Used? | Evidence |
|------------|-------|----------|
| Full-text (simple query) | **Yes** | `queryType: 'simple'`, `searchMode: 'any'` over title/description/subjects/hidden search-terms |
| OData `$filter` | **Yes** | `podcastName eq`, `search.in(podcastName,…)`, `subjects/any(s: s eq …)`, `subjects/any(s: search.in(…))`, `lang eq null`, `id eq` |
| `$orderby` | **Yes** | `release asc` / `release desc`; empty orderby → relevance (`@search.score`) |
| Facets | **Yes** | `podcastName,count:1000` and `subjects,count:1000` (subject page: subjects count 10) |
| `$count` + skip/top | **Yes** | Infinite scroll / paging |
| Highlighting | **No** | Not requested |
| Suggestions / autocomplete | **No** | Not used |
| Geo | **No** | — |
| Vectors / semantic rank | **No** | No vector fields; Free tier lacks semantic ranking |
| Scoring profiles | **Implicit only** | Default BM25-style score when not sorting by date |

### Document shape (search projection)

See `EpisodeSearchRecord` / [search-index-slimming-plan.md](./search-index-slimming-plan.md) §2. Roughly: id, titles, truncated description (230 chars), podcastName, release, duration, explicit, platform URLs, subjects[], hidden search-terms, image, lang. Retrievable card fields dominate storage; filterable/facetable attributes multiply footprint.

### Non-negotiables for any replacement

1. Keyword search over episode + podcast text (incl. curator search-terms).
2. Filter by podcast name(s), subject(s), lang null, episode id.
3. Sort by release **or** relevance.
4. Facet counts for podcasts and subjects (UI chip filters).
5. Stable pagination + total count.
6. Sub‑hundreds-of-ms typical latency for a public site (Worker already CF-caches search 600s).
7. Incremental upsert/delete from the .NET indexing path (or a dual-write Worker job).

---

## Comparison table

Limits below are as of mid‑2026 research; re-check vendor pages before committing.

| Candidate | Free / cheap limits | Filters / sort / FTS / facets | Fit to current UX | Migration effort | Ops burden | Stays free as corpus grows? | Latency / UX |
|-----------|---------------------|-------------------------------|-------------------|------------------|------------|-----------------------------|--------------|
| **Stay Azure Free + slim** | 50 MB, 3 indexes, shared HW | Full OData parity | Perfect | Low (schema only) | Low (managed) | **No** — hard 50 MB | Good today |
| **Azure Basic** | ~$74/SU‑mo, 15 GB | Full parity | Perfect | **Lowest** | Low | Paid forever | Excellent |
| **CF Worker + D1 FTS5** | Free: **5 GB** storage, 5M rows read/day; Paid Workers: same 5 GB included then $0.75/GB‑mo | FTS5 + SQL filters/sort; facets via SQL `GROUP BY` (you build) | High if Worker API mirrors today’s JSON | **High** (rewrite query + write path) | Low–med (SQL schema, FTS config) | **Yes** into multi‑GB; may need Workers Paid if read volume high | Edge-local; good with cache |
| **Self-host Meilisearch** | OSS free; host ~$5–20/mo VPS | Filters, sort, facets, typo tolerance native | High | Med (SDK + replace OData) | Med (updates, backups, TLS) | Yes if you pay the box | Excellent (&lt;50 ms typical) |
| **Meilisearch Cloud** | 14‑day trial; then ~$20+/mo | Same as OSS | High | Med | Low | **No** (paid) | Excellent |
| **Self-host Typesense** | OSS free; same VPS story | Filters, facets, sort | High | Med | Med | Yes (box cost) | Excellent |
| **Typesense Cloud** | One-time 720 cluster hours + 10 GB BW — **not recurring** | Same | High | Med | Low | **No** | Excellent |
| **Aiven OpenSearch Free** | **$0** ongoing: 4 GB RAM, **20 GB** storage, 2 shards, 50 connections; powers off after 24h with no indexing/querying | ES-like query DSL; facets/aggs | High | Med–high | Low (managed) | Comfortable capacity; not production-backed | Good while running; manual wake after idle shutdown |
| **Elastic Cloud** | 14‑day trial only | Overkill feature set | High | High | Low when paid | **No** free long-term | Excellent |
| **Algolia Build** | 1M records, **10k searches/mo**, 1 GB app | Filters/facets excellent | High | Med | Low | **No** — 10k searches kills a public site | Excellent |
| **Algolia Grow free allotment** | 10k searches + 100k records then $ | Same | High | Med | Low | **No** at traffic | Excellent |
| **Neon / Supabase PG FTS** | **0.5 GB** DB free | `tsvector` + SQL; facets DIY | Med–high | High | Low–med | **Tight** at 0.5 GB; growth → paid | Neon scales-to-zero → cold latency |
| **CF Hyperdrive → Postgres** | Needs paid PG usually | Same as PG | Med–high | High | Med | Only if PG paid | Better than raw Neon from Worker |
| **CF AI Search** | Free beta: 100k files, **20k queries/mo**, **5 metadata fields** | RAG/hybrid; weak structured facets | **Poor** for podcast browse UX | High | Low | Query + metadata caps | RAG-oriented, not catalog search |
| **CF Vectorize alone** | Vectors only | No lexical FTS replacement | **Poor** | High | Low | N/A as sole engine | Semantic ≠ keyword |
| **Cosmos full-text / vector** | Uses existing RU $; FTS preview | FTS + rank; facets weak vs Search | Med | Med–high | Low (same DB) | Not free; RU spike risk | Variable; costly |
| **Azure SQL FTS** | No meaningful free SKU for always-on | FTS + SQL | Med | High | Med | Paid | OK |
| **Client MiniSearch / Lunr** | Browser download | Local only | Poor at 82k | Med | None | Feasible only if tiny index | First load huge; bad mobile |

---

## Candidate notes

### 1. Cloudflare Worker + D1 / FTS5 (recommended free path)

**Why it fits:** You already terminate search at the Worker. Replacing the Azure pass-through with D1 queries keeps the public surface (`POST /search`) and CF cache. Free D1 storage (5 GB) is ~100× the Azure Free cap.

**Query model sketch:**

- Table `episodes` + `episodes_fts` (FTS5) on title, description, podcast name, subjects, search-terms.
- Filters: `WHERE podcast_name IN (…)`, `EXISTS` subject junction or JSON/`LIKE` normalized subjects, `lang IS NULL`, `id = ?`.
- Sort: `ORDER BY release DESC` or `ORDER BY rank` (FTS5 `bm25` / `rank`).
- Facets: separate aggregated queries (or cached facet snapshots in KV refreshed on write).
- Response: shape today’s OData-ish JSON (`value`, `@odata.count`, `@search.facets`) so Angular changes stay small.

**Gaps to engineer:** Azure `search.in` / `subjects/any` → SQL; facet `count:1000`; typo tolerance weaker than Meilisearch unless you add trigram/spell logic; write path from .NET must upsert D1 (HTTP to Worker admin route, or queue).

**Growth:** Comfortable past 82k with compact rows (align with slimming: short keys, platform ids not URLs). Workers Free row-read quota (5M/day) may force **Workers Paid** under heavy uncached traffic — still far cheaper than Azure Search Basic if storage stays in the included 5 GB.

### 2. Meilisearch / Typesense (self-host = free software; cloud ≠ free)

**Feature fit:** Excellent — filters, facets, sort, typo-tolerant search map cleanly onto podcast/subject pages.

**Cloud:** Meilisearch Cloud is trial then paid (~$20+/mo). Typesense Cloud free hours are **lifetime one-shot**, then billed. Neither is a long-term $0 managed plan for production.

**Self-host:** Real “free search engine”; you pay only for a small VM/container (~1 GB RAM likely enough for this corpus). Fits Azure+.NET write path via REST from `EpisodeSearchIndexerService`.

### 3. OpenSearch / Elasticsearch

- **Aiven OpenSearch Free:** Closest candidate for “managed + $0” with 4 GB RAM / 20 GB storage. It has no SLA, one node, two shards, one disaster-recovery backup (product page says up to three days' snapshot retention), and powers off after 24 hours with no indexing or queries. Treat it as an evaluation tier, not a production guarantee. The generic Aiven pricing page currently says 30 GB; the product-specific free-tier page says 20 GB, so this document uses the conservative product-specific limit.
- **Elastic Cloud:** Trial only → paid.
- **Self-host ES/OS:** Overkill ops for this feature set vs Meilisearch.

### 4. Algolia

Records capacity is fine; **10k search requests/month** on free Build/Grow allotments is not viable for cultpodcasts.com (homepage is separate, but search/podcast/subject/SSR page-details fallbacks still generate searches). Treat as paid product or reject.

### 5. PostgreSQL FTS (Neon / Supabase) ± Hyperdrive

Technically solid (GIN/`tsvector`, filters, DIY facets). Free **0.5 GB** is the problem: a denormalized search table with FTS indexes may fit initially if aggressively slimmed, but leaves little headroom vs D1’s 5 GB. Neon scale-to-zero hurts public TTFB unless always-warm (paid) or cached heavily at the Worker.

### 6. Staying on Azure (Free / Basic / Cosmos / SQL)

| Option | Role |
|--------|------|
| **Free + slim** | Buy time; dual-index room for cutover of compact schema |
| **Basic** | Escape hatch with near-zero UX rewrite (~$74/mo list) |
| **Cosmos FTS** | Avoids a second store but burns RU on already-costly Episodes path; facet UX and Free-tier story worse |
| **Azure SQL FTS** | New paid database; poor free story |

### 7. Cloudflare AI Search / Vectorize

Wrong product shape: RAG/file/metadata limits (5 custom metadata fields, 20k queries/mo on Free Workers) cannot replace podcast/subject facet browse. Vectorize alone does not replace lexical search.

### 8. Static / client-side search

~82k documents × (title + 230-char description + subjects + ids) is typically several–tens of MB compressed. Unacceptable as a PWA primary download; only plausible for a tiny “podcast names only” index, which does not meet current UX.

---

## Migration sketches (top options)

### A. Worker + D1 (best free path)

1. **Define** compact search table matching slimmed `EpisodeSearchRecord` (ids not URLs).
2. **Bulk load** once from Cosmos or Azure Search export into D1 (offline script / wrangler).
3. **Implement** `POST /search` in Worker: parse existing Angular body (`search`, `filter`, `orderby`, `facets`, `skip`, `top`, `count`) → SQL/FTS; return compatible JSON.
4. **Dual-write:** extend indexing to upsert/delete D1 via authenticated Worker route (or Azure → queue → Worker). Keep Azure Search until parity.
5. **Cut over** `apihost` / Worker to D1-only; retire Azure Search when stable.
6. **Angular:** prefer zero change if Worker preserves contract; otherwise replace OData filter strings with a small structured query DTO (cleaner long-term).

**Effort:** roughly 1–2 focused weeks for an engineer who knows the Worker + indexing paths; facets and filter parser are the hard parts.

### B. Self-hosted Meilisearch (best free-enough search UX)

1. Deploy Meilisearch (Docker on cheap VPS or Azure Container Instance).
2. Create index settings: searchable/filterable/facetable attributes aligned to current fields; primary key `id`.
3. Bulk import documents; point `EpisodeSearchIndexerService` at Meilisearch documents API (replace `SearchClient`).
4. Worker: either proxy Meilisearch search API or translate OData → Meilisearch filter syntax (`podcastName = "…"`, arrays for subjects).
5. Validate facet chip UX, date sort, relevance, `getPageDetails` fallback.
6. Decommission Azure Search.

**Effort:** often less than D1 for search quality; more ongoing ops (patching, backups, monitoring).

### C. Azure Basic (lowest effort paid)

1. Provision Basic in uksouth; recreate index (optionally slimmed schema).
2. Repoint indexer + Worker `apihost`.
3. No Angular change if field names unchanged.

---

## Decision guide

```text
Need zero rewrite and can spend ~$75/mo?
  → Azure AI Search Basic

Need $0 long-term and already on Cloudflare?
  → Worker + D1 FTS5 (accept building facets/filters)

Need best search UX on a shoestring and OK with a small server?
  → Self-host Meilisearch (or Typesense)

Want managed $0 without writing SQL search?
  → No production-reliable option; evaluate Aiven OpenSearch Free only if no SLA
    and manual recovery from idle shutdown are acceptable

Still on Free and only need months of headroom?
  → Execute search-index-slimming-plan.md first
```

---

## What “free won’t work” means here

These are **not** viable as permanent $0 production search for Cult Podcasts:

- **Algolia free search quotas** (10k searches/mo).
- **Meilisearch / Typesense / Elastic managed free trials** (time- or hour-boxed).
- **Neon/Supabase free 0.5 GB** as a growing primary search store.
- **Azure AI Search Free** beyond aggressive slimming (~50 MB wall).
- **Client-side full corpus search** at 82k+ rich documents.
- **Cloudflare AI Search** as a drop-in catalog/facet engine.

“Free” that **does** work long-term is almost always **open-source search (or SQLite FTS) on infrastructure you already pay for or that has multi‑GB free storage** — not a second SaaS with a marketing free tier.

---

## Managed free-tier verification (2026-07-17)

This section is the live-pricing follow-up to the earlier strategic comparison. “Ongoing” means a recurring or indefinite $0 managed plan, not a trial, signup credit, student offer, or self-hosted software. Published limits are quoted exactly where possible; “not published” is used instead of inferring RAM, storage, backup, or SLA terms.

### Bottom line and ranked ongoing $0 options

Only the following are genuinely managed and ongoing at $0 **and** expose more than Azure AI Search Free's 50 MB of storage/capacity. The ranking is for this 82,252-document podcast workload, not for general product quality.

| Rank | Managed ongoing $0 offer | Exact published free limits | Corpus and growth fit | Idle / retention / reliability | UK/EU and card |
|------|---------------------------|-----------------------------|-----------------------|--------------------------------|----------------|
| **1** | **Aiven for OpenSearch Free** | 1 node, 1 CPU, **4 GB RAM, 20 GB storage**, 2 shards (20 max/node), 50 concurrent connections, one service/org. No document or query quota published. | **Comfortable capacity and full feature fit.** Even allowing substantial Lucene overhead over the current ~49 MB Azure index, 20 GB is ample. Two shards are enough for this corpus. | Powers off after **24h with no indexing or querying**; console power-on required. No SLA, fixed maintenance, no operator-routed alerts. One DR backup; product page says snapshot retention up to **3 days**. Aiven may change free configuration/regions. | No card. User selects a region **group**, not an exact cloud/region; an EU group can be selected if offered in the console, but the exact UK/EU location is not guaranteed or migratable. |
| **2** | **Algolia Build** | **1,000,000 records**, **10,000 search requests/month**, 10,000 recommendation requests and 10,000 crawls/month. Storage and RAM are not published because quota is record/request based. | **Documents fit (~12.2× current count), traffic probably does not.** Excellent filters, facets, relevance and sorting, but 10k searches/month is only ~333/day. Suitable only if Cloudflare caching keeps origin searches below that hard ceiling. | No sleep behavior published. Build is a developer/test plan with no support/SLA published; backup/restore terms are not specified for Build. | No card. Pricing advertises US, UK and EU-West locations. |
| **3** | **Upstash Search Free** | **200,000 records**, **20,000 monthly queries/requests**, up to 10 free databases, 10,000 indexes/database, `topK` max 1,000, **1,500 characters/document**; free migration/upsert limit **10,000 documents/day**. RAM and a free-tier byte quota are not published (service FAQ gives a 50 GB database ceiling, while Free is record-limited). | **Raw count fits only to ~2.43× current size. Functional fit is poor:** filtering exists, but no independent date sort, facet counts, total-count paging, or stable search offset is documented. Search and document updates both consume requests. | Serverless; no idle deletion documented. **Preview/Early Access; no uptime SLA.** Backup/retention terms are not published. | Card is required only to upgrade to PAYG, so not for Free. Console offers region choice, but the Search-specific public docs do not enumerate regions; do not assume Redis's London/Frankfurt list applies to Search. |
| **4** | **MongoDB Atlas Free (M0) with Atlas Search** | **512 MB database quota** for uncompressed BSON plus database indexes; separate Lucene Search-index bytes are not published. Shared RAM/vCPU, up to **100 ops/s**, 500 connections, **10 GB in + 10 GB out per rolling 7 days**, 3 Search indexes, one free cluster/project. | **Possible but not comfortable.** 82k compact documents may fit, and Atlas Search supports text, filters, facets, counts, relevance/date sort. However, its accounting differs from Azure's 49 MB search index, Search shares M0 resources, and growth/reindex headroom is uncertain. Benchmark the full projection before considering it. | Automatically pauses after **30 days with zero connections**. No Atlas backups on Free (manual `mongodump` only). No SLA; 99.995% applies to M10+. | No card for M0. Free clusters exist only in a subset of regions; Atlas lists Azure UK South plus EU regions in its region catalog, but Free availability must be confirmed in the create-cluster UI. |
| **5** | **Bonsai Sandbox (Heroku add-on)** | **119 MB capacity, 125 MB RAM, 35,000 documents, 10,000 requests/day**, 2 shards, 1 concurrent search, 1 concurrent indexing operation, 159 MB outgoing/day. | **Does not fit:** 35k documents is only 43% of the current corpus, regardless of its storage being above 50 MB. | Three-node multitenant service. Heroku listing says hourly backups, but Bonsai's snapshot doc says automatic snapshots cover **paid clusters**; therefore free backup/retention is contradictory and must not be relied on. No free-plan SLA published. | Heroku Common Runtime Europe available (exact region controlled by Heroku; Private Space London is not listed as available). Card requirement is not stated by Bonsai; Heroku may require account/payment verification to install add-ons. |

**Verdict:** Aiven is the only free managed tier with comfortable capacity and native search semantics, but its official terms make it unsuitable as a dependable production promise. Therefore **none of the verified ongoing $0 managed offers is a reliable long-term production recommendation**. Aiven can be benchmarked as a no-cost experiment; D1/FTS5 or paid/self-managed infrastructure remains the defensible long-term path.

### Aiven discrepancy resolved

The earlier 4 GB / 20 GB claim is substantially correct, but “permanent production free tier” was too strong:

- The product-specific [Aiven for OpenSearch free-tier page](https://aiven.io/docs/products/opensearch/concepts/opensearch-free-tier) says **4 GB RAM and 20 GB storage**, explicitly describes the plan as learning/prototyping/evaluation, and documents no SLA plus idle shutdown.
- Aiven's generic [service-pricing page](https://aiven.io/docs/platform/concepts/service-pricing) currently says the OpenSearch Developer tier has **30 GB storage** and is “always-on.” That conflicts with the product-specific page and its 24-hour idle-shutdown rules.
- Until Aiven makes those pages consistent or the console contract shows otherwise, use **20 GB and possible idle shutdown** for planning. No heartbeat workaround should be treated as a contractual entitlement.
- Creation details and region-group restriction: [Create a free-tier OpenSearch service](https://aiven.io/docs/products/opensearch/howto/create-free-tier-opensearch). Backup details: [Aiven service backups](https://aiven.io/docs/platform/concepts/service_backups).

### Ongoing free offers that fail the >50 MB or corpus test

| Provider | Verified offer | Why it is not a candidate | Operations / region / card |
|----------|----------------|---------------------------|----------------------------|
| **Searchly** | Starter is ongoing free: **20 MB, 2 indexes**; no card. | Below Azure's 50 MB and below the existing ~49 MB index before growth. | AWS US East or EU West. Daily backups are advertised for **paid** plans; no free SLA/backup promise published. [Pricing](https://www.searchly.com/pricing), [architecture/regions](https://docs.searchly.com/article/14-introduction). |
| **Bonsai direct/Heroku** | The verifiable free plan is the 119 MB / 35k-doc Heroku Sandbox above. | Storage exceeds 50 MB but document cap fails immediately. Bonsai's direct public pricing page starts with paid Staging and does not publish a separate direct Hobby quota. | [Heroku plan table](https://elements.heroku.com/addons/bonsai), [Bonsai architecture](https://bonsai.io/docs/platform/bonsai-architecture/), [snapshots](https://bonsai.io/docs/platform/snapshots/). |
| **Skryx** (credible new EU alternative) | Site says a forever free plan and no card, but does **not publish exact free document/query/storage limits** on the public pricing material found. | Cannot verify that it exceeds 50 MB or 82k docs; its public proof point is stores at 25k+ products, not this corpus. Not ranked without exact authoritative limits. | Frankfurt/EU-hosted; no public free SLA/backup terms found. [Product](https://skryx.io/), [API docs](https://skryx.io/docs/index). |

### Trials, credits, paid-only services, and self-hosting (not ranked)

| Provider | Current verified status — not an ongoing managed free tier | UK/EU, resilience, and payment notes |
|----------|-------------------------------------------------------------|--------------------------------------|
| **Elastic Cloud** | **14-day trial** only; no card. Trial allows one hosted deployment, up to 8 GB RAM and approximately 360 GB storage (Elastic's pricing FAQ separately says 240 GB, another reason not to treat trial sizing as a plan). Then paid. | AWS/GCP/Azure regions include Europe; exact region selectable. Trial has no production SLA commitment. [Trial](https://www.elastic.co/cloud/cloud-trial-overview), [trial limits](https://www.elastic.co/docs/deploy-manage/deploy/elastic-cloud/create-an-organization). |
| **AWS OpenSearch Service** | The often-quoted **750 t2/t3.small hours/month + 10 GB EBS** was **12 months free**, not ongoing, for pre-2025-07-15 accounts. New accounts receive signup credits/free-account access for at most six months; OpenSearch is not established as an Always Free service. | London, Ireland and Frankfurt service regions exist. Managed domains retain automated snapshots **14 days**. AWS signup/account normally includes a payment method; overages bill automatically on paid plans. [Current Free Tier model](https://docs.aws.amazon.com/awsaccountbilling/latest/aboutv2/free-tier.html), [offer classification](https://docs.aws.amazon.com/awsaccountbilling/latest/aboutv2/tracking-free-tier-usage.html), [OpenSearch pricing/snapshots](https://aws.amazon.com/opensearch-service/pricing/). |
| **Instaclustr OpenSearch** | Free **trial** only; ongoing service is paid/contact-sales. | Multi-cloud AWS/Azure/GCP, including European deployments. The advertised 99.999% SLA, automated backups and support are paid-service attributes. Card requirement for trial is not stated in the public pages reviewed. [Managed OpenSearch](https://www.instaclustr.com/platform/managed-opensearch/), [pricing](https://www.instaclustr.com/pricing/). |
| **Scaleway Cloud Essentials for OpenSearch** | Paid only. Smallest published shared node is **2 vCPU / 8 GB RAM at €0.099/hour**, plus block storage at €0.000136/GB/hour. | European cloud; no UK location established in the OpenSearch docs reviewed. Managed HA is available on paid configurations. Account/payment verification applies. [Pricing](https://www.scaleway.com/en/pricing/managed-databases/), [FAQ](https://www.scaleway.com/en/docs/opensearch/faq/). |
| **IBM Cloud Databases for Elasticsearch** | Paid only, hourly by members, vCPU, RAM, disk and backup; no Lite/free plan. | IBM has UK/EU cloud regions, but deployment availability must be checked in catalog. HA and automated backup orchestration are paid-service features. IBM Cloud account/payment terms apply. [Pricing model](https://cloud.ibm.com/docs/cloud-databases?topic=cloud-databases-hosting-pricing). |
| **Oracle OCI Search with OpenSearch** | Not Always Free. The first two data-node **management fees** are waived, but compute, RAM, block/object storage are still charged. The only free access is the **30-day / US$300 credit**. | OCI has UK/EU regions; fully managed patching, resizing and automated backups are paid-service features. Signup may require card/identity verification. [OCI Free Tier](https://www.oracle.com/cloud/free/), [OpenSearch pricing](https://www.oracle.com/cloud/search/pricing/), [features/backups](https://www.oracle.com/cloud/search/features/). |
| **Meilisearch Cloud** | **14-day trial**, no card; then paid (published entry pricing starts around $20–30/month depending billing model). No ongoing cloud free plan yet. Meilisearch's March 2026 roadmap targets serverless/free tier work for Q3 2026, which is future intent, not a current offer. | Region selectable; cloud handles backups/updates. SLA up to 99.999% is an enterprise feature, not trial. [Pricing](https://www.meilisearch.com/pricing), [roadmap](https://www.meilisearch.com/blog/2026-march-roadmap). |
| **Typesense Cloud** | First **720 cluster-hours + 10 GB bandwidth once per account lifetime**; it does not renew. No card to start, then running clusters charge or stop. | London and Frankfurt available. A free single node has no HA/support SLA; production HA requires at least 3 paid nodes. [Free-tier terms](https://cloud-help.typesense.org/article/how-does-the-free-tier-work), [regions](https://cloud-help.typesense.org/article/data-center-locations), [HA](https://cloud-help.typesense.org/article/high-availability). |
| **ZincSearch hosting** | ZincSearch is free to self-host. Elestio managed hosting offers only **$20 credit valid 3 days**; OctaByte advertises a trial, not an ongoing free plan. | Elestio can use European infrastructure and manages backups/updates on paid instances. Trial card terms not clearly published. [Elestio pricing](https://elest.io/open-source/zincsearch/resources/plans-and-pricing), [OctaByte](https://octabyte.io/fully-managed-open-source-services/applications/search/zincsearch/). |
| **Quickwit hosting** | Quickwit has no first-party hosted SaaS; it is open-source/self-hosted. Elestio's managed Quickwit is paid after the same short signup credit. | European Elestio locations are available; backups/monitoring belong to the paid managed service. [Quickwit](https://quickwit.io/), [Elestio managed Quickwit](https://elest.io/open-source/quickwit). |
| **Orama Cloud** | OramaJS is free **self-hosted/in-app**. Managed Orama Cloud Pro is paid with a monthly fee plus one-time onboarding; no public cloud free tier. | Managed region/SLA/backup details are sales-scoped. [Pricing](https://orama.com/pricing). |

### Capacity interpretation for this corpus

- **Current baseline:** 82,252 growing episode documents; Azure reports ~49 MB after indexing. That 49 MB is useful for order-of-magnitude comparison but is **not portable sizing**: OpenSearch/Lucene, Algolia record accounting, Mongo BSON plus indexes, and Upstash's character/record accounting differ.
- **Comfortable:** Aiven 20 GB and Algolia's 1M-record allowance. Algolia fails on likely query volume, not corpus size.
- **Borderline:** Upstash reaches 200k records but only 20k monthly operations and lacks required browse semantics. Atlas M0 has 512 MB inclusive storage and shared resources; a full representative import is mandatory.
- **Impossible now:** Bonsai's 35k documents, Searchly's 20 MB, and any unverified free plan below 82,252 documents.
- **Growth check:** At the current document count, a plan should ideally allow at least 250k records and multiple times the measured index footprint to absorb Lucene overhead, reindexing, schema changes and several years of additions. Only Aiven clears both tests without a request-meter problem, and it still fails the production reliability test.

### Authoritative sources checked

- Aiven: [free-tier limits/idle/SLA](https://aiven.io/docs/products/opensearch/concepts/opensearch-free-tier), [generic pricing conflict](https://aiven.io/docs/platform/concepts/service-pricing), [creation/region group](https://aiven.io/docs/products/opensearch/howto/create-free-tier-opensearch), [backups](https://aiven.io/docs/platform/concepts/service_backups).
- Algolia: [pricing](https://www.algolia.com/pricing), [Build plan](https://www.algolia.com/pricing/build-plan), [support exclusion](https://support.algolia.com/hc/en-us/articles/10109389226769-How-can-I-upgrade-to-a-plan-with-Support).
- Upstash: [Search pricing](https://upstash.com/pricing/search), [FAQ and document limits](https://upstash.com/docs/search/help/faq), [filtering](https://upstash.com/docs/search/features/filtering), [search API](https://upstash.com/docs/search/sdks/ts/commands/search).
- MongoDB: [Atlas pricing](https://www.mongodb.com/pricing), [Free limits](https://www.mongodb.com/docs/atlas/reference/free-shared-limitations/), [cluster/region comparison](https://www.mongodb.com/docs/atlas/manage-clusters/), [production SLA](https://www.mongodb.com/docs/atlas/production-notes/).
- Other providers are linked directly in the exclusion tables above.

---

## References (code)

- Schema: `Class-Libraries/RedditPodcastPoster.Search/EpisodeSearchRecord.cs`
- Public proxy: `Api/src/search.ts`, `Api/src/getPageDetails.ts`
- Consumers: `website/cultpodcasts/src/app/search-api/`, `podcast-api/`, `subject-api/`, `episode.service.ts`
- Indexing: `RedditPodcastPoster.EntitySearchIndexer`, `Console-Apps/CreateSearchIndex`
- Slimming plan: [search-index-slimming-plan.md](./search-index-slimming-plan.md)
- Migration assumes Azure Search: [migration/README.md](./migration/README.md), [migration/azure-functions-microservices-reference-architecture.md](./migration/azure-functions-microservices-reference-architecture.md)
