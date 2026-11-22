# Additional YouTube uploads (RPP #1411)

A catalogue item (podcast episode, news report, TV episode or film) has one **primary** YouTube link:
`services.youtube` (and the compact `youTubeId`, or a podcast episode's `ids.youtube`). The same report,
episode or film is often uploaded more than once — non-exclusive rights, a broadcaster channel and a
programme channel, a later archive upload. Those extra uploads live on an ordered `additionalYouTube`
list on the same item.

Spotify and Apple stay single-valued.

## Model

Each entry is `{ youTubeId, url, image?, title?, channelId?, channelName?, release?, duration?, sourceEpisodeId?, addedAt }`.
The first entry is the player fallback when the primary is unavailable. Empty lists are omitted (null), like film trailers.

One YouTube video id may belong to only one catalogue item, whether as its primary, an alternate, or a film trailer.
Writes refuse a conflict with 409 naming the holder.

## Move to an existing film

`POST episode/{podcastId}/{episodeId}/film` with `filmId` uses `ServiceLinkMerge`. A service the film has no link
for is added as usual. A **different YouTube** upload is added to `additionalYouTube` instead of a 409, so the
Manic case (two channels with the same film) merges. A different Spotify or Apple link is still a 409. See
[move-episode-to-existing-film.md](move-episode-to-existing-film.md).

## Attach route

`POST episode/{podcastId}/{episodeId}/youtube-alternate` with body `{ targetKind, targetId }` attaches the
episode's YouTube upload (and any other non-conflicting service links) to an existing NewsReport, TvShowEpisode,
Film or Episode as an alternate, then deletes the episode row and points its short link at the owning item.
Same conflict / resume behaviour as Move to an existing film (202 / 400 / 404 / 409 / 500). A re-send after the
episode row is gone is a 404.

## PATCH

`PATCH` on a film (and the same field on other playable kinds' documents) accepts:

- `additionalYouTube`: the entries to keep, in display order. Existing entries are identified by `youTubeId`; a
  new entry is added from a YouTube `url`. Omitted entries are removed; `[]` removes all.
- `expectedAdditionalYouTubeIds`: the ids the client last saw. A stored id not in this list is a 409.
- `promoteYouTubeId`: swap that alternate into `services.youtube`. The previous primary becomes an additional
  upload (it is placed first).

Edit Film can add or remove additional YouTube links through this PATCH.

## Indexer, discovery and paste-a-link

A YouTube id that matches an alternate counts as already in the catalogue (`PlayableServiceUrlFilters` and
`UrlMembershipLookup`, the same path as the Indexer's catalogue-owned guard, #1068). Discovery, the indexer
and Submit / paste-a-link resolve it to the owning item and do not recreate it as a new podcast episode.

## Search

No extra search documents. The item's document carries a retrievable, non-searchable `youTubeAlternates` field:
`{youTubeId}|{release}|{channelName}` (`YouTubeAlternateSearchToken`). The same field is written to Azure Search
and to D1. The live Azure index must have the field (`CreateSearchIndex --update-existing`) before any document
carries it. The D1 Worker accepts the field once its schema migration is applied (Api, merge that Worker first).

## Website

The item page shows the primary player, with "Also on YouTube" links (channel name and date) for alternates.
If the primary is unavailable, the player falls back to the first alternate. Old short links of attached
re-uploads redirect to the owning item.

## Export

Catalogue documents already carry `additionalYouTube` as stored. PublicDatabase exports include the field on
films, TV episodes, news reports and podcast episodes.

## Production attachments

The twelve #1409 re-upload rows waiting on this are **not** attached automatically. Each is one attach call,
and production writes need Jon's OK.
