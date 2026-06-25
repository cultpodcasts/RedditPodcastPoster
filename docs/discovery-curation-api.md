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
| **Required role** | `curate` (enforced in app via Auth0 `ClientPrincipal`) |

Requests without the `curate` role receive **401 Unauthorized**. Send the same Auth0 bearer token / Easy Auth headers used for other curation endpoints (`Episode`, `Podcast`, `Subject`, etc.).

Base URL is your deployed API host (production: `api-infra` function app). Exact host depends on environment; path is `/api/DiscoveryCuration` unless your hosting adds a prefix.

---

## Endpoints

### GET `DiscoveryCuration`

Returns all **unprocessed** discovery results, optionally including ML-auto-hidden items.

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
  "hiddenCount": 42
}
```

| Field | Description |
|-------|-------------|
| `ids` | GUIDs of **discovery report documents** (`DiscoveryResultsDocument`). Required for POST — pass these back as `ids` in the submit body. |
| `results` | Flat list of candidate episodes from all unprocessed reports (filtered/sorted — see below). |
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

| Before | After POST |
|--------|------------|
| `Unprocessed` | `Processed` |

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
| `Cloud/Api/DiscoveryCurationController.cs` | Routes, `curate` auth |
| `Cloud/Api/Handlers/DiscoveryCurationHandler.cs` | GET query param, POST orchestration |
| `Cloud/Api/Services/DiscoveryResultsService.cs` | Filter, sort, mark processed |
| `Cloud/Api/Dtos/DiscoveryResponse*.cs` | Response DTOs |
| `Class-Libraries/RedditPodcastPoster.Models/DiscoveryResult.cs` | Persisted fields including scorer |
