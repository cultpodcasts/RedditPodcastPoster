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
   Token-set similarity scores a title whose words are a subset of another's (e.g. the channel trailer
   "IndoctriNation Podcast") highly; the duration and strong-match checks are what stop such an upload replacing
   the pick. When the finder's pick has no video details, the duration band against the pick is skipped and only
   the episode-duration check applies.
4. The pick and validated contenders are scored: `-PenalisedTitlePenalty` for a title containing any
   `PenalisedTitleTokens` (default 📱), `-StaticImagePenalty` for a still image. Highest score wins; ties keep the pick.

Still images are detected by `YouTubeThumbnailMotionDetector` from the three 120x90 auto thumbnails
`https://i.ytimg.com/vi/<id>/1.jpg`, `2.jpg`, `3.jpg`. **Video is never downloaded.** The largest mean absolute RGB
difference between the frames (every 2nd pixel) below `StaticImageThreshold` means static. A missing thumbnail,
timeout (`ThumbnailFetchTimeoutSeconds`), decode failure, size mismatch or blank/placeholder frame
(pixel std-dev below `PlaceholderMaxPixelStdDev`) is `Unknown`, which gets no penalty (fail-open).

## Calibration (October 2026, 69 videos)

| Group | Max frame difference |
| --- | --- |
| Still-image uploads (IndoctriNation x5, Surviving the Black Church x1) | 0.07 - 3.10 |
| Real videos, 📱 uploads (Afro Atheist) | 6.93 - 70.6 |
| Real videos, other (lowest 10.41, median ~25) | 10.41 - 98.9 |

JPEG noise puts still images slightly above zero, hence the default threshold of 5.0.

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