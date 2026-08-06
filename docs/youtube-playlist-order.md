# YouTube playlist order (depth)

**Start here for cold-start across both platforms:** [catalogue-pagination.md](catalogue-pagination.md).

This page keeps YouTube-specific depth: why curated playlists break the binary probe,
what `Arbitrary` does, the known-ID position probe sketch, and equal-timestamp failure modes.

---

## Problem

Indexing and enrichment historically assumed a YouTube playlist is either **newest-first**
(early-stop from the head) or **oldest-first** (expensive full walk). The order probe
(`PlaylistItemOrdering.IsReverseDateOrdered`) samples the head each windowed pass and flips
`Podcast.YouTubePlaylistQueryIsExpensive` accordingly.

Manually curated playlists break both assumptions:

- New videos are sometimes added at the head and sometimes appended at the end
  (observed on curated show playlists where a new episode sat near the end behind a
  bulk-added backlog).
- Bulk-added items share identical `snippet.publishedAt` (added-at) timestamps. Equal
  timestamps satisfy `IsReverseDateOrdered` (non-ascending), so the probe misclassifies the
  playlist as newest-first, early-stops on the stale head, finds nothing, and can flip the
  expensive flag to `false` — hiding appended episodes.

---

## Key API facts

| Fact | Consequence |
|------|-------------|
| `playlistItems.list` costs **1 quota unit per page of 50** | A walk of ~350 items is ~8 units/pass — cheaper than one `search.list` (100 units) |
| No reverse/tail pagination — `nextPageToken` only walks forward | "Check both ends cheaply" is not possible positionally |
| `playlistItems.list` accepts `playlistId` + `videoId` (~1 unit) and returns `snippet.position` | A known video's playlist position can be looked up without walking |
| For playlist items, `snippet.publishedAt` = **added-to-playlist time** | On curated playlists, added-at is the "new to this feed" signal regardless of position |
| A **scheduled** upload joins the playlist days before it goes public; `contentDetails.videoPublishedAt` carries the real publication | Windowing on added-at alone hides scheduled uploads. Window on `PlaylistItem.GetIndexingWindowDate()` — the later of added-at and video-published-at |

---

## `youTubePlaylistOrder` (implemented)

Nullable enum on `Podcast` (`PlaylistOrder`, JSON `youTubePlaylistOrder`):

| Value | Meaning |
|-------|---------|
| `null` (absent) | Probe head order each windowed pass; maintain `youTubePlaylistQueryIsExpensive` |
| `Arbitrary` | Curated playlist; position carries no date information |
| `ReverseChronological` / `Ascending` | Reserved for future probe-written classification; not yet consumed |

Former playlist ids are retained on `youTubePlaylistIdHistory` (`id` + `replacedAt` UTC, newest last)
whenever `YouTubePlaylistIdChange.Apply` swaps a non-empty configured id — so a bad curated swap
is recoverable from the podcast document (and surfaced on the API podcast DTO).

When `Arbitrary`:

- Walk at batch size 50 with hard page cap `ArbitraryYouTubePlaylistWalk.MaxPages` (= 20 →
  ~1000 items). Prefer an **Error** log + stop over burning quota on a mis-tagged
  channel-scale playlist.
- Head-order probe skipped; `IsExpensiveQuery` stays `null`; discovery never Applys the
  expensive flag.
- `ReleasedSince` filter still applies after the walk, on the indexing-window date
  (later of added-at and `contentDetails.videoPublishedAt`).
- `SkipExpensiveYouTubeQueries` does not degrade to a single page.

Full decision tables, discovery vs enrichment, and hourly gates:
[catalogue-pagination.md](catalogue-pagination.md) §4–5.

---

## Known-ID position probe (sketch — not yet implemented)

Goal: classify playlist order cheaply using episodes we already know.

1. Take the 2–3 most recent stored episodes that carry a `youTubeId`.
2. For each, `playlistItems.list(part=snippet, playlistId, videoId)` (~1 unit) →
   `snippet.position` + added-at.
3. Compare episode release order vs playlist position:
   - newer → lower position consistently → `ReverseChronological`
   - newer → higher position consistently → `Ascending`
   - mixed / outliers → `Arbitrary`
4. Suggest (or `--apply`) `youTubePlaylistOrder` on the podcast.

Total cost ~2–3 units. Dry-run by default; never write episodes. Does **not** find brand-new
videos — only classifies where new items tend to land.

Non-goals: hourly both-ends paging; undocumented page-token arithmetic to jump to the tail.

---

## Business-rule tests

Authoritative matrix: [catalogue-pagination.md](catalogue-pagination.md) §4
("Business-rule tests (YouTube)").

Critical YouTube-only cases:

| Rule | Test |
|------|------|
| Equal added-at timestamps count as reverse-chrono (curated failure mode) | `PlaylistItemOrderingRules` |
| Arbitrary circuit breaker trips at MaxPages with next token remaining | `ArbitraryYouTubePlaylistWalkRules` |
| Arbitrary leaves expensive flag untouched even if a probe value sneaks through | `YouTubeEpisodeRetrievalHandlerRules` |
| Scheduled upload added before the window but published inside it is retained and dated by publication | `ScheduledUploadIndexingWindowRules`, `YouTubeEpisodeProviderScheduledUploadRules` |
