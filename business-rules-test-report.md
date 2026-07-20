# Business rules test report

| | |
|---|---|
| Branch | `feature/episode-domain-phase-f-cleanup` |
| Commit | `9603b404` |
| Generated | 2026-07-08 13:17:02 +01:00 |
| Filter | `FullyQualifiedName~Rules` |

## RedditPodcastPoster.Episodes.Tests

**Summary:** 235 total — 235 passed, 0 failed, 0 skipped

### Scenarios

#### CatalogueMatchingRules
- [✓] Exact title match for IsCatalogueMatch is case-sensitive — differing case falls through to fuzzy heuristics and does not auto-accept on title alone.(alterCaseOnCatalogueTitle: False)
- [✓] Exact title match for IsCatalogueMatch is case-sensitive — differing case falls through to fuzzy heuristics and does not auto-accept on title alone.(alterCaseOnCatalogueTitle: True)
- [✓] For YouTube release authority catalogue lookup, FindCatalogueMatchByLength may match on broader duration tolerance when titles differ by typo.
- [✓] For YouTube release authority podcasts with negative publishing delay, IsCatalogueMatch accepts an aligned Spotify catalogue item for a YouTube-only stored episode.
- [✓] For YouTube release authority podcasts with negative publishing delay, IsCatalogueMatch does not treat clearly different titles as the same catalogue item.
- [✓] For YouTube release authority podcasts with positive publishing delay, IsCatalogueMatch accepts a YouTube catalogue item whose publish aligns after delay adjustment.
- [✓] For YouTube release authority podcasts with positive publishing delay, IsCatalogueMatch rejects a YouTube catalogue item whose publish exceeds the delay-alignment threshold when titles do not exactly match.
- [✓] When a catalogue item's release falls outside the Spotify catalogue tolerance window, CatalogueReleaseMatches rejects it for platform lookup filtering.
- [✓] When a catalogue lookup reducer excludes assigned platform IDs, FindCatalogueMatchByLength does not return excluded candidates.
- [✓] When a catalogue row has no release date, FindCatalogueMatchByDate may still include it in the same-date candidate set.
- [✓] When a date-based catalogue lookup reducer excludes assigned platform IDs, FindCatalogueMatchByDate does not return excluded candidates.
- [✓] When an episode match regex is configured, exact full-title equality does not bypass release and duration checks if the regex groups do not align.
- [✓] When catalogue titles differ but only one candidate shares the probe duration, the unique-duration path accepts the match without a title match.
- [✓] When enriching a YouTube-discovered episode and duration does not match any catalogue row, FindCatalogueMatchByLength may still select the sole row whose release is within twelve hours.
- [✓] When enriching a YouTube-discovered episode and multiple catalogue rows align on release within twelve hours but not on duration, FindCatalogueMatchByLength picks the closest release.
- [✓] When enriching a YouTube-discovered episode and multiple catalogue rows share its duration, FindCatalogueMatchByLength selects the row whose release is closest to the probe.
- [✓] When exactly one catalogue row shares the probe duration within the standard threshold and titles fuzzy-match at the catalogue threshold, FindCatalogueMatchByLength selects it.
- [✓] When filtering catalogue candidates for platform lookup, CatalogueReleaseMatches delegates to Spotify catalogue release tolerance.
- [✓] When multiple catalogue rows share duration within the standard threshold, FindCatalogueMatchByLength fuzzy-disambiguates by preferring the closest title match.
- [✓] When multiple catalogue rows share the probe calendar date, FindCatalogueMatchByDate fuzzy-disambiguates by preferring the closest title match.
- [✓] When probe and catalogue item share an exact title via IsMatch, merge accepts the pair even when release and duration clearly differ.
- [✓] When probe and catalogue item share an exact title, IsCatalogueMatch accepts the match even when release and duration clearly differ.
- [✓] When probe and catalogue item titles overlap by substring, the longest overlapping title wins among multiple substring matches.
- [✓] When probe and catalogue releases differ by one calendar day and titles differ by typo, FindCatalogueMatchByDate may still select the catalogue item.
- [✓] When probe release aligns with catalogue release within the date-only window, FindCatalogueMatchByDate selects the matching catalogue item.
- [✓] When the probe episode has no release date, CatalogueReleaseMatches rejects the catalogue item for platform lookup filtering.
- [✓] When the probe episode has no release date, FindCatalogueMatchByDate returns no catalogue match.
- [✓] When the probe episode has zero duration and no substring title overlap, FindCatalogueMatchByLength returns no catalogue match.
- [✓] When the probe title contains HTML entities and the catalogue title is decoded, FindCatalogueMatchByLength treats them as the same substring title match.

#### CrossPlatformMatchingRules
- [✓] For YouTube release authority podcasts with negative publishing delay, episodes must not merge on release-and-duration alone when titles clearly refer to different episodes.
- [✓] For YouTube release authority podcasts with positive publishing delay, an incoming YouTube episode may match a stored audio episode when release aligns after delay adjustment.
- [✓] For YouTube release authority podcasts, a Spotify catalogue episode may match a YouTube-only stored episode when title and duration fuzzy-match and catalogue release aligns after publishing-delay adjustment.
- [✓] When two stored episodes could both match an incoming episode, indexing must record merge failure — not pick arbitrarily.

#### EpisodeFromCandidateFactoryRules
- [✓] When a candidate has no source link, materialization copies title, description, duration, and release only because platform fields come exclusively from the link.
- [✓] When a catalogue candidate carries a platform link id, Spotify and YouTube assign the string id as-is while Apple sets AppleId only when the id parses as a long, because platform episode identity types differ.(shape: AppleParseableNumericId)
- [✓] When a catalogue candidate carries a platform link id, Spotify and YouTube assign the string id as-is while Apple sets AppleId only when the id parses as a long, because platform episode identity types differ.(shape: AppleUnparseableStringId)
- [✓] When a catalogue candidate carries a platform link id, Spotify and YouTube assign the string id as-is while Apple sets AppleId only when the id parses as a long, because platform episode identity types differ.(shape: SpotifyArbitraryStringId)
- [✓] When a catalogue candidate carries a platform link id, Spotify and YouTube assign the string id as-is while Apple sets AppleId only when the id parses as a long, because platform episode identity types differ.(shape: SpotifyFixtureStringId)
- [✓] When a catalogue candidate carries a platform link id, Spotify and YouTube assign the string id as-is while Apple sets AppleId only when the id parses as a long, because platform episode identity types differ.(shape: YouTubeArbitraryStringId)
- [✓] When a catalogue candidate carries a platform link id, Spotify and YouTube assign the string id as-is while Apple sets AppleId only when the id parses as a long, because platform episode identity types differ.(shape: YouTubeFixtureStringId)
- [✓] When a catalogue candidate has no artwork, materialization leaves episode images unset because legacy factories only set images when artwork is present.(platform: Apple)
- [✓] When a catalogue candidate has no artwork, materialization leaves episode images unset because legacy factories only set images when artwork is present.(platform: Spotify)
- [✓] When a catalogue candidate has no artwork, materialization leaves episode images unset because legacy factories only set images when artwork is present.(platform: YouTube)
- [✓] When a catalogue candidate includes artwork, materialization sets the matching platform image because indexed episodes carry per-platform artwork.(platform: Apple)
- [✓] When a catalogue candidate includes artwork, materialization sets the matching platform image because indexed episodes carry per-platform artwork.(platform: Spotify)
- [✓] When a catalogue candidate includes artwork, materialization sets the matching platform image because indexed episodes carry per-platform artwork.(platform: YouTube)
- [✓] When a Spotify catalogue candidate is materialized, the episode matches the legacy FromSpotify shape because provider boundaries must not change indexed episode fields.
- [✓] When a YouTube catalogue candidate is materialized, the episode matches the legacy FromYouTube shape because provider boundaries must not change indexed episode fields.
- [✓] When an Apple catalogue candidate is materialized, the episode matches the legacy FromApple shape because provider boundaries must not change indexed episode fields.
- [✓] When explicit content is false on materialization, the episode is not marked explicit because the flag is supplied separately from candidate core fields.(platform: Apple)
- [✓] When explicit content is false on materialization, the episode is not marked explicit because the flag is supplied separately from candidate core fields.(platform: Spotify)
- [✓] When explicit content is false on materialization, the episode is not marked explicit because the flag is supplied separately from candidate core fields.(platform: YouTube)
- [✓] When explicit content is set on materialization, the episode carries the explicit flag because catalogue APIs expose explicit separately from candidate core fields.

#### EpisodeIdentityExtensionsRules
- [✓] HasAppleIdentity is true when a positive Apple id or Apple URL is present, otherwise false.(scenario: "id_present")
- [✓] HasAppleIdentity is true when a positive Apple id or Apple URL is present, otherwise false.(scenario: "neither")
- [✓] HasAppleIdentity is true when a positive Apple id or Apple URL is present, otherwise false.(scenario: "url_only")
- [✓] HasSpotifyIdentity is true when a Spotify id or Spotify URL is present, otherwise false.(scenario: "id_present")
- [✓] HasSpotifyIdentity is true when a Spotify id or Spotify URL is present, otherwise false.(scenario: "neither")
- [✓] HasSpotifyIdentity is true when a Spotify id or Spotify URL is present, otherwise false.(scenario: "url_only")
- [✓] IncomingPlatformIdOwnedByAnotherEpisode is false when only the candidate itself carries the same platform id as the incoming episode.
- [✓] IncomingPlatformIdOwnedByAnotherEpisode is true when another stored episode already owns the incoming Apple episode id.
- [✓] IncomingPlatformIdOwnedByAnotherEpisode is true when another stored episode already owns the incoming Spotify episode id.
- [✓] ResolveSpotifyEpisodeId extracts the episode id from a Spotify episode URL when no explicit Spotify id is stored.
- [✓] ResolveSpotifyEpisodeId returns null when both Spotify id and URL are absent.
- [✓] ResolveSpotifyEpisodeId returns the explicit Spotify id when present, preferring it over extracting from the Spotify URL.
- [✓] SpotifyEpisodesMatch is true when both episodes resolve to the same Spotify episode id, including URL-only identity on one side.

#### EpisodeMappingExtensionsRules
- [✓] Platform-specific To*Patch helpers carry only the requested platform link.(service: Apple)
- [✓] Platform-specific To*Patch helpers carry only the requested platform link.(service: Spotify)
- [✓] Platform-specific To*Patch helpers carry only the requested platform link.(service: YouTube)
- [✓] ToCandidate maps each platform service to a SourceLink with that platform's id and URL.(service: Apple)
- [✓] ToCandidate maps each platform service to a SourceLink with that platform's id and URL.(service: Spotify)
- [✓] ToCandidate maps each platform service to a SourceLink with that platform's id and URL.(service: YouTube)
- [✓] ToCandidate with an unsupported source service returns a candidate with a null source link.
- [✓] When a stored episode has no platform identity for the requested service, ToCandidate returns a candidate with a null source link.
- [✓] When building a generic platform patch from a stored episode, ToPlatformPatch carries description and release but no platform link.
- [✓] When building a platform patch from a stored episode, ToSpotifyPatch carries Spotify link without mutating other platform fields.
- [✓] When building Apple and YouTube platform patches from a stored episode, each patch carries only that platform's link and release.
- [✓] When mapping a stored episode with Apple identity to a candidate, the candidate carries Apple link and numeric platform id.
- [✓] When mapping a stored episode with Spotify identity to a candidate, the candidate carries Spotify link, duration, and date-only release semantics.
- [✓] When mapping a stored YouTube-only episode to a candidate, the candidate preserves YouTube link and full datetime release.

#### EpisodeMergingRules
- [✓] A discovered episode with no match is added as a new row with a new ID.
- [✓] Merge does not replace a complete description with a shorter one.
- [✓] Merge fills missing Apple URLs; it does not replace an existing Apple URL.
- [✓] Merge fills missing Apple URLs; it does not replace an existing Apple URL.
- [✓] Merge fills missing artwork per platform; it does not replace existing artwork.
- [✓] Merge fills missing artwork per platform.
- [✓] Merge fills missing platform IDs; it does not overwrite an existing ID with a different one.
- [✓] Merge fills missing Spotify IDs when the stored episode has none.
- [✓] Merge fills missing Spotify URLs; it does not replace an existing Spotify URL.
- [✓] Merge fills missing YouTube URLs; it does not replace an existing YouTube URL.
- [✓] Merge fills missing YouTube URLs; it does not replace an existing YouTube URL.
- [✓] Merge inherits podcast default language onto newly added episodes when episode language is unset.
- [✓] Merge may replace a truncated description (ending in ...) with a longer description; it does not replace a complete description with a shorter one.
- [✓] Spotify catalogue release is date-only: re-indexing must not overwrite a stored catalogue release with a newer public availability date.

#### EpisodePlatformApplierRules
- [✓] ApplyFillMissingRelease leaves the stored release unchanged when values are equal.
- [✓] ApplyFillMissingRelease updates the stored release when the incoming release differs.
- [✓] When a stored description ends with ellipsis and the patch supplies a longer description, the applier replaces the truncated description.
- [✓] When a stored description is complete, the applier does not replace it with patch description.
- [✓] When only the platform URL is missing and id/image are already set, the applier fills only the URL.(service: Apple)
- [✓] When only the platform URL is missing and id/image are already set, the applier fills only the URL.(service: Spotify)
- [✓] When only the platform URL is missing and id/image are already set, the applier fills only the URL.(service: YouTube)
- [✓] When platform ID, URL, and image are already set, the applier leaves them unchanged because fill-missing must not replace existing platform links.(service: Apple)
- [✓] When platform ID, URL, and image are already set, the applier leaves them unchanged because fill-missing must not replace existing platform links.(service: Spotify)
- [✓] When platform ID, URL, and image are already set, the applier leaves them unchanged because fill-missing must not replace existing platform links.(service: YouTube)
- [✓] When platform ID, URL, and image are missing on a stored episode, the applier fills them because enrichment must backfill absent platform links.(service: Apple)
- [✓] When platform ID, URL, and image are missing on a stored episode, the applier fills them because enrichment must backfill absent platform links.(service: Spotify)
- [✓] When platform ID, URL, and image are missing on a stored episode, the applier fills them because enrichment must backfill absent platform links.(service: YouTube)

#### EpisodeReleaseToleranceRules
- [✓] GetAudioReleaseForPlatformLookup adjusts release by publishing delay only when authority or episode/platform configuration requires it.(scenario: "youtube_authority_without_episode_identity")
- [✓] GetAudioReleaseForPlatformLookup adjusts release by publishing delay only when authority or episode/platform configuration requires it.(scenario: "youtube_authority")
- [✓] GetAudioReleaseForPlatformLookup adjusts release by publishing delay only when authority or episode/platform configuration requires it.(scenario: "youtube_discovered_on_spotify_primary")
- [✓] GetAudioReleaseForPlatformLookup adjusts release by publishing delay only when authority or episode/platform configuration requires it.(scenario: "youtube_identity_without_audio_platform")
- [✓] GetAudioReleaseForPlatformLookup adjusts release by publishing delay only when authority or episode/platform configuration requires it.(scenario: "zero_delay")
- [✓] GetAudioReleaseForPlatformLookup subtracts publishing delay when a merged episode has both YouTube and Spotify identities on a YouTube release authority podcast.
- [✓] GetToleranceTicks for positive-delay YouTube authority omits episode length from tolerance when the stored episode length is zero.
- [✓] GetToleranceTicks returns expected thresholds for delay and release-authority scenarios.(scenario: "negative_delay")
- [✓] GetToleranceTicks returns expected thresholds for delay and release-authority scenarios.(scenario: "positive_delay_spotify_authority")
- [✓] GetToleranceTicks returns expected thresholds for delay and release-authority scenarios.(scenario: "positive_delay_youtube_authority")
- [✓] GetToleranceTicks returns expected thresholds for delay and release-authority scenarios.(scenario: "zero_delay")
- [✓] GetToleranceTicks without a podcast uses delay and release-authority parameters with the same thresholds as the podcast overload.(scenario: "negative_delay")
- [✓] GetToleranceTicks without a podcast uses delay and release-authority parameters with the same thresholds as the podcast overload.(scenario: "positive_delay_spotify_authority")
- [✓] GetToleranceTicks without a podcast uses delay and release-authority parameters with the same thresholds as the podcast overload.(scenario: "positive_delay_youtube_authority")
- [✓] GetToleranceTicks without a podcast uses delay and release-authority parameters with the same thresholds as the podcast overload.(scenario: "zero_delay")
- [✓] ShouldEnrichDespiteReleaseWindow returns false when delay, authority, platform IDs, or the current time falls outside the enrichment window.(scenario: "episode_already_has_spotify_id")
- [✓] ShouldEnrichDespiteReleaseWindow returns false when delay, authority, platform IDs, or the current time falls outside the enrichment window.(scenario: "outside_release_window")
- [✓] ShouldEnrichDespiteReleaseWindow returns false when delay, authority, platform IDs, or the current time falls outside the enrichment window.(scenario: "positive_delay")
- [✓] ShouldEnrichDespiteReleaseWindow returns false when delay, authority, platform IDs, or the current time falls outside the enrichment window.(scenario: "spotify_authority")
- [✓] ShouldEnrichDespiteReleaseWindow returns true for a YouTube-only episode near expected audio release when Spotify or Apple catalogue IDs are still missing.
- [✓] ShouldEnrichDespiteReleaseWindow returns true when only Apple catalogue identity is still missing for a YouTube-only episode inside the enrichment window.
- [✓] ShouldPreserveYouTubeAuthoritativeRelease returns false when the episode has no YouTube identity.
- [✓] ShouldPreserveYouTubeAuthoritativeRelease returns true when YouTube is release authority and the episode has YouTube identity.
- [✓] SpotifyCatalogueReleaseMatches accepts a midnight Spotify catalogue date when the expected release is the same calendar day with a non-midnight time.
- [✓] SpotifyCatalogueReleaseMatches accepts aligned Spotify catalogue dates for YouTube release authority negative-delay podcasts.
- [✓] SpotifyCatalogueReleaseMatches accepts Apple catalogue releases within tolerance for YouTube release authority negative-delay podcasts.(appleCatalogueDaysAfterSpotifyDate: 0)
- [✓] SpotifyCatalogueReleaseMatches accepts Apple catalogue releases within tolerance for YouTube release authority negative-delay podcasts.(appleCatalogueDaysAfterSpotifyDate: 4)
- [✓] SpotifyCatalogueReleaseMatches rejects far-off Spotify catalogue dates.
- [✓] When the podcast is not YouTube release authority, Spotify catalogue day tolerance is one day and SpotifyCatalogueReleaseMatches uses that window.
- [✓] When YouTube is release authority with negative delay, Spotify catalogue day tolerance is five days and SpotifyCatalogueReleaseMatches accepts releases within that window.
- [✓] When YouTube is release authority with negative delay, Spotify catalogue release may land several days early and still match within the five-day day tolerance.

#### ExactReleaseMatchStrategyRules
- [✓] Exact release strategy returns true, false, or null according to delay sign and whether the release delta falls within tolerance.(scenario: "negative_delay_defers")
- [✓] Exact release strategy returns true, false, or null according to delay sign and whether the release delta falls within tolerance.(scenario: "positive_delay_outside_defers")
- [✓] Exact release strategy returns true, false, or null according to delay sign and whether the release delta falls within tolerance.(scenario: "positive_delay_within")
- [✓] Exact release strategy returns true, false, or null according to delay sign and whether the release delta falls within tolerance.(scenario: "zero_delay_outside")
- [✓] Exact release strategy returns true, false, or null according to delay sign and whether the release delta falls within tolerance.(scenario: "zero_delay_within")
- [✓] Exact release strategy uses the stored episode length as reference when it is greater than zero.
- [✓] When the podcast has negative YouTube publishing delay, exact release strategy defers to cross-platform strategies by returning null.
- [✓] When the podcast has positive delay but releases differ beyond tolerance, exact release strategy defers by returning null.
- [✓] When the podcast has positive YouTube publishing delay and releases align within tolerance, exact release strategy returns true.
- [✓] When the podcast has zero YouTube publishing delay and releases are identical, exact release strategy returns true within the fourteen-day consideration threshold.
- [✓] When the podcast has zero YouTube publishing delay and releases differ beyond tolerance, exact release strategy returns false.

#### FuzzyTitleMatchingRules
- [✓] For a standard podcast with a long title and matching duration, each fuzzy variant strategy (replace/drop/add-filler/swap) drives a merge.(strategy: AddFillerWord)
- [✓] For a standard podcast with a long title and matching duration, each fuzzy variant strategy (replace/drop/add-filler/swap) drives a merge.(strategy: DropWord)
- [✓] For a standard podcast with a long title and matching duration, each fuzzy variant strategy (replace/drop/add-filler/swap) drives a merge.(strategy: ReplaceWord)
- [✓] For a standard podcast with a long title and matching duration, each fuzzy variant strategy (replace/drop/add-filler/swap) drives a merge.(strategy: SwapAdjacentWords)
- [✓] For a standard podcast with a short title and matching duration, each fuzzy variant strategy (replace/drop/add-filler/swap) drives a merge.(strategy: AddFillerWord)
- [✓] For a standard podcast with a short title and matching duration, each fuzzy variant strategy (replace/drop/add-filler/swap) drives a merge.(strategy: DropWord)
- [✓] For a standard podcast with a short title and matching duration, each fuzzy variant strategy (replace/drop/add-filler/swap) drives a merge.(strategy: ReplaceWord)
- [✓] For a standard podcast with a short title and matching duration, each fuzzy variant strategy (replace/drop/add-filler/swap) drives a merge.(strategy: SwapAdjacentWords)
- [✓] For a standard podcast, when the fuzzy score falls below threshold but release and duration align exactly, episodes still merge via the release-and-duration path.
- [✓] For a YouTube release authority podcast with negative publishing delay, each fuzzy variant strategy drives a cross-platform merge when release and duration also align.(strategy: AddFillerWord)
- [✓] For a YouTube release authority podcast with negative publishing delay, each fuzzy variant strategy drives a cross-platform merge when release and duration also align.(strategy: DropWord)
- [✓] For a YouTube release authority podcast with negative publishing delay, each fuzzy variant strategy drives a cross-platform merge when release and duration also align.(strategy: ReplaceWord)
- [✓] For a YouTube release authority podcast with negative publishing delay, each fuzzy variant strategy drives a cross-platform merge when release and duration also align.(strategy: SwapAdjacentWords)
- [✓] For a YouTube release authority podcast with negative publishing delay, when the fuzzy score falls below threshold, episodes must not merge even if release and duration align — negative-delay requires fuzzy.
- [✓] For YouTube release authority podcasts with negative publishing delay, when the fuzzy title matches and duration differs by exactly the cross-platform tolerance (5 minutes), episodes must not merge.
- [✓] For YouTube release authority podcasts with negative publishing delay, when the fuzzy title matches and duration differs by less than 5 minutes, episodes may be treated as the same.
- [✓] When the fuzzy title matches and duration differs by less than the standard tolerance (59 seconds), episodes may be treated as the same.
- [✓] When the fuzzy title matches but duration differs by exactly the standard tolerance (1 minute), episodes must not merge — the tolerance is strict less-than.
- [✓] When two titles share no meaningful word overlap and releases differ, the fuzzy score falls below the threshold and episodes must not merge — even with an identical duration.

#### PlatformCatalogueAdapterRules
- [✓] PlatformLinkFactory materializes a link when any of id, url, or image is present, and normalizes blank ids to null.(scenario: "all_present")
- [✓] PlatformLinkFactory materializes a link when any of id, url, or image is present, and normalizes blank ids to null.(scenario: "id_only")
- [✓] PlatformLinkFactory materializes a link when any of id, url, or image is present, and normalizes blank ids to null.(scenario: "image_only")
- [✓] PlatformLinkFactory materializes a link when any of id, url, or image is present, and normalizes blank ids to null.(scenario: "url_only")
- [✓] PlatformLinkFactory treats whitespace-only ids as absent and returns null when url and image are also null.
- [✓] When a Spotify catalogue episode is adapted, release is mapped with DateOnly precision because Spotify catalogue dates have no time-of-day.
- [✓] When a YouTube catalogue episode is adapted, publish datetime is mapped as-is because YouTube authority depends on the exact publish time.
- [✓] When an Apple catalogue episode is adapted, release is mapped with DateTimeUtc precision because Apple provides a full publish datetime.
- [✓] When PlatformLinkFactory receives null id, null url, and null image, it returns null so adapters do not materialize empty platform links.

#### PlatformEnrichmentApplicatorRules
- [✓] When a catalogue candidate has no source link, Apply returns None because there is no platform patch to apply.
- [✓] When a stored episode already has a Spotify link, applying a Spotify catalogue candidate does not replace the existing platform ID or URL.
- [✓] When a stored episode has a truncated description ending in ellipsis, applying a catalogue candidate extends the description via the fill-missing applier contract.
- [✓] When a stored episode has midnight release and the incoming candidate has no Apple identity, the applicator does not backfill publish time-of-day because AppleTimeBackfillMergePolicy requires Apple identity.
- [✓] When a stored episode has no description, applying a catalogue candidate fills the description from the candidate payload.
- [✓] When a stored episode is missing a Spotify link, applying a Spotify catalogue candidate fills the platform ID, URL, and image via the applier.
- [✓] When a stored episode is missing a YouTube link, applying a YouTube catalogue candidate fills the platform ID and URL via the applier.
- [✓] When a stored episode is missing an Apple link, applying an Apple catalogue candidate fills the platform ID and URL via the applier.
- [✓] When a stored episode with midnight release is enriched from Apple on the same calendar day, the applicator backfills publish time-of-day via merge policy.
- [✓] When a supplemental description is applied to an episode with no description, ApplyDescription fills the text via the applier contract.
- [✓] When a supplemental YouTube image link is applied but artwork already exists, ApplySupplementalLink does not overwrite the existing image.
- [✓] When a supplemental YouTube image link is applied, ApplySupplementalLink fills missing artwork without replacing an existing platform URL.
- [✓] When a YouTube release authority episode with YouTube identity already has midnight release, the applicator preserves YouTube publish time and does not backfill from Apple.

#### PlatformIdentityMatchingRules
- [✓] When a listener submitted an episode via Spotify URL before Spotify assigned an ID, indexing must merge the catalogue episode onto that stored row — not create a duplicate, even if the Reddit title differs from the Spotify title.
- [✓] When a stored episode has only a Spotify URL, indexing merges an incoming catalogue row whose Spotify ID is extracted from that same URL.
- [✓] When a stored episode has only an Apple URL, indexing merges an incoming catalogue row with the same Apple episode ID when titles align.
- [✓] When an incoming platform ID is already assigned to a different stored episode, indexing must not merge onto the wrong row.
- [✓] When two episodes have different Apple episode IDs, they must never merge — even when titles are identical.
- [✓] When two episodes have different YouTube video IDs, they must never merge — even when titles are identical.
- [✓] When two episodes share the same Apple episode ID, indexing must merge them — even when titles differ.
- [✓] When two episodes share the same YouTube video ID, indexing must merge them — even when titles differ.
- [✓] When two stored episodes already have different Spotify IDs, a new Spotify episode must not be merged by title similarity alone.
- [✓] When two stored episodes already share the same Spotify ID, indexing must treat the catalogue episode as the same row — not create a duplicate.

#### ReleaseDateMergingRules
- [✓] Apple may upgrade a date-only stored release to a full datetime when the calendar date matches.
- [✓] For YouTube-authority podcasts, re-indexing from Spotify must not replace the YouTube publish datetime with a newer Spotify catalogue date.
- [✓] When stored release is midnight UTC and Spotify provides a time on the same calendar date, merge must not backfill the time — Spotify catalogue release is date-only.
- [✓] When stored release is midnight UTC and YouTube provides a time on a different calendar date, merge must not backfill the time.
- [✓] When stored release is midnight UTC and YouTube provides a time on the same calendar date, merge must backfill the time from YouTube.

#### ResolvedItemAdapterRules
- [✓] When a ResolvedAppleItem candidate has a URL but no episode id, materialization sets the Apple URL and leaves AppleId unset because only parseable numeric ids become AppleId.
- [✓] When a ResolvedAppleItem has a URL but no episode id, adaptation produces an Apple PlatformLink with the URL and no id because resolver links may omit episode identity.
- [✓] When a ResolvedAppleItem has an empty description, adaptation preserves the empty description.
- [✓] When a ResolvedAppleItem has both episode id and URL, adaptation carries both on the Apple PlatformLink.
- [✓] When a ResolvedAppleItem is adapted, the candidate SourceLink is an Apple PlatformLink with episode id, URL, and artwork from the resolved item.
- [✓] When a ResolvedSpotifyItem is adapted, the candidate SourceLink is a Spotify PlatformLink with episode id, URL, and artwork from the resolved item.
- [✓] When a ResolvedYouTubeItem is adapted, the candidate SourceLink is a YouTube PlatformLink with video id, URL, and artwork from the resolved item.

#### SpotifyCatalogueReleaseMatchStrategyRules
- [✓] For YouTube release authority with negative publishing delay, a stored YouTube episode does not match when the incoming Spotify catalogue release falls outside tolerance.
- [✓] For YouTube release authority with negative publishing delay, a stored YouTube episode matches an aligned incoming Spotify catalogue item after delay adjustment.
- [✓] For YouTube release authority with positive publishing delay, a stored YouTube episode matches an aligned incoming Spotify catalogue item after delay adjustment.
- [✓] When an incoming Apple episode is paired with a stored YouTube episode, Spotify catalogue release strategy defers because Apple is not a Spotify catalogue lookup.
- [✓] When release authority is not YouTube, Spotify catalogue release strategy defers even for cross-platform YouTube-stored and Spotify-incoming pairs.
- [✓] When stored and incoming episodes share the same platform type, Spotify catalogue release strategy defers by returning null.
- [✓] When stored episode is not YouTube-identified, Spotify catalogue release strategy defers for Spotify-incoming pairs.
- [✓] When stored length is zero, Spotify catalogue release strategy uses incoming length as the tolerance reference for an aligned YouTube-to-Spotify pair.
- [✓] When the podcast has no YouTube publishing delay, Spotify catalogue release strategy defers to other strategies by returning null.

#### TitleDurationMatchingRules
- [✓] Custom EpisodeMatchRegex on the podcast may force a match when titles differ.
- [✓] When EpisodeMatchRegex captures a title group, symbol differences between stored and incoming titles may still match via CompareOptions.IgnoreSymbols.
- [✓] When titles and duration differ but release and duration align within standard tolerance on a podcast without YouTube release authority, episodes may be treated as the same.
- [✓] When titles differ by a typo but duration does not match, episodes are not the same.
- [✓] When titles differ by a typo but duration matches within tolerance, episodes may be treated as the same.
- [✓] When two episodes have different YouTube video IDs, IsMatch returns false even if titles align.

#### YouTubePublishDelayMatchStrategyRules
- [✓] For YouTube release authority podcasts with negative publishing delay, a Spotify catalogue episode does not match when its release falls outside the catalogue tolerance window.
- [✓] For YouTube release authority podcasts with negative publishing delay, a stored YouTube episode matches an incoming Spotify catalogue item when release aligns after delay adjustment.
- [✓] For YouTube release authority podcasts with positive publishing delay, a stored Spotify episode matches an incoming YouTube episode when publish aligns after delay adjustment.
- [✓] For YouTube release authority podcasts with positive publishing delay, an incoming YouTube episode does not match when its release exceeds the delay-alignment threshold.
- [✓] For YouTube release authority podcasts with positive publishing delay, an incoming YouTube episode matches a stored audio episode when its release aligns after delay adjustment.
- [✓] When stored and incoming episodes both carry YouTube identity, YouTube publish-delay strategy returns false because delay alignment applies only across platform types.
- [✓] When the podcast has no YouTube publishing delay, YouTube publish-delay strategy defers to other strategies by returning null.
- [✓] When the stored episode has YouTube identity but the incoming episode does not, YouTube publish-delay strategy returns false because only audio-to-YouTube alignment is supported.

## RedditPodcastPoster.PodcastServices.Apple.Tests

**Summary:** 11 total — 11 passed, 0 failed, 0 skipped

### Scenarios

#### AppleCatalogueInputMappingRules
- [✓] When an Apple API episode is mapped through catalogue input, adapter, and factory, the episode matches the legacy FromApple shape because provider boundaries must preserve indexed fields.
- [✓] When Apple episode title or description has surrounding whitespace, catalogue mapping trims both because legacy FromApple used trimmed values.

#### AppleEpisodeEnricherCatalogueRules
- [✓] When a YouTube release authority episode with negative publishing delay is enriched from Apple, the enricher applies Apple URL and preserves YouTube publish datetime.
- [✓] When Apple catalogue release is on a different calendar date, the enricher does not backfill a midnight UTC stored release.
- [✓] When Apple catalogue release shares the stored calendar date with a non-zero time, the enricher backfills midnight UTC stored release to the Apple publish datetime.
- [✓] When Apple catalogue returns an episode id already owned by another stored episode, Apple enrichment leaves the current episode unchanged.
- [✓] When no Apple catalogue match is found, Apple enrichment leaves the episode unchanged and does not mark Apple URL flags.
- [✓] When the episode is still inside the delayed YouTube publishing window, Apple enrichment is bypassed and does not query the catalogue.
- [✓] When the podcast has no Apple show id but the podcast enricher resolves one, episode enrichment continues and applies a matching catalogue row.
- [✓] When the podcast still has no Apple show id after podcast enricher runs, episode enrichment exits without querying the catalogue.

#### AppleEpisodeResolverCatalogueWrapperRules
- [✓] When the Apple resolver enriches a YouTube-discovered episode with a unique duration match, it returns the AppleEpisode mapped from the domain matcher result.

## RedditPodcastPoster.PodcastServices.Spotify.Tests

**Summary:** 41 total — 41 passed, 0 failed, 0 skipped

### Scenarios

#### EpisodeExtensionsRules
- [✓] When FullEpisode ExternalUrls contains a Spotify link, GetUrl returns that URI because catalogue mapping must resolve episode links from the first external URL entry.
- [✓] When FullEpisode has no images, GetBestImageUrl returns null because enricher thumbnail backfill must tolerate absent artwork.
- [✓] When SimpleEpisode has multiple images, GetBestImageUrl picks the tallest image because indexed artwork should prefer the highest-resolution asset.
- [✓] When SimpleEpisode has no images, GetBestImageUrl returns null because missing artwork must not throw during catalogue mapping.

#### NullEpisodesLeadInPaginatorRules
- [✓] When consecutive null slots appear across pages, NullEpisodesLeadInPaginator stops after the configured threshold because lead-in null runs should not page forever.
- [✓] When null slots are followed by valid episodes within the limit, NullEpisodesLeadInPaginator yields the valid episodes because sparse pages should still surface usable catalogue rows.
- [✓] When the first page contains null episode slots, NullEpisodesLeadInPaginator skips them and yields valid episodes because null API entries must not abort pagination.

#### PodcastEpisodesResultRules
- [✓] When episode results include items with a non-episode Type, PodcastEpisodesResult filters them out because Spotify pages can contain mixed item kinds.
- [✓] When episode results include null entries, PodcastEpisodesResult exposes only typed episodes because null slots must not surface to indexing.

#### SearchResultFinderCatalogueWrapperRules
- [✓] When the Spotify finder accepts a unique-duration match without title overlap, it maps the matched SimpleEpisode back to the platform API type.
- [✓] When the Spotify finder applies a reducer callback, excluded SimpleEpisodes are not returned even when they would otherwise match.
- [✓] When the Spotify finder resolves by release date, it returns the SimpleEpisode whose title and calendar date match the probe.

#### SimpleEpisodePaginatorRules
- [✓] When a page contains a null episode slot, SimpleEpisodePaginator skips it and yields only valid episodes because null API entries must not propagate into indexing.
- [✓] When a page is entirely null slots but has a next link, SimpleEpisodePaginator continues paging because Spotify sometimes returns sparse pages before real episodes appear.
- [✓] When releasedSince is set, SimpleEpisodePaginator excludes episodes older than the cutoff even when null slots appear on the same page.

#### SpotifyCatalogueInputMappingRules
- [✓] When a Spotify API episode is mapped through catalogue input, adapter, and factory, the episode matches the legacy FromSpotify shape because provider boundaries must preserve indexed fields.
- [✓] When FullEpisode has no images, catalogue mapping leaves Image null because both provider DTO paths must tolerate absent artwork.
- [✓] When FullEpisode HtmlDescription is null, catalogue mapping produces an empty sanitized description because the full-episode enricher path must tolerate the same API nulls as SimpleEpisode.
- [✓] When Spotify episode has no images, catalogue mapping leaves Image null because missing artwork must not throw during adapter conversion.
- [✓] When Spotify episode name has leading or trailing whitespace, catalogue mapping trims the title because indexed titles must match legacy FromSpotify behavior.
- [✓] When Spotify returns no HTML description, catalogue mapping produces an empty sanitized description because null descriptions must not propagate as null strings.
- [✓] When the same episode data arrives as SimpleEpisode or FullEpisode, catalogue mapping produces equivalent inputs because both provider paths must index identically.

#### SpotifyClientWrapperNullDtoRules
- [✓] When search response Shows Items is null, GetSimpleShows returns an empty list because missing show collections must be treated as no matches.
- [✓] When search results contain null SimpleShow entries, GetSimpleShows filters them out because null API entries must not break podcast discovery.

#### SpotifyEpisodeEnricherCatalogueReleaseReducerRules
- [✓] When a YouTube release authority episode with negative publishing delay is enriched from Spotify, the enricher filters catalogue candidates via CatalogueReleaseMatches and attaches the aligned row.

#### SpotifyEpisodeEnricherCatalogueRules
- [✓] When a YouTube-only stored episode matches a Spotify catalogue row, the enricher attaches Spotify ID and URL via the domain applicator and marks the enrichment context.
- [✓] When no Spotify catalogue match is found, the enricher leaves the episode unchanged and does not mark Spotify URL flags on the enrichment context.
- [✓] When Spotify catalogue returns an episode id already owned by another stored episode, Spotify enrichment leaves the current episode unchanged.
- [✓] When the episode is still inside the delayed YouTube publishing window, Spotify enrichment is bypassed and does not query the catalogue.
- [✓] When the Spotify resolver reports an expensive query, the enricher side effect marks the podcast Spotify episodes query as expensive.

#### SpotifyEpisodeExtensionsRules
- [✓] When FullEpisode ReleaseDate is a valid Spotify date string, GetReleaseDate parses it because enricher and resolver paths share the same release-date semantics.
- [✓] When FullEpisode ReleaseDate is null or the Spotify placeholder year, GetReleaseDate returns MinValue because full and simple episode paths must tolerate the same API quirks.(releaseDate: "0000")
- [✓] When FullEpisode ReleaseDate is null or the Spotify placeholder year, GetReleaseDate returns MinValue because full and simple episode paths must tolerate the same API quirks.(releaseDate: null)
- [✓] When SimpleEpisode ReleaseDate is a valid Spotify date string, GetReleaseDate parses it because catalogue release comparisons depend on accurate dates.
- [✓] When SimpleEpisode ReleaseDate is null or the Spotify placeholder year, GetReleaseDate returns MinValue because invalid release strings must not break indexing.(releaseDate: "0000")
- [✓] When SimpleEpisode ReleaseDate is null or the Spotify placeholder year, GetReleaseDate returns MinValue because invalid release strings must not break indexing.(releaseDate: null)

#### SpotifyExpensiveQuerySideEffectRules
- [✓] When Spotify FindEpisode does not report an expensive query, the side-effect leaves SpotifyEpisodesQueryIsExpensive unset because no throttling signal was returned.
- [✓] When Spotify FindEpisode reports an expensive query, the side-effect sets SpotifyEpisodesQueryIsExpensive on the podcast because the indexer must throttle future lookups.

#### SpotifyQueryPaginatorRules
- [✓] When pagedEpisodes Items is null, PaginateEpisodes returns an empty result because a missing first page must not throw during show lookup.
- [✓] When PaginateAll returns null episode references mixed with valid episodes, PaginateEpisodes filters them out because Spotify occasionally deserializes sparse episode arrays with null slots.
- [✓] When SkipSpotifyUrlResolving is set, PaginateEpisodes returns empty without calling the client because rate-limit recovery must short-circuit expensive Spotify paging.

## RedditPodcastPoster.PodcastServices.Tests

**Summary:** 45 total — 45 passed, 0 failed, 0 skipped

### Scenarios

#### IndexingEnrichmentRules
- [✓] Enrichment skips episodes still inside the delayed-publishing window (not yet due on YouTube).(audioPlatform: "Apple")
- [✓] Enrichment skips episodes still inside the delayed-publishing window (not yet due on YouTube).(audioPlatform: "Spotify")
- [✓] Episodes in the current discovery batch are excluded from the delayed-publishing second pass.(audioPlatform: "Apple")
- [✓] Episodes in the current discovery batch are excluded from the delayed-publishing second pass.(audioPlatform: "Spotify")
- [✓] For podcasts with positive YouTube publishing delay, a second pass enriches recently expired delayed-publishing episodes that were not part of the current discovery batch.(audioPlatform: "Apple")
- [✓] For podcasts with positive YouTube publishing delay, a second pass enriches recently expired delayed-publishing episodes that were not part of the current discovery batch.(audioPlatform: "Spotify")
- [✓] When Apple URL or ID is missing, indexing attempts Apple enrichment.
- [✓] When both Spotify and Apple links are missing, indexing enriches Spotify first then Apple and the episode retains both platform fields without cross-overwrite.
- [✓] When SkipEnrichingFromYouTube is true, YouTube enrichment is not attempted.
- [✓] When Spotify URL or ID is missing, indexing attempts Spotify enrichment.
- [✓] When Spotify, Apple, and YouTube links are all missing, indexing enriches in platform order and the episode retains all three platform fields without cross-overwrite.
- [✓] When the Apple enricher mock applies a real domain patch, indexing updates the episode's Apple platform fields on the stored row, not only the enrichment context.
- [✓] When the Spotify enricher mock applies a real domain patch, indexing updates the episode's Spotify platform fields on the stored row, not only the enrichment context.
- [✓] When the YouTube enricher mock applies a real domain patch, indexing updates the episode's YouTube platform fields on the stored row, not only the enrichment context.
- [✓] When YouTube URL or ID is missing and the podcast has a YouTube channel, indexing attempts YouTube enrichment.

#### IndexingOrchestrationRules
- [✓] Expensive-query flags discovered during indexing are persisted on the podcast.
- [✓] Full indexing discovers episodes, merges, enriches, filters, then persists.
- [✓] LastIndexed is not updated when scheduled YouTube discovery is bypassed.
- [✓] LastIndexed is not updated when Spotify URL resolving is bypassed during indexing.
- [✓] LastIndexed is not updated when YouTube URL resolving is bypassed during indexing.
- [✓] LatestReleased on the podcast reflects the most recent release among added and merged episodes.
- [✓] When a YouTube-only episode already has all configured platform links, enrich-only indexing does not invoke the enricher.
- [✓] When IgnoreAllEpisodes is set, newly discovered episodes are marked ignored before persistence.
- [✓] When merge produces failed episodes, LastIndexed is not updated even though MergedEpisodes may be empty.
- [✓] When ShouldEnrichDespiteReleaseWindow applies, enrich-only indexing includes a YouTube-only episode missing Spotify even when its release is outside the normal enrichment window.
- [✓] When SkipShortEpisodes is set, short discovered episodes are removed before merge.
- [✓] When YouTube channel search becomes forbidden during indexing, the flag is persisted on the podcast.
- [✓] When YouTube quota is exhausted and YouTube resolving flips to skipped mid-run, the quota tracker records that the podcast was not indexed.
- [✓] When YouTube quota is exhausted during enrich-only indexing, the quota tracker records that the podcast was not enriched.

#### IndexingPersistenceRules
- [✓] Enrich-only indexing does not discover new catalogue episodes from the episode provider.
- [✓] LastIndexed is not updated when indexing records merge failures.
- [✓] LastIndexed is updated only when indexing succeeds without merge failures or platform bypasses.
- [✓] Persist order: enriched episodes are saved, then filtered, then merged existing, then added.

#### IndexingScopeRules
- [✓] Episodes below minimum duration are marked ignored during indexing.

#### PlatformEnrichmentResultExtensionsRules
- [✓] When a platform enrichment result carries a platform URL but no service, ApplyTo does not mark any platform URL flags because the target persistence field is unknown.
- [✓] When a platform enrichment result carries a platform URL, ApplyTo marks the matching enrichment context URL flag because persistence tracks per-service link updates.(service: Apple)
- [✓] When a platform enrichment result carries a platform URL, ApplyTo marks the matching enrichment context URL flag because persistence tracks per-service link updates.(service: Spotify)
- [✓] When a platform enrichment result carries a platform URL, ApplyTo marks the matching enrichment context URL flag because persistence tracks per-service link updates.(service: YouTube)
- [✓] When a platform enrichment result carries a service but no platform URL, ApplyTo does not mark any platform URL flags because persistence only tracks concrete link updates.
- [✓] When a platform enrichment result did not update anything, ApplyTo leaves the enrichment context unchanged because no persistence fields were touched.
- [✓] When a platform enrichment result reports a release update, ApplyTo propagates the release to the enrichment context because persistence tracks release backfill separately.
- [✓] When a platform enrichment result reports release updated but carries no release value, ApplyTo does not mark the enrichment context release flag because there is nothing to persist.

#### PlatformEpisodeEnricherTemplateRules
- [✓] When an episode is outside the delayed-publishing window, the shared enricher template does not bypass platform enrichment.
- [✓] When an episode is still inside the delayed-publishing window, the shared enricher template bypasses platform enrichment because audio is not yet due on YouTube.
- [✓] When ApplyResolvedCandidate applies a Spotify catalogue candidate, the enrichment context records the Spotify URL update and the episode receives the platform link via the applicator.

## RedditPodcastPoster.PodcastServices.YouTube.Tests

**Summary:** 64 total — 64 passed, 0 failed, 0 skipped

### Scenarios

#### PlaylistItemFinderCatalogueWrapperRules
- [✓] When a single playlist title contains the stored episode title as a substring, exact-title matching resolves that candidate after duration validation.
- [✓] When an exact-title candidate is found but YouTube returns no video details for that id, no PlaylistItem is returned.
- [✓] When duration-only matching fails but publish delay aligns via IsCatalogueMatch, each fuzzy title variant on a short title resolves the delayed YouTube video.(strategy: AddFillerWord)
- [✓] When duration-only matching fails but publish delay aligns via IsCatalogueMatch, each fuzzy title variant on a short title resolves the delayed YouTube video.(strategy: DropWord)
- [✓] When duration-only matching fails but publish delay aligns via IsCatalogueMatch, each fuzzy title variant on a short title resolves the delayed YouTube video.(strategy: ReplaceWord)
- [✓] When duration-only matching fails but publish delay aligns via IsCatalogueMatch, each fuzzy title variant on a short title resolves the delayed YouTube video.(strategy: SwapAdjacentWords)
- [✓] When duration-only matching finds a closest video outside the two-minute tolerance, no PlaylistItem is returned.
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a long title resolves via local text closeness.(strategy: AddFillerWord)
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a long title resolves via local text closeness.(strategy: DropWord)
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a long title resolves via local text closeness.(strategy: ReplaceWord)
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a long title resolves via local text closeness.(strategy: SwapAdjacentWords)
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a short title resolves via local text closeness.(strategy: AddFillerWord)
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a short title resolves via local text closeness.(strategy: DropWord)
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a short title resolves via local text closeness.(strategy: ReplaceWord)
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a short title resolves via local text closeness.(strategy: SwapAdjacentWords)
- [✓] When episode-number matching succeeds but video duration is unacceptable, no PlaylistItem is returned.
- [✓] When exact title matching fails, each fuzzy title variant on a short title may still resolve via shared episode number and acceptable video duration.(strategy: AddFillerWord)
- [✓] When exact title matching fails, each fuzzy title variant on a short title may still resolve via shared episode number and acceptable video duration.(strategy: DropWord)
- [✓] When exact title matching fails, each fuzzy title variant on a short title may still resolve via shared episode number and acceptable video duration.(strategy: ReplaceWord)
- [✓] When exact title matching fails, each fuzzy title variant on a short title may still resolve via shared episode number and acceptable video duration.(strategy: SwapAdjacentWords)
- [✓] When exact title, episode number, and publish-delay paths fail, duration-only matching may still resolve a video within the two-minute tolerance.(offsetSeconds: 0)
- [✓] When exact title, episode number, and publish-delay paths fail, duration-only matching may still resolve a video within the two-minute tolerance.(offsetSeconds: 90)
- [✓] When fuzzy title matching finds a candidate but per-video duration validation fails, no PlaylistItem is returned.
- [✓] When live filtering leaves no completed public videos, the playlist finder returns no match.
- [✓] When live or upcoming playlist videos are filtered out, matching proceeds against completed public videos only.
- [✓] When live/upcoming filtering cannot load video details, the finder keeps the original playlist and may still match completed-looking items.
- [✓] When multiple playlist items share an exact substring title match, the playlist finder does not resolve via exact title alone.
- [✓] When multiple playlist titles share the same episode number and other matchers also fail, no PlaylistItem is returned.
- [✓] When playlist item titles share no meaningful word overlap and fall below the fuzzy threshold, no PlaylistItem is returned even when duration would otherwise align.
- [✓] When publish delay catalogue matching succeeds but video duration exceeds the 5-minute publication tolerance, no PlaylistItem is returned.
- [✓] When publish time aligns but domain catalogue matching rejects the candidate, no PlaylistItem is returned.
- [✓] When publish-delay catalogue matching succeeds and video duration is within the 5-minute publication tolerance, the matching PlaylistItem is returned.
- [✓] When publish-delay catalogue matching succeeds but the YouTube video is shorter than five minutes, no PlaylistItem is returned.
- [✓] When the closest playlist publish time is more than one day from the delay-adjusted expectation, publish-delay matching does not resolve a video.
- [✓] When the playlist finder matches on exact episode title but video duration is unacceptable, no PlaylistItem is returned.
- [✓] When the playlist finder resolves by exact episode title, it returns the PlaylistItem whose title and duration match the stored episode.
- [✓] When the stored episode lacks accurate release time, publish-delay matching is skipped even if a delayed YouTube publish would otherwise align.

#### SearchResultFinderCatalogueWrapperRules
- [✓] When duration-only matching fails but publish delay aligns via IsCatalogueMatch, each fuzzy title variant on a short title resolves the delayed YouTube video.(strategy: AddFillerWord)
- [✓] When duration-only matching fails but publish delay aligns via IsCatalogueMatch, each fuzzy title variant on a short title resolves the delayed YouTube video.(strategy: DropWord)
- [✓] When duration-only matching fails but publish delay aligns via IsCatalogueMatch, each fuzzy title variant on a short title resolves the delayed YouTube video.(strategy: ReplaceWord)
- [✓] When duration-only matching fails but publish delay aligns via IsCatalogueMatch, each fuzzy title variant on a short title resolves the delayed YouTube video.(strategy: SwapAdjacentWords)
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a short title resolves via local text closeness.(strategy: AddFillerWord)
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a short title resolves via local text closeness.(strategy: DropWord)
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a short title resolves via local text closeness.(strategy: ReplaceWord)
- [✓] When earlier matchers fail and no publish delay is configured, each fuzzy title variant on a short title resolves via local text closeness.(strategy: SwapAdjacentWords)
- [✓] When multiple search results share an exact substring title match, the search-result finder falls through to duration-only matching.
- [✓] When publish delay catalogue matching succeeds but video duration exceeds the 5-minute publication tolerance, no SearchResult is returned.
- [✓] When the search-result finder matches on exact episode title but video duration is unacceptable, no SearchResult is returned.
- [✓] When the search-result finder resolves by exact episode title, it returns the SearchResult whose title and duration match the stored episode.

#### YouTubeCatalogueInputMappingRules
- [✓] When a YouTube search result is mapped through catalogue input, adapter, and factory, the episode matches the legacy FromYouTube shape because provider boundaries must preserve indexed fields.
- [✓] When the same YouTube video is mapped from search results or a playlist item, catalogue mapping produces equivalent inputs because both retrieval paths feed the same adapter.
- [✓] When YouTube video content details omit duration, catalogue mapping uses zero duration because the provider filters zero-length videos downstream.

#### YouTubeEpisodeEnricherCatalogueRules
- [✓] When a matching catalogue item is found and the episode already has description and thumbnail, the enricher does not load supplemental YouTube video details.
- [✓] When a matching catalogue item is found and the episode is missing a YouTube thumbnail, the enricher loads video details and applies the resolved image via the applicator.
- [✓] When a matching YouTube catalogue item is found and the episode has no description, the enricher loads video details and fills the description via the applicator.
- [✓] When a stored episode has a YouTube ID but no URL, the enricher backfills the watch URL and marks the enrichment context as updated.
- [✓] When a stored episode has a YouTube URL but no ID, the enricher extracts the video ID and marks the enrichment context YouTubeId flag.
- [✓] When a stored episode has a YouTube URL that does not contain an extractable video id, the enricher leaves YouTube identity unchanged.
- [✓] When Apple identity is missing and the stored release is midnight UTC, a matching YouTube catalogue item backfills publish time-of-day on the same calendar date.
- [✓] When no matching YouTube catalogue item is found, the enricher leaves YouTube identity unchanged and does not mark YouTube URL flags.
- [✓] When the episode is still inside the delayed YouTube publishing window, YouTube enrichment is bypassed and does not query the catalogue.
- [✓] When the podcast defines a description regex, the enricher sanitises the YouTube video description before applying it to the episode.
- [✓] When the resolver returns a playlist item match without a search result, the enricher applies the catalogue candidate via the playlist-item path.
- [✓] When YouTube catalogue returns a video id already owned by another stored episode, YouTube enrichment leaves the current episode unchanged.

## RedditPodcastPoster.UrlSubmission.Tests

**Summary:** 36 total — 36 passed, 0 failed, 0 skipped

### Scenarios

#### UrlSubmissionCategorisationRules
- [✓] When a categorised platform DTO is converted for enrichment, ToAdapterInput carries episode id, title, description, release, duration, URL, and artwork.(platform: Apple)
- [✓] When a categorised platform DTO is converted for enrichment, ToAdapterInput carries episode id, title, description, release, duration, URL, and artwork.(platform: Spotify)
- [✓] When a categorised platform DTO is converted for enrichment, ToAdapterInput carries episode id, title, description, release, duration, URL, and artwork.(platform: YouTube)
- [✓] When a platform ResolvedAppleItem is mapped at the categoriser boundary, every DTO field on CategorisedAppleItem mirrors the platform resolved item.
- [✓] When a platform ResolvedSpotifyItem is mapped at the categoriser boundary, every DTO field on CategorisedSpotifyItem mirrors the platform resolved item.
- [✓] When a platform ResolvedYouTubeItem is mapped at the categoriser boundary, every DTO field on CategorisedYouTubeItem mirrors the platform resolved item including PlaylistId.
- [✓] When enrichment adapts a categorised DTO via ToAdapterInput, the EpisodeCandidate matches adapting the equivalent Resolved*ItemInput directly.(platform: Apple)
- [✓] When enrichment adapts a categorised DTO via ToAdapterInput, the EpisodeCandidate matches adapting the equivalent Resolved*ItemInput directly.(platform: Spotify)
- [✓] When enrichment adapts a categorised DTO via ToAdapterInput, the EpisodeCandidate matches adapting the equivalent Resolved*ItemInput directly.(platform: YouTube)

#### UrlSubmissionEnrichmentRules
- [✓] Submitting a URL for an episode that already exists enriches missing platform links on the stored episode.
- [✓] When a non-podcast resolved item carries artwork and the episode has no Other image, UrlSubmission enrichment stores the image on Images.YouTube (current behavior).
- [✓] When a stored episode description is complete and a resolved non-podcast item offers longer text, UrlSubmission enrichment does not replace the stored description.
- [✓] When an existing episode already has a BBC URL, UrlSubmission enrichment does not replace the stored BBC link.
- [✓] When an existing episode already has a platform identifier but is missing the platform URL, UrlSubmission enrichment fills the missing URL via the domain applicator.
- [✓] When an existing episode already has all resolved platform links, the result remains EpisodeAlreadyExists.
- [✓] When an existing episode already has an Internet Archive URL, UrlSubmission enrichment does not replace the stored link.
- [✓] When an existing episode gains missing platform links, the result becomes Enriched instead of EpisodeAlreadyExists.
- [✓] When an existing episode has a truncated Apple description ending in ellipsis, UrlSubmission enrichment extends the description parity with the domain applicator.
- [✓] When an existing episode has a truncated non-podcast description ending in ellipsis, UrlSubmission enrichment extends the description from the resolved item.
- [✓] When an existing episode has a truncated Spotify description ending in ellipsis, UrlSubmission enrichment extends the description parity with the domain applicator.
- [✓] When an existing episode has a truncated YouTube description ending in ellipsis, UrlSubmission enrichment extends the description parity with the domain applicator.
- [✓] When an existing episode has midnight UTC release and a resolved Apple item carries publish time-of-day, UrlSubmission enrichment backfills release time parity with the domain applicator.
- [✓] When an existing episode has midnight UTC release and a resolved non-podcast item carries publish time-of-day, UrlSubmission enrichment backfills release on the stored episode.
- [✓] When an existing episode has midnight UTC release and a resolved YouTube item carries publish time-of-day, UrlSubmission enrichment backfills release time parity with the domain applicator.
- [✓] When an existing episode is missing a BBC link and a resolved non-podcast BBC item is present, UrlSubmission enrichment fills the BBC URL on the stored episode.
- [✓] When an existing episode is missing an Internet Archive link and a resolved non-podcast item is present, UrlSubmission enrichment fills the Internet Archive URL on the stored episode.
- [✓] When podcast show metadata is enriched, the podcast receives missing show identifiers from resolved items.
- [✓] When the podcast already has Apple, Spotify, and YouTube show identifiers, resolved platform items do not re-enrich podcast metadata.

#### UrlSubmissionPersistenceRules
- [✓] New podcast submission saves both podcast and episode.
- [✓] Submitting a URL for an existing episode that is enriched saves the episode.
- [✓] When a new episode is added to an existing podcast, only the episode is saved because the podcast row is unchanged unless show metadata was enriched.
- [✓] When an episode already exists and no fields change, neither podcast nor episode is saved.
- [✓] When both podcast metadata and episode are enriched on an existing podcast submission, both podcast and episode are saved.
- [✓] When PersistToDatabase is false for an existing podcast submission, no repository writes occur.
- [✓] When PersistToDatabase is false, no repository writes occur.
- [✓] When podcast show metadata is enriched but the episode is unchanged, only the podcast is saved because unchanged episodes must not trigger a redundant episode write.

---

## Overall

**432 tests** — **432 passed**, **0 failed**, **0 skipped** across **6 assemblies**.

