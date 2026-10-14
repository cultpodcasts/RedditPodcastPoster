# YouTube thumbnails — choice at index time and delayed re-check

## Choosing a thumbnail

`YouTubeThumbnailResolver.GetImageUrlAsync` (YouTube library) is the single place a YouTube episode
image is chosen: enrich, episode provider, searcher, URL categoriser and `ImageUpdater` all call it.
It walks the API's thumbnail candidates largest-first and downloads each one. A candidate is usable
only when:

1. the response is a **success status** (a withdrawn tier answers **404** with a decodable placeholder body), and
2. the body is a JPEG taller than **90 px** (`YouTubeThumbnailValidation.PlaceholderMaxHeight`); the
   `default.jpg` tier only needs dimensions, since it is 120x90 by design.

`GetUsableCandidateUrls` is the no-download variant used by Bluesky cards (API heights only).

## Why a delayed re-check

YouTube can serve a real `maxresdefault` / `hq720` frame shortly after upload and **withdraw it
later**, typically when the creator's custom thumbnail is processed at a resolution below 1280 wide.
The URL then answers **404 with YouTube's 1097-byte 120x90 grey "missing image" placeholder**. A
probe at index time cannot see that coming.

Real case (2026-10-14): a Best Life After episode released 23:06 UTC was indexed about an hour later
with `maxresdefault.jpg`. Next morning: `maxresdefault` and `hq720` → 404 / 1097 B / 120x90;
`sddefault` 200 / 60207 B / 640x480; `hqdefault` 200 / 39854 B / 480x360; `mqdefault` 200 / 20678 B / 320x180.
Rules: `YouTubeThumbnailProbeRules`, `RecentYouTubeThumbnailRecheckRules`.

## The re-check (Indexer)

`HourlyOrchestration` runs the `YouTubeThumbnailRecheck` activity after `Categoriser` and before
`Poster` / `Publisher` / `Bluesky`, over the already-loaded `RecentEpisodeCandidates` (no extra
Cosmos read). `RecentYouTubeThumbnailRechecker`:

- considers episodes released within `RecheckWindowHours` whose stored YouTube image is
  `maxresdefault` or `hq720`, newest first;
- downloads each stored image (one GET, no YouTube quota), up to `MaxStoredImageProbesPerRun`;
- for an image that now fails the probe, re-resolves via `ImageUpdater` (one `videos.list` call plus
  at most one probe per candidate), up to `MaxReResolvesPerRun`, and saves only when the image changed;
- **never clears** an image — if nothing better resolves, the stored URL is kept and logged;
- runs only when this hour's index passes resolved YouTube without error (YouTube-enabled hours,
  every 3 h), so a 48 h window gives ~16 chances.

Search documents re-derive their compact image code from Cosmos on the next index run.

| Setting (`youtubeThumbnailRecheck__…`) | Default | Validated |
|---|---|---|
| `Enabled` | `true` | — |
| `RecheckWindowHours` | `48` | > 0 |
| `MaxStoredImageProbesPerRun` | `25` | > 0 |
| `MaxReResolvesPerRun` | `10` | > 0 |

Defaults live only in `YouTubeThumbnailRecheckOptions` (single source of truth); set an app setting
only to override. Log keys: see
[indexing-app-insights-queries.md](indexing-app-insights-queries.md) (`YouTubeThumbnailRecheck …`).
