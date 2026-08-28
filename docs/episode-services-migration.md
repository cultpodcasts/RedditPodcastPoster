# Episode `services` + `ids` — rollout and data migration <!-- pragma: allowlist secret -->

This is the plan for moving from split `urls` / `images` / top-level platform ids to: <!-- pragma: allowlist secret -->

- `services.{key}.{url,image}` — catalog of listen/watch destinations
- `ids.{spotify,apple,youtube}` — presence of reconstructable Spotify / Apple / YouTube <!-- pragma: allowlist secret -->

Do **not** treat this document as approval to write production Cosmos, recreate the search index, or deploy Workers/Pages.

Risk assessment (functionality + data loss, including full-upsert and publish-order traps): [episode-services-risk.md](episode-services-risk.md).

Ops runbook (human gates, backups, checks): [episode-services-ops-runbook.md](episode-services-ops-runbook.md).

Deploy plan with diagram and post-step checks: [episode-services-deploy-plan.md](episode-services-deploy-plan.md).

## Why a phased plan

Typed `Episode` deserialize already calls `EpisodeServicePresence.Hydrate`. That means **in-memory** code after this branch sees `services` / `ids` even when the Cosmos document does not store them yet. <!-- pragma: allowlist secret -->

The search indexer SQL still reads `e.urls.*` and top-level `spotifyId` / `appleId` / `youTubeId`. Matching still uses those top-level fields. So dual-write (`SyncLegacy` + `SyncIds`) stays on until every reader is switched. <!-- pragma: allowlist secret -->

The published feed / public episode JSON on this branch is `ids` + `services` only. Older R2 objects still have flat `spotify` / `apple` / `youtube` URL fields. Website helpers keep reading those leftover fields until the feed is republished. <!-- pragma: allowlist secret -->

## Tested migration code

| Piece | Role |
| --- | --- |
| `EpisodeServiceDocumentMigration.NeedsBackfill(JsonElement)` | Decide from **raw JSON** (not a hydrated `Episode`) whether `services` / `ids` are incomplete | <!-- pragma: allowlist secret -->
| `EpisodeServiceDocumentMigration.SelectDocumentsToBackfill` | Dry-run candidate list (`podcastId` + episode `id`) | <!-- pragma: allowlist secret -->
| `EpisodeServiceDocumentMigration.Apply(Episode)` | Hydrate + dual-write; returns whether the persisted shape changed (idempotent) | <!-- pragma: allowlist secret -->
| `EpisodeServiceBackfillProcessor` | Dry-run count, or load/save only candidates. Default is **not** apply | <!-- pragma: allowlist secret -->

Tests: `EpisodeServiceDocumentMigrationTests`, `EpisodeServiceBackfillProcessorTests`. <!-- pragma: allowlist secret -->

Selection **must** use raw documents. After `OnDeserialized`, a typed `Episode` always looks migrated, so `GetAll()` cannot be the candidate query. <!-- pragma: allowlist secret -->

Cosmos scan (when a host is wired; dry-run first):

```sql
SELECT c.id, c.podcastId, c.spotifyId, c.appleId, c.youTubeId, c.urls, c.ids, c.services <!-- pragma: allowlist secret -->
FROM c
```

Then `NeedsBackfill` on each item. A cheaper first pass is `NOT IS_DEFINED(c.services) OR NOT IS_DEFINED(c.ids)` — that **misses** documents that already have a partial `services` map (e.g. YouTube only) while `urls.spotify` is still set. Prefer the full `NeedsBackfill` filter. <!-- pragma: allowlist secret -->

## Phase 0 — already on this branch (safe to merge)

Code dual-reads and dual-writes. No bulk Cosmos write required for correctness of new writes:

- New/updated episodes persist `services` + `ids` **and** `urls` / `images` / top-level ids <!-- pragma: allowlist secret -->
- Search SQL and matching keep working
- Website reads `services`, then `ids`, then leftover named URL fields on old feed JSON <!-- pragma: allowlist secret -->

## Phase 1 — roll out code (order)

Deploy **without** deleting legacy fields.

1. **Azure Functions / indexer / publisher (this repo)**  
   First. Homepage publisher and public DTOs emit `ids` + `services`. Cosmos writes dual-write. <!-- pragma: allowlist secret -->

2. **Api Worker**  
   GET feed still returns R2 bytes (no reshape). OpenAPI allows both `ids`/`services` and leftover named URL fields during the overlap. <!-- pragma: allowlist secret -->

3. **Website**  
   After or with Api. Helpers tolerate old feed JSON.

4. **Republish the feed**  
   Admin `publish` homepage (or wait for the next scheduled publish) so R2 matches `ids` + `services`. Until then, leftover URL fields keep cards working. <!-- pragma: allowlist secret -->

5. **Azure Search**  
   Add retrievable `svc` (manual index change — not this PR). Reindex **after** Functions that write `svc` are live. Keep `spotifyId` / `youtubeId` / `appleId` / `podcastAppleId` / legacy `bbc` / `internetArchive`. <!-- pragma: allowlist secret -->

Do not deploy website-only against an old feed if leftover URL fallbacks are removed.

## Phase 2 — migrate Cosmos data

1. Export or page episode JSON (or run a console host that feeds `EpisodeServiceBackfillProcessor`). <!-- pragma: allowlist secret -->
2. **Dry-run** (`apply: false`): record `Candidates`. Spot-check a few ids. <!-- pragma: allowlist secret -->
3. **Apply** (`apply: true`) in batches. Only documents `NeedsBackfill` selected; `Apply` no-ops if already complete after load.
4. Re-run dry-run; candidates should be ~0 (except in-flight writes).
5. Republish feed + reindex search if those still show gaps.

`Apply` keeps `urls` and top-level ids. This is a **backfill**, not a delete. <!-- pragma: allowlist secret -->

Do not run apply against production from an agent session unless that write is explicitly requested.

## Phase 3 — stop dual-write (later PR)

Only after all of these read `services` / `ids`: <!-- pragma: allowlist secret -->

- Cosmos SQL in `CreateSearchIndex` (today still `e.urls.*`)
- Matching / enrichers (today `Episode.SpotifyId` etc.) <!-- pragma: allowlist secret -->
- Curator forms (today still PATCH `urls`)

Then, in a separate PR with its own tests:

1. Stop `SyncLegacy` on serialize
2. Stop writing top-level ids once matching reads `ids` <!-- pragma: allowlist secret -->
3. Optional strip job for `urls` / `images` — **new** tested `NeedsStrip` + dry-run + apply, never combined with Phase 2

## Rollback

- **Code:** previous website still understands leftover URL fields on old R2; previous Functions still read `urls`.
- **Data apply:** documents gain `services`/`ids` but keep `urls`. Reverting code is safe. There is no automatic delete of `services`. <!-- pragma: allowlist secret -->

## Done when

- [ ] Phase 1 deployed in the order above
- [ ] Dry-run then apply backfill; second dry-run ~0
- [ ] Feed republished; search reindexed with `svc`
- [ ] Phase 3 tracked separately; dual-write still on until that PR
