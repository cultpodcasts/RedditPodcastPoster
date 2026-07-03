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
        // Given an episode missing Spotify URL and ID on a Spotify-primary podcast
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

        var podcast = _fixture.SpotifyPrimaryPodcast("show-spotify-enrich");
        var episode = _fixture.FromYouTubeVideo(
            "yt-missing-spotify",
            "Episode missing Spotify",
            DateTime.UtcNow.AddDays(-7),
            TimeSpan.FromMinutes(45));
        episode.PodcastId = podcast.Id;
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;

        // When indexing enrichment runs for the episode
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Then the Spotify enricher is invoked and the episode is reported as updated
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
        // Given an episode missing Apple URL and ID on a podcast with Apple configured
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

        var podcast = _fixture.SpotifyPrimaryPodcast("show-apple-enrich");
        podcast.AppleId = 1234567890;

        var episode = _fixture.FromSpotifyCatalogue(
            "spotify-missing-apple",
            "Episode missing Apple",
            new Uri("https://open.spotify.com/episode/spotify-missing-apple"),
            DateTime.UtcNow.AddDays(-7),
            TimeSpan.FromMinutes(45));
        episode.PodcastId = podcast.Id;
        episode.AppleId = null;
        episode.Urls.Apple = null;

        // When indexing enrichment runs for the episode
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Then the Apple enricher is invoked and the episode is reported as updated
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
        // Given an episode missing YouTube URL and ID on a podcast with a YouTube channel
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
                PodcastServicesEpisodeEnricherTestSupport.FillYouTubeLink(context, "filled-youtube-id");
            })
            .Returns(Task.CompletedTask);

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.SpotifyPrimaryPodcast("show-youtube-enrich");
        podcast.YouTubeChannelId = "channel-youtube-enrich";

        var episode = _fixture.FromSpotifyCatalogue(
            "spotify-missing-youtube",
            "Episode missing YouTube",
            new Uri("https://open.spotify.com/episode/spotify-missing-youtube"),
            DateTime.UtcNow.AddDays(-7),
            TimeSpan.FromMinutes(45));
        episode.PodcastId = podcast.Id;
        episode.YouTubeId = string.Empty;
        episode.Urls.YouTube = null;

        // When indexing enrichment runs for the episode
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Then the YouTube enricher is invoked and the episode is reported as updated
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
        // Given a podcast configured to skip YouTube enrichment with a YouTube channel
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();

        var enricher = PodcastServicesEpisodeEnricherTestSupport.CreateEnricher(
            spotifyEnricher,
            appleEnricher,
            youTubeEnricher);

        var podcast = _fixture.SpotifyPrimaryPodcast("show-skip-youtube");
        podcast.YouTubeChannelId = "channel-skip-youtube";
        podcast.SkipEnrichingFromYouTube = true;

        var episode = _fixture.FromSpotifyCatalogue(
            "spotify-only-1",
            "Episode missing YouTube",
            new Uri("https://open.spotify.com/episode/spotify-only-1"),
            DateTime.UtcNow.AddDays(-7),
            TimeSpan.FromMinutes(45));
        episode.PodcastId = podcast.Id;
        episode.YouTubeId = string.Empty;
        episode.Urls.YouTube = null;

        // When indexing enrichment runs for the episode
        var results = await enricher.EnrichEpisodes(
            podcast,
            [episode],
            [episode],
            new IndexingContext());

        // Then the YouTube enricher is never invoked
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
        // Given a stored episode whose delayed-publishing window recently expired and is missing YouTube
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
                PodcastServicesEpisodeEnricherTestSupport.FillYouTubeLink(context, "delayed-pass-youtube");
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

        var expiredStoredEpisode = _fixture.FromSpotifyCatalogue(
            "expired-delayed-spot",
            "Recently expired delayed episode",
            new Uri("https://open.spotify.com/episode/expired-delayed-spot"),
            DateTime.UtcNow.Subtract(PublishingDelay).AddHours(-2),
            TimeSpan.FromMinutes(45));
        expiredStoredEpisode.PodcastId = podcast.Id;
        expiredStoredEpisode.YouTubeId = string.Empty;
        expiredStoredEpisode.Urls.YouTube = null;

        var discoveredEpisode = _fixture.FromSpotifyCatalogue(
            "discovered-with-youtube",
            "Discovered episode with YouTube already",
            new Uri("https://open.spotify.com/episode/discovered-with-youtube"),
            DateTime.UtcNow.AddDays(-2),
            TimeSpan.FromMinutes(45),
            "Discovered description");
        discoveredEpisode.PodcastId = podcast.Id;
        discoveredEpisode.YouTubeId = "discovered-yt-1";
        discoveredEpisode.Urls.YouTube = new Uri("https://www.youtube.com/watch?v=discovered-yt-1");

        // When indexing enrichment runs with only the discovered episode in the current batch
        var results = await enricher.EnrichEpisodes(
            podcast,
            [expiredStoredEpisode, discoveredEpisode],
            [discoveredEpisode],
            new IndexingContext());

        // Then the recently expired stored episode is enriched in the delayed-publishing second pass
        enrichedEpisodeIds.Should().ContainSingle().Which.Should().Be(expiredStoredEpisode.Id);
        results.UpdatedEpisodes.Should().ContainSingle();
        results.UpdatedEpisodes.Single().Episode.Id.Should().Be(expiredStoredEpisode.Id);
    }

    [Fact(DisplayName =
        "Episodes in the current discovery batch are excluded from the delayed-publishing second pass.")]
    public async Task episodes_in_new_episodes_batch_are_excluded_from_delayed_publishing_second_pass()
    {
        // Given a recently expired delayed-publishing episode that is also in the current discovery batch
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
                PodcastServicesEpisodeEnricherTestSupport.FillYouTubeLink(context, "single-pass-youtube");
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

        var batchEpisode = _fixture.FromSpotifyCatalogue(
            "batch-delayed-spot",
            "Batch episode awaiting YouTube",
            new Uri("https://open.spotify.com/episode/batch-delayed-spot"),
            DateTime.UtcNow.Subtract(PublishingDelay).AddHours(-2),
            TimeSpan.FromMinutes(45));
        batchEpisode.PodcastId = podcast.Id;
        batchEpisode.YouTubeId = string.Empty;
        batchEpisode.Urls.YouTube = null;

        // When indexing enrichment runs with the episode in both stored and discovery batches
        await enricher.EnrichEpisodes(
            podcast,
            [batchEpisode],
            [batchEpisode],
            new IndexingContext());

        // Then YouTube enrichment runs only once for the discovery batch, not again in the second pass
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
        // Given a stored episode still inside the delayed-publishing window and missing YouTube
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

        var inWindowEpisode = _fixture.FromSpotifyCatalogue(
            "in-window-spot",
            "Episode still awaiting YouTube",
            new Uri("https://open.spotify.com/episode/in-window-spot"),
            DateTime.UtcNow.AddHours(-12),
            TimeSpan.FromMinutes(45));
        inWindowEpisode.PodcastId = podcast.Id;
        inWindowEpisode.YouTubeId = string.Empty;
        inWindowEpisode.Urls.YouTube = null;

        // When indexing enrichment runs without the episode in the discovery batch
        var results = await enricher.EnrichEpisodes(
            podcast,
            [inWindowEpisode],
            [],
            new IndexingContext());

        // Then YouTube enrichment is not attempted while the episode remains inside the window
        youTubeEnricher.Verify(
            x => x.Enrich(
                It.IsAny<EnrichmentRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<EnrichmentContext>()),
            Times.Never);
        results.UpdatedEpisodes.Should().BeEmpty();
    }
}
