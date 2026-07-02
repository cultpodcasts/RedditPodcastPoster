using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.YouTube.Enrichment;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Enrichment;

public class IndexingEnrichmentRules
{
    [Fact(DisplayName =
        "When Spotify URL or ID is missing, indexing attempts Spotify enrichment.")]
    public async Task spotify_enricher_is_invoked_when_spotify_link_is_missing()
    {
        // Given an episode missing Spotify URL and ID on a Spotify-primary podcast
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();
        var episodeFilter = new Mock<IPodcastEpisodeFilter>();

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

        var enricher = new PodcastServicesEpisodeEnricher(
            appleEnricher.Object,
            spotifyEnricher.Object,
            youTubeEnricher.Object,
            episodeFilter.Object,
            NullLogger<PodcastServicesEpisodeEnricher>.Instance);

        var podcast = PodcastFixtures.SpotifyPrimary("show-spotify-enrich");
        var episode = EpisodeFixtures.FromYouTubeVideo(
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
        "When SkipEnrichingFromYouTube is true, YouTube enrichment is not attempted.")]
    public async Task youtube_enricher_is_skipped_when_skip_enriching_from_youtube_is_true()
    {
        // Given a podcast configured to skip YouTube enrichment with a YouTube channel
        var spotifyEnricher = new Mock<ISpotifyEpisodeEnricher>();
        var appleEnricher = new Mock<IAppleEpisodeEnricher>();
        var youTubeEnricher = new Mock<IYouTubeEpisodeEnricher>();
        var episodeFilter = new Mock<IPodcastEpisodeFilter>();

        var enricher = new PodcastServicesEpisodeEnricher(
            appleEnricher.Object,
            spotifyEnricher.Object,
            youTubeEnricher.Object,
            episodeFilter.Object,
            NullLogger<PodcastServicesEpisodeEnricher>.Instance);

        var podcast = PodcastFixtures.SpotifyPrimary("show-skip-youtube");
        podcast.YouTubeChannelId = "channel-skip-youtube";
        podcast.SkipEnrichingFromYouTube = true;

        var episode = EpisodeFixtures.FromSpotifyCatalogue(
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
}
