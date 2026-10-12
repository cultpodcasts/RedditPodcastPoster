# Discovery Curation API

Guide for UI applications that curate podcast episodes discovered by the automated discovery pipeline. Includes ML scorer fields added in 2026.

**Audience:** front-end / UI agents integrating with the Cult Podcasts API (`api-infra`).

---

## Purpose and workflow

1. **Scheduled discovery** (`discover-infra`) searches Listen Notes, Spotify, YouTube, and Taddy on a timer, enriches candidates, scores each with an accept/reject ML model, and saves a **discovery report** (`DiscoveryResultsDocument`) to Cosmos DB in `Unprocessed` state.
2. **Curator opens the UI**, which calls **GET** to load all pending results across unprocessed reports.
3. **Curator selects** episodes to accept (create/enrich podcast + episode in the catalogue).
4. **UI calls POST** with document IDs and the subset of result IDs to accept.
5. **API submits** accepted items, marks the report(s) **Processed**, sets per-result state (`Accepted` / `Rejected` / `AcceptError`), re-indexes search, and updates public discovery-info counts.

The ML scorer does **not** auto-accept or auto-reject in the API. It **ranks** and **flags** low-confidence items as hidden from the default queue. The human (or UI bulk actions) still decides what to POST as accepted.

---

## Authentication

| Item | Value |
|------|--------|
| Route prefix | `DiscoveryCuration` |
| HTTP trigger | `AuthorizationLevel.Anonymous` (Azure Functions) |
| **GET permission** | `curate` |
| **POST permission** | `curate` or `curate:supervised` |

Permissions are Auth0 API permissions (JWT `permissions` / OAuth `scope`, checked via `ClientPrincipal.HasScope`). Requests without them receive **401 Unauthorized**.

### Supervised curators

| Permission | Meaning |
|------------|---------|
| `curate` | Normal curator. POST applies the submit immediately. |
| `curate:supervised` | Discovery submits are **held for a supervisor** instead of applied (see [Supervised submit](#supervised-submit-curatesupervised)). Takes precedence over `curate` / `admin` on POST. |
| `supervise` | May review and apply held submissions (endpoints arrive in a follow-up PR; `admin` may also supervise). |

**Supervised users must hold both `curate` and `curate:supervised`.** Only discovery submit is supervised: `curate` is still needed to GET discovery reports and is what lets them edit episodes/podcasts/subjects directly. `curate:supervised` alone can POST but cannot GET.

Optional supervisor mapping: Auth0 `app_metadata.supervisorIds` exposed by a Post-Login Action as the `https://api.cultpodcasts.com/supervisors` claim; when absent, any `supervise`/`admin` holder may supervise. Send the same Auth0 bearer token / Easy Auth headers used for other curation endpoints (`Episode`, `Podcast`, `Subject`, etc.).

Base URL is your deployed API host (production: `api-infra` function app). Exact host depends on environment; path is `/api/DiscoveryCuration` unless your hosting adds a prefix.

---

## Endpoints

### GET `DiscoveryCuration`

Returns all **unprocessed** discovery results, optionally including ML-auto-hidden items. Reports held for supervisor review (`PendingSupervisor`) are listed in `awaitingReview` and their results are **not** in `results`.

#### Query parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `includeHidden` | boolean | `false` | When `true`, includes results where `autoHidden === true`. When `false`, those rows are omitted from `results` but still counted in `hiddenCount`. |

Examples:

```
GET /api/DiscoveryCuration
GET /api/DiscoveryCuration?includeHidden=true
```

#### Response shape

```json
{
  "ids": ["<discovery-report-document-guid>", "..."],
  "results": [ /* DiscoveryResponseItem[] */ ],
  "hiddenCount": 42,
  "awaitingReview": [
    { "id": "<discovery-report-document-guid>", "discoveryBegan": "2026-10-11T06:00:00Z", "resultCount": 37 }
  ]
}
```

| Field | Description |
|-------|-------------|
| `ids` | GUIDs of **discovery report documents** (`DiscoveryResultsDocument`). Required for POST — pass these back as `ids` in the submit body. |
| `results` | Flat list of candidate episodes from all unprocessed reports (filtered/sorted — see below). |
| `awaitingReview` | Reports held by a supervised curator's submit, awaiting a supervisor (`[]` when none). Not curatable: do not put these ids in POST `ids`. |
| `hiddenCount` | Total count of results with `autoHidden: true` across all unprocessed reports, **regardless of** `includeHidden`. Use for a badge (“42 hidden”). |

#### Sort order

Results are sorted server-side:

1. **`acceptProbability` descending** (highest confidence first)
2. **`released` ascending** (older release date first as tie-breaker)

Items with no score (`acceptProbability: null`) sort last (treated as `-1` internally).

#### `DiscoveryResponseItem` fields

| Field | Type | Notes |
|-------|------|-------|
| `id` | GUID | Result ID — use in POST `resultIds` to accept |
| `episodeName` | string? | |
| `showName` | string? | |
| `episodeDescription` | string? | |
| `showDescription` | string? | |
| `released` | ISO 8601 datetime | UTC |
| `duration` | timespan string | e.g. `"01:23:45"` |
| `urls` | object | `apple`, `spotify`, `youTube` URLs |
| `subjects` | string[] | Matched subject tags |
| `youTubeViews` | number? | |
| `youTubeChannelMembers` | number? | |
| `imageUrl` | URI? | |
| `discoverService` | string[] | Source services: `ListenNotes`, `Spotify`, `YouTube`, `Taddy` |
| `enrichedTimeFromApple` | boolean | |
| `enrichedUrlFromSpotify` | boolean | |
| `matchingPodcasts` | array? | `{ name, visible, visibleEpisodes }` for known catalogue matches |
| **`acceptProbability`** | number? | **0.0–1.0** ML estimate that a curator would accept this episode. `null` if not scored (legacy reports). |
| **`autoHidden`** | boolean | **`true`** when `acceptProbability < 0.05` (production threshold). Hidden from default GET. |

**Not exposed on GET:** per-result `state` (all items in an unprocessed report are effectively pending curation).

---

### POST `DiscoveryCuration`

Accepts a subset of discovery results and closes the associated report(s).

#### Request body

```json
{
  "ids": ["<discovery-report-document-guid>"],
  "resultIds": ["<result-guid-to-accept>", "..."]
}
```

| Field | JSON name | Description |
|-------|-----------|-------------|
| Document IDs | `ids` | One or more discovery report document GUIDs (from GET `ids`). |
| Accepted result IDs | `resultIds` | Result GUIDs the curator **accepts**. All other results **in those documents** become rejected when the report is processed. |

**Important:** POST operates on **whole documents**. If a report contains 100 results and the curator accepts 3, send all report IDs in `ids` and only the 3 accepted GUIDs in `resultIds`. The remaining 97 (including auto-hidden items never shown in the default GET) are marked **`Rejected`**.

Hidden items can still be accepted: load them with `includeHidden=true`, include their IDs in `resultIds`.

#### Response shape

```json
{
  "message": "Success",
  "errorsOccurred": false,
  "results": [
    {
      "discoveryItemId": "<guid>",
      "podcastId": "<guid or null>",
      "episodeId": "<guid or null>",
      "message": "CreatedEpisode"
    }
  ],
  "searchIndexerState": "Success"
}
```

| Field | Description |
|-------|-------------|
| `errorsOccurred` | `true` if any submit threw; those items get `AcceptError` state |
| `results[].message` | Url submission outcome enum name (e.g. `CreatedEpisode`, `EpisodeAlreadyExists`, `Error`) |
| `searchIndexerState` | Azure Search indexing outcome for created/updated episodes |

HTTP **500** on catastrophic failure with `{ "message": "Failure" }`.

HTTP **409** `{ "message": "...", "documentIds": [...] }` when any report cannot be claimed (held for supervisor review, already processed, or changed concurrently). Nothing is submitted.

#### Claim before submit

The API first claims **every** report in `ids` by moving it `Unprocessed → Processed` with an ETag-guarded write, **before** any episode is submitted. All-or-nothing: if any claim fails, reports already claimed are released and the request returns 409. This makes a normal submit and a supervised hold mutually exclusive. Resubmitting an already-processed report returns 409 (it used to re-ingest).

#### Supervised submit (`curate:supervised`)

The same request body is **held, not applied**:

1. Every report in `ids` must be `Unprocessed`, else **409** `{ message, documentIds }`.
2. Every id in `resultIds` must belong to those reports, else **400** `{ message, resultIds }` naming the unknown ids. Nothing is held.
3. Reports move `Unprocessed → PendingSupervisor` (ETag-guarded; rolled back on conflict).
4. A `DiscoverySubmission` (container `DiscoverySubmissions`) is saved as `Pending` with the submitter's `sub`, report ids and accepted ids exactly as sent. If the save fails, the submission is deleted and the reports released.
5. Response **202**:

```json
{ "submissionId": "<guid>", "status": "Pending" }
```

**Nothing is ingested**: no episode/podcast creation, no search indexing, no result states. Only a supervisor applying the submission moves held reports to `Processed` and ingests.

---

## Supervised discovery (held submissions and supervisor review)

Some curators (typically AI agents) carry the Auth0 permission **`curate:supervised`**. Their discovery
submits are **held**, not applied, until a supervisor reviews and applies them.

### Report and submission states

| Entity | States |
|--------|--------|
| Discovery report (`DiscoveryResultsDocument.state`) | `Unprocessed` → `PendingSupervisor` → `Processed` |
| Submission (`DiscoverySubmission.status`) | `Pending` → `Applied` \| `PartiallyApplied` \| `ApplyFailed`, or `Pending` → `Released` (no reject/return) |

Submission statuses:

| Status | Meaning | In supervisor queue |
|--------|---------|---------------------|
| `Pending` | Held, nothing ingested | yes |
| `ApplyFailed` | Apply failed. With no `itemOutcomes`: failed before any submit, reports back in `PendingSupervisor` (apply again or release). With `itemOutcomes`: every attempted item failed (retry/abandon) | yes |
| `PartiallyApplied` | Some items ingested, some `Failed`; reports are `Processed` | yes |
| `Applied` | Terminal; every item `Succeeded` or `Abandoned` | no |
| `Released` | Terminal; supervisor released the held reports to `Unprocessed`, nothing ingested | no |

- Only `Processed` reports are ingested as episodes.
- **Only a supervisor apply can promote a `PendingSupervisor` report to `Processed`.** A `curate:supervised`
  caller can never reach `Processed` by any route, even if their token also has `supervise` or `admin`.
- `POST DiscoveryCuration` by a `curate:supervised` caller returns **202** `{ "submissionId", "status": "Pending" }`,
  moves the reports to `PendingSupervisor` and ingests nothing. Held reports are listed under `awaitingReview` on
  `GET DiscoveryCuration`; anyone else submitting a held report gets **409**.

### Permissions

| Permission | May |
|-----------|-----|
| `supervise` | List the review queue, read, save a review, apply |
| `admin` | Same as `supervise`, and overrides supervisor mapping |
| `curate:supervised` | List **own** submissions and read them (status, comments, diff). Never review/apply |

Optional supervisor mapping: the supervised curator's token may carry the custom claim
`https://api.cultpodcasts.com/supervisors` (array of supervisor `sub` values, from Auth0 `app_metadata`).
It is captured on the submission at submit time (`supervisorSubjects`). When present, only those supervisors
(or an admin) may review/apply it; when absent, any supervisor may. Nobody but an admin may review their own submission.

Unknown permission → **401** (controller gate). Permitted but not allowed for this submission → **403**.

### GET `DiscoverySubmissions`

Query. Supervisor/admin: reviewable queue (`Pending` + `ApplyFailed` + `PartiallyApplied` the caller may review), oldest first.
Supervised curator: their own submissions (any status), newest first.

```json
{
  "view": "supervisor",            // or "submitter"
  "pendingCount": 2,               // badge count (supervisor: queue length incl. failures; submitter: own Pending)
  "submissions": [{
    "id": "guid", "status": "Pending", "submitterSubject": "auth0|…", "submittedAt": "…Z",
    "discoveryResultsDocumentIds": ["guid"], "submitterAcceptedCount": 3,
    "supervisorSubject": null, "appliedAt": null, "reportComment": null,
    "itemCommentCount": 0, "applyError": null,
    "failedCount": null, "succeededCount": null, "abandonedCount": null,   // null until an apply ingested
    "releasedAt": null, "releaseReason": null
  }]
}
```

### GET `DiscoverySubmissions/{submissionId}`

Query. Submitter or an allowed supervisor; otherwise 403; unknown id 404.

```json
{
  "id": "guid", "status": "Pending", "etag": "\"…\"", "canReview": true,
  "submitterSubject": "auth0|…", "submittedAt": "…Z",
  "submitterAcceptedResultIds": ["guid"],
  "supervisorAcceptedResultIds": null, "addedBySupervisorResultIds": null, "removedBySupervisorResultIds": null,
  "reportComment": null,
  "itemComments": [{ "resultId": "guid", "comment": "…" }],
  "supervisorSubject": null, "appliedAt": null, "applyError": null, "erroredResultIds": null,
  "itemOutcomes": [{                // null until an apply attempted ingestion
    "resultId": "guid", "state": "Failed",          // Succeeded | Failed | Abandoned
    "episodeId": null, "podcastId": null,
    "message": "Error",                              // ingester outcome (e.g. Created, Enriched, Error)
    "error": "HttpRequestException: …",              // why it failed; null unless last attempt failed
    "attempts": 1, "lastAttemptAt": "…Z"
  }],
  "releasedAt": null, "releaseReason": null,
  "reports": [{
    "id": "guid", "discoveryBegan": "…Z", "state": "PendingSupervisor",
    "results": [ /* every result, same item shape as GET DiscoveryCuration (incl. autoHidden) */ ]
  }],
  "history": [{ "at": "…Z", "actorSubject": "auth0|…", "action": "Submitted" }]
}
```

History actions: `Submitted`, `ReviewSaved`, `ApplyStarted`, `Applied`, `PartiallyApplied`, `ApplyFailed`, `ApplyConflict`, `RetryStarted`, `ItemsAbandoned`, `ReleaseStarted`, `Released`, `StateChanged` (admin override; also carries `fromStatus`, `toStatus`, `reason`; those fields are `null`/absent on other entries).

### PUT `DiscoverySubmissions/{submissionId}/review`

Command (`supervise`/`admin`). Saves a draft review; nothing is ingested. Omitted (`null`) fields are unchanged.

```json
{
  "etag": "\"…\"",                                 // required, from GET
  "reportComment": "…",                            // "" clears
  "itemComments": [{ "resultId": "guid", "comment": "…" }],  // replaces all; [] clears
  "acceptedResultIds": ["guid"]                    // final set: any results within the held reports
}
```

**202** command outcome (`DiscoverySubmissionCommandResponse`):

```json
{ "submissionId": "guid", "status": "Pending", "etag": "\"…\"",
  "addedBySupervisorResultIds": ["guid"], "removedBySupervisorResultIds": ["guid"],
  "message": null, "ids": null, "outcome": null, "itemOutcomes": null }
```

Errors: **400** ids outside the held reports (`message`) or comment over 4000 chars; **409** stale `etag` or the
submission is `Applied`; **403**/**404** as above.

### POST `DiscoverySubmissions/{submissionId}/apply`

Command (`supervise`/`admin`). Body `{ "etag": "…", "acceptedResultIds": ["guid"] | null }`. When
`acceptedResultIds` is omitted, the saved review set is used, else the submitter's set.

1. Verifies every report is still `PendingSupervisor` and the ids are within those reports (else 409/400).
2. Claims the submission (ETag-guarded; a concurrent apply gets 409 and ingests nothing), records final set + diff.
3. **Before any ingestion**, moves every report `PendingSupervisor` → `Processed` with a guarded (ETag) write,
   all-or-nothing. If any report cannot be claimed: **409**, nothing ingested, submission stays `Pending`
   (history `ApplyConflict`).
4. Replays the normal discovery ingest with the final set, stamps result states (`Accepted`/`Rejected`/`AcceptError`),
   re-indexes search.
5. Records per-item outcomes (`itemOutcomes`) and derives the status: `Applied` (all succeeded),
   `PartiallyApplied` (some failed) or `ApplyFailed` (all failed). Items the run never reached (aborted
   part-way) are `Failed` with a reason. Also records `supervisorSubject`, `appliedAt`.

**200** command outcome (`status` `Applied` or `PartiallyApplied`), same shape as review plus `outcome` (this run,
same shape as the `POST DiscoveryCuration` response, each item now with a nullable `error` reason) and
`itemOutcomes` (cumulative). Re-applying an `Applied` submission is a no-op **200** (`message: "Already applied."`).
Apply on `PartiallyApplied`, or `ApplyFailed` with outcomes, is **409** (use retry; never re-ingests).

**500** with `status: "ApplyFailed"`: either every item failed (see `itemOutcomes`, use retry), or the run failed before
any episode submit, in which case `itemOutcomes` is null and the reports were returned to `PendingSupervisor`
(apply again or release). Once an episode submit was attempted reports stay `Processed` (no double ingestion).

> Note: an ingester result that is not an exception (e.g. `NoUrls`, `DifferentPodcasts`) counts as `Succeeded`
> with that `message`, matching the existing `Accepted` result state; only exceptions are `Failed`.

### POST `DiscoverySubmissions/{submissionId}/retry`

Command (`supervise`/`admin`). For `PartiallyApplied`, or `ApplyFailed` with `itemOutcomes`. Gated by submission
state, not report state (reports are `Processed`). Body:

```json
{ "etag": "\"…\"", "abandonResultIds": ["guid"] }   // optional: failed items to drop instead of retrying
```

Resubmits **only** items still `Failed` (never `Succeeded` or `Abandoned`), restamps only those results on their
reports, updates `itemOutcomes` (attempts, error), and re-derives status (`Applied` when nothing is `Failed`).
Abandoning all failed items closes the submission as `Applied` without ingesting. **200** command outcome as apply.
Errors: **400** abandoning an id that is not `Failed` (`ids`); **409** stale etag or nothing to retry; **403**/**404**.

### POST `DiscoverySubmissions/{submissionId}/release`

Command (`supervise`/`admin`; never `curate:supervised`). Releases the lock on a submission that has ingested
nothing (`Pending`, or `ApplyFailed` without `itemOutcomes`). Body `{ "etag": "…", "reason": "…" | null }`.
Moves each held report `PendingSupervisor` → `Unprocessed` (guarded), sets `status: "Released"`, `releasedAt`,
`releaseReason`, history `ReleaseStarted`/`Released`. Ingests nothing. **200** command outcome; `ids` lists reports
that were not held (left unchanged). **409** stale etag or submission already ingested/terminal.

### POST `DiscoveryReports/{documentId}/release` (admin)

Admin only (`admin`; not `supervise`, never `curate:supervised`). Returns a report stuck in `PendingSupervisor`
with no live submission (no `Pending`/unapplied `ApplyFailed` submission listing it) to `Unprocessed` (guarded).
Body `{ "reason": "…" | null }` (logged). **200** `{ "documentId", "status": "Released", "message" }`;
**409** if not `PendingSupervisor` or still held by a live submission (`submissionIds` lists them); **404** unknown.

### AI-teaching data

Each submission persists `submitterAcceptedResultIds`, `supervisorAcceptedResultIds`,
`addedBySupervisorResultIds` (the AI missed) and `removedBySupervisorResultIds` (the AI wrongly accepted),
plus report and item comments. No notifications are sent; UIs use `pendingCount` for a badge.

---

## ML scorer semantics

| Concept | Behaviour |
|---------|-----------|
| **Model** | LightGBM + MiniLM embeddings; trained on historical accept/reject labels |
| **`acceptProbability`** | Estimated P(curator accepts). Higher = more likely worth reviewing first |
| **`autoHidden`** | Set at ingest when `acceptProbability < 0.05` (config: `discover__scorer__AutoHideThreshold`) |
| **Persistence** | Both fields stored on each `DiscoveryResult` in Cosmos |
| **Default GET** | Omits `autoHidden: true` rows from `results` |
| **Curator authority** | Scorer never writes `Accepted`/`Rejected`; only POST does |

Public **discovery-info** counts (site badge / notification) use **visible** results only (`!autoHidden`).

---

## State after POST

### Document level (`DiscoveryResultsDocument`)

| Before | After POST (`curate`) | After POST (`curate:supervised`) |
|--------|------------|------------|
| `Unprocessed` | `Processed` | `PendingSupervisor` |
| `PendingSupervisor` | 409, unchanged | 409, unchanged |

`PendingSupervisor → Processed` happens only when a supervisor applies the held submission. `SetProcessed` (Discover CLI) and result stamping never promote held reports.

### Result level (`DiscoveryResult`)

| Condition | State |
|-----------|--------|
| ID in POST `resultIds`, submit succeeded | `Accepted` |
| ID in POST `resultIds`, submit threw | `AcceptError` |
| All other results in the document | `Rejected` |

Enum values: `Unprocessed`, `Rejected`, `Accepted`, `AcceptError`.

---

## Legacy / unscored reports

Discovery reports created **before** the scorer deployed (or before backfill) have:

- `acceptProbability`: **`null`**
- `autoHidden`: **`false`**

They appear in the default GET like before, unsorted by score (sorted last due to null probability). `hiddenCount` is **0** for those reports.

No backfill is required for the UI to function; scorer fields simply absent until new discovery runs or a future backfill job.

---

## Example responses

### GET default (`includeHidden=false`)

```json
{
  "ids": ["a1b2c3d4-e5f6-7890-abcd-ef1234567890"],
  "hiddenCount": 2,
  "results": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "showName": "True Crime Weekly",
      "episodeName": "Inside the Compound",
      "released": "2026-06-12T08:00:00Z",
      "acceptProbability": 0.82,
      "autoHidden": false,
      "matchingPodcasts": [
        { "name": "Similar Show", "visible": true, "visibleEpisodes": 12 }
      ],
      "subjects": ["Cult", "True Crime"],
      "discoverService": ["ListenNotes", "Spotify"]
    },
    {
      "id": "22222222-2222-2222-2222-222222222222",
      "showName": "Gaming Podcast",
      "episodeName": "Cult of the Lamb speedrun",
      "released": "2026-06-12T09:30:00Z",
      "acceptProbability": 0.06,
      "autoHidden": false,
      "matchingPodcasts": null,
      "subjects": [],
      "discoverService": ["YouTube"]
    }
  ]
}
```

Two results are auto-hidden server-side; they are not in `results` but `hiddenCount: 2` tells the UI they exist.

### GET with hidden (`includeHidden=true`)

Same as above, plus hidden rows:

```json
{
  "ids": ["a1b2c3d4-e5f6-7890-abcd-ef1234567890"],
  "hiddenCount": 2,
  "results": [
    { "id": "11111111-...", "acceptProbability": 0.82, "autoHidden": false },
    { "id": "22222222-...", "acceptProbability": 0.06, "autoHidden": false },
    { "id": "33333333-...", "acceptProbability": 0.02, "autoHidden": true },
    { "id": "44444444-...", "acceptProbability": 0.01, "autoHidden": true }
  ]
}
```

### POST accept two items

```json
POST /api/DiscoveryCuration
{
  "ids": ["a1b2c3d4-e5f6-7890-abcd-ef1234567890"],
  "resultIds": [
    "11111111-1111-1111-1111-111111111111",
    "22222222-2222-2222-2222-222222222222"
  ]
}
```

All other results in that document (including hidden) → `Rejected`.

---

## UI use cases

### 1. Ranked review queue (default)

Use default GET. Results arrive **highest `acceptProbability` first**. Curator works top-down; low-confidence noise is already removed from view.

### 2. “Hidden” tab or toggle

- Show badge: **`hiddenCount`** from default GET (“Review 42 hidden”).
- Toggle or second tab: refetch with **`?includeHidden=true`**.
- Style hidden rows (muted, separate section). Allow individual accept from hidden list.

### 3. Score badges

Display `acceptProbability` as percentage or tier:

| Probability | Suggested UI |
|-------------|--------------|
| ≥ 0.50 | Green / “Likely match” |
| 0.05 – 0.50 | Neutral / “Review” |
| < 0.05 | Hidden by default; if shown, red / “Unlikely” |
| `null` | Gray / “Unscored” (legacy) |

### 4. Bulk accept high-confidence

Client-side filter `acceptProbability >= 0.5` (or user-adjustable threshold), select all, POST those `resultIds`. Remaining visible + all hidden → rejected on submit.

**Caution:** confirm with user before bulk reject of unreviewed hidden items, or review hidden tab first.

### 5. Matching podcast highlight

`matchingPodcasts` with `visible: true` and `visibleEpisodes > 0` indicates strong catalogue signal — prioritize in UI even if probability is moderate.

### 6. Empty queue vs hidden-only

If `results.length === 0` but `hiddenCount > 0`, show “All pending items are auto-hidden” with CTA to open hidden review — not “nothing to curate”.

### 7. Submit UX

- Always POST the **`ids`** array from the last GET (all unprocessed report documents being closed).
- **`resultIds`** = checked rows only (from visible and/or hidden lists).
- Warn if closing reports with unchecked visible items (they will be rejected).
- Surface `errorsOccurred` and per-item `message` / `AcceptError` for retry flows.

### 8. Legacy reports

No score columns → fall back to release-date sort (API still applies nulls-last). No hidden tab needed (`hiddenCount: 0`).

---

## Related backend docs

- [interim-deployment.md](interim-deployment.md) — deploying API + discover function when CI is inactive
- [discovery-backfill.md](discovery-backfill.md) — local `Discover.exe` CLI (not this API)

## Source files (for agents patching the API)

| File | Role |
|------|------|
| `Cloud/Api/Controllers/DiscoveryCurationController.cs` | Routes, `curate` / `curate:supervised` auth |
| `Cloud/Api/Handlers/Discovery/PostDiscoveryCurationHandler.cs` | Supervised hold vs apply routing |
| `Cloud/Api/Services/Discovery/DiscoverySubmissionHoldService.cs` | Supervised hold |
| `Cloud/Api/Services/Discovery/DiscoveryCurationSubmitService.cs` | Claim, submit, stamp |
| `Cloud/Api/Handlers/DiscoveryCurationHandler.cs` | GET query param, POST orchestration |
| `Cloud/Api/Services/DiscoveryResultsService.cs` | Filter, sort, mark processed |
| `Cloud/Api/Dtos/DiscoveryResponse*.cs` | Response DTOs |
| `Class-Libraries/RedditPodcastPoster.Models/DiscoveryResult.cs` | Persisted fields including scorer |
| `Cloud/Api/Controllers/DiscoverySubmissionsController.cs` | Supervised-submission routes and permission gates |
| `Cloud/Api/Services/Discovery/DiscoverySubmissionReviewService.cs` | List/get/review/apply/retry/release |
| `Cloud/Api/Services/Discovery/DiscoverySubmissionRules.cs` | Who may review; supervisor diff |
| `Class-Libraries/RedditPodcastPoster.Models/Discovery/DiscoverySubmission.cs` | Persisted submission |


### In-progress state (`Applying`) and manual state override

Apply, retry and release first persist `status: "Applying"` in their ETag-guarded claim, with
`inProgressOperation` (`Apply` | `Retry` | `Release`) and `inProgressSince` (both returned in list/detail, `null` otherwise).
While `Applying`, review, apply, retry, release and stuck-report release all return **409**; the submission is shown
in the supervisor queue.

The final write is merged and retried (up to 3 attempts) if it gets a 412 while the submission is still held by the
same operation; if the outcome still cannot be recorded the command returns **500** with
`"Outcome could not be recorded…"` (never `200`).

An `Applying` submission is never changed automatically. If an operation dies mid-way, an admin inspects the
reports/catalogue and sets the status by hand:

`POST /DiscoverySubmissions/{id}/state` — **admin only** (`admin` permission).

Body:
```json
{ "status": "Pending", "etag": "\"…\"", "reason": "Apply crashed during claim; reports checked" }
```
- `status` (required): a `DiscoverySubmissionStatus` name, case-insensitive — `Pending`, `Applied`, `ApplyFailed`,
  `PartiallyApplied`, `Released`. `None`, `Applying` (would be stuck with no running operation), numbers and unknown
  names → **400**.
- `etag` (required): the submission's current ETag. Missing or stale → **409**.
- `reason` (optional, ≤ 4000 chars, longer → **400**).

Effect: sets `status`, clears `inProgressOperation`, `inProgressSince` (and the internal in-progress fields), and appends
a history entry `{ "action": "StateChanged", "actorSubject": "<admin>", "fromStatus": "Applying", "toStatus": "Pending",
"reason": "…" }`. Report states and item outcomes are **not** changed — fix those separately (e.g.
`POST /DiscoveryReports/{documentId}/release`) if needed.

Responses: **200** standard command response (new `etag`, `submissionStatus`, `diff`, `itemOutcomes`); **400** invalid
status (incl. `None`/`Applying`)/reason; **403** not admin; **404** unknown submission; **409** stale/missing ETag.

### Error bodies (camelCase DTOs)

- `POST /DiscoveryCuration` 409: `{ "message": "One or more reports are not available for curation.", "documentIds": ["<guid>"] }`
- Supervised `POST /DiscoveryCuration` 400: `{ "message": "Result ids are not in the submitted reports.", "resultIds": ["<guid>"] }`
- Missing subject (400), forbidden (403), not found (404), failure (500): `{ "message": "..." }`
- `DiscoverySubmissions/*` command errors (409/400/500) use the standard command response body.
