# Move a news report to a TV show or a film

Curator commands that move one **news report** into a TV show (as a `TvShowEpisode`) or into a new film, with the
**same id** (#1368). They are the news-report form of [Move to TV show](move-to-tv-show.md) and Move to film, for a
report that was filed under the wrong kind (first use: Spotlight, BBC NI, #1367). They are long-term curator tools,
not migration-only.

Both share the target side with the episode moves, so the body, the status codes and the response are the same:
`TvShowMoveTarget` (validation, show by id / name / create, resume and conflict rules, `latestReleased`) and
`FilmMoveTarget` (name, clash check, IMDb/TMDB identity gate). Only the source side differs: a news report is looked up
by id, already has Services only (nothing is materialised from platform ids), and its row is deleted from its news
organisation.

## Endpoints

Both need the `curate` permission.

| Route | Body |
| --- | --- |
| `POST newsreport/{reportId}/tvshow` | `tvShowId` or `tvShowName`, optional `createShow` + `publisher`, optional `title` (as Move to TV show) |
| `POST newsreport/{reportId}/film` | optional `title`, `identityDecision` + `selectedIdentityKey` after an identity prompt (as Move to film) |

Responses (command outcome, not the read model):

- TV show: 202 `{ tvShowId, tvShowName, episodeId, createdShow, failureIndexing, emptiedNewsOrganisationId? }`;
  400 invalid body, removed report or removed show; 404 report / show not found (a name with no show and no
  `createShow` is a 404); 409 several shows have the name, or the report id is already a TV episode of another show
  or a film; 500 failed (re-send resumes). `episodeId` is the report id, which the TV episode keeps.
- Film: 202 `{ filmId, name, failureIndexing, emptiedNewsOrganisationId? }`; 200 with `identityDecision` (ambiguous
  IMDb/TMDB match, nothing written) or `cancelled`; 400 bad name (blank or `/`) or removed report; 404 not found;
  409 a film has the name, or the report id is already a TV episode; 500 failed (re-send resumes).

A removed report is refused (400), as a removed episode is for the TV and film moves.

## Order

1. Save the target: a created show first (fresh id, publisher, no `indexing`, best-effort identity), then the TV
   episode (`CatalogueParentKindMapper.ToTvShowEpisode(NewsReport, TvShow)`), or the film
   (`CatalogueParentKindMapper.ToFilm(NewsOrganisation, NewsReport)`: publisher social fields from the organisation;
   the film is active even when the organisation is removed). Services, subjects, posted / tweeted / Bluesky state and
   the compact YouTube id are kept.
2. Swap the search document under the same id to ContentKind `TvShowEpisode` / `Film`. Failure: 500 with
   `failureIndexing`, report kept.
3. Re-read the show and TV episode, or the film. If gone (a concurrent delete): 500, report kept.
4. Delete the report row.
5. TV only: raise the show's `latestReleased` with the conditional field patch (best effort).
6. Delete the report's short-link record (best effort). The Shortner then resolves the id from search and redirects to
   the TV episode or film page.

Re-sending resumes: a TV episode in the target show (or a film) with the report id while the report row exists is
reused, not saved twice. After a completed move a re-send to the same target is accepted again; to another show it is
a 409.

## Retiring an emptied news organisation

The move never changes or deletes the news organisation, even when it has no reports left. When the move took its last
report, the response carries `emptiedNewsOrganisationId`, and the website offers to retire it with the existing curator
edit, `PATCH newsorganisation/{id}` with `removed: true` (curate scope, no admin needed). Removal is reversible and keeps
the organisation's name, file key and redirects; nothing is hard-deleted.

`emptiedNewsOrganisationId` is omitted when reports remain, when the organisation is already removed, when it no
longer exists (an orphaned report), or when the count or the organisation read failed; none of those fails the move.

A re-send after a completed move returns 202 **without** `emptiedNewsOrganisationId`: the report row, and with it the
organisation id, is gone. So if the first 202 is lost there is no retire prompt, and the curator retires the outlet from
its edit page. The website must not rely on the field appearing on a retry.
