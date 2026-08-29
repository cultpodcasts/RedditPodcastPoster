# Episode services Phase 3 — retire leftover members

Phases 0–2 (Functions dual-write deploy + Cosmos catalog backfill + search `svc`) are **done**. This file is the operator plan for leftover DTO retire. It is **not** approval to merge [#966](https://github.com/cultpodcasts/RedditPodcastPoster/pull/966), deploy Wrangler/Pages, recreate search, or run a strip `--apply`.

Watch file: [episode-services-deploy-plan.md](episode-services-deploy-plan.md). Full-document `Save()` still withers leftover JSON keys: [episode-services-risk.md](episode-services-risk.md) D1 / D8.

**Updated:** 2026-08-29 14:07 BST.

## Can we proceed — no data loss, quality code?

**Yes, with the gates below.** Catalog listen URLs, nested ids, and cover art on `services.*.image` are the source of truth and are already on Cosmos. Leftover `urls` / top-level ids / `images` are retired from typed `Episode`; they **wither** (omitted on the next full `Save`), they are not stripped this slice.

| Gate | Status |
| --- | --- |
| Phase 2 `--all --apply` | Done 28 Aug: scanned 97306, saved 97286, mismatches 0, spot-check 1000/1000 |
| Ingest since 21:00 BST 28 Aug (`_ts` > `1787947200`) | 39 documents, **0** `NeedsBackfill` |
| Full-container dry-run 29 Aug | scanned **97345**, candidates **0** |
| Production Functions | Still **pre–Phase 3** (28 Aug script-deploy). New indexer Saves still dual-write leftover JSON **and** catalog |
| #966 | Origin **`79793077`**. Leftover DTO retire, CLI leftover subclass, backfill off Models/Persistence, STJ hooks off Episode. #966 open. Do not merge. |
| Catalog / Indexer window | **15:00 BST** `--since-ts` (21:00 BST 28 Aug), apply only if candidates, then **ask** before Functions. Next hourly after that is **16:03 BST**. |

**Data-loss posture**

- Surgical backfill patches **only** `/services` and `/ids`. It does not delete leftover JSON, title, description, `lang`, or guests.
- Typed `Episode` no longer has leftover members, so a **full upsert** after Phase 3 Functions omit leftover keys. Catalog stays. That is wither, not strip.
- Search indexer SQL reads **catalog only** (`e.ids.*` / `e.services.*`). Phase 2 backfill made leftover JSON redundant for search. Compact search fields stay.
- Do **not** run `NeedsStrip` this slice.
- Keep catalog current: after each hourly/discovery window until Phase 3 Functions are live, `--since-ts` dry-run; `--apply` only if candidates > 0.

**Quality posture**

- App writers: catalog + nested ids only. `SyncLegacy` is gone. F4 clear-slot maps empty request `urls` onto `Upsert(null)` + clear nested ids.
- Review should-fix done: `Classify()` merges leftover before empty-catalog skip; duplicate-finder SQL prefers `e.ids.*`; docs no longer claim dual-write is on.
- **Before Functions deploy:** leftover CLI is on origin. Catalog `--since-ts` must be clean (or applied). Then you **name** Indexer / `deploy functions & clis`.

## Canonical vs leftover

| Keep (source of truth) | Retire (wither on full `Save`) |
| --- | --- |
| `services.{key}.{url,image}` | `urls.*` |
| nested `ids.{spotify,apple,youtube}` | top-level `spotifyId` / `appleId` / `youTubeId` |
| Search **index** compact fields `spotifyId` / `youtubeId` / `appleId` / `image` / `svc` | `images.youtube` / `spotify` / `apple` / `other` |

Cover art coalesces from `services.*.image` using `ServiceCatalog.ImageCoalesceOrder` (YouTube → Spotify → Apple → remaining catalog keys). Application code **never writes** leftover members. Search indexer SQL and duplicate-finder SQL read catalog only.

```mermaid
flowchart TD
  P2["Phase 2 backfill done"]
  P3a["3a Domain: leftover members off Episode"]
  P3b["3b Writers and coalescers use services"]
  P3c["3c Search indexer SQL catalog only"]
  P3cli["CLI leftover subclass for backfill reads"]
  P3d["3d Script-deploy Functions when named"]
  P3keep["Keep catalog current: since-ts after ingest"]
  P3e["3e Later optional NeedsStrip"]
  P2 --> P3a --> P3b --> P3c --> P3cli --> P3d --> P3keep --> P3e
```

## PR state ([#966](https://github.com/cultpodcasts/RedditPodcastPoster/pull/966))

Branch: `cursor/episode-service-links-18b4`. **Do not merge.**

| Commit | What |
| --- | --- |
| `f4e05758` | Retire leftover `Episode` members; catalog writers; indexer SQL dual-read |
| `d0dbad11` | Classify leftover JSON; duplicate-finder nested ids; docs match Phase 3 |
| `29ab3ae9` | Leftover CLI + `BackFillEpisodeRepository`; `PatchServicesAndIds` off live repo |
| `79793077` | Remove Episode STJ hooks; backfill tests in `EpisodeServiceBackfill.Tests`; no RPP test → CLI refs |

Functions on production are **not** this commit yet. Live indexer still dual-writes leftover JSON.

## PR review (architect)

**Verdict:** ship-quality for Phase 3 if Cosmos catalog is complete (it is: 0 remaining candidates).

Landed in `d0dbad11`:

- `Classify()` was labelling leftover-only JSON `services_and_ids_both_null` because typed deserialize ignores leftover. Apply/`TryCreate` was already correct. Classify now merges leftover first.
- Duplicate-finder / search duplicate SQL prefer `e.ids.*` with leftover id fallback.
- Docs/comments no longer claim `Hydrate` / `SyncLegacy` dual-write is current. F4 is fixed on this branch.

Keep (not defects):

- Admin request DTO leftover-shaped PATCH → catalog (website form later).
- Optional `NeedsStrip` later.

## 3a — Episode DTO

Leftover properties are **off** `Episode`: `Urls`, top-level ids, `Images`. Leftover JSON is ignored on deserialize and omitted on serialize (wither). `NormalizeCatalog` (drop retired catalog key `other`, empty `ids`, empty `services`) runs on write/`Upsert` only — not STJ hydrate hooks.

## 3b — App writers and readers

Inbound admin `Urls` / `Images` on the **request** DTO still map onto `EpisodeServicePresence.Upsert` + nested ids. Admin GET may **project** those shapes from the catalog. Enrichers, posters, search image, shortener, and Cosmos LINQ categorisers use catalog + nested ids only.

Website PATCH may still send leftover-shaped fields this slice. Stopping that form payload is a later website PR.

## 3c — Search indexer SQL (not index recreate)

Indexer query is **catalog only**: `e.ids.*` / `e.services.*`. No leftover `e.urls.*`, top-level ids, or `e.images.*`. Compact `image` tokens stay lossless (`y`/`s`/`a` + full URL). Do not drop search fields. Push the indexer definition on the next **named** Functions deploy (CreateSearchIndex is a console; Azure Search indexer SQL lives with Functions datasource — confirm what the Indexer app actually pushes vs `CreateSearchIndex`).

Hourly **Indexer** function uses in-process `ToEpisodeSearchRecord` / catalog coalesce, not leftover DTO members.

## Backfill CLI

Console `EpisodeServiceBackfill`:

- Production `Episode` has no leftover members.
- CLI **`LeftoverEpisodeDocument : Episode`** adds read-only `urls`, top-level ids, `images` for Cosmos JSON deserialize. Never save that type.
- `LeftoverEpisodeCatalogPatchSource` builds surgical `services`/`ids` patches. CLI tests use `JsonEpisodeCatalogPatchSource` (`JsonElement` merge) or leftover parse. Class-Library / Cloud tests do **not** reference this CLI.
- `--since-ts` classifies (and with `--apply` patches) by `_ts`. Does not overwrite `episode-service-backfill-patches.jsonl`.
- Default is dry-run. `--apply` only when named.

## Keep catalog current (ingest after 21:00 BST 28 Aug)

Indexer/discovery on **current** production Functions still dual-write leftover + catalog, so new rows should already have `services`/`ids`. Measured:

- Window `_ts` > 21:00 BST 28 Aug: **39** hits, **0** candidates.
- Full scan 29 Aug: **97345** / **0**.

Until Phase 3 Functions are live, after each hourly/discovery: `--since-ts <unix>` dry-run. Apply only if `NeedsBackfill` > 0. After Phase 3 Indexer is live, new Saves wither leftover on full upsert; catalog-only writes need no leftover merge.

## 3d — Script-deploy Indexer (named)

Phrase **deploy functions & clis** (or an explicit “deploy Indexer”) is the deploy approval. Order remains Indexer → Discover → Api.

**15:00 BST 29 Aug:** classify `_ts` since 21:00 BST 28 Aug, apply only if candidates, then wait for a **named** Indexer script-deploy. Blob `lastModified` is deploy truth.

Soak after deploy: `AppRequests` for `HourlyOrchestration` / `activity:Indexer` (not traces alone), curator clear-slot, tweet/Bsky, one full `Save` omits leftover JSON.

## 3e — later

Optional `NeedsStrip` dry-run / `--apply`. Not the same PR or day as 3a–3d.

## Done when

- No leftover url/id/images members on `Episode`; no app writes to them.
- Cover art coalesces from catalog services.
- Indexer SQL is catalog-only (`services`/`ids`); leftover JSON is not read.
- CLI leftover subclass is the only leftover-member type; not persisted.
- Phase 3 Functions script-deployed when named; leftover JSON withers on Save.
- Catalog stays complete: `--since-ts` clean after ingest windows.
- Strip not required.
