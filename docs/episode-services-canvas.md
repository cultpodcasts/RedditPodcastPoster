<!-- pragma: allowlist secret -->
# Canvas: one service catalog + nested ids <!-- pragma: allowlist secret -->

This is the product/engineering canvas for the service-link work on `cursor/episode-service-links-18b4`. <!-- pragma: allowlist secret -->
Authoritative code notes: [episode-services.md](episode-services.md). Rollout mechanics: [episode-services-migration.md](episode-services-migration.md). <!-- pragma: allowlist secret -->

## 1. What changed <!-- pragma: allowlist secret -->

We stopped treating listen/watch destinations as a **fixed bag of named URL slots** (`urls.spotify`, `urls.apple`, `urls.youtube`, plus a leftover `urls.bbc` / `urls.internetArchive`, plus a parallel `images.*` bag). <!-- pragma: allowlist secret -->

Two adjacent objects on the stored document (and on published public JSON) now do two different jobs: <!-- pragma: allowlist secret -->

| Object | Job | Not for | <!-- pragma: allowlist secret -->
| --- | --- | --- | <!-- pragma: allowlist secret -->
| `services` | Ordered catalog of destinations. Each key is a service identity (`youtube`, `spotify`, `apple`, `bbcIplayer`, `bbcSounds`, `internetArchive`, `vimeo`, `netflix`, `amazonPrime`, or a host slug). Value is `{ url, image }`. | Matching / “do we have Spotify?” | <!-- pragma: allowlist secret -->
| `ids` | Presence of reconstructable Spotify / Apple / YouTube. `{ spotify?: string, apple?: long, youtube?: string }`. | Storing Vimeo/Netflix/BBC (those are `services` only) | <!-- pragma: allowlist secret -->

Hostname (and BBC path) decide the key. Curator “other URL” rows infer the service. Unknown hosts slug to an alphanumeric key and use the generic external icon. <!-- pragma: allowlist secret -->

There is **no** `extraServices` split. Spotify, Apple, YouTube sit in the **same** `services` map as BBC and Vimeo. <!-- pragma: allowlist secret -->

```mermaid <!-- pragma: allowlist secret -->
flowchart LR <!-- pragma: allowlist secret -->
  subgraph before [Before] <!-- pragma: allowlist secret -->
    U[urls named slots] <!-- pragma: allowlist secret -->
    I[images named slots] <!-- pragma: allowlist secret -->
    T[top-level spotifyId appleId youTubeId] <!-- pragma: allowlist secret -->
  end <!-- pragma: allowlist secret -->
  subgraph after [After — canonical] <!-- pragma: allowlist secret -->
    S["services.key.url / image"] <!-- pragma: allowlist secret -->
    D[ids.spotify apple youtube] <!-- pragma: allowlist secret -->
  end <!-- pragma: allowlist secret -->
  U --> S <!-- pragma: allowlist secret -->
  I --> S <!-- pragma: allowlist secret -->
  T --> D <!-- pragma: allowlist secret -->
``` <!-- pragma: allowlist secret -->

Until later cleanup, the document **dual-writes** both worlds so search SQL, matching, tweets, and curator PATCH `urls` keep working. <!-- pragma: allowlist secret -->

--- <!-- pragma: allowlist secret -->

## 2. How it affects the website <!-- pragma: allowlist secret -->

Public cards, hero, search results, saved items, and share/play CTAs all go through the same helpers (`collectEpisodeServices`, `spotifyUrl` / `youtubeUrl` / `appleUrl` / BBC helpers in `search-result-links.ts`). <!-- pragma: allowlist secret -->

**Resolution order** (first hit wins per service): <!-- pragma: allowlist secret -->

1. `services.{key}.url` <!-- pragma: allowlist secret -->
2. Leftover named feed fields on old R2 JSON (`spotify`, `apple`, `youtube`, `bbc`, `internetArchive`) <!-- pragma: allowlist secret -->
3. Reconstruct from `ids` (or search compact `spotifyId` / `youtubeId` / `appleId` + `podcastAppleId`) <!-- pragma: allowlist secret -->
4. Compact search `svc` string for non-id services (Sounds, iPlayer, Archive, Vimeo, Netflix, …) <!-- pragma: allowlist secret -->

**User-visible impact** <!-- pragma: allowlist secret -->

- Play / Watch / listen icons can include Vimeo, Netflix, Prime, distinct BBC Sounds vs iPlayer — not only the old five slots. <!-- pragma: allowlist secret -->
- Cover art still coalesces YouTube → Spotify → Apple → remaining service art (unchanged rule). <!-- pragma: allowlist secret -->
- Old published feed JSON (flat URL fields, no `services`) still renders. After the feed is republished, leftover named fields disappear and cards use `ids` + `services` only. <!-- pragma: allowlist secret -->
- Do **not** ship a website that drops leftover fallbacks before the feed is republished. <!-- pragma: allowlist secret -->

**What does not change for visitors** <!-- pragma: allowlist secret -->

- Episode pages, rails, and search still show the same cards. <!-- pragma: allowlist secret -->
- Sharing still picks a primary outbound URL via the same YouTube → Spotify → Apple preference, now sourced through the helpers. <!-- pragma: allowlist secret -->

--- <!-- pragma: allowlist secret -->

## 3. How it affects the admin / curator UI <!-- pragma: allowlist secret -->

Admin GET (`EpisodeDto` / `ApiEpisode`) is a **superset** during overlap: <!-- pragma: allowlist secret -->

- Still: `urls`, `images`, `spotifyId` / `appleId` / `youTubeId` <!-- pragma: allowlist secret -->
- New: `ids`, `services` <!-- pragma: allowlist secret -->

The add/edit dialogs keep **dedicated slots** for Spotify, Apple, YouTube (`DEFAULT_UI_SERVICE_KEYS`). Extra destinations are a list of URLs; the service is inferred from the host (label updates as you type). <!-- pragma: allowlist secret -->

**PATCH payload today** <!-- pragma: allowlist secret -->

- Changes to the three default slots + legacy BBC + Archive still go out as `urls.*` (and `images.*` if art changed). <!-- pragma: allowlist secret -->
- Changes to additional catalog keys go out as `services.{key}.url`. <!-- pragma: allowlist secret -->
- Server hydrate + dual-write fills `services` / `ids` from those `urls`, and keeps `urls` in sync on save. <!-- pragma: allowlist secret -->

**Curator impact** <!-- pragma: allowlist secret -->

- You can attach Vimeo / Netflix / Prime / a second BBC product without overloading the single `urls.bbc` slot. <!-- pragma: allowlist secret -->
- BBC Sounds and BBC iPlayer are **different keys**. Legacy `urls.bbc` can hold only one URL (iPlayer preferred on sync). <!-- pragma: allowlist secret -->
- Matching still uses top-level / nested ids, not “is there a Spotify URL in the form”. <!-- pragma: allowlist secret -->
- Phase 3 (later PR): stop sending `urls` from the form once the API accepts `services` + `ids` as the write model. <!-- pragma: allowlist secret -->

--- <!-- pragma: allowlist secret -->

## 4. How it affects tweets and Bluesky posts <!-- pragma: allowlist secret -->

**No behaviour change while dual-write is on.** <!-- pragma: allowlist secret -->

`TweetBuilder` and `BlueskyEmbedCardPostFactory` still choose one outbound link from **legacy** `Episode.Urls`, in this order: <!-- pragma: allowlist secret -->

1. YouTube <!-- pragma: allowlist secret -->
2. Spotify <!-- pragma: allowlist secret -->
3. Apple <!-- pragma: allowlist secret -->
4. Internet Archive <!-- pragma: allowlist secret -->
5. BBC (whichever URL is in the single `urls.bbc` slot) <!-- pragma: allowlist secret -->

If none exist, tweet build throws `No link found to tweet`. <!-- pragma: allowlist secret -->

On deserialize, `EpisodeServicePresence.Hydrate` fills `services`. On serialize, `SyncLegacy` copies catalog URLs **back** into `urls` / `images`. So a document that only stored `services.youtube.url` still presents `Urls.YouTube` to the poster. <!-- pragma: allowlist secret -->

Bluesky embed thumbnails still resolve via Spotify id (`Episode.SpotifyId`), which is dual-written with `ids.spotify`. <!-- pragma: allowlist secret -->

`hashTag` and posted/tweeted/bluesky flags are unchanged. <!-- pragma: allowlist secret -->

**Later risk (Phase 3)** <!-- pragma: allowlist secret -->

If we stop `SyncLegacy` or strip `urls` without updating the two poster factories, tweets/Bsky will fail or drop Archive/BBC. Phase 3 must switch posters to `services` (catalog order or the same YouTube→Spotify→Apple preference). <!-- pragma: allowlist secret -->

Vimeo / Netflix / Prime **do not** appear in tweets today. Adding them is a product decision, not part of this branch. <!-- pragma: allowlist secret -->

--- <!-- pragma: allowlist secret -->

## 5. Data shapes — before vs after <!-- pragma: allowlist secret -->

### 5.1 Stored document (`Episode` in Cosmos) <!-- pragma: allowlist secret -->

**Before** <!-- pragma: allowlist secret -->

```json <!-- pragma: allowlist secret -->
{ <!-- pragma: allowlist secret -->
  "spotifyId": "4rOoJ6Egrf8K2IrywzwOMk", <!-- pragma: allowlist secret -->
  "appleId": 9876543210, <!-- pragma: allowlist secret -->
  "youTubeId": "abc123DEF45", <!-- pragma: allowlist secret -->
  "urls": { <!-- pragma: allowlist secret -->
    "spotify": "https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk", <!-- pragma: allowlist secret -->
    "apple": "https://example.com/id1?i=9876543210", <!-- pragma: allowlist secret -->
    "youtube": "https://www.youtube.com/watch?v=abc123DEF45", <!-- pragma: allowlist secret -->
    "bbc": "https://www.bbc.co.uk/iplayer/episode/p0abcd12", <!-- pragma: allowlist secret -->
    "internetArchive": "https://archive.org/details/foo" <!-- pragma: allowlist secret -->
  }, <!-- pragma: allowlist secret -->
  "images": { <!-- pragma: allowlist secret -->
    "youtube": "https://i.ytimg.com/vi/abc123DEF45/hqdefault.jpg", <!-- pragma: allowlist secret -->
    "spotify": "https://i.scdn.co/image/…", <!-- pragma: allowlist secret -->
    "apple": "https://is1-ssl.mzstatic.com/…", <!-- pragma: allowlist secret -->
    "other": "https://ichef.bbci.co.uk/…" <!-- pragma: allowlist secret -->
  } <!-- pragma: allowlist secret -->
} <!-- pragma: allowlist secret -->
``` <!-- pragma: allowlist secret -->

**After (canonical + dual-write still present)** <!-- pragma: allowlist secret -->

```json <!-- pragma: allowlist secret -->
{ <!-- pragma: allowlist secret -->
  "spotifyId": "4rOoJ6Egrf8K2IrywzwOMk", <!-- pragma: allowlist secret -->
  "appleId": 9876543210, <!-- pragma: allowlist secret -->
  "youTubeId": "abc123DEF45", <!-- pragma: allowlist secret -->
  "ids": { <!-- pragma: allowlist secret -->
    "spotify": "4rOoJ6Egrf8K2IrywzwOMk", <!-- pragma: allowlist secret -->
    "apple": 9876543210, <!-- pragma: allowlist secret -->
    "youtube": "abc123DEF45" <!-- pragma: allowlist secret -->
  }, <!-- pragma: allowlist secret -->
  "urls": { "spotify": "…", "apple": "…", "youtube": "…", "bbc": "…", "internetArchive": "…" }, <!-- pragma: allowlist secret -->
  "images": { "youtube": "…", "spotify": "…", "apple": "…", "other": "…" }, <!-- pragma: allowlist secret -->
  "services": { <!-- pragma: allowlist secret -->
    "youtube": { "url": "https://www.youtube.com/watch?v=abc123DEF45", "image": "https://i.ytimg.com/vi/abc123DEF45/hqdefault.jpg" }, <!-- pragma: allowlist secret -->
    "spotify": { "url": "https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk", "image": "https://i.scdn.co/image/…" }, <!-- pragma: allowlist secret -->
    "apple": { "url": "https://example.com/id1?i=9876543210", "image": "https://is1-ssl.mzstatic.com/…" }, <!-- pragma: allowlist secret -->
    "bbcIplayer": { "url": "https://www.bbc.co.uk/iplayer/episode/p0abcd12", "image": "https://ichef.bbci.co.uk/…" }, <!-- pragma: allowlist secret -->
    "internetArchive": { "url": "https://archive.org/details/foo" } <!-- pragma: allowlist secret -->
  } <!-- pragma: allowlist secret -->
} <!-- pragma: allowlist secret -->
``` <!-- pragma: allowlist secret -->

BBC art that used to live only in `images.other` hydrates onto `services.bbcIplayer` or `services.bbcSounds`. <!-- pragma: allowlist secret -->

### 5.2 Shape-change impact matrix <!-- pragma: allowlist secret -->

| Model / payload | Removed or stopped emitting | Added | Still present (overlap) | Impact if a reader is old | Impact if a reader is new | <!-- pragma: allowlist secret -->
| --- | --- | --- | --- | --- | --- | <!-- pragma: allowlist secret -->
| Cosmos `Episode` | Nothing deleted in Phase 0–2 | `ids`, `services` | `urls`, `images`, top-level ids | Old Functions still see `urls` | In-memory hydrate always fills `services`/`ids` even when Cosmos has not been backfilled — **do not** use typed `GetAll()` to find gaps | <!-- pragma: allowlist secret -->
| Feed item `RecentEpisode` | Flat `spotify` / `apple` / `youtube` / `bbc` / `internetArchive` URL slots | `ids`, `services` | Coalesced `image` | Website leftover fallbacks keep cards alive until republish | Cards use `services` then `ids` | <!-- pragma: allowlist secret -->
| `PodcastResult` (same published feed family) | Same named URL slots | `ids`, `services` | `images` bag still on this DTO | Same as feed item | Same as feed item | <!-- pragma: allowlist secret -->
| Public GET `PublicEpisodeDto` | No bolt-on `urls` object | `ids`, `services` | Coalesced `image` | OpenAPI still allows leftover named URL fields during overlap | Saved-item / detail display maps through the public-to-card helper | <!-- pragma: allowlist secret -->
| Admin GET/PATCH `EpisodeDto` | None | `ids`, `services` | `urls`, `images`, top-level ids | Forms unchanged for default slots | Extra services editable; PATCH `services` for non-default keys | <!-- pragma: allowlist secret -->
| Website card interface | — | `ids`, `services`, leftover optional named fields | Leftover `spotify`/`apple`/`youtube`/`bbc`/`internetArchive` | N/A | Helpers hide the overlap | <!-- pragma: allowlist secret -->
| Website `SearchResult` | — | `ids`, `services`, `svc` | Compact `spotifyId` / `youtubeId` / `appleId` / `podcastAppleId` / `bbc` / `internetArchive` | Old search clients ignore `svc` | `expandSvc` + `ids` reconstruct links | <!-- pragma: allowlist secret -->
| Azure Search document | Nothing removed | Retrievable `svc` (manual index add, not this PR) | Compact ids + legacy `bbc` / `internetArchive` | Old website ignores unknown field | Non-id services appear on search cards | <!-- pragma: allowlist secret -->
| Tweet / Bluesky input | None | None (still `Episode.Urls`) | Dual-written `urls` | Posts unchanged | Posts unchanged until Phase 3 | <!-- pragma: allowlist secret -->
| Curator `EpisodePost` | None | optional `services` | `urls`, `images` | API still accepts `urls` | Extra keys persist via `services` | <!-- pragma: allowlist secret -->

### 5.3 Search `svc` (not a replacement for Spotify/Apple/YouTube) <!-- pragma: allowlist secret -->

Spotify / YouTube / Apple stay compact id fields. Other catalog keys compact to: <!-- pragma: allowlist secret -->

``` <!-- pragma: allowlist secret -->
bbcSounds:p0example|vimeo:123456789|netflix:uhttps://www.netflix.com/watch/… <!-- pragma: allowlist secret -->
``` <!-- pragma: allowlist secret -->

Empty string when none (never null — Azure Search merge ignores null). Grammar: `SearchEpisodeServices`. <!-- pragma: allowlist secret -->

--- <!-- pragma: allowlist secret -->

## 6. Why migration is two tracks (code + data) <!-- pragma: allowlist secret -->

`OnDeserialized` always hydrates. After this branch, **in-memory** `Episode` looks migrated even when Cosmos JSON has no `services`. Candidate selection **must** use raw JSON + `NeedsBackfill`. A cheap `NOT IS_DEFINED(c.services)` misses **partial** maps (YouTube in `services`, Spotify still only on `urls`). <!-- pragma: allowlist secret -->

```mermaid <!-- pragma: allowlist secret -->
flowchart TD <!-- pragma: allowlist secret -->
  P0[Phase 0 — this branch: dual-write + dual-read] <!-- pragma: allowlist secret -->
  P1[Phase 1 — deploy Functions then Api then website then republish feed then add/reindex svc] <!-- pragma: allowlist secret -->
  P2[Phase 2 — Cosmos backfill dry-run then apply] <!-- pragma: allowlist secret -->
  P3[Phase 3 — later PR: stop SyncLegacy, switch posters/SQL/forms, optional strip urls] <!-- pragma: allowlist secret -->
  P0 --> P1 --> P2 --> P3 <!-- pragma: allowlist secret -->
``` <!-- pragma: allowlist secret -->

### Phase 0 — already on the branch <!-- pragma: allowlist secret -->

New writes persist both shapes. Search SQL and matching still read `urls` / top-level ids. Website reads new then leftover. No bulk Cosmos write required for new documents. <!-- pragma: allowlist secret -->

### Phase 1 — roll out code (order) <!-- pragma: allowlist secret -->

1. Azure Functions / indexer / publisher <!-- pragma: allowlist secret -->
2. Api Worker (R2 bytes passed through; OpenAPI allows both shapes) <!-- pragma: allowlist secret -->
3. Website (helpers tolerate old feed JSON) <!-- pragma: allowlist secret -->
4. Republish the published feed so R2 is `ids` + `services` <!-- pragma: allowlist secret -->
5. Add search field `svc`, reindex after Functions that write `svc` are live <!-- pragma: allowlist secret -->

### Phase 2 — migrate stored JSON <!-- pragma: allowlist secret -->

Tested types: `EpisodeServiceDocumentMigration` (`NeedsBackfill`, `SelectDocumentsToBackfill`, `Apply`) and `EpisodeServiceBackfillProcessor` (dry-run default). <!-- pragma: allowlist secret -->

1. Page raw documents <!-- pragma: allowlist secret -->
2. Dry-run (`apply: false`) — record candidate count, spot-check <!-- pragma: allowlist secret -->
3. Apply in batches — save only when shape actually changed <!-- pragma: allowlist secret -->
4. Dry-run again ≈ 0 <!-- pragma: allowlist secret -->
5. Republish feed / reindex if those still show gaps <!-- pragma: allowlist secret -->

`Apply` **keeps** `urls` and top-level ids. This is a backfill, not a delete. Do not run apply against production from an agent session unless that write is explicitly requested. <!-- pragma: allowlist secret -->

### Phase 3 — later PR (not this branch) <!-- pragma: allowlist secret -->

Only after search SQL, matching, curator forms, and tweet/Bsky factories read `services` / `ids`: <!-- pragma: allowlist secret -->

1. Stop `SyncLegacy` <!-- pragma: allowlist secret -->
2. Stop writing top-level ids once matching reads `ids` <!-- pragma: allowlist secret -->
3. Optional strip of `urls` / `images` — new tested `NeedsStrip`, never combined with Phase 2 <!-- pragma: allowlist secret -->

### Rollback <!-- pragma: allowlist secret -->

- Revert code: old website leftover fields + old Functions `urls` still work. <!-- pragma: allowlist secret -->
- After data apply: documents **gained** `services`/`ids` and kept `urls`. Reverting code is safe. There is no automatic delete of `services`. <!-- pragma: allowlist secret -->

--- <!-- pragma: allowlist secret -->

## 7. Done when <!-- pragma: allowlist secret -->

- [ ] Phase 1 deployed in the order above <!-- pragma: allowlist secret -->
- [ ] Dry-run then apply; second dry-run ~0 <!-- pragma: allowlist secret -->
- [ ] Feed republished; search reindexed with `svc` <!-- pragma: allowlist secret -->
- [ ] Phase 3 tracked separately; dual-write still on until that PR <!-- pragma: allowlist secret -->
- [ ] Tweet/Bsky factories updated in the same PR that stops `SyncLegacy` <!-- pragma: allowlist secret -->
