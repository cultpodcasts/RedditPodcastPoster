# TV show trailers

A TV show can list trailers, teasers and clips. It mirrors [film trailers](film-trailers.md) (#1412): each trailer is a
whole video of its own, kept on the show document, attached from a podcast episode.

## Differences from film trailers

| | Film | TV show |
| --- | --- | --- |
| Search document | The film has a search document with a `trailers` token list | **None.** TV shows have no search document, so there is nothing to push |
| Attach search steps | Push the film document, then delete the episode document | Delete the episode search document only |
| `kind` on a trailer | None | None (same JSON shape as `FilmTrailer`) |
| Short link target | `/film/{name}/{shortId}` | `/tv/{name}` (a show is addressed by name only, `CatalogueShortId.TvShowPath`) |
| Claim document | `FilmTrailerClaim` | `TvShowTrailerClaim` (`ModelType` 35, `ITvShowTrailerClaimStore`) |
| Created-show id | v5-style GUID, film prefix | v5-style GUID over SHA256 with prefix `cultpodcasts:tvshow-by-name:` |

Attach never calls `RaiseLatestReleased`: a trailer is not a release of the show.

## Storage

`TvShow.trailers` (Cosmos TV shows container), in display order, omitted when empty. Each item is a `TvShowTrailer`
with the same fields as `FilmTrailer` (`id` = source episode id, `title`, `url`, `image`, `release`, `duration`, `ids`,
`services`). Trailers live on the show document only; the source episode row is deleted once the search steps succeed.

Cross-partition lookup: `ITvShowRepository.GetTvShowIdsByTrailerId` runs
`SELECT VALUE c.id FROM c WHERE EXISTS(SELECT VALUE t FROM t IN c.trailers WHERE t.id = @trailerId)`.

## Search

Trailers get no search documents and the TV show has none either. The only search change on attach is deleting the
episode's own search document (`IPlayableSearchDocumentSwap.DeleteAsync`). No index field, teardown or
`CreateSearchIndex --update-existing` run is needed, and the Free-tier data-source limit is untouched.

## URL membership (submit and indexer)

A show holds the URL as a trailer, and the source episode row is gone, so membership must still know the URL.
`UrlMembershipLookup` queries TV shows with `PlayableServiceUrlFilters.TvShowHoldsTrailerUrl`, which mirrors
`FilmHoldsServiceOrTrailerUrl` but matches trailers only:

- YouTube: the trailer's `url` (stored or canonical) or its `ids.youtube`.
- Spotify / Apple: the trailer's `ids` and `services` URLs, so a Spotify or Apple re-find still matches a
  YouTube-sourced trailer.

The owner returned is a catalogue owner of kind `TvShowEpisode` with the show's name and id as parent, no playable id
and `IsTrailer = true`. A trailer has no resubmit path, so `CatalogueServiceUrlOwner.AlwaysStopsSubmit` is true and
`CatalogueOwnershipGuard.Blocks` stops a submit even when it is routed to the same show. The indexer's YouTube holder
check (`FindYouTubeHolderExcluding`) sees trailers too, except for shows in the excluded ids.

## Attach as trailer

`POST episode/{podcastId}/{episodeId}/tvshow-trailer`, curate role. Body is one of:

- `{ "tvShowId": "...", "title": "optional" }`
- `{ "createShow": true, "tvShowName": "...", "publisher": "optional", "title": "optional" }` (giving both is a 400)

Success is `202` with `{ tvShowId, tvShowName, error, failureIndexing, showCreated }` (a command: the client GETs the
show). Order (`EpisodeTvShowTrailerAttachService`):

0. Claim the episode for the show (`TvShowTrailerClaim`, one LookUps document per episode, conditional create). Of two
   attaches of one episode to different shows exactly one create succeeds; the other is a 409 naming the claimed
   show and writes nothing. A claim that names this show is a resume. A claim is taken over (replace with If-Match)
   only when its show does not hold the trailer **and** it is older than 10 minutes.
1. Add the trailer (id = episode id) to the show with an ETag-guarded replace (3 attempts). If another show already
   holds that trailer id the attach is a 409 naming it. Otherwise 500, episode kept.
2. Delete the episode search document. Failure: 500 with `failureIndexing`, episode kept.
3. Re-read the show. If it was deleted, log `TvShowTrailerAttachShowGone`, best-effort restore the episode search
   document, keep the episode and return 500 with `failureIndexing`. If a concurrent write dropped the trailer: 500,
   episode kept (re-send re-adds it).
4. Delete the episode row. The source podcast is never changed.
5. Point the episode's short link at `/tv/{name}` (best effort).

An episode id that is already a TV episode or a film is a 409: deleting the episode search document would delete that
item's document, which has the same id.

Re-sending resumes an interrupted attach without adding the trailer twice. A removed episode, a removed show or an
episode with no link is a 400.

### Attaching to a show that does not exist yet

With `createShow`, the trailer goes to the show with that name (case-insensitive); if none exists one is created
first, holding the trailer, with a conditional create (`ITvShowRepository.TryCreate`) under the deterministic id of the
trimmed, lower-case name. Every episode check runs before the create. Identity (name, publisher) is validated with
`TvShowMoveTarget`, shared with Move to TV show. Overlapping creates of one name converge on one document; the claim
decides which of two different names wins. No attach path deletes a show.

## TV show edit (PATCH tvshow/{id})

`trailers`: `[{ id, title? }]`, the trailers to keep in order with an optional rename; omitted ones are removed; an
unknown or repeated id is a 400. `expectedTrailerIds` is the stale guard: the trailer ids the client saw. If the show
now holds a trailer the client did not list (attached since it loaded the show), the PATCH is a `409` with
`unexpectedTrailerIds` in the body and nothing is saved, so a keep-list never removes a trailer the curator has not
seen.

The PATCH saves with an ETag-guarded replace (3 attempts). On each attempt it takes the trailers and the newer
`latestReleased` from the show it just re-read, so an edit never drops a trailer attached while it was open (the
episode row is already gone). A show deleted since the read is a 404.

`GET tvshow/{id}` returns `trailers` (`id`, `title`, `url`, `image`, `release`, `duration`).

## Deferred

- Api Worker proxy / OpenAPI and website UI for the new route and fields.
- A trailer `kind` (none by design) and any search tokens for trailers.
- Backfill of `ids` / `services` is not needed (new feature); no production writes are part of this change.
