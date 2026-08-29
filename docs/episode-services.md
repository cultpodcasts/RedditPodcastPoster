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

Top-level `spotifyId` / `appleId` / `youTubeId` are leftover Cosmos JSON: ignored on typed deserialize, omitted on serialize (wither). Matching and app writers use nested `ids` only. Search indexer SQL still dual-reads leftover ids until those keys wither. Published homepage and public episode payloads expose **`ids` + `services`**. Reconstruct a listen/watch URL from `services.{key}.url`, or from `ids` (search still uses compact `spotifyId` / `youtubeId` / `appleId` + `podcastAppleId`). <!-- pragma: allowlist secret -->

Rollout order, Cosmos backfill, and tested migration types: [episode-services-migration.md](episode-services-migration.md).

Product canvas (website, curator UI, tweets/Bsky, shape impact, plan): [episode-services-canvas.md](episode-services-canvas.md).

## Phase 3 (typed Episode)

Leftover `urls` / top-level ids / `images` are **not** on `Episode`. `NormalizeCatalog` only drops a retired `other` catalog key and empty `ids`. Application code reads and writes `services` / nested `ids` only. Leftover JSON in Cosmos is ignored on deserialize and omitted on the next full `Save()` (wither).

Raw leftover JSON is still read by:

- `EpisodeServiceDocumentMigration` (backfill `NeedsBackfill` / `MergeRawLeftoverIntoCatalog`)
- Search indexer SQL (catalog first, leftover fallback until leftover keys wither)

BBC Sounds and BBC iPlayer are **distinct keys**.

## Search index (`svc`)

Spotify / YouTube / Apple remain id fields (`spotifyId`, `youtubeId`, `appleId` + `podcastAppleId`). Other services in the ordered catalog are a retrievable string `svc`:

```
bbcSounds:p0example|vimeo:123456789|netflix:uhttps://www.netflix.com/watch/…
```

Grammar (source of truth: `SearchEpisodeServices`): <!-- pragma: allowlist secret -->

- `key:payload` entries joined by `|`
- payload is a compact id when `ServiceCatalog.TryCompactUrl` round-trips the original URL
- otherwise `u` + full URL (`|` escaped as `%7C`)
- empty string when none (never null — Azure Search merge ignores null)
- legacy `bbc` and `internetArchive` fields stay populated for old clients

The coalesced cover `image` token is unchanged (YouTube → Spotify → Apple → remaining service art).

Index schema: **add** `svc` (retrievable). Do not treat this document as approval to recreate or deploy the live index.
