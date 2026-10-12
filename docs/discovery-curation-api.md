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
