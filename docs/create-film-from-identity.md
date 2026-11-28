# Create a film from a TMDB / IMDb identity

RPP #1413. A film used to exist only by moving an episode into it (`POST episode/{p}/{e}/film`, or the news-report
equivalent). When a channel uploads only the trailer of a film the catalogue does not have, the trailer had nowhere to
go: moving the trailer into a new film would make the trailer the film's main video. `POST film` creates the empty
film first; the trailer then attaches with `POST episode/{p}/{e}/trailer { "filmId": ... }`
([film-trailers.md](film-trailers.md)).

## Route

`POST film`, curate scope. Code: `FilmController.Create`, `PostFilmCreateHandler`, `FilmCreateService`.

Body (all optional, but give ids or a title):

| Field | Meaning |
| --- | --- |
| `tmdbId` | TMDB movie id |
| `imdbId` | IMDb title id, `tt` then digits |
| `title` | The film name. With ids it overrides the identity title. With no ids it is the title to look up |
| `publisher` | Overrides the identity's publisher |
| `identityDecision`, `selectedIdentityKey` | The identity-prompt answer (`select` with `tmdb:<id>`, `continue`, `cancel`), the same as Move to film |

## Behaviour

- **With ids:** a film that already has the TMDB or IMDb id is a **409** carrying that film's `filmId`, before any
  TMDB call. Otherwise the TMDB movie is read (`IIdentityMetadataEnricher.FetchFilmAsync`; none is **404**). An IMDb-only
  request learns its TMDB id from that read and is checked against it too.
- **With a title and no ids:** the same identity gate as Move to film (`FilmMoveTarget.PrepareNewFilm`). A confident
  match supplies the ids. An ambiguous match is a **200** with `identityDecision` and nothing is written. Re-POST with
  `select` / `continue` / `cancel`.
- **Name:** the request title, else the identity title. No `/`. A film with the same name (case-insensitive) is a
  **409** with that film's id.
- **Release and other fields** come from the identity through `IdentityMetadataFill` (the Move to film / film PATCH
  fill, #1410, #1430): the TMDB release date, or the year when TMDB only has a year, plus description, runtime, image,
  publisher and alternative titles. A field the curator sent is kept.
- **Empty film:** no services, trailers or episode, so it starts with no playable link. It is indexed with
  `CataloguePlayableSearchDocument.For(film)` and renders without a main video.
- **Write:** one conditional create under the same name-derived id as a film created by Attach as trailer
  (`EpisodeTrailerAttachService.NewFilmId`). Overlapping creates converge on one document, and the loser is a **409**
  naming the winner.
- **Search:** the film document is pushed after the create. If the push fails the film is kept and the response is a
  **500** with `failureIndexing: true` and the `filmId`. A film save, the Indexer's scheduled film push or
  FilmSearchPusher then indexes it.
- **Re-sending** the same identity is safe: it is the 409 above (identity or name clash) with the existing film's id,
  and nothing is written.

## Response (CQRS)

A command returns an acknowledgement, not the film: **202** `{ filmId, name }`. The client GETs `film/{filmId}` for
`tmdbId`, `imdbId`, `release` and the rest. Other outcomes:

| Status | Body |
| --- | --- |
| 200 | `identityDecision` candidates, or `cancelled: true`. Nothing written |
| 400 | `error` (no ids or title, bad id, name with `/`) |
| 404 | `error` (no TMDB movie for the id) |
| 409 | `filmId`, `name`, `error` of the existing film |
| 500 | `failureIndexing`, `filmId`, `name` when the search push failed |

## Companions (not in this PR)

- Api Worker: proxy route and OpenAPI entry, following the move-to-film and trailer-attach routes.
- Website: a curator "New film from TMDB/IMDb" action using the identity lookup and candidate picker from the move
  dialog, then "Attach trailer" from a podcast episode.
- Submit flow: check that a new film's trailer URLs count as already in the catalogue once a trailer is attached.

Tests: `FilmCreateServiceRules`, `PostFilmCreateHandlerRules`, and the trailer attach to an identity-created film in
`EpisodeTrailerAttachServiceRules` (`FunctionHost.Tests`).
