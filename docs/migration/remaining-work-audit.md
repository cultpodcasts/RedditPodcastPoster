# Remaining Migration Work - Audit

## 🔍 Current Audit Focus

Primary focus has shifted from introducing parallel service variants to **decommissioning legacy runtime usage** outside:
- `PodcastRepository`
- `LegacyPodcastToV2Migration`

---

## ✅ What is complete

- Detached episode persistence model is active.
- `PodcastUpdater` is the active default updater over detached episodes.
- **Full detached episode migration completed** (commit 62b53e1).
- Social + shortener boundaries have been migrated to `PodcastEpisode` contracts.
- All episode persistence points reviewed and fixed/verified.
- Build is green after latest fixes.

---

## ✅ **LATEST FIX CYCLE COMPLETED**

### Critical Issues Fixed
1. **PodcastUpdater missing episode persistence** ✅ FIXED
   - Added saves for enriched episodes after `EnrichEpisodes()` call
   - Added saves for filtered episodes (marked as removed)
   - Added saves for merged and newly added episodes

2. **PodcastProcessorV2 incomplete field mapping** ✅ FIXED
   - Expanded from 4 hardcoded fields to complete property copy
   - Now preserves: URLs, Description, Release, Images, Subjects, SearchTerms

3. **EnrichPodcastEpisodesProcessor conversion patterns** ✅ VERIFIED
   - Already uses clean detached episode pattern
   - Works directly with V2 episodes, no round-trip conversions needed

---

## ✅ Recently completed decommission targets

### Target Group B: Legacy conversion helpers
- Removed unused conversion helpers now that callers are gone:
  - `ToLegacyPodcast`
  - `ToLegacyEpisode`
  - `PodcastEpisode.ToLegacy()`

### Target Group C: Temporary compatibility overloads
- Removed temporary legacy overloads from:
  - `FindSpotifyEpisodeRequestFactory`
  - `FindAppleEpisodeRequestFactory`

### Target Group D: Duplicate service variants
- Retired duplicate provider/poster variants and standardized on canonical detached-episode contracts:
  - `IPodcastEpisodeProvider` / `PodcastEpisodeProvider`
  - `IPodcastEpisodePoster` / `PodcastEpisodePoster`

### Target Group E: Legacy episode-pair usage outside allowed boundaries
- Migrated additional runtime paths to detached contracts (`PodcastEpisode` + detached podcast/episode models):
  - `IEpisodeResolver` / `EpisodeResolver`
  - `EpisodeSearchIndexerService` and its mapping extension
  - `PodcastEpisodesExtension` (legacy extension block removed)
  - `SpotifyPodcastEnricher` (legacy episode-pair construction removed)
- Removed legacy embedded-model `RedditPodcastPoster.Models/PodcastEpisode.cs` from active codepaths.
- Promoted detached model pair to canonical `PodcastEpisode` naming.
- Build is green after these migrations.

---

## ⚠️ Still pending (non-decommission)

- Broader test coverage for detached-episode services.
- Reduced-key `CompactSearchRecord` rollout and UI compatibility checks.
- Final verification gates (RU/latency, end-to-end parity, runtime cleanup sweep).
