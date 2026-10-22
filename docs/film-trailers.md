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
202 with `{ filmId, name, failureIndexing }`. Order (`EpisodeTrailerAttachService`):

1. Add the trailer (id = episode id) to the film and save it with an ETag-guarded replace. If another write saved
   the film since it was read, re-read and add again (3 attempts); otherwise 500, episode kept.
2. Push the film search document. Failure: 500 with `failureIndexing`, episode kept.
3. Delete the episode search document. Failure: 500 with `failureIndexing`, episode kept.
4. Re-read the film. If a concurrent write dropped the trailer, 500 with the episode kept (re-send re-adds it).
5. Delete the episode row. The source podcast is never changed.
6. Point the episode's short link at `/film/{name}/{shortId}` (best effort).

The film PATCH saves ETag-guarded too, and carries over any trailer attached after it read the film, so an edit
never drops a trailer whose episode row is already gone.

Re-sending resumes an interrupted attach without adding the trailer twice; after a completed attach a re-send is
accepted again. A removed episode, a removed film, an episode with no link, or the film's own id is a 400.

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
