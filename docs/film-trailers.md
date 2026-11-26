# Film trailers

A film can list trailers, teasers and clips. Each one is a whole video of its own, so it is not a `services` link
(that holds one link per platform for the film itself).

## Storage

`Film.trailers` (Cosmos films container), in display order, omitted when empty:

| Field | Meaning |
| --- | --- |
| `id` | Id of the podcast episode the trailer was attached from |
| `title` | Trailer title (trimmed) |
| `url` | The page that plays it: the episode's YouTube link, otherwise its first other link by service key |
| `image` | Optional image (the episode's coalesced image) |
| `release` | Optional publish time (UTC) |
| `duration` | Length |
| `ids` | Optional source-episode Spotify / Apple / YouTube ids (same shape as episode `ids`); omitted when empty |
| `services` | Optional source-episode YouTube / Spotify / Apple URLs (`url` only); omitted when empty. Lets Spotify/Apple paste and a Spotify- or Apple-only indexer re-find still match a YouTube-sourced trailer |

Existing trailers written before these fields keep YouTube-only matching until an optional backfill (needs an explicit production write). New attaches store them at attach time.

## Search

Trailers get **no search documents of their own** (the index is at the Free-tier quota). The film's document carries
a retrievable, non-searchable `trailers` string list, one `{duration}|{url}|{title}` token per trailer
(`FilmTrailerSearchToken`). The title is last, so a reader splits on the first two `|` only. Films are pushed, not
pulled: the curator edit / attach and the FilmSearchPusher console (#1081) all build the document with
`CataloguePlayableSearchDocument.For(Film)`, which sets `trailers`. There is no film data source or indexer, so no
Cosmos query change.

A film with no trailers omits the field, so films keep pushing before the index has it. Removing a film's last
trailer pushes `[]` to clear it (merge-or-upload ignores an omitted field).

### Adding the field to the live index (no teardown)

```powershell
CreateSearchIndex --update-existing --index cultpodcasts --datasource cultpodcasts-ds
```

`--update-existing` adds any `EpisodeSearchRecord` field the index lacks and keeps every document. The episode, TV
and news indexers need no change (they never project `trailers`). Once the field exists, a FilmSearchPusher run (or
any film save) fills it. FilmSearchPusher merge-or-uploads, so a film whose last trailer was removed outside the
curator edit keeps its old tokens until its next curator save (the edit pushes `[]`).

Run this **before** the first attach. If the field is missing, the attach's film push fails and the attach stops
before the episode is deleted, so nothing is lost; re-send after adding the field.

## Attach as trailer

`POST episode/{podcastId}/{episodeId}/trailer`, body `{ "filmId": "...", "title": "optional" }`, curate role,
success `202` with `{ filmId, name, failureIndexing }`; when another film already holds the episode, `409` with
`filmId` of that film (as in step 1). Order (`EpisodeTrailerAttachService`):

0. Claim the episode for the film: one LookUps document per episode (`FilmTrailerClaim`, id derived from the episode
   id, written by `IFilmTrailerClaimStore` with a Cosmos create, never an upsert). Of two attaches of one episode to
   different films exactly one create succeeds; the other is a 409 naming the claimed film and writes nothing, so
   nothing has to be undone and no film is ever deleted. A claim that already names this film is a resume. A claim
   is taken over (replace with If-Match on the etag read, so one taker wins) only when its film does not hold the
   trailer **and** it is older than 10 minutes (a failed attach); a younger one is a 409 "in progress, try again
   shortly". Claims expire by TTL after 7 days.
1. Add the trailer (id = episode id) to the film and save it with an ETag-guarded replace. Before that add, films
   are queried across partitions for `trailers[].id` equal to the episode id. If another film already holds it
   (an earlier attach saved the film, then stopped), the attach is a 409 with that film's id and nothing is
   written. Re-sending to the film that already holds the trailer still resumes. If another write saved the film
   since it was read, re-read and add again (3 attempts); otherwise 500, episode kept.
2. Push the film search document. Failure: 500 with `failureIndexing`, episode kept.
3. Delete the episode search document. Failure: 500 with `failureIndexing`, episode kept.
4. Re-read the film. If a concurrent write dropped the trailer, 500 with `failureIndexing` and the episode kept
   (search already changed; re-send re-adds the trailer and puts search back in step).
5. Delete the episode row. The source podcast is never changed.
6. Point the episode's short link at `/film/{name}/{shortId}` (best effort).

The film PATCH saves ETag-guarded too, and carries over any trailer attached after it read the film, so an edit
never drops a trailer whose episode row is already gone. A film deleted after the PATCH read it is a 404 and is
not re-created.

Re-sending resumes an interrupted attach without adding the trailer twice; after a completed attach a re-send is
accepted again. A removed episode, a removed film, an episode with no link, or the film's own id is a 400. An
episode that is already a trailer of another film is a 409 (body `filmId` is that film).

### Attaching to a film that does not exist yet (#1418)

A trailer often comes out before its film (for example an unreleased film with no main video). Send
`{ "createFilm": true, "filmName": "...", "title": "optional" }` instead of `filmId` (giving both is a 400). The
trailer goes to the film with that name (case-insensitive), and when no film has it a film is created first, the way
Move to TV show's `createShow` works:

- Every episode check (episode found, not removed, has a link, not a trailer of a film with another name) runs before
  the film is created, so a refused attach never leaves an empty film behind. Two films with the name is a 409.
- The new film goes through the same name and IMDb/TMDB identity gate as Move to film (`FilmMoveTarget`): an
  ambiguous match is a `200` with `identityDecision` and nothing is written; re-POST with `identityDecision`
  (`select` / `continue` / `cancel`) and `selectedIdentityKey`. A cancel is a `200` with `cancelled: true`.
- The film has no release, services or video. Search holds release `0001-01-01` for it (as for any playable with no
  release), the social-post and homepage release windows skip it, and the website shows no date. The curator adds the
  release and the main video later with the film edit.
- The new film is written once, already holding the trailer, by a conditional create (`IFilmRepository.TryCreate`,
  Cosmos create rather than upsert) under an id derived from the trimmed, lower-case name. Overlapping creates of the
  same name converge on that one document (the loser re-reads it and resumes), and no empty film is ever stored: a
  later failure leaves the film holding the trailer with the episode row kept. The episode is claimed (step 0) for
  the name's film id before the create, so of two overlapping creates with different names only the claim winner
  creates a film; the other is a 409 naming it and writes nothing. No attach path deletes a film, so a film that has
  gained another trailer or a main video meanwhile is never touched. If the name's id belongs to a film renamed
  since, the call is a 409 naming it.
- A re-send finds the film by its trailer or its name, so the film is never created twice. `filmCreated: true` on the
  `202` says this call created it.

Old trailer short links keep working: the Shortner KV record for the episode key now holds the film path. After a
film rename the record still holds the old name in the path; the short id still resolves to the film.

## Film edit (PATCH film/{id})

| Field | Rule |
| --- | --- |
| `imdbId` | `tt` + 7–10 digits; `""` clears |
| `tmdbId` | positive; `0` clears; negative is a 400 |
| `services` | per key, as the episode edit: blank `url` and `image` removes the key; otherwise the key comes from the URL; URLs must be absolute http(s) |
| `trailers` | `[{ id, title? }]`: the trailers to keep, in order, with optional rename; omitted ones are removed; an unknown or repeated id is a 400 |

`GET film/{id}` returns `trailers` (`id`, `title`, `url`, `image`, `release`, `duration`).

## Public database

`PublicFilm.trailers`: `[{ title, url, release (yyyy-MM-dd), duration }]`, omitted when empty.
