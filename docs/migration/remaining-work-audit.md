# Remaining Migration Work - Audit

## 🔍 Current Audit Focus

Primary focus has shifted from introducing parallel service variants to **decommissioning legacy runtime usage** outside:
- `PodcastRepository`
- `LegacyPodcastToV2Migration`

---

## ✅ What is complete

- Detached episode persistence model is active.
- `PodcastUpdater` is the active default updater over detached episodes.
- Build is green after latest migration pass.
- Major console/API runtime paths now use `IPodcastRepositoryV2` + `IEpisodeRepository`.

---

## 🔄 Decommission targets (must be in active plan)

### Target Group A: Social + shortener boundaries
1. Migrate `ITweetPoster` to accept `PodcastEpisodeV2`.
2. Migrate `IBlueskyPoster` to accept `PodcastEpisodeV2`.
3. Migrate `IShortnerService` to accept `PodcastEpisodeV2`.
4. Remove call-site `.ToLegacy()` conversions in:
   - `PostProcessor`
   - `Tweeter`
   - `BlueskyPostManager`
   - `EpisodeHandler`

### Target Group B: Legacy conversion helpers
5. Remove unused conversion helpers once callers are gone:
   - `ToLegacyPodcast`
   - `ToLegacyEpisode`
   - `PodcastEpisodeV2.ToLegacy()`

### Target Group C: Temporary compatibility overloads
6. Remove temporary legacy overloads from:
   - `FindSpotifyEpisodeRequestFactory`
   - `FindAppleEpisodeRequestFactory`

### Target Group D: Duplicate service variants
7. Retire legacy provider/poster variants once all consumers use V2 contracts:
   - `IPodcastEpisodeProvider` / `PodcastEpisodeProvider`
   - `IPodcastEpisodePoster` / `PodcastEpisodePoster`

---

## ⚠️ Still pending (non-decommission)

- Broader test coverage for detached-episode services.
- Reduced-key `CompactSearchRecord` rollout and UI compatibility checks.
- Final verification gates (RU/latency, end-to-end parity, runtime cleanup sweep).
