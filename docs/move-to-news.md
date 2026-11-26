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
- `latestReleased` = the latest release among the moved reports.

A move into an existing organisation raises its `latestReleased` to the newest moved report after the episode rows
are deleted, with a conditional field patch (`INewsOrganisationRepository.RaiseLatestReleased`: only when the stored
value is missing or older, so it is never lowered and nothing else on the document is written). Best effort, as in
the TV move: a failure is logged and the move is still a 202. This matters because a created organisation has no
YouTube channel, so no Indexer pass raises it when later batches join.

## Order (`EpisodeNewsMoveService`)

1. Check every requested id (podcast episodes, news reports, TV episodes and films with those ids). Refuse on any
   not-found or conflict.
2. Save a created organisation, then the reports (`CatalogueParentKindMapper.ToNewsReport`: services, subjects,
   posted / tweeted / Bluesky state, **ignored and removed flags kept**). Missing `services.*.url` are filled from the
   episode's ids without platform calls, as the kind transfer does. One exception to keeping `ignored`, shared with
   the kind transfer (`CatalogueShortEpisodeIgnore.IsLengthDerivedIgnored`): an episode ignored only for being under
   the podcast's short-episode threshold (`minimumDuration`, else the posting-criteria floor unless the podcast
   bypassed short-episode checking) is not ignored as a report, because a news organisation bypasses that check.
   Longer ignored episodes stay ignored. So the same episode gets the same report whichever route moved it.
3. Swap each search document under the same id to ContentKind `NewsReport`
   (`CataloguePlayableSearchDocument.For(NewsReport)`); a removed report is deleted from the index instead. Failure:
   500 with `failureIndexing`, episodes kept.
4. Re-read the episodes (a filtered `IN` query per 100 ids, not a scan of the podcast); any edited during the move
   are remapped and re-swapped. An episode row deleted by someone else meanwhile already has its report, so it is
   listed in `movedEpisodeIds` (and logged). Re-read the organisation and each report; if any is gone: 500, episodes
   kept.
5. Delete only the moved episode rows.
6. Raise an existing organisation's `latestReleased` (above), then delete the moved episodes' short-link records, as
   the TV and film moves do (both best effort). With no record the Shortner looks the id up in search, now ContentKind
   `NewsReport`, and redirects to the news page in one hop.

Re-sending resumes: a report already in the target organisation while its episode row exists is rewritten from the
episode; with the episode row gone, the id is reported in `alreadyMovedEpisodeIds`.

As with the other moves, an episode released in the last few days may be re-added to the podcast by the Indexer
(#1068).

## Removed episodes

Unlike the TV and film moves, which refuse a removed episode (400), Move to news **moves removed episodes**: the
report keeps `removed` and its search document is deleted. This is intended, so flags are preserved; it lets a
curator mark unplayable episodes removed and still move them with the rest (as was done for Court TV). Do not change
it to match TV / film.
