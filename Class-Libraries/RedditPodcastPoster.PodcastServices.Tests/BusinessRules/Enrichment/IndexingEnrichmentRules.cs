using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Enrichers;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.YouTube.Enrichment;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Enrichment;

public class IndexingEnrichmentRules
{
    private static readonly TimeSpan PublishingDelay = TimeSpan.FromDays(1);

    private readonly DomainTestFixture _fixture = new();

    public static TheoryData<string> DelayedPublishingAudioPlatforms =>
        new() { "Apple", "Spotify" };

    [Fact(DisplayName =
        "When Spotify URL or ID is missing, indexing attempts Spotify enrichment.")]
    public async Task spotify_enricher_is_invoked_when_spotify_link_is_missing()
    {
        // Arrange
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();

        spotifyEnricher
            .Setup(x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()))
            .Callback<EnrichmentRequest, IndexingContext, EnrichmentContext>((_, _, context) =>
            {
                context.Spotify = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
            })
            .Returns(Task.CompletedTask);

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var episode = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        episode.PodcastId = podcast.Id;
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;

        // Act
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Assert
        spotifyEnricher.Verify(
            x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()),
            Times.Once);
        results.UpdatedEpisodes.Should().ContainSingle();
        results.UpdatedEpisodes.Single().Episode.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "When Apple URL or ID is missing, indexing attempts Apple enrichment.")]
    public async Task apple_enricher_is_invoked_when_apple_link_is_missing()
    {
        // Arrange
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();

        appleEnricher
            .Setup(x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()))
            .Callback<EnrichmentRequest, IndexingContext, EnrichmentContext>((_, _, context) =>
            {
                context.Apple = _fixture.DefaultAppleUrl(_fixture.CreateAppleId());
            })
            .Returns(Task.CompletedTask);

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AppleId = _fixture.CreateAppleId();

        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        episode.PodcastId = podcast.Id;
        episode.AppleId = null;
        episode.Urls.Apple = null;

        // Act
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Assert
        appleEnricher.Verify(
            x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()),
            Times.Once);
        results.UpdatedEpisodes.Should().ContainSingle();
        results.UpdatedEpisodes.Single().Episode.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "When YouTube URL or ID is missing and the podcast has a YouTube channel, indexing attempts YouTube enrichment.")]
    public async Task youtube_enricher_is_invoked_when_youtube_link_is_missing_and_channel_present()
    {
        // Arrange
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();

        youTubeEnricher
            .Setup(x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()))
            .Callback<EnrichmentRequest, IndexingContext, EnrichmentContext>((_, _, context) =>
            {
                PodcastServicesEpisodeEnricherTestSupport.FillYouTubeLink(context, _fixture.CreateYouTubeId());
            })
            .Returns(Task.CompletedTask);

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();

        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        episode.PodcastId = podcast.Id;
        episode.YouTubeId = string.Empty;
        episode.Urls.YouTube = null;

        // Act
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Assert
        youTubeEnricher.Verify(
            x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()),
            Times.Once);
        results.UpdatedEpisodes.Should().ContainSingle();
        results.UpdatedEpisodes.Single().Episode.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "When SkipEnrichingFromYouTube is true, YouTube enrichment is not attempted.")]
    public async Task youtube_enricher_is_skipped_when_skip_enriching_from_youtube_is_true()
    {
        // Arrange
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.SkipEnrichingFromYouTube = true;

        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        episode.PodcastId = podcast.Id;
        episode.YouTubeId = string.Empty;
        episode.Urls.YouTube = null;

        // Act
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Assert
        youTubeEnricher.Verify(
            x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()),
            Times.Never);
        results.UpdatedEpisodes.Should().BeEmpty();
    }

    [Theory(DisplayName =
        "For podcasts with positive YouTube publishing delay, a second pass enriches recently expired delayed-publishing episodes that were not part of the current discovery batch.")]
    [MemberData(nameof(DelayedPublishingAudioPlatforms))]
    public async Task delayed_publishing_second_pass_enriches_recently_expired_stored_episodes(
        string audioPlatform)
    {
        // Arrange
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();
        var enrichedEpisodeIds = new List<Guid>();

        youTubeEnricher
            .Setup(x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()))
            .Callback<EnrichmentRequest, IndexingContext, EnrichmentContext>((request, _, context) =>
            {
                enrichedEpisodeIds.Add(request.Episode.Id);
                PodcastServicesEpisodeEnricherTestSupport.FillYouTubeLink(context, _fixture.CreateYouTubeId());
            })
            .Returns(Task.CompletedTask);

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = PodcastServicesEpisodeEnricherTestSupport.CreateDelayedPublishingPodcast(
            _fixture.CreateSpotifyId(),
            _fixture.CreateYouTubeChannelId(),
            PublishingDelay);

        var expiredStoredEpisode = CreateStoredEpisodeAwaitingYouTubeEnrichment(
            audioPlatform,
            ReleaseRecentlyExpiredDelayedPublishing(audioPlatform, PublishingDelay));
        expiredStoredEpisode.PodcastId = podcast.Id;

        var discoveredYouTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithDuration(_fixture.CreateDuration()));
        var discoveredEpisode = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(discoveredYouTubeInput.YouTubeId)
            .WithDuration(discoveredYouTubeInput.Duration));
        discoveredEpisode.PodcastId = podcast.Id;

        // Act
        var results = await enricher.EnrichEpisodes(
            podcast,
            [expiredStoredEpisode, discoveredEpisode],
            [discoveredEpisode],
            new IndexingContext());

        // Assert the recently expired stored episode is enriched in the delayed-publishing second pass
        enrichedEpisodeIds.Should().ContainSingle().Which.Should().Be(expiredStoredEpisode.Id);
        // Assert
        results.UpdatedEpisodes.Should().ContainSingle();
        results.UpdatedEpisodes.Single().Episode.Id.Should().Be(expiredStoredEpisode.Id);
    }

    [Theory(DisplayName =
        "Episodes in the current discovery batch are excluded from the delayed-publishing second pass.")]
    [MemberData(nameof(DelayedPublishingAudioPlatforms))]
    public async Task episodes_in_new_episodes_batch_are_excluded_from_delayed_publishing_second_pass(
        string audioPlatform)
    {
        // Arrange
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();

        youTubeEnricher
            .Setup(x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()))
            .Callback<EnrichmentRequest, IndexingContext, EnrichmentContext>((_, _, context) =>
            {
                PodcastServicesEpisodeEnricherTestSupport.FillYouTubeLink(context, _fixture.CreateYouTubeId());
            })
            .Returns(Task.CompletedTask);

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = PodcastServicesEpisodeEnricherTestSupport.CreateDelayedPublishingPodcast(
            _fixture.CreateSpotifyId(),
            _fixture.CreateYouTubeChannelId(),
            PublishingDelay);

        var batchEpisode = CreateStoredEpisodeAwaitingYouTubeEnrichment(
            audioPlatform,
            ReleaseRecentlyExpiredDelayedPublishing(audioPlatform, PublishingDelay));
        batchEpisode.PodcastId = podcast.Id;

        // Act
        await enricher.EnrichEpisodes(
            podcast,
            [batchEpisode],
            [batchEpisode],
            new IndexingContext());

        // Assert
        youTubeEnricher.Verify(
            x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()),
            Times.Once);
    }

    [Theory(DisplayName =
        "Enrichment skips episodes still inside the delayed-publishing window (not yet due on YouTube).")]
    [MemberData(nameof(DelayedPublishingAudioPlatforms))]
    public async Task enrichment_skips_episodes_still_inside_delayed_publishing_window(
        string audioPlatform)
    {
        // Arrange
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = PodcastServicesEpisodeEnricherTestSupport.CreateDelayedPublishingPodcast(
            _fixture.CreateSpotifyId(),
            _fixture.CreateYouTubeChannelId(),
            PublishingDelay);

        var inWindowEpisode = CreateStoredEpisodeAwaitingYouTubeEnrichment(
            audioPlatform,
            ReleaseStillInsideDelayedPublishingWindow(audioPlatform, PublishingDelay));
        inWindowEpisode.PodcastId = podcast.Id;

        // Act
        var results = await enricher.EnrichEpisodes(
            podcast,
            [inWindowEpisode],
            [],
            new IndexingContext());

        // Assert
        youTubeEnricher.Verify(
            x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()),
            Times.Never);
        results.UpdatedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the Spotify enricher mock applies a real domain patch, indexing updates the episode's " +
        "Spotify platform fields on the stored row, not only the enrichment context.")]
    public async Task spotify_patch_applying_mock_updates_episode_state_via_real_applicator()
    {
        // Arrange
        var spotifyEnricher = PodcastServicesEpisodeEnricherTestSupport.CreateSpotifyEnricherMockApplyingPatch(_fixture);
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();
        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var episode = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithDuration(_fixture.CreateDuration()));
        episode.PodcastId = podcast.Id;
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;

        // Act
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Assert
        results.UpdatedEpisodes.Should().ContainSingle();
        episode.SpotifyId.Should().NotBeNullOrWhiteSpace();
        episode.Urls.Spotify.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When both Spotify and Apple links are missing, indexing enriches Spotify first then Apple " +
        "and the episode retains both platform fields without cross-overwrite.")]
    public async Task multi_platform_enrich_applies_spotify_then_apple_without_cross_overwrite()
    {
        // Arrange
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var appleInput = _fixture.CreateAppleCatalogueInput();
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();
        var applicator = EpisodeDomainTestServices.CreateEnrichmentApplicator();
        var spotifyAdapter = new SpotifyEpisodeAdapter();
        var appleAdapter = new AppleEpisodeAdapter();

        spotifyEnricher
            .Setup(x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()))
            .Callback<EnrichmentRequest, IndexingContext, EnrichmentContext>((request, _, context) =>
            {
                applicator.Apply(request.Podcast, request.Episode, spotifyAdapter.Adapt(spotifyInput)).ApplyTo(context);
            })
            .Returns(Task.CompletedTask);

        appleEnricher
            .Setup(x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()))
            .Callback<EnrichmentRequest, IndexingContext, EnrichmentContext>((request, _, context) =>
            {
                applicator.Apply(request.Podcast, request.Episode, appleAdapter.Adapt(appleInput)).ApplyTo(context);
            })
            .Returns(Task.CompletedTask);

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AppleId = _fixture.CreateAppleId();
        var episode = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithDuration(_fixture.CreateDuration()));
        episode.PodcastId = podcast.Id;
        episode.SpotifyId = string.Empty;
        episode.AppleId = null;
        episode.Urls.Spotify = null;
        episode.Urls.Apple = null;
        var expected = EpisodeExpectation.From(episode)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl, spotifyInput.Image)
            .WithApple(appleInput.AppleId, appleInput.AppleUrl, appleInput.Image);

        // Act
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Assert
        results.UpdatedEpisodes.Should().ContainSingle();
        episode.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When the Apple enricher mock applies a real domain patch, indexing updates the episode's " +
        "Apple platform fields on the stored row, not only the enrichment context.")]
    public async Task apple_patch_applying_mock_updates_episode_state_via_real_applicator()
    {
        // Arrange
        var appleEnricher = PodcastServicesEpisodeEnricherTestSupport.CreateAppleEnricherMockApplyingPatch(_fixture);
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();
        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AppleId = _fixture.CreateAppleId();
        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithDuration(_fixture.CreateDuration()));
        episode.PodcastId = podcast.Id;
        episode.AppleId = null;
        episode.Urls.Apple = null;

        // Act
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Assert
        results.UpdatedEpisodes.Should().ContainSingle();
        episode.AppleId.Should().NotBeNull();
        episode.Urls.Apple.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When the YouTube enricher mock applies a real domain patch, indexing updates the episode's " +
        "YouTube platform fields on the stored row, not only the enrichment context.")]
    public async Task youtube_patch_applying_mock_updates_episode_state_via_real_applicator()
    {
        // Arrange
        var youTubeEnricher = PodcastServicesEpisodeEnricherTestSupport.CreateYouTubeEnricherMockApplyingPatch(_fixture);
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithDuration(_fixture.CreateDuration()));
        episode.PodcastId = podcast.Id;
        episode.YouTubeId = string.Empty;
        episode.Urls.YouTube = null;

        // Act
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Assert
        results.UpdatedEpisodes.Should().ContainSingle();
        episode.YouTubeId.Should().NotBeNullOrWhiteSpace();
        episode.Urls.YouTube.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When Spotify, Apple, and YouTube links are all missing, indexing enriches in platform order " +
        "and the episode retains all three platform fields without cross-overwrite.")]
    public async Task multi_platform_enrich_applies_spotify_apple_youtube_without_cross_overwrite()
    {
        // Arrange
        var spotifyEnricher = PodcastServicesEpisodeEnricherTestSupport.CreateSpotifyEnricherMockApplyingPatch(_fixture);
        var appleEnricher = PodcastServicesEpisodeEnricherTestSupport.CreateAppleEnricherMockApplyingPatch(_fixture);
        var youTubeEnricher = PodcastServicesEpisodeEnricherTestSupport.CreateYouTubeEnricherMockApplyingPatch(_fixture);
        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AppleId = _fixture.CreateAppleId();
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Length = _fixture.CreateDuration();
            e.YouTubeId = string.Empty;
            e.SpotifyId = string.Empty;
            e.AppleId = null;
            e.Urls = new ServiceUrls();
        });
        episode.PodcastId = podcast.Id;

        // Act
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Assert
        results.UpdatedEpisodes.Should().ContainSingle();
        episode.SpotifyId.Should().NotBeNullOrWhiteSpace();
        episode.AppleId.Should().NotBeNull();
        episode.YouTubeId.Should().NotBeNullOrWhiteSpace();
        episode.Urls.Spotify.Should().NotBeNull();
        episode.Urls.Apple.Should().NotBeNull();
        episode.Urls.YouTube.Should().NotBeNull();
    }

    private Episode CreateStoredEpisodeAwaitingYouTubeEnrichment(string audioPlatform, DateTime release)
    {
        var duration = _fixture.CreateDuration();
        var episode = audioPlatform switch
        {
            "Apple" => _fixture.CreateAppleCatalogueEpisode(b => b
                .WithRelease(release)
                .WithDuration(duration)),
            "Spotify" => _fixture.CreateSpotifyCatalogueEpisode(b => b
                .WithRelease(release)
                .WithDuration(duration)),
            _ => throw new ArgumentOutOfRangeException(nameof(audioPlatform), audioPlatform, null)
        };

        episode.YouTubeId = string.Empty;
        episode.Urls.YouTube = null;
        return episode;
    }

    private static DateTime ReleaseRecentlyExpiredDelayedPublishing(
        string audioPlatform,
        TimeSpan publishingDelay) =>
        audioPlatform switch
        {
            "Apple" => DomainTestFixture.AudioReleaseRecentlyExpiredDelayedPublishing(publishingDelay),
            "Spotify" => DomainTestFixture.SpotifyCatalogueReleaseRecentlyExpiredDelayedPublishing(publishingDelay),
            _ => throw new ArgumentOutOfRangeException(nameof(audioPlatform), audioPlatform, null)
        };

    private static DateTime ReleaseStillInsideDelayedPublishingWindow(
        string audioPlatform,
        TimeSpan publishingDelay) =>
        audioPlatform switch
        {
            "Apple" => DomainTestFixture.AudioReleaseStillInsideDelayedPublishingWindow(publishingDelay),
            "Spotify" => DomainTestFixture.SpotifyCatalogueReleaseStillInsideDelayedPublishingWindow(publishingDelay),
            _ => throw new ArgumentOutOfRangeException(nameof(audioPlatform), audioPlatform, null)
        };
}
