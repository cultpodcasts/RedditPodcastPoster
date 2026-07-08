using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Tests.Support;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class IndexingOrchestrationRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private static readonly DateTime EpisodeRelease = DomainTestFixture.UtcDateDaysAgo(30);
    private static readonly TimeSpan SubMinimumDuration =
        PodcastUpdaterTestHarness.DefaultPostingCriteria.MinimumDuration - TimeSpan.FromMinutes(1);

    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Full indexing discovers episodes, merges, enriches, filters, then persists.")]
    public async Task full_indexing_runs_discover_merge_enrich_filter_and_persist_stages()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var spotifyShowId = _fixture.CreateSpotifyId();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(spotifyShowId);
        harness.PodcastRepository.Seed(podcast);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(_fixture.CreateDuration()));

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([discovered]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert discovery, enrichment, and filtering all run before persistence
        harness.EpisodeProvider.Verify(
            x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()),
            Times.Once);
        harness.EpisodeEnricher.Verify(
            x => x.EnrichEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()),
            Times.Once);
        harness.PodcastFilter.Verify(
            x => x.Filter(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<List<string>>()),
            Times.Once);
        harness.EpisodeRepository.SavedEpisodes.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When SkipShortEpisodes is set, short discovered episodes are removed before merge.")]
    public async Task skip_short_episodes_removes_short_discovered_episodes_before_merge()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var spotifyShowId = _fixture.CreateSpotifyId();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(spotifyShowId);
        harness.PodcastRepository.Seed(podcast);

        var shortEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(SubMinimumDuration));

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([shortEpisode]);

        var indexingContext = PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince) with
        {
            SkipShortEpisodes = true
        };

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            indexingContext);

        // Assert the short episode is not added because it was removed before merge
        harness.EpisodeRepository.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "LatestReleased on the podcast reflects the most recent release among added and merged episodes.")]
    public async Task latest_released_is_updated_from_added_and_merged_episodes()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.LatestReleased = EpisodeRelease.AddDays(-30);
        harness.PodcastRepository.Seed(podcast);

        var mergeInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithRelease(EpisodeRelease.AddDays(1))
            .WithDuration(_fixture.CreateDuration())
            .WithDescription("Truncated description..."));
        var mergedExisting = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(mergeInput.SpotifyId)
            .WithRelease(mergeInput.Release)
            .WithDuration(mergeInput.Duration)
            .WithDescription(mergeInput.Description));
        mergedExisting.Id = _fixture.CreateGuid();
        mergedExisting.PodcastId = podcast.Id;
        harness.EpisodeRepository.Seed(mergedExisting);

        var mergeDiscovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(mergeInput.SpotifyId)
            .WithSpotifyUrl(new Uri($"{mergeInput.SpotifyUrl}?si=merge"))
            .WithRelease(mergeInput.Release)
            .WithDuration(mergeInput.Duration)
            .WithDescription(_fixture.Create<string>()));

        var addedInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithRelease(EpisodeRelease.AddDays(5))
            .WithDuration(_fixture.CreateDuration()));
        var addedDiscovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(addedInput.SpotifyId)
            .WithRelease(addedInput.Release)
            .WithDuration(addedInput.Duration));

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([mergeDiscovered, addedDiscovered]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert LatestReleased reflects the newest added episode release
        podcast.LatestReleased.Should().Be(EpisodeRelease.AddDays(5));
        harness.PodcastRepository.GetStored(podcast.Id).LatestReleased.Should().Be(EpisodeRelease.AddDays(5));
    }

    [Fact(DisplayName =
        "Expensive-query flags discovered during indexing are persisted on the podcast.")]
    public async Task expensive_query_flags_are_persisted_on_the_podcast()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.SpotifyEpisodesQueryIsExpensive = null;
        podcast.YouTubePlaylistQueryIsExpensive = null;
        harness.PodcastRepository.Seed(podcast);

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IndexingContext>((indexedPodcast, _, _) =>
            {
                indexedPodcast.SpotifyEpisodesQueryIsExpensive = true;
                indexedPodcast.YouTubePlaylistQueryIsExpensive = true;
            })
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert the discovered expensive-query flags are persisted on the podcast
        podcast.HasExpensiveSpotifyEpisodesQuery().Should().BeTrue();
        podcast.HasExpensiveYouTubePlaylistQuery().Should().BeTrue();
        harness.PodcastRepository.SavedPodcasts.Should().ContainSingle();
        harness.PodcastRepository.GetStored(podcast.Id).HasExpensiveSpotifyEpisodesQuery().Should().BeTrue();
        harness.PodcastRepository.GetStored(podcast.Id).HasExpensiveYouTubePlaylistQuery().Should().BeTrue();
    }

    [Fact(DisplayName =
        "LastIndexed is not updated when Spotify URL resolving is bypassed during indexing.")]
    public async Task last_indexed_is_not_set_when_spotify_bypass_occurs()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.LastIndexed = null;
        harness.PodcastRepository.Seed(podcast);

        var indexingContext = PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince);

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IndexingContext>((_, _, context) =>
            {
                context.SkipSpotifyUrlResolving = true;
            })
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            indexingContext);

        // Assert LastIndexed remains unset
        podcast.LastIndexed.Should().BeNull();
        harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "LastIndexed is not updated when YouTube URL resolving is bypassed during indexing.")]
    public async Task last_indexed_is_not_set_when_youtube_bypass_occurs()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.LastIndexed = null;
        harness.PodcastRepository.Seed(podcast);

        var indexingContext = PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince);

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IndexingContext>((_, _, context) =>
            {
                context.SkipYouTubeUrlResolving = true;
            })
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            indexingContext);

        // Assert LastIndexed remains unset
        podcast.LastIndexed.Should().BeNull();
        harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "LastIndexed is not updated when scheduled YouTube discovery is bypassed.")]
    public async Task last_indexed_is_not_set_when_scheduled_youtube_discovery_is_bypassed()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.LastIndexed = null;
        });
        harness.PodcastRepository.Seed(podcast);

        var indexingContext = new IndexingContext(ReleasedSince, SkipYouTubeUrlResolving: true);

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            indexingContext);

        // Assert LastIndexed remains unset
        podcast.LastIndexed.Should().BeNull();
        harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When ShouldEnrichDespiteReleaseWindow applies, enrich-only indexing includes a YouTube-only episode " +
        "missing Spotify even when its release is outside the normal enrichment window.")]
    public async Task enrich_only_includes_episode_when_should_enrich_despite_release_window()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var delay = TimeSpan.FromDays(-31).Add(TimeSpan.FromHours(-12));
        var expectedAudioRelease = DateTime.UtcNow.AddDays(4);
        var youTubeRelease = expectedAudioRelease.Add(delay);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.SpotifyId = _fixture.CreateSpotifyId();
        podcast.AppleId = null;
        podcast.YouTubePublicationOffset = delay.Ticks;
        harness.PodcastRepository.Seed(podcast);

        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            release: youTubeRelease);
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;
        harness.EpisodeRepository.Seed(episode);

        var enrichedEpisodeIds = new List<Guid>();
        harness.EpisodeEnricher
            .Setup(x => x.EnrichEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IList<Episode>, IndexingContext>((_, contextEpisodes, _, _) =>
            {
                enrichedEpisodeIds.AddRange(contextEpisodes.Select(e => e.Id));
            })
            .ReturnsAsync(new EnrichmentResults([]));

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: true,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        enrichedEpisodeIds.Should().Contain(episode.Id);
        harness.EpisodeEnricher.Verify(
            x => x.EnrichEpisodes(
                podcast,
                It.Is<IEnumerable<Episode>>(eps => eps.Any(e => e.Id == episode.Id)),
                It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When a YouTube-only episode already has all configured platform links, enrich-only indexing " +
        "does not invoke the enricher.")]
    public async Task enrich_only_excludes_fully_linked_episode_when_no_platform_ids_missing()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.SpotifyId = _fixture.CreateSpotifyId();
        podcast.AppleId = null;
        harness.PodcastRepository.Seed(podcast);

        var spotifyId = _fixture.CreateSpotifyId();
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeAndSpotify(
            podcast,
            spotifyId,
            youTubeId,
            release: DomainTestFixture.UtcDateDaysAgo(30));
        harness.EpisodeRepository.Seed(episode);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: true,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert — orchestrator still calls EnrichEpisodes, but no rows need platform backfill
        harness.EpisodeEnricher.Verify(
            x => x.EnrichEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.Is<IList<Episode>>(eps => !eps.Any()),
                It.IsAny<IndexingContext>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When IgnoreAllEpisodes is set, newly discovered episodes are marked ignored before persistence.")]
    public async Task ignore_all_episodes_marks_added_episodes_ignored()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.IgnoreAllEpisodes = true;
        harness.PodcastRepository.Seed(podcast);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(_fixture.CreateDuration()));
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([discovered]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        harness.EpisodeRepository.SavedEpisodes.Should().ContainSingle();
        harness.EpisodeRepository.SavedEpisodes.Single().Ignored.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When YouTube channel search becomes forbidden during indexing, the flag is persisted on the podcast.")]
    public async Task youtube_channel_search_forbidden_flag_is_persisted()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.YouTubeChannelSearchForbidden = null;
        harness.PodcastRepository.Seed(podcast);

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IndexingContext>((indexedPodcast, _, _) =>
            {
                indexedPodcast.YouTubeChannelSearchForbidden = true;
            })
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        podcast.HasYouTubeChannelSearchForbidden().Should().BeTrue();
        harness.PodcastRepository.SavedPodcasts.Should().ContainSingle();
        harness.PodcastRepository.GetStored(podcast.Id).HasYouTubeChannelSearchForbidden().Should().BeTrue();
    }

    [Fact(DisplayName =
        "When YouTube quota is exhausted during enrich-only indexing, the quota tracker records " +
        "that the podcast was not enriched.")]
    public async Task youtube_quota_exhausted_records_not_enriched_for_enrich_only()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        harness.PodcastRepository.Seed(podcast);

        var indexingContext = PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince) with
        {
            YouTubeQuotaExhausted = true
        };

        // Act
        await harness.Updater.Update(podcast, enrichOnly: true, indexingContext);

        // Assert
        harness.YouTubeQuotaUsageTracker.Verify(
            x => x.RecordPodcastNotEnrichedDueToQuotaAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        harness.YouTubeQuotaUsageTracker.Verify(
            x => x.RecordPodcastNotIndexedDueToQuotaAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When YouTube quota is exhausted and YouTube resolving flips to skipped mid-run, " +
        "the quota tracker records that the podcast was not indexed.")]
    public async Task youtube_quota_exhausted_records_not_indexed_when_discovery_bypassed()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.LastIndexed = null;
        });
        harness.PodcastRepository.Seed(podcast);

        var indexingContext = PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince) with
        {
            YouTubeQuotaExhausted = true
        };

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IndexingContext>((_, _, context) =>
            {
                context.SkipYouTubeUrlResolving = true;
            })
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(podcast, enrichOnly: false, indexingContext);

        // Assert
        harness.YouTubeQuotaUsageTracker.Verify(
            x => x.RecordPodcastNotIndexedDueToQuotaAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        harness.YouTubeQuotaUsageTracker.Verify(
            x => x.RecordPodcastNotEnrichedDueToQuotaAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When merge produces failed episodes, LastIndexed is not updated even though MergedEpisodes may be empty.")]
    public async Task failed_merge_does_not_update_last_indexed()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.LastIndexed = null;
        harness.PodcastRepository.Seed(podcast);

        var sharedRelease = DomainTestFixture.UtcDaysAgo(32);
        var sharedLength = _fixture.CreateDuration();
        var sharedTitle = _fixture.CreateTitle();
        var (youTubeOnly, appleOnly) = _fixture.CreateAmbiguousMatchStoredEpisodes(
            podcast,
            sharedRelease,
            sharedLength,
            sharedTitle);
        harness.EpisodeRepository.Seed(youTubeOnly);
        harness.EpisodeRepository.Seed(appleOnly);

        var discovered = _fixture.CreateAmbiguousMatchSpotifyIncoming(
            sharedRelease,
            sharedLength,
            sharedTitle);
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([discovered]);

        // Act
        var result = await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        result.MergeResult.FailedEpisodes.Should().ContainSingle();
        podcast.LastIndexed.Should().BeNull();
    }
}
