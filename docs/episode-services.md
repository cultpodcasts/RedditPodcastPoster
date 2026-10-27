# Episode service links (URL + image)

Canonical storage is an adjacent per-service map on the Cosmos episode document:

```json
"services": {
  "youtube": { "url": "https://www.youtube.com/watch?v=…", "image": "https://i.ytimg.com/vi/…/hqdefault.jpg" },
  "spotify": { "url": "https://open.spotify.com/episode/…", "image": "https://i.scdn.co/image/…" },
  "bbcIplayer": { "url": "https://www.bbc.co.uk/iplayer/episode/…", "image": "https://ichef.bbci.co.uk/…" },
  "vimeo": { "url": "https://vimeo.com/123456789", "image": "https://i.vimeocdn.com/video/…" }
}
```

JSON **keys** are the service identity used for logos (`ServiceCatalog` / website `service-catalog.ts`). A URL can also be resolved to a key from host/path (BBC Sounds vs iPlayer, Vimeo, Netflix, Amazon Prime, …). Unknown hosts slug to an alphanumeric key so they can still render with the generic `external-service` icon. There is no catch-all `other` service; leftover `images.other` is cover art.

The catalog is one ordered list (YouTube, Spotify, Apple, BBC iPlayer, BBC Sounds, Internet Archive, Vimeo, Netflix, Amazon Prime, Paramount+, HBO Max, Play Suisse, TVNZ+). Curator forms show dedicated slots for `DefaultUiKeys` (Spotify, Apple, YouTube) and a list of additional URLs whose service is inferred from the host.

Platform identity for matching and “do we have Spotify / Apple / YouTube?” lives on **`ids`**, not on a named URL slot:

```json
"ids": {
  "spotify": "4rOoJ6Egrf8K2IrywzwOMk",
  "apple": 9876543210,
  "youtube": "abc123DEF45"
}
```

Top-level `spotifyId` / `appleId` / `youTubeId` on **podcast episodes** are leftover Cosmos JSON: ignored on typed deserialize, omitted on serialize (wither). Matching and app writers use nested `ids` only. Episode-pull search indexer SQL reads nested `ids` / `services` only (catalog backfill is complete) — it must **not** fall back to leftover top-level `youTubeId`. Published homepage and public episode payloads expose **`ids` + `services`**. Reconstruct a listen/watch URL from `services.{key}.url`, or from `ids` (search still uses compact `spotifyId` / `youtubeId` / `appleId` + `podcastAppleId`).

### `ids.youtube` always follows `services.youtube.url`

The episode-pull search SQL projects `youtubeId` from nested `ids.youtube` only, so an episode with a YouTube url but no stored id has no Watch icon. `EpisodeServicePresence.YouTubeEpisodeId` falls back to the url for reads; **do not** use it to decide whether the stored id still has to be written (it is never empty once a url exists). Use `EpisodeServicePresence.NestedYouTubeId`. Writes keep the two in step:

- `EpisodeServicePresence.Upsert(episode, "youtube", url, …)` (when it writes a url) and `EpisodeRepository.Save` call `FillYouTubeIdFromUrl`: a missing `ids.youtube` is filled from the url's video id. An id that is already set is never replaced (callers that change the video, such as the curator PATCH, set both), and a url with no video id leaves it empty.

Repair for documents already in Cosmos: `EpisodeServiceBackfill --youtube-id-only` (dry-run unless `--apply`; `--ids a,b` or `--all`). One patch of `/ids/youtube` per document (or `/ids` when absent) with a filter predicate, so it never overwrites an id and re-running is a no-op. The patch bumps `_ts`, so the next scheduled run of the episode search indexer (every 30 minutes, or `CreateSearchIndex -i cultpodcasts -d cultpodcasts-ds -x cultpodcasts-indexer -r`) re-indexes the episode with its `youtubeId`.

### Catalogue playables (Film / TvShowEpisode / NewsReport)

These kinds have **no** nested `ids`. Platform presence is `services` only (`url` + optional `image`). Film / TvShowEpisode / NewsReport implement **`ICatalogueYouTubeItem`** with a compact top-level **`youTubeId`** (synced from `services.youtube.url` on catalogue edits and backfill) so sibling Azure Search datasources can project `youtubeId` for Watch icons — not on the shared `Playable` / `IPlayable` surface, and never on podcast `Episode` (leftover Cosmos `youTubeId` is ignored; nested `ids.youtube` only). Sibling pull SQL uses `PlayableSearchSql` (`c.youTubeId`); do not couple it to the episode-container pull query.

Rollout order, Cosmos backfill, and tested migration types: [episode-services-migration.md](episode-services-migration.md).

Product canvas (website, curator UI, tweets/Bsky, shape impact, plan): [episode-services-canvas.md](episode-services-canvas.md).

## Phase 3 (typed Episode)

Leftover `urls` / top-level ids / `images` are **not** on `Episode`. `NormalizeCatalog` drops a retired `other` catalog key, renames legacy unknown-host slug `iviewabcnetau` → first-class `abcIview` (pre-catalog Cosmos docs from `KeyFromUnknownHost`), and clears empty `ids`. Application code reads and writes `services` / nested `ids` only. Leftover JSON in Cosmos is ignored on deserialize and omitted on the next full `Save()` (wither).

Raw leftover JSON is still read by:

- Console `EpisodeServiceBackfill` (`EpisodeServiceDocumentMigration`, `LeftoverEpisodeDocument` — retired `urls` / ids / `images` members, read-only; never persist that type)
- Tests: `Console-Apps/EpisodeServiceBackfill.Tests` only (no Class-Library or Cloud test project references the CLI)
- Search indexer SQL (catalog first, leftover fallback until leftover keys wither)

BBC Sounds and BBC iPlayer are **distinct keys**.

## Search index (`svc`)

Spotify / YouTube / Apple remain id fields (`spotifyId`, `youtubeId`, `appleId` + `podcastAppleId`). Other services in the ordered catalog are a retrievable string `svc`:

```
bbcSounds:p0example|vimeo:123456789|netflix:uhttps://www.netflix.com/watch/…
```

Grammar (source of truth: `SearchEpisodeServices`):

- `key:payload` entries joined by `|`
- payload is a compact id when `ServiceCatalog.TryCompactUrl` round-trips the original URL
- otherwise `u` + full URL (`|` escaped as `%7C`)
- empty string when none (never null — Azure Search merge ignores null)
- legacy `bbc` and `internetArchive` fields stay populated for old clients

The coalesced cover `image` token is unchanged (YouTube → Spotify → Apple → remaining service art).

Pull-path Cosmos SQL for `svc` / `image` is generated from `ServiceCatalog.SearchEncodedKeys` / `ImageCoalesceOrder` (`SearchIndexCosmosSql` in Models) — **all** streaming catalog keys (ITVX, discovery+, Disney+, Channel 4, Netflix, …), not a hardcoded subset. Keys stay aligned with push-path C# (`SearchEpisodeServices` / `SearchEpisodeImage`), but the **payload dialect does not**: Cosmos SQL keeps the legacy form `key:` + raw `e.services.*.url` (no `u` prefix, no `|`/`%` escape). Push `Encode()` uses the compact grammar above. Clients (`expandSvc` / `TryExpandCompactUrl`) accept both. Aligning SQL with `Encode()` is optional follow-up, not required for catalog-key parity.

### Ops: after adding a streaming ServiceKey (required delivery step)

Adding a plugin updates **repo-generated** pull SQL (`SearchIndexCosmosSql` ← `SearchEncodedKeys`). It does **not** by itself change Azure’s stored datasource query or rewrite search documents.

**Do not recreate / teardown the live index** when Arte (or any new streamer) URLs were submitted before the datasource SQL included that key. Empty `svc` on those docs is fixed by refreshing the projection + reindexing — same `svc` field, new `key:…` segments inside it.

```powershell
CreateSearchIndex --update-existing --index cultpodcasts --datasource cultpodcasts-ds
Index --reindex-search -n "<podcast name>"   # or --podcast-id <guid>
```

Updating live `cultpodcasts-ds` only changes the **pull projection**. Existing search documents keep stale empty `svc`/`image` until they are rewritten:

1. **Targeted:** `Index --reindex-search -n "<podcast name>"` (or `--podcast-id`) — push path from Cosmos; preferred for spot fixes (and for “submitted before SQL bump” episodes).
2. **Bulk:** run the Azure Search pull indexer in batches. Free-tier (~10k docs/day) means a full corpus after HWM reset needs **multiple days** of continued indexer runs — do not assume one run refreshes every streaming episode.
3. Spot-check search REST: `filter=id eq '<episodeId>'`, assert non-empty `svc` and `image` for streaming-only episodes.

Do **not** treat “SQL includes discoveryPlus / arte” as “every episode already has svc/image in search.”

Index schema: **add** `svc` (retrievable) only if missing. Do not treat this document as approval to recreate or deploy the live index.

Procedure checklist: [`.cursor/skills/add-streaming-service/SKILL.md`](../.cursor/skills/add-streaming-service/SKILL.md) §8.
