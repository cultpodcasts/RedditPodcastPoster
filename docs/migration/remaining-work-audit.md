# Remaining Migration Work - Audit

## 🔍 Current Audit Focus

Primary focus has shifted from introducing parallel service variants to **decommissioning legacy runtime usage** outside:
- `PodcastRepository`
- `LegacyPodcastToV2Migration`

---

## ✅ What is complete

- Detached episode persistence model is active.
- `PodcastUpdater` is the active default updater over detached episodes.
- **Full V2 detached episode migration completed** (commit 62b53e1).
- Social + shortener boundaries have been migrated to `PodcastEpisodeV2` contracts.
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

## 🔄 Remaining decommission targets

### Target Group B: Legacy conversion helpers
1. Remove unused conversion helpers once callers are gone:
   - `ToLegacyPodcast`
   - `ToLegacyEpisode`
   - `PodcastEpisodeV2.ToLegacy()`

### Target Group C: Temporary compatibility overloads
2. Remove temporary legacy overloads from:
   - `FindSpotifyEpisodeRequestFactory`
   - `FindAppleEpisodeRequestFactory`

### Target Group D: Duplicate service variants
3. Retire legacy provider/poster variants once all consumers use detached contracts:
   - `IPodcastEpisodeProvider` / `PodcastEpisodeProvider`
   - `IPodcastEpisodePoster` / `PodcastEpisodePoster`

---

## ⚠️ Still pending (non-decommission)

- Broader test coverage for detached-episode services.
- Reduced-key `CompactSearchRecord` rollout and UI compatibility checks.
- Final verification gates (RU/latency, end-to-end parity, runtime cleanup sweep).
