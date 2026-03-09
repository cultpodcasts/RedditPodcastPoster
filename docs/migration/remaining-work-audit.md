# Remaining Migration Work - Audit

## 🔍 Current Audit Focus

Primary focus is decommissioning remaining legacy runtime usage outside:
- `PodcastRepository`
- `LegacyPodcastToV2Migration`

---

## ✅ What is complete

- Detached episode persistence model is active.
- `PodcastUpdater` is the active default updater over detached episodes.
- Build is green after latest migration pass.
- Major console/API runtime paths now use `IPodcastRepositoryV2` + `IEpisodeRepository`.
- **Decommission Target Group A complete:**
  - `ITweetPoster` migrated to `PodcastEpisodeV2`.
  - `IBlueskyPoster` migrated to `PodcastEpisodeV2`.
  - `IShortnerService` migrated to `PodcastEpisodeV2`.
  - `.ToLegacy()` social/shortener boundaries removed from primary call sites.

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
