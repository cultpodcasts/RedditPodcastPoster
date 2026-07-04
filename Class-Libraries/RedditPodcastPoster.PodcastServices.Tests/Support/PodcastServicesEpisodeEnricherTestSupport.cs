using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.YouTube.Enrichment;

namespace RedditPodcastPoster.PodcastServices.Tests.Support;

internal static class PodcastServicesEpisodeEnricherTestSupport
{
    public static PodcastServicesEpisodeEnricher CreateEnricher(
        Mock<ISpotifyEpisodeEnricher> spotifyEnricher,
        Mock<IAppleEpisodeEnricher> appleEnricher,
        Mock<IYouTubeEpisodeEnricher> youTubeEnricher,
        TimeSpan? delayedPublicationEvaluationThreshold = null)
    {
        var episodeFilter = new PodcastEpisodeFilter(
            Options.Create(new DelayedYouTubePublication
            {
                EvaluationThreshold = delayedPublicationEvaluationThreshold ?? TimeSpan.FromDays(7)
            }),
            NullLogger<PodcastEpisodeFilter>.Instance);

        return new PodcastServicesEpisodeEnricher(
            appleEnricher.Object,
            spotifyEnricher.Object,
            youTubeEnricher.Object,
            episodeFilter,
            NullLogger<PodcastServicesEpisodeEnricher>.Instance);
    }

    public static Podcast CreateDelayedPublishingPodcast(
        string spotifyShowId,
        string youTubeChannelId,
        TimeSpan publishingDelay) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Delayed-publishing podcast",
            SpotifyId = spotifyShowId,
            ReleaseAuthority = Service.Spotify,
            YouTubeChannelId = youTubeChannelId,
            YouTubePublicationOffset = publishingDelay.Ticks,
            SkipEnrichingFromYouTube = null
        };

    public static void FillYouTubeLink(EnrichmentContext enrichmentContext, string youTubeId)
    {
        enrichmentContext.YouTube = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        enrichmentContext.YouTubeId = youTubeId;
    }
}
