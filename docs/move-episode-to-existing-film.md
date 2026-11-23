# Move to an existing film (RPP #1522)

`POST episode/{podcastId}/{episodeId}/film` with body `{ "filmId": "<existing film id>" }` merges a podcast episode
that is another copy of a film already in the catalogue into that film. Without `filmId` the route creates a new film
from the episode, as before (`title` and the identity fields only apply to that case).

What the merge does (`EpisodeFilmMoveService.MergeIntoFilmAsync`):

1. Materialises the episode's Spotify / Apple / YouTube ids into `services` urls.
2. Plans the link merge with the shared `ServiceLinkMerge` helper (meant for reuse by #1471 merge episodes and the #1416
   split): links for services the film has none for are added; identical links (YouTube compared by video id) are
   already held; a **different YouTube** upload is added as an `additionalYouTube` entry (RPP #1411) so two
   non-exclusive uploads of the same film can live on one item; a different Spotify or Apple link is a **409**
   naming the film, and nothing is written.
3. Saves the film (ETag-guarded) with the added links, any YouTube alternates, the synced compact `youTubeId`, and the
   episode's subjects the film doesn't have (never ones the curator removed from the film). The film's name, identity,
   release, description and id are unchanged.
4. Pushes the film's search document, deletes the episode's search document, re-checks the film, then deletes the
   episode row and points the episode's short link at the film page. Any failure before the row delete keeps the
   episode and a re-send resumes (500 with `failureIndexing`).

Because the film now owns the episode's urls, the Indexer's catalogue-owned guard (#1068) does not re-add a recently
released episode to the podcast.

| Status | When |
|---|---|
| 202 | Merged (`filmId`, `name`) |
| 400 | Removed episode or film; `filmId` is the episode's own id; the episode has no links; `filmId` on a news report move |
| 404 | Podcast, episode or film not found (also a re-send after a finished merge) |
| 409 | The film already has a different Spotify, Apple or other non-YouTube link for one of the episode's services (`error` names the service) |
| 500 | Did not finish; episode kept; re-send resumes |
