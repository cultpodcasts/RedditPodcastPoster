# Move news reports between news organisations

Issue #1397. Use this to merge two news organisations that are really the same publisher, for example
the two Associated Press organisations. For moving one report to TV/film, see
[move-news-report.md](move-news-report.md). For moving episodes into news, see [move-to-news.md](move-to-news.md).

## Routes (RPP Functions, `Cloud/Api`)

| Route | Scope | Purpose |
| --- | --- | --- |
| `POST newsorganisation/{sourceId}/reports/move` | `curate` | Move reports from the source to the target organisation |
| `DELETE newsorganisation/{id}[?redirectToNewsOrganisationId={targetId}]` | `admin` | Hard-delete an organisation that has no reports, optionally redirecting its old name route |

The move is `curate`, like every other move route and the organisation PATCH (confirmed by Jon on #1401). The
delete is `admin`, because it cannot be undone (like `DELETE newsreport` and organisation rename). The Edge M2M
client is not given either new scope.

### Move request

```json
{
  "targetNewsOrganisationId": "<guid>",
  "reportIds": ["<guid>", "..."],
  "moveYouTubeChannels": true
}
```

- `reportIds` is optional. Leave it out (or send `null`) to move the next 500 reports in the source. Repeat the call
  until `remainingReportCount` is 0. An explicit empty array is a 400, so an empty client selection never moves a
  batch. At most 500 ids are allowed per request, and `00000000-…` is refused.
- `moveYouTubeChannels` (default `false`) also moves the source's main YouTube channel (and its playlist, when the
  main channel owns it) and its additional channels into the target's `additionalYouTubeChannels`. Channels the
  target already has are skipped, and `movedYouTubeChannelIds` lists only the channels actually added. The source's
  YouTube fields are cleared and saved first, so the duplicate-channel check never sees one channel on both
  organisations.
  - The main channel's `youTubePlaylistIdHistory`, `youTubePlaylistOrder` and `youTubeIndexingHints` are **not**
    carried to the target (additional channels have no such fields). They are logged.
  - A main channel that indexes a playlist **owned by another channel** is refused: as an additional channel it would
    index the whole channel. No organisation is written; set the target's channels by hand with
    `PATCH newsorganisation/{id}`.

### What a move does

1. Validates the request: the target is required, differs from the source, exists and is not removed.
2. Takes the parent indexing lease (the indexers' single-flight marker) on both organisations for the whole request.
   If either is being indexed, the request is refused with 409 and `retryable: true` and nothing is written; retry in a few
   minutes (an id conflict 409 has no `retryable`). The lease uses the indexers' own stale window. This
   stops an indexer pass from updating a source row that is about to be deleted, or indexing the source's channel
   mid-move.
3. Plans every id before any write. An id is:
   - **moved**: found in the source (also when a copy already exists in the target, as after a retry);
   - **alreadyMoved**: found only in the target;
   - **conflict**: found in a third organisation, as a TV episode or film, or its YouTube video (`youTubeId`) is
     already a different report of the target. The request returns 409;
   - **notFound**: not found anywhere. The request returns 404.
   There are no writes on 404 or 409.
4. For each report: sets the target organisation's properties (id, name, `ParentRemoved`, search terms,
   language), saves it in the target partition and swaps its search document (`SeriesName` becomes the target
   name). Removed or parent-removed reports are deleted from search (ignored reports stay in search, as everywhere
   else). If indexing fails, the response is 500 with `failureIndexing` and the source rows are kept, so a retry is
   safe.
5. Re-reads the target rows, then deletes the source rows. A row already gone counts as deleted. If some deletes
   fail, the response is 500 and `movedReportIds` lists only the reports that really moved; the rest are still in the
   source. Re-send to finish.
6. Patches the target's `latestReleased` (a conditional patch, never lowered) and deletes the KV short-link record
   for each moved report, and also for each `alreadyMovedReportIds` report, so a re-send after a part-way failure
   always finishes these steps. The Shortner then resolves the id from search, which now names the target.
7. Optionally moves the YouTube channels (see above). If saving the target fails, the source's channels are restored
   and the response is still 202 with `youTubeChannelMoveFailed: true`: **re-send with `moveYouTubeChannels` to
   retry**. Only if that restore also fails (or for the foreign-playlist refusal) does the error point to
   `PATCH newsorganisation/{id}`; the original channel payload is in the error log.
8. Returns `remainingReportCount` and, when the source is empty and not removed, `emptiedNewsOrganisationId`.

Response codes: 202 Accepted, 400 bad request, 404 not found, 409 conflict or being indexed, 500 failed.

### Retiring the emptied organisation

A soft remove isn't enough: name lookups include removed organisations, so the old name would still
return 409 duplicate. Call `DELETE newsorganisation/{id}` (admin).

- It returns 409 with `remainingReportCount` while any report (removed ones included) is still in the
  organisation, and 409 with `message` and `retryable: true` while it is being indexed. The count and delete run under the parent
  indexing lease, so an indexer cannot add a report in between.
- Pass `?redirectToNewsOrganisationId={targetId}` (the organisation the reports went to). When the names differ, the
  delete records a news-organisation redirect from the old name route to the target's, using the same Cloudflare KV
  redirect and loop guard as the admin rename. No redirect is needed (or written) when the names match
  case-insensitively, and none is written while another news organisation still has the old name. A missing,
  removed or self target, or a redirect the loop guard refuses, is a 400 and nothing is deleted. The redirect is
  written before the delete, so a failed delete can be retried with the same request.
- When the organisation is empty, it deletes the organisation and releases its file key. 200 and 404 have empty
  bodies.

## Associated Press runbook

`88acfa0c-7e6c-4d19-9ceb-3bb8fc3d3afc` stays. `ff5bb494-38f1-4eca-ac72-54aa5c836865` (176 reports, AP
Archive channel `UCHTK-2W11Vh1V4uwofOfR4w`) is folded into it.

1. Back up both organisation documents and the `ff5bb494…` reports.
2. `POST newsorganisation/ff5bb494-38f1-4eca-ac72-54aa5c836865/reports/move` with
   `{"targetNewsOrganisationId":"88acfa0c-7e6c-4d19-9ceb-3bb8fc3d3afc","moveYouTubeChannels":true}`. A 409 that says
   an organisation is being indexed means: wait a few minutes and re-send.
3. Check that `ff5bb494…` has 0 reports, that search shows them under "Associated Press", and that short
   links resolve.
4. `DELETE newsorganisation/ff5bb494-38f1-4eca-ac72-54aa5c836865?redirectToNewsOrganisationId=88acfa0c-7e6c-4d19-9ceb-3bb8fc3d3afc`
   (admin). Both are named "Associated Press", so no redirect is written; the parameter is harmless and keeps the
   runbook general.
