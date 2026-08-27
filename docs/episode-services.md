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

JSON **keys** are the service identity used for logos (`ServiceCatalog` / website `service-catalog.ts`). A URL can also be resolved to a key from host/path (BBC Sounds vs iPlayer, Vimeo, Netflix, Amazon Prime, …). Unknown hosts slug to an alphanumeric key so they can still render with the generic `external-service` icon.

The catalog is one ordered list (YouTube, Spotify, Apple, BBC iPlayer, BBC Sounds, Internet Archive, Vimeo, Netflix, Amazon Prime, Other). Curator forms show dedicated slots for `DefaultUiKeys` (Spotify, Apple, YouTube) and a list of other URLs whose service is inferred from the host.

First-class episode ids (`spotifyId`, `youTubeId`, `appleId`) stay for matching and search reconstruction. They are not replaced by `services`.

## Dual-write (migration)

Existing documents only have split `urls` + `images` (BBC art in `images.other`). On deserialize, `EpisodeServicePresence.Hydrate` <!-- pragma: allowlist secret --> fills `services`. On serialize, `SyncLegacy` still writes `urls` / `images` so Cosmos SQL indexers and admin forms keep working.

BBC Sounds and BBC iPlayer are **distinct keys**. Legacy `urls.bbc` can hold only one URL (iPlayer preferred on sync).

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
