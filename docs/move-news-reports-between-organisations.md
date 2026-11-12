# Move news reports between news organisations

Issue #1397. Use this to merge two news organisations that are really the same publisher, for example
the two Associated Press organisations. For moving one report to TV/film, see
[move-news-report.md](move-news-report.md). For moving episodes into news, see [move-to-news.md](move-to-news.md).

## Routes (RPP Functions, `Cloud/Api`)

| Route | Scope | Purpose |
| --- | --- | --- |
| `POST newsorganisation/{sourceId}/reports/move` | `curate` | Move reports from the source to the target organisation |
| `DELETE newsorganisation/{id}` | `admin` | Hard-delete an organisation that has no reports |

The move is `curate`, like every other move route and the organisation PATCH. The delete is `admin`,
because it cannot be undone (like `DELETE newsreport` and organisation rename). The Edge M2M client is
not given either new scope.

### Move request

```json
{
  "targetNewsOrganisationId": "<guid>",
  "reportIds": ["<guid>", "..."],
  "moveYouTubeChannels": true
}
```

- `reportIds` is optional. Leave it out to move the next 500 reports in the source. Repeat the call until
  `remainingReportCount` is 0. At most 500 ids are allowed per request.
- `moveYouTubeChannels` (default `false`) also moves the source's main YouTube channel (and its playlist,
  if the playlist belongs to that channel) and its additional channels into the target's
  `additionalYouTubeChannels`. Channels the target already has are skipped. The source's YouTube fields
  are cleared and saved first, so the duplicate-channel check never sees one channel on both organisations.

### What a move does

1. Validates the request: the target is required, differs from the source, exists and is not removed.
2. Plans every id before any write. An id is:
   - **moved**: found in the source (also when a copy already exists in the target, as after a retry);
   - **alreadyMoved**: found only in the target;
   - **conflict**: found in a third organisation, or as a TV episode or film. The request returns 409;
   - **notFound**: not found anywhere. The request returns 404.
   There are no writes on 404 or 409.
3. For each report: sets the target organisation's properties (id, name, `ParentRemoved`, search terms,
   language), saves it in the target partition and swaps its search document (`SeriesName` becomes the
   target name). Removed or excluded reports are deleted from search. If indexing fails, the response is
   500 with `failureIndexing` and the source rows are kept, so a retry is safe.
4. Re-reads the target rows, then deletes the source rows.
5. Raises the latest-released event and deletes the KV short-link record for each moved report. The
   Shortner then resolves the id from search, which now names the target.
6. Optionally moves the YouTube channels (see above). If this fails after the reports moved, the
   response is still 202 with `youTubeChannelMoveFailed: true`. Fix it with the organisation PATCH.
7. Returns `remainingReportCount` and, when the source is empty and not removed, `emptiedNewsOrganisationId`.

Response codes: 202 Accepted, 400 bad request, 404 not found, 409 conflict, 500 failed.

### Retiring the emptied organisation

A soft remove isn't enough: name lookups include removed organisations, so the old name would still
return 409 duplicate. Call `DELETE newsorganisation/{id}` (admin). It returns 409 with
`remainingReportCount` while any report (removed ones included) is still in the organisation. When the
organisation is empty, it deletes the organisation and releases its file key.

## Associated Press runbook

`88acfa0c-7e6c-4d19-9ceb-3bb8fc3d3afc` stays. `ff5bb494-38f1-4eca-ac72-54aa5c836865` (176 reports, AP
Archive channel `UCHTK-2W11Vh1V4uwofOfR4w`) is folded into it.

1. Back up both organisation documents and the `ff5bb494…` reports.
2. `POST newsorganisation/ff5bb494-38f1-4eca-ac72-54aa5c836865/reports/move` with
   `{"targetNewsOrganisationId":"88acfa0c-7e6c-4d19-9ceb-3bb8fc3d3afc","moveYouTubeChannels":true}`.
3. Check that `ff5bb494…` has 0 reports, that search shows them under "Associated Press", and that short
   links resolve.
4. `DELETE newsorganisation/ff5bb494-38f1-4eca-ac72-54aa5c836865` (admin).
