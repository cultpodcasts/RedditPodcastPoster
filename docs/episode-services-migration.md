# Episode `services` + `ids` — rollout and data migration

This is the plan for moving from split `urls` / `images` / top-level platform ids to:

- `services.{key}.{url,image}` — catalog of listen/watch destinations
- `ids.{spotify,apple,youtube}` — presence of reconstructable Spotify / Apple / YouTube

Do **not** treat this document as approval to write production Cosmos, recreate the search index, or deploy Workers/Pages.

Risk assessment (functionality + data loss, including full-upsert and publish-order traps): [episode-services-risk.md](episode-services-risk.md).

Ops runbook (human gates, backups, checks): [episode-services-ops-runbook.md](episode-services-ops-runbook.md).

Deploy plan with diagram and post-step checks: [episode-services-deploy-plan.md](episode-services-deploy-plan.md).

## Why a phased plan

Phase 3 (this freeze branch): leftover members are **not** on typed `Episode`. `NormalizeCatalog` does not copy leftover `urls` into the catalog. App matching and writers use nested `ids` / `services`. Leftover JSON is still dual-**read** by the **EpisodeServiceBackfill CLI** (`MergeRawLeftoverIntoCatalog`) and by search indexer SQL until leftover keys wither. Optional `NeedsStrip` is later.

The published feed / public episode JSON is `ids` + `services` only. Older R2 objects may still have flat named URL fields. Website helpers keep reading those leftover fields until the feed is republished.

## Tested migration code

| Piece | Role | Assembly |
| --- | --- | --- |
| `EpisodeServiceDocumentMigration.NeedsBackfill(JsonElement)` | Decide from **raw JSON** (not a hydrated `Episode`) whether `services` / `ids` are incomplete | `EpisodeServiceBackfill` CLI |
| `EpisodeServiceDocumentMigration.SelectDocumentsToBackfill` | Dry-run candidate list (`podcastId` + episode `id`) | CLI |
| `EpisodeServiceDocumentMigration.Apply(Episode)` | `NormalizeCatalog` + nested ids; in-memory only. Does not write leftover members | CLI |
| `EpisodeServiceBackfillProcessor` | Dry-run count, or surgical `/services` `/ids` patch. Default is **not** apply | CLI |
| `BackFillEpisodeRepository` | Cosmos `PatchServicesAndIds` — not on production `IEpisodeRepository` | CLI |

Tests: `Console-Apps/EpisodeServiceBackfill.Tests` (`EpisodeServiceDocumentMigrationTests`, `EpisodeServiceBackfillProcessorTests`, leftover document tests). **Do not** reference this CLI from Class-Library or Cloud `*Tests` projects.

Selection **must** use raw documents. Typed `Episode` has no leftover members, so `GetAll()` cannot see `urls` / top-level ids / `images`.

Cosmos scan (when a host is wired; dry-run first):

```sql
SELECT c.id, c.podcastId, c.spotifyId, c.appleId, c.youTubeId, c.urls, c.ids, c.services
FROM c
```

Then `NeedsBackfill` on each item. A cheaper first pass is `NOT IS_DEFINED(c.services) OR NOT IS_DEFINED(c.ids)` — that **misses** documents that already have a partial `services` map (e.g. YouTube only) while `urls.spotify` is still set. Prefer the full `NeedsBackfill` filter.

## Phase 0 — historical (superseded by Phase 3 on this branch)

Phase 0 shipped dual-read and dual-write. **This freeze branch no longer dual-writes leftover members.** Keep leftover JSON in Cosmos until wither/strip.

Historical notes:

- New/updated episodes persist `services` + `ids` **and** `urls` / `images` / top-level ids
- Search SQL and matching keep working
- Website reads `services`, then `ids`, then leftover named URL fields on old feed JSON

## Phase 1 — roll out code (order)

Deploy **without** deleting legacy fields.

1. **Azure Functions / indexer / publisher (this repo)**  
   First. Homepage publisher and public DTOs emit `ids` + `services`. Cosmos writes dual-write.

2. **Api Worker**  
   GET feed still returns R2 bytes (no reshape). OpenAPI allows both `ids`/`services` and leftover named URL fields during the overlap.

3. **Website**  
   After or with Api. Helpers tolerate old feed JSON.

4. **Republish the feed**  
   Admin `publish` homepage (or wait for the next scheduled publish) so R2 matches `ids` + `services`. Until then, leftover URL fields keep cards working.

5. **Azure Search**  
   Add retrievable `svc` (manual index change — not this PR). Reindex **after** Functions that write `svc` are live. Keep `spotifyId` / `youtubeId` / `appleId` / `podcastAppleId` / legacy `bbc` / `internetArchive`.

Do not deploy website-only against an old feed if leftover URL fallbacks are removed.

## Phase 2 — migrate Cosmos data

1. Export or page episode JSON (or run a console host that feeds `EpisodeServiceBackfillProcessor`).
2. **Dry-run** (`apply: false`): record `Candidates`. Spot-check a few ids.
3. **Apply** (`apply: true`) in batches. Only documents `NeedsBackfill` selected; `Apply` no-ops if already complete after load.
4. Re-run dry-run; candidates should be ~0 (except in-flight writes).
5. Republish feed + reindex search if those still show gaps.

`Apply` keeps `urls` and top-level ids. This is a **backfill**, not a delete.

Do not run apply against production from an agent session unless that write is explicitly requested.

## Phase 3 — leftover DTO retire (this branch)

Typed `Episode` no longer has leftover members. Writers stop dual-write. Search indexer SQL prefers `e.ids.*` / `e.services.*` with leftover JSON as **read fallback**. Curator PATCH may still send leftover-shaped `urls` on the **request** DTO; the applier maps those onto catalog.

Optional strip job for leftover Cosmos keys — **later** `NeedsStrip` + dry-run + apply, never combined with Phase 2.

## Rollback

- **Code:** previous website still understands leftover URL fields on old R2; previous Functions still read `urls`.
- **Data apply:** documents gain `services`/`ids` but keep `urls`. Reverting code is safe. There is no automatic delete of `services`.

## Done when

- [ ] Phase 1 deployed in the order above
- [ ] Dry-run then apply backfill; second dry-run ~0
- [ ] Feed republished; search reindexed with `svc`
- [ ] Phase 3 leftover DTO retire is on this freeze branch; leftover JSON withers on Save; strip is later
