using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.YouTube.Enrichment;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Enrichment;

public class IndexingEnrichmentRules
{
    private static readonly TimeSpan PublishingDelay = TimeSpan.FromDays(1);

    private readonly DomainTestFixture _fixture = new();

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
                context.Spotify = new Uri("https://open.spotify.com/episode/filled-by-enricher");
            })
            .Returns(Task.CompletedTask);

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH");
        var episode = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithDuration(TimeSpan.FromMinutes(45)));
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
                context.Apple = new Uri("https://podcasts.apple.com/us/podcast/episode/id9999999999");
            })
            .Returns(Task.CompletedTask);

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH");
        podcast.AppleId = _fixture.CreateAppleId();

        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(TimeSpan.FromMinutes(45)));
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

        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH");
        podcast.YouTubeChannelId = "UCchannel123456789";

        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(TimeSpan.FromMinutes(45)));
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

        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH");
        podcast.YouTubeChannelId = "UCchannel123456789";
        podcast.SkipEnrichingFromYouTube = true;

        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(TimeSpan.FromMinutes(45)));
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

    [Fact(DisplayName =
        "For podcasts with positive YouTube publishing delay, a second pass enriches recently expired delayed-publishing episodes that were not part of the current discovery batch.")]
    public async Task delayed_publishing_second_pass_enriches_recently_expired_stored_episodes()
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
            "show-delayed-pass",
            "channel-delayed-pass",
            PublishingDelay);

        var expiredStoredEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(DateTime.UtcNow.Subtract(PublishingDelay).AddHours(-2))
            .WithDuration(TimeSpan.FromMinutes(45)));
        expiredStoredEpisode.PodcastId = podcast.Id;
        expiredStoredEpisode.YouTubeId = string.Empty;
        expiredStoredEpisode.Urls.YouTube = null;

        var discoveredYouTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithDuration(TimeSpan.FromMinutes(45)));
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

    [Fact(DisplayName =
        "Episodes in the current discovery batch are excluded from the delayed-publishing second pass.")]
    public async Task episodes_in_new_episodes_batch_are_excluded_from_delayed_publishing_second_pass()
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
            "show-single-pass",
            "channel-single-pass",
            PublishingDelay);

        var batchEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(DateTime.UtcNow.Subtract(PublishingDelay).AddHours(-2))
            .WithDuration(TimeSpan.FromMinutes(45)));
        batchEpisode.PodcastId = podcast.Id;
        batchEpisode.YouTubeId = string.Empty;
        batchEpisode.Urls.YouTube = null;

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

    [Fact(DisplayName =
        "Enrichment skips episodes still inside the delayed-publishing window (not yet due on YouTube).")]
    public async Task enrichment_skips_episodes_still_inside_delayed_publishing_window()
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
            "show-in-window",
            "channel-in-window",
            PublishingDelay);

        var inWindowEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(DateTime.UtcNow.AddHours(-12))
            .WithDuration(TimeSpan.FromMinutes(45)));
        inWindowEpisode.PodcastId = podcast.Id;
        inWindowEpisode.YouTubeId = string.Empty;
        inWindowEpisode.Urls.YouTube = null;

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
}
