# Catalogue content types — Azure Search storage impact (Phase 0)

**Status:** planning / read-only analysis. No index or Cosmos writes.  
**Related:** [catalogue-content-types-epic.md](./catalogue-content-types-epic.md), [search-index-slimming-plan.md](./search-index-slimming-plan.md) §3, ADR [0003](./adr/0003-unified-playable-search-document.md).

---

## Executive summary

| Question | Answer |
|----------|--------|
| Will unified `contentKind` search **by itself** push the Free-tier **50 MB** cap? | **Unlikely** for phased rollout (extra fields + modest new rows), **if** index slimming is live first and schema changes are **in-place** on the existing index. |
| What is the **primary quota risk**? | **Dual live indexes** during blue/green (summed `storageSize`) — same class of blocker as slimming §8. |
| What is the **secondary quota risk**? | **Large-scale NewsReport** indexing (many new rows with searchable descriptions) without a cap or SKU plan. |
| Do NewsOrganisation joins double row count? | **No** — only **NewsReports** (playables) are indexed; organisation name is **denormalized** at index time (`newsOrganisationName` / `parentName`). |

**Recommended sequencing:** (1) ship index slimming cutover if not already live → ~40 MB baseline; (2) **add** `contentKind` (+ kind-specific parent fields) **in place** on `cultpodcasts`; (3) index new TvShow/Movie/News rows via merge/upload as flags enable submits; (4) **migration tools** **move** mis-filed Podcast/Episode rows (news-station YouTube, movies, TV) — search **swap** (delete Episode doc + upload new `contentKind` doc) keeps Free-tier **doc count ~flat**.

---

## Baseline (authoritative measurements)

From [search-index-slimming-plan.md](./search-index-slimming-plan.md) §3A — **2026-07-17**, production Free tier, index `cultpodcasts`:

| Metric | Value |
|--------|-------|
| `documentCount` | **82,252** |
| `storageSize` | **51,462,953 B ≈ 49.08 MB** (~98% of 50 MB cap) |
| Tier | Free (~50 MB **shared across all indexes** on the service) |

**Post-slimming estimate** (if URL→ID, YT image derivation, drop `explicit`, duration trim are live — code in `EpisodeSearchRecord` / `ToEpisodeSearchRecord` reflects slim shape): **≈40.4 MB** (~17–18% reduction ≈ **8.7 MB** headroom).

**Re-measure before Phase 2:** `GET …/indexes/cultpodcasts/stats` (or portal) after any slimming cutover. All estimates below use **~600 B/doc** all-in (retrievable values + inverted/filter/facet structures) as a working average from the Jul 2026 baseline.

---

## Three quota levers (your concern)

Azure AI Search `storageSize` is **not** wire JSON size. Persistent storage scales with **document count**, **field values**, and **field attributes** (searchable / filterable / facetable). See slimming plan §3.1.

### 1. Extra fields on existing podcast rows (~82k)

Proposed additions for a unified playable document:

| Field | Attributes (proposed) | On Podcast docs? | Storage note |
|-------|----------------------|------------------|--------------|
| `contentKind` | filterable + facetable | Yes — value `Podcast` | New facet bucket on **all** docs; small values (~7 B). Estimate **+0.5–1.5 MB** service-wide. |
| `parentName` | searchable + filterable + facetable | **No** — use existing `podcastName` | Avoid duplicating ~1.5 MB of parent labels on 82k rows. |
| `parentId` | filterable (optional) | **Defer** for Podcast | Episode `id` + `podcastName` suffice today; saves ~3 MB if omitted. |
| Kind-specific (`tvShowName`, …) | — | Empty on Podcast | No cost on podcast rows. |

**Subtotal (existing rows only): ≈0.5–2 MB** — low risk if we **do not** duplicate `podcastName` into `parentName`.

Renaming `episodeTitle` → generic `title` would be a **consumer migration**, not a meaningful quota win (slimming plan §3.3: key names ≈ negligible on disk).

### 2. New indexed rows (playables)

Only **playable** documents are indexed (Episodes, TvShowEpisodes, Movies, NewsReports). Parent containers (Podcasts, TvShows, NewsOrganisations) are join sources at index time — **not** separate search rows.

| Source | Phase 2 initial volume | Storage @ ~600 B/doc | Notes |
|--------|------------------------|----------------------|-------|
| New submits (flagged streaming) | ~0 → hundreds/year | +0.3–1.2 MB | Low until flags widen. |
| **Migrate** mis-filed Episodes → TvShow/Movie/NewsReport | Same GUIDs / same ~N docs | **~0 MB net** if **swap**; **+N×~600 B** if copy-without-delete | **Required** work — news-station YT + movies + TV already in Podcasts. Prefer swap. |
| NewsReports from **new** submit path | Growth after flag | Scales with ingest | BBC `/news/` not submit today; YT news arrives via **migration** first. |
| News at scale if migrate **duplicates** without delete | e.g. +10k–50k extra | **≈6–30 MB** | **Would breach** Free tier — migration **must** delete Episode search docs. |

**NewsOrganisation join:** adds one denormalized string per report (~20 B avg parent name) — **included in the row estimate**, not an extra row.

**Subtotal (realistic phased rollout): +0.5–3 MB.**  
**Subtotal (aggressive news indexing): +6–30 MB** — plan capacity before enabling news at volume.

### 3. Dual indexes during migration

From slimming plan §8.1 — **`storageSize` sums across all indexes** on the service.

| Situation | Both indexes live | Fits Free 50 MB? |
|-----------|-------------------|------------------|
| Pre-slimming (~49 MB) × 2 | ~98 MB | **No** |
| Post-slimming (~40 MB) × 2 | ~80 MB | **No** |
| In-place schema **add fields** only | 1 index | **Yes** (single index grows by §1+§2) |
| Blue/green `cultpodcasts` + `cultpodcasts-v2` | 2 full copies | **Requires SKU bump** (Basic S1 ≈ 2 GB) or delete-old-then-rebuild downtime |

**Verdict:** Treat dual-index blue/green on Free tier as a **blocker** for both slimming and content-types work. Prefer:

1. **In-place** add `contentKind` (+ nullable kind-specific parent fields) to the live index schema.
2. **MergeOrUpload** existing episodes with `contentKind: Podcast`.
3. If a **new** index is unavoidable (immutable field change), use **temporary SKU bump** or **delete-then-rebuild** — same playbook as slimming §8.

Combining slimming v2 **and** content-types in **one** new index still implies **one** blue/green window — not two separate dual-index periods.

---

## Unified document shape — storage-conscious choices

See ADR [0003](./adr/0003-unified-playable-search-document.md). Summary tuned for quota:

| Decision | Storage impact |
|----------|----------------|
| One index, discriminated by `contentKind` | Single quota pool; one facet pipeline. |
| Keep `podcastName` for `Podcast` only | Avoid ~1.5 MB duplicate parent labels. |
| `parentName` (or `tvShowName` / `movieName` / `newsOrganisationName`) on non-Podcast rows only | Pay parent-label cost only on new rows. |
| Reuse `episodeTitle` / `episodeDescription` for all kinds (display headline + blurb) | No second searchable title field; same inverted-index cost class as today. |
| Reuse `svc` compact encoding for streaming URLs on TvShow/Movie/News | Same pattern as episodes; avoid per-platform URL columns. |
| Do **not** index NewsOrganisations as their own documents | Saves N_org rows; join at indexer. |
| Truncate descriptions to existing `DescriptionSize` (230) | Caps worst-case news/TV text growth. |

---

## Scenario matrix (will we exceed 50 MB?)

Assumes **post-slimming baseline ≈40 MB**, **single index**, in-place `contentKind` add.

| Scenario | Est. total | vs 50 MB |
|----------|------------|----------|
| A. Slimming live + `contentKind` on 82k + phased streaming submits | ~41–43 MB | **Safe** |
| B. A + migrate ~N mis-filed (search **swap**, flat doc count) | ~41–43 MB | **Safe** (contentKind attribute cost only) |
| C. B + 5k NewsReports (230-char descriptions) | ~45–47 MB | **Tight** — re-measure |
| D. B + 20k NewsReports | ~52–58 MB | **Over** — SKU or cap news |
| E. Dual full index (40 + 40 MB) on Free | ~80 MB | **Fails** — do not |
| F. No slimming (49 MB) + content-types fields + 5k news | ~52+ MB | **Over** — slim first |

---

## Verification checklist (before Phase 2 implementation)

- [ ] Read live `documentCount` + `storageSize` on `cultpodcasts` (post-slimming).
- [ ] Confirm slimming cutover complete or scheduled **before** large new row classes.
- [ ] Cosmos **read-only** counts: episodes with `svc` / BBC iPlayer / Netflix keys (backfill sizing) — dry-run only.
- [ ] Build **sample** index on non-prod with 1k mixed playables + `contentKind`; compare `storageSize` / doc (empirical).
- [ ] Decide news indexing cap / description limit / SKU before Phase 4 news submit.
- [ ] Document cutover: in-place vs new index; if new index, plan SKU bump or downtime rebuild.

---

## Open decisions (storage-related)

1. **Preserve playable GUID** on Cosmos/search move? (**Recommended yes** — same search `id`, change `contentKind` + parent fields.)
2. **Dry-run sizing** — count Episode rows under news-station / movie / TV candidate Podcasts before first `--apply`.
3. **Single new index name** — combine remaining slimming deltas + `contentKind` in one cutover to avoid two dual-index windows?
4. **Re-measure cadence** — alert when `storageSize` > 45 MB (90% of Free cap)?

---

## References

- [search-index-slimming-plan.md](./search-index-slimming-plan.md) §3, §8
- [catalogue-content-types-epic.md](./catalogue-content-types-epic.md)
- ADR [0002](./adr/0002-separate-catalogue-content-containers.md), [0003](./adr/0003-unified-playable-search-document.md)
- Microsoft: [Index size and schema](https://learn.microsoft.com/azure/search/search-what-is-an-index#physical-structure-and-size), [Capacity planning](https://learn.microsoft.com/azure/search/search-capacity-planning)
