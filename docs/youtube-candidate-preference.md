# YouTube candidate preference (📱 and still-image penalties)

When the YouTube finders (`PlaylistItemFinder`, `YouTubeSearchResultFinder`) pick a video for an episode, the
pick is passed through `YouTubeCandidatePreferenceApplier` (decorators `PreferringPlaylistItemFinder` /
`PreferringYouTubeSearchResultFinder`). This covers every YouTube lookup that chooses between candidates:
enrichment, index-time enrichment and submit-url lookups that resolve via `YouTubeUrlResolver`.
Submit of an explicit YouTube URL, index-from-playlist and discovery do not choose between candidates and are unaffected.

## Why

Some channels upload the same episode twice:

- **IndoctriNation** (`UC67JLTD22dSnVadqXKcYPcA`) uploads a real video (e.g. "The Oracle's Daughter with Sarah Green",
  `4AcC36Aw2nY`) and a still-image version titled "IndoctriNation - <title>" (`S03n89L5YLA`).
- **The Afro Atheist** (`UC-Vn8JbQ4sbMKYzz7HFMtFg`) uploads duplicates whose `snippet.title` ends in 📱
  (U+1F4F1), e.g. "EL-ROI CANNOT SAVE ANYONE 📱" (`HIuO5jaDWFs`) vs "EL-ROI CANNOT SAVE ANYONE" (`ExbrJpfl4Cg`).
  The emoji is literally in the title; the 📱 uploads are ordinary 16:9 videos, not Shorts, and the API exposes no
  structured field for them, so the rule matches the title token.

## How

1. The finder picks a match exactly as before.
2. If the candidate list has other entries, contenders are those whose title (📱 removed, lower-cased) has a FuzzySharp
   token-set ratio of at least `ContenderMinTitleSimilarity` with the pick, at most `MaxContenders` of them.
   No contenders: the pick is returned, with no extra API call and no thumbnail fetch.
3. Contenders are fetched in one `videos.list` call and must be completed public videos, pass
   `YouTubeVideoDurationMatcher`, be within `ContenderMaxDurationDifferenceSeconds` of the pick and pass
   `YouTubeEnrichmentCandidate.MeetsStrongMatch`, the same checks as the finder's own match.
   The channel trailer "IndoctriNation Podcast" (`DJg4gR1j4n0`) is below `ContenderMinTitleSimilarity` against
   a pick such as "IndoctriNation - The Oracle's Daughter with Sarah Green", so preference does not fetch it.
   A true subset title (for example "IndoctriNation", whose tokens are contained in that pick) scores above the
   gate. `YouTubeVideoDurationMatcher` and `MeetsStrongMatch` are what stop that upload replacing the pick.
   When the finder's pick has no video details, the duration band against the pick is skipped and only
   the episode-duration check applies.
4. The pick and validated contenders are scored: `-PenalisedTitlePenalty` for a title containing any
   `PenalisedTitleTokens` (default 📱), `-StaticImagePenalty` for a still image. Highest score wins; ties keep the pick.

Still images are detected by `YouTubeThumbnailMotionDetector` from the three 120x90 auto thumbnails
`https://i.ytimg.com/vi/<id>/1.jpg`, `2.jpg`, `3.jpg`. **Video is never downloaded.** The client follows at most
`MaxThumbnailRedirects` redirects and reads at most `MaxThumbnailBytes` (also the `HttpClient` response-buffer
ceiling and the decoded RGB ceiling). Frames are decoded with SkiaSharp 4.153.1 (MIT). The YouTube library also
references `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.153.1 so Linux Azure Functions and Linux CLIs load
`libSkiaSharp` without fontconfig; Windows uses the Win32 native assets that ship with SkiaSharp. Dimensions
are read from the image header before a bitmap is allocated, and a frame whose decoded RGB buffer would exceed
`MaxThumbnailBytes` is rejected. The largest mean absolute RGB difference between the frames (every 2nd
pixel) below `StaticImageThreshold` means static. A missing thumbnail, timeout (`ThumbnailFetchTimeoutSeconds`),
body over the byte ceiling, redirect failure, decode failure, size mismatch, blank/placeholder frame
(pixel std-dev below `PlaceholderMaxPixelStdDev`), or any other read failure is `Unknown`, which gets no penalty
(fail-open). Caller cancellation is not treated as Unknown.

## Calibration (October 2026, 69 videos; SkiaSharp remeasurement of the named pairs)

| Group | Max frame difference |
| --- | --- |
| Still-image uploads (IndoctriNation x5, Surviving the Black Church x1) | 0.07 - 3.10 |
| Real videos, 📱 uploads (Afro Atheist) | 6.93 - 70.6 |
| Real videos, other (lowest 10.41, median ~25) | 10.41 - 98.9 |

JPEG noise puts still images slightly above zero, hence the default threshold of 5.0.

The same metric was remeasured with SkiaSharp, fetching only `1.jpg` / `2.jpg` / `3.jpg`. At two decimal places
the named pairs sit on the original band, so the threshold stays 5.0 (above the highest remeasured still, 3.10,
and below the lowest remeasured video, 6.93).

| Upload | Id | Max frame difference | Class |
| --- | --- | --- | --- |
| IndoctriNation video | `4AcC36Aw2nY` | 32.54 | video |
| IndoctriNation still | `S03n89L5YLA` | 3.10 | still |
| Second pair, video | `2trVqjllYjo` | 28.20 | video |
| Second pair, still | `UXwFIqL21Mk` | 2.30 | still |
| 📱 video (lowest remeasured video) | `HIuO5jaDWFs` | 6.93 | video |
| Plain video | `ExbrJpfl4Cg` | 21.65 | video |

## Options (`youTubeCandidatePreference`)

| Option | Default |
| --- | --- |
| `Enabled` | `true` (set `false` to roll back to the finders' pick) |
| `PenalisedTitleTokens` | `["📱"]` |
| `PenalisedTitlePenalty` | `10` |
| `StaticImagePenalty` | `10` |
| `StaticImageThreshold` | `5.0` |
| `PlaceholderMaxPixelStdDev` | `1.0` |
| `ThumbnailFetchTimeoutSeconds` | `5` |
| `MaxThumbnailBytes` | `262144` (256 KiB) |
| `MaxThumbnailRedirects` | `3` |
| `MaxContenders` | `3` |
| `ContenderMinTitleSimilarity` | `85` |
| `ContenderMaxDurationDifferenceSeconds` | `120` |

## Logging

Category `RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference` is at Information in every function
app (`ApplicationLogLevels`, each `host.json`). Each multi-candidate choice logs the scores, and replacing the finder's pick logs
"YouTube candidate preference replaced ...". A replacement therefore logs two Information lines (scores, then replacement); this is intentional.
Per-video motion results are at Debug.

```kusto
traces
| where message startswith "YouTube candidate preference"
| project timestamp, cloud_RoleName, message
| order by timestamp desc
```