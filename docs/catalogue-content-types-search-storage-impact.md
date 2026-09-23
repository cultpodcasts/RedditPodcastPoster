# Catalogue content types — Azure Search storage impact (Phase 0)

**Status:** planning / read-only analysis. No index or Cosmos writes.  
**Updated:** 2026-09-23 — Movie → **Film**; no parent name on Film; search default all-kinds (ADR-0003).  
**Related:** [catalogue-content-types-epic.md](./catalogue-content-types-epic.md), [search-index-slimming-plan.md](./search-index-slimming-plan.md) §3, ADR [0003](./adr/0003-unified-playable-search-document.md).

---

## Executive summary

| Question | Answer |
|----------|--------|
| Will unified `contentKind` search **by itself** push the Free-tier **50 MB** cap? | **Unlikely** for phased rollout **if** slimming is live (or headroom exists) and schema changes are **in-place**. |
| Primary quota risk? | **Dual live indexes** during blue/green (summed `storageSize`). |
| Secondary quota risk? | Large-scale **NewsReport** indexing without a cap or SKU plan. |
| Do parent joins double row count? | **No** — only playables indexed; Film has **no** parent join. |
| Migration of mis-filed Episode → Film/Tv/News? | **~flat doc count** if search **swap** (same GUID); **+N docs** if copy-without-delete. |

**Recommended sequencing:** (1) measure live `storageSize` during Phase 2; (2) build **fresh index** with new projection + remaining slimming (OPEN-011) via SKU bump or delete-rebuild; (3) point Worker at new index; (4) migration tools **swap** docs by kind.

---

## Baseline (re-measure — do not treat as current)

From [search-index-slimming-plan.md](./search-index-slimming-plan.md) §3A — **2026-07-17**, Free tier, index `cultpodcasts`:

| Metric | Value (Jul 2026) |
|--------|------------------|
| `documentCount` | **82,252** |
| `storageSize` | **≈49.08 MB** (~98% of 50 MB) |

Post-slimming estimate (if URL→ID / `svc` / drop `explicit` live): **≈40 MB** headroom. **Verify before Phase 2.**

Working average used below: **~600 B/doc** all-in.

---

## Quota levers

### 1. Extra fields on existing podcast rows (~82k)

| Field | On Podcast docs? | Note |
|-------|------------------|------|
| `contentKind` | Yes — `Episode` | Facetable; ~0.5–1.5 MB service-wide |
| `title` / `description` / `seriesName` | Yes (projected; may overlap legacy during cutover) | Prefer add-then-retire legacy keys |
| `seriesDescription` | Yes — **new** parent text | Truncate; main new quota lever on 82k rows |
| Film series fields | N/A — omit on Film | No fake parent |

**Subtotal (existing rows): ≈0.5–2 MB.**

### 2. New / migrated indexed rows

Only **playables** (Episodes, TvShowEpisodes, Films, NewsReports).

| Source | Storage note |
|--------|----------------|
| New flagged submits | Low until flags widen |
| **Migrate** Episode → Film/Tv/NewsReport | **~0 MB net** if swap same GUID; disaster if duplicate |
| News at scale without delete | Can breach Free tier |

**Film rows:** `title` / `description` only; no `seriesName` / `seriesDescription`.

### 3. Dual indexes

| Situation | Fits Free 50 MB? |
|-----------|------------------|
| In-place field add | **Yes** (growth = §1+§2) |
| Two full copies (~40+40 MB) | **No** — SKU bump or delete-rebuild |

---

## Scenario matrix (post-slimming ~40 MB baseline assumed)

| Scenario | Est. total | vs 50 MB |
|----------|------------|----------|
| A. `contentKind` on 82k + phased submits | ~41–43 MB | Safe |
| B. A + migrate via **swap** | ~41–43 MB | Safe |
| C. B + 5k extra NewsReports (no swap) | ~45–47 MB | Tight |
| D. Dual full index | ~80 MB | **Fail** |
| E. No slimming (~49 MB) + fields + news | ~52+ MB | **Over** — slim/re-measure first |

---

## Verification checklist (before Phase 2)

- [ ] Live `documentCount` + `storageSize` on `cultpodcasts`
- [ ] Confirm slimming state
- [ ] Cosmos read-only sizing for migration candidates (dry-run)
- [ ] Sample mixed-kind index on non-prod if practical
- [ ] Cutover plan: in-place vs new index + SKU

## Open decisions (storage-related)

1. Preserve GUID on migrate? **Yes** (signed).  
2. Dry-run sizing before first `--apply`.  
3. Combine remaining slimming + `contentKind` in one cutover if new index unavoidable.  
4. Alert when `storageSize` > 45 MB?

## References

- [catalogue-content-types-epic.md](./catalogue-content-types-epic.md)
- ADR [0002](./adr/0002-separate-catalogue-content-containers.md), [0003](./adr/0003-unified-playable-search-document.md)
- [search-index-slimming-plan.md](./search-index-slimming-plan.md)
