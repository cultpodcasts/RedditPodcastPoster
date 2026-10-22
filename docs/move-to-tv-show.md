# Move to TV show

Curator command that moves one podcast episode into a TV show as a `TvShowEpisode` with the **same id**. It mirrors
Move to film (`EpisodeFilmMoveService`) and the trailer attach (`EpisodeTrailerAttachService`). First use: routing
BBC / BBC Three podcast items into their real programmes (BBC Panorama, Reported Missing, My Family, …) with
publisher BBC, instead of catch-all "BBC" shows.

## Endpoint

`POST episode/{podcastId}/{episodeId}/tvshow`, curate role, body:

| Field | Meaning |
| --- | --- |
| `tvShowId` | An existing show, by id |
| `tvShowName` | A show by name (case-insensitive, trimmed, no `/`) |
| `createShow` | With `tvShowName`: create the show when no show has the name |
| `publisher` | Publisher of a show the move creates. Only valid with `createShow` |
| `title` | Optional TV episode title; blank keeps the episode title |

Exactly one of `tvShowId` / `tvShowName`. Responses (command outcome, not the read model):
202 `{ tvShowId, tvShowName, episodeId, createdShow, failureIndexing }`; 400 invalid body, removed episode or
removed show; 404 podcast / episode / show not found (a name with no show and no `createShow` is a 404, so a typo
never creates a show); 409 several shows have the name, or the episode id is already a TV episode of another show
or a film; 500 failed (re-send resumes).

`createShow` with a name one show already has joins that show and does **not** change its publisher
(`createdShow: false`), so a re-send after an interrupted create never makes a second show.

A created show gets a fresh id (never the podcast id), `publisherName` from `publisher` and **no `indexing`**, so it
claims no YouTube channel. The BBC / BBC Three podcasts keep their channel ids as the holding inbox for new submits
(see the BBC show mapping).

## Order (`EpisodeTvShowMoveService`)

1. Save the TV episode (`CatalogueParentKindMapper.ToTvShowEpisode`: services, subjects, posted / tweeted /
   Bluesky state). A created show is saved first, with `latestReleased` = the episode release.
2. Swap the search document under the same id to ContentKind `TvShowEpisode`
   (`CataloguePlayableSearchDocument.For(TvShowEpisode)`, the shape the TV episode edit and pull indexer write).
   Failure: 500, episode kept.
3. Re-read the show and the TV episode. If either was deleted concurrently: 500, episode kept.
4. Delete the episode row. The source podcast is never changed, even when it is left empty.
5. Raise the show's `latestReleased` with a field patch (`PatchIndexerState`, only `/latestReleased`), so a
   concurrent curator edit of the show is never overwritten. Best effort.
6. Point the episode's short link at `/tv/{show}/{prefixed short id}` (best effort), as the trailer attach does.

Re-sending resumes: a TV episode with the episode id in the target show while the episode row exists is reused, not
saved twice. After a completed move a re-send to the same show is accepted again; to another show it is a 409.

As with Move to film, an episode released in the last few days may be re-added to the podcast by the Indexer
(#1068); the website warns.
