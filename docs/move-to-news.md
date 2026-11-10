# Move to news

Curator command that moves **selected** podcast episodes into a news organisation as `NewsReport`s with the **same
ids** (#1388). It is not a kind transfer: the podcast row, its other episodes, its Spotify / Apple / YouTube show ids
and its file key are never changed, and the podcast is never deleted, even when it is left with no episodes. First
use: splitting a podcast whose YouTube-only episodes cannot be enriched (no Spotify / Apple URL) into a news
organisation, while the episodes with audio URLs stay on the podcast.

## Endpoints

Both need the `curate` permission and take the same organisation fields:

| Route | Body |
| --- | --- |
| `POST episode/{podcastId}/{episodeId}/news` | organisation fields |
| `POST podcast/{podcastId}/news` | `episodeIds: [...]` (1–500 ids) plus organisation fields |

| Field | Meaning |
| --- | --- |
| `newsOrganisationId` | An existing news organisation, by id |
| `newsOrganisationName` | A news organisation by name (case-insensitive, trimmed, no `/`). Only news organisations are matched, so a podcast with the same name is not a clash |
| `createOrganisation` | With `newsOrganisationName`: create the organisation when no news organisation has the name |
| `publisher` | Publisher of an organisation the move creates. Only valid with `createOrganisation`; omitted means none |

Exactly one of `newsOrganisationId` / `newsOrganisationName`. The batch route creates or resolves the organisation
**once** and moves only the listed ids.

Responses (command outcome, not the read model), both routes:
202 `{ newsOrganisationId, newsOrganisationName, createdOrganisation, movedEpisodeIds, alreadyMovedEpisodeIds,
notFoundEpisodeIds, conflictEpisodeIds, failureIndexing }` (the single route adds `episodeId`);
400 invalid body or removed organisation; 404 podcast / organisation not found, or any id that is not an episode of
the podcast (`notFoundEpisodeIds`); 409 several organisations have the name, or any id that is already a TV episode,
a film or a news report of another organisation (`conflictEpisodeIds`); 500 failed (re-send resumes).

A 404 or 409 refuses the **whole** request before anything is written: no organisation is created and no episode
moves. A name with no organisation and no `createOrganisation` is a 404, so a typo never creates one.
`createOrganisation` with a name one organisation already has joins it and does **not** change its publisher
(`createdOrganisation: false`).

## A created organisation

- A new id (`Guid.NewGuid()`), never the podcast id.
- A newly allocated `news-` file key (the podcast keeps its own key).
- `publisherName` from `publisher`, else empty.
- `indexing` with only the news creation defaults (`CatalogueIndexingSource.ApplyNewsOrganisationCreationDefaults`):
  `bypassShortEpisodeChecking` and `alwaysPromoteAsHero` on, `indexAllEpisodes` off. No YouTube channel or playlist and
  no Spotify / Apple id are copied from the podcast: a news organisation on the channel a channel-scoped podcast holds
  would be refused by `ChannelWideYouTubeClaimGuard`, and new uploads on that channel keep landing on the podcast.
- `latestReleased` = the latest release among the moved reports. Moves into an existing organisation do not change
  its `latestReleased`.

## Order (`EpisodeNewsMoveService`)

1. Check every requested id (podcast episodes, news reports, TV episodes and films with those ids). Refuse on any
   not-found or conflict.
2. Save a created organisation, then the reports (`CatalogueParentKindMapper.ToNewsReport`: services, subjects,
   posted / tweeted / Bluesky state, **ignored and removed flags kept**). Missing `services.*.url` are filled from the
   episode's ids without platform calls, as the kind transfer does.
3. Swap each search document under the same id to ContentKind `NewsReport`
   (`CataloguePlayableSearchDocument.For(NewsReport)`); a removed report is deleted from the index instead. Failure:
   500 with `failureIndexing`, episodes kept.
4. Re-read the episodes; any edited during the move are remapped and re-swapped. Re-read the organisation and each
   report; if any is gone: 500, episodes kept.
5. Delete only the moved episode rows.

Re-sending resumes: a report already in the target organisation while its episode row exists is rewritten from the
episode; with the episode row gone, the id is reported in `alreadyMovedEpisodeIds`. Short-link records are left as
they are (as the kind transfer does); old `/podcast/...` links rely on the site's moved-kind redirect.

As with the other moves, an episode released in the last few days may be re-added to the podcast by the Indexer
(#1068).
